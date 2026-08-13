# 播放器与 URL 模板

插件只生成 URL Scheme，不安装应用，也不修改操作系统协议关联。除 IINA/macOS 外，下表表示当前实现和首次安装默认平台，不表示已经完成目标系统验证。

| 播放器 | 初始适用平台 | 续播 | 外挂字幕 |
|---|---|---:|---:|
| PotPlayer | Windows | 是 | 是 |
| IINA | macOS | 是 | 未确认自动挂载 |
| VLC media player | Windows、macOS、iOS、Android、Linux | 否 | iOS 支持；其他平台未确认 |
| Infuse | macOS、iOS | 否 | 是 |
| mpv | Windows、macOS、Linux | 取决于本机 Handler | 取决于本机 Handler |
| nPlayer | iOS、Android | 否 | 未确认 |

管理员可以修改内置播放器的适用平台。mpv 和 nPlayer 默认关闭，因为第三方 Handler 的实现并不统一。

## IINA

IINA 使用：

```text
iina://weblink?url=...&new_window=1&mpv_start=...&mpv_http-header-fields=...
```

媒体地址不携带查询票据，短期票据通过 IINA 允许的 `mpv_http-header-fields` 传递，因此标题不会包含 `api_key`。IINA 的 URL Scheme 安全白名单包含 `start` 与 `http-header-fields`，但不包含自动外挂字幕所需的 `sub-file`；选择字幕时插件会保留选择并给出非阻断说明，不会为内置 IINA 生成无法交付的字幕地址。

实现依据：[IINA AppDelegate.swift](https://github.com/iina/iina/blob/develop/iina/AppDelegate.swift)。

## 其他内置播放器

- PotPlayer 使用媒体 URL 加 `/current`、`/seek=<秒>` 和 `/sub=<URL>`。
- VLC iOS 使用 `vlc-x-callback://x-callback-url/stream?url=...&sub=...`，其他平台使用 `vlc://`。
- Infuse 使用 `infuse://x-callback-url/play?url=...&sub=...`。
- mpv 使用 `mpv://play/`，实际能力由本机注册该协议的 Handler 决定。
- nPlayer 把 HTTP(S) 地址转换为 `nplayer-http(s)://`。

参考：[VLC iOS URL Handler](https://github.com/videolan/vlc-ios/blob/master/Sources/Helpers/Network/URLHandler.swift)、[Infuse x-callback API](https://support.firecore.com/hc/en-us/articles/215090997-API-for-Third-Party-Apps-Services)、[PotPlayer 参数参考](https://potplayer.org/en/update/history.html)。

## 自定义播放器

模板必须以非 Web 自定义协议开头并包含 `{url}`，支持以下占位符：

| 占位符 | 内容 |
|---|---|
| `{url}` | 短期票据保护的媒体 URL |
| `{title}` | Emby 媒体标题 |
| `{subtitle}` | 所选外挂字幕 URL |
| `{start}` | 续播秒数，没有续播位置时为 `0` |
| `{headers}` | 播放器需要附加的短期票据请求头 |

除 `{start}` 外，占位符都会百分号编码。如果一个 query 参数的值完全由空占位符构成，整个参数会被删除。例如没有字幕时：

```text
myplayer://open?url={url}&sub={subtitle}
```

会省略 `sub`，而不是生成 `sub=`。组合值如 `label=prefix-{title}` 和静态空参数不会被删除。

模板包含 `{subtitle}` 才声明外挂字幕能力并签发字幕票据；包含 `{headers}` 才声明请求头能力。支持 mpv 参数的 IINA 衍生应用可配置：

```text
iina-nova://weblink?url={url}&new_window=1&mpv_start={start}&mpv_sub-file={subtitle}&mpv_http-header-fields={headers}
```

`{headers}` 只能放在播放器官方定义的 HTTP 请求头参数中。不要把它放入标题、文件名或会转交第三方的位置。媒体和字幕分别使用 `X-Emby-Playback-Ticket` 与 `X-Emby-Subtitle-Ticket`，播放器需要把对应请求头应用到各自请求。

## 网络要求

- 播放器必须能访问生成地址中的 Emby 主机和 base path。
- HTTPS 证书必须被播放器信任。
- 反向代理必须保留外部协议、Host、base path 和票据请求头。
- 票据 URL 在有效期内仍是 Bearer 凭证，不应分享或发送到第三方服务。
