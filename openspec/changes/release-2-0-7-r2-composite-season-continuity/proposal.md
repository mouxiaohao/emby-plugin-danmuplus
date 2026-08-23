## Why

TMDB 同步后，一个 Emby 本地动画 Season 可能已经对应弹幕来源的多个连续正式季；2.0.7 虽能在该本地 Season 内递归匹配这些分段，但动画整剧继续匹配下一个本地 Season 时仍会重新使用 Emby 的本地季号，因而可能重复命中已被合集消费的来源季。2.0.7r2 需要在同一弹幕站点内安全延续上一相邻本地季的权威终结逻辑季号，同时保留现有 TMDB 别名搜索能力。

## What Changes

- 仅对用户主动发起、以动画 Series 本身为目标并枚举全部正季的整剧匹配启用跨本地季连续链；动画判定复用服务器现有动画类型识别。显式单季、按季度上下文缩窄的 Series 请求、手工关键词单季，以及所有后台路径均不执行本功能。
- 不写死 S01/S02：当一个本地季的完整权威计划包含多个来源逻辑 Season 时，它会激活连续链；紧邻的下一本地季以其终结逻辑季 M+1 为初始目标。连续链激活后，只要每一季都完整、当前且紧邻，就继续把本季终结逻辑季传给下一季，即使中间某个本地季只匹配一个来源逻辑 Season。
- 连续链同时锁定首次形成多季合集的弹幕站点 `ProviderId`。后续延续季只从该站点搜索和选择候选，不允许跨站回退；不同来源季度使用不同 CandidateId/MediaId 是正常且必要的，不会复制上一季的媒体 ID、集映射或本地绑定。
- 将派生的初始逻辑季号和站点锁同时用于普通候选、TMDB 别名候选、自动选择、候选证据以及 2.0.7 余集递归的 active logical Season 起点。验收样例中，本地“爱书的下克上” S1 按现有站点优先级在弹弹Play经 TMDB 别名“小书痴的下克上”连续匹配来源第一、二、三季，本地 S2 随后仅在弹弹Play内按来源第四季搜索并自动匹配。
- 连续性只接受动画整剧请求中紧邻、正季号、完整且当前 generation 的服务器权威结果；缺季、Season 0、取消、超时、歧义、部分计划、陈旧证据、浏览器字段或站点不一致均不得制造或修改偏移。连续链已激活但锁定站点无法提供可信结果时，该延续季保持未匹配，不回退其他站点或本地季号。
- 未激活连续链的整剧季仍保持现有全站候选搜索和站点优先级；同一逻辑季内无论有任意 N 个 Part 都不会改变终结逻辑季号或激活跨季站点锁。比如本地 S1=Season 1 Part 1+Part 2 时，本地 S2 仍按 Season 2 匹配，不能推成 Part 3 或 Season 3。
- 将版本更新为 2.0.7r2：保持 AssemblyVersion 2.0.7.0 与 mapping protocol V22，递增 FileVersion、informational/config/User-Agent 和前端 cache marker，并补充累计 README/UPDATE 记录与回归证据。
- 非目标：不改变 Emby 的真实 Season/集号归属，不加入“爱书/小书痴”等硬编码同义词，不为显式单季预计算上一季，不让媒体导入、item-added、retry/replay 或其他无人值守路径执行跨季连续匹配，也不从本地 ProviderId、浏览器 payload 或历史下载反推连续性。

## Capabilities

### New Capabilities

- 无。

### Modified Capabilities

- `season-danmu-matching`: 增加动画整剧主动匹配中相邻本地正季之间的逻辑季号与来源站点连续链，并要求 continuation-adjusted 目标季在锁定站点内继续执行既有 TMDB 别名发现和评分。
- `parent-season-aware-episode-mapping`: 增加跨目标季连续性证明、来源站点锁、候选证据和重建/fingerprint 门禁，禁止浏览器、陈旧 generation、不完整前序计划或不同站点作者化逻辑季偏移。

## Impact

- 后端：动画 Series 整剧入口、`CompositeSeasonTargetSetCoordinator` 的连续状态、`DanmuController` 的 whole-Series 编排、`DanmuMatchSearchEngine` 的显式目标季号/站点上下文、候选证据与复合计划重建。
- 模型：新增仅服务器可见的前序连续性、初始/终结逻辑季号和 required ProviderId 证明；现有公开 SeasonNumber 继续表示 Emby 本地季号，V22 请求模型不新增浏览器可写字段。
- 测试：新增动画门禁、整剧限定、通用 N−1→N 连续链、任意 Part 数不参与季号计算、同站强制、缺季/失败/陈旧安全门禁以及“爱书的下克上”TMDB 别名 S1=来源 S1-S3、S2=来源 S4 的端到端回归；显式单季和后台路径验证零前序搜索。
- 发布：更新 2.0.7r2 版本、README、UPDATE 与独立审阅包；现场预览或部署仍需用户另行明确授权。
