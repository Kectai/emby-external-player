using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public enum PlaybackTicketLookupStatus
{
    Invalid,
    Expired,
    Found,
}

internal enum ActiveReservationStatus
{
    Invalid,
    CapacityLimited,
    Reserved,
}

public sealed class PlaybackReportTicketStore
{
    public const int DefaultActiveCapacity = 256;
    public const int DefaultPerUserActiveCapacity = 8;
    public const int DefaultTotalCapacity = 512;

    private static readonly Regex TicketPattern = new(
        "^[A-Za-z0-9_-]{43}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex LaunchIdPattern = new(
        "^[a-f0-9]{32}$",
        RegexOptions.CultureInvariant);

    private readonly ConcurrentDictionary<string, PlaybackReportState> tickets =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PlaybackLeaseState> leases =
        new(StringComparer.Ordinal);
    private readonly IClock clock;
    private readonly int activeCapacity;
    private readonly int perUserActiveCapacity;
    private readonly int totalCapacity;
    private readonly object issueSync = new();
    private readonly object capacitySync = new();
    private readonly Dictionary<Guid, int> activeByUser = new();
    private int activeCount;

    public PlaybackReportTicketStore(
        IClock clock,
        int activeCapacity = DefaultActiveCapacity,
        int perUserActiveCapacity = DefaultPerUserActiveCapacity,
        int totalCapacity = DefaultTotalCapacity)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.activeCapacity = activeCapacity > 0
            ? activeCapacity
            : throw new ArgumentOutOfRangeException(nameof(activeCapacity));
        this.perUserActiveCapacity = perUserActiveCapacity > 0 &&
            perUserActiveCapacity <= activeCapacity
                ? perUserActiveCapacity
                : throw new ArgumentOutOfRangeException(nameof(perUserActiveCapacity));
        this.totalCapacity = totalCapacity >= activeCapacity
            ? totalCapacity
            : throw new ArgumentOutOfRangeException(nameof(totalCapacity));
    }

    public int Count => tickets.Count;

    public int ActiveCount
    {
        get
        {
            lock (capacitySync)
            {
                return activeCount;
            }
        }
    }

    public PlaybackReportTicket Issue(PlaybackReportGrant grant, TimeSpan lifetime)
    {
        ValidateGrant(grant);
        if (lifetime < TimeSpan.FromMinutes(LaunchTicketStore.MinimumLifetimeMinutes) ||
            lifetime > TimeSpan.FromMinutes(LaunchTicketStore.MaximumLifetimeMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        lock (issueSync)
        {
            CleanupExpired(64);
            if (tickets.Count >= totalCapacity)
            {
                throw new InvalidOperationException("The playback reporting ticket capacity is full.");
            }
            var progressKey = CreateProgressKey(grant.UserId, grant.CanonicalItemId);
            var lease = leases.GetOrAdd(progressKey, _ => new PlaybackLeaseState(progressKey));
            long generation;
            lock (lease.GenerationSync)
            {
                generation = checked(++lease.NextLaunchGeneration);
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var rawTicket = CreateRandomTicket();
                var ticketHash = Hash(rawTicket);
                var launchId = Guid.NewGuid().ToString("N");
                var now = clock.UtcNow;
                var expiresAt = now.Add(lifetime);
                var state = new PlaybackReportState(
                    grant,
                    launchId,
                    generation,
                    now,
                    expiresAt,
                    lease);
                if (tickets.TryAdd(ticketHash, state))
                {
                    lease.RetainUntilUtc = expiresAt > lease.RetainUntilUtc
                        ? expiresAt
                        : lease.RetainUntilUtc;
                    return new PlaybackReportTicket(rawTicket, launchId, expiresAt);
                }
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique playback reporting ticket.");
    }

    public PlaybackTicketLookupStatus TryGet(
        string rawTicket,
        out PlaybackReportState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(rawTicket) || !TicketPattern.IsMatch(rawTicket))
        {
            return PlaybackTicketLookupStatus.Invalid;
        }
        if (!tickets.TryGetValue(Hash(rawTicket), out state))
        {
            return PlaybackTicketLookupStatus.Invalid;
        }
        return state.ExpiresAtUtc <= clock.UtcNow
            ? PlaybackTicketLookupStatus.Expired
            : PlaybackTicketLookupStatus.Found;
    }

    public bool Revoke(string rawTicket)
    {
        if (string.IsNullOrWhiteSpace(rawTicket) || !TicketPattern.IsMatch(rawTicket))
        {
            return false;
        }
        lock (issueSync)
        {
            var key = Hash(rawTicket);
            if (!tickets.TryGetValue(key, out var state) || IsActiveReserved(state))
            {
                return false;
            }
            if (!tickets.TryRemove(key, out state))
            {
                return false;
            }
            if (state.Lease.Owner is null && !tickets.Values.Any(candidate =>
                    ReferenceEquals(candidate.Lease, state.Lease)) &&
                leases.TryGetValue(state.Lease.ProgressKey, out var retainedLease) &&
                ReferenceEquals(retainedLease, state.Lease))
            {
                leases.TryRemove(state.Lease.ProgressKey, out _);
            }
            return true;
        }
    }

    public IReadOnlyCollection<PlaybackReportState> Snapshot() =>
        tickets.Values.ToArray();

    internal bool IsCurrent(string rawTicket, PlaybackReportState expected)
    {
        if (string.IsNullOrWhiteSpace(rawTicket) || !TicketPattern.IsMatch(rawTicket))
        {
            return false;
        }
        return tickets.TryGetValue(Hash(rawTicket), out var current) &&
            ReferenceEquals(current, expected) && current.ExpiresAtUtc > clock.UtcNow;
    }

    internal ActiveReservationStatus TryReserveCurrent(
        string rawTicket,
        PlaybackReportState state)
    {
        if (string.IsNullOrWhiteSpace(rawTicket) || !TicketPattern.IsMatch(rawTicket))
        {
            return ActiveReservationStatus.Invalid;
        }
        lock (issueSync)
        {
            if (!tickets.TryGetValue(Hash(rawTicket), out var current) ||
                !ReferenceEquals(current, state) || state.ExpiresAtUtc <= clock.UtcNow)
            {
                return ActiveReservationStatus.Invalid;
            }
            lock (capacitySync)
            {
                if (state.ActiveCapacityReserved)
                {
                    return ActiveReservationStatus.Reserved;
                }
                activeByUser.TryGetValue(state.Grant.UserId, out var userCount);
                if (activeCount >= activeCapacity || userCount >= perUserActiveCapacity)
                {
                    return ActiveReservationStatus.CapacityLimited;
                }
                state.ActiveCapacityReserved = true;
                activeCount++;
                activeByUser[state.Grant.UserId] = userCount + 1;
                return ActiveReservationStatus.Reserved;
            }
        }
    }

    internal void ReleaseActive(PlaybackReportState state)
    {
        lock (capacitySync)
        {
            if (!state.ActiveCapacityReserved)
            {
                return;
            }
            state.ActiveCapacityReserved = false;
            activeCount = Math.Max(0, activeCount - 1);
            if (activeByUser.TryGetValue(state.Grant.UserId, out var userCount))
            {
                if (userCount <= 1)
                {
                    activeByUser.Remove(state.Grant.UserId);
                }
                else
                {
                    activeByUser[state.Grant.UserId] = userCount - 1;
                }
            }
        }
    }

    private bool IsActiveReserved(PlaybackReportState state)
    {
        lock (capacitySync)
        {
            return state.ActiveCapacityReserved;
        }
    }

    public int CleanupExpired(int maximum)
    {
        if (maximum <= 0)
        {
            return 0;
        }
        lock (issueSync)
        {
            var removed = 0;
            var now = clock.UtcNow;
            foreach (var pair in tickets)
            {
                if (removed >= maximum)
                {
                    break;
                }
                var state = pair.Value;
                if (state.ExpiresAtUtc <= now &&
                    (!state.Started || state.Terminal || state.Dormant) &&
                    !IsActiveReserved(state) &&
                    tickets.TryRemove(pair.Key, out _))
                {
                    ReleaseActive(state);
                    removed++;
                }
            }
            foreach (var pair in leases)
            {
                if (pair.Value.RetainUntilUtc <= now && pair.Value.Owner is null &&
                    !tickets.Values.Any(candidate => ReferenceEquals(candidate.Lease, pair.Value)) &&
                    leases.TryGetValue(pair.Key, out var current) &&
                    ReferenceEquals(current, pair.Value))
                {
                    leases.TryRemove(pair.Key, out _);
                }
            }
            return removed;
        }
    }

    public int Clear()
    {
        lock (issueSync)
        {
            var removed = 0;
            foreach (var key in tickets.Keys)
            {
                if (tickets.TryRemove(key, out _))
                {
                    removed++;
                }
            }
            leases.Clear();
            lock (capacitySync)
            {
                activeCount = 0;
                activeByUser.Clear();
            }
            return removed;
        }
    }

    public static bool IsValidLaunchId(string value) =>
        !string.IsNullOrEmpty(value) && LaunchIdPattern.IsMatch(value);

    private static void ValidateGrant(PlaybackReportGrant grant)
    {
        if (grant is null)
        {
            throw new ArgumentNullException(nameof(grant));
        }
        if (grant.UserId == Guid.Empty || grant.ItemId == Guid.Empty ||
            grant.CanonicalItemId == Guid.Empty || string.IsNullOrWhiteSpace(grant.MediaSourceId) ||
            grant.MediaSourceId.Length > 256 || grant.RunTimeTicks < 0 ||
            string.IsNullOrWhiteSpace(grant.PlayerName) || grant.PlayerName.Length > 80 ||
            (grant.ClientAddress?.Length ?? 0) > 128)
        {
            throw new ArgumentException("The playback reporting grant is invalid.", nameof(grant));
        }
    }

    private static string CreateProgressKey(Guid userId, Guid canonicalItemId) =>
        userId.ToString("N") + ":" + canonicalItemId.ToString("N");

    private static string CreateRandomTicket()
    {
        var bytes = new byte[32];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Hash(string rawTicket)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(rawTicket)));
    }
}

