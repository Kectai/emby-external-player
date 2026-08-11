using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Xml.Serialization;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PluginOptionsEditorTests
{
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
            Assert.IsFalse(container.EditorRoot.EditorItems.Any(item => item.EditorType == EditorTypes.DxDataGrid));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [TestMethod]
    public void PersistentXml_ContainsOnlyCustomPlayerData()
    {
        var serializer = new XmlSerializer(typeof(PluginOptions));
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        serializer.Serialize(writer, new PluginOptions());

        var xml = writer.ToString();
        Assert.IsTrue(xml.Contains(nameof(PluginOptions.CustomPlayers), StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains("CustomPlayersEditor", StringComparison.Ordinal));
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
                    UrlTemplate = "elmedia://open?url={url}",
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
        Assert.AreEqual(CustomPlayerPlatform.MacOS, restored.CustomPlayers[0].Platform);
    }
}
