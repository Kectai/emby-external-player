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
        Assert.AreEqual("选择播放器", PluginStrings.GetWebStrings("zh-CN")["ChoosePlayer"]);
        Assert.AreEqual("自定义播放器", PluginStrings.GetWebStrings("zh-CN")["CustomPlayer"]);
        Assert.AreEqual(
            "{0} 的应用跳转可能不会自动加载所选外挂字幕，但不会影响媒体打开。",
            PluginStrings.GetWebStrings("zh-CN")["SubtitleMayNotLoadForPlayer"]);
        Assert.AreEqual(
            "默认播放器",
            PluginStrings.GetWebStrings("zh-CN")["DefaultPlayer"]);
        Assert.AreEqual("播放偏好", PluginStrings.GetWebStrings("zh-CN")["PlaybackPreferences"]);
        Assert.AreEqual(
            "Unable to save the default player.",
            PluginStrings.GetWebStrings("fr-FR")["DefaultPlayerSaveError"]);
        Assert.IsFalse(PluginStrings.GetWebStrings("zh-CN").ContainsKey("RetryLaunch"));
        Assert.AreEqual("外部播放", PluginStrings.GetWebStrings("zh-Hant")["ExternalPlay"]);
        Assert.AreEqual("開啟", PluginStrings.GetWebStrings("zh-Hant")["Open"]);
        Assert.AreEqual("External play", PluginStrings.GetWebStrings("fr-FR")["ExternalPlay"]);
        Assert.AreEqual("Built-in", PluginStrings.GetWebStrings("fr-FR")["BuiltInPlayer"]);
        Assert.AreEqual("添加播放器", PluginStrings.Get(nameof(PluginStrings.CustomPlayerAdd), "zh-CN"));
    }

    [TestMethod]
    public void CustomTemplate_RejectsWebAndScriptSchemes()
    {
        Assert.IsFalse(CustomPlayerTemplate.IsValid("javascript:{url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("intent://open?url={url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("mailto:open?url={url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("ms-settings:open?url={url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("https://example.test/?url={url}"));
        Assert.IsFalse(CustomPlayerTemplate.IsValid("player://open?url={unknown}"));
        Assert.IsTrue(CustomPlayerTemplate.IsValid("player-pro://open?url={url}&title={title}"));
        Assert.IsTrue(CustomPlayerTemplate.IsValid("iina-nova://weblink?url={url}&mpv_http-header-fields={headers}"));
        Assert.IsFalse(CustomPlayerTemplate.SupportsHttpRequestHeaders("generic://weblink?url={url}&mpv_start={start}"));
    }
}
