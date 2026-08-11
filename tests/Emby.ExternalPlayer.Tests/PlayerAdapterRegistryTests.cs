using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PlayerAdapterRegistryTests
{
    private const string StreamUrl = "https://emby.example/ExternalPlayer/Stream/a_b-c";
    private const string SubtitleUrl = "https://emby.example/ExternalPlayer/Subtitle/a_b-c/2.srt";

    [TestMethod]
    public void PotPlayer_EncodesStreamSubtitleAndResumePosition()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.PotPlayer, CreateContext());

        Assert.AreEqual(
            "potplayer://https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "?current=90&sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt",
            url);
    }

    [TestMethod]
    public void Iina_UsesWeblinkAndMpvOptions()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Iina, CreateContext());

        Assert.AreEqual(
            "iina://weblink?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&new_window=1&mpv_start=90" +
            "&mpv_sub_file=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt",
            url);
    }

    [TestMethod]
    public void Infuse_UsesOfficialCallbackShape()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Infuse, CreateContext());

        Assert.AreEqual(
            "infuse://x-callback-url/play?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c",
            url);
    }

    [TestMethod]
    public void Availability_FiltersDisabledAndForeignPlatformPlayers()
    {
        var options = new PluginOptions
        {
            EnablePotPlayer = true,
            EnableIina = true,
            EnableVlc = true,
            EnableInfuse = true,
        };

        var players = CreateRegistry().GetAvailable(options, ClientPlatform.Windows, true);

        CollectionAssert.AreEquivalent(
            new[] { PlayerId.PotPlayer, PlayerId.Vlc },
            players.Select(player => player.Id).ToArray());
    }

    [TestMethod]
    public void BuildLaunchUrl_RejectsRelativeStreamUrl()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateRegistry().BuildLaunchUrl(
                PlayerId.Vlc,
                new PlayerLaunchContext { StreamUrl = "/Videos/1/stream" }));
    }

    private static PlayerAdapterRegistry CreateRegistry() => new();

    private static PlayerLaunchContext CreateContext()
    {
        return new PlayerLaunchContext
        {
            StreamUrl = StreamUrl,
            SubtitleUrl = SubtitleUrl,
            StartPositionTicks = TimeSpan.FromSeconds(90).Ticks,
        };
    }
}
