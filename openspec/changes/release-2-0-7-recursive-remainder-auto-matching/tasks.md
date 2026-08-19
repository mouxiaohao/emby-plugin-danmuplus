## 1. 基线、隔离工作树与范围冻结

- [x] 1.1 在任何实现前重新解析并记录 `2.0.6r2` `develop` checkpoint `07fbb408d54ee1b6201d4f217122079070527c5a`、其祖先关系、当前分支/HEAD 和工作树状态，确认实现比较基准不是后续工作树行为。
- [x] 1.2 从该精确 checkpoint 建立独立实现分支和隔离 worktree，验证主检出目录及用户维护的 `AGENTS.md`、未跟踪文件和无关修改均未被切换、覆盖或暂存。
- [x] 1.3 顺序运行 2.0.6r2 的后端主回归、复合季/目标季范围/稀疏对齐/候选证据、搜索质量、取消以及前端 Smart Match 基线检查，并在改代码前记录所有既有失败。
- [x] 1.4 冻结生产代码、测试、2.0.7 版本/文档和本 change 工件的 changed-file allowlist；明确排除 Provider 适配器、真实 Emby Season 归属、持久化 schema、既有绑定、重试/回放语义及无关文件。

> Baseline record (2026-08-19): `HEAD` and the implementation comparison baseline are both `07fbb408d54ee1b6201d4f217122079070527c5a` on isolated `codex/release-2.0.7-remainder-auto-matching`; the only pre-existing worktree changes were this change's untracked OpenSpec artifacts. The frozen implementation allowlist is limited to remainder orchestration/title-analysis/search-context/evidence and presentation files under the existing matching/plan path, their focused regression fixtures, version/resource metadata, `README.md`, `UPDATE.md`, and this change's artifacts. It excludes Provider adapters, real Emby Season ownership, persistent schema/saved bindings, retry/replay semantics, and unrelated files. Sequential Debug baseline suites passed; no Release build was run. The official Release `--no-build` test launcher could not start because this fresh worktree had no prebuilt Release executable; this is recorded as an environment prerequisite, not a product-test failure. A transient Fody output-file lock during sequential Debug builds cleared after retry; all affected suites then passed.
- [x] 1.5 在任何生产代码修改前，对主规格、活跃 `fix-sparse-episode-number-alignment` 和本 change 做逐 Requirement 三方审计：确认本 change 的 `season-danmu-matching / Shared manual and automatic matching behavior` 已完整合并 sparse 坐标、alignment parity、S00 搜索前跳过和交互余集递归语义，并记录它与 `parent-season-aware-episode-mapping` 的 `ExplicitAnchor`、窗口/前沿和 fingerprint 依赖/同步顺序；禁止以后用任一 delta 覆盖另一份变更。

> Audit record (2026-08-19): Luna confirmed sparse implementation ancestors `cd2d438` and `680d31f` are contained by baseline `07fbb408`; existing `CompositeSeasonPlanner`/`CompositeSeasonMatchService`, `ExplicitAnchor`, V22, and fingerprint rebuild fence are reusable. This delta's shared matching Requirement already merges sparse coordinates, alignment parity, S00 pre-search skip, and interactive recursion; retain the `parent-season-aware-episode-mapping` ExplicitAnchor/window-frontier/fingerprint dependency and synchronize deltas by Requirement merge, never last-writer-wins replacement.

## 2. 先冻结 Part、标题族与状态机测试契约

- [x] 2.1 在生产解析器实现前新增 Part parser 表驱动测试，覆盖 `Part 2`、`part:.2`、`Part II`、`第二部分`、大小写/NFKC/空白/标点等价，以及零、负数、非法罗马数字、裸数字、`部`、`篇`、`cour`、Episode/Season 标记拒绝。
- [x] 2.2 在生产标题族实现前新增 title-family 测试，覆盖去除父剧名和重复公共字符串但保留 Part token、现有标题资格下限、跨标题通道隔离，以及 S1 父剧名直出后接“第1季/第一季/Season 1 + Part N”或直接 `Part N` 的兼容路径。
- [x] 2.3 在生产状态机实现前新增 `Selected`/`NotApplicable`/`Rejected`/`Unknown` 转移测试，证明只有 `NotApplicable` 可进入下一层，Part gap/歧义/畸形、详情不完整、取消和 Provider 失败均不得降级。
- [x] 2.4 先新增显式 Season 冲突测试，证明每个标题通道中明确且可解析的其他正季号在 Part、元数据和逻辑季唯一性计算前被排除，冲突或不可安全解析的显式 Season 信息 fail closed。
- [x] 2.5 先新增三级优先级契约测试，冻结“连续 Part → 相似标题年份/集数 → 下一逻辑季”的顺序、相同稳定来源去重、不同来源歧义拒绝和完整 canonical 候选覆盖要求。

