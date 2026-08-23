using System;
using System.IO;
using System.Text;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer.Web;

public sealed class DashboardBootstrapInstaller
{
    private readonly string appJsPath;
    private readonly string indexHtmlPath;
    private readonly ILogger logger;

    public DashboardBootstrapInstaller(string applicationResourcesPath, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(applicationResourcesPath))
        {
            throw new ArgumentException("Application resources path is required.", nameof(applicationResourcesPath));
        }

        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var dashboardPath = Path.Combine(applicationResourcesPath, "dashboard-ui");
        appJsPath = Path.Combine(dashboardPath, "app.js");
        indexHtmlPath = Path.Combine(dashboardPath, "index.html");
    }

    public DashboardPatchStatus EnsureInstalled()
    {
        var appResult = Update(
            appJsPath,
            "app.js",
            DashboardPatchEngine.Apply,
            warnWhenUnavailable: true);
        if (appResult == DashboardPatchStatus.UnsupportedAnchor)
        {
            return appResult;
        }

        var indexResult = Update(
            indexHtmlPath,
            "index.html",
            DashboardIndexPatchEngine.Apply,
            warnWhenUnavailable: false);
        return appResult == DashboardPatchStatus.Applied || indexResult == DashboardPatchStatus.Applied
            ? DashboardPatchStatus.Applied
            : appResult;
    }

    public DashboardPatchStatus EnsureRemoved()
    {
        var appResult = Update(
            appJsPath,
            "app.js",
            DashboardPatchEngine.Remove,
            warnWhenUnavailable: false);
        var indexResult = Update(
            indexHtmlPath,
            "index.html",
            DashboardIndexPatchEngine.Remove,
            warnWhenUnavailable: false);
        return appResult == DashboardPatchStatus.Removed || indexResult == DashboardPatchStatus.Removed
            ? DashboardPatchStatus.Removed
            : appResult;
    }

    private DashboardPatchStatus Update(
        string path,
        string fileName,
        Func<string, DashboardPatchResult> patch,
        bool warnWhenUnavailable)
    {
        try
        {
            if (!File.Exists(path))
            {
                if (warnWhenUnavailable)
                {
                    logger.Warn("External Player could not locate the Emby Web {0} file.", fileName);
                }
                return DashboardPatchStatus.UnsupportedAnchor;
            }

            var bytes = File.ReadAllBytes(path);
            var hasUtf8Bom = bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF;
            var contentOffset = hasUtf8Bom ? 3 : 0;
            var original = Encoding.UTF8.GetString(bytes, contentOffset, bytes.Length - contentOffset);
            var result = patch(original);
            if (result.Status != DashboardPatchStatus.Applied &&
                result.Status != DashboardPatchStatus.Removed)
            {
                if (result.Status == DashboardPatchStatus.UnsupportedAnchor && warnWhenUnavailable)
                {
                    logger.Warn(
                        "External Player disabled its Web integration because the {0} anchor is unsupported.",
                        fileName);
                }

                return result.Status;
            }

            ReplaceAtomically(path, result.Content, hasUtf8Bom);
            logger.Info("External Player Web bootstrap state changed in {0}: {1}.", fileName, result.Status);
            return result.Status;
        }
        catch (Exception exception)
        {
            logger.ErrorException(
                "External Player could not update the Emby Web {0} bootstrap; Emby Web was left usable.",
                exception,
                fileName);
            return DashboardPatchStatus.UnsupportedAnchor;
        }
    }

    private static void ReplaceAtomically(string path, string content, bool emitUtf8Bom)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The dashboard file path has no parent directory.");
        var temporaryPath = Path.Combine(directory, ".emby-ep-" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(emitUtf8Bom));
            File.Replace(temporaryPath, path, null);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
