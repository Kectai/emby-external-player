using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

[Route("/ExternalPlayer/Manifest", "GET", Summary = "Gets external-player choices for an Emby video")]
[Authenticated]
public sealed class GetExternalPlayerManifest : IReturn<Domain.ExternalPlayerManifest>
{
    public string ItemId { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Resolve", "POST", Summary = "Creates a short-lived external-player launch URL")]
[Authenticated]
public sealed class ResolveExternalPlayer : IReturn<Domain.LaunchResolution>
{
    public string ItemId { get; set; } = string.Empty;

    public string MediaSourceId { get; set; } = string.Empty;

    public int? SubtitleStreamIndex { get; set; }

    public bool Resume { get; set; }

    public string PlayerId { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;
}
