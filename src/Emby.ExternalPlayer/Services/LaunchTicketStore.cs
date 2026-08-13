using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public sealed class LaunchTicketStore
{
    public const int MinimumLifetimeMinutes = 30;
    public const int MaximumLifetimeMinutes = 720;
    public const int DefaultLifetimeMinutes = 480;
    public const int DefaultCapacity = 2000;
    public const int DefaultPerUserCapacity = 100;

    private static readonly Regex TicketPattern = new(
        "^[A-Za-z0-9_-]{43}$",
        RegexOptions.CultureInvariant);

    private readonly ConcurrentDictionary<string, TicketEntry> tickets = new(StringComparer.Ordinal);
    private readonly IClock clock;
    private readonly int capacity;
    private readonly int perUserCapacity;
    private readonly object issueLock = new();

    public LaunchTicketStore(
        IClock clock,
        int capacity = DefaultCapacity,
        int perUserCapacity = DefaultPerUserCapacity)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        this.perUserCapacity = perUserCapacity > 0
            ? Math.Min(perUserCapacity, capacity)
            : throw new ArgumentOutOfRangeException(nameof(perUserCapacity));
    }

    public int Count => tickets.Count;

    public static TimeSpan CreateLifetime(int minutes)
    {
        if (minutes < MinimumLifetimeMinutes || minutes > MaximumLifetimeMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }

        return TimeSpan.FromMinutes(minutes);
    }

    public LaunchTicket Issue(LaunchTicketPayload payload, TimeSpan lifetime)
    {
        return IssueBatch(new[] { payload }, lifetime)[0];
    }

    public IReadOnlyList<LaunchTicket> IssueBatch(
        IReadOnlyList<LaunchTicketPayload> payloads,
        TimeSpan lifetime)
    {
        if (payloads is null || payloads.Count == 0)
        {
            throw new ArgumentException("At least one playback ticket payload is required.", nameof(payloads));
        }

        if (lifetime < TimeSpan.FromMinutes(MinimumLifetimeMinutes) ||
            lifetime > TimeSpan.FromMinutes(MaximumLifetimeMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        var userId = payloads[0]?.UserId
            ?? throw new ArgumentException("A playback ticket payload is required.", nameof(payloads));
        if (payloads.Count > perUserCapacity || payloads.Count > capacity)
        {
            throw new InvalidOperationException("The playback ticket batch exceeds the store capacity.");
        }
        foreach (var payload in payloads)
        {
            if (payload is null || payload.UserId != userId)
            {
                throw new ArgumentException(
                    "All playback tickets in a batch must belong to the same user.",
                    nameof(payloads));
            }
        }

        lock (issueLock)
        {
            RemoveExpired();
            while (CountForUser(userId) + payloads.Count > perUserCapacity)
            {
                if (!TryEvictOldest(userId) &&
                    CountForUser(userId) + payloads.Count > perUserCapacity)
                {
                    throw new InvalidOperationException("Unable to enforce the per-user playback ticket capacity.");
                }
            }

            while (tickets.Count + payloads.Count > capacity)
            {
                // A caller may recycle only its own tickets. This prevents one
                // playback user from evicting another user's active stream.
                if (!TryEvictOldest(userId) && tickets.Count + payloads.Count > capacity)
                {
                    throw new InvalidOperationException("The playback ticket capacity is full.");
                }
            }

            var issued = new List<LaunchTicket>(payloads.Count);
            var issuedKeys = new List<string>(payloads.Count);
            foreach (var payload in payloads)
            {
                var added = false;
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    var rawTicket = CreateRandomTicket();
                    var key = Hash(rawTicket);
                    var createdAt = clock.UtcNow;
                    var expiresAt = createdAt.Add(lifetime);
                    if (tickets.TryAdd(key, new TicketEntry(payload, createdAt, expiresAt)))
                    {
                        issued.Add(new LaunchTicket(rawTicket, expiresAt));
                        issuedKeys.Add(key);
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    foreach (var key in issuedKeys)
                    {
                        tickets.TryRemove(key, out _);
                    }
                    throw new InvalidOperationException("Unable to allocate a unique playback ticket.");
                }
            }
            return issued;
        }
    }

    public bool TryGet(string rawTicket, out LaunchTicketPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(rawTicket) || !TicketPattern.IsMatch(rawTicket))
        {
            return false;
        }

        var key = Hash(rawTicket);
        if (!tickets.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= clock.UtcNow)
        {
            tickets.TryRemove(key, out _);
            return false;
        }

        payload = entry.Payload;
        return true;
    }

    public int RemoveExpired()
    {
        var removed = 0;
        var now = clock.UtcNow;
        foreach (var pair in tickets)
        {
            if (pair.Value.ExpiresAt <= now && tickets.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    public bool Revoke(string rawTicket)
    {
        if (string.IsNullOrWhiteSpace(rawTicket) || !TicketPattern.IsMatch(rawTicket))
        {
            return false;
        }

        return tickets.TryRemove(Hash(rawTicket), out _);
    }

    public int Clear()
    {
        var removed = 0;
        foreach (var key in tickets.Keys)
        {
            if (tickets.TryRemove(key, out _))
            {
                removed++;
            }
        }
        return removed;
    }

    private static string CreateRandomTicket()
    {
        var bytes = new byte[32];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Hash(string rawTicket)
    {
        using var sha256 = SHA256.Create();
        var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawTicket));
        return Convert.ToBase64String(digest);
    }

    private int CountForUser(Guid userId)
    {
        var count = 0;
        foreach (var entry in tickets.Values)
        {
            if (entry.Payload.UserId == userId)
            {
                count++;
            }
        }
        return count;
    }

    private bool TryEvictOldest(Guid userId)
    {
        string? oldestKey = null;
        var oldestCreatedAt = DateTimeOffset.MaxValue;
        foreach (var pair in tickets)
        {
            if (pair.Value.Payload.UserId == userId && pair.Value.CreatedAt < oldestCreatedAt)
            {
                oldestKey = pair.Key;
                oldestCreatedAt = pair.Value.CreatedAt;
            }
        }

        return oldestKey is not null && tickets.TryRemove(oldestKey, out _);
    }

    private sealed class TicketEntry
    {
        public TicketEntry(
            LaunchTicketPayload payload,
            DateTimeOffset createdAt,
            DateTimeOffset expiresAt)
        {
            Payload = payload;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public LaunchTicketPayload Payload { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset ExpiresAt { get; }
    }
}
