## 1. Freeze the r6 release boundary

- [x] 1.1 Create an isolated r7 workspace/branch from the verified r6 source and record r6 product, package, frontend-marker, and protocol hashes.
- [x] 1.2 Extend the narrow-release gate so r7 permits only the specified UI, transient source-title transport, tests, version, OpenSpec, and packaging files while rejecting collection/segment or target-scope changes.
- [x] 1.3 Add source/hash assertions proving r5/r6 Season eligibility, S0 behavior, plan fingerprints, selection evidence, mapping protocol 21, confirmation, and download execution remain unchanged.

## 2. Make busy pages non-actionable

- [x] 2.1 Remove force-refresh rendering from the shared busy/request renderer while retaining only an available cancel action.
- [x] 2.2 Preserve the dialog-scoped force-refresh value without resetting, locking, or snapshotting it during preview, rematch, detail, and plan-validation requests.
- [x] 2.3 Add frontend regressions for zero busy checkboxes and exactly one restored checkbox with the previous value after success, cancellation, retryable error, and zero-task startup recovery.

## 3. Transport verified mapping titles

- [x] 3.1 Capture bounded source Episode titles from the provider media already resolved during composite plan construction without issuing another provider request.
- [x] 3.2 Add a presentation-only source Episode title to composite preview rows and explicitly exclude it from fingerprints, equality, evidence, compact selections, task snapshots, persistence authority, and download validation.
- [x] 3.3 Render each expanded mapping as local season/Episode label plus library title to source position plus server title, with clean fallbacks and no internal identifiers.
- [x] 3.4 Add backend call-count/fingerprint fixtures and frontend full/missing/long/malicious-title regressions, including narrow viewport layout.

## 4. Unify temporary-season manual matching

- [x] 4.1 Introduce a range-keyed dialog state using SeasonId, run start/count, plan generation/fingerprint, and search generation; stop storing temporary-range candidates in the Season-wide candidate list.
- [x] 4.2 Route unmatched `手动匹配` and mapped `重新匹配` to the same `手动匹配未匹配临时季` picker without changing the authoritative mapping before apply.
- [x] 4.3 Preserve adapter-specific temporary-range search, source-start/count input, authoritative apply, back, cancel, error, and overview restoration semantics for both whole-Series and direct Season entry points.
- [x] 4.4 Extract/reuse a candidate-row presentation component that shows each server score exactly once without recalculating, filtering, or reordering candidates.
- [x] 4.5 Mount the r6 evidence-bound lazy-detail control on every temporary-range candidate with radio-event isolation, request deduplication, row-local retry, and range/search/evidence stale gates.
- [x] 4.6 Add regressions proving zero eager detail calls, one click/one candidate resolution, other candidates unresolved, identical entry menus, unchanged mapping on back/failure, and correct temporary-range request fields.

## 5. Preserve r5/r6 behavior

- [x] 5.1 Re-run whole-Series S0/null exclusion, explicit S0, foreign/unknown-parent filtering, One Punch Man, Seitokai Yakuindomo, Series/direct-Season parity, identifier metamorphic, and scope-fingerprint drift fixtures.
- [x] 5.2 Re-run r6 initial Episode/manual Episode lazy-detail, SelectionEvidenceToken rotation, mapping-detail, compact force-refresh, download snapshot/lock, and no-task recovery regressions.
- [x] 5.3 Verify candidate inspection and mapping-title display produce no binding, selection, metadata, XML, identifier, or tracked-download writes.

## 6. Version, package, and deploy

- [x] 6.1 Raise the plugin file/informational/config version to r7 and the frontend cache/install marker to V23 while retaining mapping protocol 21.
- [x] 6.2 Run frontend syntax/regressions, backend deterministic suites, Release build, r7 narrow-delta gate, strict OpenSpec validation, and whitespace checks.
- [x] 6.3 Package paired r7 DLL/frontend assets, record hashes, back up the active r6 trio, deploy atomically, restart Emby, and verify hashes, health, ownership, marker counts, and startup logs.
- [x] 6.4 Perform read-only live acceptance on whole-Series and direct Season mapped/unmatched temporary runs: busy checkbox absence, unified menu title, single score, lazy one-candidate expansion, dual mapping titles, unchanged back/cancel mapping, and zero writes.
- [x] 6.5 Document the paired r6 rollback directory and verified restore procedure.
