using System;

namespace Emby.ExternalPlayer.Domain;

public sealed class PlaybackReportGrant
{
    public Guid UserId { get; set; }

    public Guid ItemId { get; set; }

    public Guid CanonicalItemId { get; set; }

    public string MediaSourceId { get; set; } = string.Empty;

    public bool IsRemoteStrm { get; set; }

    public long RunTimeTicks { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public string ClientAddress { get; set; } = string.Empty;
}

public sealed class PlaybackReportTicket
{
    public PlaybackReportTicket(
        string value,
        string launchId,
        DateTimeOffset expiresAtUtc)
    {
        Value = value;
        LaunchId = launchId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string Value { get; }

    public string LaunchId { get; }

    public DateTimeOffset ExpiresAtUtc { get; }
}

public class PlaybackReportRequest
{
    public int ProtocolVersion { get; set; }

    public string LaunchId { get; set; } = string.Empty;

    public long? OwnerRevision { get; set; }

    public int Epoch { get; set; }

    public long Sequence { get; set; }

    public long PositionTicks { get; set; }

    public long? RunTimeTicks { get; set; }

    public bool IsPaused { get; set; }

    public double PlaybackRate { get; set; }

    public string ClientTimeUtc { get; set; } = string.Empty;

    public string? ClientEndReason { get; set; }
}

public sealed class PlaybackReportResponse
{
    public bool Accepted { get; set; }

    public long? OwnerRevision { get; set; }

    public int Epoch { get; set; }

    public long AcceptedSequence { get; set; }

    public bool Terminal { get; set; }

    public string? Reason { get; set; }

    public int? RetryAfterSeconds { get; set; }

    public string ServerTimeUtc { get; set; } = string.Empty;
}

public sealed class PlaybackReportingCapability
{
    public int ProtocolVersion { get; set; }

    public int HeartbeatSeconds { get; set; }

    public string TicketExpiresAtUtc { get; set; } = string.Empty;
}

public enum PlaybackProgressEventKind
{
    TimeUpdate,
    Pause,
    Unpause,
}

public sealed class PlaybackSessionHandle
{
    public string SessionId { get; set; } = string.Empty;

    public string PlaySessionId { get; set; } = string.Empty;

    public object NativeSession { get; set; } = null!;
}

public sealed class PlaybackReportOperationResult
{
    public int StatusCode { get; set; }

    public int? RetryAfterSeconds { get; set; }

    public PlaybackReportResponse Response { get; set; } = new();
}
