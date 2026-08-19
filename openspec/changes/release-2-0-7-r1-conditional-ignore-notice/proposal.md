## Why

Smart Match 的 Season 范围摘要目前只要存在“显示/参与匹配”统计，就无条件追加“忽略项不可选择，也不会进入下载。”；即使服务器报告的四类忽略计数全部为零，用户仍会看到这句不符合当前结果的提示。2.0.7r1 需要让提示严格跟随真实忽略项，同时保留既有忽略项安全边界。

## What Changes

- 仅当服务器响应中的 `IgnoredParentZeroEpisodeCount`、`IgnoredOtherSeasonEpisodeCount`、`IgnoredUnknownParentEpisodeCount`、`IgnoredInvalidEpisodeCount` 归一化后的总和大于零时，生成 `只读忽略 N 集（分类）。忽略项不可选择，也不会进入下载。` 这一条完整范围摘要分支；安全提示必须是该明细分支的固定后缀，不得由独立 ignored-total presentation gate 或第二 helper 追加。
- 当忽略总数为零、缺失、无效或负数时，仍可显示“显示 N 集；参与匹配 M 集”的范围摘要，但不得显示忽略项提示。
- 当真实忽略项存在时，`scopeSummaryLine` 一次性返回包含“只读忽略 N 集（分类明细）”及其固定安全后缀的唯一字符串；whole-Series、single-Season、rematch/rebuild 三个 renderer 只消费该字符串，不从 DOM、历史状态、客户端选择或另一提示 helper 推断。
- 保持既有安全语义：忽略项不可选择、不进入复合选择请求、计划映射或下载；本次不改变后端忽略计数、Season ownership、mapping protocol、下载范围或任何持久化数据。
- 将版本标记更新为 2.0.7r1：Assembly 保持 `2.0.7.0`，File 升为 `2.0.7.1`，informational/configuration/TMDB User-Agent 使用 `2.0.7r1`，前端安装标记由 V32 升至 V33，配置缓存 token 为 `2-0-7r1`，mapping protocol 保持 V22；不发布、推送、合并、打 tag 或部署，除非后续另行明确授权。

## Capabilities

### New Capabilities

<!-- None. -->

### Modified Capabilities

- `smart-match-error-and-presentation`: 让忽略项安全提示只在服务器权威忽略计数总和大于零时显示，并在重渲染时严格清除不存在的提示。

## Impact

- 前端：`Frontend/DanmuSmartMatch.CustomCssJS.js` 的 Season 范围摘要渲染及对应回归夹具。
- 后端/协议：复用现有四个只读忽略计数字段；无需新增响应字段、客户端作者化字段、配置或 mapping protocol 版本。
- 版本与文档：`2.0.7.0`/`2.0.7.1`/`2.0.7r1` assembly/file/product identity、配置与 User-Agent、前端 V33/cache token、`README.md` 与累计 `UPDATE.md`。
- 验证：前端有/无忽略项、无效计数、whole-Series/single-Season/rematch 重渲染回归，完整 Release build、相关后端范围回归、严格 OpenSpec、`git diff --check` 与凭据/生成物审计。
