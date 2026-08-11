using System.Collections.Generic;

namespace Emby.ExternalPlayer.Domain;

public sealed class ExternalPlayerManifest
{
    public bool Enabled { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public string ButtonText { get; set; } = string.Empty;

    public string ButtonPlacement { get; set; } = string.Empty;

    public bool ResumeByDefault { get; set; }

    public long ResumePositionTicks { get; set; }

    public IReadOnlyCollection<MediaVersionDescriptor> MediaSources { get; set; } =
        new List<MediaVersionDescriptor>();

    public IReadOnlyCollection<PlayerApiDescriptor> Players { get; set; } =
        new List<PlayerApiDescriptor>();

    public Dictionary<string, string> Texts { get; set; } =
        new Dictionary<string, string>();
}

public sealed class MediaVersionDescriptor
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Container { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public IReadOnlyCollection<SubtitleDescriptor> Subtitles { get; set; } =
        new List<SubtitleDescriptor>();
}

public sealed class SubtitleDescriptor
{
    public int Index { get; set; }

    public string DisplayTitle { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}

public sealed class PlayerApiDescriptor
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsCustom { get; set; }

    public bool SupportsStartPosition { get; set; }

    public bool SupportsExternalSubtitle { get; set; }

    public bool SupportsDisplayTitle { get; set; }

    public IReadOnlyCollection<string> LaunchSchemes { get; set; } = new List<string>();
}

public sealed class LaunchResolution
{
    public string LaunchUrl { get; set; } = string.Empty;

    public string TicketExpiresAt { get; set; } = string.Empty;

    public IReadOnlyCollection<string> Warnings { get; set; } = new List<string>();
}
