# 2.0.5r1 commit and independent-revert map

This record is local verification evidence for change
`release-2-0-5-r1-search-behavior-and-presentation`. It intentionally contains
no credentials, authenticated payloads, private access details, or deployment
commands.

## Baseline

- Implementation branch: `codex/2.0.5r1-matching-behavior`
- Exact baseline: `f8a4356537dcf0c8f913bb970bb2bcdc689096fd`
- Initial implementation-worktree scope: only
  `openspec/changes/release-2-0-5-r1-search-behavior-and-presentation/` was
  untracked; no business source was modified.
- Original `develop` checkout: pre-existing dirty user worktree, explicitly out
  of scope and never used for implementation writes.

## Baseline regression evidence

| Check | Result | Note |
| --- | --- | --- |
| `node Frontend/DanmuSmartMatch.RegressionTests.js` | PASS | Frontend smart-match baseline |
| Main backend regression executable | PASS | Existing compiler warnings retained |
| `RegressionTests/BoundedSearchPolicy` | PRE-EXISTING FAIL | Its source-text assertion accepts only a two-argument `SearchMergedAsync(keyword, cancellationToken)` spelling, while baseline Bilibili uses the cancellation token as the second argument of the typed three-argument overload |
| `RegressionTests/R3SearchQuality` | PASS | Search-quality baseline |
| `RegressionTests/TitleFidelity` | PASS | Fidelity baseline |
| Main backend `--tmdb-alias` | PASS | TMDB alias baseline |
| Main backend `--seven-day-replay` | PASS | Seven-day replay baseline |

The bounded-search failure was observed before any 2.0.5r1 business-source
edit. After l3, test-only commit `419b730a9c874d3f98e3940769d3bbc946192915`
made the source assertion accept the existing typed three-argument overload
without weakening its second-argument cancellation-token check. The complete
bounded-search regression then passed.

## Per-slice staging allowlists

Each l commit may contain only the listed production area and its focused tests.
OpenSpec progress, this record, version metadata, cumulative documentation,
packages, `bin`/`obj`, logs, and unrelated files stay out of l1-l10 commits.

| Slice | Production allowlist | Focused-test allowlist |
| --- | --- | --- |
| l1 | `Frontend/DanmuSmartMatch.CustomCssJS.js` | `Frontend/DanmuSmartMatch.RegressionTests.js` |
| l2 | `Frontend/DanmuSmartMatch.CustomCssJS.js` | `Frontend/DanmuSmartMatch.RegressionTests.js` |
| l3 | `Model/DanmuMatchResult.cs`, `Scraper/DanmuMatchSearchEngine.cs`, `Core/Controllers/DanmuController.cs`, `LibraryManagerEventsHelper.cs`, frontend smart-match source | Main backend/R3/bounded/Bilibili fixtures and frontend regression |
| l4 | `Configuration/configPage.html` | Configuration resource assertions in `RegressionTests/Program.cs` |
| l5 | `Configuration/configPage.html` | Configuration resource assertions in `RegressionTests/Program.cs` |
| l6 | l6-owned state/request fields in match DTO/controller/search engine and frontend smart-match source | TMDB alias, main/R3/candidate-evidence and frontend fixtures |
| l7 | `Core/BoundedSearchPolicy.cs`, `Core/SearchOperationRegistry.cs`, search/composite coordination and only affected caller wrappers | Bounded-search, operation/composite, main, single-target and seven-day fixtures |
| l8 | `Scraper/DanmuMatchScorer.cs` | Main/R3/TMDB/scorer fixtures |
| l9 | `Scraper/DanmuMatchScorer.cs` | `RegressionTests/TitleFidelity/Program.cs` and affected main/R3 scorer fixtures |
| l10 | l10-owned fields/branches in match DTO/controller/search engine and frontend smart-match source | Search-term, main/R3/candidate-evidence and frontend fixtures |

If implementation proves an additional file is strictly necessary, update this
allowlist before staging and explain why; never stage by broad directory or glob.

## Commit slots

