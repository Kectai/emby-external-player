using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PlaybackReportCoordinatorTests
{
    [TestMethod]
    public void Issue_UsesIndependentUrlSafeTicketAndStoresOnlyItsHash()
    {
        var harness = new Harness();
        var ticket = harness.Issue();

        Assert.IsTrue(Regex.IsMatch(ticket.Value, "^[A-Za-z0-9_-]{43}$"));
        Assert.IsTrue(Regex.IsMatch(ticket.LaunchId, "^[a-f0-9]{32}$"));
        Assert.AreNotEqual(ticket.Value, ticket.LaunchId);

        var field = typeof(PlaybackReportTicketStore).GetField(
            "tickets",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var dictionary = (IEnumerable?)field?.GetValue(harness.Store);
        Assert.IsNotNull(dictionary);
        foreach (var pair in dictionary)
        {
            var key = pair!.GetType().GetProperty("Key")!.GetValue(pair) as string;
            Assert.AreNotEqual(ticket.Value, key);
        }
    }

    [TestMethod]
    public async Task Lifecycle_IsIdempotentAndUsesEmbyBridgeOnlyOncePerSequence()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = harness.Request(ticket, sequence: 1, positionTicks: 20_000_000);

        var startAck = await harness.Coordinator.StartAsync(ticket.Value, start);
        var duplicateStart = await harness.Coordinator.StartAsync(ticket.Value, start);
        Assert.AreEqual(200, startAck.StatusCode);
        Assert.AreEqual(startAck.Response.OwnerRevision, duplicateStart.Response.OwnerRevision);
        Assert.AreEqual(1, harness.Bridge.StartCount);

        var progress = harness.Request(
            ticket,
            sequence: 2,
            positionTicks: 80_000_000,
            ownerRevision: startAck.Response.OwnerRevision);
        var progressAck = await harness.Coordinator.ProgressAsync(ticket.Value, progress);
        var duplicateProgress = await harness.Coordinator.ProgressAsync(ticket.Value, progress);
        Assert.IsTrue(progressAck.Response.Accepted);
        Assert.IsTrue(duplicateProgress.Response.Accepted);
        Assert.AreEqual(1, harness.Bridge.ProgressCount);

        var stop = harness.Request(
            ticket,
            sequence: 3,
            positionTicks: 90_000_000,
            ownerRevision: startAck.Response.OwnerRevision,
            endReason: "windowClosed");
        var stopAck = await harness.Coordinator.StopAsync(ticket.Value, stop);
        var duplicateStop = await harness.Coordinator.StopAsync(ticket.Value, stop);
        Assert.IsTrue(stopAck.Response.Accepted);
        Assert.IsTrue(stopAck.Response.Terminal);
        Assert.IsTrue(duplicateStop.Response.Accepted);
        Assert.AreEqual(1, harness.Bridge.StopCount);
    }

    [TestMethod]
    public async Task TerminalZeroCannotOverwriteTheLastAcceptedNonzeroPosition()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, sequence: 1, positionTicks: 20_000_000));
        await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(
                ticket,
                sequence: 2,
                positionTicks: 80_000_000,
                ownerRevision: start.Response.OwnerRevision));

        var stop = await harness.Coordinator.StopAsync(
            ticket.Value,
            harness.Request(
                ticket,
                sequence: 3,
                positionTicks: 0,
                ownerRevision: start.Response.OwnerRevision,
                endReason: "windowClosed"));

        Assert.IsTrue(stop.Response.Accepted);
        Assert.AreEqual(80_000_000, harness.Bridge.LastStopPositionTicks);
    }

    [TestMethod]
    public async Task NewerLaunchSupersedesOlderOwnerAndRejectsItsLateProgressAndStop()
    {
        var harness = new Harness();
        var older = harness.Issue();
        var newer = harness.Issue();
        var olderAck = await harness.Coordinator.StartAsync(
            older.Value,
            harness.Request(older, 1, 10_000_000));
        var newerAck = await harness.Coordinator.StartAsync(
            newer.Value,
            harness.Request(newer, 1, 5_000_000));

        Assert.AreEqual(2, harness.Bridge.StartCount);
        Assert.AreEqual(1, harness.Bridge.StopCount, "the old Emby session must be stopped during transfer");
        Assert.IsTrue(newerAck.Response.OwnerRevision > olderAck.Response.OwnerRevision);

        var oldProgress = await harness.Coordinator.ProgressAsync(
            older.Value,
            harness.Request(older, 2, 99_000_000, olderAck.Response.OwnerRevision));
        var oldStop = await harness.Coordinator.StopAsync(
            older.Value,
            harness.Request(older, 3, 100_000_000, olderAck.Response.OwnerRevision, "shutdown"));
        Assert.AreEqual("superseded", oldProgress.Response.Reason);
        Assert.AreEqual("superseded", oldStop.Response.Reason);
        Assert.AreEqual(newerAck.Response.OwnerRevision, oldProgress.Response.OwnerRevision);
        Assert.IsFalse(oldProgress.Response.Accepted);
        Assert.AreEqual(0, harness.Bridge.ProgressCount);
        Assert.AreEqual(1, harness.Bridge.StopCount, "late old Stop must not touch the new lease");

        var newProgress = await harness.Coordinator.ProgressAsync(
            newer.Value,
            harness.Request(newer, 2, 20_000_000, newerAck.Response.OwnerRevision));
        Assert.IsTrue(newProgress.Response.Accepted);
        Assert.AreEqual(1, harness.Bridge.ProgressCount);
    }

    [TestMethod]
    public async Task OlderStartArrivingAfterNewerStartCannotTakeOwnership()
    {
        var harness = new Harness();
        var older = harness.Issue();
        var newer = harness.Issue();

        var newerAck = await harness.Coordinator.StartAsync(
            newer.Value,
            harness.Request(newer, 1, 10_000_000));
        var olderResult = await harness.Coordinator.StartAsync(
            older.Value,
            harness.Request(older, 1, 20_000_000));

        Assert.IsTrue(newerAck.Response.Accepted);
        Assert.AreEqual("superseded", olderResult.Response.Reason);
        Assert.AreEqual(1, harness.Bridge.StartCount);
    }

    [TestMethod]
    public async Task LeasesAreIsolatedAcrossUsersAndCanonicalItems()
    {
        var harness = new Harness();
        var first = harness.Issue();
        var otherUser = harness.Issue(userId: Guid.NewGuid());
        var otherItem = harness.Issue(itemId: Guid.NewGuid());

        await harness.Coordinator.StartAsync(first.Value, harness.Request(first, 1, 0));
        await harness.Coordinator.StartAsync(otherUser.Value, harness.Request(otherUser, 1, 0));
        await harness.Coordinator.StartAsync(otherItem.Value, harness.Request(otherItem, 1, 0));

        Assert.AreEqual(3, harness.Bridge.StartCount);
        Assert.AreEqual(0, harness.Bridge.StopCount);
    }

    [TestMethod]
    public async Task WatchdogMakesSessionDormantAndHigherEpochCanResumeIt()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var startAck = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 10_000_000));
        Assert.AreEqual(1, harness.Store.ActiveCount);
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(91);

        Assert.AreEqual(1, await harness.Coordinator.ScanInactiveAsync());
        Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        Assert.AreEqual(0, harness.Store.ActiveCount);
        var staleProgress = await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(ticket, 2, 20_000_000, startAck.Response.OwnerRevision));
        Assert.AreEqual(409, staleProgress.StatusCode);

        var resume = harness.Request(ticket, 1, 20_000_000);
        resume.Epoch = 2;
        var resumed = await harness.Coordinator.StartAsync(ticket.Value, resume);
        Assert.IsTrue(resumed.Response.Accepted);
        Assert.AreEqual(2, resumed.Response.Epoch);
        Assert.IsTrue(resumed.Response.OwnerRevision > startAck.Response.OwnerRevision);
        Assert.AreEqual(2, harness.Bridge.StartCount);
        Assert.AreEqual(1, harness.Store.ActiveCount);
    }

    [TestMethod]
    public async Task DormantLaunchCannotResumeAfterANewerLaunchStarts()
    {
        var harness = new Harness();
        var older = harness.Issue();
        await harness.Coordinator.StartAsync(
            older.Value,
            harness.Request(older, 1, 10_000_000));
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(91);
        await harness.Coordinator.ScanInactiveAsync();

        var newer = harness.Issue();
        await harness.Coordinator.StartAsync(
            newer.Value,
            harness.Request(newer, 1, 20_000_000));
        var staleResume = harness.Request(older, 1, 30_000_000);
        staleResume.Epoch = 2;
        var result = await harness.Coordinator.StartAsync(older.Value, staleResume);

        Assert.AreEqual("superseded", result.Response.Reason);
        Assert.IsTrue(result.Response.Terminal);
        Assert.AreEqual(2, harness.Bridge.StartCount);
    }

    [TestMethod]
    public async Task ValidationRejectsWrongLaunchOwnerAndPositionWithoutCallingBridge()
    {
        var harness = new Harness();
        var ticket = harness.Issue(runTimeTicks: TimeSpan.FromMinutes(10).Ticks);
        var wrongLaunch = harness.Request(ticket, 1, 0);
        wrongLaunch.LaunchId = Guid.NewGuid().ToString("N");
        Assert.AreEqual(401, (await harness.Coordinator.StartAsync(ticket.Value, wrongLaunch)).StatusCode);

        var unexpectedOwner = harness.Request(ticket, 1, 0, ownerRevision: 9);
        Assert.AreEqual(400, (await harness.Coordinator.StartAsync(ticket.Value, unexpectedOwner)).StatusCode);

        var excessivePosition = harness.Request(ticket, 1, TimeSpan.FromHours(1).Ticks);
        Assert.AreEqual(400, (await harness.Coordinator.StartAsync(ticket.Value, excessivePosition)).StatusCode);
        Assert.AreEqual(0, harness.Bridge.StartCount);
    }

    [TestMethod]
    public async Task ClientRunTimeCompletesAFreshStrmPlaybackSession()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var runTimeTicks = TimeSpan.FromMinutes(42).Ticks;

        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 10_000_000, runTimeTicks: runTimeTicks));
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastStartRunTimeTicks);

        await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(
                ticket,
                2,
                20_000_000,
                start.Response.OwnerRevision,
                runTimeTicks: runTimeTicks));
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastProgressRunTimeTicks);

        await harness.Coordinator.StopAsync(
            ticket.Value,
            harness.Request(
                ticket,
                3,
                30_000_000,
                start.Response.OwnerRevision,
                "windowClosed"));
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastStopRunTimeTicks);
    }

    [TestMethod]
    public async Task FirstValidRunTimeCanArriveAfterPlaybackStart()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 10_000_000));
        Assert.AreEqual(0, harness.Bridge.LastStartRunTimeTicks);

        var runTimeTicks = TimeSpan.FromMinutes(55).Ticks;
        await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(
                ticket,
                2,
                20_000_000,
                start.Response.OwnerRevision,
                runTimeTicks: runTimeTicks));
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastProgressRunTimeTicks);

        await harness.Coordinator.StopAsync(
            ticket.Value,
            harness.Request(
                ticket,
                3,
                30_000_000,
                start.Response.OwnerRevision,
                "shutdown"));
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastStopRunTimeTicks);
    }

    [TestMethod]
    public async Task RunTimeFirstReportedByStopGetsAFinalProgressCheckIn()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 10_000_000));
        var runTimeTicks = TimeSpan.FromMinutes(35).Ticks;

        var stop = await harness.Coordinator.StopAsync(
            ticket.Value,
            harness.Request(
                ticket,
                2,
                0,
                start.Response.OwnerRevision,
                "windowClosed",
                runTimeTicks));

        Assert.AreEqual(200, stop.StatusCode);
        Assert.AreEqual(1, harness.Bridge.ProgressCount);
        Assert.AreEqual(10_000_000, harness.Bridge.LastProgressPositionTicks);
        Assert.AreEqual(10_000_000, harness.Bridge.LastStopPositionTicks);
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastProgressRunTimeTicks);
        Assert.AreEqual(runTimeTicks, harness.Bridge.LastStopRunTimeTicks);
    }

    [TestMethod]
    public async Task EmbyRunTimeRemainsAuthoritativeAndInvalidClientValuesAreRejected()
    {
        var harness = new Harness();
        var embyRunTimeTicks = TimeSpan.FromMinutes(60).Ticks;
        var ticket = harness.Issue(runTimeTicks: embyRunTimeTicks);
        var clientRunTimeTicks = TimeSpan.FromMinutes(45).Ticks;

        var accepted = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 0, runTimeTicks: clientRunTimeTicks));
        Assert.AreEqual(200, accepted.StatusCode);
        Assert.AreEqual(embyRunTimeTicks, harness.Bridge.LastStartRunTimeTicks);

        var invalidHarness = new Harness();
        var invalidTicket = invalidHarness.Issue();
        var zero = invalidHarness.Request(invalidTicket, 1, 0, runTimeTicks: 0);
        var tooLarge = invalidHarness.Request(
            invalidTicket,
            1,
            0,
            runTimeTicks: PlaybackReportCoordinator.MaximumProtocolInteger + 1);
        var excessivePosition = invalidHarness.Request(
            invalidTicket,
            1,
            TimeSpan.FromHours(1).Ticks,
            runTimeTicks: TimeSpan.FromMinutes(10).Ticks);

        Assert.AreEqual(400, (await invalidHarness.Coordinator.StartAsync(
            invalidTicket.Value,
            zero)).StatusCode);
        Assert.AreEqual(400, (await invalidHarness.Coordinator.StartAsync(
            invalidTicket.Value,
            tooLarge)).StatusCode);
        Assert.AreEqual(400, (await invalidHarness.Coordinator.StartAsync(
            invalidTicket.Value,
            excessivePosition)).StatusCode);
        Assert.AreEqual(0, invalidHarness.Bridge.StartCount);
    }

    [TestMethod]
    public async Task RateLimiterAllowsNormalHeartbeatButRejectsAnImmediateFlood()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(ticket.Value, harness.Request(ticket, 1, 0));
        for (var sequence = 2; sequence <= 4; sequence++)
        {
            var accepted = await harness.Coordinator.ProgressAsync(
                ticket.Value,
                harness.Request(ticket, sequence, sequence * 10_000_000, start.Response.OwnerRevision));
            Assert.AreEqual(200, accepted.StatusCode);
        }
        var limited = await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(ticket, 5, 50_000_000, start.Response.OwnerRevision));
        Assert.AreEqual(429, limited.StatusCode);
        Assert.AreEqual(5, limited.RetryAfterSeconds);
        Assert.AreEqual(5, limited.Response.RetryAfterSeconds);

        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(10);
        var recovered = await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(ticket, 6, 60_000_000, start.Response.OwnerRevision));
        Assert.AreEqual(200, recovered.StatusCode);
    }

    [TestMethod]
    public async Task ActiveCapacityIsReservedAtStartAndReleasedAtStop()
    {
        var harness = new Harness(activeCapacity: 1, perUserActiveCapacity: 1, totalCapacity: 4);
        var first = harness.Issue(itemId: Guid.NewGuid());
        var second = harness.Issue(itemId: Guid.NewGuid());

        var firstStart = await harness.Coordinator.StartAsync(
            first.Value,
            harness.Request(first, 1, 0));
        var limited = await harness.Coordinator.StartAsync(
            second.Value,
            harness.Request(second, 1, 0));

        Assert.IsTrue(firstStart.Response.Accepted);
        Assert.AreEqual(1, harness.Store.ActiveCount);
        Assert.AreEqual(429, limited.StatusCode);
        Assert.AreEqual("capacityLimited", limited.Response.Reason);

        await harness.Coordinator.StopAsync(
            first.Value,
            harness.Request(first, 2, 0, firstStart.Response.OwnerRevision, "shutdown"));
        var secondStart = await harness.Coordinator.StartAsync(
            second.Value,
            harness.Request(second, 1, 0));

        Assert.IsTrue(secondStart.Response.Accepted);
        Assert.AreEqual(1, harness.Store.ActiveCount);
    }

    [TestMethod]
    public async Task DisableStopsActiveSessionsAndInvalidatesEveryReportingTicket()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        await harness.Coordinator.StartAsync(ticket.Value, harness.Request(ticket, 1, 0));

        await harness.Coordinator.DisableAsync();

        Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        Assert.AreEqual(0, harness.Store.Count);
        Assert.AreEqual(0, harness.Store.ActiveCount);
        Assert.AreEqual(410, (await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(ticket, 2, 1, ownerRevision: 1))).StatusCode);
    }

    [TestMethod]
    public async Task DisableWaitsForAnInFlightStartAndCannotLeaveAGhostSession()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        harness.Bridge.BlockNextStart();

        var startTask = harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 321_000_000));
        await harness.Bridge.WaitForBlockedStartAsync();
        var disableTask = harness.Coordinator.DisableAsync();

        Assert.IsFalse(disableTask.IsCompleted, "disable must join a Start that already owns the state gate");
        harness.Bridge.ReleaseBlockedStart();
        var start = await startTask;
        await disableTask;

        Assert.IsFalse(start.Response.Accepted);
        Assert.AreEqual(410, start.StatusCode);
        Assert.AreEqual("disabled", start.Response.Reason);
        Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        Assert.AreEqual(321_000_000, harness.Bridge.LastStopPositionTicks);
        Assert.AreEqual(0, harness.Store.ActiveCount);
        Assert.AreEqual(0, harness.Store.Count);
    }

    [TestMethod]
    public async Task CleanupCannotDetachAnInFlightStartAndExpiryPreventsPublication()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        harness.Bridge.BlockNextStart();

        var startTask = harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 654_000_000));
        await harness.Bridge.WaitForBlockedStartAsync();
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddMinutes(31);

        Assert.AreEqual(0, harness.Store.CleanupExpired(64));
        Assert.AreEqual(1, harness.Store.Count);
        Assert.AreEqual(1, harness.Store.ActiveCount);

        harness.Bridge.ReleaseBlockedStart();
        var result = await startTask;

        Assert.AreEqual(410, result.StatusCode);
        Assert.AreEqual("expired", result.Response.Reason);
        Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        Assert.AreEqual(654_000_000, harness.Bridge.LastStopPositionTicks);
        Assert.AreEqual(0, harness.Store.ActiveCount);
        Assert.AreEqual(1, harness.Store.CleanupExpired(64));
        Assert.AreEqual(0, harness.Store.Count);
    }

    [TestMethod]
    public async Task RevokedProgressAuthorizationStillEndsTheExistingEmbySession()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 120_000_000));
        harness.Bridge.RevokeAuthorization();

        var progress = await harness.Coordinator.ProgressAsync(
            ticket.Value,
            harness.Request(ticket, 2, 130_000_000, start.Response.OwnerRevision));

        Assert.AreEqual(410, progress.StatusCode);
        Assert.AreEqual("unauthorized", progress.Response.Reason);
        Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        Assert.AreEqual(120_000_000, harness.Bridge.LastStopPositionTicks);
        Assert.AreEqual(0, harness.Store.ActiveCount);
    }

    [TestMethod]
    public async Task RevokedStopAuthorizationFallsBackToInternalSessionCleanup()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 220_000_000));
        harness.Bridge.RevokeAuthorization();

        var stop = await harness.Coordinator.StopAsync(
            ticket.Value,
            harness.Request(
                ticket,
                2,
                230_000_000,
                start.Response.OwnerRevision,
                "windowClosed"));

        Assert.AreEqual(410, stop.StatusCode);
        Assert.AreEqual("unauthorized", stop.Response.Reason);
        Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        Assert.AreEqual(220_000_000, harness.Bridge.LastStopPositionTicks);
        Assert.AreEqual(0, harness.Store.ActiveCount);
    }

    [TestMethod]
    public async Task RequestThatExpiresWhileWaitingForTheStateGateCannotReachEmby()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        var start = await harness.Coordinator.StartAsync(
            ticket.Value,
            harness.Request(ticket, 1, 0));
        Assert.AreEqual(PlaybackTicketLookupStatus.Found, harness.Store.TryGet(ticket.Value, out var state));
        var gate = (SemaphoreSlim)typeof(PlaybackReportState)
            .GetProperty("Gate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(state!)!;
        await gate.WaitAsync();
        try
        {
            var progressTask = harness.Coordinator.ProgressAsync(
                ticket.Value,
                harness.Request(ticket, 2, 10_000_000, start.Response.OwnerRevision));
            harness.Clock.UtcNow = harness.Clock.UtcNow.AddMinutes(31);
            gate.Release();
            gate = null!;

            var result = await progressTask;
            Assert.AreEqual(410, result.StatusCode);
            Assert.AreEqual("expired", result.Response.Reason);
            Assert.AreEqual(0, harness.Bridge.ProgressCount);
            Assert.AreEqual(1, harness.Bridge.AutomatedStopCount);
        }
        finally
        {
            gate?.Release();
        }
    }

    [TestMethod]
    public async Task WatchdogRechecksHeartbeatAfterWaitingForTheStateGate()
    {
        var harness = new Harness();
        var ticket = harness.Issue();
        await harness.Coordinator.StartAsync(ticket.Value, harness.Request(ticket, 1, 0));
        Assert.AreEqual(PlaybackTicketLookupStatus.Found, harness.Store.TryGet(ticket.Value, out var state));
        harness.Clock.UtcNow = harness.Clock.UtcNow.AddSeconds(91);
        var gate = (SemaphoreSlim)typeof(PlaybackReportState)
            .GetProperty("Gate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(state!)!;
        await gate.WaitAsync();
        try
        {
            var scanTask = harness.Coordinator.ScanInactiveAsync();
            typeof(PlaybackReportState)
                .GetProperty(nameof(PlaybackReportState.LastHeartbeatAtUtc))!
                .SetValue(state, harness.Clock.UtcNow);
            gate.Release();
            gate = null!;

            Assert.AreEqual(0, await scanTask);
            Assert.AreEqual(0, harness.Bridge.AutomatedStopCount);
            Assert.AreEqual(1, harness.Store.ActiveCount);
        }
        finally
        {
            gate?.Release();
        }
    }

    private sealed class Harness
    {
        private readonly Guid defaultUserId = Guid.NewGuid();
        private readonly Guid defaultItemId = Guid.NewGuid();

        public Harness(
            int activeCapacity = PlaybackReportTicketStore.DefaultActiveCapacity,
            int perUserActiveCapacity = PlaybackReportTicketStore.DefaultPerUserActiveCapacity,
            int totalCapacity = PlaybackReportTicketStore.DefaultTotalCapacity)
        {
            Store = new PlaybackReportTicketStore(
                Clock,
                activeCapacity,
                perUserActiveCapacity,
                totalCapacity);
            Coordinator = new PlaybackReportCoordinator(Store, Clock);
            Coordinator.SetBridge(Bridge);
        }

        public FakeClock Clock { get; } = new();

        public FakeBridge Bridge { get; } = new();

        public PlaybackReportTicketStore Store { get; }

        public PlaybackReportCoordinator Coordinator { get; }

        public PlaybackReportTicket Issue(
            Guid? userId = null,
            Guid? itemId = null,
            long runTimeTicks = 0)
        {
            var selectedItem = itemId ?? defaultItemId;
            return Store.Issue(new PlaybackReportGrant
            {
                UserId = userId ?? defaultUserId,
                ItemId = selectedItem,
                CanonicalItemId = selectedItem,
                MediaSourceId = "source-1",
                RunTimeTicks = runTimeTicks,
                PlayerName = "Test Player",
            }, TimeSpan.FromMinutes(30));
        }

        public PlaybackReportRequest Request(
            PlaybackReportTicket ticket,
            long sequence,
            long positionTicks,
            long? ownerRevision = null,
            string? endReason = null,
            long? runTimeTicks = null) =>
            new()
            {
                ProtocolVersion = 1,
                LaunchId = ticket.LaunchId,
                OwnerRevision = ownerRevision,
                Epoch = 1,
                Sequence = sequence,
                PositionTicks = positionTicks,
                RunTimeTicks = runTimeTicks,
                IsPaused = false,
                PlaybackRate = 1,
                ClientTimeUtc = Clock.UtcNow.ToString("O"),
                ClientEndReason = endReason,
            };
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeBridge : IPlaybackSessionBridge
    {
        private TaskCompletionSource<bool>? blockedStart;
        private TaskCompletionSource<bool>? blockedStartEntered;
        private bool authorizationRevoked;

        public int StartCount { get; private set; }

        public int ProgressCount { get; private set; }

        public int StopCount { get; private set; }

        public int AutomatedStopCount { get; private set; }

        public long LastStopPositionTicks { get; private set; }

        public long LastStartRunTimeTicks { get; private set; }

        public long LastProgressRunTimeTicks { get; private set; }

        public long LastProgressPositionTicks { get; private set; }

        public long LastStopRunTimeTicks { get; private set; }

        public void BlockNextStart()
        {
            blockedStart = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            blockedStartEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitForBlockedStartAsync() =>
            blockedStartEntered?.Task ?? Task.CompletedTask;

        public void ReleaseBlockedStart() => blockedStart?.TrySetResult(true);

        public void RevokeAuthorization() => authorizationRevoked = true;

        public async Task<PlaybackSessionHandle> StartAsync(
            PlaybackReportGrant grant,
            string launchId,
            PlaybackReportRequest request)
        {
            StartCount++;
            LastStartRunTimeTicks = grant.RunTimeTicks;
            if (blockedStart is not null)
            {
                blockedStartEntered!.TrySetResult(true);
                await blockedStart.Task;
                blockedStart = null;
                blockedStartEntered = null;
            }
            return new PlaybackSessionHandle
            {
                SessionId = "session-" + StartCount,
                PlaySessionId = "play-" + StartCount,
                NativeSession = new object(),
            };
        }

        public Task ProgressAsync(
            PlaybackReportGrant grant,
            PlaybackSessionHandle session,
            PlaybackReportRequest request,
            PlaybackProgressEventKind eventKind)
        {
            if (authorizationRevoked)
            {
                throw new PlaybackAuthorizationException("revoked");
            }
            ProgressCount++;
            LastProgressPositionTicks = request.PositionTicks;
            LastProgressRunTimeTicks = grant.RunTimeTicks;
            return Task.CompletedTask;
        }

        public Task StopAsync(
            PlaybackReportGrant grant,
            PlaybackSessionHandle session,
            long positionTicks,
            bool isAutomated)
        {
            if (authorizationRevoked && !isAutomated)
            {
                throw new PlaybackAuthorizationException("revoked");
            }
            StopCount++;
            LastStopPositionTicks = positionTicks;
            LastStopRunTimeTicks = grant.RunTimeTicks;
            if (isAutomated)
            {
                AutomatedStopCount++;
            }
            return Task.CompletedTask;
        }
    }
}
