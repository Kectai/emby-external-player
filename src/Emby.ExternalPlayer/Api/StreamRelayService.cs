using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class StreamRelayService : IService, IRequiresRequest
{
    private readonly IHttpClient httpClient;
    private readonly IHttpResultFactory resultFactory;

    public StreamRelayService(IHttpClient httpClient, IHttpResultFactory resultFactory)
    {
        this.httpClient = httpClient;
        this.resultFactory = resultFactory;
    }

    public IRequest Request { get; set; } = null!;

    public Task<object> Get(GetExternalPlayerStream request)
    {
        var payload = GetTicket(request.Ticket);
        return CreateRelayResult(
            payload.UpstreamUrl,
            payload.AccessToken,
            payload.ContentType,
            payload.ContentLength);
    }

    public Task<object> Get(GetExternalPlayerSubtitle request)
    {
        var payload = GetTicket(request.Ticket);
        if (payload.SubtitleUpstreamUrl is null ||
            payload.SubtitleStreamIndex != request.Index)
        {
            throw new ResourceNotFoundException("The subtitle playback ticket is invalid.");
        }

        return CreateRelayResult(
            payload.SubtitleUpstreamUrl,
            payload.AccessToken,
            "text/plain; charset=utf-8",
            null);
    }

    private LaunchTicketPayload GetTicket(string rawTicket)
    {
        var runtime = Plugin.Runtime
            ?? throw new InvalidOperationException("The External Player runtime is unavailable.");
        if (!runtime.Tickets.TryGet(rawTicket, out var payload) || payload is null)
        {
            throw new ResourceNotFoundException("The playback ticket is invalid or expired.");
        }

        return payload;
    }

    private async Task<object> CreateRelayResult(
        string upstreamUrl,
        string? accessToken,
        string contentType,
        long? knownContentLength)
    {
        var metadata = knownContentLength.HasValue
            ? new RelayMetadata(knownContentLength, contentType)
            : await InspectAsync(upstreamUrl, accessToken, Request.CancellationToken).ConfigureAwait(false);
        var incomingHasRange = !string.IsNullOrWhiteSpace(Request.Headers.Get("Range"));

        return await resultFactory.GetStaticResult(Request, new StaticResultOptions
        {
            CacheKey = Guid.Empty,
            ContentLength = metadata.ContentLength,
            ContentType = string.IsNullOrWhiteSpace(metadata.ContentType) ? contentType : metadata.ContentType,
            IsHeadRequest = string.Equals(Request.Verb, "HEAD", StringComparison.OrdinalIgnoreCase),
            SupportsRangeRequests = true,
            RequestHeaders = Request.Headers.ToDictionary(),
            ResponseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Accept-Ranges"] = "bytes",
                ["Cache-Control"] = "private, no-store",
                ["X-Content-Type-Options"] = "nosniff",
            },
            ContentFactory = (offset, length, cancellationToken) =>
                OpenStreamAsync(
                    upstreamUrl,
                    accessToken,
                    incomingHasRange,
                    offset,
                    length,
                    metadata.ContentLength,
                    cancellationToken),
        }).ConfigureAwait(false);
    }

    private async Task<RelayMetadata> InspectAsync(
        string upstreamUrl,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(
            CreateOptions(upstreamUrl, accessToken, cancellationToken),
            "HEAD").ConfigureAwait(false);
        EnsureSuccess(response, rangeRequested: false);
        return new RelayMetadata(response.ContentLength, response.ContentType);
    }

    private async Task<StreamHandler> OpenStreamAsync(
        string upstreamUrl,
        string? accessToken,
        bool rangeRequested,
        long offset,
        long length,
        long? totalLength,
        CancellationToken cancellationToken)
    {
        var options = CreateOptions(upstreamUrl, accessToken, cancellationToken);
        if (rangeRequested)
        {
            options.RequestHeaders["Range"] = RelayRange.BuildHeader(offset, length);
        }

        var response = await httpClient.SendAsync(options, "GET").ConfigureAwait(false);
        try
        {
            EnsureSuccess(response, rangeRequested);
            return new StreamHandler
            {
                Stream = response.Content,
                Length = response.ContentLength,
                TotalLength = totalLength,
                Handlers = new IDisposable[] { response },
            };
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static HttpRequestOptions CreateOptions(
        string upstreamUrl,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var options = new HttpRequestOptions
        {
            Url = upstreamUrl,
            BufferContent = false,
            CancellationToken = cancellationToken,
            EnableAutomaticTimeouts = false,
            LogErrors = false,
            LogRequest = false,
            LogResponse = false,
            LogResponseHeaders = false,
            ThrowOnErrorResponse = false,
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            options.RequestHeaders["X-Emby-Token"] = accessToken;
        }

        return options;
    }

    private static void EnsureSuccess(HttpResponseInfo response, bool rangeRequested)
    {
        if (rangeRequested && response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new InvalidOperationException("The upstream media endpoint did not honor the byte range.");
        }

        if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
        {
            throw new ResourceNotFoundException("The upstream media endpoint rejected the ticket request.");
        }
    }

    private sealed class RelayMetadata
    {
        public RelayMetadata(long? contentLength, string contentType)
        {
            ContentLength = contentLength;
            ContentType = contentType;
        }

        public long? ContentLength { get; }

        public string ContentType { get; }
    }
}
