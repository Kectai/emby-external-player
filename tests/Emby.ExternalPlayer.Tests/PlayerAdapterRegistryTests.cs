using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class PlayerAdapterRegistryTests
{
    private const string CustomId = "0123456789abcdef0123456789abcdef";
    private const string CustomPlayerId = "custom-" + CustomId;
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
    public void Iina_PassesShortLivedTicketInAnHttpHeaderInsteadOfTheMediaUrl()
    {
        var context = CreateContext();
        context.HttpRequestHeaders = new[]
        {
            ServerUrlBuilder.PlaybackTicketHeaderName + ": short_lived-ticket",
        };

        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Iina, context);

        Assert.AreEqual(
            "iina://weblink?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&new_window=1&mpv_start=90" +
            "&mpv_http-header-fields=X-Emby-Playback-Ticket%3A%20short_lived-ticket",
            url);
        Assert.IsFalse(new Uri(url).Query.Split('&')[0].Contains("api_key", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Iina_IsolatesHeaderAuthenticatedPlaybackInANewWindow()
    {
        var context = CreateContext();
        context.StartPositionTicks = 0;
        context.HttpRequestHeaders = new[] { "X-Emby-Playback-Ticket: short_lived-ticket" };

        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Iina, context);

        StringAssert.Contains(url, "&new_window=1&mpv_http-header-fields=");
        Assert.IsFalse(url.Contains("mpv_start=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Mpv_UsesTheOfficialUrlProtocolShape()
    {
        var context = CreateContext();
        context.StreamUrl = "https://emby.example/media/a file.mkv?quality=原画";

        var url = CreateRegistry().BuildLaunchUrl(PlayerId.Mpv, context);

        Assert.AreEqual(
            "mpv://https://emby.example/media/a%20file.mkv?quality=%E5%8E%9F%E7%94%BB",
            url);
        Assert.IsFalse(url.Contains("mpv://play/", StringComparison.Ordinal));
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
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "myPLAYER pro",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "myplayer://open?url={url}&title={title}&sub={subtitle}&start={start}",
                },
            },
        };

        var descriptor = CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Single(player => player.Id == CustomPlayerId);
        var url = CreateRegistry().BuildLaunchUrl(CustomPlayerId, options, CreateContext());

        Assert.AreEqual("myPLAYER pro", descriptor.DisplayName);
        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.ExternalSubtitle));
        CollectionAssert.AreEqual(new[] { "myplayer" }, descriptor.LaunchSchemes.ToArray());
        Assert.AreEqual(
            "myplayer://open?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&title=Movie%20%26%20One" +
            "&sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2Fa_b-c%2F2.srt&start=90",
            url);
    }

    [TestMethod]
    public void CustomPlayer_RemovesOnlyQueryParametersWhosePlaceholderValueIsEmpty()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "Optional parameters",
                    UrlTemplate = "optional://open?sub={subtitle}&url={url}&title={title}" +
                        "&label=prefix-{title}&empty=#fragment",
                },
            },
        };
        var context = CreateContext();
        context.SubtitleUrl = null;
        context.Title = null;

        var url = CreateRegistry().BuildLaunchUrl(CustomPlayerId, options, context);

        Assert.AreEqual(
            "optional://open?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&label=prefix-&empty=#fragment",
            url);
        Assert.IsFalse(url.Contains("sub=", StringComparison.Ordinal));
        Assert.IsFalse(url.Contains("title=", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CustomPlayer_RemovesEmptyHeaderParameterButKeepsZeroStart()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "Optional headers",
                    UrlTemplate = "optional://open?url={url}&headers={headers}&start={start}",
                },
            },
        };
        var context = CreateContext();
        context.HttpRequestHeaders = Array.Empty<string>();
        context.StartPositionTicks = 0;

        var url = CreateRegistry().BuildLaunchUrl(CustomPlayerId, options, context);

        Assert.IsFalse(url.Contains("headers=", StringComparison.Ordinal));
        StringAssert.EndsWith(url, "&start=0");
    }

    [TestMethod]
    public void CustomPlayer_CanBeAvailableOnMultipleSelectedPlatforms()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "Cross-platform player",
                    Platforms = PlayerPlatforms.Windows | PlayerPlatforms.MacOS,
                    UrlTemplate = "cross-player://open?url={url}",
                },
            },
        };

        Assert.IsTrue(CreateRegistry().GetAvailable(options, ClientPlatform.Windows, true)
            .Any(player => player.Id == CustomPlayerId));
        Assert.IsTrue(CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Any(player => player.Id == CustomPlayerId));
        Assert.IsFalse(CreateRegistry().GetAvailable(options, ClientPlatform.IOS, true)
            .Any(player => player.Id == CustomPlayerId));
    }

    [TestMethod]
    public void BuiltInPlayerAvailability_UsesAdministratorConfiguredPlatforms()
    {
        var options = new PluginOptions
        {
            EnablePotPlayer = true,
            EnableIina = true,
            EnableVlc = true,
            EnableInfuse = true,
            EnableMpv = true,
            EnableNPlayer = true,
        };
        var registry = CreateRegistry();

        options.IinaPlatformScope = PlayerPlatforms.MacOS | PlayerPlatforms.IOS;
        options.VlcPlatformScope = PlayerPlatforms.Windows;

        Assert.IsTrue(registry.GetAvailable(options, ClientPlatform.IOS, true)
            .Any(player => player.BuiltInId == PlayerId.Iina));
        Assert.IsFalse(registry.GetAvailable(options, ClientPlatform.Android, true)
            .Any(player => player.BuiltInId == PlayerId.Vlc));
        Assert.IsTrue(registry.GetAvailable(options, ClientPlatform.Windows, true)
            .Any(player => player.BuiltInId == PlayerId.Vlc));
    }

    [TestMethod]
    public void CustomIinaDerivedPlayer_UsesPlaybackTicketHeaderWithoutAnApiKeyQuery()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "IINA Nova",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "iina-nova://weblink?url={url}&new_window=1&mpv_start={start}&mpv_http-header-fields={headers}",
                    EnablePlaybackReporting = true,
                },
            },
        };
        var context = CreateContext();
        context.HttpRequestHeaders = new[] { "X-Emby-Playback-Ticket: short_ticket" };

        var descriptor = CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Single(player => player.Id == CustomPlayerId);
        var url = CreateRegistry().BuildLaunchUrl(CustomPlayerId, options, context);

        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.HttpRequestHeaders));
        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.PlaybackReporting));
        Assert.AreEqual(
            "iina-nova://weblink?url=https%3A%2F%2Femby.example%2FExternalPlayer%2FStream%2Fa_b-c" +
            "&new_window=1&mpv_start=90" +
            "&mpv_http-header-fields=X-Emby-Playback-Ticket%3A%20short_ticket",
            url);
        Assert.IsFalse(url.Contains("api_key", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void CustomIinaDerivedPlayer_UsesSeparateHeaderTicketsForCleanSubtitleUrl()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "IINA Nova",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "iina-nova://weblink?url={url}&sub={subtitle}" +
                        "&mpv_http-header-fields={headers}",
                },
            },
        };
        var context = CreateContext();
        context.SubtitleUrl = "https://emby.example/ExternalPlayer/Subtitle/2/subtitle.ass";
        context.HttpRequestHeaders = new[]
        {
            ServerUrlBuilder.PlaybackTicketHeaderName + ": media_ticket",
            ServerUrlBuilder.SubtitleTicketHeaderName + ": subtitle_ticket",
        };

        var url = CreateRegistry().BuildLaunchUrl(CustomPlayerId, options, context);

        var query = new Uri(url).Query;
        Assert.IsFalse(query.Contains("api_key", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(query, "sub=https%3A%2F%2Femby.example%2FExternalPlayer%2FSubtitle%2F2%2Fsubtitle.ass");
        StringAssert.Contains(query, "X-Emby-Playback-Ticket%3A%20media_ticket%2CX-Emby-Subtitle-Ticket%3A%20subtitle_ticket");
    }

    [TestMethod]
    public void CustomPlayer_DoesNotInferHeaderSupportFromMpvLikeParameters()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "Generic player",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "generic://weblink?url={url}&mpv_start={start}",
                },
            },
        };

        var descriptor = CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Single(player => player.Id == CustomPlayerId);

        Assert.IsFalse(descriptor.Capabilities.HasFlag(PlayerCapabilities.HttpRequestHeaders));
        Assert.IsFalse(descriptor.Capabilities.HasFlag(PlayerCapabilities.PlaybackReporting));
    }

    [TestMethod]
    public void CustomHeaderPlayer_ReceivesExplicitPlaybackReportingCapabilityForAnyScheme()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "Generic header player",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "generic://open?url={url}&headers={headers}",
                    EnablePlaybackReporting = true,
                },
            },
        };

        var descriptor = CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Single(player => player.Id == CustomPlayerId);

        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.HttpRequestHeaders));
        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.PlaybackReporting));
    }

    [TestMethod]
    public void CustomIinaScheme_DoesNotImplicitlyEnablePlaybackReporting()
    {
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new()
                {
                    Id = CustomId,
                    Enabled = true,
                    ApplicationName = "IINA Fork",
                    Platform = CustomPlayerPlatform.MacOS,
                    UrlTemplate = "third-party-iina://open?url={url}&headers={headers}",
                },
            },
        };

        var descriptor = CreateRegistry().GetAvailable(options, ClientPlatform.MacOS, true)
            .Single(player => player.Id == CustomPlayerId);

        Assert.IsTrue(descriptor.Capabilities.HasFlag(PlayerCapabilities.HttpRequestHeaders));
        Assert.IsFalse(descriptor.Capabilities.HasFlag(PlayerCapabilities.PlaybackReporting));
    }

    [TestMethod]
    public void CustomPlayer_RuntimeIdDoesNotChangeWhenAnotherPlayerIsRemoved()
    {
        const string otherId = "fedcba9876543210fedcba9876543210";
        var options = new PluginOptions
        {
            CustomPlayers = new CustomPlayerOptionsCollection
            {
                new() { Id = otherId, Enabled = true, ApplicationName = "First", UrlTemplate = "first://open?url={url}" },
                new() { Id = CustomId, Enabled = true, ApplicationName = "Stable", UrlTemplate = "stable://open?url={url}" },
            },
        };
        var registry = CreateRegistry();
        var before = registry.GetAvailable(options, ClientPlatform.Unknown, false)
            .Single(player => player.DisplayName == "Stable").Id;

        options.CustomPlayers.RemoveAt(0);
        var after = registry.GetAvailable(options, ClientPlatform.Unknown, false)
            .Single(player => player.DisplayName == "Stable").Id;

        Assert.AreEqual(CustomPlayerId, before);
        Assert.AreEqual(before, after);
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

    [TestMethod]
    public void BuildLaunchUrl_RejectsHeaderInjection()
    {
        var context = CreateContext();
        context.HttpRequestHeaders = new[] { "X-Test: safe\r\nX-Injected: unsafe" };

        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateRegistry().BuildLaunchUrl(PlayerId.Iina, context));
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
