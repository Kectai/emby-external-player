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
    public void Issue_EnforcesBoundedCapacity()
    {
        var store = new LaunchTicketStore(new FakeClock(), 1);
        store.Issue(CreatePayload(), TimeSpan.FromMinutes(30));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => store.Issue(CreatePayload(), TimeSpan.FromMinutes(30)));
    }

    private static LaunchTicketPayload CreatePayload()
    {
        return new LaunchTicketPayload
        {
            UserId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            MediaSourceId = "source-1",
            UpstreamUrl = "https://media.example/video.mkv",
            AccessToken = "must-remain-server-side",
        };
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);
    }
}
