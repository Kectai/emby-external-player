using System;

namespace Emby.ExternalPlayer.Domain;

public sealed class LaunchTicketPayload
{
    public Guid UserId { get; set; }

    public Guid ItemId { get; set; }

    public string MediaSourceId { get; set; } = string.Empty;

    public string MediaFilePath { get; set; } = string.Empty;

    public string? SubtitleFilePath { get; set; }

    public int? SubtitleStreamIndex { get; set; }

    public long ContentLength { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public string SafeFileName { get; set; } = "media.bin";

    public string UrlFileName { get; set; } = "media";

    public string SubtitleContentType { get; set; } = "text/plain; charset=utf-8";

    public long? SubtitleContentLength { get; set; }

    public string? SafeSubtitleFileName { get; set; }

    public long StartPositionTicks { get; set; }
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
