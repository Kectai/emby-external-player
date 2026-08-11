# 客户端 URL Handler

插件只生成 URL，不会安装应用或修改操作系统协议关联。浏览器第一次跳转到自定义协议时通常会弹出确认；若被拦截，弹窗会保留“点此重试”的手动链接。

| 播放器 | 默认平台 | 生成形式 | 续播 | 外挂字幕 |
|---|---|---|---:|---:|
| PotPlayer | Windows | `potplayer://...` | 是 | 是 |
| IINA | macOS | `iina://weblink?url=...&mpv_start=...` | 是 | 否 |
| VLC | 桌面/移动 | 桌面 `vlc://...`；iOS x-callback | 否 | iOS |
| Infuse | macOS/iOS/iPadOS | `infuse://x-callback-url/play?...` | 否 | 是 |
| MPV | 桌面 | `mpv://play/...` | 否 | 否 |
| nPlayer | iOS/Android | `nplayer-http(s)://...` | 否 | 否 |

MPV 与 nPlayer 默认关闭，因为不同第三方 handler 的实现差异较大。

## IINA

建议 IINA 1.4.3 或更高版本。本机只读检查确认 IINA 1.4.4 注册了 `iina` scheme；为了不写入现有应用的播放历史，自动化测试没有实际启动播放器。IINA 当前源码定义了 `open`/`weblink`、`url`、`new_window` 和 `mpv_*` 参数白名单，插件只使用其中的 `mpv_start`，不把字幕路径作为 mpv 参数注入。

来源：[IINA AppDelegate.swift](https://github.com/iina/iina/blob/develop/iina/AppDelegate.swift)。

## VLC

iOS 使用 `vlc-x-callback://x-callback-url/stream?url=...&sub=...`；这与 VLC iOS 官方源码的 URL handler 一致。桌面和 Android 的 `vlc://` 行为会受浏览器、安装包和系统关联影响，发布前应在目标设备点击验证。

来源：[VLC iOS URLHandler.swift](https://github.com/videolan/vlc-ios/blob/master/Sources/Helpers/Network/URLHandler.swift)。

## Infuse

使用官方 x-callback `play` API 的 `url`、`filename`（需要时）和 `sub` 参数。插件当前不声称 Infuse 支持续播 URL 参数。

来源：[Firecore：API for Third-Party Apps & Services](https://support.firecore.com/hc/en-us/articles/215090997-API-for-Third-Party-Apps-Services)。

## PotPlayer

PotPlayer 没有稳定的公开英文 URL handler 规范。插件对 scheme、时间和字幕参数做了独立编码及单元测试，但仍应在目标 Windows 版本验证：纯 URL、中文标题、HTTPS、外挂字幕和续播。若实际版本不接受参数，可先关闭字幕或续播，或切换 VLC。

## 证书与网络

- 播放器必须能访问生成 URL 中的 Emby 公网地址；浏览器能播放不代表播放器网络路径相同。
- HTTPS 证书必须被播放器信任。
- 反向代理应正确传递外部协议、Host 和 base path。
- 票据 URL 不含 Emby token，但仍是有效期内的 Bearer 凭证，不应分享。
