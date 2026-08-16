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
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class StreamRelayService : IService, IRequiresRequest
{
    private readonly IHttpResultFactory resultFactory;
    private readonly ILibraryManager libraryManager;
    private readonly IMediaSourceManager mediaSourceManager;
    private readonly IUserManager userManager;

    public StreamRelayService(
        IHttpResultFactory resultFactory,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        IUserManager userManager)
    {
        this.resultFactory = resultFactory;
        this.libraryManager = libraryManager;
        this.mediaSourceManager = mediaSourceManager;
        this.userManager = userManager;
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
        var authorizedPath = expectedScope == LaunchTicketScope.Media
            ? mediaSource?.Protocol == MediaProtocol.File ? mediaSource.Path : null
            : mediaSource?.MediaStreams?.FirstOrDefault(stream =>
                stream.Type == MediaStreamType.Subtitle &&
                stream.Protocol == MediaProtocol.File &&
                stream.Index == payload.SubtitleStreamIndex &&
                stream.IsExternal)?.Path;
        if (!PathsEqual(authorizedPath, payload.FilePath))
        {
            runtime.Tickets.Revoke(rawTicket);
            throw UnauthorizedTicket("The playback ticket no longer matches the selected media source.");
        }

        return payload;
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
