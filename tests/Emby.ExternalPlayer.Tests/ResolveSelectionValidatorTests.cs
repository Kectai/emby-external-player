using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class ResolveSelectionValidatorTests
{
    [TestMethod]
    public void Validate_AcceptsOwnedSourceAndExternalSubtitle()
    {
        var context = CreateContext();

        var selection = ResolveSelectionValidator.Validate(
            new PluginOptions(),
            new PlayerAdapterRegistry(),
            context,
            "PotPlayer",
            ClientPlatform.Windows,
            "source-1",
            3);

        Assert.AreEqual("PotPlayer", selection.PlayerId);
        Assert.AreEqual("source-1", selection.MediaSource.Id);
        Assert.AreEqual(3, selection.Subtitle?.Index);
    }

    [TestMethod]
    public void Validate_RejectsUnknownOrDisabledPlayer()
    {
        var context = CreateContext();
        var options = new PluginOptions { EnablePotPlayer = false };

        Assert.ThrowsExactly<ArgumentException>(() => ResolveSelectionValidator.Validate(
            options, new PlayerAdapterRegistry(), context, "unknown", ClientPlatform.Windows, "source-1", null));
        Assert.ThrowsExactly<ArgumentException>(() => ResolveSelectionValidator.Validate(
            options, new PlayerAdapterRegistry(), context, "PotPlayer", ClientPlatform.Windows, "source-1", null));
    }

    [TestMethod]
    public void Validate_RejectsForeignSourceOrSubtitle()
    {
        var context = CreateContext();
        var options = new PluginOptions();
        var players = new PlayerAdapterRegistry();

        Assert.ThrowsExactly<ArgumentException>(() => ResolveSelectionValidator.Validate(
            options, players, context, "PotPlayer", ClientPlatform.Windows, "source-2", null));
        Assert.ThrowsExactly<ArgumentException>(() => ResolveSelectionValidator.Validate(
            options, players, context, "PotPlayer", ClientPlatform.Windows, "source-1", 4));
    }

    [TestMethod]
    public void Validate_AcceptsSubtitleSelectionForPlayerWithoutAutomaticSubtitleCapability()
    {
        var selection = ResolveSelectionValidator.Validate(
            new PluginOptions(),
            new PlayerAdapterRegistry(),
            CreateContext(),
            "Iina",
            ClientPlatform.MacOS,
            "source-1",
            3);

        Assert.AreEqual(3, selection.Subtitle?.Index);
        Assert.IsFalse(selection.Player.Capabilities.HasFlag(PlayerCapabilities.ExternalSubtitle));
    }

    private static MediaManifestContext CreateContext()
    {
        return new MediaManifestContext
        {
            MediaSources = new[]
            {
                new MediaSourceInfo
                {
                    Id = "source-1",
                    MediaStreams = new List<MediaStream>
                    {
                        new() { Index = 3, Type = MediaStreamType.Subtitle, IsExternal = true },
                        new() { Index = 4, Type = MediaStreamType.Subtitle, IsExternal = false },
                    },
                },
            },
        };
    }
}
