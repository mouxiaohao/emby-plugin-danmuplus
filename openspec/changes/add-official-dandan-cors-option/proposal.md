## Why

Dandanplay API 凭据仅向开发者开放，而 DanmuPlus 仍处于开发阶段，因此新安装应能直接使用插件维护的 CORS 代理，无需用户自行申请凭据或填写代理地址。同时，已有自定义代理配置必须在升级后保持原有路由，避免无意切换服务。

## What Changes

- 在现有“使用代理 API”模式下新增“使用插件官方 CORS 地址”选项；新安装以及缺少新字段且没有既有 `ProxyCorsUrl` 的配置默认勾选。
- 对缺少新字段但已有非空 `ProxyCorsUrl` 的升级配置继续使用自定义 CORS；用户明确取消勾选后启用自定义输入框，来回切换不清空其保存值。
- 勾选官方 CORS 时，由后端使用内建地址；该地址不渲染到 HTML/JavaScript，不写入配置、配置响应或输入框，软件界面始终不展示它。
- 保持现有一级路由语义：非代理模式使用官方 API 并由插件本地签名；代理加官方 CORS 或自定义 CORS 时均由代理签名，插件不在本地签名。
- 保持现有 Dandanplay `search`、`bangumi`、`comment` 调用、标题/年份/季度/集数评分、自动/手动绑定以及 XML/ASS 输出，不引入 Hash 识别或 `/match`。
- 本次人工在线验证仅作为代理可行性的前置证据；`2.0.1-r5` 实施后仍执行官方 CORS、自定义 CORS、官方直连三路回归。
- 本变更依赖 `cache-bust-plugin-config-page`，并与另一个对话中的 r5 变更联合构建、部署和发布，不单独发布。

明确非目标：不将官方 CORS 地址视为秘密或访问控制机制；尽管界面不显示，公开源码、反编译 DLL 或网络流量仍可观察该地址。

## Capabilities

### New Capabilities

- `dandan-official-cors-routing`: 定义官方 CORS 选择、默认与升级迁移、界面隐藏约束，以及直连/官方代理/自定义代理三路路由行为。

### Modified Capabilities

无。

## Impact

- 影响 Dandanplay 配置模型、配置保存与响应序列化、Emby 插件配置页面及 Dandanplay 请求路由。
- 后端将包含官方 CORS 地址常量；该常量不得进入前端资源或持久化配置。
- 发布验证需覆盖配置迁移、UI 状态、三个既有 API 端点、匹配与输出回归、日志凭据检查，以及 r5 的备份、校验和与回滚流程。
- 不修改现有 `add-dandan-api-proxy-mode` 与 `cache-bust-plugin-config-page` change；实施时按依赖整合。
