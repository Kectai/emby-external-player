using System;
using System.IO;
using System.Text;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer.Web;

public sealed class DashboardBootstrapInstaller
{
    private readonly string appJsPath;
    private readonly ILogger logger;

    public DashboardBootstrapInstaller(string applicationResourcesPath, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(applicationResourcesPath))
        {
            throw new ArgumentException("Application resources path is required.", nameof(applicationResourcesPath));
        }

        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        appJsPath = Path.Combine(applicationResourcesPath, "dashboard-ui", "app.js");
    }

    public DashboardPatchStatus EnsureInstalled()
    {
        return Update(DashboardPatchEngine.Apply);
    }

    public DashboardPatchStatus EnsureRemoved()
    {
        return Update(DashboardPatchEngine.Remove);
    }

    private DashboardPatchStatus Update(Func<string, DashboardPatchResult> patch)
    {
        try
        {
            if (!File.Exists(appJsPath))
            {
                logger.Warn("External Player could not locate the Emby Web app.js file.");
                return DashboardPatchStatus.UnsupportedAnchor;
            }

            var original = File.ReadAllText(appJsPath, Encoding.UTF8);
            var result = patch(original);
            if (result.Status != DashboardPatchStatus.Applied &&
                result.Status != DashboardPatchStatus.Removed)
            {
                if (result.Status == DashboardPatchStatus.UnsupportedAnchor)
                {
                    logger.Warn("External Player disabled its Web integration because the app.js anchor is unsupported.");
                }

                return result.Status;
            }

            ReplaceAtomically(result.Content);
            logger.Info("External Player Web bootstrap state changed: {0}.", result.Status);
            return result.Status;
        }
        catch (Exception exception)
        {
            logger.ErrorException(
                "External Player could not update its Web bootstrap; Emby Web was left usable.",
                exception);
            return DashboardPatchStatus.UnsupportedAnchor;
        }
    }

    private void ReplaceAtomically(string content)
    {
        var directory = Path.GetDirectoryName(appJsPath)
            ?? throw new InvalidOperationException("The app.js path has no parent directory.");
        var temporaryPath = Path.Combine(directory, ".emby-external-player-" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            File.Replace(temporaryPath, appJsPath, null);
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
