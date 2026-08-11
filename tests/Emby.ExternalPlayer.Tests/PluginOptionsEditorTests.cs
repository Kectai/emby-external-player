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
    public void CustomPlayers_UseTheOfficialDynamicChildCollectionPattern()
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

            Assert.AreEqual(2, options.CustomPlayers.Count, "keep valid rows and append exactly one empty add row");
            Assert.AreEqual("myPLAYER pro", options.CustomPlayers[0].EditorTitle);
            Assert.AreEqual("添加播放器", options.CustomPlayers[1].EditorTitle);

            var descriptor = TypeDescriptor.GetProperties(options)[nameof(PluginOptions.CustomPlayers)];
            Assert.IsNotNull(descriptor);
            Assert.IsTrue(descriptor.IsBrowsable);
            Assert.AreEqual("自定义播放器", descriptor.DisplayName);

            var container = (EditObjectContainer)options.CreateEditContainer();
            Assert.IsTrue(container.EditorRoot.EditorItems.Any(item =>
                item.Id == nameof(PluginOptions.CustomPlayers) && item.EditorType == EditorTypes.Group));
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
    public void GenericUiPageSavePayload_RoundTripsAndPrunesTheEmptyAddRow()
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
        Assert.AreEqual(2, restored.CustomPlayers.Count);
        restored.NormalizeCustomPlayers();
        Assert.AreEqual(1, restored.CustomPlayers.Count);
        Assert.AreEqual("Elmedia Video Player", restored.CustomPlayers[0].ApplicationName);
        Assert.AreEqual(CustomPlayerPlatform.MacOS, restored.CustomPlayers[0].Platform);
    }
}
