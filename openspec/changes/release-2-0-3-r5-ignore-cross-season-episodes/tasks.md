## 1. Freeze the r4 baseline and define the r5 contract

- [x] 1.1 Record the deployed r4 source commit, DLL/JavaScript/configuration hashes, V20 protocol marker, and the read-only One Punch Man and Seitokai Yakuindomo inventory/preview evidence used as the r5 comparison baseline.
- [x] 1.2 Create an r5 implementation branch/worktree from the frozen r4 source without importing experimental-version files or changing the r4 release tree.
- [x] 1.3 Define the r5 numeric mapping protocol, plan-generation contract, frontend cache marker, structured target-inventory, `no-eligible-episodes`, and stale-scope diagnostics.
- [x] 1.4 Sync the deployed r4 delta specs into main specs without archiving the still-open r4 change, then add and strictly validate an r5 `parent-season-aware-episode-mapping` delta that removes foreign/unknown supplemental runs, updates maximal-group and cross-target scenarios, and leaves no normal-Season SHALL permitting S00/other-season matching.

## 2. Build the authoritative target-Season episode inventory

- [x] 2.1 Add an immutable target-Season Episode inventory model keyed by valid Emby ItemId and carrying observed `ParentIndexNumber`, stable Season display ordinal, and placement metadata.
- [x] 2.2 Implement one target-Season inventory builder over `targetSeason.GetEpisodes()` that de-duplicates identical ItemIds, preserves deterministic ordering evidence, and fails closed on unavailable or irreconcilable metadata.
- [x] 2.3 Add deterministic inventory diagnostics for unavailable/incomplete enumeration, invalid ItemIds, conflicting parent-season observations, and unknown parent-season values without guessing from filenames, positions, provider identifiers, or episode numbers.
- [x] 2.4 Add inventory-level regressions proving standalone Season 0 uses only its own Parent 0 Episode inventory even when the same Episodes are also displayed by another Season.

## 3. Implement target-season eligibility and ordering

- [x] 3.1 Implement a pure target-scope operation that accepts only records whose `ParentIndexNumber` exactly equals the selected Season's known `IndexNumber`, physically separating all Parent 0, other-season, unknown-parent, and invalid records before planning.
- [x] 3.2 Preserve standalone Season 0 by applying the same exact equality rule with target number zero, while rejecting an unknown target Season number with zero search/download/write activity.
- [x] 3.3 Produce stable eligible ordering from the target Season's display/placement evidence and ItemId as the final tie-break, without sorting or identifying Episodes solely by `IndexNumber`.
- [x] 3.4 Produce read-only out-of-scope counts grouped by Parent 0, other-season, unknown-parent, and invalid identity; keep the excluded records out of mappings, unmatched runs, temporary seasons, and executable entries.
- [x] 3.5 Add pure regressions for S1 with S01E01-E12 plus S00E01-E07, S1 with another normal Season mixed in, duplicate episode numbers across logical seasons, unknown parent values, no eligible Episodes, and standalone S0 filtering.

## 4. Route every interactive batch path through the shared scope

- [x] 4.1 Change whole-Series target enumeration to include only Seasons with known positive `IndexNumber`, skipping Season 0 and unknown-number Seasons before search, scoring, rendering, download, and Season identifier mirroring.
- [x] 4.2 Make whole-Series and explicit single-Season preview call the same target-Season inventory/scope coordinator so the same SeasonId produces identical eligible ItemIds, expected episode count, scores, mappings, temporary runs, and completeness.
- [x] 4.3 Make confidence-selected and manually selected Season candidates always build explicit mappings only over eligible Episodes; a short source shall create temporary runs only from remaining eligible Episodes.
- [x] 4.4 Remove or seal every reachable r4 supplemental/foreign-season planning path so an ignored Episode cannot be reintroduced by rematch, restore, compact selections, direct groups, or legacy fallback.
- [x] 4.5 Preserve single-Episode and Movie exact-identifier behavior unchanged and add scope-contract tests proving that the new filter is limited to Series/Season batch operations.

## 5. Unify download, retry, fingerprint, and identifier mirroring

- [x] 5.1 Make preview, StartTrackedDownload, partial confirmation, download preflight, task snapshot, and exact retry rebuild the same target-season scope from the current target Season inventory rather than trusting client exclusions.
- [x] 5.2 Extend the plan fingerprint with Series/Season identity, target number, every observed ItemId-to-parent-season pair, eligibility outcome, eligible ordering, selections, and mappings.
- [x] 5.3 Reject download and retry with a structured stale-plan result and zero file/metadata writes when an Episode changes into or out of the eligible target scope after preview.
- [x] 5.4 Calculate task totals, success/failure/cancelled counts, partial status, and terminal completeness only from eligible executable mappings.
- [x] 5.5 Retain the write-only complete-single-source Season display mirror: after terminal success overwrite only the verified target provider key when every eligible Episode is covered; perform no write for partial, multi-source, failed, cancelled, or stale plans and never clear any identifier.
- [x] 5.6 Add deterministic terminal and retry regressions for complete eligible coverage with ignored S00 records, eligible partial coverage, multi-source coverage, cancellation, failure, stale parent metadata, same-provider overwrite, foreign-key preservation, and old-generation completion.

## 6. Unify automatic Season processing

