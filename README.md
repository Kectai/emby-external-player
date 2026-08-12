# Emby External Player

一个轻量的 Emby Server 4.9.x 插件：只安装服务端 DLL，即可在 Emby Web 的电影、剧集和普通视频详情页增加“外部播放”按钮，并选择播放器、媒体版本、外挂字幕和续播位置。

不需要浏览器扩展或用户脚本；不写 Emby 数据库，不做播放进度回传，不启动后台轮询任务。

## 功能

- PotPlayer、IINA、VLC media player、Infuse；mpv 与 nPlayer 为默认关闭的实验适配器，应用名称保持官方大小写。
- 配置页提供独立的自定义播放器编辑区，可一次新增多个草稿，每条配置单独保存或删除，不再与页面整体保存强耦合；名称原样显示，URL Scheme 模板支持 `{url}`、`{title}`、`{subtitle}`、`{start}`、`{headers}`。
- 界面和配置页根据 Emby 客户端语言适配简体中文、繁体中文和英文，其他语言回退英文。
- 详情页入口直接复用“从头开始”按钮的 Emby 类与原生图标结构，并持续跟随页面重建恢复在其右侧；播放器选择器跟随当前主题，并为内置/自定义应用提供清晰的选中状态和窄屏布局；媒体版本和字幕使用与字段精确等宽的可访问下拉列表。
- 安全中转 URL 的末段使用 Emby 媒体标题，播放器不再把固定路由名 `stream.js` 当作标题；该修复不依赖某个播放器的私有参数。
- IINA 及声明请求头能力的自定义播放器在安全中转地址中不再携带 `api_key` 查询串；短期票据通过受限 HTTP 请求头交付，标题栏只从干净的媒体路径读取名称。
- 多媒体版本、SRT/ASS 等 Emby 已识别的外挂字幕、服务端 UserData 续播位置；字幕只对声明支持该参数的播放器开放，避免选择后静默丢失。
- 默认 `SecureTicketRelay`：播放器 URL 不包含 Emby token，支持 HEAD 和单 Range/206。
- 短期 256 位随机票据，只在内存保存哈希索引，默认 8 小时、最多 2000 条，服务重启全部失效。
- 启动时幂等加载 Web 模块；停止、禁用或正常卸载时精确移除自己的加载片段。
- Web 适配失败时保持 Emby 原生播放不受影响。

## 支持范围

- 已在隔离的 Emby Server 4.9.1.80、4.9.3.0 和 4.9.5.0（.NET 6 宿主）完成 DLL 加载、配置 UI、认证、两媒体版本、SRT/ASS、续播、HEAD、非连续 Range 和泄漏扫描。
- 插件程序集目标框架为 `netstandard2.1`，SDK 编译基线固定为最低支持版本 4.9.1.80；与上述三个宿主实测无冲突。
- 只支持由服务器提供的 Emby Web。原生电视/移动客户端若不加载服务器 Web UI，不会显示按钮。
- Secure 模式当前只处理本地 `File` 媒体源。STRM、HLS 和远程 URL 需要显式启用 `LegacyTokenUrl`，并承担 token 暴露风险。

## 安装

1. 从 `artifacts/Emby.ExternalPlayer-1.4.9.zip` 取出 `Emby.ExternalPlayer.dll`。
2. 停止 Emby Server，把 DLL 放入 Emby 程序数据目录的 `plugins` 文件夹。
3. 启动 Emby，在插件设置中确认 `External Player` 已启用；默认安全模式无需额外配置。
4. 强制刷新一次 Emby Web，然后进入有媒体源的视频详情页。

升级和卸载前应正常停止 Emby，让插件安全撤销 Web 加载片段。完整步骤见 [安装与运维](docs/INSTALL.md)。

## 构建与验证

要求 .NET SDK 10.0.203。仓库把 NuGet、临时文件、编译产物和测试结果全部定向到项目内 `.local/`：

```bash
./scripts/test.sh
./scripts/build.sh
./scripts/package.sh
```

生成的发布 ZIP 和 SHA-256 位于 `artifacts/`。隔离 Emby 集成测试的准备和命令见 [测试说明](docs/TESTING.md)。

## 文档

- [详细设计](docs/DESIGN.md)
- [安装与运维](docs/INSTALL.md)
- [客户端 URL Handler](docs/CLIENT_HANDLERS.md)
- [安全模型](docs/SECURITY.md)
- [兼容性矩阵](docs/COMPATIBILITY.md)
- [测试说明](docs/TESTING.md)

## 许可证

MIT，见 [LICENSE](LICENSE)。
