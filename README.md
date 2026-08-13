# Emby External Player

Emby External Player 是一个面向 Emby Server 4.9.x 的轻量服务端插件。它在服务器提供的 Emby Web 视频详情页加入“外部播放”入口，让用户选择播放器、媒体版本、外挂字幕和续播位置，再通过操作系统 URL Scheme 打开本地播放器。

> 本项目最初只为个人使用而开发。目前仅在 macOS 上完成实际使用和验证；Windows、iOS、Android 与 Linux 的播放器适配依据公开协议实现，尚未在对应系统上进行端到端验证。

## 主要功能

- 内置 PotPlayer、IINA、VLC media player、Infuse、mpv 和 nPlayer 适配器。
- 管理员可启用播放器、配置适用平台及各平台默认播放器。
- 支持自定义播放器、多个适用平台和 URL Scheme 模板。
- 每个 Emby 用户可按当前平台保存自己的默认播放器。
- 支持媒体版本、Emby 已识别的外挂字幕和从上次位置继续。
- 提供简体中文、繁体中文和英文界面。
- 使用短期、分作用域的媒体与字幕票据，不把完整 Emby Token 交给播放器。
- 支持 HEAD 和单 Range 请求，不启动常驻轮询，也不修改 Emby 数据库。

## 使用范围

- 仅支持服务器提供的 Emby Web；不改变原生电视或移动客户端。
- 仅处理本地 `File` 媒体源和本地外挂字幕。
- STRM、HLS、远程 URL 和虚拟媒体源不会降级为携带 Emby Token 的播放地址。
- 当前只负责打开单个媒体，不回传播放进度，也不向播放器传递连续播放列表。
- Web 入口依赖 Emby 4.9.x 的 Dashboard UI 结构；Emby 更新后可能需要调整适配。

## 安装

1. 从发布包中取出 `Emby.ExternalPlayer.dll`。
2. 停止 Emby Server，把 DLL 放入 Emby 程序数据目录的 `plugins` 文件夹。
3. 启动 Emby，在插件设置中启用所需播放器。
4. 强制刷新 Emby Web，再打开视频详情页。

详细安装、升级和卸载步骤见 [安装说明](docs/INSTALL.md)。

## 文档

- [架构](docs/ARCHITECTURE.md)
- [播放器与 URL 模板](docs/CLIENT_HANDLERS.md)
- [兼容性](docs/COMPATIBILITY.md)
- [安全模型](docs/SECURITY.md)
- [测试](docs/TESTING.md)

## 许可证

MIT，见 [LICENSE](LICENSE)。
