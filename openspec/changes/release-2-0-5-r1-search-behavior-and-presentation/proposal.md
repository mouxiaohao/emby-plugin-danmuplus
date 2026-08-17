## Why

The 2.0.4r2 smart-match workflow still lets one failed provider block otherwise valid automatic matches, exposes noisy TMDB-alias fallback state, and transforms explicit manual-keyword results beyond what the user requested. 2.0.5r1 also needs a small set of presentation, configuration-page, and scoring corrections whose implementation and tests can each be reverted independently.

## What Changes

- Remove the composite-season warning clause `该季包含多个来源或存在未识别区间；` and show `下列卡片仅用于本次下载映射，不会改变Emby 的季归属。` exactly once per applicable Series/Season smart-match result rather than once per Season card; non-applicable results show no such guidance.
- Treat a provider fault as provider-local: keep its public diagnostic, continue ranking completed providers, and allow a completed provider's otherwise valid high-confidence result to proceed through preview and automatic matching. Parent/user cancellation and structural-plan ambiguity remain fail-closed.
- Change only the configuration page's visible heading from `Danmu 配置` to `DanmuPlus 配置`, and point its `源码` link at `https://github.com/mouxiaohao/emby-plugin-danmuplus/tree/main`. Plugin identity, assembly name, configuration route, Emby plugin-list name, update/release URLs, and saved configuration remain unchanged.
- Continue through later eligible TMDB aliases when one alias request faults. When every eligible alias either faults or finishes without an automatically acceptable match, discard the accumulated alias candidate presentation, suppress TMDB-specific browser diagnostics, and expose `重新匹配`; that action searches the unmodified parent Series title directly and applies the target Season's automatic scoring rules.
- Remove the shared smart-match search deadlines of 10 seconds per provider, 30 seconds per interactive operation, and 45 seconds per automatic operation. Preserve explicit user/parent cancellation, the global/per-provider concurrency gates and settlement safety, provider transport safeguards, the 180-second Movie/Episode download deadline, and the seven-day duplicate/replay policy.
- Remove the `0.79` contradictory-season/year evidence cap and remove the restricted fidelity bridge that promotes an eligible `0.85` candidate to the automatic confidence threshold. Ordinary scoring evidence and the standard automatic threshold remain.
- Keep every explicit manual-keyword search on the established optimized discovery path: reject empty or whitespace-only input before any provider call, apply the existing outer trim and provider-owned normalization, retain candidate eligibility, `MergeSources`, server scoring/reasons, and the provider-fair sixty-row `OrderCandidates` window, and keep zero-score Movie candidates available for review. Manual-keyword discovery MUST NOT invoke TMDB aliases, discard candidates for missing an automatic threshold, classify or auto-select a row, start a download, or persist a binding before an evidence-validated explicit selection.
- Refine Season scoring after the ten independently revertible slices: parent-title evidence contributes 60 points, Season-name evidence 20, and an exact known year 20; Episode count remains available to authoritative mapping but contributes no score and cannot block an otherwise valid match. After an authoritative plan is built, show a yellow warning when the verified source contains more Episodes than the target Season's eligible local inventory; do not warn when local inventory is equal or larger, and preserve the existing residual temporary-Season workflow.
- Stamp 2.0.5r1 version metadata and update cumulative documentation after the ten behavior slices are complete.
- Implement l1 through l10 as ten separately revertible slices, each carrying its own focused regression updates; keep the final version/documentation update separate so reverting one `l` does not leave the tree uncompilable or its tests inconsistent. In particular, l6 parent-title rematch, l7 deadline removal, and l10 manual-keyword discovery remain independently revertible; l6 and l10 use separate additive request/result discriminators and do not require symbols introduced by the other slice.

## Capabilities

### New Capabilities

- `manual-provider-search`: Defines the existing optimized, server-scored discovery pipeline and no-automatic-selection contract for an explicitly entered keyword across supported media entry points.
- `plugin-configuration-presentation`: Defines the DanmuPlus-only page heading and canonical main-branch source link while preserving the plugin's compatibility identity.

### Modified Capabilities

- `season-danmu-matching`: Changes provider-failure handling, removes shared search deadlines and two automatic-score overrides, and defines the parent-title automatic fallback after unsuccessful TMDB aliases.
- `smart-match-error-and-presentation`: Removes the repeated composite warning clause, renders the remaining mapping guidance once, and hides TMDB-only diagnostic detail from the browser.

## Impact

- Matching/search backend: `Core/BoundedSearchPolicy.cs`, `Core/SearchOperationRegistry.cs`, `Scraper/CompositeSeasonTargetSetCoordinator.cs`, `Scraper/DanmuMatchSearchEngine.cs`, `Scraper/DanmuMatchScorer.cs`, automatic matching in `LibraryManagerEventsHelper.cs`, and match DTO/controller state in `Model/DanmuMatchResult.cs` and `Core/Controllers/DanmuController.cs`.
- Browser/configuration: `Frontend/DanmuSmartMatch.CustomCssJS.js`, `Frontend/DanmuSmartMatch.RegressionTests.js`, `Configuration/configPage.html`, and configuration resource regressions.
- Verification/versioning: focused C# and Node regressions, `Emby.Plugin.Danmu.csproj`, `Configuration/PluginConfiguration.cs`, `README.md`, and `UPDATE.md`.
- No new runtime dependency, data migration, provider credential change, or provider download/protocol change is planned. Creating these artifacts does not authorize a push, merge, release, or deployment; separately authorized live verification still follows the recorded local-verification and backup gates.
