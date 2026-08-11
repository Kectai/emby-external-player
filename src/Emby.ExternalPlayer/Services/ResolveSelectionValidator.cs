using System;
using System.Collections.Generic;
using System.Linq;
using Emby.ExternalPlayer.Domain;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Emby.ExternalPlayer.Services;

public sealed class ResolveSelection
{
    public PlayerId PlayerId { get; set; }

    public PlayerDescriptor Player { get; set; } = null!;

    public MediaSourceInfo MediaSource { get; set; } = null!;

    public MediaStream? Subtitle { get; set; }
}

public static class ResolveSelectionValidator
{
    public static ResolveSelection Validate(
        PluginOptions options,
        PlayerAdapterRegistry players,
        MediaManifestContext context,
        string playerIdValue,
        ClientPlatform platform,
        string mediaSourceId,
        int? subtitleStreamIndex)
    {
        if (!Enum.TryParse(playerIdValue, ignoreCase: true, out PlayerId playerId) ||
            !Enum.IsDefined(typeof(PlayerId), playerId))
        {
            throw new ArgumentException("Unknown player id.", nameof(playerIdValue));
        }

        var player = players
            .GetAvailable(options, platform, options.ShowOnlyPlatformPlayers)
            .FirstOrDefault(candidate => candidate.Id == playerId);
        if (player is null)
        {
            throw new ArgumentException(
                "The requested player is disabled or unavailable on this platform.",
                nameof(playerIdValue));
        }

        if (string.IsNullOrWhiteSpace(mediaSourceId))
        {
            throw new ArgumentException("A media source is required.", nameof(mediaSourceId));
        }

        var mediaSource = context.MediaSources.FirstOrDefault(source =>
            string.Equals(source.Id, mediaSourceId, StringComparison.Ordinal));
        if (mediaSource is null)
        {
            throw new ArgumentException("The selected media source is not available.", nameof(mediaSourceId));
        }

        MediaStream? subtitle = null;
        if (subtitleStreamIndex.HasValue)
        {
            subtitle = (mediaSource.MediaStreams ?? new List<MediaStream>())
                .FirstOrDefault(stream =>
                    stream.Type == MediaStreamType.Subtitle &&
                    stream.IsExternal &&
                    stream.Index == subtitleStreamIndex.Value);
            if (subtitle is null)
            {
                throw new ArgumentException(
                    "The selected external subtitle is not available.",
                    nameof(subtitleStreamIndex));
            }
        }

        return new ResolveSelection
        {
            PlayerId = playerId,
            Player = player,
            MediaSource = mediaSource,
            Subtitle = subtitle,
        };
    }
}
