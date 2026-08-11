# 兼容性矩阵

验证日期：2026-08-12。所有服务器、媒体、日志、缓存和程序数据均位于仓库 `.local/`，端口为 18091/18093/18095；本机已安装的 8096 Emby 实例未被修改。

| 项目 | Emby 4.9.1.80 | Emby 4.9.3.0 | Emby 4.9.5.0 |
|---|---:|---:|---:|
| `netstandard2.1` DLL 加载 | 通过 | 通过 | 通过 |
| Generic UI 配置页 | 通过 | 通过 | 通过 |
| JS/CSS 匿名资源 | 通过 | 通过 | 通过 |
| `app.js` 已知锚点、单次注入 | 通过 | 通过 | 通过 |
| Manifest/Resolve 真实鉴权 | 通过 | 通过 | 通过 |
| 两媒体版本 | 通过 | 通过 | 通过 |
| SRT/ASS | 通过 | 通过 | 通过 |
| 120 秒续播 | 通过 | 通过 | 通过 |
| HEAD | 200 | 200 | 200 |
| 4 个 Range（含第二媒体版本） | 4 × 206 | 4 × 206 | 4 × 206 |
| token/票据/完整 URL 泄漏扫描 | 通过 | 通过 | 通过 |
| 正常停止撤销注入 | 通过 | 通过 | 通过 |

插件 1.0.0 曾使用 `MediaBrowser.Server.Core` 4.9.1.90 编译，在 4.9.1.80 宿主上会因强版本程序集引用触发 `ReflectionTypeLoadException`，导致 DLL 被扫描但插件类型无法发现。1.0.1 将 SDK 基线降至 4.9.1.80，并通过发布门禁防止意外升级；同一 DLL 已验证可向上加载到 4.9.3.0 与 4.9.5.0。

插件 1.0.1 的 Web `Resolve` 调用未显式设置 `dataType: "json"`；Emby Web 4.9.1.80 因而完成 POST 却把未解析的结果交给回调，最终导航到 `/web/undefined`。1.0.2 显式请求 JSON、兼容 PascalCase/camelCase，并对无效 `LaunchUrl` 安全失败。Web 回归测试覆盖成功跳转和缺失地址不得跳转两条路径。

插件 1.1.0 增加简体中文、繁体中文、英文语言目录、配置页本地化、自定义播放器模板及按播放器声明的协议白名单。隔离集成测试同时验证中文 Manifest、中文 Generic UI、官方应用名称、IINA 标题参数以及自定义播放器集合可被 Emby Generic UI 正常构建。

插件 1.1.1 修正上述 IINA 标题判断：IINA 的 URL scheme 会拒绝不在 `safeMPVOptions` 中的 `force-media-title`。新版改为通用标题路径 `/ExternalPlayer/Stream/{媒体标题}?api_key={短期票据}`，并依靠 Emby 对 `api_key` 查询值的日志脱敏保护票据；旧 `stream.js` 路由保留兼容。该修复已通过 URL 构造、Unicode/路径注入、IINA 参数和 Web 回归测试；完整 Emby 测试宿主已按要求清理，尚未为 1.1.1 重新下载。

Web 模块测试使用无依赖的假 DOM 覆盖：重复加载只保留一个按钮、事件退订、PascalCase/camelCase API 兼容、Escape 关闭、焦点恢复与 focus trap。实际 Chrome、Firefox、Safari 的人工视觉回归尚需在部署环境完成，因此不能把 DOM 自动化等同于三款浏览器实测。

播放器 URL 适配器均有编码与能力测试。本机检测到 IINA 1.4.4 注册了 `iina` 协议，但遵循环境隔离要求未实际拉起；PotPlayer、VLC media player、Infuse 未安装，需按 [客户端说明](CLIENT_HANDLERS.md) 完成人工矩阵。

## 支持与限制

- 支持 Emby Web 的电影、单集和普通 Video 条目；服务端统一要求条目继承 Emby `Video` 且当前用户可见、允许播放。
- Secure 模式支持服务器本地 `File` 媒体和本地外挂字幕；不支持 STRM、HLS、远程 URL、实时电视和需要转码的虚拟源。
- 不支持不加载服务器 `dashboard-ui` 的原生客户端。
- 不做播放进度回传。
- 仅对精确的 4.9.x `app.js` 锚点注入；未知版本安全失败。
