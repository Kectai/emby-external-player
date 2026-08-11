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
    public void DirectStreamUrl_EncodesMediaSourceAndNormalizesExtension()
    {
        var itemId = Guid.Parse("f7e75cae-5055-4706-bfe1-4c4dbf33a573");

        var result = ServerUrlBuilder.BuildDirectStreamUrl(
            "https://media.example/emby/",
            itemId,
            "source id&2",
            "MKV");

        Assert.AreEqual(
            "https://media.example/emby/Videos/f7e75cae50554706bfe14c4dbf33a573/stream.mkv" +
            "?Static=true&MediaSourceId=source%20id%262",
            result);
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
    public void TicketRoutes_UseEmbyLogRedactionOrQuietExtensions()
    {
        StringAssert.Contains(
            ServerUrlBuilder.BuildTicketStreamUrl("https://media.example/", "ticket", "Movie"),
            "?api_key=ticket");
        Assert.IsTrue(ServerUrlBuilder.BuildTicketSubtitleUrl(
            "https://media.example/", "ticket", 3, "srt").EndsWith("/subtitle.css", StringComparison.Ordinal));
    }
}
