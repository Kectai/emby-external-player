using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class SecurityPolicyTests
{
    [TestMethod]
    public void LocalMediaFile_RejectsRelativeAndMissingPaths()
    {
        var testRoot = Environment.GetEnvironmentVariable("EMBY_EXTERNAL_PLAYER_TEST_ROOT")
            ?? throw new InvalidOperationException("The isolated test root is not configured.");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            LocalMediaFilePolicy.RequireExistingFile("relative/video.mkv"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            LocalMediaFilePolicy.RequireExistingFile(Path.Combine(
                testRoot,
                "definitely-does-not-exist.mkv")));
    }

    [TestMethod]
    public void SafeFileName_RemovesHeaderAndPathInjectionCharacters()
    {
        Assert.AreEqual(
            "evil__X-Injected_ yes_movie.mkv",
            SafeFileNamePolicy.Create("evil\r\nX-Injected: yes_movie.mkv", "mkv"));
        Assert.AreEqual("movie.mkv", SafeFileNamePolicy.Create("../movie.mkv", "mkv"));
        Assert.AreEqual(
            "inline; filename=\"movie.mkv\"",
            SafeFileNamePolicy.CreateContentDisposition("movie.mkv"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SafeFileNamePolicy.CreateContentDisposition("movie.mkv\r\nX-Evil: yes"));
    }

    [TestMethod]
    public void UrlTitle_PreservesUnicodeButRemovesPathAndQuerySyntax()
    {
        Assert.AreEqual(
            "电影_第一集_片名_测试",
            SafeFileNamePolicy.CreateUrlTitle("电影/第一集?片名#测试"));
        Assert.AreEqual("media", SafeFileNamePolicy.CreateUrlTitle(" ../.. "));
        Assert.AreEqual("🎬 电影", SafeFileNamePolicy.CreateUrlTitle("🎬 电影"));
    }
}