public sealed class PlaybackReportState
{
    internal PlaybackReportState(
        PlaybackReportGrant grant,
        string launchId,
        long launchGeneration,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc,
        PlaybackLeaseState lease)
    {
        Grant = grant;
        LaunchId = launchId;
        LaunchGeneration = launchGeneration;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        LastHeartbeatAtUtc = createdAtUtc;
        Lease = lease;
    }

    public PlaybackReportGrant Grant { get; }

    public string LaunchId { get; }

    public long LaunchGeneration { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool Started { get; internal set; }

    public bool Dormant { get; internal set; }

    public bool Terminal { get; internal set; }

    public string? TerminalReason { get; internal set; }

    public long? OwnerRevision { get; internal set; }

    public int CurrentEpoch { get; internal set; }

    public long LastAcceptedSequence { get; internal set; }

    public long LastPositionTicks { get; internal set; }

    public bool LastIsPaused { get; internal set; }

    public DateTimeOffset LastHeartbeatAtUtc { get; internal set; }

    public PlaybackSessionHandle? Session { get; internal set; }

    internal bool ActiveCapacityReserved { get; set; }

    internal SemaphoreSlim Gate { get; } = new(1, 1);

    internal PlaybackLeaseState Lease { get; }

    private readonly object rateSync = new();
    private double rateTokens = 4;
    private DateTimeOffset rateUpdatedAtUtc;

    internal bool TryConsumeRateToken(DateTimeOffset now)
    {
        lock (rateSync)
        {
            if (rateUpdatedAtUtc == default)
            {
                rateUpdatedAtUtc = CreatedAtUtc;
            }
            var elapsedSeconds = Math.Max(0, (now - rateUpdatedAtUtc).TotalSeconds);
            rateTokens = Math.Min(4, rateTokens + elapsedSeconds * 0.2);
            rateUpdatedAtUtc = now;
            if (rateTokens < 1)
            {
                return false;
            }
            rateTokens -= 1;
            return true;
        }
    }
}

internal sealed class PlaybackLeaseState
{
    public PlaybackLeaseState(string progressKey)
    {
        ProgressKey = progressKey;
    }

    public string ProgressKey { get; }

    public object GenerationSync { get; } = new();

    public SemaphoreSlim Gate { get; } = new(1, 1);

    public long NextLaunchGeneration { get; set; }

    public long NextOwnerRevision { get; set; }

    public long HighestStartedGeneration { get; set; }

    public PlaybackReportState? Owner { get; set; }

    public DateTimeOffset RetainUntilUtc { get; set; }
}
