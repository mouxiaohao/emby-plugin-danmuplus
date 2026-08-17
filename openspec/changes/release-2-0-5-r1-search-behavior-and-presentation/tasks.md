## 1. Baseline and rollback discipline

- [x] 1.1 Verify the implementation worktree is `codex/2.0.5r1-matching-behavior` at exact baseline `f8a4356537dcf0c8f913bb970bb2bcdc689096fd`, record the original dirty checkout as out of scope, and confirm only this change's OpenSpec artifacts are initially modified.
- [x] 1.2 Run the baseline frontend regression, main backend regression, bounded-search, r3 search-quality, title-fidelity, TMDB-alias, and seven-day replay checks sequentially; record any pre-existing failure before editing code.
- [x] 1.3 Create `artifacts/2.0.5r1/COMMIT_MAP.md` with slots for l1-l10 commit hashes, focused tests, and independent-revert evidence; do not include secrets, raw authenticated responses, or NAS credentials.
- [x] 1.4 Define per-slice staging allowlists so each l commit contains only its production files and focused tests; keep OpenSpec checkbox progress, version metadata, cumulative documentation, verification records, build output, and unrelated user files out of all l1-l10 commits.

## 2. l1 remove the obsolete composite warning clause

- [x] 2.1 Change only the reusable composite guidance text so `该季包含多个来源或存在未识别区间；` is absent and the exact retained sentence `下列卡片仅用于本次下载映射，不会改变Emby 的季归属。` remains intact.
- [x] 2.2 Add a focused frontend regression asserting zero removed-clause occurrences, the exact retained sentence, and no malformed punctuation or whitespace while leaving render placement unchanged.
- [x] 2.3 Run the focused frontend regression, stage only l1 production/test files, commit as `fix(l1): remove obsolete composite warning`, and record the hash and checks in the commit map.

## 3. l2 render mapping guidance once

- [x] 3.1 Move the reusable mapping-only guidance from each composite Season summary to the Series/Season result container, rendering it once when the applicable mapping-card result is shown and rebuilding it cleanly after rematch.
- [x] 3.2 Add frontend fixtures for multiple composite Seasons, multiple virtual groups in one Season, and rematch rerender; assert exactly one retained sentence per result and no duplicate DOM nodes.
- [x] 3.3 Run the focused frontend regression, stage only l2 production/test files, commit as `fix(l2): show mapping guidance once`, and record the hash and checks in the commit map.

## 4. l3 isolate provider failures from successful matching

- [x] 4.1 Extend search outcome state so completed-provider coverage, provider-local faults, and parent/user cancellation are distinguishable without removing the compatibility diagnostics or aggregate completeness field.
- [x] 4.2 Update Movie, Season, Episode-via-Season, whole-Series, residual/composite, and automatic library-import consumers to evaluate ordinary confidence from completed providers while retaining structural-plan, all-provider-failure, ambiguity, and parent-cancellation fail-closed gates.
- [x] 4.3 Add deterministic backend tests for one failed plus one high-confidence provider, one failed plus ambiguous successful candidates, all providers failed, provider-native partial failure, and parent cancellation; prove only the first case proceeds normally and no failure path writes metadata early.
- [x] 4.4 Extend frontend diagnostics coverage so the failed website remains a non-blocking public notice while successful-provider candidates and mappings remain usable.
- [x] 4.5 Run r3 search-quality, bounded-search, Bilibili partial-failure, main backend, and frontend focused checks; stage only l3 production/test files, commit as `fix(l3): keep provider failures non-blocking`, and record the hash and checks.

## 5. l4 change only the configuration-page heading

- [x] 5.1 Change the visible configuration-page heading from `Danmu 配置` to exact `DanmuPlus 配置` without editing plugin identity, page/resource keys, configuration routes, assembly name, Emby plugin-list naming, or saved settings.
- [x] 5.2 Add source-template and generated-resource regressions asserting the new heading and freezing the unchanged plugin compatibility identity.
- [x] 5.3 Run the focused configuration resource checks, stage only l4 production/test files, commit as `fix(l4): brand configuration heading as DanmuPlus`, and record the hash and checks.

## 6. l5 point source at the DanmuPlus main branch

