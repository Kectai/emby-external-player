using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PlayerAdapterRegistryTests
{
    private const string StreamUrl = "https://emby.example/ExternalPlayer/Stream/a_b-c";
    private const string SubtitleUrl = "https://emby.example/ExternalPlayer/Subtitle/a_b-c/2.srt";

    [TestMethod]
    public void PotPlayer_UsesCommandArgumentsForResumeAndSubtitle()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.PotPlayer, CreateContext());

        Assert.AreEqual(
            "potplayer://https://emby.example/ExternalPlayer/Stream/a_b-c" +
            " /current /seek=90" +
            " /sub=https://emby.example/ExternalPlayer/Subtitle/a_b-c/2.srt",
            url);
    }

    [TestMethod]
    public void BuildLaunchUrl_CanonicalizesLiteralWhitespaceBeforePotPlayerArguments()
    {
        var context = CreateContext();
        context.StreamUrl = "https://emby.example/media/a file.mkv";
        context.SubtitleUrl = "https://emby.example/sub/a file.srt";

        var url = CreateRegistry().BuildLaunchUrl(PlayerId.PotPlayer, context);

        Assert.AreEqual(
            "potplayer://https://emby.example/media/a%20file.mkv" +
            " /current /seek=90 /sub=https://emby.example/sub/a%20file.srt",
            url);
    }

    [TestMethod]
    public void Iina_UsesOnlySafeSupportedWeblinkOptions()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Iina, CreateContext());

        Assert.AreEqual(
            "iina://weblink?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&new_window=1&mpv_start=90",
            url);
    }

    [TestMethod]
    public void Infuse_UsesOfficialCallbackShape()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Infuse, CreateContext());

        Assert.AreEqual(
            "infuse://x-callback-url/play?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c",
            url.Replace(
                "&sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt",
                string.Empty));
    }

    [TestMethod]
    public void Infuse_IncludesOfficialSubtitleParameter()
    {
        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Infuse, CreateContext());

        StringAssert.EndsWith(
            url,
            "&sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt");
    }

    [TestMethod]
    public void VlcIos_UsesCallbackSchemeAndSubtitleParameter()
    {
        var context = CreateContext();
        context.Platform = ClientPlatform.IOS;

        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Vlc, context);

        Assert.AreEqual(
            "vlc-x-callback://x-callback-url/stream?" +
            "url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c&" +
            "sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt",
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

    [TestMethod]
    public void BuildLaunchUrl_RejectsNonHttpSubtitleUrl()
    {
        var context = CreateContext();
        context.SubtitleUrl = "file:///etc/passwd";

        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateRegistry().BuildLaunchUrl(PlayerId.PotPlayer, context));
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
