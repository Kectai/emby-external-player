using System;

namespace Emby.ExternalPlayer.Domain;

public sealed class LaunchTicketPayload
{
    public Guid UserId { get; set; }

    public Guid ItemId { get; set; }

    public string MediaSourceId { get; set; } = string.Empty;

    public string UpstreamUrl { get; set; } = string.Empty;

    public string? SubtitleUpstreamUrl { get; set; }

    public int? SubtitleStreamIndex { get; set; }

    public string? AccessToken { get; set; }

    public long? ContentLength { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

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
