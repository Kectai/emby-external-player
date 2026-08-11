using System.Globalization;
using System.Text.Json;
using System.Xml.Serialization;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Editors;
using Emby.Web.GenericEdit.Elements.DxGrid;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PluginOptionsEditorTests
{
    [TestMethod]
    public void CustomPlayers_UseAnAddEditDeleteGridInsteadOfFixedSlots()
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

            Assert.AreEqual(1, options.CustomPlayers.Count, "legacy empty slots must be removed");
            Assert.AreEqual("自定义播放器", options.CustomPlayersCaption.Caption);
            Assert.IsTrue(options.CustomPlayersCaption.IsVisible);
            Assert.IsTrue(options.CustomPlayersHelp.IsVisible);
            Assert.AreEqual(true, options.CustomPlayersEditor.Options.editing.allowAdding);
            Assert.AreEqual(true, options.CustomPlayersEditor.Options.editing.allowDeleting);
            Assert.AreEqual(true, options.CustomPlayersEditor.Options.editing.allowUpdating);
            Assert.AreEqual("添加播放器", options.CustomPlayersEditor.Options.editing.texts.addRow);
            Assert.AreEqual(false, options.CustomPlayersEditor.Options.paging.enabled);

            var container = (EditObjectContainer)options.CreateEditContainer();
            var gridEditor = container.EditorRoot.EditorItems.OfType<EditorDxGrid>().Single();
            Assert.AreEqual(nameof(PluginOptions.CustomPlayers), gridEditor.DataSourceId);
            Assert.IsFalse(container.EditorRoot.EditorItems.Any(item =>
                item.Id == nameof(PluginOptions.CustomPlayers) && item.EditorType == EditorTypes.Group));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [TestMethod]
    public void CustomPlayerEditorMetadata_IsNotWrittenToPersistentXml()
    {
        var serializer = new XmlSerializer(typeof(PluginOptions));
        using var writer = new StringWriter(CultureInfo.InvariantCulture);

        serializer.Serialize(writer, new PluginOptions());

        var xml = writer.ToString();
        Assert.IsFalse(xml.Contains(nameof(PluginOptions.CustomPlayersEditor), StringComparison.Ordinal));
        Assert.IsFalse(xml.Contains(nameof(PluginOptions.CustomPlayersCaption), StringComparison.Ordinal));
        Assert.IsTrue(xml.Contains(nameof(PluginOptions.CustomPlayers), StringComparison.Ordinal));
    }

    [TestMethod]
    public void GenericUiPageSavePayload_RoundTripsDynamicPlayerRows()
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
        Assert.AreEqual("Elmedia Video Player", restored.CustomPlayers[0].ApplicationName);
        Assert.AreEqual(CustomPlayerPlatform.MacOS, restored.CustomPlayers[0].Platform);
    }
}
