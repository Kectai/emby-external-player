# 安全模型

## 默认信任边界

Manifest 和 Resolve 使用 Emby 的认证上下文确定用户，不接受浏览器提交的用户 ID。服务端重新验证条目可见性、播放权限、媒体源 ID、外挂字幕索引和播放器 ID。

SecureTicketRelay 在 Resolve 时只接受 Emby 返回的本地 `File` 媒体源。物理路径不返回浏览器；票据中也不保存 Emby token。每次流请求仍会重新检查用户是否存在、条目是否可见以及是否仍有播放权限，权限撤销后立即废止票据。

## 播放票据

- 32 字节密码学随机数，URL-safe Base64 表示。
- 内存字典只以 SHA-256 摘要为键，不保存原始票据作为键。
- 绝对有效期 30–720 分钟，默认 8 小时；不会滑动续期。
- 最多 2000 条，优先清理过期项并淘汰最早创建项。
- 可重复用于播放器的 HEAD 和多个 Range 请求。
- 服务或插件重启后内存票据全部失效。

票据本身是短期 Bearer 凭证。生产环境必须使用 HTTPS，避免复制完整播放器 URL，也不要把它发往分析、错误上报或第三方重定向服务。

## 本地文件流

插件不向用户提供的 URL 发起请求，也不通过本机 Emby HTTP 接口回源。这样同时消除了 SSRF 面和 Emby 核心记录回源认证头的风险。

文件由 Emby 已授权的 MediaSource/MediaStream 路径确定，使用 64 KB 异步 FileStream，并由 Emby `IHttpResultFactory` 限定输出长度和处理单 Range。文件大小在出票后变化会使票据失败，避免内容被静默替换。

流和字幕路由使用固定的 `stream.js`/`subtitle.css` 尾缀。这是针对 Emby 4.9.x 核心请求日志行为的保护：核心跳过静态扩展的详细请求日志，从而不记录 URL 中的原始票据。实际响应的 `Content-Type`、`Content-Length` 和 `Content-Disposition` 都按媒体文件返回，并非 JavaScript/CSS 内容。

## 日志和响应

插件日志不包含 Emby token、原始票据、完整播放 URL或服务器物理路径。响应文件名限制为 ASCII 安全集，拒绝 CR/LF、引号和反斜杠注入；响应包含 `Cache-Control: private, no-store` 和 `X-Content-Type-Options: nosniff`。

集成测试在认证完成后记录日志偏移，执行 Manifest、Resolve、HEAD、同一版本的三次非连续 Range、第二媒体版本 Range 和两种字幕读取，再按精确 token、全部票据和完整 URL 扫描新增日志。认证偏移是必要的，因为 Emby 4.9.x 自身会在 `AuthenticateByName` 阶段记录新签发的 token；该行为不由插件产生。

## LegacyTokenUrl

Legacy 模式把 Emby token 放入原生 stream URL 的 `api_key` 查询参数，只用于 Secure 不支持的远程源。它可能暴露在播放器历史、代理访问日志、剪贴板和故障截图中。配置页明确标注风险，默认关闭。

## 剩余风险

- 票据有效期内被复制后可重放；默认不绑定 IP，以免浏览器与播放器出口不同。
- Web 按钮依赖非官方的受控 UI 锚点；Emby 更新可能使按钮安全失效。
- 服务器进程本身有权读取媒体文件，插件共享这一信任边界。
- 自定义协议最终由客户端应用解析；建议保持播放器为受支持的新版本。