## 3. 纯标题分析与权威候选资格

- [x] 3.1 从 `DanmuMatchScorer` 提取可复用的 loose normalization、identity-bearing title、父剧名/季名 residual、本地化 ordinal 和显式 Season 冲突原语，并用回归证明普通 Season/Movie 评分及 60/20/20 权重未改变。
- [x] 3.2 实现与 Season parser 分离的有界 `PartTitleParser`，仅在 `part`/`部分` marker 关联严格正阿拉伯、罗马或中文序数时返回统一整数，不把 `第N季` 或任意裸序数当 Part。
- [x] 3.3 实现 title-family 比较和 S1 parent-only 例外，按规范移除父剧身份与重复核心、保留 Part residual，并在任何 Part/元数据决策前执行不同正季号硬排除。
- [x] 3.4 实现服务器权威候选详情验证，使用现有 source projection 校验非空且唯一的 EpisodeId/CommentId 和稳定 MediaId；仅验证后 Episode 数大于 3 的来源可参与余集自动选择，`EpisodeSize` 只作发现提示。
- [x] 3.5 实现按 `target SeasonId + (providerId, candidate lookup id)` 键控的 operation-local 异步详情缓存，同时缓存成功与终止失败；详情解析后另以 `(providerId, canonical MediaId)` 去重，保证每次预览内同一候选最多请求一次。
- [x] 3.6 让余集资格筛选始终读取 `CanonicalCandidates` 而非 60 行展示投影，并证明浏览器候选 payload、Provider-fair 排序和现有候选详情只读接口没有被扩大或用于作者化自动决策。

## 4. 三级余集服务与虚拟逻辑季搜索上下文

- [x] 4.1 定义 provider-neutral 余集协调服务的输入、闭合 operation policy、四态 tier 结果和内部终止原因；保持 `CompositeSeasonPlanner` 纯净，并禁止该服务写文件、绑定 ProviderId 或修改 Emby 对象。
- [x] 4.2 实现连续 Part tier：无 Part 首段视为 Part 1，后续以最后已选 Part 为权威，只接受唯一稳定来源的精确下一整数；任一适用但缺号、跳号、畸形或歧义结果必须 `Rejected` 并终止本轮。
- [x] 4.3 实现相似标题元数据 tier：仅在无合格 Part marker 时，按余集首集可信年份和当前 maximal suffix run 集数选择唯一 exact tuple；过滤后仅一项且年份匹配、验证集数不符时仍选择并携带权威黄色告警来源。
- [x] 4.4 为搜索/评分 API 增加显式 expected logical Season number 和 in-memory suffix descriptor，使用权威父剧名、整个当前 suffix run 及其第一集年份，而不复用真实 `contextItem.IndexNumber`、制造 Season 实体或改变 Provider detail 的真实本地 Season 上下文。
- [x] 4.5 实现下一逻辑季 tier：仅在前两层均为 `NotApplicable` 时按父剧名 60 + 递增季号 20 + 余集首集精确年份 20 评分，硬排除其他明确季，只接受唯一且 `>= 0.90` 的结果，并禁止 TMDB alias 0.80 门槛参与。
- [x] 4.6 实现递归编排：每轮只处理首个可证明连续的 maximal suffix run，Part 成功不增加 logical Season，逻辑季成功仅增加一次；维护 lookup/stable used-source 集合并以上一轮权威重建结果驱动下一轮。
- [x] 4.7 以初始未匹配 eligible ItemId 数作为成功轮数上界，并在每轮提交前验证 generation 当前、未匹配数严格减少、至少新增一个映射、既有映射不丢失/漂移且未复用来源；失败时丢弃本次尝试并保留最后有效计划。
- [x] 4.8 将无候选、门禁拒绝、歧义、取消、Provider 失败、超时、详情/覆盖不完整、证据过期和 no-progress 统一为安全停止，禁止在 `Unknown`/`Rejected` 后继续低优先级选择或后续递归。

