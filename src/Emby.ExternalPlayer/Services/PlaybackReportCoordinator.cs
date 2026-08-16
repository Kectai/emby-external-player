using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public sealed class PlaybackReportCoordinator
{
    public const int ProtocolVersion = 1;
    public const int HeartbeatSeconds = 10;
    public const int WatchdogTimeoutSeconds = 90;
    public const int MaximumRequestBytes = 2048;
    public const long MaximumProtocolInteger = 9007199254740991;

    private static readonly HashSet<string> AllowedEndReasons = new(StringComparer.Ordinal)
    {
        "endFile",
        "windowClosed",
        "shutdown",
        "mediaChanged",
    };

    private readonly PlaybackReportTicketStore store;
    private readonly IClock clock;
    private IPlaybackSessionBridge? bridge;
    private volatile bool enabled = true;

    public PlaybackReportCoordinator(PlaybackReportTicketStore store, IClock clock)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public int ActiveCount => store.ActiveCount;

    public bool Enabled => enabled;

    public void SetBridge(IPlaybackSessionBridge value)
    {
        bridge = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void Enable() => enabled = true;

    public Task<PlaybackReportOperationResult> StartAsync(
        string rawTicket,
        PlaybackReportRequest request) =>
        ProcessStartAsync(rawTicket, request);

    public Task<PlaybackReportOperationResult> ProgressAsync(
        string rawTicket,
        PlaybackReportRequest request) =>
        ProcessProgressAsync(rawTicket, request);

    public Task<PlaybackReportOperationResult> StopAsync(
        string rawTicket,
        PlaybackReportRequest request) =>
        ProcessStopAsync(rawTicket, request);

    public async Task<int> ScanInactiveAsync(int maximum = 64)
    {
        if (!enabled || maximum <= 0)
        {
            return 0;
        }
        var now = clock.UtcNow;
        var candidates = store.Snapshot()
            .Where(state => state.Started && !state.Dormant && !state.Terminal &&
                (state.ExpiresAtUtc <= now ||
                 now - state.LastHeartbeatAtUtc >= TimeSpan.FromSeconds(WatchdogTimeoutSeconds)))
            .Take(maximum)
            .ToArray();
        var stopped = 0;
        foreach (var state in candidates)
        {
            await state.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await state.Lease.Gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!state.Started || state.Dormant || state.Terminal ||
                        !ReferenceEquals(state.Lease.Owner, state))
                    {
                        continue;
                    }
                    var currentTime = clock.UtcNow;
                    var expired = state.ExpiresAtUtc <= currentTime;
                    var inactive = currentTime - state.LastHeartbeatAtUtc >=
                        TimeSpan.FromSeconds(WatchdogTimeoutSeconds);
                    if (!expired && !inactive)
                    {
                        continue;
                    }
                    await TryStopBridgeAsync(state, isAutomated: true).ConfigureAwait(false);
                    state.Session = null;
                    state.Dormant = !expired;
                    state.Terminal = expired;
                    state.TerminalReason = expired ? "expired" : null;
                    state.Lease.Owner = null;
                    store.ReleaseActive(state);
                    stopped++;
                }
                finally
                {
                    state.Lease.Gate.Release();
                }
            }
            finally
            {
                state.Gate.Release();
            }
        }
        store.CleanupExpired(maximum);
        return stopped;
    }

    public async Task DisableAsync()
    {
        enabled = false;
        var states = store.Snapshot()
            .ToArray();
        foreach (var state in states)
        {
            await state.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await state.Lease.Gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(state.Lease.Owner, state))
                    {
                        await TryStopBridgeAsync(state, isAutomated: true).ConfigureAwait(false);
                        state.Lease.Owner = null;
                    }
                    state.Session = null;
                    state.Terminal = true;
                    state.Dormant = false;
                    state.TerminalReason = "disabled";
                    store.ReleaseActive(state);
                }
                finally
                {
                    state.Lease.Gate.Release();
                }
            }
            finally
            {
                state.Gate.Release();
            }
        }
        store.Clear();
    }

    private async Task<PlaybackReportOperationResult> ProcessStartAsync(
        string rawTicket,
        PlaybackReportRequest request)
    {
        var resolved = Resolve(rawTicket, request, requireOwnerRevision: false, isStop: false);
        if (resolved.Result is not null || resolved.State is null)
        {
            return resolved.Result!;
        }
        var state = resolved.State;
        await state.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (state.Terminal)
            {
                return TerminalResult(state, request);
            }
            await state.Lease.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (state.Terminal)
                {
                    return TerminalResult(state, request);
                }
                var lifecycleError = RevalidateLifecycle(rawTicket, state, request);
                if (lifecycleError is not null)
                {
                    MarkTerminalAndRelease(state, lifecycleError.Response.Reason ?? "terminal");
                    return lifecycleError;
                }
                if (state.Started && !state.Dormant)
                {
                    if (!ReferenceEquals(state.Lease.Owner, state))
                    {
                        MarkSuperseded(state);
                        return TerminalResult(state, request);
                    }
                    if (request.Epoch == state.CurrentEpoch &&
                        request.Sequence <= state.LastAcceptedSequence)
                    {
                        return Accepted(state, terminal: false);
                    }
                    return Error(409, request, "epochConflict", terminal: false);
                }

                if (!state.TryConsumeRateToken(clock.UtcNow))
                {
                    return Error(429, request, "rateLimited", terminal: false, retryAfterSeconds: 5);
                }

                var expectedEpoch = state.Dormant ? state.CurrentEpoch + 1 : 1;
                if (request.Epoch != expectedEpoch)
                {
                    return Error(409, request, "epochConflict", terminal: false);
                }
                if (state.LaunchGeneration < state.Lease.HighestStartedGeneration)
                {
                    MarkSuperseded(state);
                    return TerminalResult(state, request);
                }

                var previousOwner = state.Lease.Owner;
                if (previousOwner is not null && !ReferenceEquals(previousOwner, state))
                {
                    MarkSuperseded(previousOwner);
                    state.Lease.Owner = null;
                    await TryStopBridgeAsync(previousOwner, isAutomated: true).ConfigureAwait(false);
                    previousOwner.Session = null;
                }

                var currentBridge = bridge;
                if (currentBridge is null)
                {
                    return Error(410, request, "unavailable", terminal: true);
                }
                if (state.Lease.NextOwnerRevision >= MaximumProtocolInteger)
                {
                    return Error(500, request, "revisionExhausted", terminal: false);
                }
                var reservation = store.TryReserveCurrent(rawTicket, state);
                if (reservation == ActiveReservationStatus.Invalid)
                {
                    var reservationError = RevalidateLifecycle(rawTicket, state, request) ??
                        Error(410, request, "revoked", terminal: true);
                    MarkTerminalAndRelease(
                        state,
                        reservationError.Response.Reason ?? "terminal");
                    return reservationError;
                }
                if (reservation == ActiveReservationStatus.CapacityLimited)
                {
                    return Error(
                        429,
                        request,
                        "capacityLimited",
                        terminal: false,
                        retryAfterSeconds: 5);
                }
                var ownerRevision = ++state.Lease.NextOwnerRevision;
                PlaybackSessionHandle session;
                try
                {
                    session = await currentBridge.StartAsync(
                        state.Grant,
                        state.LaunchId,
                        request).ConfigureAwait(false);
                }
                catch (PlaybackAuthorizationException)
                {
                    MarkTerminalAndRelease(state, "unauthorized");
                    return Error(410, request, "unauthorized", terminal: true);
                }
                catch
                {
                    store.ReleaseActive(state);
                    return Error(500, request, "temporaryFailure", terminal: false);
                }

                state.Session = session;
                var postStartLifecycleError = RevalidateLifecycle(rawTicket, state, request);
                if (postStartLifecycleError is not null)
                {
                    await TryStopBridgeAsync(
                        state,
                        isAutomated: true,
                        positionTicks: request.PositionTicks).ConfigureAwait(false);
                    MarkTerminalAndRelease(
                        state,
                        postStartLifecycleError.Response.Reason ?? "terminal");
                    return postStartLifecycleError;
                }
                state.Started = true;
                state.Dormant = false;
                state.Terminal = false;
                state.TerminalReason = null;
                state.OwnerRevision = ownerRevision;
                state.CurrentEpoch = request.Epoch;
                state.LastAcceptedSequence = request.Sequence;
                state.LastPositionTicks = request.PositionTicks;
                state.LastIsPaused = request.IsPaused;
                state.LastHeartbeatAtUtc = clock.UtcNow;
                state.Lease.HighestStartedGeneration = Math.Max(
                    state.Lease.HighestStartedGeneration,
                    state.LaunchGeneration);
                if (previousOwner is not null && !ReferenceEquals(previousOwner, state))
                {
                    previousOwner.OwnerRevision = ownerRevision;
                }
                state.Lease.Owner = state;
                return Accepted(state, terminal: false);
            }
            finally
            {
                state.Lease.Gate.Release();
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task<PlaybackReportOperationResult> ProcessProgressAsync(
        string rawTicket,
        PlaybackReportRequest request)
    {
        var resolved = Resolve(rawTicket, request, requireOwnerRevision: true, isStop: false);
        if (resolved.Result is not null || resolved.State is null)
        {
            return resolved.Result!;
        }
        var state = resolved.State;
        await state.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (state.Terminal)
            {
                return TerminalResult(state, request);
            }
            await state.Lease.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var lifecycleError = RevalidateLifecycle(rawTicket, state, request);
                if (lifecycleError is not null)
                {
                    await TryStopBridgeAsync(state, isAutomated: true).ConfigureAwait(false);
                    MarkTerminalAndRelease(state, lifecycleError.Response.Reason ?? "terminal");
                    return lifecycleError;
                }
                var ownerError = ValidateActiveOwner(state, request);
                if (ownerError is not null)
                {
                    return ownerError;
                }
                if (request.Sequence <= state.LastAcceptedSequence)
                {
                    return Accepted(state, terminal: false);
                }
                if (!state.TryConsumeRateToken(clock.UtcNow))
                {
                    return Error(429, request, "rateLimited", terminal: false, retryAfterSeconds: 5);
                }

                var currentBridge = bridge;
                if (currentBridge is null || state.Session is null)
                {
                    return Error(410, request, "unavailable", terminal: true);
                }
                var eventKind = request.IsPaused == state.LastIsPaused
                    ? PlaybackProgressEventKind.TimeUpdate
                    : request.IsPaused
                        ? PlaybackProgressEventKind.Pause
                        : PlaybackProgressEventKind.Unpause;
                try
                {
                    await currentBridge.ProgressAsync(
                        state.Grant,
                        state.Session,
                        request,
                        eventKind).ConfigureAwait(false);
                }
                catch (PlaybackAuthorizationException)
                {
                    await TryStopBridgeAsync(state, isAutomated: true).ConfigureAwait(false);
                    MarkTerminalAndRelease(state, "unauthorized");
                    return Error(410, request, "unauthorized", terminal: true);
                }
                catch
                {
                    return Error(500, request, "temporaryFailure", terminal: false);
                }

                AcceptPosition(state, request);
                return Accepted(state, terminal: false);
            }
            finally
            {
                state.Lease.Gate.Release();
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task<PlaybackReportOperationResult> ProcessStopAsync(
        string rawTicket,
        PlaybackReportRequest request)
    {
        var resolved = Resolve(rawTicket, request, requireOwnerRevision: true, isStop: true);
        if (resolved.Result is not null || resolved.State is null)
        {
            return resolved.Result!;
        }
        var state = resolved.State;
        await state.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (state.Terminal)
            {
                return TerminalResult(state, request);
            }
            await state.Lease.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var lifecycleError = RevalidateLifecycle(rawTicket, state, request);
                if (lifecycleError is not null)
                {
                    await TryStopBridgeAsync(state, isAutomated: true).ConfigureAwait(false);
                    MarkTerminalAndRelease(state, lifecycleError.Response.Reason ?? "terminal");
                    return lifecycleError;
                }
                var ownerError = ValidateActiveOwner(state, request);
                if (ownerError is not null)
                {
                    return ownerError;
                }
                if (request.Sequence <= state.LastAcceptedSequence)
                {
                    return Accepted(state, terminal: false);
                }
                if (!state.TryConsumeRateToken(clock.UtcNow))
                {
                    return Error(429, request, "rateLimited", terminal: false, retryAfterSeconds: 5);
                }
                var currentBridge = bridge;
                if (currentBridge is null || state.Session is null)
                {
                    return Error(410, request, "unavailable", terminal: true);
                }
                var finalPositionTicks = request.PositionTicks == 0 &&
                    state.LastPositionTicks > 0
                        ? state.LastPositionTicks
                        : request.PositionTicks;
                try
                {
                    await currentBridge.StopAsync(
                        state.Grant,
                        state.Session,
                        finalPositionTicks,
                        isAutomated: false).ConfigureAwait(false);
                }
                catch (PlaybackAuthorizationException)
                {
                    await TryStopBridgeAsync(state, isAutomated: true).ConfigureAwait(false);
                    MarkTerminalAndRelease(state, "unauthorized");
                    return Error(410, request, "unauthorized", terminal: true);
                }
                catch
                {
                    return Error(500, request, "temporaryFailure", terminal: false);
                }

                AcceptPosition(state, request, finalPositionTicks);
                state.Terminal = true;
                state.TerminalReason = "stopped";
                state.Dormant = false;
                state.Session = null;
                if (ReferenceEquals(state.Lease.Owner, state))
                {
                    state.Lease.Owner = null;
                }
                store.ReleaseActive(state);
                return Accepted(state, terminal: true, reason: "stopped");
            }
            finally
            {
                state.Lease.Gate.Release();
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private (PlaybackReportState? State, PlaybackReportOperationResult? Result) Resolve(
        string rawTicket,
        PlaybackReportRequest request,
        bool requireOwnerRevision,
        bool isStop)
    {
        if (!enabled)
        {
            return (null, Error(410, request, "disabled", terminal: true));
        }
        var lookup = store.TryGet(rawTicket, out var state);
        if (lookup == PlaybackTicketLookupStatus.Invalid || state is null)
        {
            return (null, Error(401, request, "invalidTicket", terminal: true));
        }
        if (lookup == PlaybackTicketLookupStatus.Expired)
        {
            return (state, Error(410, request, "expired", terminal: true));
        }
        if (!string.Equals(request.LaunchId, state.LaunchId, StringComparison.Ordinal))
        {
            return (state, Error(401, request, "launchMismatch", terminal: true));
        }
        var validationError = ValidateRequest(state, request, requireOwnerRevision, isStop);
        return validationError is null
            ? (state, null)
            : (state, Error(400, request, validationError, terminal: true));
    }

    private static string? ValidateRequest(
        PlaybackReportState state,
        PlaybackReportRequest request,
        bool requireOwnerRevision,
        bool isStop)
    {
        if (request is null || request.ProtocolVersion != ProtocolVersion ||
            !PlaybackReportTicketStore.IsValidLaunchId(request.LaunchId))
        {
            return "invalidProtocol";
        }
        if (request.Epoch <= 0 || request.Sequence <= 0 ||
            request.Sequence > MaximumProtocolInteger || request.PositionTicks < 0 ||
            request.PositionTicks > MaximumProtocolInteger)
        {
            return "invalidSequenceOrPosition";
        }
        if (requireOwnerRevision)
        {
            if (!request.OwnerRevision.HasValue || request.OwnerRevision <= 0 ||
                request.OwnerRevision > MaximumProtocolInteger)
            {
                return "invalidOwnerRevision";
            }
        }
        else if (request.OwnerRevision.HasValue)
        {
            return "unexpectedOwnerRevision";
        }
        if (double.IsNaN(request.PlaybackRate) || double.IsInfinity(request.PlaybackRate) ||
            request.PlaybackRate < 0.1 || request.PlaybackRate > 16)
        {
            return "invalidPlaybackRate";
        }
        if (state.Grant.RunTimeTicks > 0 &&
            request.PositionTicks > state.Grant.RunTimeTicks &&
            request.PositionTicks - state.Grant.RunTimeTicks > TimeSpan.FromMinutes(5).Ticks)
        {
            return "invalidPosition";
        }
        if (string.IsNullOrWhiteSpace(request.ClientTimeUtc) || request.ClientTimeUtc.Length > 64 ||
            !DateTimeOffset.TryParse(
                request.ClientTimeUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            return "invalidClientTime";
        }
        if (!isStop && !string.IsNullOrEmpty(request.ClientEndReason))
        {
            return "unexpectedEndReason";
        }
        if (isStop && !string.IsNullOrEmpty(request.ClientEndReason) &&
            !AllowedEndReasons.Contains(request.ClientEndReason))
        {
            return "invalidEndReason";
        }
        return null;
    }

    private PlaybackReportOperationResult? ValidateActiveOwner(
        PlaybackReportState state,
        PlaybackReportRequest request)
    {
        if (state.Dormant || !state.Started)
        {
            return Error(409, request, "epochRequired", terminal: false);
        }
        if (!ReferenceEquals(state.Lease.Owner, state) ||
            state.OwnerRevision != request.OwnerRevision)
        {
            MarkSuperseded(state);
            return TerminalResult(state, request);
        }
        if (state.CurrentEpoch != request.Epoch)
        {
            return Error(409, request, "epochConflict", terminal: false);
        }
        return null;
    }

    private void AcceptPosition(
        PlaybackReportState state,
        PlaybackReportRequest request,
        long? positionTicks = null)
    {
        state.LastAcceptedSequence = request.Sequence;
        state.LastPositionTicks = positionTicks ?? request.PositionTicks;
        state.LastIsPaused = request.IsPaused;
        state.LastHeartbeatAtUtc = clock.UtcNow;
    }

    private void MarkSuperseded(PlaybackReportState state)
    {
        state.Terminal = true;
        state.Dormant = false;
        state.TerminalReason = "superseded";
        store.ReleaseActive(state);
    }

    private void MarkTerminalAndRelease(PlaybackReportState state, string reason)
    {
        state.Terminal = true;
        state.Dormant = false;
        state.TerminalReason = reason;
        state.Session = null;
        if (ReferenceEquals(state.Lease.Owner, state))
        {
            state.Lease.Owner = null;
        }
        store.ReleaseActive(state);
    }

    private PlaybackReportOperationResult? RevalidateLifecycle(
        string rawTicket,
        PlaybackReportState state,
        PlaybackReportRequest request)
    {
        if (!enabled)
        {
            return Error(410, request, "disabled", terminal: true);
        }
        if (state.ExpiresAtUtc <= clock.UtcNow)
        {
            return Error(410, request, "expired", terminal: true);
        }
        if (!store.IsCurrent(rawTicket, state))
        {
            return Error(410, request, "revoked", terminal: true);
        }
        return null;
    }

    private async Task TryStopBridgeAsync(
        PlaybackReportState state,
        bool isAutomated,
        long? positionTicks = null)
    {
        var currentBridge = bridge;
        if (currentBridge is null || state.Session is null)
        {
            return;
        }
        try
        {
            await currentBridge.StopAsync(
                state.Grant,
                state.Session,
                positionTicks ?? state.LastPositionTicks,
                isAutomated).ConfigureAwait(false);
        }
        catch
        {
            // Cleanup is best effort. The authorization is still made terminal so
            // stale clients cannot keep mutating shared progress.
        }
    }

    private PlaybackReportOperationResult TerminalResult(
        PlaybackReportState state,
        PlaybackReportRequest request)
    {
        if (string.Equals(state.TerminalReason, "superseded", StringComparison.Ordinal))
        {
            return new PlaybackReportOperationResult
            {
                StatusCode = 200,
                Response = CreateResponse(
                    accepted: false,
                    state.OwnerRevision,
                    state.CurrentEpoch > 0 ? state.CurrentEpoch : Math.Max(1, request.Epoch),
                    state.LastAcceptedSequence,
                    terminal: true,
                    reason: "superseded"),
            };
        }
        if (string.Equals(state.TerminalReason, "stopped", StringComparison.Ordinal))
        {
            return Accepted(state, terminal: true, reason: "stopped");
        }
        return Error(410, request, state.TerminalReason ?? "terminal", terminal: true);
    }

    private PlaybackReportOperationResult Accepted(
        PlaybackReportState state,
        bool terminal,
        string? reason = null) =>
        new()
        {
            StatusCode = 200,
            Response = CreateResponse(
                accepted: true,
                state.OwnerRevision,
                Math.Max(1, state.CurrentEpoch),
                state.LastAcceptedSequence,
                terminal,
                reason),
        };

    private PlaybackReportOperationResult Error(
        int statusCode,
        PlaybackReportRequest? request,
        string reason,
        bool terminal,
        int? retryAfterSeconds = null) =>
        new()
        {
            StatusCode = statusCode,
            RetryAfterSeconds = retryAfterSeconds,
            Response = CreateResponse(
                accepted: false,
                ownerRevision: null,
                Math.Max(1, request?.Epoch ?? 1),
                acceptedSequence: 0,
                terminal,
                reason,
                retryAfterSeconds),
        };

    private PlaybackReportResponse CreateResponse(
        bool accepted,
        long? ownerRevision,
        int epoch,
        long acceptedSequence,
        bool terminal,
        string? reason,
        int? retryAfterSeconds = null) =>
        new()
        {
            Accepted = accepted,
            OwnerRevision = ownerRevision,
            Epoch = epoch,
            AcceptedSequence = acceptedSequence,
            Terminal = terminal,
            Reason = reason,
            RetryAfterSeconds = retryAfterSeconds,
            ServerTimeUtc = clock.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };
}
