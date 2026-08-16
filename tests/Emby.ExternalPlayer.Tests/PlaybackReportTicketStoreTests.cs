using System.Collections;
using System.Reflection;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PlaybackReportTicketStoreTests
{
    [TestMethod]
    public void IssueDoesNotTreatUnstartedTicketsAsActiveSessions()
    {
        var clock = new FakeClock();
        var store = new PlaybackReportTicketStore(
            clock,
            activeCapacity: 1,
            perUserActiveCapacity: 1,
            totalCapacity: 2);
        var first = store.Issue(CreateGrant(Guid.NewGuid()), TimeSpan.FromMinutes(30));
        var second = store.Issue(CreateGrant(Guid.NewGuid()), TimeSpan.FromMinutes(30));

        Assert.AreEqual(PlaybackTicketLookupStatus.Found, store.TryGet(first.Value, out _));
        Assert.AreEqual(PlaybackTicketLookupStatus.Found, store.TryGet(second.Value, out _));
        Assert.AreEqual(0, store.ActiveCount);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            store.Issue(CreateGrant(Guid.NewGuid()), TimeSpan.FromMinutes(30)));
    }

    [TestMethod]
    public void IssueAllowsMultipleUnstartedTicketsForTheSameUser()
    {
        var userId = Guid.NewGuid();
        var store = new PlaybackReportTicketStore(
            new FakeClock(),
            activeCapacity: 3,
            perUserActiveCapacity: 1,
            totalCapacity: 3);
        store.Issue(CreateGrant(userId), TimeSpan.FromMinutes(30));
        store.Issue(CreateGrant(userId), TimeSpan.FromMinutes(30));
        store.Issue(CreateGrant(userId), TimeSpan.FromMinutes(30));

        Assert.AreEqual(3, store.Count);
        Assert.AreEqual(0, store.ActiveCount);
    }

    [TestMethod]
    public void NewStoreRejectsTicketFromBeforeRestart()
    {
        var clock = new FakeClock();
        var ticket = new PlaybackReportTicketStore(clock)
            .Issue(CreateGrant(Guid.NewGuid()), TimeSpan.FromMinutes(30));

        Assert.AreEqual(
            PlaybackTicketLookupStatus.Invalid,
            new PlaybackReportTicketStore(clock).TryGet(ticket.Value, out _));
    }

    [TestMethod]
    public void RevokingAnUnstartedTicketAlsoReleasesItsUnusedLease()
    {
        var store = new PlaybackReportTicketStore(new FakeClock());
        var ticket = store.Issue(CreateGrant(Guid.NewGuid()), TimeSpan.FromMinutes(30));

        Assert.IsTrue(store.Revoke(ticket.Value));
        var leasesField = typeof(PlaybackReportTicketStore).GetField(
            "leases",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var leases = (ICollection?)leasesField?.GetValue(store);
        Assert.IsNotNull(leases);
        Assert.AreEqual(0, leases.Count);
    }

    private static PlaybackReportGrant CreateGrant(Guid userId) => new()
    {
        UserId = userId,
        ItemId = Guid.NewGuid(),
        CanonicalItemId = Guid.NewGuid(),
        MediaSourceId = "source",
        RunTimeTicks = TimeSpan.FromHours(1).Ticks,
        PlayerName = "Test Player",
    };

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
    }
}
