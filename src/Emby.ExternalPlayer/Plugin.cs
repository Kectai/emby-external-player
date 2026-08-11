using System;
using System.Collections.Generic;
using System.Linq;
using Emby.ExternalPlayer.Domain;
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
    private readonly object optionsSync = new();
    private CustomPlayerOptionsCollection? customPlayersSnapshot;
    private bool savingCustomPlayers;

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

    protected override PluginOptions OnBeforeShowUI(PluginOptions options)
    {
        lock (optionsSync)
        {
            options.PrepareForEditor();
            customPlayersSnapshot = CloneCustomPlayers(options.CustomPlayers);
        }
        return options;
    }

    protected override bool OnOptionsSaving(PluginOptions options)
    {
        lock (optionsSync)
        {
            if (!savingCustomPlayers && customPlayersSnapshot is not null)
            {
                options.CustomPlayers = CloneCustomPlayers(customPlayersSnapshot);
            }
            options.NormalizeCustomPlayers();
        }
        return base.OnOptionsSaving(options);
    }

    protected override void OnOptionsSaved(PluginOptions options)
    {
        logger.Info("External Player options were updated.");
        PluginEntryPoint.Instance?.Refresh(options);
    }

    public IReadOnlyCollection<CustomPlayerConfiguration> GetCustomPlayerConfigurations()
    {
        lock (optionsSync)
        {
            var options = GetOptions();
            var changed = options.NormalizeCustomPlayers();
            customPlayersSnapshot = CloneCustomPlayers(options.CustomPlayers);
            if (changed)
            {
                SaveCustomPlayerOptions(options);
            }
            return ((IEnumerable<CustomPlayerOptions>)options.CustomPlayers)
                .Select(ToConfiguration)
                .ToArray();
        }
    }

    public CustomPlayerConfiguration SaveCustomPlayerConfiguration(CustomPlayerConfiguration value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (string.IsNullOrWhiteSpace(value.ApplicationName) || value.ApplicationName.Length > 80)
        {
            throw new ArgumentException(PluginStrings.CustomPlayerNameRequired, nameof(value));
        }
        if (!Enum.TryParse(value.Platform, true, out CustomPlayerPlatform platform) ||
            !Enum.IsDefined(typeof(CustomPlayerPlatform), platform))
        {
            throw new ArgumentException("The custom player platform is invalid.", nameof(value));
        }
        if (!CustomPlayerTemplate.IsValid(value.UrlTemplate))
        {
            throw new ArgumentException(PluginStrings.CustomPlayerTemplateInvalid, nameof(value));
        }

        lock (optionsSync)
        {
            var options = GetOptions();
            options.NormalizeCustomPlayers();
            var index = string.IsNullOrWhiteSpace(value.Id)
                ? -1
                : options.CustomPlayers.FindIndex(player =>
                    string.Equals(player.Id, value.Id, StringComparison.Ordinal));
            var player = new CustomPlayerOptions
            {
                Id = index >= 0 ? options.CustomPlayers[index].Id : Guid.NewGuid().ToString("N"),
                Enabled = value.Enabled,
                ApplicationName = value.ApplicationName,
                Platform = platform,
                UrlTemplate = value.UrlTemplate,
            };
            if (index >= 0)
            {
                options.CustomPlayers[index] = player;
            }
            else
            {
                options.CustomPlayers.Add(player);
            }
            customPlayersSnapshot = CloneCustomPlayers(options.CustomPlayers);
            SaveCustomPlayerOptions(options);
            return ToConfiguration(player);
        }
    }

    public bool DeleteCustomPlayerConfiguration(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }
        lock (optionsSync)
        {
            var options = GetOptions();
            options.NormalizeCustomPlayers();
            var removed = options.CustomPlayers.RemoveAll(player =>
                string.Equals(player.Id, id, StringComparison.Ordinal)) > 0;
            if (!removed)
            {
                return false;
            }
            customPlayersSnapshot = CloneCustomPlayers(options.CustomPlayers);
            SaveCustomPlayerOptions(options);
            return true;
        }
    }

    private void SaveCustomPlayerOptions(PluginOptions options)
    {
        savingCustomPlayers = true;
        try
        {
            SaveOptions(options);
        }
        finally
        {
            savingCustomPlayers = false;
        }
    }

    private static CustomPlayerOptionsCollection CloneCustomPlayers(IEnumerable<CustomPlayerOptions> players) =>
        new(players.Select(player => new CustomPlayerOptions
        {
            Id = player.Id,
            Enabled = player.Enabled,
            ApplicationName = player.ApplicationName,
            Platform = player.Platform,
            UrlTemplate = player.UrlTemplate,
        }));

    private static CustomPlayerConfiguration ToConfiguration(CustomPlayerOptions player) => new()
    {
        Id = player.Id,
        Enabled = player.Enabled,
        ApplicationName = player.ApplicationName,
        Platform = player.Platform.ToString(),
        UrlTemplate = player.UrlTemplate,
    };

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