## 5. 选择模型、证据、ExplicitAnchor 与权威重建

- [x] 5.1 为递归来源增加闭合 origin（`remainder-part`、`remainder-metadata`、`remainder-metadata-count-warning`、`remainder-logical-season`）和内部 decision kind；只增加服务器响应型 count-warning 状态，不接受浏览器提交的评分、Part、年份、集数或告警作为证据。
- [x] 5.2 将每个自动来源写成普通 `DanmuCompositeSeasonSelection`：使用当前 generation/protocol、`AlignmentIntent = ExplicitAnchor`、当前 suffix 首个 ItemId、完整 suffix 行数、首个已验证 source EpisodeId 及其观测编号，并注册新的 target-bound `SelectionEvidenceToken`。
- [x] 5.3 扩展服务器证据以覆盖目标 Season、generation、unmatched run、lookup/stable source identity、完整已验证 source inventory、选择层级和该层决策事实；确保 Part、metadata warning 和 logical-Season 各自具备可复核字段。
- [x] 5.4 每次追加 compact selection 后只调用现有 `BuildCompositePlanAsync` 权威重建，复核所有来源详情、CommentId、anchors、considered ItemIds、resolved modes、精确映射和来源唯一性，不直接插入浏览器映射或绕过 sparse alignment。
- [x] 5.5 将自动 selection order/origin/evidence、验证来源清单、anchors、considered local ItemIds、warning provenance 与最终 exact mappings 纳入现有 generation/fingerprint 重建链；若无需客户端作者化字段则保持 mapping protocol V22。
- [x] 5.6 在预览确认、下载 preflight、排队执行和 metadata write 前重建并比较当前 generation/fingerprint；来源身份、库存、anchor、CommentId、告警依据或映射变化时判 stale 并保证 XML/metadata 零写入。

## 6. Controller 交互策略与后台非递归接线

- [x] 6.1 在 `DanmuController`/共享 Season operation 中显式传递 `InteractiveRecursive` 或 `BackgroundNonRecursive`，不得以可空 Boolean、调用方猜测或浏览器字段决定余集递归权限。
- [x] 6.2 将用户主动单 Season 预览（含真实显式 S00）在首段由自动确信或人工确认并权威应用后接入同一余集服务，保持首段未成功时的既有错误/取消行为。
- [x] 6.3 将用户主动 whole-Series 的每个正季号接入同一共享 Season operation 和余集服务，同时在任何搜索前继续排除 S00 与未知季号，保证与相同 Season 的单季入口结果一致。
- [x] 6.4 保持 manual-keyword discovery 只展示、无自动选择；仅在用户显式确认初始候选后启动递归，并证明人工/自动选中同一首段时产生相同余集决策和映射。
- [x] 6.5 将正季媒体库导入、item-added、自动下载、retry、replay 及所有无人值守入口固定为 `BackgroundNonRecursive`，证明首段有余集时 provider 余集搜索/详情/绑定/写入调用均为零；并证明 whole-Series 与所有无人值守 S00 在 provider 搜索、选择、规划、绑定、下载和 metadata write 前即被跳过。

## 7. 响应模型与前端静默/黄色展示

- [x] 7.1 在 mapped composite group 上增加仅由服务器权威重建派生的 additive response-only Episode-count-mismatch Boolean，旧前端可忽略，新前端不得从候选 `EpisodeSize` 或 DOM 计数推断该值。
- [x] 7.2 在 whole-Series 和 single-Season 共用渲染路径中，对 sole same-year count-mismatch 的已匹配 group 使用既有黄色 Season 集数提示样式且只显示一次；来源较少/较多均不阻止绑定或制造本地 Episode。
- [x] 7.3 将余集 `NotApplicable`、`Rejected`、`Unknown` 或递归耗尽呈现为已确认 prefix 加普通临时未匹配 group，不切换为 `no_match`/`failed`，不弹顶层“匹配失败”，也不在未匹配卡片显示评分。
- [x] 7.4 保持初始 Season 尚无任何权威 segment 时的既有错误与诊断展示；仅对首段成功后的余集停止应用 silent partial-state 规则。
- [x] 7.5 在 rematch、重新预览、generation 更新和结果销毁时清除旧 warning/临时状态，并保证重建后的权威 Boolean 为 false/缺失时不会残留或重复黄色提示。

