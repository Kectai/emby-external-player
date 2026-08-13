using System;
using System.Collections.Generic;
using System.Linq;
using Emby.ExternalPlayer.Domain;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Emby.ExternalPlayer.Services;

public sealed class MediaManifestContext
{
    public BaseItem Item { get; set; } = null!;

    public User User { get; set; } = null!;

    public IReadOnlyCollection<MediaSourceInfo> MediaSources { get; set; } =
        new List<MediaSourceInfo>();

    public long ResumePositionTicks { get; set; }
}

public sealed class MediaManifestService
{
    private readonly ILibraryManager libraryManager;
    private readonly IMediaSourceManager mediaSourceManager;
    private readonly IUserDataManager userDataManager;

    public MediaManifestService(
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IUserDataManager userDataManager)
    {
        this.libraryManager = libraryManager;
        this.mediaSourceManager = mediaSourceManager;
        this.userDataManager = userDataManager;
    }

    public MediaManifestContext GetContext(string itemId, User user)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item id is required.", nameof(itemId));
        }

        if (user is null)
        {
            throw new UnauthorizedAccessException("An authenticated Emby user is required.");
        }

        var internalId = libraryManager.GetInternalId(itemId);
        var item = libraryManager.GetItemById(internalId);
        if (!(item is Video) || !item.IsVisible(user) || !user.Policy.EnableMediaPlayback)
        {
            throw new ResourceNotFoundException("The requested playable video was not found.");
        }

        var sources = mediaSourceManager.GetStaticMediaSources(
            item,
            enablePathSubstitution: false,
            fillChapters: false,
            deviceProfile: null,
            user: user);

        var userData = userDataManager.GetUserData(user, item);
        return new MediaManifestContext
        {
            Item = item,
            User = user,
            MediaSources = sources,
            ResumePositionTicks = ResumePositionPolicy.FromEmbyUserData(
                userData?.PlaybackPositionTicks ?? 0),
        };
    }

    public static IReadOnlyCollection<MediaVersionDescriptor> MapMediaSources(MediaManifestContext context)
    {
        var defaultSourceId = context.Item.GetDefaultMediaSourceId();
        return context.MediaSources.Select((source, index) => new MediaVersionDescriptor
        {
            Id = source.Id ?? string.Empty,
            Name = string.IsNullOrWhiteSpace(source.Name)
                ? "Version " + (index + 1)
                : source.Name,
            IsDefault = !string.IsNullOrWhiteSpace(defaultSourceId)
                ? string.Equals(source.Id, defaultSourceId, StringComparison.Ordinal)
                : index == 0,
            Subtitles = MapSubtitles(source),
        }).ToArray();
    }

    public static IReadOnlyCollection<SubtitleDescriptor> MapSubtitles(MediaSourceInfo source)
    {
        return (source.MediaStreams ?? new List<MediaStream>())
            .Where(stream => stream.Type == MediaStreamType.Subtitle && stream.IsExternal)
            .Select(stream => new SubtitleDescriptor
            {
                Index = stream.Index,
                DisplayTitle = string.IsNullOrWhiteSpace(stream.DisplayTitle)
                    ? (string.IsNullOrWhiteSpace(stream.Language) ? "Subtitle " + stream.Index : stream.Language)
                    : stream.DisplayTitle,
                Language = stream.Language ?? string.Empty,
                Format = ServerUrlBuilder.NormalizeExtension(stream.Codec, "srt"),
                IsDefault = stream.IsDefault,
            })
            .ToArray();
    }
}
