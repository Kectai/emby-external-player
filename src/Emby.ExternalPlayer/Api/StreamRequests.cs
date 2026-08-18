using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;

namespace Emby.ExternalPlayer.Api;

[Route("/ExternalPlayer/Stream/{FileName}", "GET,HEAD", Summary = "Relays a titled video using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerStream
{
    public string FileName { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Stream/{LaunchId}/{FileName}", "GET,HEAD", Summary = "Relays a titled video bound to an external-player launch")]
[Unauthenticated]
public sealed class GetExternalPlayerLaunchStream
{
    public string LaunchId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Subtitle/{Index}/{FileName}", "GET,HEAD", Summary = "Relays an external subtitle using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerSubtitle
{
    public int Index { get; set; }

    public string FileName { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Remote/{FileName}", "GET,HEAD", Summary = "Resolves an authorized STRM source without exposing its long-lived signature")]
[Unauthenticated]
public sealed class GetExternalPlayerRemoteStream
{
    public string FileName { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Remote/{LaunchId}/{FileName}", "GET,HEAD", Summary = "Resolves an authorized STRM source bound to a playback-reporting launch")]
[Unauthenticated]
public sealed class GetExternalPlayerRemoteLaunchStream
{
    public string LaunchId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
}
