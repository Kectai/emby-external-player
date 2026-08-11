using System;
using Emby.ExternalPlayer.Localization;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Common;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer;

public sealed class Plugin : BasePluginSimpleUI<PluginOptions>
{
    public static readonly Guid PluginId = new("f7e75cae-5055-4706-bfe1-4c4dbf33a573");

    private readonly ILogger logger;

    public Plugin(IApplicationHost applicationHost, ILogManager logManager)
        : base(applicationHost)
    {
        Instance = this;
        Runtime = new PluginRuntime();
        logger = logManager.GetLogger(Name);
        logger.Info("External Player plugin is loading.");
    }

    public static Plugin? Instance { get; private set; }

    public static PluginRuntime? Runtime { get; private set; }

    public override string Name => "External Player";

    public override string Description => PluginStrings.EditorDescription;

    public override Guid Id => PluginId;

    public PluginOptions Options => GetOptions();

    protected override void OnOptionsSaved(PluginOptions options)
    {
        logger.Info("External Player options were updated.");
        PluginEntryPoint.Instance?.Refresh(options);
    }

    public override void OnUninstalling()
    {
        PluginEntryPoint.Instance?.Refresh(new PluginOptions
        {
            Enabled = false,
            EnableWebButton = false,
        });
        base.OnUninstalling();
    }
}
