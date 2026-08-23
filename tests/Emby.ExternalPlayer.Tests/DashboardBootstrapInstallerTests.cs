using System.Text;
using Emby.ExternalPlayer.Web;
using MediaBrowser.Model.Logging;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class DashboardBootstrapInstallerTests
{
    [TestMethod]
    public void Install_KeepsAppPluginLoadingAndAddsCacheRepairBootstrap()
    {
        var testRoot = Environment.GetEnvironmentVariable("EMBY_EXTERNAL_PLAYER_TEST_ROOT")
            ?? throw new InvalidOperationException("Project-local test root is not configured.");
        var resourcesPath = Path.Combine(testRoot, "dashboard-bootstrap-" + Guid.NewGuid().ToString("N"));
        var dashboardPath = Path.Combine(resourcesPath, "dashboard-ui");
        Directory.CreateDirectory(dashboardPath);
        var indexPath = Path.Combine(dashboardPath, "index.html");
        var appPath = Path.Combine(dashboardPath, "app.js");
        const string indexSource = "<body>\n    <script src=\"apploader.js\" defer></script>\n</body>";
        const string appSource = "before;Promise.all(list.map(loadPlugin));after";
        var installedAppSource = "before;list.push(\"" + DashboardPatchEngine.ModulePath + "\")," +
            DashboardPatchEngine.Marker + "Promise.all(list.map(loadPlugin));after";
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        try
        {
            File.WriteAllText(indexPath, indexSource, utf8Bom);
            File.WriteAllText(appPath, installedAppSource, new UTF8Encoding(false));
            var installer = new DashboardBootstrapInstaller(resourcesPath, new TestLogger());

            Assert.AreEqual(DashboardPatchStatus.Applied, installer.EnsureInstalled());
            CollectionAssert.AreEqual(
                utf8Bom.GetPreamble(),
                File.ReadAllBytes(indexPath).Take(utf8Bom.GetPreamble().Length).ToArray());
            StringAssert.Contains(File.ReadAllText(indexPath), DashboardIndexPatchEngine.Marker);
            StringAssert.Contains(File.ReadAllText(indexPath), DashboardIndexPatchEngine.BootstrapPath);
            Assert.AreEqual(
                installedAppSource,
                File.ReadAllText(appPath));
            Assert.AreEqual(DashboardPatchStatus.AlreadyApplied, installer.EnsureInstalled());

            Assert.AreEqual(DashboardPatchStatus.Removed, installer.EnsureRemoved());
            Assert.AreEqual(indexSource, File.ReadAllText(indexPath));
            Assert.AreEqual(appSource, File.ReadAllText(appPath));
            CollectionAssert.AreEqual(
                utf8Bom.GetPreamble(),
                File.ReadAllBytes(indexPath).Take(utf8Bom.GetPreamble().Length).ToArray());
        }
        finally
        {
            if (Directory.Exists(resourcesPath))
            {
                Directory.Delete(resourcesPath, recursive: true);
            }
        }
    }

    private sealed class TestLogger : ILogger
    {
        public void Debug(ReadOnlyMemory<char> message) { }

        public void Debug(string message, params object[] paramList) { }

        public void Error(ReadOnlyMemory<char> message) { }

        public void Error(string message, params object[] paramList) { }

        public void ErrorException(string message, Exception exception, params object[] paramList) { }

        public void Fatal(string message, params object[] paramList) { }

        public void FatalException(string message, Exception exception, params object[] paramList) { }

        public void Info(ReadOnlyMemory<char> message) { }

        public void Info(string message, params object[] paramList) { }

        public void Log(LogSeverity severity, ReadOnlyMemory<char> message) { }

        public void Log(LogSeverity severity, string message, params object[] paramList) { }

        public void LogMultiline(string message, LogSeverity severity, StringBuilder additionalContent) { }

        public void Warn(ReadOnlyMemory<char> message) { }

        public void Warn(string message, params object[] paramList) { }
    }
}
