using System;
using System.Collections.Generic;
using System.Linq;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class ExternalPlayerApiService : IService, IRequiresRequest
{
    private readonly IAuthorizationContext authorizationContext;
    private readonly MediaManifestService manifestService;

    public ExternalPlayerApiService(
        IAuthorizationContext authorizationContext,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IUserDataManager userDataManager)
    {
        this.authorizationContext = authorizationContext;
        manifestService = new MediaManifestService(libraryManager, mediaSourceManager, userDataManager);
    }

    public IRequest Request { get; set; } = null!;

    public object Get(GetExternalPlayerManifest request)
    {
        var options = GetOptions();
        EnsureEnabled(options);
        var user = GetAuthenticatedUser();
        var context = manifestService.GetContext(request.ItemId, user);
        var platform = ParsePlatform(request.Platform);

        return new ExternalPlayerManifest
        {
            Enabled = options.EnableWebButton,
            ItemId = context.Item.Id.ToString("N"),
            ItemName = context.Item.Name ?? string.Empty,
            ButtonText = options.ButtonText,
            ButtonPlacement = options.ButtonPlacement.ToString(),
            ResumeByDefault = options.ResumeByDefault,
            ResumePositionTicks = context.ResumePositionTicks,
            MediaSources = MediaManifestService.MapMediaSources(context),
            Players = GetRuntime().Players
                .GetAvailable(options, platform, options.ShowOnlyPlatformPlayers)
                .Select(ToApiDescriptor)
                .ToArray(),
        };
    }

    public object Post(ResolveExternalPlayer request)
    {
        var options = GetOptions();
        EnsureEnabled(options);
        var user = GetAuthenticatedUser();
        var context = manifestService.GetContext(request.ItemId, user);
        var platform = ParsePlatform(request.Platform);
        var playerId = ParsePlayer(request.PlayerId);
        var runtime = GetRuntime();

        var allowedPlayer = runtime.Players
            .GetAvailable(options, platform, options.ShowOnlyPlatformPlayers)
            .Any(player => player.Id == playerId);
        if (!allowedPlayer)
        {
            throw new ArgumentException("The requested player is disabled or unavailable on this platform.");
        }

        var mediaSource = context.MediaSources.FirstOrDefault(source =>
            string.Equals(source.Id, request.MediaSourceId, StringComparison.Ordinal));
        if (mediaSource is null)
        {
            throw new ArgumentException("The selected media source is not available.");
        }

        var apiBase = ServerUrlBuilder.GetApiBase(Request.AbsoluteUri, "ExternalPlayer/Resolve");
        var upstreamStreamUrl = ServerUrlBuilder.BuildDirectStreamUrl(
            apiBase,
            context.Item.Id,
            mediaSource.Id,
            mediaSource.Container);

        var subtitle = FindSubtitle(mediaSource, request.SubtitleStreamIndex);
        var upstreamSubtitleUrl = subtitle is null
            ? null
            : ServerUrlBuilder.BuildSubtitleUrl(
                apiBase,
                context.Item.Id,
                mediaSource.Id,
                subtitle.Index,
                subtitle.Codec);

        string streamUrl;
        string? subtitleUrl;
        DateTimeOffset? expiresAt = null;
        if (options.StreamMode == StreamMode.LegacyTokenUrl)
        {
            var token = authorizationContext.GetAuthorizationInfo(Request).Token;
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnauthorizedAccessException("The Emby access token is unavailable.");
            }

            streamUrl = ServerUrlBuilder.AppendApiKey(upstreamStreamUrl, token);
            subtitleUrl = upstreamSubtitleUrl is null
                ? null
                : ServerUrlBuilder.AppendApiKey(upstreamSubtitleUrl, token);
        }
        else
        {
            var authorization = authorizationContext.GetAuthorizationInfo(Request);
            var ticket = runtime.Tickets.Issue(
                new LaunchTicketPayload
                {
                    UserId = user.Id,
                    ItemId = context.Item.Id,
                    MediaSourceId = mediaSource.Id,
                    UpstreamUrl = upstreamStreamUrl,
                    SubtitleUpstreamUrl = upstreamSubtitleUrl,
                    SubtitleStreamIndex = subtitle?.Index,
                    AccessToken = authorization.Token,
                    ContentLength = mediaSource.Size,
                    ContentType = "video/" + ServerUrlBuilder.NormalizeExtension(mediaSource.Container, "mkv"),
                    StartPositionTicks = request.Resume ? context.ResumePositionTicks : 0,
                },
                TimeSpan.FromMinutes(options.TicketLifetimeMinutes));

            expiresAt = ticket.ExpiresAt;
            streamUrl = ServerUrlBuilder.BuildTicketStreamUrl(apiBase, ticket.Value, mediaSource.Container);
            subtitleUrl = subtitle is null
                ? null
                : ServerUrlBuilder.BuildTicketSubtitleUrl(
                    apiBase,
                    ticket.Value,
                    subtitle.Index,
                    subtitle.Codec);
        }

        var launchUrl = runtime.Players.BuildLaunchUrl(playerId, new PlayerLaunchContext
        {
            StreamUrl = streamUrl,
            SubtitleUrl = subtitleUrl,
            Title = context.Item.Name,
            StartPositionTicks = request.Resume ? context.ResumePositionTicks : 0,
        });

        return new LaunchResolution
        {
            LaunchUrl = launchUrl,
            ExpiresAt = expiresAt?.ToString("O") ?? string.Empty,
        };
    }

    private MediaBrowser.Controller.Entities.User GetAuthenticatedUser()
    {
        return authorizationContext.GetAuthorizationInfo(Request).User
            ?? throw new UnauthorizedAccessException("An authenticated Emby user is required.");
    }

    private static PluginOptions GetOptions() =>
        Plugin.Instance?.Options ?? new PluginOptions();

    private static PluginRuntime GetRuntime() =>
        Plugin.Runtime ?? throw new InvalidOperationException("The External Player runtime is unavailable.");

    private static void EnsureEnabled(PluginOptions options)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("External Player is disabled.");
        }
    }

    private static PlayerApiDescriptor ToApiDescriptor(PlayerDescriptor descriptor)
    {
        return new PlayerApiDescriptor
        {
            Id = descriptor.Id.ToString(),
            DisplayName = descriptor.DisplayName,
            SupportsStartPosition = (descriptor.Capabilities & PlayerCapabilities.StartPosition) != 0,
            SupportsExternalSubtitle = (descriptor.Capabilities & PlayerCapabilities.ExternalSubtitle) != 0,
        };
    }

    private static ClientPlatform ParsePlatform(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out ClientPlatform platform)
            ? platform
            : ClientPlatform.Unknown;
    }

    private static PlayerId ParsePlayer(string value)
    {
        if (!Enum.TryParse(value, ignoreCase: true, out PlayerId playerId))
        {
            throw new ArgumentException("Unknown player id.", nameof(value));
        }

        return playerId;
    }

    private static MediaBrowser.Model.Entities.MediaStream? FindSubtitle(
        MediaSourceInfo mediaSource,
        int? streamIndex)
    {
        if (!streamIndex.HasValue)
        {
            return null;
        }

        var subtitle = (mediaSource.MediaStreams ?? new List<MediaBrowser.Model.Entities.MediaStream>())
            .FirstOrDefault(stream =>
                stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Subtitle &&
                stream.IsExternal &&
                stream.Index == streamIndex.Value);
        return subtitle ?? throw new ArgumentException("The selected external subtitle is not available.");
    }
}
