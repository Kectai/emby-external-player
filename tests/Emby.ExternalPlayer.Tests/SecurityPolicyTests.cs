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
        Assert.AreEqual("media.mkv", SafeFileNamePolicy.CreateGeneric("mkv"));
        Assert.AreEqual(
            "inline; filename=\"movie.mkv\"",
            SafeFileNamePolicy.CreateContentDisposition("movie.mkv"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SafeFileNamePolicy.CreateContentDisposition("movie.mkv\r\nX-Evil: yes"));
    }

    [TestMethod]
    public void SubtitleFileName_PreservesOnlyTheRealBaseNameAndSafeExtension()
    {
        Assert.AreEqual(
            "Movie.简中.default.ass",
            SafeFileNamePolicy.CreateFileName(
                Path.Combine("private", "library", "Movie.简中.default.ass"),
                "ASS",
                "subtitle"));
        Assert.AreEqual(
            "subtitle.srt",
            SafeFileNamePolicy.CreateFileName(null, "srt", "subtitle"));
        Assert.AreEqual(
            "inline; filename=\"Movie.__.ass\"; filename*=UTF-8''Movie.%E7%AE%80%E4%B8%AD.ass",
            SafeFileNamePolicy.CreateContentDisposition("Movie.简中.ass"));
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
