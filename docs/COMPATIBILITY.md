# 兼容性

## 已验证环境

当前开发和实际使用环境均为 macOS。以下 Emby Server 版本在 macOS 隔离环境中完成 DLL 加载、配置页、认证 API、媒体与字幕读取、续播、HEAD、Range 和日志泄漏检查：

| Emby Server | 插件加载 | Web 配置 | 播放清单与解析 | 媒体/字幕流 |
|---|---:|---:|---:|---:|
| 4.9.1.80 | 通过 | 通过 | 通过 | 通过 |
| 4.9.3.0 | 通过 | 通过 | 通过 | 通过 |
| 4.9.5.0 | 通过 | 通过 | 通过 | 通过 |

插件目标框架为 `netstandard2.1`，Emby SDK 编译基线固定为 4.9.1.80。

## 播放器状态

| 播放器 | 初始适用平台 | 实际验证状态 |
|---|---|---|
| IINA | macOS | 已在 macOS 实际使用 |
| PotPlayer | Windows | 已实现，未在 Windows 验证 |
| VLC media player | Windows、macOS、iOS、Android、Linux | 已实现，未完成各平台端到端验证 |
| Infuse | macOS、iOS | 已实现，未完成端到端验证 |
| mpv | Windows、macOS、Linux | 实验适配器，默认关闭 |
| nPlayer | iOS、Android | 实验适配器，默认关闭 |

表中的平台是首次安装默认值，管理员可以在插件配置中修改。除 macOS 外的实现依据播放器公开协议或社区兼容形式编写，不构成已验证支持承诺。

## 限制

- 只支持服务器提供的 Emby Web。
- 只支持本地 `File` 媒体源和 Emby 已识别的本地外挂字幕。
- 原生电视、移动客户端以及 STRM、HLS、远程 URL 不在当前范围内。
- Emby Dashboard UI 没有稳定的官方按钮扩展接口；未知 Web 结构会使入口安全失效，而不会修改不认识的文件内容。
- 浏览器、操作系统和播放器都可能拦截或改变自定义协议行为，需要在目标环境人工确认。
