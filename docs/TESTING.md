# 测试

## 常用命令

```bash
./scripts/test.sh
./scripts/verify.sh
./scripts/package.sh
```

- `test.sh` 运行 .NET 单元测试和 Web 测试。
- `verify.sh` 在测试基础上检查程序集与 Web 资源体积、目标框架、SDK 基线、测试凭据和 Git diff。
- `package.sh` 生成版本化 ZIP 和 SHA-256 文件。

所有 NuGet 缓存、编译输出、测试结果和临时文件都写入仓库忽略的 `.local/`，不会使用或修改本机 Emby Server 数据。

## 覆盖范围

自动化测试覆盖：

- Dashboard UI 加载器的幂等安装、安全撤销和未知锚点处理。
- Manifest、Resolve、管理员配置和用户默认播放器的认证、校验与并发行为。
- 内置与自定义播放器平台范围、URL 模板、空参数裁剪和协议限制。
- 媒体与字幕票据的作用域、容量、过期、格式、权限和文件状态复核。
- HEAD、单 Range、文件名清理、请求头注入和日志敏感信息扫描。
- Emby Web 路由重建、重复进入、配置自动保存、服务器或用户切换、弹窗焦点和窄屏布局。
- 用户播放器偏好文件的迁移、原子写入、备份恢复、权限和卸载清理。
- 回传票据摘要存储、容量、协议字段和限频。
- Start/Progress/Stop 幂等、乱序拒绝、单写入者租约转移、旧 Stop 隔离和 watchdog epoch 恢复。
- STRM 尚无服务端时长时采纳客户端时长、延迟获得时长、Emby 时长优先及非法时长拒绝。
- STRM 重定向的单一 30 秒租约、过期清理、跨 `User-Agent` 的源级失败退避与 `Retry-After`、禁止返回旧地址和主动访问 CDN，以及并发请求合并、取消与发布所有权。

`tests/visual/` 是不连接 Emby Server 的本地视觉夹具，用于人工检查主题、间距和响应式布局。

## 隔离集成测试

`scripts/test-integration.sh` 只接受：

- 回环地址且不是默认 8096 端口的 Emby 测试实例。
- 位于当前项目 `.local/` 下的 program data 和 `dashboard-ui/app.js`。

脚本会验证真实插件加载、认证、配置、两种媒体版本、SRT/ASS、续播、HEAD、Range 和日志泄漏。测试宿主未准备时，依赖官方 `app.js` 的兼容性用例会跳过，其余测试仍可运行。

## 人工验证

当前发布前至少验证 macOS Emby Web、IINA、HTTPS、字幕、续播、前进后退和窄屏页面。进度回传还需在真实 IINA 中验证 `http-header-fields` 可读性、暂停、seek、正常结束、窗口关闭、断网、睡眠恢复和连续媒体切换。Windows、iOS、Android 或 Linux 在标记为已验证之前，必须分别完成对应播放器的实际协议跳转和读取测试。
