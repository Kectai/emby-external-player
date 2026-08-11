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

        var content = source.Insert(anchorIndex, Injection);
        return new DashboardPatchResult(DashboardPatchStatus.Applied, content);
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
