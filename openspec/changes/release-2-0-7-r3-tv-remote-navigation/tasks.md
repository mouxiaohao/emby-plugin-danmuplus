## 1. Baseline and failing contracts

- [x] 1.1 Record the isolated 2.0.7r3 worktree branch/base/status, the 2.0.7r2 V34/V22 frontend hash and Arrow-handler absence, Emby 4.9.5.0 health, active DLL/configuration hashes, and the enabled CustomCssJS component inventory without recording credentials or component content.
- [x] 1.2 Extend the frontend fake DOM with active-element focus/blur/focusin, geometry, visibility/computed-style, prevent-scroll, class-list, propagation cancellation, pointer-mode, and body-scroll instrumentation needed to exercise the real dialog controller.
- [x] 1.3 Add failing real-key fixtures for deterministic entry/lost-focus recovery, nested topmost ownership, four-direction half-plane scoring/ties/edges, disabled-hidden exclusion, and no host-page propagation.
- [x] 1.4 Add failing fixtures for radio/checkbox one-shot confirm, native button/search behavior, editable Left/Right, Up/Down field exit, Tab boundaries, pointer handoff, focus styles, and key-repeat suppression.
- [x] 1.5 Add failing fixtures for candidate rerender continuity, each existing parent/child boundary, parent scroll plus focus restoration, busy/no-target recovery, off-screen body-only reveal, cleanup, and unchanged Android command Back/host-pop behavior.

## 2. Dialog-local remote implementation

- [x] 2.1 Implement bounded eligible-control discovery, standard key normalization, topmost gating, half-plane geometry, deterministic scoring, and edge retention without changing candidate order or selection.
- [x] 2.2 Implement direction event ownership, Tab containment, editable-field exceptions, and radio/checkbox-only confirm bridging while leaving native button/search/disclosure activation unsynthesized.
- [x] 2.3 Implement transient semantic surface focus capture/completion around every body/footer replacement and integrate parent-return focus with the existing action/row/section/neighbor viewport context.
- [x] 2.4 Implement prevent-scroll focusing, minimum dialog-body-only reveal, remote/pointer input-mode handoff, high-contrast focus/focus-within styles, and idempotent listener cleanup.
- [x] 2.5 Advance the final frontend install marker to V36 and release identity to 2.0.7r3 while retaining assembly compatibility 2.0.7.0, mapping protocol V22, and 2.0.7r2 backend behavior.

## 3. Deterministic verification

- [x] 3.1 Run `node --check` on the frontend and regression files and run the complete Smart Match JavaScript regression suite with all new remote fixtures passing.
- [x] 3.2 Prove static guards for V36/V22 with V35 absent, focus styling and controller lifecycle, and the absence of `scrollIntoView`, host-scroller ownership, MutationObserver/timer/animation-frame focus correction, new history/backbutton/Back paths, private Emby focus APIs, responsive television heuristics, and matching/API changes.
- [x] 3.3 Run strict OpenSpec validation and reconcile proposal, spec, design, tasks, and implementation behavior.
- [x] 3.4 Run the complete .NET regression suite and a clean sequential Release build; verify no unexpected warning/error regression and all 2.0.7r3 version fields.
- [x] 3.5 Run `git diff --check`, scope/status/diff review, credential and private-data scan, and verify the dirty primary checkout plus every earlier worktree/artifact remains unchanged.

## 4. Documentation and review package

- [x] 4.1 Update README current-version behavior and cumulative UPDATE with 2.0.7r3 D-pad/OK/focus support, V36/V22 identity, compatibility limits, and live-test scope while preserving all history and demonstration assets.
- [x] 4.2 Assemble `artifacts/2.0.7r3/review-package` with the Release DLL, V36 frontend, cumulative UPDATE, verification record, and SHA-256 manifest generated from reviewed bytes.
- [x] 4.3 Independently read back every review-package file/hash, compare the packaged frontend and DLL with the reviewed build outputs, and repeat the relevant automated gates on the packaged asset.

## 5. Synology deployment and live remote acceptance

