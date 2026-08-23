using MediaBrowser.Model.Services;
using MediaBrowser.Controller.Net;

namespace Emby.ExternalPlayer.Api;

[Route("/{WebRoot}/modules/embyexternalplayer/plugin.js", "GET", Summary = "Gets the External Player Web module")]
[Unauthenticated]
public sealed class GetExternalPlayerWebModule
{
    public string WebRoot { get; set; } = string.Empty;
}

[Route("/{WebRoot}/modules/embyexternalplayer/bootstrap.js", "GET", Summary = "Gets the External Player fail-open bootstrap")]
[Unauthenticated]
public sealed class GetExternalPlayerBootstrap
{
    public string WebRoot { get; set; } = string.Empty;
}

[Route("/{WebRoot}/modules/embyexternalplayer/language.js", "GET", Summary = "Gets the External Player language module")]
[Unauthenticated]
public sealed class GetExternalPlayerLanguageModule
{
    public string WebRoot { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Web/style.css", "GET", Summary = "Gets the External Player stylesheet")]
[Unauthenticated]
public sealed class GetExternalPlayerStylesheet
{
}
