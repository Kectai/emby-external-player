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
    public const int DefaultCapacity = 2048;

    private readonly ConcurrentDictionary<string, TicketEntry> tickets = new(StringComparer.Ordinal);
    private readonly IClock clock;
    private readonly int capacity;

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

        RemoveExpired();
        if (tickets.Count >= capacity)
        {
            throw new InvalidOperationException("The playback ticket store is at capacity.");
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var rawTicket = CreateRandomTicket();
            var key = Hash(rawTicket);
            var expiresAt = clock.UtcNow.Add(lifetime);
            if (tickets.TryAdd(key, new TicketEntry(payload, expiresAt)))
            {
                return new LaunchTicket(rawTicket, expiresAt);
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

    private sealed class TicketEntry
    {
        public TicketEntry(LaunchTicketPayload payload, DateTimeOffset expiresAt)
        {
            Payload = payload;
            ExpiresAt = expiresAt;
        }

        public LaunchTicketPayload Payload { get; }

        public DateTimeOffset ExpiresAt { get; }
    }
}
