# 安装与运维

## 前置条件

- Emby Server 4.9.x；发布前自动化验证版本为 4.9.1.80、4.9.3.0 与 4.9.5.0。
- Emby 的程序数据目录及服务器自带 `dashboard-ui/app.js` 对 Emby 进程可写。
- 浏览器可以通过 HTTPS/HTTP 访问 Emby，目标播放器已注册对应 URL scheme。

插件不安装播放器、不注册客户端协议，也不要求浏览器扩展。

## 首次安装

1. 正常停止 Emby Server。
2. 解压发布包，只把 `Emby.ExternalPlayer.dll` 复制到 Emby 程序数据目录下的 `plugins` 文件夹。
3. 启动 Emby Server。
4. 进入管理后台的插件页面，打开 `External Player` 设置页。
5. 保持 `SecureTicketRelay`，按平台启用播放器并设置默认播放器。
6. 强制刷新 Emby Web，打开电影、单集或普通视频详情页。

启动时插件只向 `dashboard-ui/app.js` 的已知 4.9.x 锚点插入一条带唯一标记的模块加载语句。若文件只读或锚点未知，插件后端仍可加载，但不会改动未知内容，也不会影响原生播放。

## 配置要点

- `Enabled`：总开关；关闭后 API 停止工作并撤销 Web 加载片段。
- `EnableWebButton`：只控制 Web UI 集成。
- `StreamMode`：默认安全票据；只有 Secure 不支持的远程源才考虑 Legacy。
- `TicketLifetimeMinutes`：30–720 分钟，默认 480。
- `RestartNearEndMinutes`：距结尾小于该值时不再续播，默认 5 分钟。
- 各平台默认播放器会排在弹窗按钮首位。

## 升级

1. 正常停止 Emby，确认日志出现 `External Player Web bootstrap state changed: Removed.`。
2. 替换程序数据目录中的 DLL。
3. 启动 Emby 并检查配置页、详情页按钮和一条 Range 请求。

Emby 自身升级会替换 `app.js`。插件下次启动会针对已知锚点重新注入；锚点不匹配时安全失败，不做模糊修改。

## 禁用与卸载

推荐先在插件设置关闭 `Enabled` 或 `EnableWebButton`，再正常停止 Emby 并删除 DLL。正常停止时插件会精确移除自己的标记片段。

不要在 Emby 被强制终止后直接删除 DLL；这种情况下清理回调可能没有执行。如果已经发生，恢复 DLL、启动 Emby、关闭插件并正常停止一次。最后手工检查 `app.js` 中是否还存在：

```text
/* Emby.ExternalPlayer bootstrap: 6f784f38 */
```

## 故障排查

- 插件列表中不存在：先在服务器启动日志搜索 `Emby.ExternalPlayer`。若随后出现 `Could not load file or assembly ... Version=...`，说明 DLL 的 Emby SDK 编译版本高于服务器版本；1.0.1 已把最低编译基线固定为 4.9.1.80。
- 点击播放器后访问 `/web/undefined`：这是 1.0.1 未要求 Emby Web AJAX 将 Resolve 响应解析为 JSON 所致；升级到 1.0.2。新版也会拒绝缺少有效 `LaunchUrl` 的响应，不再导航到 `undefined`。
- 没有按钮：检查插件设置、服务器日志中的锚点警告、浏览器是否强制刷新，以及当前条目是否有可播放媒体源。
- 点击无反应：确认播放器协议已注册；浏览器第一次打开自定义协议通常需要用户确认。
- Secure 返回“不支持本地文件”：媒体源是 STRM/HLS/远程 URL。优先保持安全模式；确有需要时才使用 Legacy。
- 反向代理：应保留原 Host、协议和 base path。Resolve 根据当前请求地址生成播放器 URL。
- HTTPS：外部播放器必须信任证书；自签名证书是否可用取决于播放器平台。
