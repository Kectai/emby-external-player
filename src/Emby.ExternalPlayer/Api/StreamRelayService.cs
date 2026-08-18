using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class StreamRelayService : IService, IRequiresRequest
{
    private readonly IHttpResultFactory resultFactory;
    private readonly ILibraryManager libraryManager;
    private readonly ILogger logger;
    private readonly IMediaSourceManager mediaSourceManager;
    private readonly IUserManager userManager;

    public StreamRelayService(
        IHttpResultFactory resultFactory,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IUserManager userManager,
        ILogManager logManager)
    {
        this.resultFactory = resultFactory;
        this.libraryManager = libraryManager;
        this.mediaSourceManager = mediaSourceManager;
        this.userManager = userManager;
        logger = logManager.GetLogger(Plugin.Instance?.Name ?? "External Player");
    }

    public IRequest Request { get; set; } = null!;

    public Task<object> Get(GetExternalPlayerStream request)
    {
        return GetStream(request.FileName, launchId: null);
    }

    public Task<object> Get(GetExternalPlayerLaunchStream request)
    {
        return GetStream(request.FileName, request.LaunchId);
    }

    private Task<object> GetStream(string fileName, string? launchId)
    {
        var rawTicket = Request.Headers[ServerUrlBuilder.PlaybackTicketHeaderName] ??
            Request.QueryString["api_key"] ?? string.Empty;
        var payload = GetTicket(rawTicket, LaunchTicketScope.Media);
        if (!string.Equals(payload.LaunchId, launchId ?? string.Empty, StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(fileName) &&
             !string.Equals(fileName, payload.UrlFileName, StringComparison.Ordinal)))
        {
            throw new ResourceNotFoundException("The playback launch path does not match the ticket.");
        }
        return CreateFileResult(
            payload.FilePath,
            payload.ContentType,
            payload.SafeFileName,
            payload.ContentLength,
            payload.LastWriteTimeUtcTicks);
    }

    public Task<object> Head(GetExternalPlayerStream request) => Get(request);

    public Task<object> Head(GetExternalPlayerLaunchStream request) => Get(request);

    public Task<object> Get(GetExternalPlayerSubtitle request)
    {
        var rawTicket = Request.Headers[ServerUrlBuilder.SubtitleTicketHeaderName] ??
            Request.QueryString["api_key"] ?? string.Empty;
        var payload = GetTicket(rawTicket, LaunchTicketScope.Subtitle);
        if (payload.SubtitleStreamIndex != request.Index)
        {
            throw new ResourceNotFoundException("The subtitle playback ticket is invalid.");
        }

        if (!string.Equals(request.FileName, payload.UrlFileName, StringComparison.Ordinal))
        {
            throw new ResourceNotFoundException("The subtitle file name does not match the ticket.");
        }

        return CreateFileResult(
            payload.FilePath,
            payload.ContentType,
            payload.SafeFileName,
            payload.ContentLength,
            payload.LastWriteTimeUtcTicks);
    }

    public Task<object> Head(GetExternalPlayerSubtitle request) => Get(request);

    public async Task<object> Get(GetExternalPlayerRemoteStream request)
    {
        return await GetRemoteStream(request.FileName, launchId: null).ConfigureAwait(false);
    }

    public async Task<object> Get(GetExternalPlayerRemoteLaunchStream request)
    {
        return await GetRemoteStream(request.FileName, request.LaunchId).ConfigureAwait(false);
    }

    private async Task<object> GetRemoteStream(string fileName, string? launchId)
    {
        var rawTicket = Request.QueryString["api_key"] ?? string.Empty;
        if (!RemoteStreamPolicy.TryParseLaunchToken(rawTicket, out var credentials))
        {
            throw UnauthorizedTicket("The remote playback ticket is invalid or expired.");
        }
        var payload = GetTicket(credentials.MediaTicket, LaunchTicketScope.RemoteStream);
        RequireRemotePathMatch(payload, credentials, fileName, launchId);

        var runtime = Plugin.Runtime
            ?? throw new InvalidOperationException("The External Player runtime is unavailable.");
        ResolvedRemoteStream resolved;
        try
        {
            resolved = await runtime.RemoteStreams.ResolveAsync(
                    payload.RemoteUrl,
                    Request.UserAgent,
                    runtime.Clock.UtcNow,
                    Request.CancellationToken)
                .ConfigureAwait(false);
        }
        catch (RemoteResolutionThrottledException exception)
        {
            logger.Debug(
                "External Player throttled STRM redirect resolution for item " +
                payload.ItemId.ToString("N") + ".");
            Request.Response.StatusCode = 429;
            Request.Response.AddHeader(
                "Retry-After",
                exception.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture));
            Request.Response.AddHeader("Cache-Control", "private, no-store");
            return string.Empty;
        }
        catch (RemoteSourceUnavailableException exception)
        {
            logger.Debug(
                "External Player could not temporarily resolve the STRM source for item " +
                payload.ItemId.ToString("N") + ".");
            Request.Response.StatusCode = 503;
            Request.Response.AddHeader(
                "Retry-After",
                exception.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture));
            Request.Response.AddHeader("Cache-Control", "private, no-store");
            return string.Empty;
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is InvalidOperationException ||
            exception is System.Net.Http.HttpRequestException ||
            exception is TaskCanceledException)
        {
            logger.Debug(
                "External Player could not resolve the authorized STRM redirect for item " +
                payload.ItemId.ToString("N") + ": " + GetRemoteFailureReason(exception));
            throw new ResourceNotFoundException(
                "The selected STRM source could not provide a safe temporary media redirect.");
        }

        // The shared source request is independent of any one caller. Recheck
        // authorization and the descriptor after awaiting it, before exposing
        // the resolved CDN location to this response.
        payload = GetTicket(credentials.MediaTicket, LaunchTicketScope.RemoteStream);
        RequireRemotePathMatch(payload, credentials, fileName, launchId);

        logger.Debug(
            "External Player resolved a temporary STRM redirect for item " +
            payload.ItemId.ToString("N") + ".");
        Request.Response.StatusCode = 302;
        Request.Response.AddHeader("Location", resolved.Url);
        Request.Response.AddHeader("Cache-Control", "private, no-store");
        Request.Response.AddHeader("Pragma", "no-cache");
        Request.Response.AddHeader("Referrer-Policy", "no-referrer");
        Request.Response.AddHeader("X-Content-Type-Options", "nosniff");
        return string.Empty;
    }

    private static void RequireRemotePathMatch(
        LaunchTicketPayload payload,
        RemoteLaunchCredentials credentials,
        string fileName,
        string? launchId)
    {
        if (!string.Equals(payload.LaunchId, launchId ?? string.Empty, StringComparison.Ordinal) ||
            credentials.HasPlaybackReporting != !string.IsNullOrEmpty(launchId) ||
            !string.Equals(fileName, payload.UrlFileName, StringComparison.Ordinal))
        {
            throw new ResourceNotFoundException(
                "The remote stream file name does not match the ticket.");
        }
    }

    public Task<object> Head(GetExternalPlayerRemoteStream request) => Get(request);

    public Task<object> Head(GetExternalPlayerRemoteLaunchStream request) => Get(request);

    private static string GetRemoteFailureReason(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        TaskCanceledException => "The source request timed out or was canceled.",
        System.Net.Http.HttpRequestException => "The source request failed.",
        _ => "The source or redirect URL was invalid.",
    };

    private LaunchTicketPayload GetTicket(string rawTicket, LaunchTicketScope expectedScope)
    {
        var runtime = Plugin.Runtime
            ?? throw new InvalidOperationException("The External Player runtime is unavailable.");
        if (Plugin.Instance?.Options.Enabled != true)
        {
            runtime.Tickets.Clear();
            throw UnauthorizedTicket("External Player is disabled.");
        }
        if (!runtime.Tickets.TryGet(rawTicket, out var payload) || payload is null)
        {
            throw UnauthorizedTicket("The playback ticket is invalid or expired.");
        }

        var user = userManager.GetUserById(payload.UserId);
        var item = libraryManager.GetItemById(payload.ItemId);
        if (payload.Scope != expectedScope || user is null || item is null ||
            !item.IsVisible(user) || !user.Policy.EnableMediaPlayback)
        {
            runtime.Tickets.Revoke(rawTicket);
            throw UnauthorizedTicket("The playback ticket is no longer authorized.");
        }

        var mediaSource = mediaSourceManager.GetStaticMediaSources(
                item,
                enablePathSubstitution: false,
                fillChapters: false,
                deviceProfile: null,
                user: user)
            .FirstOrDefault(source =>
                string.Equals(source.Id, payload.MediaSourceId, StringComparison.Ordinal));
        var matchesResource = expectedScope switch
        {
            LaunchTicketScope.Media =>
                mediaSource?.Protocol == MediaProtocol.File &&
                PathsEqual(mediaSource.Path, payload.FilePath),
            LaunchTicketScope.Subtitle => PathsEqual(
                mediaSource?.MediaStreams?.FirstOrDefault(stream =>
                    stream.Type == MediaStreamType.Subtitle &&
                    stream.Protocol == MediaProtocol.File &&
                    stream.Index == payload.SubtitleStreamIndex &&
                    stream.IsExternal)?.Path,
                payload.FilePath),
            LaunchTicketScope.RemoteStream =>
                RemoteMediaSourceMatches(item.Path, mediaSource, payload),
            _ => false,
        };
        if (!matchesResource)
        {
            runtime.Tickets.Revoke(rawTicket);
            throw UnauthorizedTicket("The playback ticket no longer matches the selected media source.");
        }

        return payload;
    }

    private static bool RemoteMediaSourceMatches(
        string? itemPath,
        MediaBrowser.Model.Dto.MediaSourceInfo? mediaSource,
        LaunchTicketPayload payload)
    {
        if (mediaSource is null || mediaSource.Protocol == MediaProtocol.File)
        {
            return false;
        }

        try
        {
            var current = RemoteMediaSourcePolicy.RequireDirectStrmSource(itemPath, mediaSource);
            return PathsEqual(current.DescriptorFile.FullName, payload.FilePath) &&
                current.DescriptorFile.Length == payload.ContentLength &&
                current.DescriptorFile.LastWriteTimeUtc.Ticks == payload.LastWriteTimeUtcTicks &&
                string.Equals(current.Url, payload.RemoteUrl, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task<object> CreateFileResult(
        string path,
        string contentType,
        string safeFileName,
        long issuedContentLength,
        long issuedLastWriteTimeUtcTicks)
    {
        var file = LocalMediaFilePolicy.RequireExistingFile(path);
        if (issuedContentLength < 0 || file.Length != issuedContentLength ||
            file.LastWriteTimeUtc.Ticks != issuedLastWriteTimeUtcTicks)
        {
            throw new ResourceNotFoundException("The selected media file changed after the ticket was issued.");
        }

        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept-Ranges"] = "bytes",
            ["Cache-Control"] = "private, no-store",
            ["X-Content-Type-Options"] = "nosniff",
            ["Content-Disposition"] = SafeFileNamePolicy.CreateContentDisposition(safeFileName),
            ["ETag"] = CreateEntityTag(file),
            ["Last-Modified"] = file.LastWriteTimeUtc.ToString("R", CultureInfo.InvariantCulture),
        };

        return await resultFactory.GetStaticResult(Request, new StaticResultOptions
        {
            CacheKey = Guid.Empty,
            ContentLength = file.Length,
            ContentType = contentType,
            IsHeadRequest = string.Equals(Request.Verb, "HEAD", StringComparison.OrdinalIgnoreCase),
            SupportsRangeRequests = true,
            RequestHeaders = Request.Headers.ToDictionary(),
            ResponseHeaders = responseHeaders,
            ContentFactory = (offset, length, cancellationToken) =>
                OpenFileAsync(
                    file.FullName,
                    offset,
                    length,
                    file.Length,
                    issuedLastWriteTimeUtcTicks,
                    cancellationToken),
        }).ConfigureAwait(false);
    }

    private static Task<StreamHandler> OpenFileAsync(
        string path,
        long offset,
        long length,
        long totalLength,
        long issuedLastWriteTimeUtcTicks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (offset < 0 || length < 0 || offset > totalLength || length > totalLength - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested file range is invalid.");
        }
        var file = LocalMediaFilePolicy.RequireExistingFile(path);
        if (file.Length != totalLength || file.LastWriteTimeUtc.Ticks != issuedLastWriteTimeUtcTicks)
        {
            throw new ResourceNotFoundException("The selected media file changed after the ticket was issued.");
        }
        var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            useAsync: true);
        stream.Seek(offset, SeekOrigin.Begin);
        return Task.FromResult(new StreamHandler
        {
            Stream = stream,
            Length = length,
            TotalLength = totalLength,
        });
    }


    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }
        var comparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            return false;
        }
    }

    private static string CreateEntityTag(FileInfo file) =>
        "\"" + file.Length.ToString("x", CultureInfo.InvariantCulture) + "-" +
        file.LastWriteTimeUtc.Ticks.ToString("x", CultureInfo.InvariantCulture) + "\"";

    private static MediaBrowser.Controller.Net.SecurityException UnauthorizedTicket(string message) =>
        new(message, SecurityExceptionType.Unauthenticated);
}
