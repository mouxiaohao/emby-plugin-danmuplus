## 1. Restore and freeze the real r5 baseline

- [x] 1.1 Roll the live Emby plugin back to the paired pre-r6 DLL, CustomCssJS configuration, and plugin configuration; verify hashes and HTTP health.
- [x] 1.2 Create an isolated r6 workspace by copying the exact source workspace that produced the deployed r5 DLL hash.
- [x] 1.3 Record r5 DLL/JavaScript hashes, protocol/cache markers, baseline test results, and a diffable source snapshot before r6 edits.
- [x] 1.4 Add release gates proving the r6 package is built only from the r5-derived workspace and contains no collection/segment implementation absent from r5.

## 2. Preserve r5 behavior with explicit regression gates

- [x] 2.1 Add/retain whole-Series tests proving S0 and unknown-number Seasons are not searched, rendered, or executed.
- [x] 2.2 Add/retain One Punch Man and Seitokai Yakuindomo tests proving normal Seasons exclude displayed S00/foreign/unknown-parent Episodes from counts, scoring, mappings, temporary ranges, provider calls, downloads, and completeness.
- [x] 2.3 Add/retain explicit S0, short eligible source, Series/direct-Season parity, scope-fingerprint drift, retry/automatic/mirror, and identifier-metamorphic tests.
- [x] 2.4 Add frontend gates proving normal S1 has no S00 temporary card and every authoritative Season plan retains `查看集映射详情` with per-Episode rows.

## 3. Add the read-only candidate-detail backend

- [x] 3.1 Define a presentation-safe request/response contract bound to target, provider, candidate, and short-lived preview evidence.
- [x] 3.2 Add the authenticated detail operation using existing r5 provider normalization without invoking planning, confirmation, binding, persistence, or download paths.
- [x] 3.3 Remove only the eager fallback detail loop used to infer single-Episode suggestions; do not alter r5 discovery, target scope, or plan construction.
- [x] 3.4 Add deterministic call-count, forged/stale evidence, ordering, error, and no-side-effect tests.

## 4. Add manual-only lazy candidate details

- [x] 4.1 Add dialog-scoped per-candidate idle/loading/loaded/error/expanded state with request deduplication and stale-response rejection.
- [x] 4.2 Add a shared `解析并查看详情` control that stops row selection and expands number/title rows beneath only that candidate.
- [x] 4.3 Gate the control to explicit Episode manual rematch/search results; keep initial exact Episode controls unchanged and detail-free while hiding internal identifiers.
- [x] 4.4 Add the control to existing r5 whole-Series per-Season and direct Season manual candidate lists without changing confirmation or navigation semantics.
- [x] 4.5 Keep the local Episode summary visible on manual candidate pages and keep the existing `查看集映射详情` feature structurally separate.

## 5. Make force refresh compact and dialog-scoped

- [x] 5.1 Render one lower-left checkbox labelled exactly `强制刷新` on every pre-download level.
- [x] 5.2 Remove repeated seven-day explanation text and Esc/X close hints while preserving existing buttons and navigation.
- [x] 5.3 Preserve one mutable value across navigation/loading/retry and ensure matching/detail/planning requests are independent of it.
- [x] 5.4 Snapshot and lock at single/multi download start, reuse one value for all tasks, and unlock only when startup fails before any task exists.

## 6. Verify, package, and deploy

- [x] 6.1 Run all frontend tests, backend deterministic suites, r5 target-scope gates, Release build, strict OpenSpec validation, and diff/whitespace checks.
- [x] 6.2 Perform read-only live acceptance for One Punch Man, Seitokai Yakuindomo, explicit S0, initial exact Episode, searched Episode, direct Season, and whole-Series per-Season flows.
- [x] 6.3 Verify zero initial detail calls, one-candidate expansion, persistent local context, preserved mapping-detail view, compact force checkbox, and no writes during inspection.
- [x] 6.4 Package paired r6 assets from the r5-derived workspace, back up the active r5 trio, deploy atomically, restart Emby, verify hashes/health/logs, and document rollback.
