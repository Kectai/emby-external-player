using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class LaunchTicketStoreTests
{
    [TestMethod]
    public void Issue_ReturnsUrlSafeRandomTicket_AndStoresOnlyHash()
    {
        var clock = new FakeClock();
        var store = new LaunchTicketStore(clock);
        var payload = CreatePayload();

        var ticket = store.Issue(payload, TimeSpan.FromMinutes(30));

        Assert.IsTrue(Regex.IsMatch(ticket.Value, "^[A-Za-z0-9_-]{43}$"));
        Assert.IsTrue(store.TryGet(ticket.Value, out var restored));
        Assert.AreSame(payload, restored);

        var field = typeof(LaunchTicketStore).GetField("tickets", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var dictionary = (IEnumerable?)field.GetValue(store);
        Assert.IsNotNull(dictionary);
        foreach (var pair in dictionary)
        {
            var key = pair!.GetType().GetProperty("Key")!.GetValue(pair) as string;
            Assert.AreNotEqual(ticket.Value, key, "Raw tickets must never be dictionary keys.");
        }
    }

    [TestMethod]
    public void TryGet_RejectsExpiredTicketAndRemovesIt()
    {
        var clock = new FakeClock();
        var store = new LaunchTicketStore(clock);
        var ticket = store.Issue(CreatePayload(), TimeSpan.FromMinutes(30));

        clock.UtcNow = clock.UtcNow.AddMinutes(31);

        Assert.IsFalse(store.TryGet(ticket.Value, out _));
        Assert.AreEqual(0, store.Count);
    }

    [TestMethod]
    public void Issue_EnforcesLifetimeBounds()
    {
        var store = new LaunchTicketStore(new FakeClock());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => store.Issue(CreatePayload(), TimeSpan.FromMinutes(29)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => store.Issue(CreatePayload(), TimeSpan.FromMinutes(721)));
    }

    [TestMethod]
    public void CreateLifetime_RejectsTamperedConfigurationBeforeTimeSpanConversion()
    {
        Assert.AreEqual(
            TimeSpan.FromMinutes(LaunchTicketStore.DefaultLifetimeMinutes),
            LaunchTicketStore.CreateLifetime(LaunchTicketStore.DefaultLifetimeMinutes));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LaunchTicketStore.CreateLifetime(29));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LaunchTicketStore.CreateLifetime(721));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LaunchTicketStore.CreateLifetime(int.MaxValue));
    }

    [TestMethod]
    public void Issue_EvictsOldestTicketAtBoundedCapacity()
    {
        var clock = new FakeClock();
        var store = new LaunchTicketStore(clock, 1);
        var userId = Guid.NewGuid();
        var oldest = store.Issue(CreatePayload(userId), TimeSpan.FromMinutes(30));
        clock.UtcNow = clock.UtcNow.AddSeconds(1);

        var newest = store.Issue(CreatePayload(userId), TimeSpan.FromMinutes(30));

        Assert.IsFalse(store.TryGet(oldest.Value, out _));
        Assert.IsTrue(store.TryGet(newest.Value, out _));
        Assert.AreEqual(1, store.Count);
    }

    [TestMethod]
    public void Issue_RecyclesOnlyTheIssuingUsersOldestTicket()
    {
        var clock = new FakeClock();
        var store = new LaunchTicketStore(clock, capacity: 3, perUserCapacity: 2);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var first = store.Issue(CreatePayload(firstUser), TimeSpan.FromMinutes(30));
        var other = store.Issue(CreatePayload(secondUser), TimeSpan.FromMinutes(30));
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        store.Issue(CreatePayload(firstUser), TimeSpan.FromMinutes(30));
        clock.UtcNow = clock.UtcNow.AddSeconds(1);

        store.Issue(CreatePayload(firstUser), TimeSpan.FromMinutes(30));

        Assert.IsFalse(store.TryGet(first.Value, out _));
        Assert.IsTrue(store.TryGet(other.Value, out _), "one user must not evict another user's ticket");
    }

    [TestMethod]
    public void Issue_RejectsCrossUserEvictionWhenGlobalCapacityIsFull()
    {
        var store = new LaunchTicketStore(new FakeClock(), capacity: 1, perUserCapacity: 1);
        var existing = store.Issue(CreatePayload(Guid.NewGuid()), TimeSpan.FromMinutes(30));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            store.Issue(CreatePayload(Guid.NewGuid()), TimeSpan.FromMinutes(30)));
        Assert.IsTrue(store.TryGet(existing.Value, out _));
    }

    [TestMethod]
    public void IssueBatch_ReservesMediaAndSubtitleTicketsTogether()
    {
        var userId = Guid.NewGuid();
        var store = new LaunchTicketStore(new FakeClock(), capacity: 2, perUserCapacity: 2);

        var tickets = store.IssueBatch(
            new[] { CreatePayload(userId), CreatePayload(userId) },
            TimeSpan.FromMinutes(30));

        Assert.AreEqual(2, tickets.Count);
        Assert.IsTrue(store.TryGet(tickets[0].Value, out _));
        Assert.IsTrue(store.TryGet(tickets[1].Value, out _));
    }

    [TestMethod]
    public void IssueBatch_DoesNotPartiallyIssueWhenCapacityBelongsToAnotherUser()
    {
        var store = new LaunchTicketStore(new FakeClock(), capacity: 2, perUserCapacity: 2);
        var existing = store.Issue(CreatePayload(Guid.NewGuid()), TimeSpan.FromMinutes(30));
        var userId = Guid.NewGuid();

        Assert.ThrowsExactly<InvalidOperationException>(() => store.IssueBatch(
            new[] { CreatePayload(userId), CreatePayload(userId) },
            TimeSpan.FromMinutes(30)));
        Assert.AreEqual(1, store.Count);
        Assert.IsTrue(store.TryGet(existing.Value, out _));
    }

    [TestMethod]
    public void IssueBatch_RejectsMixedUsers()
    {
        var store = new LaunchTicketStore(new FakeClock());

        Assert.ThrowsExactly<ArgumentException>(() => store.IssueBatch(
            new[] { CreatePayload(Guid.NewGuid()), CreatePayload(Guid.NewGuid()) },
            TimeSpan.FromMinutes(30)));
        Assert.AreEqual(0, store.Count);
    }

    [TestMethod]
    public void TryGet_AllowsConcurrentRangeStyleReads()
    {
        var store = new LaunchTicketStore(new FakeClock());
        var payload = CreatePayload();
        var ticket = store.Issue(payload, TimeSpan.FromMinutes(30));
        var failures = 0;

        Parallel.For(0, 500, _ =>
        {
            if (!store.TryGet(ticket.Value, out var restored) || !ReferenceEquals(payload, restored))
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.AreEqual(0, failures);
    }

    [TestMethod]
    public void NewRuntimeStore_RejectsTicketIssuedBeforeRestart()
    {
        var clock = new FakeClock();
        var ticket = new LaunchTicketStore(clock)
            .Issue(CreatePayload(), TimeSpan.FromMinutes(30));

        var restartedStore = new LaunchTicketStore(clock);

        Assert.IsFalse(restartedStore.TryGet(ticket.Value, out _));
    }

    [TestMethod]
    public void Revoke_InvalidatesTicket()
    {
        var store = new LaunchTicketStore(new FakeClock());
        var ticket = store.Issue(CreatePayload(), TimeSpan.FromMinutes(30));

        Assert.IsTrue(store.Revoke(ticket.Value));
        Assert.IsFalse(store.TryGet(ticket.Value, out _));
    }

    [TestMethod]
    public void Clear_RevokesEveryTicket()
    {
        var store = new LaunchTicketStore(new FakeClock());
        var first = store.Issue(CreatePayload(), TimeSpan.FromMinutes(30));
        var second = store.Issue(CreatePayload(), TimeSpan.FromMinutes(30));

        Assert.AreEqual(2, store.Clear());
        Assert.IsFalse(store.TryGet(first.Value, out _));
        Assert.IsFalse(store.TryGet(second.Value, out _));
    }

    [TestMethod]
    public void TryGet_RejectsMalformedTicketsWithoutHashingThem()
    {
        var store = new LaunchTicketStore(new FakeClock());

        Assert.IsFalse(store.TryGet(new string('a', 10000), out _));
        Assert.IsFalse(store.TryGet("not-a-ticket", out _));
    }

    private static LaunchTicketPayload CreatePayload(Guid? userId = null)
    {
        return new LaunchTicketPayload
        {
            UserId = userId ?? Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            MediaSourceId = "source-1",
            FilePath = "/library/video.mkv",
            ContentLength = 1024,
        };
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
    }
}
