using System;
using Emby.ExternalPlayer.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer;

public sealed class PluginEntryPoint : IServerEntryPoint, IDisposable
{
    private readonly DashboardBootstrapInstaller installer;
    private bool disposed;

    public PluginEntryPoint(IServerApplicationPaths applicationPaths, ILogManager logManager)
    {
        var logger = logManager.GetLogger(Plugin.Instance?.Name ?? "External Player");
        installer = new DashboardBootstrapInstaller(applicationPaths.ApplicationResourcesPath, logger);
        Instance = this;
    }

    public static PluginEntryPoint? Instance { get; private set; }

    public void Run()
    {
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
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        installer.EnsureRemoved();
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
