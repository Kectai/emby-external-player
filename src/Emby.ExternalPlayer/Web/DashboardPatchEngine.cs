using System;

namespace Emby.ExternalPlayer.Web;

public enum DashboardPatchStatus
{
    Applied,
    AlreadyApplied,
    Removed,
    NotPresent,
    UnsupportedAnchor,
}

public sealed class DashboardPatchResult
{
    public DashboardPatchResult(DashboardPatchStatus status, string content)
    {
        Status = status;
        Content = content;
    }

    public DashboardPatchStatus Status { get; }

    public string Content { get; }
}

// Keep the media-page module in Emby's normal plugin-loading phase. Loading it
// after appready misses lifecycle events that the detail page depends on.
public static class DashboardPatchEngine
{
    public const string Marker = "/* Emby.ExternalPlayer bootstrap: 6f784f38 */";
    public const string ModulePath = "./modules/embyexternalplayer/plugin.js";
    public const string PrimaryAnchor = "Promise.all(list.map(loadPlugin))";

    private static readonly string Injection =
        "list.push(\"" + ModulePath + "\")," + Marker;

    public static DashboardPatchResult Apply(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.Contains(Marker, StringComparison.Ordinal))
        {
            return new DashboardPatchResult(DashboardPatchStatus.AlreadyApplied, source);
        }

        var anchorIndex = source.IndexOf(PrimaryAnchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
        {
            return new DashboardPatchResult(DashboardPatchStatus.UnsupportedAnchor, source);
        }

        return new DashboardPatchResult(
            DashboardPatchStatus.Applied,
            source.Insert(anchorIndex, Injection));
    }

    public static DashboardPatchResult Remove(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var index = source.IndexOf(Injection, StringComparison.Ordinal);
        if (index < 0)
        {
            return new DashboardPatchResult(DashboardPatchStatus.NotPresent, source);
        }

        return new DashboardPatchResult(
            DashboardPatchStatus.Removed,
            source.Remove(index, Injection.Length));
    }
}

// This bootstrap only repairs a stale browser cache. The feature module still
// loads through app.js together with Emby's other plugins.
public static class DashboardIndexPatchEngine
{
    public const string Marker = "<!-- Emby.ExternalPlayer cache bootstrap: 759248d1 -->";
    public const string BootstrapPath = "modules/embyexternalplayer/bootstrap.js?v=3";
    public const string PrimaryAnchor = "<script src=\"apploader.js\" defer></script>";

    private static readonly string[] SupportedAnchors =
    {
        PrimaryAnchor,
        "<script defer src=\"apploader.js\"></script>",
    };

    public static DashboardPatchResult Apply(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (source.Contains(Marker, StringComparison.Ordinal))
        {
            return new DashboardPatchResult(DashboardPatchStatus.AlreadyApplied, source);
        }

        var precleaned = Remove(source);
        if (precleaned.Status == DashboardPatchStatus.Removed)
        {
            source = precleaned.Content;
        }

        foreach (var anchor in SupportedAnchors)
        {
            var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
            if (anchorIndex < 0)
            {
                continue;
            }

            var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            return new DashboardPatchResult(
                DashboardPatchStatus.Applied,
                source.Insert(anchorIndex, GetInjection(newline)));
        }

        return new DashboardPatchResult(DashboardPatchStatus.UnsupportedAnchor, source);
    }

    public static DashboardPatchResult Remove(string source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        foreach (var newline in new[] { "\r\n", "\n" })
        {
            var injection = GetInjection(newline);
            var index = source.IndexOf(injection, StringComparison.Ordinal);
            if (index >= 0)
            {
                return new DashboardPatchResult(
                    DashboardPatchStatus.Removed,
                    source.Remove(index, injection.Length));
            }
        }

        // Clean the first, unreleased index bootstrap from local test installs.
        foreach (var newline in new[] { "\r\n", "\n" })
        {
            var legacyInjection = "<script src=\"modules/embyexternalplayer/bootstrap.js?v=2\" defer></script>" + newline +
                "    <!-- Emby.ExternalPlayer bootstrap: 6f784f38 -->" + newline + "    ";
            var index = source.IndexOf(legacyInjection, StringComparison.Ordinal);
            if (index >= 0)
            {
                return new DashboardPatchResult(
                    DashboardPatchStatus.Removed,
                    source.Remove(index, legacyInjection.Length));
            }
        }

        return new DashboardPatchResult(DashboardPatchStatus.NotPresent, source);
    }

    private static string GetInjection(string newline)
    {
        return "<script src=\"" + BootstrapPath + "\" defer></script>" + newline +
            "    " + Marker + newline + "    ";
    }
}