- [x] 6.1 Change only the configuration page's `源码` href to `https://github.com/mouxiaohao/emby-plugin-danmuplus/tree/main`, retaining its external-link behavior and every unrelated update, release, proxy, and documentation URL.
- [x] 6.2 Add a focused configuration regression for the exact repository/branch URL and negative checks for the legacy upstream URL without exposing private backend constants.
- [x] 6.3 Run the focused configuration resource checks, stage only l5 production/test files, commit as `fix(l5): link DanmuPlus main source`, and record the hash and checks.

## 7. l10 preserve scored explicit-keyword discovery

- [x] 7.1 Add an l10-owned exact `manual-keyword` request/result intent, derived only from an explicitly edited valid keyword; do not introduce or reference any l6-owned type, endpoint, field, or test fixture.
- [x] 7.2 Reject empty or whitespace-only explicit input without a provider call; otherwise retain the existing outer trim, provider-owned normalization, candidate eligibility, `MergeSources`, ordinary score/reason generation, and provider-fair sixty-row `OrderCandidates` projection, including zero-score Movie rows.
- [x] 7.3 Stop manual-keyword discovery before TMDB alias expansion and `ClassifyResult`; do not discard rows merely for missing the automatic threshold, preselect a candidate, download, bind, or write metadata, while retaining ordinary target-bound evidence and authoritative detail/mapping validation after explicit selection.
- [x] 7.4 Add backend fixtures for trim/provider normalization, eligibility, merge/de-duplication, score/reason ordering, provider-fair sixty-row projection, zero-score Movie candidates, provider-local failure, all-provider failure, and explicit cancellation.
- [x] 7.5 Add frontend fixtures proving an edited keyword activates manual-keyword mode, displays server scores/reasons without preselection, keeps a high-confidence row manual, and enters the existing trusted detail/planning path only after explicit selection.
- [x] 7.6 Run search-term, manual-keyword core, r3 search-quality, main backend, composite planner, candidate-evidence/detail, frontend, and build checks; stage only l10 production/test files, commit as `fix(l10): preserve scored manual keyword results`, and record the hash and checks.

## 8. l7 remove shared search deadlines

- [x] 8.1 Remove the shared search layer's 10-second provider-call, 30-second interactive-operation, and 45-second automatic-operation deadline enforcement while retaining the policy's global/per-provider concurrency ownership and late-task settlement behavior.
- [x] 8.2 Remove elapsed-time `CancelAfter` and deadline token creation from the search operation registry, default Movie/Season search wrappers, and composite target coordination; continue propagating explicit caller/parent cancellation.
- [x] 8.3 Replace timeout-specific bounded-search fixtures with deterministic controllable-provider tests proving work remains pending beyond the former deadline, completes when released, cancels promptly when explicitly requested, and never releases a non-cooperative provider gate before settlement.
- [x] 8.4 Run retained regressions proving provider-native faults remain provider-local, maximum concurrency and one-provider-per-site isolation remain, the 180-second Movie/Episode download arbiter is unchanged, and seven-day skip/replay behavior is unchanged.
- [x] 8.5 Run bounded-search, search-operation, composite-target, main backend, single-target arbiter, and seven-day replay checks; stage only l7 production/test files, commit as `fix(l7): remove shared search deadlines`, and record the hash and checks.

## 9. l8 remove the 79-point contradiction cap

- [x] 9.1 Remove only the automatic contradiction cap that clamps explicit Season-number or known-year conflicts to `0.79`, deleting now-unused private rule code without changing ordinary title/year/episode weights or the automatic threshold.
- [x] 9.2 Replace cap assertions with scorer fixtures proving conflicting candidates retain their ordinary calculated score, including values below and above 0.80, while conflict evidence can still be explained and manual behavior remains unchanged.
- [x] 9.3 Run TMDB alias, scorer, r3 search-quality, and main backend focused checks; stage only l8 production/test files, commit as `fix(l8): remove contradiction score cap`, and record the hash and checks.

## 10. l9 remove the restricted fidelity bridge

- [x] 10.1 Remove the 0.85-to-automatic-threshold fidelity promotion and its unused constants/branches without removing Unicode normalization, fidelity evidence, ordinary scores, or equal-score fidelity tie resolution.
- [x] 10.2 Update title-fidelity fixtures so a unique exact-fidelity candidate with base score 0.85 remains 0.85, multiple/equal candidates remain deterministic, and no replacement bonus or equivalent bridge exists.
- [x] 10.3 Run title-fidelity, scorer, r3 search-quality, and main backend focused checks; stage only l9 production/test files, commit as `fix(l9): remove restricted fidelity bridge`, and record the hash and checks.