## 8. 后端确定性与安全回归

- [x] 8.1 添加连续 Part 2→3→4 递归集成夹具，组合阿拉伯/严格罗马/中文表达及 `part:.2` 标点，验证 exact-next、稳定来源去重、确定顺序和递归耗尽。
- [x] 8.2 添加安全门禁夹具，覆盖验证来源 0/1/2/3 集全部排除、未知搜索集数经详情验证为 4 集后可参与、空/重复 EpisodeId 或 CommentId 以及失败详情导致安全停止。
- [x] 8.3 添加 JOJO 无 Part 夹具，覆盖唯一同年同集数确信绑定、多个相同 tuple 歧义、sole same-year 不同集数仍绑定并产生 warning，以及 sole 年份缺失/不同不绑定。
- [x] 8.4 添加 Frieren 夹具：真实 S1 E1-E28 后的 suffix 从 E29/2026 构造 logical S2，按 60/20/20 达到 0.90 后锚定 source E1；再覆盖后续 suffix 使用其首集年份和 logical S3、低于 0.90 与首集无年份停止。
- [x] 8.5 添加标题/优先级安全夹具，覆盖 S1 parent-only 变体、不同明确季排除、Part 2 歧义、Part 2 后仅 Part 4、畸形 Part、完整覆盖未知以及所有 `Rejected`/`Unknown` 均不得降级。
- [x] 8.6 添加取消、Provider exception/timeout、部分 Provider 覆盖、证据过期和详情缓存失败夹具，证明取消前首段未成功沿用顶层行为，首段后取消保留 prefix、停止递归且不产生确信绑定。
- [x] 8.7 添加入口/范围夹具，证明 whole-Series 与 single-Season 对同一正季一致，显式单季 S00 可递归，whole-Series/无人值守 S00 及 foreign/unknown-parent Episode 在搜索、计数、映射、临时组和写入前被排除。
- [x] 8.8 扩展 sparse/ExplicitAnchor 回归，覆盖 local E29→source E1、缺失 local E30 时 E31→source E3、编号不可靠时整段 positional、不同来源窗口边界、无混合模式且自动段不继承前一来源 offset。
- [x] 8.9 添加 progress/source-uniqueness/fingerprint 回归，覆盖 alias 行解析为同一 stable source、重复已用来源、零新增映射、既有映射漂移、source inventory/CommentId/anchor/warning 变化，以及所有 stale/no-progress 情况零下载与零 metadata write。

## 9. 前端与既有范围回归

- [x] 9.1 添加前端夹具，验证 count-mismatch 已匹配 group 恰有一个本地化黄色提示，count equal/非权威候选 metadata 无提示，rematch/rebuild 不残留旧提示。
- [x] 9.2 添加前端夹具，验证 no candidate、Part ambiguity、Provider/detail failure、取消和递归耗尽均保留已匹配卡片与临时未匹配卡片，且无顶层失败/banner/popup/未匹配评分。
- [x] 9.3 添加协议/状态夹具，验证 response-only warning 不会回传为规划证据、V22 selection/fingerprint 契约保持一致、旧前端可忽略字段，并冻结仅在新增 client-authored planning field 时才同步升级协议和拒绝旧 draft。
- [x] 9.4 顺序运行完整后端、复合 planner、候选 detail/evidence、搜索/scorer/TMDB alias、目标季范围、Season 0、自动导入、retry/replay、下载 stale fence 与完整前端 Smart Match 回归，确认无并行 .NET 输出锁且所有既有兼容策略不回退。

## 10. 2.0.7 元数据、本地验收与变更整合

