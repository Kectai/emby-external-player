using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class StreamRelayService : IService, IRequiresRequest
{
    private readonly IHttpResultFactory resultFactory;
    private readonly ILibraryManager libraryManager;
    private readonly IUserManager userManager;

    public StreamRelayService(
        IHttpResultFactory resultFactory,
        ILibraryManager libraryManager,
        IUserManager userManager)
    {
        this.resultFactory = resultFactory;
        this.libraryManager = libraryManager;
        this.userManager = userManager;
    }

    public IRequest Request { get; set; } = null!;

    public Task<object> Get(GetExternalPlayerStream request)
    {
        var rawTicket = !string.IsNullOrWhiteSpace(request.Ticket)
            ? request.Ticket
            : Request.QueryString["api_key"] ?? string.Empty;
        var payload = GetTicket(rawTicket);
        if (!string.IsNullOrWhiteSpace(request.FileName) &&
            !string.Equals(request.FileName, payload.UrlFileName, StringComparison.Ordinal))
        {
            throw new ResourceNotFoundException("The playback title does not match the ticket.");
        }
        return CreateFileResult(
            payload.MediaFilePath,
            payload.ContentType,
            payload.SafeFileName,
            payload.ContentLength);
    }

    public Task<object> Head(GetExternalPlayerStream request) => Get(request);

    public Task<object> Get(GetExternalPlayerSubtitle request)
    {
        var payload = GetTicket(request.Ticket);
        if (payload.SubtitleFilePath is null ||
            payload.SubtitleStreamIndex != request.Index)
        {
            throw new ResourceNotFoundException("The subtitle playback ticket is invalid.");
        }

        var subtitleFile = LocalMediaFilePolicy.RequireExistingFile(payload.SubtitleFilePath);
        return CreateFileResult(
            subtitleFile.FullName,
            payload.SubtitleContentType,
            payload.SafeSubtitleFileName ?? "subtitle.srt",
            payload.SubtitleContentLength ?? -1);
    }

    public Task<object> Head(GetExternalPlayerSubtitle request) => Get(request);

    private LaunchTicketPayload GetTicket(string rawTicket)
    {
        var runtime = Plugin.Runtime
            ?? throw new InvalidOperationException("The External Player runtime is unavailable.");
        if (!runtime.Tickets.TryGet(rawTicket, out var payload) || payload is null)
        {
            throw UnauthorizedTicket("The playback ticket is invalid or expired.");
        }

        var user = userManager.GetUserById(payload.UserId);
        var item = libraryManager.GetItemById(payload.ItemId);
        if (user is null || item is null || !item.IsVisible(user) || !user.Policy.EnableMediaPlayback)
        {
            runtime.Tickets.Revoke(rawTicket);
            throw UnauthorizedTicket("The playback ticket is no longer authorized.");
        }

        return payload;
    }

    private async Task<object> CreateFileResult(
        string path,
        string contentType,
        string safeFileName,
        long issuedContentLength)
    {
        var file = LocalMediaFilePolicy.RequireExistingFile(path);
        if (issuedContentLength < 0 || file.Length != issuedContentLength)
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
                OpenFileAsync(file.FullName, offset, length, file.Length, cancellationToken),
        }).ConfigureAwait(false);
    }

    private static Task<StreamHandler> OpenFileAsync(
        string path,
        long offset,
        long length,
        long totalLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (offset < 0 || length < 0 || offset > totalLength || length > totalLength - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested file range is invalid.");
        }
        var stream = new FileStream(
            path,
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

    private static string CreateEntityTag(FileInfo file) =>
        "\"" + file.Length.ToString("x", CultureInfo.InvariantCulture) + "-" +
        file.LastWriteTimeUtc.Ticks.ToString("x", CultureInfo.InvariantCulture) + "\"";

    private static MediaBrowser.Controller.Net.SecurityException UnauthorizedTicket(string message) =>
        new(message, SecurityExceptionType.Unauthenticated);
}
