using System;
using System.Collections.Generic;

namespace Emby.ExternalPlayer.Domain;

[Flags]
public enum PlayerCapabilities
{
    None = 0,
    StartPosition = 1,
    ExternalSubtitle = 2,
}

public sealed class PlayerDescriptor
{
    public PlayerDescriptor(
        PlayerId id,
        string displayName,
        IReadOnlyCollection<ClientPlatform> platforms,
        PlayerCapabilities capabilities)
    {
        Id = id;
        DisplayName = displayName;
        Platforms = platforms;
        Capabilities = capabilities;
    }

    public PlayerId Id { get; }

    public string DisplayName { get; }

    public IReadOnlyCollection<ClientPlatform> Platforms { get; }

    public PlayerCapabilities Capabilities { get; }
}