## 11. l6 replace exhausted aliases with parent-title rematch

- [x] 11.1 Track alias attempted/succeeded/exhausted state so only an alias reaching the current automatic threshold can replace the canonical automatic result; continue with later eligible aliases after one alias request faults, and enter exhaustion only after every eligible alias either faults or completes below threshold; on exhaustion, publish no accumulated alias candidate rows or TMDB-specific browser diagnostics.
- [x] 11.2 Add an l6-owned parent-title automatic-rematch request/state, independent of every l10-owned symbol, that resolves the authoritative parent Series title, sends exactly that one term with TMDB expansion disabled, and rejoins ordinary target-Season scoring; reject a mixed rematch-plus-keyword request without a provider call so it cannot enter l10 manual-keyword processing.
- [x] 11.3 Render alias exhaustion as failed/unmatched with a right-side `重新匹配` action, keep unrelated provider-failure diagnostics visible, and replace the state with fresh parent-title automatic candidates after activation.
- [x] 11.4 Add backend/frontend JOJO Season 1 fixtures for many aliases returning repeated low-confidence Dandanplay sources, successful alias short-circuit, alias request fault, missing parent title, unrelated provider failure, stale alias-state replacement, and exact separation from l10 manual mode.
- [x] 11.5 Run TMDB alias, search engine, controller, candidate-evidence, r3 search-quality, and frontend focused checks; stage only l6 production/test files, commit as `fix(l6): rematch exhausted aliases by parent title`, and record the hash and checks.

## 12. 2.0.5r1 metadata and cumulative documentation

- [x] 12.1 Set assembly/file version to `2.0.5.1`, informational and configuration version to `2.0.5r1`, and advance the established frontend/configuration cache marker needed to load the changed JavaScript and page resource without changing plugin/page identity.
- [x] 12.2 Add a cumulative 2.0.5r1 entry to `UPDATE.md` and update README current-version/behavior text while preserving all prior release history, screenshots, demonstration images, installation compatibility notes, and the explicit retained seven-day/download-timeout behavior.
- [x] 12.3 Add `artifacts/2.0.5r1/VERIFICATION.md` with the baseline, l1-l10 commit map, planned checks, artifact/hash placeholders, approval gates, and no credentials or raw authenticated data.
- [x] 12.4 Run version/resource/documentation consistency checks, stage only metadata, cumulative docs, OpenSpec progress, and verification records, commit separately as `release: prepare 2.0.5r1 verification`, and record its hash outside the l1-l10 rollback map.

## 13. Full local verification

- [ ] 13.1 Run the main backend regression and every affected specialized regression project sequentially, including bounded search, r3 search quality, search-term policy, title fidelity, TMDB alias, candidate detail/evidence, single-target download, and seven-day replay coverage.
- [ ] 13.2 Run the complete frontend smart-match regression and configuration source/generated-resource assertions; verify manual-keyword rows display only server scores/reasons in server order without browser re-scoring, re-sorting, preselection, or internal-value exposure, and verify TMDB-exhaustion results expose no alias candidates or TMDB diagnostics.
- [x] 13.3 Run a clean sequential Release build, capture DLL version/size/SHA-256, and verify no competing .NET process caused mapped-file output locks.
- [ ] 13.4 Run `openspec.cmd validate release-2-0-5-r1-search-behavior-and-presentation --strict`, final OpenSpec status, `git diff --check`, and a changed-file allowlist/scope audit.
- [ ] 13.5 Audit source, staged files, logs, verification records, DLL-visible configuration strings, and any local package manifest for credential values without printing secrets; verify TMDB keys/tokens, Dandan credentials, signatures, authorization headers, and private deployment details are absent.

## 14. Independent l1-l10 rollback record

- [x] 14.1 Record the exact l1-l10 commit hashes, focused evidence, and ordinary single-commit rollback commands without rewriting or squashing the behavior commits.
- [x] 14.2 Record that the user explicitly waived the exhaustive ten-tree inverse verification matrix and requested live deployment testing first; do not create or mutate a temporary revert worktree for this cycle.

