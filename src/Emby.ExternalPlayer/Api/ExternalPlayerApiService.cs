using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Localization;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Services;
using MediaBrowser.Model.Net;

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
        var defaultPlayer = options.GetDefaultPlayer(platform);
        var texts = PluginStrings.GetWebStrings(request.Language);

        return new ExternalPlayerManifest
        {
            Enabled = options.EnableWebButton,
            ItemId = context.Item.Id.ToString("N"),
            ItemName = context.Item.Name ?? string.Empty,
            ButtonText = options.UseLocalizedButtonText
                ? texts[nameof(PluginStrings.ExternalPlay)]
                : options.ButtonText,
            ButtonPlacement = options.ButtonPlacement.ToString(),
            ResumeByDefault = options.ResumeByDefault,
            ResumePositionTicks = context.ResumePositionTicks,
            MediaSources = MediaManifestService.MapMediaSources(context),
            Players = GetRuntime().Players
                .GetAvailable(options, platform, options.ShowOnlyPlatformPlayers)
                .OrderBy(player => player.BuiltInId == defaultPlayer ? 0 : 1)
                .Select(ToApiDescriptor)
                .ToArray(),
            Texts = texts,
        };
    }

    public object Post(ResolveExternalPlayer request)
    {
        var options = GetOptions();
        EnsureEnabled(options);
        var user = GetAuthenticatedUser();
        var context = manifestService.GetContext(request.ItemId, user);
        var platform = ParsePlatform(request.Platform);
        var runtime = GetRuntime();
        var selection = ResolveSelectionValidator.Validate(
            options,
            runtime.Players,
            context,
            request.PlayerId,
            platform,
            request.MediaSourceId,
            request.SubtitleStreamIndex);

        var publicApiBase = ServerUrlBuilder.GetApiBase(Request.AbsoluteUri, "ExternalPlayer/Resolve");
        var directStreamUrl = ServerUrlBuilder.BuildDirectStreamUrl(
            publicApiBase,
            context.Item.Id,
            selection.MediaSource.Id,
            selection.MediaSource.Container);

        var directSubtitleUrl = selection.Subtitle is null
            ? null
            : ServerUrlBuilder.BuildSubtitleUrl(
                publicApiBase,
                context.Item.Id,
                selection.MediaSource.Id,
                selection.Subtitle.Index,
                selection.Subtitle.Codec);

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

            streamUrl = ServerUrlBuilder.AppendApiKey(directStreamUrl, token);
            subtitleUrl = directSubtitleUrl is null
                ? null
                : ServerUrlBuilder.AppendApiKey(directSubtitleUrl, token);
        }
        else
        {
            if (selection.MediaSource.Protocol != MediaProtocol.File)
            {
                throw new ArgumentException(
                    "SecureTicketRelay supports local file media sources only. Use LegacyTokenUrl for this source.");
            }

            var mediaFile = RequireAvailableFile(selection.MediaSource.Path);
            FileInfo? subtitleFile = null;
            if (selection.Subtitle is not null)
            {
                if (selection.Subtitle.Protocol != MediaProtocol.File)
                {
                    throw new ArgumentException(
                        "SecureTicketRelay supports local external subtitle files only.");
                }

                subtitleFile = RequireAvailableFile(selection.Subtitle.Path);
            }

            var ticket = runtime.Tickets.Issue(
                new LaunchTicketPayload
                {
                    UserId = user.Id,
                    ItemId = context.Item.Id,
                    MediaSourceId = selection.MediaSource.Id,
                    MediaFilePath = mediaFile.FullName,
                    SubtitleFilePath = subtitleFile?.FullName,
                    SubtitleStreamIndex = selection.Subtitle?.Index,
                    ContentLength = mediaFile.Length,
                    ContentType = MimeTypes.GetMimeType(
                        "stream." + ServerUrlBuilder.NormalizeExtension(selection.MediaSource.Container, "mkv")),
                    SafeFileName = SafeFileNamePolicy.Create(
                        mediaFile.Name,
                        selection.MediaSource.Container),
                    SubtitleContentType = selection.Subtitle is null
                        ? "text/plain; charset=utf-8"
                        : MimeTypes.GetMimeType(
                            "subtitle." + ServerUrlBuilder.NormalizeExtension(selection.Subtitle.Codec, "srt")),
                    SubtitleContentLength = subtitleFile?.Length,
                    SafeSubtitleFileName = selection.Subtitle is null
                        ? null
                        : SafeFileNamePolicy.Create(subtitleFile?.Name, selection.Subtitle.Codec),
                    StartPositionTicks = request.Resume ? context.ResumePositionTicks : 0,
                },
                TimeSpan.FromMinutes(options.TicketLifetimeMinutes));

            expiresAt = ticket.ExpiresAt;
            streamUrl = ServerUrlBuilder.BuildTicketStreamUrl(
                publicApiBase,
                ticket.Value,
                selection.MediaSource.Container);
            subtitleUrl = selection.Subtitle is null
                ? null
                : ServerUrlBuilder.BuildTicketSubtitleUrl(
                    publicApiBase,
                    ticket.Value,
                    selection.Subtitle.Index,
                    selection.Subtitle.Codec);
        }

        var launchUrl = runtime.Players.BuildLaunchUrl(selection.PlayerId, options, new PlayerLaunchContext
        {
            StreamUrl = streamUrl,
            SubtitleUrl = subtitleUrl,
            Title = context.Item.Name,
            StartPositionTicks = request.Resume ? context.ResumePositionTicks : 0,
            Platform = platform,
        });

        var texts = PluginStrings.GetWebStrings(request.Language);
        var warnings = new List<string>();
        if (request.Resume && context.ResumePositionTicks > 0 &&
            (selection.Player.Capabilities & PlayerCapabilities.StartPosition) == 0)
        {
            warnings.Add(texts[nameof(PluginStrings.ResumeUnsupportedWarning)]);
        }

        if (selection.Subtitle is not null &&
            (selection.Player.Capabilities & PlayerCapabilities.ExternalSubtitle) == 0)
        {
            warnings.Add(texts[nameof(PluginStrings.SubtitleUnsupportedWarning)]);
        }

        return new LaunchResolution
        {
            LaunchUrl = launchUrl,
            TicketExpiresAt = expiresAt?.ToString("O") ?? string.Empty,
            Warnings = warnings,
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

    private static FileInfo RequireAvailableFile(string? path)
    {
        try
        {
            return LocalMediaFilePolicy.RequireExistingFile(path);
        }
        catch (InvalidOperationException)
        {
            throw new ResourceNotFoundException("The selected local media file is unavailable.");
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
            SupportsDisplayTitle = (descriptor.Capabilities & PlayerCapabilities.DisplayTitle) != 0,
            LaunchSchemes = descriptor.LaunchSchemes,
        };
    }

    private static ClientPlatform ParsePlatform(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out ClientPlatform platform)
            ? platform
            : ClientPlatform.Unknown;
    }

}
