using System;
using System.Collections.Generic;

namespace Emby.ExternalPlayer.Domain;

[Flags]
public enum PlayerCapabilities
{
    None = 0,
    StartPosition = 1,
    ExternalSubtitle = 2,
    HttpRequestHeaders = 4,
    PlaybackReporting = 8,
}

public sealed class PlayerDescriptor
{
    public PlayerDescriptor(
        PlayerId id,
        string displayName,
        IReadOnlyCollection<ClientPlatform> platforms,
        PlayerCapabilities capabilities,
        IReadOnlyCollection<string> launchSchemes)
        : this(id.ToString(), id, displayName, platforms, capabilities, launchSchemes)
    {
    }

    public PlayerDescriptor(
        string id,
        string displayName,
        IReadOnlyCollection<ClientPlatform> platforms,
        PlayerCapabilities capabilities,
        IReadOnlyCollection<string> launchSchemes)
        : this(id, null, displayName, platforms, capabilities, launchSchemes)
    {
    }

    private PlayerDescriptor(
        string id,
        PlayerId? builtInId,
        string displayName,
        IReadOnlyCollection<ClientPlatform> platforms,
        PlayerCapabilities capabilities,
        IReadOnlyCollection<string> launchSchemes)
    {
        Id = id;
        BuiltInId = builtInId;
        DisplayName = displayName;
        Platforms = platforms;
        Capabilities = capabilities;
        LaunchSchemes = launchSchemes;
    }

    public string Id { get; }

    public PlayerId? BuiltInId { get; }

    public string DisplayName { get; }

    public IReadOnlyCollection<ClientPlatform> Platforms { get; }

    public PlayerCapabilities Capabilities { get; }

    public IReadOnlyCollection<string> LaunchSchemes { get; }
}