## 15. Local packaging and approval-gated live verification

- [ ] 15.1 Assemble a local 2.0.5r1 review package containing only the verified Release DLL, matching CustomCssJS source asset, checksums, cumulative notes, and verification record; exclude `bin`, `obj`, logs, backups, diagnostics, credentials, deployment helpers with private paths, and unrelated artifacts.
- [ ] 15.2 Re-read the local package, verify file/version/hash agreement and forbidden-entry absence, and keep it local; do not push, tag, publish a GitHub Release, merge, or deploy.
- [x] 15.3 Obtain explicit user authorization before any Synology file replacement or Emby restart; authorization for this live-validation deployment is recorded in the task conversation, while credentials and private rollback locations remain outside repository artifacts.
- [ ] 15.4 If authorized, back up the paired deployed DLL, configuration, and CustomCssJS asset with hashes, ownership, and modes before replacing the approved files; retain a directly usable rollback copy.
- [ ] 15.5 Under the existing authorization, restart Emby, verify health, and perform live checks for multi-Season guidance, one-provider failure with successful siblings, JOJO Season 1 alias exhaustion/parent-title rematch, scored manual keywords with no preselection, long search plus explicit cancellation, ordinary automatic scoring after l8/l9, and unchanged seven-day replay/180-second download behavior.
- [ ] 15.6 If authorized, compare deployed DLL/asset hashes to the verified local package, scan bounded application logs for redacted failures, and either record success or restore the backup pair and re-verify Emby health.

## 16. Final review and handoff

- [ ] 16.1 Have a Sol high-reasoning reviewer inspect the final diff, l boundaries, specs/tasks coherence, failure/cancellation/persistence safety, scored manual-keyword explicit-selection boundary, TMDB state transition, and all verification evidence; resolve every blocking finding before handoff.
- [ ] 16.2 Confirm the delivered worktree is clean at the reviewed candidate, the original dirty checkout remains untouched, every l hash is recorded, and no push/merge/release/deployment occurred without its explicit gate.
- [ ] 16.3 Present the 2.0.5r1 implementation, verification summary, local artifact paths/hashes, ten recorded rollback commands without claiming waived inverse tests, residual risks, and live-validation result to the user for the next decision.

## 17. Post-slice Season score and verified source-surplus refinement

- [x] 17.1 Replace only the Season ordinary score distribution with parent title 60, Season name 20, exact known year 20, and Episode count 0; keep the automatic threshold, Movie score, TMDB parent-term maximum policy, and authoritative Episode mapping unchanged.
- [x] 17.2 Add focused scorer and TMDB-alias fixtures proving Episode-count mismatch neither changes score nor blocks an otherwise 80-plus Season, exact year contributes 20, missing/conflicting year contributes zero, and Movie behavior remains unchanged.
- [x] 17.3 Add a response-only authoritative Season source-surplus state derived from the actual provider Episode details used by a successfully applied composite plan; do not trust candidate `EpisodeSize`, sum several sources, or publish the state for failed, cancelled, stale, evidence-invalid, or zero-mapping plans.
- [x] 17.4 Render `库内集数少于来源集数` once in yellow in the shared whole-Series/single-Season composite summary only when that state is true; do not render it on candidate or temporary-Season cards, and clear it on a false/absent rerender.
- [x] 17.5 Add composite planner/controller and frontend regressions for local-smaller, equal, local-larger with retained temporary runs, candidate/detail count disagreement, multi-source independence, failed authority, whole-Series, single-Season, and rerender behavior.
- [x] 17.6 Recover a terminal generic Season marker only in the TMDB alias parent-maximum path when a short parent alias leaves a four-or-more-character continuation containing a letter and verified at `0.90` against a strictly equal-length window of a known parent title in the same source-title channel; cover the exact Bookworm S2/S3/S4 strings, OVA tie, ordinary scoring, named Season plus generic alias, short/numeric/unrelated/overlong prefix, trailing text, conflicting marker, Season 1, and Movie boundaries.
- [ ] 17.7 Run the affected focused suites, full backend/frontend regressions, sequential Release build, strict OpenSpec validation, diff/scope/credential checks, Sol-high final review, then back up and deploy the verified DLL/CustomCssJS pair under the existing authorization without initiating a download during live testing.
