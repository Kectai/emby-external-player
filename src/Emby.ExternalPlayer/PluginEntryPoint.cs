using System;
using System.Threading;
using System.Threading.Tasks;
using Emby.ExternalPlayer.Services;
using Emby.ExternalPlayer.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer;

public sealed class PluginEntryPoint : IServerEntryPoint, IDisposable
{
    private readonly DashboardBootstrapInstaller installer;
    private readonly ILogger logger;
    private readonly EmbyPlaybackSessionBridge playbackBridge;
    private Timer? watchdog;
    private int watchdogRunning;
    private bool disposed;

    public PluginEntryPoint(
        IServerApplicationPaths applicationPaths,
        ILogManager logManager,
        IUserManager userManager,
        ILibraryManager libraryManager,
        IMediaSourceManager mediaSourceManager,
        ISessionManager sessionManager)
    {
        logger = logManager.GetLogger(Plugin.Instance?.Name ?? "External Player");
        installer = new DashboardBootstrapInstaller(applicationPaths.ApplicationResourcesPath, logger);
        playbackBridge = new EmbyPlaybackSessionBridge(
            userManager,
            libraryManager,
            mediaSourceManager,
            sessionManager);
        Instance = this;
    }

    public static PluginEntryPoint? Instance { get; private set; }

    public void Run()
    {
        Plugin.Runtime?.PlaybackReports.SetBridge(playbackBridge);
        Refresh(Plugin.Instance?.Options ?? new PluginOptions());
    }

    public void Refresh(PluginOptions options)
    {
        if (disposed)
        {
            return;
        }

        if (options.Enabled && options.EnableWebButton)
        {
            installer.EnsureInstalled();
        }
        else
        {
            installer.EnsureRemoved();
        }

        var coordinator = Plugin.Runtime?.PlaybackReports;
        if (coordinator is null)
        {
            return;
        }
        coordinator.SetBridge(playbackBridge);
        if (options.Enabled)
        {
            coordinator.Enable();
            watchdog ??= new Timer(
                _ => _ = RunWatchdogAsync(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));
        }
        else
        {
            watchdog?.Dispose();
            watchdog = null;
            try
            {
                coordinator.DisableAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                logger.ErrorException(
                    "External Player could not finish every playback reporting session while disabling.",
                    exception);
            }
        }
    }

    private async Task RunWatchdogAsync()
    {
        if (disposed || Interlocked.Exchange(ref watchdogRunning, 1) != 0)
        {
            return;
        }
        try
        {
            var runtime = Plugin.Runtime;
            if (runtime is null)
            {
                return;
            }
            runtime.Tickets.RemoveExpired();
            runtime.RemoteStreams.RemoveExpired(runtime.Clock.UtcNow);
            if (runtime.PlaybackReports.ActiveCount > 0)
            {
                await runtime.PlaybackReports.ScanInactiveAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.ErrorException("External Player playback reporting watchdog failed.", exception);
        }
        finally
        {
            Interlocked.Exchange(ref watchdogRunning, 0);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        watchdog?.Dispose();
        watchdog = null;
        try
        {
            Plugin.Runtime?.PlaybackReports.DisableAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            logger.ErrorException(
                "External Player could not finish every playback reporting session while stopping.",
                exception);
        }
        installer.EnsureRemoved();
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
