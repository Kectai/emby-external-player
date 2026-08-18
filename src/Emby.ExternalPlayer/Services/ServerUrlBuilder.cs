using System;
using System.Text.RegularExpressions;

namespace Emby.ExternalPlayer.Services;

public static class ServerUrlBuilder
{
    public const string PlaybackTicketHeaderName = "X-Emby-Playback-Ticket";
    public const string SubtitleTicketHeaderName = "X-Emby-Subtitle-Ticket";
    public const string ProgressTicketHeaderName = "X-Emby-Progress-Ticket";
    public const string ProgressProtocolHeaderName = "X-Emby-Progress-Protocol";
    public const string ProgressExpiresHeaderName = "X-Emby-Progress-Expires";

    private static readonly Regex SafeExtension = new("^[a-z0-9]{1,12}$", RegexOptions.Compiled);

    public static string GetApiBase(string absoluteRequestUrl, string routeMarker)
    {
        if (!Uri.TryCreate(absoluteRequestUrl, UriKind.Absolute, out var requestUri) ||
            (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The request URL must be an absolute HTTP URL.", nameof(absoluteRequestUrl));
        }

        var marker = "/" + routeMarker.Trim('/');
        var markerIndex = requestUri.AbsolutePath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            throw new ArgumentException("The request URL does not contain the expected route.", nameof(absoluteRequestUrl));
        }

        var builder = new UriBuilder(requestUri)
        {
            Path = requestUri.AbsolutePath.Substring(0, markerIndex).TrimEnd('/') + "/",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri.AbsoluteUri;
    }

    public static string BuildTicketStreamUrl(string apiBase, string ticket, string urlFileName)
    {
        return Combine(
            apiBase,
            "ExternalPlayer/Stream/" + Uri.EscapeDataString(urlFileName) +
            "?api_key=" + Uri.EscapeDataString(ticket));
    }

    public static string BuildHeaderTicketStreamUrl(string apiBase, string urlFileName)
    {
        return Combine(
            apiBase,
            "ExternalPlayer/Stream/" + Uri.EscapeDataString(urlFileName));
    }

    public static string BuildHeaderTicketStreamUrl(
        string apiBase,
        string launchId,
        string urlFileName)
    {
        if (!PlaybackReportTicketStore.IsValidLaunchId(launchId))
        {
            throw new ArgumentException("The launch id is invalid.", nameof(launchId));
        }
        return Combine(
            apiBase,
            "ExternalPlayer/Stream/" + launchId + "/" + Uri.EscapeDataString(urlFileName));
    }

    public static string BuildTicketSubtitleUrl(
        string apiBase,
        string ticket,
        int index,
        string? format,
        string? fileName = null)
    {
        var extension = NormalizeExtension(format, "srt");
        return Combine(
            apiBase,
            "ExternalPlayer/Subtitle/" + index + "/" +
            Uri.EscapeDataString(fileName ?? "subtitle." + extension) +
            "?api_key=" + Uri.EscapeDataString(ticket));
    }

    public static string BuildHeaderTicketSubtitleUrl(
        string apiBase,
        int index,
        string? format,
        string? fileName = null)
    {
        var extension = NormalizeExtension(format, "srt");
        return Combine(
            apiBase,
            "ExternalPlayer/Subtitle/" + index + "/" +
            Uri.EscapeDataString(fileName ?? "subtitle." + extension));
    }

    public static string BuildTicketRemoteStreamUrl(
        string apiBase,
        string ticket,
        string fileName)
    {
        return Combine(
            apiBase,
            "ExternalPlayer/Remote/" + Uri.EscapeDataString(fileName) +
            "?api_key=" + Uri.EscapeDataString(ticket));
    }

    public static string BuildTicketRemoteLaunchStreamUrl(
        string apiBase,
        string launchToken,
        string launchId,
        string fileName)
    {
        if (!PlaybackReportTicketStore.IsValidLaunchId(launchId))
        {
            throw new ArgumentException("The launch id is invalid.", nameof(launchId));
        }
        return Combine(
            apiBase,
            "ExternalPlayer/Remote/" + launchId + "/" + Uri.EscapeDataString(fileName) +
            "?api_key=" + Uri.EscapeDataString(launchToken));
    }

    public static string NormalizeExtension(string? value, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        return SafeExtension.IsMatch(normalized) ? normalized : fallback;
    }

    private static string Combine(string apiBase, string relative)
    {
        return new Uri(new Uri(apiBase, UriKind.Absolute), relative).AbsoluteUri;
    }
}
