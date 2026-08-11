using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Emby.ExternalPlayer.Domain;

namespace Emby.ExternalPlayer.Services;

public static class CustomPlayerTemplate
{
    private static readonly Regex SchemePattern = new(
        "^(?<scheme>[A-Za-z][A-Za-z0-9+.-]{1,31}):",
        RegexOptions.CultureInvariant);

    private static readonly Regex PlaceholderPattern = new(
        "\\{(?<name>[^{}]+)\\}",
        RegexOptions.CultureInvariant);

    private static readonly ISet<string> AllowedPlaceholders =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "url", "title", "subtitle", "start", "headers",
        };

    private static readonly ISet<string> ProhibitedSchemes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "about", "blob", "data", "file", "http", "https", "javascript", "vbscript",
        };

    public static bool IsValid(string? template)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Length > 2048 ||
            !template.Contains("{url}", StringComparison.Ordinal))
        {
            return false;
        }

        var schemeMatch = SchemePattern.Match(template);
        if (!schemeMatch.Success || ProhibitedSchemes.Contains(schemeMatch.Groups["scheme"].Value))
        {
            return false;
        }

        var withoutPlaceholders = PlaceholderPattern.Replace(template, match =>
            AllowedPlaceholders.Contains(match.Groups["name"].Value) ? string.Empty : "{invalid}");
        return withoutPlaceholders.IndexOf('{') < 0 && withoutPlaceholders.IndexOf('}') < 0;
    }

    public static string GetScheme(string template)
    {
        if (!IsValid(template))
        {
            throw new ArgumentException("The custom player URL template is invalid.", nameof(template));
        }
        return SchemePattern.Match(template).Groups["scheme"].Value.ToLowerInvariant();
    }

    public static string Render(string template, PlayerLaunchContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (!IsValid(template))
        {
            throw new ArgumentException("The custom player URL template is invalid.", nameof(template));
        }

        var hasHeaderPlaceholder = template.Contains("{headers}", StringComparison.Ordinal);
        var rendered = template
            .Replace("{url}", Encode(context.StreamUrl), StringComparison.Ordinal)
            .Replace("{title}", Encode(context.Title ?? string.Empty), StringComparison.Ordinal)
            .Replace("{subtitle}", Encode(context.SubtitleUrl ?? string.Empty), StringComparison.Ordinal)
            .Replace("{headers}", Encode(string.Join(",", context.HttpRequestHeaders)), StringComparison.Ordinal)
            .Replace(
                "{start}",
                Math.Max(0, context.StartPositionTicks / TimeSpan.TicksPerSecond)
                    .ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);

        if (!hasHeaderPlaceholder && context.HttpRequestHeaders.Count > 0 &&
            SupportsHttpRequestHeaders(template))
        {
            rendered += (rendered.Contains("?", StringComparison.Ordinal) ? "&" : "?") +
                "mpv_http-header-fields=" + Encode(string.Join(",", context.HttpRequestHeaders));
        }

        if (!Uri.TryCreate(rendered, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, GetScheme(template), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The custom player template produced an invalid URL.");
        }
        return rendered;
    }

    public static bool SupportsHttpRequestHeaders(string template) =>
        template.Contains("{headers}", StringComparison.Ordinal) ||
        (template.Contains("weblink?", StringComparison.OrdinalIgnoreCase) &&
         template.Contains("mpv_", StringComparison.OrdinalIgnoreCase) &&
         !template.Contains("mpv_http-header-fields=", StringComparison.OrdinalIgnoreCase));

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
