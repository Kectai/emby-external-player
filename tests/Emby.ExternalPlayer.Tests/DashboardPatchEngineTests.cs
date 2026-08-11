using Emby.ExternalPlayer.Web;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class DashboardPatchEngineTests
{
    [TestMethod]
    public void Apply_InsertsUniqueModuleBeforeSupportedAnchor()
    {
        const string source = "before;Promise.all(list.map(loadPlugin));after";

        var result = DashboardPatchEngine.Apply(source);

        Assert.AreEqual(DashboardPatchStatus.Applied, result.Status);
        StringAssert.Contains(result.Content, DashboardPatchEngine.Marker);
        Assert.IsTrue(
            result.Content.IndexOf(DashboardPatchEngine.ModulePath, StringComparison.Ordinal) <
            result.Content.IndexOf(DashboardPatchEngine.PrimaryAnchor, StringComparison.Ordinal));
        StringAssert.Contains(
            result.Content,
            "list.push(\"" + DashboardPatchEngine.ModulePath + "\")," +
            DashboardPatchEngine.Marker + DashboardPatchEngine.PrimaryAnchor);
    }

    [TestMethod]
    [DataRow("4.9.1.80")]
    [DataRow("4.9.3.0")]
    [DataRow("4.9.5.0")]
    public void Apply_IsCompatibleWithVerifiedOfficialEmbyAppJs(string version)
    {
        var testRoot = Environment.GetEnvironmentVariable("EMBY_EXTERNAL_PLAYER_TEST_ROOT")
            ?? throw new InvalidOperationException("Project-local test root is not configured.");
        var projectRoot = Directory.GetParent(Directory.GetParent(testRoot)!.FullName)!.FullName;
        var appJsPath = Path.Combine(
            projectRoot,
            ".local",
            "emby-hosts",
            version,
            "osx-arm64",
            "EmbyServer.app",
            "Contents",
            "Resources",
            "dashboard-ui",
            "app.js");
        Assert.IsTrue(File.Exists(appJsPath), "The verified official Emby fixture is missing.");
        var fixture = File.ReadAllText(appJsPath);
        var precleaned = DashboardPatchEngine.Remove(fixture);
        var source = precleaned.Status == DashboardPatchStatus.Removed
            ? precleaned.Content
            : fixture;

        var applied = DashboardPatchEngine.Apply(source);
        var removed = DashboardPatchEngine.Remove(applied.Content);

        Assert.AreEqual(DashboardPatchStatus.Applied, applied.Status);
        Assert.AreEqual(1, CountOccurrences(applied.Content, DashboardPatchEngine.Marker));
        Assert.AreEqual(DashboardPatchStatus.Removed, removed.Status);
        Assert.AreEqual(source, removed.Content);
    }

    [TestMethod]
    public void Apply_IsIdempotent()
    {
        const string source = "before;Promise.all(list.map(loadPlugin));after";
        var once = DashboardPatchEngine.Apply(source);

        var twice = DashboardPatchEngine.Apply(once.Content);

        Assert.AreEqual(DashboardPatchStatus.AlreadyApplied, twice.Status);
        Assert.AreEqual(once.Content, twice.Content);
        Assert.AreEqual(1, CountOccurrences(twice.Content, DashboardPatchEngine.Marker));
    }

    [TestMethod]
    public void Apply_UnknownAnchorDoesNotModifyContent()
    {
        const string source = "a future Emby app loader";

        var result = DashboardPatchEngine.Apply(source);

        Assert.AreEqual(DashboardPatchStatus.UnsupportedAnchor, result.Status);
        Assert.AreEqual(source, result.Content);
    }

    [TestMethod]
    public void Remove_OnlyRemovesOwnedInjection()
    {
        const string source = "before;Promise.all(list.map(loadPlugin));after";
        var installed = DashboardPatchEngine.Apply(source);

        var removed = DashboardPatchEngine.Remove(installed.Content);

        Assert.AreEqual(DashboardPatchStatus.Removed, removed.Status);
        Assert.AreEqual(source, removed.Content);
    }

    private static int CountOccurrences(string value, string expected)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(expected, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += expected.Length;
        }

        return count;
    }
}
