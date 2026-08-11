using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;

namespace Emby.ExternalPlayer.Api;

[Route("/ExternalPlayer/Stream/{Ticket}/stream.{Extension}", "GET,HEAD", Summary = "Relays a video using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerStream
{
    public string Ticket { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Subtitle/{Ticket}/{Index}.{Extension}", "GET,HEAD", Summary = "Relays an external subtitle using a short-lived playback ticket")]
[Unauthenticated]
public sealed class GetExternalPlayerSubtitle
{
    public string Ticket { get; set; } = string.Empty;

    public int Index { get; set; }

    public string Extension { get; set; } = string.Empty;
}