- [x] 10.1 将 assembly/file version 更新为 `2.0.7.0`、informational/configuration version 与 TMDB alias client User-Agent 更新为 `2.0.7` / `DanmuPlus/2.0.7`，同步 `TmdbAliasTests` 的版本断言，并将前端安装/cache marker 从 V31 递增；保持插件 identity、configuration schema、保存绑定和 mapping protocol V22，除非 5.5 的契约审计证明必须同步升级两端。
- [x] 10.2 在 `UPDATE.md` 增加累计的 2.0.7 说明并更新 README 当前版本/余集行为，保留全部历史记录、安装兼容信息、Smart Match 演示图片及其引用，不删除或重排用户维护的展示资源。
- [x] 10.3 执行一次无竞争进程的 clean Release build，记录 DLL/CustomCssJS 对应版本、大小和 SHA-256，并验证构建产物来自精确基线上的受审实现 worktree。
- [x] 10.4 在任何 delta spec 同步或 change 归档前，对主规格、`fix-sparse-episode-number-alignment` delta 和本 2.0.7 delta 做三方逐 Requirement 合并审计，保留稀疏窗口/前沿、`ExplicitAnchor`、来源 provenance/stale fence 与递归证据/进度全部语义，解决冲突后分别严格验证且禁止 last-writer-wins 覆盖。
- [x] 10.5 运行 `openspec.cmd validate release-2-0-7-recursive-remainder-auto-matching --strict`、相关 `fix-sparse-episode-number-alignment` 严格验证、最终 status 和 delta/main coherence 检查，并记录所有通过结果或未解决阻塞。
- [x] 10.6 运行 `git diff --check`、changed-file allowlist/范围审计、生成物/归档禁止项检查及 credential scan；不得打印或纳入 TMDB key、Provider 凭据、Authorization/header、签名、NAS 路径或其他私密部署数据。
- [x] 10.7 由 Sol high 对最终 diff、任务/spec/design 一致性、三级无降级状态机、首段 Provider 锁、交互/后台边界、source uniqueness、稀疏对齐、取消/partial-state、零写 stale fence 和验证证据做最终审查并关闭所有阻塞发现。

> Final review record (2026-08-19): Sol high returned PASS with no P0/P1 findings after the Provider-lock repair; the Provider-lock evidence/rebuild/fingerprint chain remains closed and no prior safety boundary regressed.

> Final review record (2026-08-19, title-family repair): Sol high returned PASS with no remaining P0/P1 findings; the tightened title-family/Part boundary is coherent with the existing Provider-lock and evidence/rebuild/fingerprint chain, so task 10.7 is closed for this corrected local build.

## 11. 本地交付与另行批准的现场步骤

- [x] 11.1 组装仅供本地审阅的 2.0.7 DLL/CustomCssJS 配对、校验和、累计说明与验证记录，复读版本/hash/禁止项并保持本地；不得据此自动 push、merge、tag、发布或部署。
- [x] 11.2 将“本地实现与验收已完成”和“live Emby preview/部署尚未获批”分开记录；未获得现场授权时把现场步骤标记为 deferred/no-op，而不是把未授权部署当作实现完成前置或擅自改变外部状态。

> Validation record (2026-08-19): the isolated worktree completed a clean Release build, all listed deterministic backend/frontend suites, strict validation for this change and `fix-sparse-episode-number-alignment`, scope audit, and the local review package. The user had explicitly authorized live preview and deployment in this task, but that local validation stage had not performed either external action. Tasks 11.3–11.5 therefore remained pending at that checkpoint; no push, merge, tag, Release publication, file replacement, restart, preview, download, or metadata write was performed there.
- [x] 11.3 在执行任何 live Emby preview 前另行取得并记录用户明确批准；获批后仅对用户库中实际存在的安全夹具执行只读 MatchPreview，代表性覆盖同站 Part、JOJO metadata continuation、Frieren logical Season 与 silent partial/unmatched。取消、后台非递归、stale/零写门禁以及没有真实现场夹具的黄色 warning 由确定性回归验收，不得为凑现场覆盖而触发危险路径；除非另有授权不得确认、下载、绑定、导入、启动后台任务或写 metadata。
- [x] 11.4 在替换 Synology 文件、重启 Emby、部署、回滚、推送、合并、打 tag 或发布 Release 前分别取得所需明确批准；preview 批准不得自动扩张为正式部署或发布批准。
- [x] 11.5 若正式部署获批，先备份并校验现有 2.0.6r2 DLL/CustomCssJS/config 配对及 owner/mode，再原子替换已审配对、重启并读回 health/version/cache marker/protocol/hash/代表性预览；失败时恢复备份并复验，未获批则保持 no-op。