| Slice | Commit | Focused checks | Independent inverse | Resulting tree | Restored final tree |
| --- | --- | --- | --- | --- | --- |
| l1 | `f90f779ce1219693fd2ea2ba0713451a893c9a02` | frontend smart-match regression; staged diff check | waived by user | not run | not run |
| l2 | `d6070f60f9344476ea677c968fd62da87e79856c` | frontend smart-match regression; staged diff check | waived by user | not run | not run |
| l3 | `720732398d12293db1e86009bbbfcd498917a1b8` | frontend; R3; main backend/Bilibili partial failure; TMDB alias; bounded-search after test-harness correction; staged diff check | waived by user | not run | not run |
| l4 | `9b008697274eb3cc12382eaf9b081101d3402e26` | Release build; main backend configuration-resource regression; staged diff check | waived by user | not run | not run |
| l5 | `1594b53524614763d3d19a7084ef4f1fc45f87f9` | Release build; main backend configuration-resource regression; staged diff check | waived by user | not run | not run |
| l6 | `5713498de45912384f013fa2180a1e7fadc4f002` | TMDB alias; SearchTermPolicy; main backend; R3 search-quality; frontend smart-match; Debug build; staged diff check | waived by user | not run | not run |
| l7 | `9a9b5e6a111abc2ca03b87a732a0acd51e1b3610` | BoundedSearchPolicy; R3 search-quality; TMDB alias cancellation; SearchTermPolicy; composite-season planner; seven-day replay; main backend; Debug build; staged diff check | waived by user | not run | not run |
| l8 | `67cd8f453ae2999e2e81406e5a5161b543e17792` | TitleFidelity; TMDB alias; R3 search-quality; main backend; staged diff check | waived by user | not run | not run |
| l9 | `ee09089a5460d2e13bb3424432366a500fa9296f` | TitleFidelity; R3 search-quality; EpisodeSelectionPolicy; main backend; Debug build; staged diff check | waived by user | not run | not run |
| l10 | `8f2dc5fd65df836e833712743deaaa6eb633e104` | SearchTermPolicy; main backend; `--manual-keyword-core`; R3 search-quality; composite-season planner; frontend smart-match; Debug build; staged diff check | waived by user | not run | not run |

The user explicitly waived the exhaustive ten-tree inverse matrix and requested
live deployment testing first. This record preserves the independent commits
and rollback boundaries without claiming that an inverse tree was exercised.

## Auxiliary test-only commits

- `419b730a9c874d3f98e3940769d3bbc946192915` — accept the baseline Bilibili
  typed `SearchMergedAsync` overload while still requiring the caller token as
  its second argument. This commit contains no production behavior and is not
  part of any l1-l10 rollback.

## Separate release-preparation commit

- Commit: `6e4c3c09df81150a3fa0d49fe9d5f11dc405d490`
- Checks: frontend regression; version/resource/document consistency; strict OpenSpec validation; staged allowlist, secret-pattern, and diff checks
- This commit is not part of the l1-l10 behavioral rollback map.

## Post-slice Season scoring and source-surplus refinement

- Commit: `872aa157a39cd1b235b52f7a7b630305e7201a3d`
- Behavior: Season scoring `60/20/20/0`; Episode-count differences are non-blocking; a response-only authoritative state drives the yellow local-shortage notice.
- Checks: frontend full regression; TMDB alias; composite planner; TitleFidelity; SearchTermPolicy; R3; BoundedSearchPolicy; R5 target scope; EpisodeSelectionPolicy; TemporaryRangePolicy; main backend; manual-keyword core; seven-day replay; clean Release build; strict OpenSpec; staged scope, secret-pattern, and diff checks.
- Independent rollback: `git revert 872aa157a39cd1b235b52f7a7b630305e7201a3d`
- This commit is independent of l1-l10 and can be reverted without rewriting their history.

## Post-slice TMDB alias terminal-marker correction

- Commit: `6d20fc0a7ac0027aea0309ac78b4f316256f7516`
- Behavior: In the TMDB alias parent-maximum path only, recover one verified terminal generic Season marker when a short parent alias leaves a bounded continuation matching a known parent-title window; reject OVA, named-Season, conflicting-number, numeric-only, short, unrelated, overlong, and trailing-text false positives.
- Checks: TMDB alias; TitleFidelity; SearchTermPolicy; R3 search quality; main backend; composite-season planner; frontend syntax and full regression; clean Release build; strict OpenSpec; final Sol-high review; staged scope, secret-pattern, and diff checks.
- Independent rollback: `git revert 6d20fc0a7ac0027aea0309ac78b4f316256f7516`
- This correction refines `872aa157a39cd1b235b52f7a7b630305e7201a3d`; reverting it retains the `60/20/20/0` score and source-surplus notice while removing only the alias-continuation recovery.

## Post-slice strict full-title Season fallback

- Commit: `b8b5ab26aeaf2eae26025eba5ebe4e8c61aa4f85`
- Behavior: When one concrete library Season title and one source-title item both lose their complete Season residual after the same matched parent is removed by loose normalization, strict complete-title NFKC fidelity equality may supply the existing 20-point Season-name component. Parent-only titles, different symbols, conflicting Season markers, one-sided residuals, cross-channel evidence, l9 confidence, Season 1, and Movie scoring remain unchanged.
- Checks: TitleFidelity; TMDB alias; SearchTermPolicy; R3 search quality; complete main backend regression; clean sequential Release build; strict OpenSpec validation; final Sol-high review with no P0, P1, or P2 finding; staged scope, secret-pattern, and diff checks.
- Artifact source commit: `68279e8847d6de4d91a286ca51cf25c2f1fe1e1a`, the documentation-only child of the reviewed implementation commit.
- Release DLL: 1633792 bytes; SHA-256 `009f03c73b956057a1807e0c2122f6b0d111c9c68626ed613e52afc09f62c7a6`.
- Independent rollback: `git revert b8b5ab26aeaf2eae26025eba5ebe4e8c61aa4f85`
- This local candidate has not been pushed, published, or deployed. The previously deployed V27 pair remains unchanged until fresh authorization is granted.
