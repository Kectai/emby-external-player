using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            "about", "blob", "data", "facetime", "facetime-audio", "file", "ftp", "http",
            "https", "intent", "javascript", "mailto", "shell", "sms", "ssh", "tel", "vbscript",
        };

    public static bool IsValid(string? template)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Length > 2048 ||
            !template.Contains("{url}", StringComparison.Ordinal))
        {
            return false;
        }

        var schemeMatch = SchemePattern.Match(template);
        var scheme = schemeMatch.Success ? schemeMatch.Groups["scheme"].Value : string.Empty;
        if (!schemeMatch.Success || ProhibitedSchemes.Contains(scheme) ||
            scheme.StartsWith("ms-", StringComparison.OrdinalIgnoreCase))
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

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{url}"] = context.StreamUrl,
            ["{title}"] = context.Title ?? string.Empty,
            ["{subtitle}"] = context.SubtitleUrl ?? string.Empty,
            ["{headers}"] = string.Join(",", context.HttpRequestHeaders ?? Array.Empty<string>()),
            ["{start}"] = Math.Max(0, context.StartPositionTicks / TimeSpan.TicksPerSecond)
                .ToString(CultureInfo.InvariantCulture),
        };
        var rendered = RemoveEmptyQueryParameters(template, values);
        foreach (var value in values)
        {
            rendered = rendered.Replace(value.Key, Encode(value.Value), StringComparison.Ordinal);
        }

        if (!Uri.TryCreate(rendered, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, GetScheme(template), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The custom player template produced an invalid URL.");
        }
        return rendered;
    }

    public static bool SupportsHttpRequestHeaders(string template) =>
        template.Contains("{headers}", StringComparison.Ordinal);

    private static string RemoveEmptyQueryParameters(
        string template,
        IReadOnlyDictionary<string, string> values)
    {
        var queryStart = template.IndexOf('?');
        if (queryStart < 0)
        {
            return template;
        }
        var fragmentStart = template.IndexOf('#', queryStart + 1);
        var queryEnd = fragmentStart >= 0 ? fragmentStart : template.Length;
        var parameters = template.Substring(queryStart + 1, queryEnd - queryStart - 1)
            .Split('&')
            .Where(parameter =>
            {
                var separator = parameter.IndexOf('=');
                return separator < 0 ||
                    !values.TryGetValue(parameter.Substring(separator + 1), out var value) ||
                    value.Length > 0;
            })
            .ToArray();
        var fragment = fragmentStart >= 0 ? template.Substring(fragmentStart) : string.Empty;
        return template.Substring(0, queryStart) +
            (parameters.Length > 0 ? "?" + string.Join("&", parameters) : string.Empty) +
            fragment;
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
}
