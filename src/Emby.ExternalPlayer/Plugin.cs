using System;
using System.Collections.Generic;
using System.Linq;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Localization;
using Emby.ExternalPlayer.Services;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer;

public sealed class Plugin : BasePluginSimpleUI<PluginOptions>
{
    public static readonly Guid PluginId = new("f7e75cae-5055-4706-bfe1-4c4dbf33a573");

    private readonly ILogger logger;
    private readonly object optionsSync = new();
    private readonly string preferenceConfigurationDirectory;
    private UserPlayerPreferenceStore? userPlayerPreferenceStore;
    private CustomPlayerOptionsCollection? customPlayersSnapshot;
    private Dictionary<PlayerId, PlayerPlatforms>? builtInPlatformSnapshot;
    private Dictionary<PlayerId, bool>? builtInEnabledSnapshot;
    private bool separatePreferenceStoreReady;
    private bool savingManagedOptions;
    private bool uninstalling;

    public Plugin(
        IApplicationHost applicationHost,
        ILogManager logManager,
        IServerApplicationPaths applicationPaths)
        : base(applicationHost)
    {
        Instance = this;
        Runtime = new PluginRuntime();
        logger = logManager.GetLogger(Name);
        preferenceConfigurationDirectory = applicationPaths.ConfigurationDirectoryPath;
        var options = GetOptions();
        var optionsChanged = options.NormalizeTicketLifetime() |
            options.NormalizeCustomPlayers() |
            options.NormalizeBuiltInPlatformScopes();
        var preferencesChanged = options.NormalizeUserPlayerPreferences();
        var previousPreferences = CloneUserPlayerPreferences(options.UserPlayerPreferences);
        try
        {
            userPlayerPreferenceStore = new UserPlayerPreferenceStore(
                preferenceConfigurationDirectory,
                logger);
            userPlayerPreferenceStore.Import(options.UserPlayerPreferences);
            separatePreferenceStoreReady = true;
            if (options.UserPlayerPreferences.Count > 0 || preferencesChanged)
            {
                options.UserPlayerPreferences = new UserPlayerPreferenceOptionsCollection();
                optionsChanged = true;
            }
        }
        catch (Exception exception)
        {
            logger.ErrorException(
                "External Player could not migrate user default-player preferences; legacy storage will remain active.",
                exception);
            userPlayerPreferenceStore = null;
            optionsChanged |= preferencesChanged;
        }
        if (optionsChanged)
        {
            try
            {
                SaveManagedOptions(options);
            }
            catch (Exception exception)
            {
                if (separatePreferenceStoreReady)
                {
                    options.UserPlayerPreferences = previousPreferences;
                }
                logger.ErrorException(
                    "External Player could not persist normalized legacy settings; plugin loading will continue.",
                    exception);
            }
        }
        CleanupMissingCustomPlayerPreferences(options);
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
            builtInPlatformSnapshot = SnapshotBuiltInPlatforms(options);
            builtInEnabledSnapshot = SnapshotBuiltInEnabled(options);
            if (separatePreferenceStoreReady && userPlayerPreferenceStore is not null)
            {
                options.UserPlayerPreferences = new UserPlayerPreferenceOptionsCollection();
            }
        }
        return options;
    }

    protected override bool OnOptionsSaving(PluginOptions options)
    {
        lock (optionsSync)
        {
            if (!savingManagedOptions && customPlayersSnapshot is not null)
            {
                options.CustomPlayers = CloneCustomPlayers(customPlayersSnapshot);
            }
            if (!savingManagedOptions && builtInPlatformSnapshot is not null)
            {
                RestoreBuiltInPlatforms(options, builtInPlatformSnapshot);
            }
            if (!savingManagedOptions && builtInEnabledSnapshot is not null)
            {
                RestoreBuiltInEnabled(options, builtInEnabledSnapshot);
            }
            if (separatePreferenceStoreReady)
            {
                options.UserPlayerPreferences = new UserPlayerPreferenceOptionsCollection();
            }
            options.NormalizeCustomPlayers();
            options.NormalizeBuiltInPlatformScopes();
            if (!separatePreferenceStoreReady)
            {
                options.NormalizeUserPlayerPreferences();
            }
        }
        return base.OnOptionsSaving(options);
    }

    protected override void OnOptionsSaved(PluginOptions options)
    {
        lock (optionsSync)
        {
            if (!savingManagedOptions && RestoreLatestIndependentOptions(options))
            {
                SaveManagedOptions(options);
            }
            if (!options.Enabled)
            {
                Runtime?.Tickets.Clear();
            }
            logger.Info("External Player options were updated.");
            PluginEntryPoint.Instance?.Refresh(options);
        }
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
                SaveManagedOptions(options);
            }
            return ((IEnumerable<CustomPlayerOptions>)options.CustomPlayers)
                .Select(ToConfiguration)
                .ToArray();
        }
    }

    public IReadOnlyCollection<BuiltInPlayerPlatformConfiguration> GetBuiltInPlayerPlatformConfigurations()
    {
        lock (optionsSync)
        {
            var options = GetOptions();
            if (options.NormalizeBuiltInPlatformScopes())
            {
                SaveManagedOptions(options);
            }
            builtInPlatformSnapshot = SnapshotBuiltInPlatforms(options);
            builtInEnabledSnapshot = SnapshotBuiltInEnabled(options);
            return Enum.GetValues(typeof(PlayerId)).Cast<PlayerId>()
                .Select(playerId => ToBuiltInPlatformConfiguration(
                    playerId,
                    options.IsPlayerEnabled(playerId),
                    options.GetPlayerPlatforms(playerId)))
                .ToArray();
        }
    }

    public BuiltInPlayerPlatformConfiguration SaveBuiltInPlayerPlatformConfiguration(
        string playerIdValue,
        string[] platformNames,
        bool? enabled = null)
    {
        if (string.IsNullOrWhiteSpace(playerIdValue) ||
            int.TryParse(playerIdValue, out _) ||
            !Enum.TryParse(playerIdValue, true, out PlayerId playerId) ||
            !Enum.IsDefined(typeof(PlayerId), playerId))
        {
            throw new ArgumentException("The built-in player id is invalid.", nameof(playerIdValue));
        }
        var platforms = ParsePlatformNames(platformNames, allowLegacyAny: false);
        lock (optionsSync)
        {
            var options = GetOptions();
            options.NormalizeBuiltInPlatformScopes();
            var previousPlatforms = SnapshotBuiltInPlatforms(options);
            var previousEnabled = SnapshotBuiltInEnabled(options);
            if (enabled.HasValue)
            {
                options.SetPlayerEnabled(playerId, enabled.Value);
            }
            options.SetPlayerPlatforms(playerId, platforms);
            try
            {
                SaveManagedOptions(options);
                builtInPlatformSnapshot = SnapshotBuiltInPlatforms(options);
                builtInEnabledSnapshot = SnapshotBuiltInEnabled(options);
            }
            catch
            {
                RestoreBuiltInPlatforms(options, previousPlatforms);
                RestoreBuiltInEnabled(options, previousEnabled);
                throw;
            }
            return ToBuiltInPlatformConfiguration(
                playerId,
                options.IsPlayerEnabled(playerId),
                options.GetPlayerPlatforms(playerId));
        }
    }

    public CustomPlayerConfiguration SaveCustomPlayerConfiguration(CustomPlayerConfiguration value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (!string.IsNullOrWhiteSpace(value.Id) && !Guid.TryParseExact(value.Id, "N", out _))
        {
            throw new ArgumentException("The custom player id is invalid.", nameof(value));
        }
        if (string.IsNullOrWhiteSpace(value.ApplicationName))
        {
            throw new ArgumentException(PluginStrings.CustomPlayerNameRequired, nameof(value));
        }
        if (value.ApplicationName.Length > 80)
        {
            throw new ArgumentException(PluginStrings.CustomPlayerNameTooLong, nameof(value));
        }
        var platforms = ParseCustomPlatforms(value);
        if (!CustomPlayerTemplate.IsValid(value.UrlTemplate))
        {
            throw new ArgumentException(PluginStrings.CustomPlayerTemplateInvalid, nameof(value));
        }
        if (value.EnablePlaybackReporting &&
            !CustomPlayerTemplate.SupportsHttpRequestHeaders(value.UrlTemplate))
        {
            throw new ArgumentException(PluginStrings.PlaybackReportingRequiresHeaders, nameof(value));
        }

        lock (optionsSync)
        {
            var options = GetOptions();
            options.NormalizeCustomPlayers();
            var previousPlayers = CloneCustomPlayers(options.CustomPlayers);
            var index = string.IsNullOrWhiteSpace(value.Id)
                ? -1
                : options.CustomPlayers.FindIndex(player =>
                    string.Equals(player.Id, value.Id, StringComparison.OrdinalIgnoreCase));
            var player = new CustomPlayerOptions
            {
                Id = index >= 0
                    ? options.CustomPlayers[index].Id
                    : string.IsNullOrWhiteSpace(value.Id)
                        ? Guid.NewGuid().ToString("N")
                        : value.Id.ToLowerInvariant(),
                Enabled = value.Enabled,
                ApplicationName = value.ApplicationName,
                Platforms = platforms,
                UrlTemplate = value.UrlTemplate,
                EnablePlaybackReporting = value.EnablePlaybackReporting,
            };
            if (index >= 0)
            {
                options.CustomPlayers[index] = player;
            }
            else
            {
                options.CustomPlayers.Add(player);
            }
            try
            {
                SaveManagedOptions(options);
                customPlayersSnapshot = CloneCustomPlayers(options.CustomPlayers);
            }
            catch
            {
                options.CustomPlayers = previousPlayers;
                throw;
            }
            return ToConfiguration(player);
        }
    }

    public bool DeleteCustomPlayerConfiguration(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Guid.TryParseExact(id, "N", out _))
        {
            return false;
        }
        lock (optionsSync)
        {
            var options = GetOptions();
            options.NormalizeCustomPlayers();
            var previousPlayers = CloneCustomPlayers(options.CustomPlayers);
            var previousPreferences = CloneUserPlayerPreferences(options.UserPlayerPreferences);
            var removed = options.CustomPlayers.RemoveAll(player =>
                string.Equals(player.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removed)
            {
                return false;
            }
            var playerId = "custom-" + id;
            if (!separatePreferenceStoreReady)
            {
                options.UserPlayerPreferences.RemoveAll(preference => string.Equals(
                    preference.PlayerId,
                    playerId,
                    StringComparison.OrdinalIgnoreCase));
            }
            try
            {
                SaveManagedOptions(options);
                customPlayersSnapshot = CloneCustomPlayers(options.CustomPlayers);
            }
            catch
            {
                options.CustomPlayers = previousPlayers;
                options.UserPlayerPreferences = previousPreferences;
                throw;
            }
            var preferenceStore = userPlayerPreferenceStore;
            if (separatePreferenceStoreReady && preferenceStore is not null)
            {
                try
                {
                    preferenceStore.RemovePlayer(playerId);
                }
                catch (Exception exception)
                {
                    logger.ErrorException(
                        "External Player could not remove stale user preferences for a deleted custom player.",
                        exception);
                }
            }
            return true;
        }
    }

    public string? GetUserDefaultPlayer(Guid userId, ClientPlatform platform)
    {
        lock (optionsSync)
        {
            var stored = userPlayerPreferenceStore?.Get(userId, platform);
            return stored ?? (!separatePreferenceStoreReady
                ? GetOptions().GetUserDefaultPlayer(userId, platform)
                : null);
        }
    }

    public string SaveUserDefaultPlayer(Guid userId, ClientPlatform platform, string playerId)
    {
        lock (optionsSync)
        {
            if (uninstalling)
            {
                throw new InvalidOperationException("External Player is uninstalling.");
            }
            var options = GetOptions();
            var selected = Runtime?.Players
                .GetAvailable(options, platform, options.ShowOnlyPlatformPlayers)
                .FirstOrDefault(player => string.Equals(
                    player.Id,
                    playerId,
                    StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                throw new ArgumentException(
                    "The requested player is disabled or unavailable on this platform.",
                    nameof(playerId));
            }
            EnsurePreferenceStoreReady();
            var stored = userPlayerPreferenceStore!.Set(userId, platform, selected.Id);
            logger.Info("External Player user default-player preference was updated.");
            return stored;
        }
    }

    private void EnsurePreferenceStoreReady()
    {
        if (separatePreferenceStoreReady && userPlayerPreferenceStore is not null)
        {
            return;
        }

        var options = GetOptions();
        var previousPreferences = CloneUserPlayerPreferences(options.UserPlayerPreferences);
        var store = new UserPlayerPreferenceStore(preferenceConfigurationDirectory, logger);
        store.Import(options.UserPlayerPreferences);
        userPlayerPreferenceStore = store;
        separatePreferenceStoreReady = true;
        CleanupMissingCustomPlayerPreferences(options);
        if (options.UserPlayerPreferences.Count == 0)
        {
            return;
        }

        options.UserPlayerPreferences = new UserPlayerPreferenceOptionsCollection();
        try
        {
            SaveManagedOptions(options);
        }
        catch (Exception exception)
        {
            options.UserPlayerPreferences = previousPreferences;
            logger.ErrorException(
                "External Player migrated user preferences but could not clear the legacy copy.",
                exception);
        }
    }

    private void CleanupMissingCustomPlayerPreferences(PluginOptions options)
    {
        if (!separatePreferenceStoreReady || userPlayerPreferenceStore is null)
        {
            return;
        }
        try
        {
            userPlayerPreferenceStore.RemoveMissingCustomPlayers(
                ((IEnumerable<CustomPlayerOptions>)options.CustomPlayers).Select(
                player => "custom-" + player.Id));
        }
        catch (Exception exception)
        {
            logger.ErrorException(
                "External Player could not clean stale user preferences; cleanup will be retried later.",
                exception);
        }
    }

    private void SaveManagedOptions(PluginOptions options)
    {
        options.ValidateOrThrow();
        savingManagedOptions = true;
        try
        {
            SaveOptions(options);
        }
        finally
        {
            savingManagedOptions = false;
        }
    }

    private bool RestoreLatestIndependentOptions(PluginOptions options)
    {
        var changed = false;
        if (customPlayersSnapshot is not null &&
            !CustomPlayersEqual(options.CustomPlayers, customPlayersSnapshot))
        {
            options.CustomPlayers = CloneCustomPlayers(customPlayersSnapshot);
            changed = true;
        }
        if (builtInPlatformSnapshot is not null &&
            !BuiltInPlatformsEqual(options, builtInPlatformSnapshot))
        {
            RestoreBuiltInPlatforms(options, builtInPlatformSnapshot);
            changed = true;
        }
        if (builtInEnabledSnapshot is not null &&
            !BuiltInEnabledEqual(options, builtInEnabledSnapshot))
        {
            RestoreBuiltInEnabled(options, builtInEnabledSnapshot);
            changed = true;
        }
        if (separatePreferenceStoreReady && options.UserPlayerPreferences.Count > 0)
        {
            options.UserPlayerPreferences = new UserPlayerPreferenceOptionsCollection();
            changed = true;
        }
        return changed;
    }

    private static bool CustomPlayersEqual(
        IEnumerable<CustomPlayerOptions> first,
        IEnumerable<CustomPlayerOptions> second)
    {
        var left = first.ToArray();
        var right = second.ToArray();
        return left.Length == right.Length && left.Zip(right, (a, b) =>
            string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) &&
            a.Enabled == b.Enabled &&
            a.EnablePlaybackReporting == b.EnablePlaybackReporting &&
            string.Equals(a.ApplicationName, b.ApplicationName, StringComparison.Ordinal) &&
            a.GetEffectivePlatforms() == b.GetEffectivePlatforms() &&
            string.Equals(a.UrlTemplate, b.UrlTemplate, StringComparison.Ordinal)).All(equal => equal);
    }

    private static bool BuiltInPlatformsEqual(
        PluginOptions options,
        IReadOnlyDictionary<PlayerId, PlayerPlatforms> expected) =>
        expected.All(value => options.GetPlayerPlatforms(value.Key) == value.Value);

    private static bool BuiltInEnabledEqual(
        PluginOptions options,
        IReadOnlyDictionary<PlayerId, bool> expected) =>
        expected.All(value => options.IsPlayerEnabled(value.Key) == value.Value);

    private static CustomPlayerOptionsCollection CloneCustomPlayers(IEnumerable<CustomPlayerOptions> players) =>
        new(players.Select(player => new CustomPlayerOptions
        {
            Id = player.Id,
            Enabled = player.Enabled,
            ApplicationName = player.ApplicationName,
            Platform = player.Platform,
            Platforms = player.Platforms,
            UrlTemplate = player.UrlTemplate,
            EnablePlaybackReporting = player.EnablePlaybackReporting,
        }));

    private static UserPlayerPreferenceOptionsCollection CloneUserPlayerPreferences(
        IEnumerable<UserPlayerPreferenceOptions> preferences) =>
        new(preferences.Select(preference => new UserPlayerPreferenceOptions
        {
            UserId = preference.UserId,
            Platform = preference.Platform,
            PlayerId = preference.PlayerId,
        }));

    private static CustomPlayerConfiguration ToConfiguration(CustomPlayerOptions player) => new()
    {
        Id = player.Id,
        Enabled = player.Enabled,
        ApplicationName = player.ApplicationName,
        Platform = LegacyPlatformName(player.Platforms),
        Platforms = PlatformNames(player.Platforms),
        UrlTemplate = player.UrlTemplate,
        EnablePlaybackReporting = player.EnablePlaybackReporting,
    };

    private static PlayerPlatforms ParseCustomPlatforms(CustomPlayerConfiguration value)
    {
        var names = (value.Platforms ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        if (names.Length == 0 && !string.IsNullOrWhiteSpace(value.Platform))
        {
            names = new[] { value.Platform };
        }
        return ParsePlatformNames(names, allowLegacyAny: true);
    }

    private static string[] PlatformNames(PlayerPlatforms platforms)
    {
        var result = new List<string>();
        foreach (var candidate in new[]
                 {
                     PlayerPlatforms.Windows, PlayerPlatforms.MacOS,
                     PlayerPlatforms.IOS, PlayerPlatforms.Android,
                     PlayerPlatforms.Linux,
                 })
        {
            if ((platforms & candidate) != 0)
            {
                result.Add(candidate.ToString());
            }
        }
        return result.ToArray();
    }

    private static string LegacyPlatformName(PlayerPlatforms platforms)
    {
        var names = PlatformNames(platforms);
        return names.Length == 1 ? names[0] : "Any";
    }

    private static PlayerPlatforms ParsePlatformNames(
        IEnumerable<string>? platformNames,
        bool allowLegacyAny)
    {
        var names = (platformNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        if (names.Length == 0 || names.Length > 5)
        {
            throw new ArgumentException("At least one player platform is required.", nameof(platformNames));
        }
        var result = PlayerPlatforms.None;
        foreach (var name in names)
        {
            if (allowLegacyAny &&
                (string.Equals(name, "Any", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(name, "All", StringComparison.OrdinalIgnoreCase)))
            {
                return PlayerPlatforms.All;
            }
            if (name.Length > 16 || int.TryParse(name, out _) ||
                !Enum.TryParse(name, true, out PlayerPlatforms parsed) ||
                !Enum.IsDefined(typeof(PlayerPlatforms), parsed) ||
                parsed == PlayerPlatforms.None || parsed == PlayerPlatforms.All ||
                (parsed & ~PlayerPlatforms.All) != 0)
            {
                throw new ArgumentException("The player platform is invalid.", nameof(platformNames));
            }
            result |= parsed;
        }
        return result;
    }

    private static Dictionary<PlayerId, PlayerPlatforms> SnapshotBuiltInPlatforms(PluginOptions options) =>
        Enum.GetValues(typeof(PlayerId)).Cast<PlayerId>()
            .ToDictionary(playerId => playerId, options.GetPlayerPlatforms);

    private static Dictionary<PlayerId, bool> SnapshotBuiltInEnabled(PluginOptions options) =>
        Enum.GetValues(typeof(PlayerId)).Cast<PlayerId>()
            .ToDictionary(playerId => playerId, options.IsPlayerEnabled);

    private static void RestoreBuiltInPlatforms(
        PluginOptions options,
        IReadOnlyDictionary<PlayerId, PlayerPlatforms> values)
    {
        foreach (var value in values)
        {
            options.SetPlayerPlatforms(value.Key, value.Value);
        }
    }

    private static void RestoreBuiltInEnabled(
        PluginOptions options,
        IReadOnlyDictionary<PlayerId, bool> values)
    {
        foreach (var value in values)
        {
            options.SetPlayerEnabled(value.Key, value.Value);
        }
    }

    private static BuiltInPlayerPlatformConfiguration ToBuiltInPlatformConfiguration(
        PlayerId playerId,
        bool enabled,
        PlayerPlatforms platforms) => new()
        {
            PlayerId = playerId.ToString(),
            DisplayName = playerId switch
            {
                PlayerId.Iina => "IINA",
                PlayerId.Vlc => "VLC media player",
                PlayerId.Mpv => "mpv",
                PlayerId.NPlayer => "nPlayer",
                _ => playerId.ToString(),
            },
            Enabled = enabled,
            Platforms = PlatformNames(platforms),
        };

    public override void OnUninstalling()
    {
        Runtime?.Tickets.Clear();
        try
        {
            lock (optionsSync)
            {
                uninstalling = true;
                if (userPlayerPreferenceStore is not null)
                {
                    userPlayerPreferenceStore.DeleteFiles();
                }
                else
                {
                    UserPlayerPreferenceStore.DeleteFiles(preferenceConfigurationDirectory);
                }
            }
        }
        catch (Exception exception)
        {
            logger.ErrorException(
                "External Player could not delete its user preference files during uninstall.",
                exception);
        }
        PluginEntryPoint.Instance?.Refresh(new PluginOptions
        {
            Enabled = false,
            EnableWebButton = false,
        });
        base.OnUninstalling();
    }
}
