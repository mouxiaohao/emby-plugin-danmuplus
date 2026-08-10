## 1. r5 集成基线

- [x] 1.1 从一致的最新 r5 集成基线开始，确认 `add-dandan-api-proxy-mode` 的实现基础与 `cache-bust-plugin-config-page` 已按依赖纳入，并记录其他对话中的 r5 变更范围；不得改写这些 change 的 planning artifacts 或遗漏混合工作区修改。
- [x] 1.2 记录本次 Worker 与 Emby 手工成功验证为代理可行性的前置证据，并明确标注它不计入联合 r5 候选构建的回归结果。（证据见既有 `openspec/changes/add-dandan-api-proxy-mode/verification.md`；r5 候选记录仅引用该前置证据，不将其计入 r5 回归。）

## 2. 配置模型与迁移

- [x] 2.1 在 Dandanplay 配置模型中加入可空的官方 CORS 选择字段，并实现统一解析：明确值优先；字段缺失且 `ProxyCorsUrl` 为空时为 true；字段缺失且 `ProxyCorsUrl` 非空时为 false。
- [x] 2.2 更新配置加载与保存边界，使配置页每次保存都持久化明确的 true/false，同时始终保留用户自定义 `ProxyCorsUrl`，且不把官方 CORS 地址写入配置 XML、配置响应或任何输入字段。
- [x] 2.3 为新安装、旧配置缺字段且无地址、旧配置缺字段且有地址、明确 true、明确 false 且地址为空/非空的组合添加确定性迁移与序列化回归。

## 3. 配置页面

- [x] 3.1 在现有代理 API 区域增加“使用插件官方 CORS 地址”复选框，不在标签、说明、HTML、JavaScript、隐藏字段、placeholder 或前端常量中包含实际地址。
- [x] 3.2 实现 UI 状态同步：`UseProxyApi` 关闭时代理来源控件不参与当前路由；代理开启且官方 CORS 勾选时禁用自定义输入；取消勾选时启用输入；切换过程不清空用户自定义值。
- [x] 3.3 更新配置页加载/保存回归，验证解析后的勾选状态、明确布尔值回写、自定义值无损切换，并与 `cache-bust-plugin-config-page` 的版本化页面/控制器标识联合验证。

## 4. 后端路由

- [x] 4.1 仅在 Dandanplay 后端路由实现中加入私有官方 CORS 常量 `https://danmuplus-dandan-proxy.mouxiaohao.workers.dev/cors/`，确保该常量不属于配置模型或可序列化 DTO。
- [x] 4.2 扩展集中请求路由：非代理模式直连官方 API 并本地签名；代理加官方 CORS 使用内建前缀且不解析/发送本地签名；代理加自定义 CORS 使用保存前缀且不解析/发送本地签名。
- [x] 4.3 让 `search`、`bangumi` 与 `comment` 三个既有端点复用同一来源决策、前缀规范化和清理后的错误处理；自定义代理地址为空或无效时显式失败，不回退到官方 CORS 或直连。

## 5. 确定性回归与泄漏断言

- [ ] 5.1 为三个端点逐一断言直连、官方 CORS、自定义 CORS 的精确目标 URL、查询参数、HTTP 行为和认证头集合，证明两种代理路径均不解析或发送本地 Dandanplay 签名。（已覆盖集中路由决策、精确 URL 与代理分支跳过本地凭据解析；仍缺伪 HTTP 逐端点断言实际请求头集合和凭据解析行为。）
- [x] 5.2 添加负向字面量与序列化断言：官方 CORS 地址只能出现在批准的后端常量位置，不得出现在 HTML、JavaScript、生成的配置页资源、配置 XML、配置响应、输入框或测试快照中。
- [ ] 5.3 回归现有标题/年份/季度/集数评分、自动匹配、手动绑定、`search`/`bangumi`/`comment`、XML/ASS、其他提供商、STRM、重复跳过与重试行为，并断言未引入媒体 Hash 或 `/match`。（已覆盖评分、路由构造、既有下载相关实现与 Hash/`/match` 负向扫描；仍缺自动/手动绑定、ASS、STRM、重复跳过和重试的完整可执行回归。）
- [ ] 5.4 扫描自动化测试与应用日志输出，断言不包含 AppId、Secret、签名值或认证请求头，且错误消息可诊断但已脱敏。（已完成自动化源码/输出扫描与脱敏状态摘要；仍缺三路 smoke 后的完整 Emby 应用日志扫描。）

