using System;

namespace Emby.ExternalPlayer.Domain;

public sealed class CustomPlayerConfiguration
{
    public string Id { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string ApplicationName { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string[] Platforms { get; set; } = Array.Empty<string>();

    public string UrlTemplate { get; set; } = string.Empty;

    public bool EnablePlaybackReporting { get; set; }
}

public sealed class BuiltInPlayerPlatformConfiguration
{
    public string PlayerId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string[] Platforms { get; set; } = Array.Empty<string>();
}
