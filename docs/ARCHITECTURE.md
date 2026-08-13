# 架构

## 目标

插件只解决一件事：在服务器提供的 Emby Web 中选择媒体和外部播放器，并安全地把当前媒体交给本地应用。

它不负责播放进度回传、转码、远程媒体代理、播放器安装检测或原生 Emby 客户端集成。

## 组成

```text
Emby Web
  └─ external-player.js / external-player.css
       ├─ 获取媒体与播放器清单
       ├─ 选择播放器、版本、字幕和续播方式
       └─ 打开 Resolve 返回的 URL Scheme

Emby.ExternalPlayer.dll
  ├─ 配置与播放器注册表
  ├─ Manifest / Resolve / 配置 API
  ├─ 短期票据存储
  ├─ 媒体与字幕流入口
  └─ Dashboard UI 加载器安装与撤销
```

Web 资源嵌入 DLL，由匿名只读资源接口提供。插件启动时只向 `dashboard-ui/app.js` 的已知锚点加入带唯一标记的加载语句；停止、禁用或卸载时精确移除该语句。锚点未知或文件不可写时安全失败，不替换整个 Emby Web 文件。

## 播放流程

1. Web 模块识别当前可见的视频详情页，在“从头开始”按钮右侧加入外部播放入口。
2. `GET /ExternalPlayer/Manifest` 根据当前认证用户、媒体条目、平台和语言返回可用播放器、媒体版本、外挂字幕、续播位置及个人默认播放器。
3. 用户确认后，Web 模块向 `POST /ExternalPlayer/Resolve` 提交所选媒体源、字幕、播放器和续播方式。
4. 服务端重新检查用户权限、媒体源、字幕、播放器状态和平台范围。
5. 服务端为媒体和字幕分别签发短期票据，生成播放器 URL Scheme。
6. 浏览器把 URL Scheme 交给操作系统，播放器通过票据流入口读取媒体和字幕。

Manifest 只用于展示。Resolve 不信任浏览器先前取得的数据，因此直接构造请求不能绕过服务端校验。

## API

| 路由 | 权限 | 用途 |
|---|---|---|
| `GET /ExternalPlayer/Manifest` | 已认证用户 | 获取当前条目的播放器和媒体清单 |
| `POST /ExternalPlayer/Resolve` | 已认证用户 | 校验选择并生成启动地址 |
| `POST /ExternalPlayer/UserDefaultPlayer` | 已认证用户 | 保存当前用户的平台默认播放器 |
| `GET/POST/DELETE /ExternalPlayer/CustomPlayers` | 管理员 | 管理自定义播放器 |
| `GET/POST /ExternalPlayer/BuiltInPlayerPlatforms` | 管理员 | 管理内置播放器适用平台 |
| `GET/HEAD /ExternalPlayer/Stream/{FileName}` | 短期票据 | 读取媒体 |
| `GET/HEAD /ExternalPlayer/Subtitle/{Index}/{FileName}` | 短期票据 | 读取外挂字幕 |

管理员 API 虽使用认证路由，仍会在服务端显式检查管理员权限。

## 配置

基础设置和内置播放器平台范围使用 Emby Generic UI。字段变化通过串行自动保存提交，仍经过 Emby 的验证和保存回调；离开页面前会刷新待保存修改。

自定义播放器是多字段实体，使用独立的逐项保存与删除 API。新条目在客户端先生成稳定 GUID，响应丢失后重试不会重复创建。

个人默认播放器按“服务器、用户、平台”隔离。服务端仅按“用户、平台”持久化到 Emby 配置目录的独立 XML；文件采用原子替换和 `.bak` 恢复副本，不与管理员插件配置竞争写入。

## 播放器模型

内置和自定义播放器统一描述为：

- 稳定播放器 ID
- 官方应用名称
- 启用状态
- 适用平台集合
- URL Scheme 白名单
- 续播、外挂字幕和请求头能力

自定义模板必须使用非 Web 自定义协议并包含 `{url}`。可选占位符为 `{title}`、`{subtitle}`、`{start}` 和 `{headers}`；具体渲染规则见 [播放器与 URL 模板](CLIENT_HANDLERS.md)。

## 票据与流

媒体与字幕票据彼此独立，每张票据绑定用户、条目、媒体源、资源路径、长度、最后修改时间、作用域和绝对过期时间。服务端只保存票据摘要，禁用插件、卸载或重启都会清空内存票据。

播放器支持请求头时，票据通过 `X-Emby-Playback-Ticket` 和 `X-Emby-Subtitle-Ticket` 传递；否则使用 Emby 可脱敏的 `api_key` 查询参数。流入口在每次读取时重新检查票据、用户权限、当前媒体源和文件状态。

## Web 生命周期

Emby Web 是单页应用，同一详情页可能保留隐藏旧节点并重建可见节点。Web 模块使用路由代次和可见性检查选择当前动作区，清理重复入口，并使异步 Manifest、配置请求和用户偏好保存只更新其原始服务器、用户和页面上下文。

配置页增强同样绑定服务器和用户上下文。切换服务器、切换用户、离开页面或刷新时，旧请求和定时任务不能更新新的页面实例。

## 边界

- 物理文件路径只保留在服务端，不返回 Web 或写入生产日志。
- 浏览器无法可靠枚举已安装应用，插件只展示管理员启用的入口。
- URL Scheme 的最终解析行为由客户端应用决定。
- 当前一次只打开一个媒体条目，不维护播放器队列。
- 详细威胁模型见 [安全模型](SECURITY.md)。
