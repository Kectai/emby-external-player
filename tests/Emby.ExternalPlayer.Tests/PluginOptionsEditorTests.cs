using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Xml.Serialization;
using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PluginOptionsEditorTests
{
    [TestMethod]
    public void TicketLifetime_UsesEmbyRangeMetadata_AndNormalizesTamperedPersistence()
    {
        var property = typeof(PluginOptions).GetProperty(nameof(PluginOptions.TicketLifetimeMinutes));
        Assert.IsNotNull(property);
        var minimum = property.GetCustomAttributes(typeof(MinValueAttribute), false).SingleOrDefault();
        var maximum = property.GetCustomAttributes(typeof(MaxValueAttribute), false).SingleOrDefault();
        Assert.IsNotNull(minimum, "Emby Generic UI must receive its native minimum-value metadata");
        Assert.IsNotNull(maximum, "Emby Generic UI must receive its native maximum-value metadata");
        var container = (EditObjectContainer)new PluginOptions().CreateEditContainer();
        var editor = container.EditorRoot.EditorItems.Single(
            item => item.Id == nameof(PluginOptions.TicketLifetimeMinutes));
        Assert.AreEqual(
            LaunchTicketStore.MinimumLifetimeMinutes,
            Convert.ToInt32(editor.GetType().GetProperty("MinValue")?.GetValue(editor), CultureInfo.InvariantCulture));
        Assert.AreEqual(
            LaunchTicketStore.MaximumLifetimeMinutes,
            Convert.ToInt32(editor.GetType().GetProperty("MaxValue")?.GetValue(editor), CultureInfo.InvariantCulture));

        var options = JsonSerializer.Deserialize<PluginOptions>("{\"TicketLifetimeMinutes\":2147483647}");
        Assert.IsNotNull(options);
        Assert.IsTrue(options.NormalizeTicketLifetime());
        Assert.AreEqual(LaunchTicketStore.DefaultLifetimeMinutes, options.TicketLifetimeMinutes);
        Assert.IsFalse(options.NormalizeTicketLifetime());

        Assert.ThrowsExactly<ValidationException>(() =>
            new PluginOptions { TicketLifetimeMinutes = 29 }.ValidateOrThrow());
        Assert.ThrowsExactly<ValidationException>(() =>
            new PluginOptions { TicketLifetimeMinutes = 721 }.ValidateOrThrow());
    }

    [TestMethod]
    public void CustomPlayers_AreManagedByTheIndependentConfigurationApi()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var options = new PluginOptions
            {
                CustomPlayers = new CustomPlayerOptionsCollection
                {
                    new(),
                    new()
                    {
                        Enabled = true,
                        ApplicationName = "myPLAYER pro",
                        Platform = CustomPlayerPlatform.MacOS,
                        UrlTemplate = "myplayer://open?url={url}",
                    },
                },
            };

            options.PrepareForEditor();

            Assert.AreEqual(1, options.CustomPlayers.Count, "legacy empty rows must be removed");
            Assert.AreEqual("myPLAYER pro", options.CustomPlayers[0].EditorTitle);
            Assert.IsFalse(string.IsNullOrWhiteSpace(options.CustomPlayers[0].Id));

            var descriptor = TypeDescriptor.GetProperties(options)[nameof(PluginOptions.CustomPlayers)];
            Assert.IsNotNull(descriptor);
            Assert.IsFalse(descriptor.IsBrowsable);

            var container = (EditObjectContainer)options.CreateEditContainer();
            Assert.IsFalse(container.EditorRoot.EditorItems.Any(item =>
                item.Id == nameof(PluginOptions.CustomPlayers)));
            Assert.IsFalse(container.EditorRoot.EditorItems.Any(item =>
                item.Id == nameof(PluginOptions.UserPlayerPreferences)));
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    nameof(PluginOptions.DefaultPlayerWindows),
                    nameof(PluginOptions.DefaultPlayerMacOS),
                    nameof(PluginOptions.DefaultPlayerIOS),
                    nameof(PluginOptions.DefaultPlayerAndroid),
                },
                container.EditorRoot.EditorItems.Select(item => item.Id).ToArray(),
                "administrator defaults for each client platform must remain in Emby's settings editor");
            Assert.IsFalse(container.EditorRoot.EditorItems.Any(item => item.EditorType == EditorTypes.DxDataGrid));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [TestMethod]
    public void PersistentXml_ContainsIndependentPlayerConfigurationData()
    {
        var serializer = new XmlSerializer(typeof(PluginOptions));
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        serializer.Serialize(writer, new PluginOptions());

        var xml = writer.ToString();
        Assert.IsTrue(xml.Contains(nameof(PluginOptions.CustomPlayers), StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains(nameof(PluginOptions.UserPlayerPreferences), StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains(nameof(PluginOptions.IinaPlatformScope), StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains(nameof(PluginOptions.DefaultPlayerMacOS), StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains("CustomPlayersEditor", StringComparison.Ordinal));
        Assert.IsFalse(
            xml.Contains("RestartNearEndMinutes", StringComparison.Ordinal),
            "the plugin-specific resume threshold must no longer be persisted");
    }

    [TestMethod]
    public void PersistentXml_IgnoresTheRemovedLegacyResumeThreshold()
    {
        var serializer = new XmlSerializer(typeof(PluginOptions));
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        serializer.Serialize(writer, new PluginOptions());
        var legacyXml = writer.ToString().Replace(
            "</PluginOptions>",
            "<RestartNearEndMinutes>5</RestartNearEndMinutes></PluginOptions>",
            StringComparison.Ordinal);

        using var reader = new StringReader(legacyXml);
        var restored = serializer.Deserialize(reader) as PluginOptions;

        Assert.IsNotNull(restored, "existing 1.5.2 configuration must still load after the setting is removed");
        Assert.IsTrue(restored.ResumeByDefault);
    }

    [TestMethod]
    public void PersistentPayload_RoundTripsCustomPlayerIds()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Enabled = true,
                    ApplicationName = "Elmedia Video Player",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "elmedia://open?url={url}&headers={headers}",
                    EnablePlaybackReporting = true,
                },
            },
        };
        options.PrepareForEditor();

        var payload = JsonSerializer.Serialize(options);
        var restored = JsonSerializer.Deserialize<PluginOptions>(payload);

        Assert.IsNotNull(restored);
        Assert.AreEqual(1, restored.CustomPlayers.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(restored.CustomPlayers[0].Id));
        Assert.AreEqual("Elmedia Video Player", restored.CustomPlayers[0].ApplicationName);
        Assert.AreEqual(PlayerPlatforms.MacOS, restored.CustomPlayers[0].Platforms);
        Assert.IsTrue(restored.CustomPlayers[0].EnablePlaybackReporting);
    }

    [TestMethod]
    public void PlaybackReporting_NormalizationDisablesTamperedConfigurationWithoutHeaders()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Enabled = true,
                    ApplicationName = "Third-party IINA",
                    UrlTemplate = "third-party-iina://open?url={url}",
                    EnablePlaybackReporting = true,
                },
            },
        };

        Assert.IsTrue(options.NormalizeCustomPlayers());
        Assert.IsFalse(options.CustomPlayers[0].EnablePlaybackReporting);
        Assert.IsFalse(options.NormalizeCustomPlayers());
    }

    [TestMethod]
    public void BuiltInPlatformScopes_AreMultiPlatformAndHaveStableDefaults()
    {
        var options = new PluginOptions();

        Assert.AreEqual(PlayerPlatforms.Windows, options.GetPlayerPlatforms(PlayerId.PotPlayer));
        Assert.AreEqual(PlayerPlatforms.MacOS, options.GetPlayerPlatforms(PlayerId.Iina));
        Assert.AreEqual(PlayerPlatforms.All, options.GetPlayerPlatforms(PlayerId.Vlc));
        Assert.AreEqual(
            PlayerPlatforms.MacOS | PlayerPlatforms.IOS,
            options.GetPlayerPlatforms(PlayerId.Infuse));

        options.SetPlayerPlatforms(PlayerId.Iina, PlayerPlatforms.MacOS | PlayerPlatforms.IOS);

        Assert.AreEqual(
            PlayerPlatforms.MacOS | PlayerPlatforms.IOS,
            options.GetPlayerPlatforms(PlayerId.Iina));
        Assert.AreEqual(PlayerId.Iina, options.GetDefaultPlayer(ClientPlatform.MacOS));
        Assert.AreEqual(PlayerId.Infuse, options.GetDefaultPlayer(ClientPlatform.IOS));
    }

    [TestMethod]
    public void BuiltInPlatformScopes_RejectEmptyAndUnknownFlags()
    {
        var options = new PluginOptions();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            options.SetPlayerPlatforms(PlayerId.Iina, PlayerPlatforms.None));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            options.SetPlayerPlatforms(PlayerId.Iina, (PlayerPlatforms)64));
    }

    [TestMethod]
    public void BuiltInPlatformScopes_RejectRemovingAnAdministratorDefaultPlatform()
    {
        var options = new PluginOptions();
        options.SetPlayerPlatforms(PlayerId.Iina, PlayerPlatforms.IOS);

        Assert.ThrowsExactly<ValidationException>(() => options.ValidateOrThrow());
    }

    [TestMethod]
    public void UserDefaultPlayers_AreIsolatedByAuthenticatedUserAndPlatform()
    {
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var options = new PluginOptions();

        options.SetUserDefaultPlayer(firstUser, ClientPlatform.MacOS, "Iina");
        options.SetUserDefaultPlayer(firstUser, ClientPlatform.Windows, "Vlc");
        options.SetUserDefaultPlayer(secondUser, ClientPlatform.MacOS, "custom-0123456789abcdef0123456789abcdef");

        Assert.AreEqual("Iina", options.GetUserDefaultPlayer(firstUser, ClientPlatform.MacOS));
        Assert.AreEqual("Vlc", options.GetUserDefaultPlayer(firstUser, ClientPlatform.Windows));
        Assert.AreEqual(
            "custom-0123456789abcdef0123456789abcdef",
            options.GetUserDefaultPlayer(secondUser, ClientPlatform.MacOS));
        Assert.IsNull(options.GetUserDefaultPlayer(secondUser, ClientPlatform.Windows));

        options.SetUserDefaultPlayer(firstUser, ClientPlatform.MacOS, "Vlc");
        Assert.AreEqual("Vlc", options.GetUserDefaultPlayer(firstUser, ClientPlatform.MacOS));
        Assert.AreEqual("Vlc", options.GetUserDefaultPlayer(firstUser, ClientPlatform.Windows));
    }

    [TestMethod]
    public void NormalizeUserPlayerPreferences_RemovesInvalidAndDuplicateEntries()
    {
        var userId = Guid.NewGuid().ToString("N");
        var options = new PluginOptions
        {
            UserPlayerPreferences = new UserPlayerPreferenceOptionsCollection
            {
                new() { UserId = "not-a-user", Platform = ClientPlatform.MacOS, PlayerId = "Iina" },
                new() { UserId = userId, Platform = ClientPlatform.Unknown, PlayerId = "Iina" },
                new() { UserId = userId, Platform = ClientPlatform.MacOS, PlayerId = "invalid://player" },
                new() { UserId = userId, Platform = ClientPlatform.MacOS, PlayerId = "Iina" },
                new() { UserId = userId, Platform = ClientPlatform.MacOS, PlayerId = "Vlc" },
            },
        };

        Assert.IsTrue(options.NormalizeUserPlayerPreferences());
        Assert.AreEqual(1, options.UserPlayerPreferences.Count);
        Assert.AreEqual("Vlc", options.UserPlayerPreferences[0].PlayerId, "the newest preference must win");
        Assert.IsFalse(options.NormalizeUserPlayerPreferences(), "normalization must be idempotent");
    }

    [TestMethod]
    public void SetUserDefaultPlayer_RejectsUnknownPlatformsAndInvalidPlayerIds()
    {
        var options = new PluginOptions();
        var userId = Guid.NewGuid();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            options.SetUserDefaultPlayer(userId, ClientPlatform.Unknown, "Iina"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            options.SetUserDefaultPlayer(userId, ClientPlatform.MacOS, "javascript:alert(1)"));
    }

    [TestMethod]
    public void NormalizeCustomPlayers_DoesNotInferHeaderCapabilityFromLegacyTemplates()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Enabled = true,
                    ApplicationName = "IINA Nova",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "iina-nova://weblink?url={url}&new_window=1&mpv_start={start}",
                },
            },
        };

        Assert.IsTrue(options.NormalizeCustomPlayers(), "a stable id must be assigned");
        Assert.AreEqual(
            PlayerPlatforms.MacOS,
            options.CustomPlayers[0].Platforms,
            "the legacy single platform must migrate without broadening availability");
        Assert.AreEqual(
            "iina-nova://weblink?url={url}&new_window=1&mpv_start={start}",
            options.CustomPlayers[0].UrlTemplate,
            "capability-bearing placeholders require an explicit administrator edit");
        Assert.IsFalse(options.NormalizeCustomPlayers(), "normalization must be idempotent");
    }

    [TestMethod]
    public void NormalizeCustomPlayers_MigratesLegacyAnyToAllPlatforms()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Enabled = true,
                    ApplicationName = "Cross-platform handler",
                    Platform = CustomPlayerPlatform.Any,
                    UrlTemplate = "cross-player://open?url={url}",
                },
            },
        };

        Assert.IsTrue(options.NormalizeCustomPlayers());
        Assert.AreEqual(PlayerPlatforms.All, options.CustomPlayers[0].Platforms);
    }

    [TestMethod]
    public void NormalizeCustomPlayers_RepairsDuplicatePersistentIds()
    {
        const string duplicate = "0123456789abcdef0123456789abcdef";
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new() { Id = duplicate, Enabled = true, ApplicationName = "One", UrlTemplate = "one://open?url={url}" },
                new() { Id = duplicate, Enabled = true, ApplicationName = "Two", UrlTemplate = "two://open?url={url}" },
            },
        };

        Assert.IsTrue(options.NormalizeCustomPlayers());
        Assert.AreNotEqual(options.CustomPlayers[0].Id, options.CustomPlayers[1].Id);
    }

    [TestMethod]
    public void NormalizeCustomPlayers_RepairsNullLegacyValuesWithoutFailingPluginLoad()
    {
        var player = new CustomPlayerOptions();
        typeof(CustomPlayerOptions).GetProperty(nameof(CustomPlayerOptions.ApplicationName))!
            .SetValue(player, null);
        typeof(CustomPlayerOptions).GetProperty(nameof(CustomPlayerOptions.UrlTemplate))!
            .SetValue(player, null);
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection { player },
        };

        options.NormalizeCustomPlayers();

        Assert.AreEqual(0, options.CustomPlayers.Count, "a fully empty legacy row must be removed");
    }
}
