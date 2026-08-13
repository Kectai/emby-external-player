using System;

namespace Emby.ExternalPlayer.Domain;

public enum LaunchTicketScope
{
    Media,
    Subtitle,
}

public sealed class LaunchTicketPayload
{
    public LaunchTicketScope Scope { get; set; }

    public Guid UserId { get; set; }

    public Guid ItemId { get; set; }

    public string MediaSourceId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int? SubtitleStreamIndex { get; set; }

    public long ContentLength { get; set; }

    public long LastWriteTimeUtcTicks { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public string SafeFileName { get; set; } = "media.bin";

    public string UrlFileName { get; set; } = "media";

    public string? SubtitleFormat { get; set; }
}

public sealed class LaunchTicket
{
    public LaunchTicket(string value, DateTimeOffset expiresAt)
    {
        Value = value;
        ExpiresAt = expiresAt;
    }

    public string Value { get; }

    public DateTimeOffset ExpiresAt { get; }
}
