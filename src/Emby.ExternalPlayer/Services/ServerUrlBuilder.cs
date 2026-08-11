using System;
using System.Text.RegularExpressions;

namespace Emby.ExternalPlayer.Services;

public static class ServerUrlBuilder
{
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

    public static string BuildDirectStreamUrl(
        string apiBase,
        Guid itemId,
        string mediaSourceId,
        string? container)
    {
        var extension = NormalizeExtension(container, "mkv");
        return Combine(
            apiBase,
            "Videos/" + itemId.ToString("N") + "/stream." + extension +
            "?Static=true&MediaSourceId=" + Uri.EscapeDataString(mediaSourceId));
    }

    public static string BuildSubtitleUrl(
        string apiBase,
        Guid itemId,
        string mediaSourceId,
        int streamIndex,
        string? format)
    {
        var extension = NormalizeExtension(format, "srt");
        return Combine(
            apiBase,
            "Videos/" + itemId.ToString("N") + "/" + Uri.EscapeDataString(mediaSourceId) +
            "/Subtitles/" + streamIndex + "/Stream." + extension);
    }

    public static string BuildTicketStreamUrl(string apiBase, string ticket, string urlFileName)
    {
        return Combine(
            apiBase,
            "ExternalPlayer/Stream/" + Uri.EscapeDataString(urlFileName) +
            "?api_key=" + Uri.EscapeDataString(ticket));
    }

    public static string BuildTicketSubtitleUrl(string apiBase, string ticket, int index, string? format)
    {
        return Combine(
            apiBase,
            "ExternalPlayer/Subtitle/" + Uri.EscapeDataString(ticket) + "/" + index +
            "/subtitle.css");
    }

    public static string AppendApiKey(string url, string accessToken)
    {
        return url + (url.Contains("?", StringComparison.Ordinal) ? "&" : "?") +
               "api_key=" + Uri.EscapeDataString(accessToken);
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
