using System;
using System.Globalization;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Model.Services;

namespace Emby.ExternalPlayer.Api;

public sealed class PlaybackReportApiService : IService, IRequiresRequest
{
    public IRequest Request { get; set; } = null!;

    public Task<object> Post(ReportExternalPlaybackStart request) =>
        ExecuteAsync(request, (coordinator, ticket) => coordinator.StartAsync(ticket, request));

    public Task<object> Post(ReportExternalPlaybackProgress request) =>
        ExecuteAsync(request, (coordinator, ticket) => coordinator.ProgressAsync(ticket, request));

    public Task<object> Post(ReportExternalPlaybackStop request) =>
        ExecuteAsync(request, (coordinator, ticket) => coordinator.StopAsync(ticket, request));

    private async Task<object> ExecuteAsync(
        PlaybackReportRequest request,
        Func<PlaybackReportCoordinator, string, Task<PlaybackReportOperationResult>> operation)
    {
        Request.Response.AddHeader("Cache-Control", "no-store");
        Request.Response.AddHeader("X-Content-Type-Options", "nosniff");
        if (Request.ContentLength < 0 ||
            Request.ContentLength > PlaybackReportCoordinator.MaximumRequestBytes)
        {
            return SetResponse(new PlaybackReportOperationResult
            {
                StatusCode = 413,
                Response = new PlaybackReportResponse
                {
                    Accepted = false,
                    Epoch = Math.Max(1, request.Epoch),
                    AcceptedSequence = 0,
                    Terminal = true,
                    Reason = "requestTooLarge",
                    ServerTimeUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                },
            });
        }

        var runtime = Plugin.Runtime;
        if (runtime is null || Plugin.Instance?.Options.Enabled != true)
        {
            return SetResponse(new PlaybackReportOperationResult
            {
                StatusCode = 410,
                Response = new PlaybackReportResponse
                {
                    Accepted = false,
                    Epoch = Math.Max(1, request.Epoch),
                    AcceptedSequence = 0,
                    Terminal = true,
                    Reason = "disabled",
                    ServerTimeUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                },
            });
        }

        var rawTicket = Request.Headers[ServerUrlBuilder.ProgressTicketHeaderName] ?? string.Empty;
        var result = await operation(runtime.PlaybackReports, rawTicket).ConfigureAwait(false);
        return SetResponse(result);
    }

    private object SetResponse(PlaybackReportOperationResult result)
    {
        Request.Response.StatusCode = result.StatusCode;
        if (result.RetryAfterSeconds.HasValue)
        {
            Request.Response.AddHeader(
                "Retry-After",
                result.RetryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture));
        }
        return result.Response;
    }
}
