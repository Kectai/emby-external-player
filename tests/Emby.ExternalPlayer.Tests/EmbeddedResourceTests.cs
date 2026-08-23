using Emby.ExternalPlayer.Api;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class EmbeddedResourceTests
{
    [TestMethod]
    public void PluginAssembly_ContainsFixedWebAssets()
    {
        var resources = typeof(WebResourceService).Assembly.GetManifestResourceNames();

        Assert.IsTrue(resources.Any(name => name.EndsWith("external-player-language.js", StringComparison.Ordinal)));
        Assert.IsTrue(resources.Any(name => name.EndsWith("external-player.js", StringComparison.Ordinal)));
        Assert.IsTrue(resources.Any(name => name.EndsWith("external-player.css", StringComparison.Ordinal)));
    }
}
