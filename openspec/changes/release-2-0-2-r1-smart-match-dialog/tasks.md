## 1. Establish the r6 Source Baseline

- [x] 1.1 Locate or obtain the exact rebuildable r6 source commit/archive and create an isolated worktree that contains none of the current r7/r8 experimental changes.
- [x] 1.2 Build and compare the baseline against the archived r6 DLL, frontend script, configuration markers, deterministic tests, and representative live preview behavior; stop and request the missing source if equivalence cannot be established.
- [x] 1.3 Add a forbidden-symbol and changed-file check that rejects segment, collection, temporary-Season, or other r7/r8 functionality from the `2.0.2r1` candidate.

## 2. Chinese Match Explanations

- [x] 2.1 Add normalized presentation-only Chinese mappings for all r6 match-origin codes and their accepted aliases, without changing provider-id recognition or wire values.
- [x] 2.2 Add normalized Chinese mappings for all r6 decision-reason codes and Chinese primary fallbacks for unknown non-empty origin/reason values while omitting empty fragments.
- [x] 2.3 Add deterministic regressions for whitespace/case normalization, every known origin/reason, unknown fallbacks, empty values, secondary diagnostics, and unchanged `重新智能匹配` behavior.

## 3. Intentional Dialog Dismissal

- [x] 3.1 Remove backdrop dismissal without blocking or intercepting unrelated Emby page clicks.
- [x] 3.2 Route the top-right close action and topmost-dialog Escape handling through the existing closable guard while preserving unconditional explicit force-close behavior.
- [x] 3.3 Centralize idempotent disposal so close and force-close remove the overlay and keyboard listener exactly once, and add regressions for closable/protected states, stacked dialogs, repeated cleanup, and unchanged task state.

## 4. Version and Documentation

- [x] 4.1 Set the release string to exactly `2.0.2r1`, numeric file version to `2.0.2.1`, and frontend installation marker to V12 without changing r6 backend contracts.
- [x] 4.2 Update README release notes and usage text for Chinese explanations, backdrop behavior, ×/Escape behavior, protected downloads, and the strict r6 compatibility boundary.

## 5. Verification, Packaging, and Deployment

- [x] 5.1 Run frontend syntax and deterministic regressions, backend regression executable, Release build, strict OpenSpec validation, diff checks, and the r7/r8 forbidden-symbol review.
- [x] 5.2 Package a paired `2.0.2r1` DLL and frontend asset with SHA-256 verification and reproducible source/build notes.
- [x] 5.3 Back up the deployed r6 DLL and both plugin configurations, deploy the paired candidate, restart Emby, and verify plugin load plus rollback readiness.
- [x] 5.4 Live-test known Chinese explanations, backdrop non-dismissal, and ×/Escape in closable state without downloading or modifying metadata; verify unknown fallbacks, protected-state close rejection, and unchanged r6 Movie/Series/Season/Episode behavior with deterministic frontend/backend regressions because those states cannot be safely produced live without synthetic responses or starting a download.
