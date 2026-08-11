using Emby.ExternalPlayer.Localization;
using Emby.ExternalPlayer.Services;

namespace Emby.ExternalPlayer.Tests;

[TestClass]
public sealed class LocalizationTests
{
    [TestMethod]
    public void WebStrings_FollowConfiguredLanguageAndFallBackToEnglish()
    {
        Assert.AreEqual("外部播放", PluginStrings.GetWebStrings("zh-CN")["ExternalPlay"]);
        Assert.AreEqual("外部播放", PluginStrings.GetWebStrings("zh-Hant")["ExternalPlay"]);
        Assert.AreEqual("External play", PluginStrings.GetWebStrings("fr-FR")["ExternalPlay"]);
    }

    [TestMethod]
    public void CustomTemplate_RejectsWebAndScriptSchemes()
    {
        Assert.IsFalse(CustomPlayerTemplate.IsValid("javascript:{url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("https://example.test/?url={url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("player://open?url={unknown}"));
        Assert.IsTrue(CustomPlayerTemplate.IsValid("player-pro://open?url={url}&title={title}"));
    }
}
