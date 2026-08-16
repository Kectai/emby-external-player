using System;
using System.Collections.Generic;
using System.Globalization;
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

    public object Get(GetCustomPlayerConfigurations request)
    {
        RequireAdministrator();
        return Plugin.Instance?.GetCustomPlayerConfigurations().ToArray()
            ?? Array.Empty<CustomPlayerConfiguration>();
    }

    public object Get(GetBuiltInPlayerPlatformConfigurations request)
    {
        RequireAdministrator();
        return Plugin.Instance?.GetBuiltInPlayerPlatformConfigurations().ToArray()
            ?? Array.Empty<BuiltInPlayerPlatformConfiguration>();
    }

    public object Post(SaveBuiltInPlayerPlatformConfiguration request)
    {
        RequireAdministrator();
        return (Plugin.Instance
                ?? throw new InvalidOperationException("The External Player plugin is unavailable."))
            .SaveBuiltInPlayerPlatformConfiguration(request.PlayerId, request.Platforms, request.Enabled);
    }

    public object Post(SaveCustomPlayerConfiguration request)
    {
        RequireAdministrator();
        var plugin = Plugin.Instance
            ?? throw new InvalidOperationException("The External Player plugin is unavailable.");
        return plugin.SaveCustomPlayerConfiguration(new CustomPlayerConfiguration
        {
            Id = request.Id,
            Enabled = request.Enabled,
            ApplicationName = request.ApplicationName,
            Platform = request.Platform,
            Platforms = request.Platforms,
            UrlTemplate = request.UrlTemplate,
            EnablePlaybackReporting = request.EnablePlaybackReporting,
        });
    }

    public object Delete(DeleteCustomPlayerConfiguration request)
    {
        RequireAdministrator();
        var plugin = Plugin.Instance
            ?? throw new InvalidOperationException("The External Player plugin is unavailable.");
        return new
        {
            Deleted = plugin.DeleteCustomPlayerConfiguration(request.Id),
        };
    }

    public object Get(GetExternalPlayerManifest request)
    {
        var options = GetOptions();
        EnsureEnabled(options);
        var user = GetAuthenticatedUser();
        var context = manifestService.GetContext(request.ItemId, user);
        var platform = ParsePlatform(request.Platform);
        var defaultPlayer = options.GetDefaultPlayer(platform);
        var texts = PluginStrings.GetWebStrings(request.Language);
        var availablePlayers = GetRuntime().Players
            .GetAvailable(options, platform, options.ShowOnlyPlatformPlayers)
            .OrderBy(player => player.BuiltInId == defaultPlayer ? 0 : 1)
            .ToArray();
        var storedUserDefault = Plugin.Instance?.GetUserDefaultPlayer(user.Id, platform);
        var userDefault = availablePlayers.FirstOrDefault(player => string.Equals(
            player.Id.ToString(),
            storedUserDefault,
            StringComparison.OrdinalIgnoreCase));
        var administratorDefault = availablePlayers.FirstOrDefault(player =>
            player.BuiltInId == defaultPlayer) ?? availablePlayers.FirstOrDefault();
        var effectiveDefault = userDefault ?? administratorDefault;

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
            DefaultPlayerId = effectiveDefault?.Id.ToString() ?? string.Empty,
            MediaSources = MediaManifestService.MapMediaSources(context),
            Players = availablePlayers
                .Select(ToApiDescriptor)
                .ToArray(),
            Texts = texts,
        };
    }

    public object Post(SaveUserDefaultPlayerPreference request)
    {
        var options = GetOptions();
        EnsureEnabled(options);
        var user = GetAuthenticatedUser();
        var platform = ParsePlatform(request.Platform);
        if (platform == ClientPlatform.Unknown)
        {
            throw new ArgumentException("A supported client platform is required.", nameof(request.Platform));
        }

        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            throw new ArgumentException("A default player is required.", nameof(request.PlayerId));
        }

        var savedPlayerId = Plugin.Instance?.SaveUserDefaultPlayer(
            user.Id,
            platform,
            request.PlayerId)
            ?? throw new InvalidOperationException("The External Player plugin is unavailable.");
        return new UserDefaultPlayerPreference
        {
            Platform = platform.ToString(),
            PlayerId = savedPlayerId,
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
        if (selection.MediaSource.Protocol != MediaProtocol.File)
        {
            throw new ArgumentException(
                "The secure relay supports local file media sources only.");
        }

        var mediaFile = RequireAvailableFile(selection.MediaSource.Path);
        var supportsExternalSubtitle =
            (selection.Player.Capabilities & PlayerCapabilities.ExternalSubtitle) != 0;
        var attachSubtitle = selection.Subtitle is not null && supportsExternalSubtitle;
        FileInfo? subtitleFile = null;
        if (attachSubtitle)
        {
            if (selection.Subtitle!.Protocol != MediaProtocol.File)
            {
                throw new ArgumentException(
                    "The secure relay supports local external subtitle files only.");
            }

            subtitleFile = RequireAvailableFile(selection.Subtitle.Path);
        }

        var lifetime = LaunchTicketStore.CreateLifetime(options.TicketLifetimeMinutes);
        var urlFileName = SafeFileNamePolicy.CreateUrlTitle(context.Item.Name);
        var mediaFormat = ServerUrlBuilder.NormalizeExtension(selection.MediaSource.Container, "mkv");
        var useHeaderTickets =
            (selection.Player.Capabilities & PlayerCapabilities.HttpRequestHeaders) != 0;
        var enablePlaybackReporting = useHeaderTickets &&
            (selection.Player.Capabilities & PlayerCapabilities.PlaybackReporting) != 0;
        PlaybackReportTicket? progressTicket = null;
        if (enablePlaybackReporting && runtime.PlaybackReports.Enabled)
        {
            try
            {
                progressTicket = runtime.PlaybackReportTickets.Issue(new PlaybackReportGrant
                {
                    UserId = user.Id,
                    ItemId = context.Item.Id,
                    CanonicalItemId = context.Item.Id,
                    MediaSourceId = selection.MediaSource.Id,
                    RunTimeTicks = selection.MediaSource.RunTimeTicks ?? context.Item.RunTimeTicks ?? 0,
                    PlayerName = selection.Player.DisplayName,
                    ClientAddress = Request.RemoteIp?.ToString() ?? string.Empty,
                }, lifetime);
                if (!runtime.PlaybackReports.Enabled)
                {
                    runtime.PlaybackReportTickets.Revoke(progressTicket.Value);
                    progressTicket = null;
                }
            }
            catch (InvalidOperationException)
            {
                // Reporting is an optional capability. Capacity pressure must
                // never prevent the media relay itself from being launched.
                progressTicket = null;
            }
        }
        var ticketPayloads = new List<LaunchTicketPayload>
        {
            new LaunchTicketPayload
            {
                LaunchId = progressTicket?.LaunchId ?? string.Empty,
                Scope = LaunchTicketScope.Media,
                UserId = user.Id,
                ItemId = context.Item.Id,
                MediaSourceId = selection.MediaSource.Id,
                FilePath = mediaFile.FullName,
                ContentLength = mediaFile.Length,
                LastWriteTimeUtcTicks = mediaFile.LastWriteTimeUtc.Ticks,
                ContentType = MimeTypes.GetMimeType("stream." + mediaFormat),
                SafeFileName = SafeFileNamePolicy.CreateGeneric(mediaFormat),
                UrlFileName = urlFileName,
            },
        };

        var subtitleFormat = string.Empty;
        var subtitleFileName = string.Empty;
        if (attachSubtitle && selection.Subtitle is not null && subtitleFile is not null)
        {
            subtitleFormat = ServerUrlBuilder.NormalizeExtension(selection.Subtitle.Codec, "srt");
            subtitleFileName = SafeFileNamePolicy.CreateFileName(
                subtitleFile.FullName,
                subtitleFormat,
                "subtitle");
            ticketPayloads.Add(new LaunchTicketPayload
            {
                Scope = LaunchTicketScope.Subtitle,
                UserId = user.Id,
                ItemId = context.Item.Id,
                MediaSourceId = selection.MediaSource.Id,
                FilePath = subtitleFile.FullName,
                SubtitleStreamIndex = selection.Subtitle.Index,
                ContentLength = subtitleFile.Length,
                LastWriteTimeUtcTicks = subtitleFile.LastWriteTimeUtc.Ticks,
                ContentType = MimeTypes.GetMimeType("subtitle." + subtitleFormat),
                SafeFileName = subtitleFileName,
                UrlFileName = subtitleFileName,
                SubtitleFormat = subtitleFormat,
            });
        }

        IReadOnlyList<LaunchTicket> issuedTickets;
        try
        {
            issuedTickets = runtime.Tickets.IssueBatch(ticketPayloads, lifetime);
        }
        catch
        {
            if (progressTicket is not null)
            {
                runtime.PlaybackReportTickets.Revoke(progressTicket.Value);
            }
            throw;
        }
        var mediaTicket = issuedTickets[0];
        var streamRequestHeaders = new List<string>();

        string streamUrl;
        if (useHeaderTickets)
        {
            streamUrl = progressTicket is null
                ? ServerUrlBuilder.BuildHeaderTicketStreamUrl(publicApiBase, urlFileName)
                : ServerUrlBuilder.BuildHeaderTicketStreamUrl(
                    publicApiBase,
                    progressTicket.LaunchId,
                    urlFileName);
            streamRequestHeaders.Add(
                ServerUrlBuilder.PlaybackTicketHeaderName + ": " + mediaTicket.Value);
            if (progressTicket is not null)
            {
                streamRequestHeaders.Add(
                    ServerUrlBuilder.ProgressTicketHeaderName + ": " + progressTicket.Value);
                streamRequestHeaders.Add(
                    ServerUrlBuilder.ProgressProtocolHeaderName + ": " +
                    PlaybackReportCoordinator.ProtocolVersion.ToString(CultureInfo.InvariantCulture));
                streamRequestHeaders.Add(
                    ServerUrlBuilder.ProgressExpiresHeaderName + ": " +
                    progressTicket.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
            }
        }
        else
        {
            streamUrl = ServerUrlBuilder.BuildTicketStreamUrl(
                publicApiBase,
                mediaTicket.Value,
                urlFileName);
        }

        string? subtitleUrl = null;
        if (attachSubtitle && selection.Subtitle is not null && subtitleFile is not null)
        {
            if (useHeaderTickets)
            {
                subtitleUrl = ServerUrlBuilder.BuildHeaderTicketSubtitleUrl(
                    publicApiBase,
                    selection.Subtitle.Index,
                    subtitleFormat,
                    subtitleFileName);
                streamRequestHeaders.Add(
                    ServerUrlBuilder.SubtitleTicketHeaderName + ": " + issuedTickets[1].Value);
            }
            else
            {
                subtitleUrl = ServerUrlBuilder.BuildTicketSubtitleUrl(
                    publicApiBase,
                    issuedTickets[1].Value,
                    selection.Subtitle.Index,
                    subtitleFormat,
                    subtitleFileName);
            }
        }

        var launchUrl = runtime.Players.BuildLaunchUrl(selection.PlayerId, options, new PlayerLaunchContext
        {
            StreamUrl = streamUrl,
            SubtitleUrl = subtitleUrl,
            Title = context.Item.Name,
            HttpRequestHeaders = streamRequestHeaders,
            StartPositionTicks = request.Resume ? context.ResumePositionTicks : 0,
            Platform = platform,
        });

        var texts = PluginStrings.GetWebStrings(request.Language);
        var warnings = new List<string>();
        if (selection.Subtitle is not null && !supportsExternalSubtitle)
        {
            warnings.Add(string.Format(
                CultureInfo.CurrentCulture,
                texts[nameof(PluginStrings.SubtitleMayNotLoadForPlayer)],
                selection.Player.DisplayName));
        }
        if (request.Resume && context.ResumePositionTicks > 0 &&
            (selection.Player.Capabilities & PlayerCapabilities.StartPosition) == 0)
        {
            warnings.Add(texts[nameof(PluginStrings.ResumeUnsupportedWarning)]);
        }

        return new LaunchResolution
        {
            LaunchUrl = launchUrl,
            TicketExpiresAt = mediaTicket.ExpiresAt.ToString("O"),
            Warnings = warnings,
            PlaybackReporting = progressTicket is null
                ? null
                : new PlaybackReportingCapability
                {
                    ProtocolVersion = PlaybackReportCoordinator.ProtocolVersion,
                    HeartbeatSeconds = PlaybackReportCoordinator.HeartbeatSeconds,
                    TicketExpiresAtUtc = progressTicket.ExpiresAtUtc.ToString(
                        "O",
                        CultureInfo.InvariantCulture),
                },
        };
    }

    private MediaBrowser.Controller.Entities.User GetAuthenticatedUser()
    {
        return authorizationContext.GetAuthorizationInfo(Request).User
            ?? throw new UnauthorizedAccessException("An authenticated Emby user is required.");
    }

    private MediaBrowser.Controller.Entities.User RequireAdministrator()
    {
        var user = GetAuthenticatedUser();
        if (!user.Policy.IsAdministrator)
        {
            throw new UnauthorizedAccessException("An Emby administrator is required.");
        }
        return user;
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
            IsCustom = !descriptor.BuiltInId.HasValue,
            SupportsStartPosition = (descriptor.Capabilities & PlayerCapabilities.StartPosition) != 0,
            SupportsExternalSubtitle = (descriptor.Capabilities & PlayerCapabilities.ExternalSubtitle) != 0,
            LaunchSchemes = descriptor.LaunchSchemes,
        };
    }

    private static ClientPlatform ParsePlatform(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            Enum.TryParse(value, ignoreCase: true, out ClientPlatform platform) &&
            Enum.IsDefined(typeof(ClientPlatform), platform)
                ? platform
                : ClientPlatform.Unknown;
    }

}
