using System;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

[Route("/ExternalPlayer/Manifest", "GET", Summary = "Gets external-player choices for an Emby video")]
[Authenticated]
public sealed class GetExternalPlayerManifest : IReturn<Domain.ExternalPlayerManifest>
{
    public string ItemId { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;
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

    public string Language { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/UserDefaultPlayer", "POST", Summary = "Sets the authenticated user's default external player for a platform")]
[Authenticated]
public sealed class SaveUserDefaultPlayerPreference : IReturn<Domain.UserDefaultPlayerPreference>
{
    public string Platform { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/CustomPlayers", "GET", Summary = "Gets custom external-player configurations")]
[Authenticated]
public sealed class GetCustomPlayerConfigurations : IReturn<Domain.CustomPlayerConfiguration[]>
{
}

[Route("/ExternalPlayer/BuiltInPlayerPlatforms", "GET", Summary = "Gets built-in player platform configurations")]
[Authenticated]
public sealed class GetBuiltInPlayerPlatformConfigurations :
    IReturn<Domain.BuiltInPlayerPlatformConfiguration[]>
{
}

[Route("/ExternalPlayer/BuiltInPlayerPlatforms", "POST", Summary = "Updates a built-in player's platforms")]
[Authenticated]
public sealed class SaveBuiltInPlayerPlatformConfiguration :
    IReturn<Domain.BuiltInPlayerPlatformConfiguration>
{
    public string PlayerId { get; set; } = string.Empty;

    public string[] Platforms { get; set; } = Array.Empty<string>();
}

[Route("/ExternalPlayer/CustomPlayers", "POST", Summary = "Creates or updates a custom external-player configuration")]
[Authenticated]
public sealed class SaveCustomPlayerConfiguration : IReturn<Domain.CustomPlayerConfiguration>
{
    public string Id { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string ApplicationName { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string[] Platforms { get; set; } = Array.Empty<string>();

    public string UrlTemplate { get; set; } = string.Empty;

    public bool EnablePlaybackReporting { get; set; }
}

[Route("/ExternalPlayer/CustomPlayers/{Id}", "DELETE", Summary = "Deletes a custom external-player configuration")]
[Authenticated]
public sealed class DeleteCustomPlayerConfiguration
{
    public string Id { get; set; } = string.Empty;
}

[Route("/ExternalPlayer/Playback/Start", "POST", Summary = "Starts an external-player playback reporting session")]
[Unauthenticated]
public sealed class ReportExternalPlaybackStart : Domain.PlaybackReportRequest,
    IReturn<Domain.PlaybackReportResponse>
{
}

[Route("/ExternalPlayer/Playback/Progress", "POST", Summary = "Updates an external-player playback reporting session")]
[Unauthenticated]
public sealed class ReportExternalPlaybackProgress : Domain.PlaybackReportRequest,
    IReturn<Domain.PlaybackReportResponse>
{
}

[Route("/ExternalPlayer/Playback/Stop", "POST", Summary = "Stops an external-player playback reporting session")]
[Unauthenticated]
public sealed class ReportExternalPlaybackStop : Domain.PlaybackReportRequest,
    IReturn<Domain.PlaybackReportResponse>
{
}
