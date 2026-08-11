# 测试说明

## 环境隔离

`scripts/dotnet-local.sh` 固定以下目录到仓库 `.local/`：

- `DOTNET_CLI_HOME`
- NuGet packages 与 HTTP cache
- `TMPDIR`
- 编译中间产物和输出
- 测试结果与工作目录

`.local/` 和 `artifacts/` 不进入 Git。集成脚本拒绝 8096、非回环地址或项目外的 Emby program data/dashboard 文件。

## 单元与 Web 测试

```bash
./scripts/test.sh
```

覆盖加载器幂等与安全移除、未知锚点、播放器编码、URL base path、媒体/字幕/播放器选择校验、续播边界、票据随机性/过期/容量/并发/重启失效、Range、路径与 header 注入，以及 Web 生命周期和可访问性行为。

## 隔离 Emby 集成测试

先在项目 `.local/emby-hosts/<version>/` 放置官方测试发行版，并使用 `.local/emby-programdata/version-<version>/` 启动到非 8096 回环端口。测试媒体应位于 `.local/test-media/`。

示例：

```bash
EMBY_INTEGRATION_BASE='http://127.0.0.1:18095/' \
EMBY_INTEGRATION_VERSION='4.9.5.0' \
EMBY_INTEGRATION_USER='integration-admin' \
EMBY_INTEGRATION_PASSWORD='local-test-only-4.9.5' \
EMBY_INTEGRATION_PROGRAMDATA="$PWD/.local/emby-programdata/version-4.9.5.0" \
EMBY_INTEGRATION_DASHBOARD_APP="$PWD/.local/emby-hosts/4.9.5.0/osx-arm64/EmbyServer.app/Contents/Resources/dashboard-ui/app.js" \
./scripts/test-integration.sh
```

脚本验证官方版本号、插件加载、Generic UI 配置、Web 资源、唯一注入标记、真实认证、权限拒绝、两个实际媒体版本、SRT/ASS 实际读取、续播、HEAD、四个 Range 和新增日志泄漏。

## 发布检查

```bash
./scripts/verify.sh
./scripts/package.sh
```

`verify.sh` 要求所有自动化测试通过、DLL 小于 1 MiB、内嵌 JS+CSS 小于 80 KiB、SDK 最低编译基线保持 4.9.1.80，并扫描源代码中不应出现的硬编码测试凭据。`package.sh` 生成 ZIP 与 SHA-256。

## 人工检查

发布到实际用户前还需完成：

- Chrome、Firefox、Safari 的详情页、前进后退、窄屏和协议拦截提示。
- 至少一个 Windows 播放器和一个 macOS/iOS 播放器的纯 URL、HTTPS、字幕与续播。
- 实际反向代理 base path 和有效 HTTPS 证书。
- Emby 升级后的未知锚点安全失败演练。