- [x] 6.1 Route automatic positive-Season processing through the same target-Season inventory, exact target-scope filter, bounded search, explicit planner, execution snapshot, and terminal mirror used by interactive matching.
- [x] 6.2 Support an explicit automatic Season 0 event through the same Season 0 own-inventory Parent 0 scope as the interactive Season 0 entry while keeping whole-Series interactive target enumeration Season-0-free.
- [x] 6.3 Ensure automatic processing does not read Series/Season/Episode plugin identifiers or manual bindings as batch evidence and cannot use a separate supplemental/direct continuation path to adopt foreign Episodes.
- [x] 6.4 Add regressions proving interactive and automatic snapshots are identical for normal Season and S0 targets, ambiguous/incomplete searches perform zero download/write, and the existing identifier metamorphic matrix remains invariant.

## 7. Upgrade the frontend protocol and presentation

- [x] 7.1 Upgrade the server/client mapping protocol and CustomCssJS cache marker together, require the r5 plan generation on preview selections and downloads, and reject V20/r4 drafts or foreign-season selections with a structured stale-protocol response.
- [x] 7.2 Clear incompatible in-dialog r4 selections on fresh r5 preview without silently submitting them, while preserving the current dialog generation, cancellation, error normalization, score display, rematch, remove, and restore safeguards.
- [x] 7.3 Render only eligible mappings and temporary seasons as actionable cards; optionally show a read-only summary of displayed versus matched versus ignored cross-season counts with no buttons or wire selections for ignored Episodes.
- [x] 7.4 Add frontend regressions proving normal S1 has no S00 temporary card, whole-Series has no Season 0 target card, explicit S0 renders its Parent 0 plan, ignored records never enter compact selections, and V20 drafts cannot execute.

## 8. Complete deterministic regression and scope gates

- [x] 8.1 Add end-to-end One Punch Man fixtures proving Series and S1 parity: twelve Parent 1 mappings, zero temporary runs, seven Parent 0 records reported only as out of scope, and nineteen observed ItemIds represented once in the scope snapshot.
- [x] 8.2 Add Seitokai Yakuindomo fixtures proving thirteen Parent 1 mappings, zero temporary runs, eight placed Parent 0 records ignored by S1, and explicit standalone S0 uses only the real S0 item's Parent 0 inventory.
- [x] 8.3 Add a short-source fixture proving ten of twelve eligible Episodes map and exactly one two-Episode eligible temporary run remains regardless of foreign display records.
- [x] 8.4 Add target-Season/S0 ItemId de-duplication, cross-normal-season, unknown-parent, empty-eligible, inventory-unavailable, parent-change stale, and Series-versus-Season semantic parity regressions.
- [x] 8.5 Run the Release solution build, full backend regressions, all focused r4/r5 suites, frontend syntax/regression checks, whitespace checks, and strict OpenSpec validation; resolve every new error before packaging.
- [x] 8.6 Add an r5 scope gate that rejects experimental files, requires .NET Standard 2.0/C# 8 compatibility, validates the paired version/protocol/cache markers, and compares only the intended product/test/artifact changes against the frozen r4 baseline.
- [x] 8.7 Run a cross-change specification audit after the r4 sync and r5 delta, proving the effective main specs contain no requirement or scenario that turns foreign/unknown Episodes into actionable temporary runs for a normal Season.

## 9. Package and deploy the paired r5 release

- [x] 9.1 Generate paired r5 DLL and CustomCssJS assets plus atomic configuration updater and verified LF restart helper; record exact sizes, SHA-256 hashes, assembly/file/product versions, protocol marker, and rollback assets in `artifacts/2.0.3r5/VERIFICATION.md`.
- [x] 9.2 Before deployment, back up the active r4 DLL, Danmu configuration, CustomCssJS configuration, database/WAL/SHM, library configuration, and plugin state with absolute paths and hashes; verify the rollback trio in an isolated dry run.
- [x] 9.3 Atomically deploy the paired r5 DLL/JavaScript, restart Emby, and verify the user-approved Emby 4.9.5.0 HTTP health baseline, plugin loading, active hashes, unique r5 marker, and absence of load errors before any product write test.

## 10. Perform live r5 acceptance and safe cleanup

- [x] 10.1 Run authenticated read-only whole-Series and S1 previews for One Punch Man and Seitokai Yakuindomo, proving Season 0 is absent as a Series target, only Parent N Episodes enter normal-season plans, and ignored S00 Episodes create no temporary cards or executable mappings.
- [x] 10.2 Run an authenticated read-only explicit Season 0 preview proving it uses the real Season 0 item's own inventory, includes only Parent 0 Episodes exactly once, and does not merge records from normal-Season display inventories.
- [x] 10.3 Verify browser UI parity, score/error rendering, ignored-count presentation, no stale V20 draft execution, and no console regression in both whole-Series and single-Season entry points.
- [ ] 10.4 Using only an isolated Emby fixture that is successfully indexed, verify complete eligible single-source download/mirror, eligible partial temporary run, retry, cancellation/failure/stale no-write behavior, ignored foreign zero execution, membership preservation, and target-key-only identifier overwrite.
- [ ] 10.5 Remove the test virtual library before deleting media, confirm fixture ItemIds return 404, verify the resolved fixture/staging paths are non-symlink and outside every production root, delete only those exact paths, and remove state only when its SeasonId uniquely matches the fixture.
- [x] 10.6 Reconfirm production HTTP health, DLL/configuration hashes, marker uniqueness, plugin load status, production membership invariants, and rollback instructions; keep r5 deployed only if every mandatory read-only gate passes and document any write-fixture blocker without claiming full acceptance.
