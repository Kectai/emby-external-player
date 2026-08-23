using Emby.ExternalPlayer.Web;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class DashboardIndexPatchEngineTests
{
    [TestMethod]
    public void Apply_InsertsIndependentBootstrapBeforeAppLoader()
    {
        const string source = "<body>\n    <script src=\"apploader.js\" defer></script>\n</body>";

        var result = DashboardIndexPatchEngine.Apply(source);

        Assert.AreEqual(DashboardPatchStatus.Applied, result.Status);
        StringAssert.Contains(result.Content, DashboardIndexPatchEngine.Marker);
        Assert.IsTrue(
            result.Content.IndexOf(DashboardIndexPatchEngine.BootstrapPath, StringComparison.Ordinal) <
            result.Content.IndexOf(DashboardIndexPatchEngine.PrimaryAnchor, StringComparison.Ordinal));
        StringAssert.Contains(
            result.Content,
            "<script src=\"" + DashboardIndexPatchEngine.BootstrapPath + "\" defer></script>\n    " +
            DashboardIndexPatchEngine.Marker + "\n    " + DashboardIndexPatchEngine.PrimaryAnchor);
    }

    [TestMethod]
    [DataRow("4.9.1.80")]
    [DataRow("4.9.3.0")]
    [DataRow("4.9.5.0")]
    public void Apply_IsCompatibleWithVerifiedOfficialEmbyIndexHtml(string version)
    {
        var testRoot = Environment.GetEnvironmentVariable("EMBY_EXTERNAL_PLAYER_TEST_ROOT")
            ?? throw new InvalidOperationException("Project-local test root is not configured.");
        var projectRoot = Directory.GetParent(Directory.GetParent(testRoot)!.FullName)!.FullName;
        var indexHtmlPath = Path.Combine(
            projectRoot,
            ".local",
            "emby-hosts",
            version,
            "osx-arm64",
            "EmbyServer.app",
            "Contents",
            "Resources",
            "dashboard-ui",
            "index.html");
        if (!File.Exists(indexHtmlPath))
        {
            Assert.Inconclusive(
                "The optional full Emby index.html fixture is not present in the project-local test directory.");
        }
        var fixture = File.ReadAllText(indexHtmlPath);
        var precleaned = DashboardIndexPatchEngine.Remove(fixture);
        var source = precleaned.Status == DashboardPatchStatus.Removed
            ? precleaned.Content
            : fixture;

        var applied = DashboardIndexPatchEngine.Apply(source);
        var removed = DashboardIndexPatchEngine.Remove(applied.Content);

        Assert.AreEqual(DashboardPatchStatus.Applied, applied.Status);
        Assert.AreEqual(1, CountOccurrences(applied.Content, DashboardIndexPatchEngine.Marker));
        Assert.AreEqual(DashboardPatchStatus.Removed, removed.Status);
        Assert.AreEqual(source, removed.Content);
    }

    [TestMethod]
    public void Apply_PreservesCrLfStyle()
    {
        const string source = "<body>\r\n    <script src=\"apploader.js\" defer></script>\r\n</body>";

        var result = DashboardIndexPatchEngine.Apply(source);

        StringAssert.Contains(
            result.Content,
            "bootstrap.js?v=3\" defer></script>\r\n    " + DashboardIndexPatchEngine.Marker + "\r\n    ");
        Assert.IsFalse(result.Content.Replace("\r\n", string.Empty).Contains('\n'));
    }

    [TestMethod]
    public void Apply_IsIdempotent()
    {
        const string source = "<script src=\"apploader.js\" defer></script>";
        var once = DashboardIndexPatchEngine.Apply(source);

        var twice = DashboardIndexPatchEngine.Apply(once.Content);

        Assert.AreEqual(DashboardPatchStatus.AlreadyApplied, twice.Status);
        Assert.AreEqual(once.Content, twice.Content);
        Assert.AreEqual(1, CountOccurrences(twice.Content, DashboardIndexPatchEngine.Marker));
    }

    [TestMethod]
    public void Apply_UnknownAnchorDoesNotModifyContent()
    {
        const string source = "a future Emby app loader";

        var result = DashboardIndexPatchEngine.Apply(source);

        Assert.AreEqual(DashboardPatchStatus.UnsupportedAnchor, result.Status);
        Assert.AreEqual(source, result.Content);
    }

    [TestMethod]
    public void Remove_OnlyRemovesOwnedInjection()
    {
        const string source = "<script src=\"apploader.js\" defer></script>";
        var installed = DashboardIndexPatchEngine.Apply(source);

        var removed = DashboardIndexPatchEngine.Remove(installed.Content);

        Assert.AreEqual(DashboardPatchStatus.Removed, removed.Status);
        Assert.AreEqual(source, removed.Content);
    }

    [TestMethod]
    public void Apply_MigratesTheUnreleasedIndexBootstrap()
    {
        const string oldMarker = "<!-- Emby.ExternalPlayer bootstrap: 6f784f38 -->";
        const string source = "<script src=\"modules/embyexternalplayer/bootstrap.js?v=2\" defer></script>\n    " +
            oldMarker + "\n    <script src=\"apploader.js\" defer></script>";

        var result = DashboardIndexPatchEngine.Apply(source);

        Assert.AreEqual(DashboardPatchStatus.Applied, result.Status);
        StringAssert.Contains(result.Content, DashboardIndexPatchEngine.Marker);
        Assert.IsFalse(result.Content.Contains(oldMarker, StringComparison.Ordinal));
        Assert.IsFalse(result.Content.Contains("bootstrap.js?v=2", StringComparison.Ordinal));
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