## 6. 联合 r5 构建与发布前备份

Verification note (2026-08-10): the final-process direct-route smoke covered
`search`, `bangumi`, tracked `comment`, and non-empty XML/ASS output. The log
segment contained no configured Dandan credential literals, access token,
signature query, NAS password, or saved custom-proxy literal. Emby itself logs
the `X-Emby-Authorization` header name and non-secret client metadata, so tasks
5.3/5.4 remain unchecked pending their stricter full-matrix assertions.

- [x] 6.1 与其他已确认的 r5 变更联合运行完整回归并构建 `2.0.1-r5` Release DLL；确认版本元数据和缓存版本标识来自同一候选构建，不单独发布本 change。
- [x] 6.2 计算候选 DLL 与相关前端资源的 SHA-256，检查联合 diff 完整包含各 r5 变更且不包含凭据、签名或意外的工作区文件。（候选与扫描结果记录在 `artifacts/2.0.1-r5/VERIFICATION.md`。）
- [x] 6.3 部署前备份 Emby 当前 DLL、插件配置和必要的前端脚本，记录绝对备份路径、原始 SHA-256、服务状态和可执行的回滚步骤。（最终快照与保留的 r4 回滚目标记录于 `artifacts/2.0.1-r5/VERIFICATION.md`。）

## 7. Emby 三路 live smoke

- [ ] 7.1 部署联合 r5 候选到 Emby，重启并确认插件正常加载、配置页无需清理浏览器缓存即可显示新控件，且页面及配置网络响应均不展示官方 CORS 地址。
- [ ] 7.2 使用《葬送的芙莉莲》等动画剧集在官方 CORS 路径完成最小 live smoke，覆盖 `search`、`bangumi`、`comment`、自动/手动绑定以及 XML/ASS 结果。
- [ ] 7.3 使用独立填写的自定义 CORS 路径完成相同 live smoke，并验证取消/重新勾选不会清空自定义地址。
- [ ] 7.4 使用官方直连本地签名路径完成相同 live smoke，确认两种代理路径没有使用本地凭据，同时其他弹幕提供商继续工作。
- [ ] 7.5 检查 Emby 启动与请求日志无新增错误，并确认日志不含 AppId、Secret、签名、认证头或其他敏感信息；记录三路结果但不记录凭据。

## 8. 发布与回滚确认

Verification note (2026-08-10): direct mode was already the saved mode. With
ASS temporarily enabled, one representative Dandan tracked Episode completed
1/1 successfully and produced non-empty XML and ASS; `finally` restored the
saved direct/custom/official-selection semantics and disabled ASS. Task 7.4
remains unchecked because its full automatic/manual and cross-route matrix was
not repeated in this pass.

- [x] 8.1 验证部署 DLL/前端资源 SHA-256 与候选产物一致，并在任何关键检查失败时恢复已记录的 DLL、配置与前端备份，重启 Emby 后用原始 SHA-256 确认回滚。（DLL 命中最终候选；CustomCssJS 目标内容精确匹配且插件本体未改；失败的首次配置保存已从快照恢复并复核原始 SHA-256。）
- [ ] 8.2 在全部联合 r5 验证通过且获得用户确认后，编写中文 release 说明，说明官方/自定义 CORS 选择、迁移规则、UI 隐藏边界、三路回归结果及其他 r5 变更，不公开任何凭据。
- [ ] 8.3 按既定发布流程提交完整 r5 变更、推送 GitHub、合并到 `main` 并更新 `2.0.1-r5` Release；复核源码包、DLL、校验和与中文说明相互一致。