- [x] 5.1 Immediately before deployment, create a timestamped shared-volume rollback directory containing the active DLL and complete CustomCssJS XML; record hashes, sizes, owner/mode, Emby health, and every component name/state/content hash without exposing credentials.
- [x] 5.2 Stage the reviewed pair and replace only the enabled named Smart Match component in a copy of the freshly frozen XML; reparse it and prove the unrelated `danmuku` entry plus all non-target configuration fields are unchanged.
- [x] 5.3 Atomically install the staged DLL/XML with preserved owner/mode, restart Emby, require HTTP 200/4.9.5.0, and verify runtime DLL/config hashes plus exactly one V35 marker and no active V34 Smart Match marker.
- [x] 5.4 Authenticate through the deployed Emby client at a 1280x720 television viewport and use Arrow/Enter/Tab/Escape equivalents to verify initial focus, candidate traversal/selection, search-field editing/search, footer movement, disclosure, child entry/return, pointer handoff, and visible focus without unintended bind/download writes.
- [x] 5.5 Record focus rectangles, dialog-body/host scroll, route, topmost ownership, request counts/shapes, protected/busy behavior, close/Back cleanup, post-close host control, and non-target CustomCssJS hash; treat browser emulation as deployed web-event acceptance rather than a claim of physical remote hardware coverage.
- [x] 5.6 If any staging, restart, health, hash, focus, route, host-scroll, interaction, or unrelated-configuration gate fails, restore the paired rollback DLL/XML and reverify exact hashes plus HTTP 200; otherwise retain the rollback directory and deployed evidence.

## 6. Final review

- [x] 6.1 Review the complete diff, focus/event ownership, native-control behavior, parent/child scroll interaction, matching/API non-change, version/package consistency, deployment evidence, rollback readiness, and remaining OpenSpec state; resolve every blocking finding and rerun affected gates.

> Initial V35 acceptance record (2026-08-25, superseded by the section 7 follow-up): the reviewed DLL and target-only V35 CustomCssJS update were active on Emby 4.9.5.0 with HTTP 200, exact runtime/package hashes, one V35 marker, no active V34 marker, and the unrelated `danmuku` hash unchanged. Authenticated 1280x720 browser acceptance passed real and production-fixture D-pad focus, radio/checkbox one-shot confirm, keyword Enter search, Tab containment, pointer handoff, topmost ownership, rerender/parent-return focus, dialog-only reveal, protected-busy recovery, cleanup, and zero plugin write requests. The injector does not execute native caret displacement even in a plain control, so the run proved the editable event was unprevented, remained in-field, and did not escape to Emby; physical television remote hardware remained explicitly outside the claimed coverage. Rollback was not needed and the root-only backup set remains retained.

## 7. Follow-up Season navigation and temporary-suffix rematch

- [x] 7.1 Reproduce and record the real Season candidate beam skipping every narrow radio, the missing Up-to-X body-top alignment, and the later-selection payload that triggers the backend's unique-trailing-suffix rejection.
- [x] 7.2 Add failing frontend fixtures for full-card candidate traversal/confirm/Space and pointer preservation, Up-to-X body-only alignment, and three mapped temporary Seasons whose first or middle rematch prunes exactly the clicked suffix.
- [x] 7.3 Implement one full-width focus proxy per candidate radio across every Smart Match candidate/source/part surface, with synchronized checked semantics and no duplicate activation; implement the directional X body-top boundary without host scrolling.
- [x] 7.4 Implement explicit-rematch suffix exclusions and selection pruning with exact Back/failure rollback, partial-replacement unmatched remainder, and unchanged clicked-run-only ordinary Remove behavior.
- [x] 7.5 Run syntax, complete frontend regressions, strict OpenSpec, complete .NET regressions, clean sequential Release build, diff/scope/privacy checks, and rebuild/read back the 2.0.7r3 review package and manifest.
- [x] 7.6 Freeze a fresh paired rollback backup, replace only the named V36 Smart Match component plus reviewed DLL if changed, restart/health/hash verify, and repeat authenticated 1280x720 real Season candidate, X-scroll, and bounded composite-suffix acceptance while preserving the unrelated component and plugin configuration.
- [x] 7.7 Perform final implementation/evidence review, resolve every blocking finding, and record the follow-up acceptance boundary including physical-remote limitations.

> Final V36 follow-up acceptance (2026-08-26): the reviewed V36 frontend and unchanged reviewed DLL are active on Emby 4.9.5.0 with HTTP 200, exact runtime/package hashes, one V36 marker, no V35/V34 marker, and unchanged `danmuku` plus plugin-configuration hashes. Real 1280x720 Season acceptance proved Down from the right-aligned search action lands on a full candidate card, one Enter selects one native radio, and Up to X resets only dialog-body scroll. Real whole-series acceptance proved the same X boundary and rematching Stone Ocean temporary Season 1 converts completed temporary Seasons 1/2/3 into one `S05E01–S05E38` unmatched suffix with 38 exclusions and zero stale selections; returning restored all three original mappings and sources. The server recorded only MatchPreview requests and no bind/download/apply/metadata write. Rollback was not invoked and the fresh root-only paired backup remains retained. Physical television hardware remains outside the browser-emulated acceptance claim.
