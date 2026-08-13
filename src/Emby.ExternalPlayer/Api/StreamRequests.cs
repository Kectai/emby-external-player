using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;

namespace Emby.ExternalPlayer.Api;

[Route("/ExternalPlayer/Stream/{FileName}", "GET,HEAD", Summary = "Relays a titled video using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerStream
{
    public string FileName { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Subtitle/{Index}/{FileName}", "GET,HEAD", Summary = "Relays an external subtitle using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerSubtitle
{
    public int Index { get; set; }

    public string FileName { get; set; } = string.Empty;
}
