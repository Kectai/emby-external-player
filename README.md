# Emby External Player

[![Build and release](https://github.com/Kectai/emby-external-player/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/Kectai/emby-external-player/actions/workflows/build-and-release.yml)
[![Latest release](https://img.shields.io/github/v/release/Kectai/emby-external-player)](https://github.com/Kectai/emby-external-player/releases/latest)

Emby External Player 是一个面向 Emby Server 4.9.x 的轻量服务端插件。它在服务器提供的 Emby Web 视频详情页加入“外部播放”入口，让用户选择播放器、媒体版本、外挂字幕和续播位置，再通过操作系统 URL Scheme 打开本地播放器。

**本项目源于作者的个人使用需求。**不同播放器对 HDR、媒体格式和字幕的支持各不相同，实际播放效果也可能有所差异。本项目希望能在 Emby Web 中更方便地切换外部播放器，在遇到兼容性问题时快速选择更合适的播放器，同时保留媒体版本、字幕等常用选项。代码公开在 GitHub，主要用于留存项目，也希望能为有类似需求的用户提供参考或直接使用。目前仅在 macOS 上通过 IINA 完成实际使用和验证，其他平台适配依据公开协议实现，尚未进行端到端验证。项目将随作者自身需求不定期更新，目前没有固定的维护计划或功能路线图。

## 界面预览

![Emby 视频详情页中的外部播放入口](docs/images/external-play-button.png)

## 主要功能

- 内置 PotPlayer、IINA、VLC media player、Infuse、mpv 和 nPlayer 适配器。
- 管理员可启用播放器、配置适用平台及各平台默认播放器。
- 支持自定义播放器、多个适用平台和 URL Scheme 模板。
- 每个 Emby 用户可按当前平台保存自己的默认播放器。
- 支持媒体版本、Emby 已识别的外挂字幕和从上次位置继续。
- 提供简体中文、繁体中文和英文界面。
- 使用短期、分作用域的媒体与字幕票据，不把完整 Emby Token 交给播放器。
- 支持 HEAD 和单 Range 请求。
- 配合 [IINA Emby Playback Reporter](https://github.com/Kectai/iina-emby-playback-reporter) 在 IINA 及兼容的第三方衍生客户端中回传播放、暂停、跳转和最终位置；自定义播放器由管理员逐项启用，未安装客户端插件时仍可正常外部播放。

## 适用环境

- Emby Server 4.9.x 提供的 Emby Web。
- 本地 `File` 媒体源和 Emby 已识别的本地外挂字幕。
- 能够由浏览器通过 URL Scheme 唤起并访问 Emby 地址的播放器。
- Web 入口依赖 Emby 4.9.x 的 Dashboard UI 结构；Emby 更新后可能需要调整适配。

## 安装

1. 从 [最新 Release](https://github.com/Kectai/emby-external-player/releases/latest) 下载 `Emby.ExternalPlayer-1.7.0.zip`，校验同页提供的 SHA-256 后取出 `Emby.ExternalPlayer.dll`。
2. 停止 Emby Server，把 DLL 放入 Emby 程序数据目录的 `plugins` 文件夹。
3. 启动 Emby，在插件设置中启用所需播放器。
4. 强制刷新 Emby Web，再打开视频详情页。

如需播放进度回传，请另外安装配套 IINA 插件。内置 IINA 默认支持；第三方衍生客户端需要在自定义播放器中使用 `{headers}`，并显式打开“启用播放进度回传”。服务端 `1.7.0` 支持 Playback Reporting Protocol v1；客户端插件不是使用外部播放的前置条件。

详细安装、升级和卸载步骤见 [安装说明](docs/INSTALL.md)。

## 配置原则

- 播放器的启用状态、适用平台和各平台默认项由管理员统一配置；每个用户仍可在 Web 中保存自己的默认播放器。
- 内置 IINA 适配器默认附加播放进度回传信息；其他内置播放器不回传。
- 自定义播放器只有同时包含 `{headers}` 并打开“启用播放进度回传”时才附加回传信息，不依据名称、URL Scheme 或客户端标识猜测。
- 关闭服务端总开关会结束本插件建立的活跃回传会话，但不会影响外部播放器继续播放媒体。

## 隐私与安全

插件不把 Emby API Key 写入播放器 URL 或交给外部播放器；媒体、字幕和进度回传分别使用短期、分作用域票据。仓库发布流程会扫描本机路径、邮箱、私钥、常见 Token 和凭据形态。完整威胁模型与反向代理要求见 [安全模型](docs/SECURITY.md)。

## 文档

- [架构](docs/ARCHITECTURE.md)
- [播放器与 URL 模板](docs/CLIENT_HANDLERS.md)
- [兼容性](docs/COMPATIBILITY.md)
- [安全模型](docs/SECURITY.md)
- [测试](docs/TESTING.md)

## 许可证

MIT，见 [LICENSE](LICENSE)。
