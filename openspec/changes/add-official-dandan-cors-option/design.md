## Context

参见 `proposal.md` 的动机以及 `specs/dandan-official-cors-routing/spec.md` 的行为契约。现有 `add-dandan-api-proxy-mode` 已规划以 `UseProxyApi` 和 `ProxyCorsUrl` 在官方直连与 CORS 前缀代理之间路由三个 Dandanplay v2 端点；本变更只在代理分支内增加官方/自定义 CORS 来源选择，不修改该 change。

本变更横跨序列化配置、Emby 嵌入式配置页和 Dandanplay 后端请求路由，并需要区分“旧配置没有新字段”与“用户明确保存 false”。同时它依赖 `cache-bust-plugin-config-page`，以确保 r5 新控件不会被旧浏览器缓存遮蔽。

## Goals / Non-Goals

**Goals:**

- 用单一、可迁移的配置状态表示官方 CORS 选择，同时保留现有自定义地址。
- 让后端成为官方 CORS 地址的唯一持有者，并从所有前端资源与配置载荷中排除该地址。
- 在同一请求构建入口实现直连、官方代理、自定义代理三路确定性路由。
- 将实施、部署和发布验证纳入其他 r5 变更的联合产物。

**Non-Goals:**

- 不把 Worker URL 当作秘密、鉴权凭据或防滥用措施；源码、反编译和网络观察均可能发现它。
- 不修改 Worker、Cloudflare Secrets、Dandanplay API 契约或凭据申请流程。
- 不改变匹配算法、媒体绑定、评论转换、缓存、重试或其他提供商。
- 不单独构建、部署或发布本 change，也不修改 `add-dandan-api-proxy-mode` 或 `cache-bust-plugin-config-page` 的 artifacts。

## Decisions

### 以可空布尔字段保留“未迁移”状态

在 Dandanplay 配置中新增可空布尔选择字段（实施时采用与现有命名一致的名称，例如 `UseOfficialProxyCors`），不把后端地址写入该字段或任何其他配置字段。运行时解析规则为：字段有值时严格使用该值；字段无值时，仅当 `ProxyCorsUrl` 为空白才解析为 true，否则解析为 false。配置页保存时总是写入明确的 true/false，因此用户明确取消勾选且自定义地址暂时为空的状态也不会在下次加载时被重置。

选择可空字段而不是初始化为 true，是因为普通布尔默认值无法区分旧安装与用户明确选择 false；另加迁移版本号会增加状态组合，却不能提供额外行为。解析可以在配置访问边界集中完成，避免读取配置时自动持久化，从而使升级检查保持无副作用。

### 后端常量与可序列化配置严格分离

将 `https://danmuplus-dandan-proxy.mouxiaohao.workers.dev/cors/` 定义为 Dandanplay 后端路由代码中的私有常量。配置对象、配置 API DTO、嵌入式 HTML、JavaScript、输入默认值、placeholder、帮助文本以及前端测试夹具均不得包含该字面量。配置响应可以返回官方 CORS 选择的解析后布尔值和用户自己的 `ProxyCorsUrl`，但不得用内建地址填充后者。

采用后端常量而不是隐藏输入或由 JavaScript 拼接，是因为任何发送到浏览器的数据都违背“软件界面不暴露”的约束。该边界不承诺真正保密：开源仓库、DLL 和代理网络请求仍可观察 URL。

### 在现有代理路由决策中加入二级来源选择

保留 `UseProxyApi` 为一级开关，并在集中请求路由器中按以下顺序决策：

1. `UseProxyApi == false`：使用官方 API URL，解析本地 AppId/Secret 并沿用现有签名头。
2. `UseProxyApi == true` 且官方 CORS 解析为 true：以私有常量作为 CORS 前缀，拼接完整官方目标 URL，并在解析本地凭据之前结束签名分支。
3. `UseProxyApi == true` 且官方 CORS 解析为 false：验证、规范化并使用保存的 `ProxyCorsUrl`，同样跳过本地签名。

官方与自定义前缀复用现有绝对 URL 拼接、末尾斜杠规范化和错误清理逻辑；自定义地址缺失或无效时显式报配置错误，不静默回退到官方 CORS 或直连。这样管理员选择的信任边界不会因错误而改变。

