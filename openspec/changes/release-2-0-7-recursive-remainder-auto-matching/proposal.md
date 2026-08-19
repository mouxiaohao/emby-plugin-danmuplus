## Why

Emby 的一个本地 Season 可能连续包含同一上游作品的多个 Part，或包含后续正式季；当前 Smart Match 在首段成功后只把余集留下为临时未匹配组，用户仍需逐段手动搜索和确认。2.0.7 需要在保持保守门禁和可重建证据链的前提下，自动识别可信的后续段并递归处理余集。

## What Changes

- 以 `2.0.6r2` 的 `develop` checkpoint `07fbb408d54ee1b6201d4f217122079070527c5a` 为开发基线，仅在用户主动触发的交互式 Season Smart Match 中启用余集自动匹配。
- 首段匹配成功后，把该权威来源的 Provider 固定为整条余集递归链的来源站点；后续 Part、元数据和逻辑季搜索均先排除其他 Provider 候选，跨站同年同集数或合并结果不得参与唯一性/歧义判断，也不得在同站无结果时自动回退到另一站。
- 在同站完整 canonical 候选集中排除已绑定来源、明确属于其他季的标题和集数不明或不超过 3 集的候选，再按固定优先级递归选择余集来源：
  1. 先以首段/上一段真实的非父剧标题核心证明候选属于同一标题族，再在残差中识别 `part`/`部分` 及阿拉伯、罗马或中文数字，并只接受下一连续 Part；Part 标记只能提供序号，不能制造标题族身份；
  2. 对相似但无 Part 标记的候选，接受年份和集数均匹配且唯一的结果；若过滤后仅剩一个候选，年份匹配但集数不匹配，仍可绑定并显示既有黄色集数警告；
  3. 前两类均失败时，把剩余本地集构造成下一逻辑季，使用余集首集年份、父剧名和递增季号按 60/20/20 评分，达到 90 分后自动选择。
- 第一季标题比较仅在首段全部权威标题通道都没有非父剧身份核心时兼容父剧名直出，以及后续标题为“父剧名 + 第1季/第一季 + Part N”或“父剧名 + Part N”的形式；若首段已有“星尘斗士”等身份核心，parent-only alias 不得把“石之海”等其他篇章提升为同族。
- 每个自动选择都以服务器验证过的候选证据和 `ExplicitAnchor` 逐集映射加入复合计划；证据和指纹必须闭合记录首段 Provider 锁并在重建时拒绝 Provider 漂移，递归还必须去重来源、缓存详情请求、限制深度，并要求每轮未匹配集数严格减少。
- 预期无候选、门禁拒绝、歧义或递归耗尽时保留已确认映射及临时未匹配组，不显示顶层“匹配失败”或错误弹窗；取消、超时、Provider 失败或信息不完整同样停止递归且不得提升为确信匹配。
- 非目标：媒体库导入、事件重试及其他无人值守正季路径继续保持非递归，whole-Series 和所有无人值守路径继续在搜索前跳过 Season 0；不改变 Emby 的真实 Season 归属，不写入复合 Season 级插件 ProviderId，不放宽手动绑定、下载重建、稀疏集号或现有 Provider 兼容策略。

## Capabilities

### New Capabilities

<!-- None. This change extends the existing season matching, composite mapping, and presentation contracts. -->

### Modified Capabilities

- `season-danmu-matching`: 增加交互式首段成功后的 Part、唯一年份/集数以及下一逻辑季三级余集自动选择和有界递归规则。
- `parent-season-aware-episode-mapping`: 增加余集自动来源的显式锚点映射、权威选择证据、进度/去重门禁及临时未匹配保留规则。
- `smart-match-error-and-presentation`: 增加余集自动匹配失败的静默未匹配展示，以及唯一年份候选集数不符时的黄色提示规则。

## Impact

- 后端：`DanmuController` 的交互式 Season 预览/复合计划入口、`DanmuMatchSearchEngine` 的虚拟季搜索上下文、`DanmuMatchScorer` 的标题残差与季号冲突复用，以及新增的余集决策服务。
- 模型与协议：复合选择需要保留自动来源、候选证据、`ExplicitAnchor` 和可选集数不符警告；下载阶段仍必须通过服务器重建与计划指纹校验。
- 规格整合：2.0.7 的同名 Season 行为 Requirement 已合并活跃 `fix-sparse-episode-number-alignment` 的稀疏坐标、对齐一致性和 S00 搜索前跳过语义；实现及后续同步/归档均须保持该语义并审计变更顺序。
- 前端：复用临时未匹配组和黄色集数警告，不新增顶层失败状态；2.0.7 版本与缓存标记随实现更新，映射协议仅在字段契约确有变化时递增。
- 验证：扩展复合季、JOJO/Frieren/石之海、同站候选门禁、标题评分、目标季范围和前端回归；执行 Release build、确定性候选顺序/阈值检查、严格 OpenSpec 验证和 `git diff --check`。
