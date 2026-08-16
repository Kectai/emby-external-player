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
            [nameof(EnabledDescription)] = "Master switch for the plugin API and Web integration. Disabling it immediately revokes playback tickets.",
            [nameof(EnableWebButton)] = "Show Web button",
            [nameof(EnableWebButtonDescription)] = "Show the external-player button on supported Emby Web detail pages.",
            [nameof(UseLocalizedButtonText)] = "Use localized button text",
            [nameof(UseLocalizedButtonTextDescription)] = "Use the language selected by the current Emby Web client.",
            [nameof(ButtonText)] = "Custom button text",
            [nameof(ButtonTextDescription)] = "Used only when localized button text is disabled.",
            [nameof(ButtonPlacement)] = "Button placement",
            [nameof(AfterPrimaryPlay)] = "After Play / From Beginning",
            [nameof(EndOfActionRow)] = "End of the action row",
            [nameof(ShowOnlyPlatformPlayers)] = "Show only platform players",
            [nameof(ResumeByDefault)] = "Resume by default",
            [nameof(TicketLifetimeMinutes)] = "Ticket lifetime (minutes)",
            [nameof(TicketLifetimeMinutesDescription)] = "Absolute playback-ticket lifetime. Allowed range: 30 to 720 minutes.",
            [nameof(DefaultPlayerWindows)] = "Default player on Windows",
            [nameof(DefaultPlayerMacOS)] = "Default player on macOS",
            [nameof(DefaultPlayerIOS)] = "Default player on iOS",
            [nameof(DefaultPlayerAndroid)] = "Default player on Android",
            [nameof(CustomPlayers)] = "Custom players",
            [nameof(CustomPlayersDescription)] = "Custom players are managed independently below. Multiple drafts can be added at once, and each player has its own Save and Delete action. Application names are displayed exactly as entered. Templates support {url}, {title}, {subtitle}, {start}, and {headers}.",
            [nameof(CustomPlayerAdd)] = "Add player",
            [nameof(CustomPlayer)] = "Custom player",
            [nameof(CustomPlayerEnabled)] = "Enabled",
            [nameof(ApplicationName)] = "Official application name",
            [nameof(ApplicationNameDescription)] = "Displayed exactly as entered; capitalization is never changed.",
            [nameof(Platform)] = "Available platforms",
            [nameof(AnyPlatform)] = "Any platform",
            [nameof(UrlTemplate)] = "URL scheme template",
            [nameof(UrlTemplateDescription)] = "Example: myplayer://open?url={url}&title={title}. Placeholder values are percent-encoded.",
            [nameof(EnablePlaybackReporting)] = "Enable playback progress reporting",
            [nameof(EnablePlaybackReportingDescription)] = "Requires {headers} in the URL template and a compatible, trusted reporter plugin in the player.",
            [nameof(PlaybackReportingRequiresHeaders)] = "Playback progress reporting requires {headers} in the URL template.",
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
            [nameof(SubtitleMayNotLoadForPlayer)] = "{0} may not automatically load the selected external subtitle from its application link. The media will still open.",
            [nameof(PlaybackPreferences)] = "Playback preferences",
            [nameof(DefaultPlayer)] = "Default player",
            [nameof(DefaultPlayerSaved)] = "Default player saved.",
            [nameof(DefaultPlayerSaveError)] = "Unable to save the default player.",
            [nameof(ResumeFromLastPosition)] = "Resume from the last position",
            [nameof(Cancel)] = "Cancel",
            [nameof(ResolveError)] = "Unable to create the playback address. Check permissions, the media version, and the server connection.",
            [nameof(InvalidLaunchUrl)] = "The server did not return a safe application URL.",
            [nameof(ResumeUnsupportedWarning)] = "The selected player does not support a start position in its URL handler.",
            [nameof(ButtonTextRequired)] = "Custom button text cannot be empty when localized button text is disabled.",
            [nameof(ButtonTextTooLong)] = "Custom button text cannot exceed 40 characters.",
            [nameof(TicketLifetimeInvalid)] = "Ticket lifetime must be between 30 and 720 minutes.",
            [nameof(DefaultPlayerDisabled)] = "The selected default player is disabled.",
            [nameof(DefaultPlayerUnsupported)] = "The selected default player does not support this platform.",
            [nameof(CustomPlayerNameRequired)] = "An enabled custom player requires an application name.",
            [nameof(CustomPlayerNameTooLong)] = "The application name cannot exceed 80 characters.",
            [nameof(CustomPlayerTemplateRequired)] = "An enabled custom player requires a URL scheme template.",
            [nameof(CustomPlayerTemplateInvalid)] = "The URL scheme template must start with a safe custom scheme and may use only {url}, {title}, {subtitle}, {start}, and {headers}.",
        };

    private static readonly Dictionary<string, string> SimplifiedChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(EditorTitle)] = "外部播放器",
            [nameof(EditorDescription)] = "在 Emby Web 中添加轻量的外部播放器选择器；安全中转模式不会把 Emby 访问令牌放入播放器地址。",
            [nameof(Enabled)] = "启用插件",
            [nameof(EnabledDescription)] = "控制插件 API 和 Web 集成的总开关；关闭后会立即撤销所有播放票据。",
            [nameof(EnableWebButton)] = "显示网页按钮",
            [nameof(EnableWebButtonDescription)] = "在支持的 Emby Web 媒体详情页显示外部播放按钮。",
            [nameof(UseLocalizedButtonText)] = "按钮文字跟随界面语言",
            [nameof(UseLocalizedButtonTextDescription)] = "使用当前 Emby Web 客户端设置的语言。",
            [nameof(ButtonText)] = "自定义按钮文字",
            [nameof(ButtonTextDescription)] = "仅在关闭“按钮文字跟随界面语言”时使用。",
            [nameof(ButtonPlacement)] = "按钮位置",
            [nameof(AfterPrimaryPlay)] = "播放/从头开始按钮之后",
            [nameof(EndOfActionRow)] = "操作栏末尾",
            [nameof(ShowOnlyPlatformPlayers)] = "仅显示当前平台播放器",
            [nameof(ResumeByDefault)] = "默认继续播放",
            [nameof(TicketLifetimeMinutes)] = "票据有效期（分钟）",
            [nameof(TicketLifetimeMinutesDescription)] = "播放票据的绝对有效期，允许范围为 30 到 720 分钟。",
            [nameof(DefaultPlayerWindows)] = "Windows 默认播放器",
            [nameof(DefaultPlayerMacOS)] = "macOS 默认播放器",
            [nameof(DefaultPlayerIOS)] = "iOS 默认播放器",
            [nameof(DefaultPlayerAndroid)] = "Android 默认播放器",
            [nameof(CustomPlayers)] = "自定义播放器",
            [nameof(CustomPlayersDescription)] = "自定义播放器在下方独立管理，可一次添加多个草稿，每个播放器都有自己的保存和删除操作。应用名称按输入原样显示；模板支持 {url}、{title}、{subtitle}、{start} 和 {headers}。",
            [nameof(CustomPlayerAdd)] = "添加播放器",
            [nameof(CustomPlayer)] = "自定义播放器",
            [nameof(CustomPlayerEnabled)] = "启用",
            [nameof(ApplicationName)] = "官方应用名称",
            [nameof(ApplicationNameDescription)] = "完全按输入显示，不会自动调整大小写。",
            [nameof(Platform)] = "适用平台",
            [nameof(AnyPlatform)] = "所有平台",
            [nameof(UrlTemplate)] = "URL Scheme 模板",
            [nameof(UrlTemplateDescription)] = "例如：myplayer://open?url={url}&title={title}。占位符值会进行 URL 编码。",
            [nameof(EnablePlaybackReporting)] = "启用播放进度回传",
            [nameof(EnablePlaybackReportingDescription)] = "URL 模板必须包含 {headers}，且播放器中需安装兼容、可信的回传插件。",
            [nameof(PlaybackReportingRequiresHeaders)] = "启用播放进度回传时，URL 模板必须包含 {headers}。",
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
            [nameof(SubtitleMayNotLoadForPlayer)] = "{0} 的应用跳转可能不会自动加载所选外挂字幕，但不会影响媒体打开。",
            [nameof(PlaybackPreferences)] = "播放偏好",
            [nameof(DefaultPlayer)] = "默认播放器",
            [nameof(DefaultPlayerSaved)] = "已保存默认播放器。",
            [nameof(DefaultPlayerSaveError)] = "无法保存默认播放器。",
            [nameof(ResumeFromLastPosition)] = "从上次位置继续",
            [nameof(Cancel)] = "取消",
            [nameof(ResolveError)] = "无法生成播放地址，请检查权限、媒体版本或服务器连接。",
            [nameof(InvalidLaunchUrl)] = "服务器未返回安全的应用启动地址。",
            [nameof(ResumeUnsupportedWarning)] = "所选播放器的 URL 处理器不支持指定起始位置。",
            [nameof(ButtonTextRequired)] = "关闭本地化按钮文字时，自定义按钮文字不能为空。",
            [nameof(ButtonTextTooLong)] = "自定义按钮文字不能超过 40 个字符。",
            [nameof(TicketLifetimeInvalid)] = "票据有效期必须在 30 到 720 分钟之间。",
            [nameof(DefaultPlayerDisabled)] = "所选默认播放器已被禁用。",
            [nameof(DefaultPlayerUnsupported)] = "所选默认播放器不支持此平台。",
            [nameof(CustomPlayerNameRequired)] = "已启用的自定义播放器必须填写应用名称。",
            [nameof(CustomPlayerNameTooLong)] = "应用名称不能超过 80 个字符。",
            [nameof(CustomPlayerTemplateRequired)] = "已启用的自定义播放器必须填写 URL Scheme 模板。",
            [nameof(CustomPlayerTemplateInvalid)] = "URL Scheme 模板必须以安全的自定义协议开头，并且只能使用 {url}、{title}、{subtitle}、{start} 和 {headers}。",
        };

    private static readonly IReadOnlyDictionary<string, string> TraditionalChinese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(EditorTitle)] = "外部播放器",
            [nameof(EditorDescription)] = "在 Emby Web 中加入輕量的外部播放器選擇器；安全中轉模式不會把 Emby 存取權杖放入播放器網址。",
            [nameof(Enabled)] = "啟用外掛",
            [nameof(EnabledDescription)] = "控制外掛 API 和 Web 整合的總開關；關閉後會立即撤銷所有播放票據。",
            [nameof(EnableWebButton)] = "顯示網頁按鈕",
            [nameof(EnableWebButtonDescription)] = "在支援的 Emby Web 媒體詳細資料頁顯示外部播放按鈕。",
            [nameof(UseLocalizedButtonText)] = "按鈕文字跟隨介面語言",
            [nameof(UseLocalizedButtonTextDescription)] = "使用目前 Emby Web 用戶端設定的語言。",
            [nameof(ButtonText)] = "自訂按鈕文字",
            [nameof(ButtonTextDescription)] = "僅在關閉「按鈕文字跟隨介面語言」時使用。",
            [nameof(ButtonPlacement)] = "按鈕位置",
            [nameof(AfterPrimaryPlay)] = "播放/從頭開始按鈕之後",
            [nameof(EndOfActionRow)] = "操作列末尾",
            [nameof(ShowOnlyPlatformPlayers)] = "僅顯示目前平台的播放器",
            [nameof(ResumeByDefault)] = "預設繼續播放",
            [nameof(TicketLifetimeMinutes)] = "票據有效期（分鐘）",
            [nameof(TicketLifetimeMinutesDescription)] = "播放票據的絕對有效期，允許範圍為 30 到 720 分鐘。",
            [nameof(DefaultPlayerWindows)] = "Windows 預設播放器",
            [nameof(DefaultPlayerMacOS)] = "macOS 預設播放器",
            [nameof(DefaultPlayerIOS)] = "iOS 預設播放器",
            [nameof(DefaultPlayerAndroid)] = "Android 預設播放器",
            [nameof(CustomPlayers)] = "自訂播放器",
            [nameof(CustomPlayersDescription)] = "自訂播放器在下方獨立管理，可一次加入多個草稿，每個播放器都有自己的儲存和刪除操作。應用程式名稱按輸入原樣顯示；範本支援 {url}、{title}、{subtitle}、{start} 和 {headers}。",
            [nameof(CustomPlayerAdd)] = "新增播放器",
            [nameof(CustomPlayer)] = "自訂播放器",
            [nameof(CustomPlayerEnabled)] = "啟用",
            [nameof(ApplicationName)] = "官方應用程式名稱",
            [nameof(ApplicationNameDescription)] = "完全按輸入顯示，不會自動調整大小寫。",
            [nameof(Platform)] = "適用平台",
            [nameof(AnyPlatform)] = "所有平台",
            [nameof(UrlTemplate)] = "URL Scheme 範本",
            [nameof(UrlTemplateDescription)] = "例如：myplayer://open?url={url}&title={title}。預留位置值會進行 URL 編碼。",
            [nameof(EnablePlaybackReporting)] = "啟用播放進度回傳",
            [nameof(EnablePlaybackReportingDescription)] = "URL 範本必須包含 {headers}，且播放器中需安裝相容、可信的回傳外掛。",
            [nameof(PlaybackReportingRequiresHeaders)] = "啟用播放進度回傳時，URL 範本必須包含 {headers}。",
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
            [nameof(SubtitleMayNotLoadForPlayer)] = "{0} 的應用程式跳轉可能不會自動載入所選外掛字幕，但不影響媒體開啟。",
            [nameof(PlaybackPreferences)] = "播放偏好",
            [nameof(DefaultPlayer)] = "預設播放器",
            [nameof(DefaultPlayerSaved)] = "已儲存預設播放器。",
            [nameof(DefaultPlayerSaveError)] = "無法儲存預設播放器。",
            [nameof(ResumeFromLastPosition)] = "從上次位置繼續",
            [nameof(Cancel)] = "取消",
            [nameof(ResolveError)] = "無法產生播放網址，請檢查權限、媒體版本或伺服器連線。",
            [nameof(InvalidLaunchUrl)] = "伺服器未傳回安全的應用程式啟動網址。",
            [nameof(ResumeUnsupportedWarning)] = "所選播放器的 URL 處理器不支援指定開始位置。",
            [nameof(ButtonTextRequired)] = "關閉本地化按鈕文字時，自訂按鈕文字不能為空。",
            [nameof(ButtonTextTooLong)] = "自訂按鈕文字不能超過 40 個字元。",
            [nameof(TicketLifetimeInvalid)] = "票據有效期必須在 30 到 720 分鐘之間。",
            [nameof(DefaultPlayerDisabled)] = "所選預設播放器已停用。",
            [nameof(DefaultPlayerUnsupported)] = "所選預設播放器不支援此平台。",
            [nameof(CustomPlayerNameRequired)] = "已啟用的自訂播放器必須填寫應用程式名稱。",
            [nameof(CustomPlayerNameTooLong)] = "應用程式名稱不能超過 80 個字元。",
            [nameof(CustomPlayerTemplateRequired)] = "已啟用的自訂播放器必須填寫 URL Scheme 範本。",
            [nameof(CustomPlayerTemplateInvalid)] = "URL Scheme 範本必須以安全的自訂通訊協定開頭，且只能使用 {url}、{title}、{subtitle}、{start} 和 {headers}。",
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
    public static string TicketLifetimeMinutes => Get(nameof(TicketLifetimeMinutes));
    public static string TicketLifetimeMinutesDescription => Get(nameof(TicketLifetimeMinutesDescription));
    public static string DefaultPlayerWindows => Get(nameof(DefaultPlayerWindows));
    public static string DefaultPlayerMacOS => Get(nameof(DefaultPlayerMacOS));
    public static string DefaultPlayerIOS => Get(nameof(DefaultPlayerIOS));
    public static string DefaultPlayerAndroid => Get(nameof(DefaultPlayerAndroid));
    public static string CustomPlayers => Get(nameof(CustomPlayers));
    public static string CustomPlayersDescription => Get(nameof(CustomPlayersDescription));
    public static string CustomPlayerAdd => Get(nameof(CustomPlayerAdd));
    public static string CustomPlayer => Get(nameof(CustomPlayer));
    public static string CustomPlayerEnabled => Get(nameof(CustomPlayerEnabled));
    public static string ApplicationName => Get(nameof(ApplicationName));
    public static string ApplicationNameDescription => Get(nameof(ApplicationNameDescription));
    public static string Platform => Get(nameof(Platform));
    public static string AnyPlatform => Get(nameof(AnyPlatform));
    public static string UrlTemplate => Get(nameof(UrlTemplate));
    public static string UrlTemplateDescription => Get(nameof(UrlTemplateDescription));
    public static string EnablePlaybackReporting => Get(nameof(EnablePlaybackReporting));
    public static string EnablePlaybackReportingDescription => Get(nameof(EnablePlaybackReportingDescription));
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
    public static string SubtitleMayNotLoadForPlayer => Get(nameof(SubtitleMayNotLoadForPlayer));
    public static string PlaybackPreferences => Get(nameof(PlaybackPreferences));
    public static string DefaultPlayer => Get(nameof(DefaultPlayer));
    public static string DefaultPlayerSaved => Get(nameof(DefaultPlayerSaved));
    public static string DefaultPlayerSaveError => Get(nameof(DefaultPlayerSaveError));
    public static string ResumeFromLastPosition => Get(nameof(ResumeFromLastPosition));
    public static string Cancel => Get(nameof(Cancel));
    public static string ResolveError => Get(nameof(ResolveError));
    public static string InvalidLaunchUrl => Get(nameof(InvalidLaunchUrl));
    public static string ResumeUnsupportedWarning => Get(nameof(ResumeUnsupportedWarning));
    public static string ButtonTextRequired => Get(nameof(ButtonTextRequired));
    public static string ButtonTextTooLong => Get(nameof(ButtonTextTooLong));
    public static string TicketLifetimeInvalid => Get(nameof(TicketLifetimeInvalid));
    public static string DefaultPlayerDisabled => Get(nameof(DefaultPlayerDisabled));
    public static string DefaultPlayerUnsupported => Get(nameof(DefaultPlayerUnsupported));
    public static string CustomPlayerNameRequired => Get(nameof(CustomPlayerNameRequired));
    public static string CustomPlayerNameTooLong => Get(nameof(CustomPlayerNameTooLong));
    public static string CustomPlayerTemplateRequired => Get(nameof(CustomPlayerTemplateRequired));
    public static string CustomPlayerTemplateInvalid => Get(nameof(CustomPlayerTemplateInvalid));
    public static string PlaybackReportingRequiresHeaders => Get(nameof(PlaybackReportingRequiresHeaders));

    public static Dictionary<string, string> GetWebStrings(string? language)
    {
        var catalog = GetCatalog(language);
        var keys = new[]
        {
            nameof(ExternalPlay), nameof(ChoosePlayer), nameof(Open), nameof(BuiltInPlayer),
            nameof(CustomPlayer), nameof(CustomPlayerHint), nameof(NoCustomPlayerHint),
            nameof(MediaVersion), nameof(VersionNumber), nameof(Subtitle),
            nameof(NoExternalSubtitle), nameof(SubtitleNumber), nameof(SubtitleMayNotLoadForPlayer),
            nameof(PlaybackPreferences), nameof(DefaultPlayer), nameof(DefaultPlayerSaved),
            nameof(DefaultPlayerSaveError),
            nameof(ResumeFromLastPosition), nameof(Cancel), nameof(ResolveError), nameof(InvalidLaunchUrl),
            nameof(ResumeUnsupportedWarning),
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
