using System;
using System.Collections.Generic;
using System.Globalization;

namespace Emby.ExternalPlayer.Localization;

/// <summary>
/// Single-assembly localization catalog used by both Emby's generic options UI
/// and the injected Web module. Unsupported locales intentionally fall back to
/// English so adding a new translation never changes the wire contract.
/// </summary>
public static class PluginStrings
{
    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(EditorTitle)] = "External Player",
            [nameof(EditorDescription)] = "Adds a lightweight external-player chooser to Emby Web. The secure relay keeps the Emby access token out of player URLs.",
            [nameof(Enabled)] = "Enabled",
            [nameof(EnabledDescription)] = "Master switch for the plugin API and Web integration.",
            [nameof(EnableWebButton)] = "Show Web button",
            [nameof(EnableWebButtonDescription)] = "Show the external-player button on supported Emby Web detail pages.",
            [nameof(UseLocalizedButtonText)] = "Use localized button text",
            [nameof(UseLocalizedButtonTextDescription)] = "Use the language selected by the current Emby Web client.",
            [nameof(ButtonText)] = "Custom button text",
            [nameof(ButtonTextDescription)] = "Used only when localized button text is disabled.",
            [nameof(ButtonPlacement)] = "Button placement",
            [nameof(AfterPrimaryPlay)] = "After the primary play button",
            [nameof(EndOfActionRow)] = "End of the action row",
            [nameof(ShowOnlyPlatformPlayers)] = "Show only platform players",
            [nameof(ResumeByDefault)] = "Resume by default",
            [nameof(RestartNearEndMinutes)] = "Restart near end (minutes)",
            [nameof(RestartNearEndMinutesDescription)] = "Treat a saved position this close to the end as completed. Allowed range: 0 to 30 minutes.",
            [nameof(StreamMode)] = "Streaming mode",
            [nameof(StreamModeDescription)] = "Secure relay hides Emby tokens and supports local files. Legacy mode exposes the token in the player URL.",
            [nameof(SecureTicketRelay)] = "Secure ticket relay",
            [nameof(LegacyTokenUrl)] = "Legacy token URL",
            [nameof(TicketLifetimeMinutes)] = "Ticket lifetime (minutes)",
            [nameof(TicketLifetimeMinutesDescription)] = "Absolute playback-ticket lifetime. Allowed range: 30 to 720 minutes.",
            [nameof(EnablePotPlayer)] = "Enable PotPlayer",
            [nameof(EnableIINA)] = "Enable IINA",
            [nameof(EnableVLC)] = "Enable VLC media player",
            [nameof(EnableInfuse)] = "Enable Infuse",
            [nameof(EnableMpv)] = "Enable mpv",
            [nameof(EnableNPlayer)] = "Enable nPlayer",
            [nameof(DefaultPlayerWindows)] = "Default player on Windows",
            [nameof(DefaultPlayerMacOS)] = "Default player on macOS",
            [nameof(DefaultPlayerIOS)] = "Default player on iOS",
            [nameof(DefaultPlayerAndroid)] = "Default player on Android",
            [nameof(DebugLogging)] = "Diagnostic logging",
            [nameof(DebugLoggingDescription)] = "Writes diagnostic event names only. Tokens and resolved URLs are never logged.",
            [nameof(CustomPlayers)] = "Custom players",
            [nameof(CustomPlayersDescription)] = "Use the grid to add, edit, or remove URL handlers. Application names are displayed exactly as entered. Templates support {url}, {title}, {subtitle}, and {start}.",
            [nameof(CustomPlayersEmpty)] = "No custom players. Select Add player to create one.",
            [nameof(CustomPlayerAdd)] = "Add player",
            [nameof(CustomPlayerEdit)] = "Edit player",
            [nameof(CustomPlayerDelete)] = "Delete player",
            [nameof(CustomPlayerDeleteConfirm)] = "Delete this custom player?",
            [nameof(CustomPlayerDeleteTitle)] = "Delete custom player",
            [nameof(CustomPlayerSave)] = "Save",
            [nameof(CustomPlayerCancel)] = "Cancel",
            [nameof(CustomPlayer)] = "Custom player",
            [nameof(CustomPlayerEnabled)] = "Enabled",
            [nameof(ApplicationName)] = "Official application name",
            [nameof(ApplicationNameDescription)] = "Displayed exactly as entered; capitalization is never changed.",
            [nameof(Platform)] = "Platform",
            [nameof(AnyPlatform)] = "Any platform",
            [nameof(UrlTemplate)] = "URL scheme template",
            [nameof(UrlTemplateDescription)] = "Example: myplayer://open?url={url}&title={title}. Placeholder values are percent-encoded.",
            [nameof(PotPlayer)] = "PotPlayer",
            [nameof(IINA)] = "IINA",
            [nameof(VLCMediaPlayer)] = "VLC media player",
            [nameof(Infuse)] = "Infuse",
            [nameof(Mpv)] = "mpv",
            [nameof(NPlayer)] = "nPlayer",
            [nameof(Windows)] = "Windows",
            [nameof(MacOS)] = "macOS",
            [nameof(IOS)] = "iOS",
            [nameof(Android)] = "Android",
            [nameof(Linux)] = "Linux",
            [nameof(ExternalPlay)] = "External play",
            [nameof(ChoosePlayer)] = "Choose a player",
            [nameof(Open)] = "Open",
            [nameof(BuiltInPlayer)] = "Built-in",
            [nameof(CustomPlayerHint)] = "Custom applications configured in the plugin are shown here.",
            [nameof(NoCustomPlayerHint)] = "Custom applications can be added in the External Player plugin settings.",
            [nameof(MediaVersion)] = "Media version",
            [nameof(VersionNumber)] = "Version {0}",
            [nameof(Subtitle)] = "Subtitle",
            [nameof(NoExternalSubtitle)] = "Do not load an external subtitle",
            [nameof(SubtitleNumber)] = "Subtitle {0}",
            [nameof(ResumeFromLastPosition)] = "Resume from the last position",
            [nameof(RetryLaunch)] = "If the player did not open, select here to retry",
            [nameof(Cancel)] = "Cancel",
            [nameof(ResolveError)] = "Unable to create the playback address. Check permissions, the media version, and the server connection.",
            [nameof(InvalidLaunchUrl)] = "The server did not return a safe application URL.",
            [nameof(ResumeUnsupportedWarning)] = "The selected player does not support a start position in its URL handler.",
            [nameof(SubtitleUnsupportedWarning)] = "The selected player does not support an external subtitle in its URL handler.",
            [nameof(ButtonTextRequired)] = "Custom button text cannot be empty when localized button text is disabled.",
            [nameof(ButtonTextTooLong)] = "Custom button text cannot exceed 40 characters.",
            [nameof(TicketLifetimeInvalid)] = "Ticket lifetime must be between 30 and 720 minutes.",
            [nameof(RestartNearEndInvalid)] = "Restart-near-end must be between 0 and 30 minutes.",
            [nameof(DefaultPlayerDisabled)] = "The selected default player is disabled.",
            [nameof(DefaultPlayerUnsupported)] = "The selected default player does not support this platform.",
            [nameof(CustomPlayerNameRequired)] = "An enabled custom player requires an application name.",
            [nameof(CustomPlayerNameTooLong)] = "The application name cannot exceed 80 characters.",
            [nameof(CustomPlayerTemplateRequired)] = "An enabled custom player requires a URL scheme template.",
            [nameof(CustomPlayerTemplateInvalid)] = "The URL scheme template must start with a safe custom scheme and may use only {url}, {title}, {subtitle}, and {start}.",
        };

    private static readonly Dictionary<string, string> SimplifiedChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(EditorTitle)] = "外部播放器",
            [nameof(EditorDescription)] = "在 Emby Web 中添加轻量的外部播放器选择器；安全中转模式不会把 Emby 访问令牌放入播放器地址。",
            [nameof(Enabled)] = "启用插件",
            [nameof(EnabledDescription)] = "控制插件 API 和 Web 集成的总开关。",
            [nameof(EnableWebButton)] = "显示网页按钮",
            [nameof(EnableWebButtonDescription)] = "在支持的 Emby Web 媒体详情页显示外部播放按钮。",
            [nameof(UseLocalizedButtonText)] = "按钮文字跟随界面语言",
            [nameof(UseLocalizedButtonTextDescription)] = "使用当前 Emby Web 客户端设置的语言。",
            [nameof(ButtonText)] = "自定义按钮文字",
            [nameof(ButtonTextDescription)] = "仅在关闭“按钮文字跟随界面语言”时使用。",
            [nameof(ButtonPlacement)] = "按钮位置",
            [nameof(AfterPrimaryPlay)] = "主播放按钮之后",
            [nameof(EndOfActionRow)] = "操作栏末尾",
            [nameof(ShowOnlyPlatformPlayers)] = "仅显示当前平台播放器",
            [nameof(ResumeByDefault)] = "默认继续播放",
            [nameof(RestartNearEndMinutes)] = "接近结尾时重新播放（分钟）",
            [nameof(RestartNearEndMinutesDescription)] = "保存位置距离结尾小于该值时视为已完成，允许范围为 0 到 30 分钟。",
            [nameof(StreamMode)] = "串流模式",
            [nameof(StreamModeDescription)] = "安全中转会隐藏 Emby 令牌并支持本地文件；旧版模式会在播放器地址中暴露令牌。",
            [nameof(SecureTicketRelay)] = "安全票据中转",
            [nameof(LegacyTokenUrl)] = "旧版令牌地址",
            [nameof(TicketLifetimeMinutes)] = "票据有效期（分钟）",
            [nameof(TicketLifetimeMinutesDescription)] = "播放票据的绝对有效期，允许范围为 30 到 720 分钟。",
            [nameof(EnablePotPlayer)] = "启用 PotPlayer",
            [nameof(EnableIINA)] = "启用 IINA",
            [nameof(EnableVLC)] = "启用 VLC media player",
            [nameof(EnableInfuse)] = "启用 Infuse",
            [nameof(EnableMpv)] = "启用 mpv",
            [nameof(EnableNPlayer)] = "启用 nPlayer",
            [nameof(DefaultPlayerWindows)] = "Windows 默认播放器",
            [nameof(DefaultPlayerMacOS)] = "macOS 默认播放器",
            [nameof(DefaultPlayerIOS)] = "iOS 默认播放器",
            [nameof(DefaultPlayerAndroid)] = "Android 默认播放器",
            [nameof(DebugLogging)] = "诊断日志",
            [nameof(DebugLoggingDescription)] = "仅记录诊断事件名称，不记录令牌或解析后的地址。",
            [nameof(CustomPlayers)] = "自定义播放器",
            [nameof(CustomPlayersDescription)] = "可在表格中新增、编辑或删除 URL 处理器。应用名称会完全按输入显示；模板支持 {url}、{title}、{subtitle} 和 {start}。",
            [nameof(CustomPlayersEmpty)] = "尚未配置自定义播放器，请选择“添加播放器”。",
            [nameof(CustomPlayerAdd)] = "添加播放器",
            [nameof(CustomPlayerEdit)] = "编辑播放器",
            [nameof(CustomPlayerDelete)] = "删除播放器",
            [nameof(CustomPlayerDeleteConfirm)] = "确定删除这个自定义播放器吗？",
            [nameof(CustomPlayerDeleteTitle)] = "删除自定义播放器",
            [nameof(CustomPlayerSave)] = "保存",
            [nameof(CustomPlayerCancel)] = "取消",
            [nameof(CustomPlayer)] = "自定义播放器",
            [nameof(CustomPlayerEnabled)] = "启用",
            [nameof(ApplicationName)] = "官方应用名称",
            [nameof(ApplicationNameDescription)] = "完全按输入显示，不会自动调整大小写。",
            [nameof(Platform)] = "平台",
            [nameof(AnyPlatform)] = "所有平台",
            [nameof(UrlTemplate)] = "URL Scheme 模板",
            [nameof(UrlTemplateDescription)] = "例如：myplayer://open?url={url}&title={title}。占位符值会进行 URL 编码。",
            [nameof(Windows)] = "Windows",
            [nameof(MacOS)] = "macOS",
            [nameof(IOS)] = "iOS",
            [nameof(Android)] = "Android",
            [nameof(Linux)] = "Linux",
            [nameof(ExternalPlay)] = "外部播放",
            [nameof(ChoosePlayer)] = "选择播放器",
            [nameof(Open)] = "打开",
            [nameof(BuiltInPlayer)] = "内置",
            [nameof(CustomPlayerHint)] = "在插件中配置的自定义应用会显示在此处。",
            [nameof(NoCustomPlayerHint)] = "可在“外部播放器”插件设置中添加自定义应用。",
            [nameof(MediaVersion)] = "媒体版本",
            [nameof(VersionNumber)] = "版本 {0}",
            [nameof(Subtitle)] = "字幕",
            [nameof(NoExternalSubtitle)] = "不加载外挂字幕",
            [nameof(SubtitleNumber)] = "字幕 {0}",
            [nameof(ResumeFromLastPosition)] = "从上次位置继续",
            [nameof(RetryLaunch)] = "若播放器未自动打开，请点此重试",
            [nameof(Cancel)] = "取消",
            [nameof(ResolveError)] = "无法生成播放地址，请检查权限、媒体版本或服务器连接。",
            [nameof(InvalidLaunchUrl)] = "服务器未返回安全的应用启动地址。",
            [nameof(ResumeUnsupportedWarning)] = "所选播放器的 URL 处理器不支持指定起始位置。",
            [nameof(SubtitleUnsupportedWarning)] = "所选播放器的 URL 处理器不支持外挂字幕。",
            [nameof(ButtonTextRequired)] = "关闭本地化按钮文字时，自定义按钮文字不能为空。",
            [nameof(ButtonTextTooLong)] = "自定义按钮文字不能超过 40 个字符。",
            [nameof(TicketLifetimeInvalid)] = "票据有效期必须在 30 到 720 分钟之间。",
            [nameof(RestartNearEndInvalid)] = "接近结尾重新播放的时间必须在 0 到 30 分钟之间。",
            [nameof(DefaultPlayerDisabled)] = "所选默认播放器已被禁用。",
            [nameof(DefaultPlayerUnsupported)] = "所选默认播放器不支持此平台。",
            [nameof(CustomPlayerNameRequired)] = "已启用的自定义播放器必须填写应用名称。",
            [nameof(CustomPlayerNameTooLong)] = "应用名称不能超过 80 个字符。",
            [nameof(CustomPlayerTemplateRequired)] = "已启用的自定义播放器必须填写 URL Scheme 模板。",
            [nameof(CustomPlayerTemplateInvalid)] = "URL Scheme 模板必须以安全的自定义协议开头，并且只能使用 {url}、{title}、{subtitle} 和 {start}。",
        };

    private static readonly IReadOnlyDictionary<string, string> TraditionalChinese =
        new Dictionary<string, string>(SimplifiedChinese, StringComparer.Ordinal)
        {
            [nameof(EditorTitle)] = "外部播放器",
            [nameof(EditorDescription)] = "在 Emby Web 中加入輕量的外部播放器選擇器；安全中轉模式不會把 Emby 存取權杖放入播放器網址。",
            [nameof(Enabled)] = "啟用外掛",
            [nameof(EnableWebButton)] = "顯示網頁按鈕",
            [nameof(UseLocalizedButtonText)] = "按鈕文字跟隨介面語言",
            [nameof(ButtonText)] = "自訂按鈕文字",
            [nameof(CustomPlayers)] = "自訂播放器",
            [nameof(CustomPlayersDescription)] = "可在表格中新增、編輯或刪除 URL 處理器。應用程式名稱會完全按輸入顯示；範本支援 {url}、{title}、{subtitle} 和 {start}。",
            [nameof(CustomPlayersEmpty)] = "尚未設定自訂播放器，請選擇「新增播放器」。",
            [nameof(CustomPlayerAdd)] = "新增播放器",
            [nameof(CustomPlayerEdit)] = "編輯播放器",
            [nameof(CustomPlayerDelete)] = "刪除播放器",
            [nameof(CustomPlayerDeleteConfirm)] = "確定刪除這個自訂播放器嗎？",
            [nameof(CustomPlayerDeleteTitle)] = "刪除自訂播放器",
            [nameof(CustomPlayerSave)] = "儲存",
            [nameof(CustomPlayerCancel)] = "取消",
            [nameof(CustomPlayer)] = "自訂播放器",
            [nameof(CustomPlayerEnabled)] = "啟用",
            [nameof(ApplicationName)] = "官方應用程式名稱",
            [nameof(ApplicationNameDescription)] = "完全按輸入顯示，不會自動調整大小寫。",
            [nameof(AnyPlatform)] = "所有平台",
            [nameof(ExternalPlay)] = "外部播放",
            [nameof(ChoosePlayer)] = "選擇播放器",
            [nameof(Open)] = "開啟",
            [nameof(BuiltInPlayer)] = "內建",
            [nameof(CustomPlayerHint)] = "在外掛中設定的自訂應用程式會顯示在此處。",
            [nameof(NoCustomPlayerHint)] = "可在「外部播放器」外掛設定中加入自訂應用程式。",
            [nameof(MediaVersion)] = "媒體版本",
            [nameof(VersionNumber)] = "版本 {0}",
            [nameof(Subtitle)] = "字幕",
            [nameof(NoExternalSubtitle)] = "不載入外掛字幕",
            [nameof(SubtitleNumber)] = "字幕 {0}",
            [nameof(ResumeFromLastPosition)] = "從上次位置繼續",
            [nameof(RetryLaunch)] = "若播放器未自動開啟，請點此重試",
            [nameof(Cancel)] = "取消",
            [nameof(ResolveError)] = "無法產生播放網址，請檢查權限、媒體版本或伺服器連線。",
            [nameof(InvalidLaunchUrl)] = "伺服器未傳回安全的應用程式啟動網址。",
        };

    public static string EditorTitle => Get(nameof(EditorTitle));
    public static string EditorDescription => Get(nameof(EditorDescription));
    public static string Enabled => Get(nameof(Enabled));
    public static string EnabledDescription => Get(nameof(EnabledDescription));
    public static string EnableWebButton => Get(nameof(EnableWebButton));
    public static string EnableWebButtonDescription => Get(nameof(EnableWebButtonDescription));
    public static string UseLocalizedButtonText => Get(nameof(UseLocalizedButtonText));
    public static string UseLocalizedButtonTextDescription => Get(nameof(UseLocalizedButtonTextDescription));
    public static string ButtonText => Get(nameof(ButtonText));
    public static string ButtonTextDescription => Get(nameof(ButtonTextDescription));
    public static string ButtonPlacement => Get(nameof(ButtonPlacement));
    public static string AfterPrimaryPlay => Get(nameof(AfterPrimaryPlay));
    public static string EndOfActionRow => Get(nameof(EndOfActionRow));
    public static string ShowOnlyPlatformPlayers => Get(nameof(ShowOnlyPlatformPlayers));
    public static string ResumeByDefault => Get(nameof(ResumeByDefault));
    public static string RestartNearEndMinutes => Get(nameof(RestartNearEndMinutes));
    public static string RestartNearEndMinutesDescription => Get(nameof(RestartNearEndMinutesDescription));
    public static string StreamMode => Get(nameof(StreamMode));
    public static string StreamModeDescription => Get(nameof(StreamModeDescription));
    public static string SecureTicketRelay => Get(nameof(SecureTicketRelay));
    public static string LegacyTokenUrl => Get(nameof(LegacyTokenUrl));
    public static string TicketLifetimeMinutes => Get(nameof(TicketLifetimeMinutes));
    public static string TicketLifetimeMinutesDescription => Get(nameof(TicketLifetimeMinutesDescription));
    public static string EnablePotPlayer => Get(nameof(EnablePotPlayer));
    public static string EnableIINA => Get(nameof(EnableIINA));
    public static string EnableVLC => Get(nameof(EnableVLC));
    public static string EnableInfuse => Get(nameof(EnableInfuse));
    public static string EnableMpv => Get(nameof(EnableMpv));
    public static string EnableNPlayer => Get(nameof(EnableNPlayer));
    public static string DefaultPlayerWindows => Get(nameof(DefaultPlayerWindows));
    public static string DefaultPlayerMacOS => Get(nameof(DefaultPlayerMacOS));
    public static string DefaultPlayerIOS => Get(nameof(DefaultPlayerIOS));
    public static string DefaultPlayerAndroid => Get(nameof(DefaultPlayerAndroid));
    public static string DebugLogging => Get(nameof(DebugLogging));
    public static string DebugLoggingDescription => Get(nameof(DebugLoggingDescription));
    public static string CustomPlayers => Get(nameof(CustomPlayers));
    public static string CustomPlayersDescription => Get(nameof(CustomPlayersDescription));
    public static string CustomPlayersEmpty => Get(nameof(CustomPlayersEmpty));
    public static string CustomPlayerAdd => Get(nameof(CustomPlayerAdd));
    public static string CustomPlayerEdit => Get(nameof(CustomPlayerEdit));
    public static string CustomPlayerDelete => Get(nameof(CustomPlayerDelete));
    public static string CustomPlayerDeleteConfirm => Get(nameof(CustomPlayerDeleteConfirm));
    public static string CustomPlayerDeleteTitle => Get(nameof(CustomPlayerDeleteTitle));
    public static string CustomPlayerSave => Get(nameof(CustomPlayerSave));
    public static string CustomPlayerCancel => Get(nameof(CustomPlayerCancel));
    public static string CustomPlayer => Get(nameof(CustomPlayer));
    public static string CustomPlayerEnabled => Get(nameof(CustomPlayerEnabled));
    public static string ApplicationName => Get(nameof(ApplicationName));
    public static string ApplicationNameDescription => Get(nameof(ApplicationNameDescription));
    public static string Platform => Get(nameof(Platform));
    public static string AnyPlatform => Get(nameof(AnyPlatform));
    public static string UrlTemplate => Get(nameof(UrlTemplate));
    public static string UrlTemplateDescription => Get(nameof(UrlTemplateDescription));
    public static string PotPlayer => Get(nameof(PotPlayer));
    public static string IINA => Get(nameof(IINA));
    public static string VLCMediaPlayer => Get(nameof(VLCMediaPlayer));
    public static string Infuse => Get(nameof(Infuse));
    public static string Mpv => Get(nameof(Mpv));
    public static string NPlayer => Get(nameof(NPlayer));
    public static string Windows => Get(nameof(Windows));
    public static string MacOS => Get(nameof(MacOS));
    public static string IOS => Get(nameof(IOS));
    public static string Android => Get(nameof(Android));
    public static string Linux => Get(nameof(Linux));
    public static string ExternalPlay => Get(nameof(ExternalPlay));
    public static string ChoosePlayer => Get(nameof(ChoosePlayer));
    public static string Open => Get(nameof(Open));
    public static string BuiltInPlayer => Get(nameof(BuiltInPlayer));
    public static string CustomPlayerHint => Get(nameof(CustomPlayerHint));
    public static string NoCustomPlayerHint => Get(nameof(NoCustomPlayerHint));
    public static string MediaVersion => Get(nameof(MediaVersion));
    public static string VersionNumber => Get(nameof(VersionNumber));
    public static string Subtitle => Get(nameof(Subtitle));
    public static string NoExternalSubtitle => Get(nameof(NoExternalSubtitle));
    public static string SubtitleNumber => Get(nameof(SubtitleNumber));
    public static string ResumeFromLastPosition => Get(nameof(ResumeFromLastPosition));
    public static string RetryLaunch => Get(nameof(RetryLaunch));
    public static string Cancel => Get(nameof(Cancel));
    public static string ResolveError => Get(nameof(ResolveError));
    public static string InvalidLaunchUrl => Get(nameof(InvalidLaunchUrl));
    public static string ResumeUnsupportedWarning => Get(nameof(ResumeUnsupportedWarning));
    public static string SubtitleUnsupportedWarning => Get(nameof(SubtitleUnsupportedWarning));
    public static string ButtonTextRequired => Get(nameof(ButtonTextRequired));
    public static string ButtonTextTooLong => Get(nameof(ButtonTextTooLong));
    public static string TicketLifetimeInvalid => Get(nameof(TicketLifetimeInvalid));
    public static string RestartNearEndInvalid => Get(nameof(RestartNearEndInvalid));
    public static string DefaultPlayerDisabled => Get(nameof(DefaultPlayerDisabled));
    public static string DefaultPlayerUnsupported => Get(nameof(DefaultPlayerUnsupported));
    public static string CustomPlayerNameRequired => Get(nameof(CustomPlayerNameRequired));
    public static string CustomPlayerNameTooLong => Get(nameof(CustomPlayerNameTooLong));
    public static string CustomPlayerTemplateRequired => Get(nameof(CustomPlayerTemplateRequired));
    public static string CustomPlayerTemplateInvalid => Get(nameof(CustomPlayerTemplateInvalid));

    public static Dictionary<string, string> GetWebStrings(string? language)
    {
        var catalog = GetCatalog(language);
        var keys = new[]
        {
            nameof(ExternalPlay), nameof(ChoosePlayer), nameof(Open), nameof(BuiltInPlayer),
            nameof(CustomPlayer), nameof(CustomPlayerHint), nameof(NoCustomPlayerHint),
            nameof(MediaVersion), nameof(VersionNumber), nameof(Subtitle),
            nameof(NoExternalSubtitle), nameof(SubtitleNumber), nameof(ResumeFromLastPosition),
            nameof(RetryLaunch), nameof(Cancel), nameof(ResolveError), nameof(InvalidLaunchUrl),
            nameof(ResumeUnsupportedWarning), nameof(SubtitleUnsupportedWarning),
        };
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            result[key] = catalog.TryGetValue(key, out var value) ? value : English[key];
        }
        return result;
    }

    public static string Get(string key, string? language = null)
    {
        var catalog = GetCatalog(language ?? CultureInfo.CurrentUICulture.Name);
        return catalog.TryGetValue(key, out var value)
            ? value
            : English.TryGetValue(key, out var fallback) ? fallback : key;
    }

    private static IReadOnlyDictionary<string, string> GetCatalog(string? language)
    {
        var normalized = (language ?? string.Empty).Replace('_', '-');
        if (normalized.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
        {
            return TraditionalChinese;
        }
        if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return SimplifiedChinese;
        }
        return English;
    }
}
