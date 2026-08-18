using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class ServerUrlBuilderTests
{
    [TestMethod]
    public void GetApiBase_PreservesReverseProxyPrefix()
    {
        var result = ServerUrlBuilder.GetApiBase(
            "https://media.example/emby/ExternalPlayer/Resolve?x=1",
            "ExternalPlayer/Resolve");

        Assert.AreEqual("https://media.example/emby/", result);
    }

    [TestMethod]
    public void Extension_RejectsPathAndQueryInjection()
    {
        Assert.AreEqual("mkv", ServerUrlBuilder.NormalizeExtension("mkv?api_key=bad", "mkv"));
        Assert.AreEqual("srt", ServerUrlBuilder.NormalizeExtension("../secret", "srt"));
    }

    [TestMethod]
    public void TicketUrl_UsesMediaTitleAndRedactedQueryParameter()
    {
        var result = ServerUrlBuilder.BuildTicketStreamUrl(
            "https://media.example/emby/",
            "short_lived-ticket",
            "中文 Movie & One");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Stream/" +
            "%E4%B8%AD%E6%96%87%20Movie%20%26%20One?api_key=short_lived-ticket",
            result);
    }

    [TestMethod]
    public void HeaderTicketUrl_ContainsOnlyTheMediaTitle()
    {
        var result = ServerUrlBuilder.BuildHeaderTicketStreamUrl(
            "https://media.example/emby/",
            "中文 Movie & One");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Stream/" +
            "%E4%B8%AD%E6%96%87%20Movie%20%26%20One",
            result);
        Assert.IsFalse(result.Contains("?", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("api_key", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ReportingHeaderTicketUrl_BindsTheLaunchIdInThePath()
    {
        var result = ServerUrlBuilder.BuildHeaderTicketStreamUrl(
            "https://media.example/emby/",
            "f74b0d6ee5af4a76a9f24e0942b49267",
            "中文 Movie");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Stream/" +
            "f74b0d6ee5af4a76a9f24e0942b49267/%E4%B8%AD%E6%96%87%20Movie",
            result);
        Assert.ThrowsExactly<ArgumentException>(() =>
            ServerUrlBuilder.BuildHeaderTicketStreamUrl(
                "https://media.example/",
                "../bad",
                "Movie"));
    }

    [TestMethod]
    public void TicketRoutes_UseEmbyLogRedactionAndRealSubtitleExtensions()
    {
        StringAssert.Contains(
            ServerUrlBuilder.BuildTicketStreamUrl("https://media.example/", "ticket", "Movie"),
            "?api_key=ticket");
        Assert.AreEqual(
            "https://media.example/ExternalPlayer/Subtitle/3/subtitle.srt?api_key=ticket",
            ServerUrlBuilder.BuildTicketSubtitleUrl(
                "https://media.example/", "ticket", 3, "srt"));
        Assert.AreEqual(
            "https://media.example/ExternalPlayer/Subtitle/4/subtitle.ass?api_key=ticket",
            ServerUrlBuilder.BuildTicketSubtitleUrl(
                "https://media.example/", "ticket", 4, "ASS"));
    }

    [TestMethod]
    public void HeaderTicketSubtitleUrl_KeepsTheVisibleFileNameFreeOfCredentials()
    {
        var result = ServerUrlBuilder.BuildHeaderTicketSubtitleUrl(
            "https://media.example/emby/", 4, "ASS");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Subtitle/4/subtitle.ass",
            result);
        Assert.IsFalse(result.Contains("?", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("api_key", StringComparison.OrdinalIgnoreCase));

        var namedResult = ServerUrlBuilder.BuildHeaderTicketSubtitleUrl(
            "https://media.example/emby/", 4, "ASS", "Movie.简中.ass");
        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Subtitle/4/Movie.%E7%AE%80%E4%B8%AD.ass",
            namedResult);
    }

    [TestMethod]
    public void RemoteStreamUrl_ContainsTicketButNeverTheSignedOriginUrl()
    {
        var result = ServerUrlBuilder.BuildTicketRemoteStreamUrl(
            "https://media.example/emby/",
            "short_lived-ticket",
            "中文 Movie.mkv");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Remote/" +
            "%E4%B8%AD%E6%96%87%20Movie.mkv?api_key=short_lived-ticket",
            result);
        Assert.IsFalse(result.Contains("Signature", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains("cdn.example", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RemoteReportingUrl_BindsLaunchWithoutExposingTheOriginUrl()
    {
        var result = ServerUrlBuilder.BuildTicketRemoteLaunchStreamUrl(
            "https://media.example/emby/",
            "v1.media.progress.1786974752",
            "f74b0d6ee5af4a76a9f24e0942b49267",
            "中文 Movie.mkv");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Remote/" +
            "f74b0d6ee5af4a76a9f24e0942b49267/" +
            "%E4%B8%AD%E6%96%87%20Movie.mkv?api_key=v1.media.progress.1786974752",
            result);
        Assert.ThrowsExactly<ArgumentException>(() =>
            ServerUrlBuilder.BuildTicketRemoteLaunchStreamUrl(
                "https://media.example/", "token", "../bad", "Movie.mkv"));
        Assert.IsFalse(result.Contains("cdn.example", StringComparison.OrdinalIgnoreCase));
    }
}
