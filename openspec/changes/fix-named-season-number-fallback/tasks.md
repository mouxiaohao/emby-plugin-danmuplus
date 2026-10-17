## 1. Shared fallback and deterministic checks

- [x] 1.1 Add failing regression coverage for named-to-numbered fallback, named precedence, ambiguity, invalid ordinal, explicit keyword, cancellation, and incomplete discovery.
- [x] 1.2 Implement one shared standard-Season retry with original metadata preserved, canonical candidates merged, and diagnostics retained.
- [x] 1.3 Verify initial fallback feeds Bookworm-style logical Seasons 1-3 and proven next-physical-Season continuation still uses logical Season 4.

## 2. Compatibility and delivery

- [x] 2.1 Run targeted search, TMDB, composite, remainder, continuity, and full backend regressions serially; run strict OpenSpec validation and a Release build.
- [x] 2.2 Document fallback behavior and package a DLL, hashes, baseline, and rollback boundary.
- [x] 2.3 Preserve the original checkout and initial local-only package; the user has now authorized the separately backed-up NAS rollout below.

## 3. Current-main plus NAS hotfix delivery

- [x] 3.1 Verify current remote main and preserve the exact NAS confirmed-download/retry source patch as an independently reviewable baseline commit; reapply the fallback without losing either regression registration.
- [x] 3.2 Re-run backend, frontend, focused fallback/composite/logical-continuity and confirmed-download regressions serially, strict OpenSpec validation, and a clean Release build on the integrated source.
- [x] 3.3 Back up live DLL and configuration, deploy only the changed DLL, verify health, hashes and unchanged unrelated configuration, and retain an executable rollback.
- [x] 3.4 Validate Bookworm on NAS for physical S1 combining source S1-S3 and next physical S2 continuing source S4. Bookworm has no problematic descriptive Season name, so do not count it as natural fallback evidence or edit its metadata. Validate descriptive-name fallback with a suitable real named Season if available; otherwise use isolated test input and explicitly disclose the simulated boundary.
- [ ] 3.5 After successful acceptance, review the scoped diff, commit, push the codex branch and create a non-draft PR to latest main; do not merge or publish a release without a further request.

## Acceptance record (2026-10-17)

- Current-main base: `6d307dd`; the existing NAS confirmed-download/retry patch is retained in a separate baseline commit.
- Integrated source passed 19 serial checks, then full backend plus a clean Release build after adding fallback diagnostics (0 errors; 131 pre-existing warnings).
- The installed DLL was independently read back and passed targeted fallback, confirmed-download, logical-inventory and full backend tests in a source-layout-preserving local isolation.
- NAS read-only Bookworm UI/API preview: physical S1 maps 36 Episodes to Dandan sources 14727 / 15293 / 15634 in groups 14 + 12 + 10; physical S2 maps 24 Episodes to source 18302 (logical S4). No unmatched runs.
- Validation boundary: existing named NAS samples matched during ordinary discovery. They do not prove a live fallback trigger. The failure-to-numbered fallback branch was verified with the exact NAS DLL in isolated local fixtures; no production Season metadata was renamed.
- DLL-only deployment preserved all plugin configuration file hashes, including CustomCssJS and Danmu settings; backup and a guarded DLL-only rollback were retained. No danmu download or binding write was performed.
