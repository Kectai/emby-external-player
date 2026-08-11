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
    public void TicketUrl_DoesNotContainEmbyAccessToken()
    {
        var result = ServerUrlBuilder.BuildTicketStreamUrl(
            "https://media.example/emby/",
            "short_lived-ticket",
            "mp4");

        Assert.AreEqual(
            "https://media.example/emby/ExternalPlayer/Stream/short_lived-ticket/stream.mp4",
            result);
        Assert.IsFalse(result.Contains("api_key", StringComparison.OrdinalIgnoreCase));
    }
}
