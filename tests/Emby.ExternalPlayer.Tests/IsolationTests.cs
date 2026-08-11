namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class IsolationTests
{
    [TestMethod]
    public void TestWorkAndSystemTemporaryPathsAreInsideProjectLocalDirectory()
    {
        var testRoot = Environment.GetEnvironmentVariable("EMBY_EXTERNAL_PLAYER_TEST_ROOT");

        Assert.IsFalse(string.IsNullOrWhiteSpace(testRoot));
        StringAssert.Contains(Path.GetFullPath(testRoot!), Path.Combine("emby-external-player-plugin-design", ".local"));
        StringAssert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(testRoot!).Replace("test-work", "tmp"));
    }
}
