namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class IsolationTests
{
    [TestMethod]
    public void TestWorkAndSystemTemporaryPathsAreInsideProjectLocalDirectory()
    {
        var testRoot = Environment.GetEnvironmentVariable("EMBY_EXTERNAL_PLAYER_TEST_ROOT");

        Assert.IsFalse(string.IsNullOrWhiteSpace(testRoot));
        var fullTestRoot = Path.GetFullPath(testRoot!);
        var localRoot = Directory.GetParent(fullTestRoot)?.FullName;

        Assert.IsNotNull(localRoot);
        Assert.AreEqual(".local", Path.GetFileName(localRoot));
        Assert.AreEqual("test-work", Path.GetFileName(fullTestRoot));
        StringAssert.StartsWith(
            Path.GetFullPath(Path.GetTempPath()),
            Path.Combine(localRoot, "tmp"));
    }
}
