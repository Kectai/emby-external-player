using Emby.ExternalPlayer.Domain;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class RemoteStreamPolicyTests
{
    [TestMethod]
    public void ResolveMediaExtension_UsesActualRemoteMediaPathInsteadOfStrmContainer()
    {
        var result = RemoteStreamPolicy.ResolveMediaExtension(
            "strm",
            "https://openlist.example/d/Series/Episode.m2ts?sign=secret");

        Assert.AreEqual("m2ts", result);
    }

    [TestMethod]
    public void ResolveMediaExtension_UsesKnownContainerWhenRemotePathHasNoMediaSuffix()
    {
        Assert.AreEqual(
            "mp4",
            RemoteStreamPolicy.ResolveMediaExtension(
                "mp4",
                "https://openlist.example/api/download?id=123"));
        Assert.AreEqual(
            "mkv",
            RemoteStreamPolicy.ResolveMediaExtension(
                "strm",
                "https://openlist.example/api/download?id=123"));
    }

    [TestMethod]
    public void ResolveMediaExtension_DoesNotExposeArbitraryEndpointSuffixes()
    {
        Assert.AreEqual(
            "mkv",
            RemoteStreamPolicy.ResolveMediaExtension(
                "strm",
                "https://openlist.example/download.php?file=movie.mkv"));
    }

    [TestMethod]
    public void CreateFileName_UsesTitleAndNeverOriginQuery()
    {
        var result = RemoteStreamPolicy.CreateFileName("Movie Episode", "mkv");

        Assert.AreEqual("Movie Episode.mkv", result);
        Assert.IsFalse(result.Contains("sign=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void LaunchToken_SeparatesMediaAndProgressTickets()
    {
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(1786974752);
        var mediaTicket = new string('m', 43);
        var progressTicket = new PlaybackReportTicket(
            new string('p', 43),
            "f74b0d6ee5af4a76a9f24e0942b49267",
            expiresAt);

        var token = RemoteStreamPolicy.CreateLaunchToken(mediaTicket, progressTicket);

        Assert.AreEqual(
            "v1." + mediaTicket + "." + progressTicket.Value + ".1786974752",
            token);
        Assert.IsTrue(RemoteStreamPolicy.TryParseLaunchToken(token, out var credentials));
        Assert.AreEqual(mediaTicket, credentials.MediaTicket);
        Assert.AreEqual(progressTicket.Value, credentials.ProgressTicket);
        Assert.AreEqual(expiresAt, credentials.ProgressExpiresAtUtc);
        Assert.IsTrue(credentials.HasPlaybackReporting);
    }

    [TestMethod]
    public void LaunchToken_AcceptsPlainMediaTicketAndRejectsMalformedValues()
    {
        var mediaTicket = new string('m', 43);

        Assert.IsTrue(RemoteStreamPolicy.TryParseLaunchToken(mediaTicket, out var plain));
        Assert.AreEqual(mediaTicket, plain.MediaTicket);
        Assert.IsFalse(plain.HasPlaybackReporting);
        Assert.IsFalse(RemoteStreamPolicy.TryParseLaunchToken(
            "v2." + mediaTicket + "." + new string('p', 43) + ".1786974752",
            out _));
        Assert.IsFalse(RemoteStreamPolicy.TryParseLaunchToken(
            "v1." + mediaTicket + ".bad.1786974752",
            out _));
        Assert.IsFalse(RemoteStreamPolicy.TryParseLaunchToken(
            "v1." + mediaTicket + "." + new string('p', 43) + ".not-a-time",
            out _));
    }
}
