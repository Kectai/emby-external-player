using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public sealed class LaunchTicketStore
{
    public const int MinimumLifetimeMinutes = 30;
    public const int MaximumLifetimeMinutes = 720;
    public const int DefaultCapacity = 2000;

    private readonly ConcurrentDictionary<string, TicketEntry> tickets = new(StringComparer.Ordinal);
    private readonly IClock clock;
    private readonly int capacity;
    private readonly object issueLock = new();

    public LaunchTicketStore(IClock clock, int capacity = DefaultCapacity)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    }

    public int Count => tickets.Count;

    public LaunchTicket Issue(LaunchTicketPayload payload, TimeSpan lifetime)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (lifetime < TimeSpan.FromMinutes(MinimumLifetimeMinutes) ||
            lifetime > TimeSpan.FromMinutes(MaximumLifetimeMinutes))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        lock (issueLock)
        {
            RemoveExpired();
            while (tickets.Count >= capacity)
            {
                EvictOldest();
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var rawTicket = CreateRandomTicket();
                var key = Hash(rawTicket);
                var createdAt = clock.UtcNow;
                var expiresAt = createdAt.Add(lifetime);
                if (tickets.TryAdd(key, new TicketEntry(payload, createdAt, expiresAt)))
                {
                    return new LaunchTicket(rawTicket, expiresAt);
                }
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique playback ticket.");
    }

    public bool TryGet(string rawTicket, out LaunchTicketPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(rawTicket))
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
        entry.LastAccessAt = clock.UtcNow;
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
        if (string.IsNullOrWhiteSpace(rawTicket))
        {
            return false;
        }

        return tickets.TryRemove(Hash(rawTicket), out _);
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

    private void EvictOldest()
    {
        string? oldestKey = null;
        var oldestCreatedAt = DateTimeOffset.MaxValue;
        foreach (var pair in tickets)
        {
            if (pair.Value.CreatedAt < oldestCreatedAt)
            {
                oldestKey = pair.Key;
                oldestCreatedAt = pair.Value.CreatedAt;
            }
        }

        if (oldestKey is null || !tickets.TryRemove(oldestKey, out _))
        {
            throw new InvalidOperationException("Unable to enforce the playback ticket capacity.");
        }
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
            LastAccessAt = createdAt;
        }

        public LaunchTicketPayload Payload { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset ExpiresAt { get; }

        public DateTimeOffset LastAccessAt { get; set; }
    }
}