> Initial live record (2026-08-19): the reviewed pre-Provider-lock 2.0.7 pair was backed up/deployed atomically and read back healthy with plugin version 2.0.7.0, frontend V32, protocol V22, matching hashes, unchanged Danmu configuration, and clean startup logs. Read-only MatchPreview proved Frieren 28+10 logical-Season recursion, but exposed a cross-Provider ambiguity defect for JOJO: `星尘斗士` had one same-Provider `埃及篇` continuation plus an equivalent tuple from another Provider, while `石之海` had same-Provider Part 2/3 rows plus another Provider's combined release. The user therefore refined the contract to lock all remainder rounds to the first authoritative segment's Provider. No download, binding, import, background task, or metadata write was performed; final live acceptance remained pending at that point.

## 12. 现场反馈：首段 Provider 锁与重新交付

- [x] 12.1 将首段权威来源的 Provider identity 冻结为 operation-level lock，在初始 canonical pool、fresh logical-season search、详情解析、Part applicability、metadata tuple/警告唯一性及每轮递归前排除其他 Provider；同站无结果时静默停止且不得跨站回退。
- [x] 12.2 将 Provider lock 纳入服务器余集证据、clone/validation、selection fingerprint、Build 重建和 stale/零写门禁；证明浏览器不能作者化该锁，首段或任一余集 Provider 漂移时预览/下载/metadata execution 均 fail closed。
- [x] 12.3 新增确定性回归：JOJO `星尘斗士` 的跨站同 tuple 不再制造歧义、同站双 tuple 仍拒绝；`石之海` 忽略跨站 38 集合并项并按同站 Part 2→3 递归；logical-season fresh search 只接受首段同站；锁定站点无候选时不降级到另一站。
- [x] 12.4 在完成全部现场反馈修正（含 13.x）后，顺序运行受影响回归、完整主回归、clean Release build、严格 OpenSpec/diff/allowlist/credential 审计，重新生成并哈希审阅包；由 Sol 关闭阻塞后，使用既有明确授权和已验证回滚材料原子覆盖部署，并只读复测 JOJO S2/S5、Frieren、health/version/V32/V22/hash/config，仍禁止下载、绑定和 metadata 写入。

## 13. 现场反馈：Part 不得制造标题族身份

- [x] 13.1 将标题族资格与 Part ordinal 解析彻底分离：当上一来源存在非父剧 identity core 时，候选必须真实共享该 core，禁止因合法 Part marker 把 candidate core 覆盖成 last core；仅在 S1 首段所有权威标题通道都没有非父剧 core 时允许 parent-only fallback，generic parent alias 不得桥接不同篇章。
- [x] 13.2 新增确定性回归：`星尘斗士` 不得接 `石之海 Part.2/Part.3`，但可经 metadata 接同站 `星尘斗士 埃及篇`；`星尘斗士 Part.2` 与 `Parent FooPart2` 仍可用；mixed parent aliases 不得放宽；parent-only fallback 仅允许 logical S1，S00/S2 必须拒绝而带真实 non-parent core 的 S2 Part 仍可用；现场错误 48/48 允许先解析同站权威详情，但 evidence register、Build 与 commit 必须全部为零。

> Final live record (2026-08-19): the title-family-corrected DLL was backed up and deployed atomically under the user's existing explicit authorization while the byte-identical V32/V22 CustomCssJS XML remained untouched. Health, plugin 2.0.7.0, hashes, configuration, ownership/modes, and startup logs passed. Read-only previews passed JOJO S2 as `星尘斗士` 24 + same-Provider `埃及篇` 24, JOJO S5's real 24-Episode inventory as `石之海` 12 + same-Provider Part 2 12 with no Part 3/combined-source overreach, and Frieren as 28 + logical Season 2 10 using 2026 and score 1.0. All had no unmatched run or warning. Live acceptance did not invoke confirmation, cancellation, download, binding, import, background execution, or metadata writes; cancellation/background/write fences plus warning and silent-unmatched behavior remain covered by deterministic regressions. No push, merge, tag, Release publication, or archive occurred.
