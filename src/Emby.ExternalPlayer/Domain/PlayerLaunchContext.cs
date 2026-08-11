using System.Collections.Generic;

namespace Emby.ExternalPlayer.Domain;

public sealed class PlayerLaunchContext
{
    public string StreamUrl { get; set; } = string.Empty;

    public string? SubtitleUrl { get; set; }

    public string? Title { get; set; }

    public IReadOnlyCollection<string> HttpRequestHeaders { get; set; } = new List<string>();

    public long StartPositionTicks { get; set; }

    public ClientPlatform Platform { get; set; }
}
