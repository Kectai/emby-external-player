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
            "&mpv_force-media-title=Movie%20%26%20One" +
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
            new[] { "PotPlayer", "Vlc" },
            players.Select(player => player.Id).ToArray());
    }

    [TestMethod]
    public void Availability_UsesOfficialApplicationNamesWithoutChangingCase()
    {
        var players = CreateRegistry().GetAvailable(new PluginOptions
        {
            EnablePotPlayer = true,
            EnableIina = true,
            EnableVlc = true,
            EnableInfuse = true,
            EnableMpv = true,
            EnableNPlayer = true,
            ShowOnlyPlatformPlayers = false,
        }, ClientPlatform.Unknown, false);

        CollectionAssert.AreEquivalent(
            new[] { "PotPlayer", "IINA", "VLC media player", "Infuse", "mpv", "nPlayer" },
            players.Where(player => player.BuiltInId.HasValue).Select(player => player.DisplayName).ToArray());
    }

    [TestMethod]
    public void CustomPlayer_PreservesNameAndExpandsGenericContext()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Enabled = true,
                    ApplicationName = "myPLAYER pro",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "myplayer://open?url={url}&title={title}&sub={subtitle}&start={start}",
                },
            },
        };

        var descriptor = CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Single(player => player.Id == "custom-1");
        var url = CreateRegistry().BuildLaunchUrl("custom-1", options, CreateContext());

        Assert.AreEqual("myPLAYER pro", descriptor.DisplayName);
        CollectionAssert.AreEqual(new[] { "myplayer" }, descriptor.LaunchSchemes.ToArray());
        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.DisplayTitle));
        Assert.AreEqual(
            "myplayer://open?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&title=Movie%20%26%20One" +
            "&sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt&start=90",
            url);
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
            Title = "Movie & One",
            StartPositionTicks = TimeSpan.FromSeconds(90).Ticks,
        };
    }
}
