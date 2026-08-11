using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;

namespace Emby.ExternalPlayer.Api;

[Route("/ExternalPlayer/Stream/{Ticket}/stream.js", "GET,HEAD", Summary = "Relays a video using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerStream
{
    public string Ticket { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Subtitle/{Ticket}/{Index}/subtitle.css", "GET,HEAD", Summary = "Relays an external subtitle using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerSubtitle
{
    public string Ticket { get; set; } = string.Empty;

    public int Index { get; set; }
}