为三个端点分别实现分支会产生漂移，因此不采用；在 Worker URL 中附带本地签名也不采用，因为 Worker 使用自己的凭据签名目标路径。

### UI 只传递选择状态与用户自定义值

配置页在现有代理 API 区域加入复选框。代理模式关闭时保持代理来源控件非活动；代理模式开启且复选框选中时禁用自定义地址输入，取消时启用。加载时使用后端解析出的选择状态，保存时同时提交明确布尔值和当前自定义地址。切换仅改变可用状态，不清空 DOM 值，也不在后端保存时清空 `ProxyCorsUrl`。

继续保存禁用输入的自定义值而不是在切换时删除，是为了允许用户在官方服务不可用时无损切回自定义代理。复选框旁只说明“使用插件官方 CORS 地址”，不显示或推导实际 URL。

### r5 验证以确定性断言为主并辅以三路 live smoke

单元/回归断言覆盖可空字段的四类状态、前端启禁与值保留、三个端点的精确目标、签名分支、无地址前端暴露以及日志脱敏。源文件和生成前端资源需做字面量扫描，确保内建 URL 只位于批准的后端常量位置；配置序列化测试确认官方选择不会把 URL 写入 XML 或配置响应。

本次已完成的手工 Worker/Emby 验证只记录为可行性证据。联合 r5 DLL 部署后，使用动画剧集代表样本完成官方 CORS、自定义 CORS、官方直连三路最小 live smoke，覆盖 `search`、`bangumi`、`comment`，并复核自动/手动绑定、XML/ASS、其他提供商以及日志凭据泄漏。测试凭据只通过现有安全配置提供，不进入源码、规划、命令输出或 release notes。

## Risks / Trade-offs

- [公开 Worker 地址可被源码、DLL 或网络流量识别并滥用配额] → 在文档中诚实声明非秘密；依靠 Cloudflare/Dandanplay 侧监控与限额，而不以 UI 隐藏作为安全控制。
- [可空布尔在序列化器中被意外写成 false 或省略语义丢失] → 为缺字段/true/false 与空/非空自定义地址组合建立序列化和迁移回归。
- [配置 API 映射把后端常量复制到 `ProxyCorsUrl`] → 保持常量不属于配置模型，并加入 XML、JSON/配置响应和前端资源的负向断言。
- [三路中的某一路错误地继承本地签名头] → 在凭据解析之前集中分支，并对每个端点断言目标与认证头集合。
- [官方 Worker 不可用或达到免费配额] → 返回已清理的提供商错误且不静默改路由，允许管理员取消勾选后切换到自定义代理或关闭代理使用直连。
- [联合 r5 的配置页资源仍命中旧缓存] → 在合并后依赖 `cache-bust-plugin-config-page` 的版本化页面/控制器标识进行浏览器回归。
- [多个对话的 r5 工作区发生交叉覆盖] → 本 change 保持独立 artifacts；实施时从一致的 r5 集成基线按依赖顺序合并并复核完整 diff。

## Migration Plan

1. 在 r5 集成分支上先整合 `add-dandan-api-proxy-mode` 的既有实现基础和 `cache-bust-plugin-config-page`，再实施本 change；不回写二者的 planning artifacts。
2. 加入可空配置字段、解析辅助逻辑、UI 与集中路由分支，运行确定性回归并完成 Release 构建。
3. 对联合 r5 DLL、配置页资源和源码执行内建 URL 暴露扫描；验证地址仅存在于后端批准常量，且 AppId、Secret、签名不进入日志或发布产物说明。
4. 部署前备份当前 Emby DLL、插件配置和必要的前端脚本，记录备份路径与原始 SHA-256；计算候选 DLL/前端资源校验和。
5. 部署联合 `2.0.1-r5` 候选，重启 Emby，并确认旧有非空 `ProxyCorsUrl` 配置仍默认使用自定义代理；另以新安装/无地址配置确认默认勾选官方 CORS。
6. 完成官方 CORS、自定义 CORS、官方直连三路 live smoke，以及匹配、绑定、XML/ASS、其他提供商与日志检查。
7. 验证通过后准备中文 release notes，明确本 change 与其他 r5 变更为同一联合版本；按既定流程推送并合并。
8. 任一关键检查失败时停止发布，恢复备份 DLL、配置和前端脚本，重启 Emby，并以记录的 SHA-256 确认回滚产物。
