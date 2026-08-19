# 2.0.6r1 local verification

This record covers local deterministic verification, three earlier authorized r1 live attempts, and the later Android phone/car-head-unit root-cause probes. The first two r1 attempts failed and were immediately rolled back; the third passed desktop acceptance. Diagnostic C proved Emby's cancelable `command: "back"` precedes and suppresses the downstream `backbutton`; diagnostic D then canceled that command and invoked Smart Match once, and the user confirmed correct child return, top-level close, protected-state retention, and host-route stability on both Android devices. Those diagnostics establish the formal V30 design but do not by themselves accept the final V30 asset. No push, merge, tag, or publication was performed. Host details, credentials, private paths, media identifiers, and raw responses are intentionally excluded.

## Frozen 2.0.6 baseline

- Git base: `d22a1069524bd891c5b36c758f75f4112a19e1f4`
- Predecessor OpenSpec state: 36/36 complete
- Frozen 2.0.6 review/deployed DLL: 1657856 bytes; SHA-256 `a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c`
- Frozen 2.0.6 frontend: 234032 bytes; SHA-256 `a447671b98f991075254665bf3c74d029fd0f3b6ddb5aecd661377d0bd1cd3a3`
- Frozen `artifacts/2.0.6` tree SHA-256: `39620afe3c634696f30670a1ecc0f93071521fed833ce687b0f5105f5ac7fd7c`
- Actual tracked baseline patch: 28240 bytes; SHA-256 `94b32b46a4f06473e4d7fa4ee170d70ca359ed9c51b152374f3d971d347003c1`
- Full path/size/hash inventory: `BASELINE-MANIFEST.md`

## Frontend acceptance

- `node --check Frontend/DanmuSmartMatch.CustomCssJS.js`: PASS
- `node --check Frontend/DanmuSmartMatch.RegressionTests.js`: PASS
- `node Frontend/DanmuSmartMatch.RegressionTests.js`: PASS — `Danmu smart-match frontend regression checks passed.`
- Real-DOM compatibility regression: PASS locally — the real Series `查看候选` entry and visible return run over array-like `children` with length/index access but no iterator, `forEach`, or `some`; entry reaches the child at zero and return finds the initiating action instead of taking the zero-clamped raw fallback.
- Entry boundaries: whole-Series Season, composite temporary range, Episode source, and Movie part/version all enter at zero after content creation; ordinary direct/replacement candidate results, including empty results, use the same top rule.
- Return ladder: initiating action, logical row, enclosing section, pre-recorded logical neighbor, clamped raw offset, and non-scrollable zero are independently asserted. Action-first and row-first fixtures intentionally resolve to different offsets.
- Changed geometry: collapsed mapping disclosure, changed parent height, removed/merged logical rows, nested contexts, and last-in-first-out consumption are covered deterministically.
- Reviewer correction: a real ordinary Season candidate save returns to a rebuilt composite overview where the initiating action and row no longer exist; both display forms now share the stable Season section identity, and the regression proves section fallback preserves its original viewport-relative offset while consuming exactly one context.
- Recovery: pre-child search/detail failures consume only their provisional context; failed Episode/Movie submission restores the child with exactly one retained context; a valid accepted TaskId clears the non-returnable context.
- Isolation: candidate arrays retain server order; inline detail, busy, overview, same-page progress, scoring, filters, provider grouping, requests, selection, mapping, binding, and download contracts are not changed by viewport helpers.

## Host-history and Android command acceptance

- Desktop and Android open, internal return, visible X, Escape, force close, and ordinary close: zero `pushState`, zero `replaceState`, and zero `history.back` calls.
- Host `popstate` on either platform: every stacked Smart Match overlay is disposed with zero additional traversal and without Smart Match parent navigation.
- Android identity: trimmed/case-insensitive UA-CH platform and conventional UA token positives pass; malformed, missing, throwing, narrow-responsive, and touch-only counterexamples remain desktop.
- Android command ownership: exact `command: "back"` is canceled only for the topmost connected Android dialog, then invokes its return state machine exactly once. Secondary parent return, top-level close, busy/protected retention, nested topmost isolation, no-overlay/non-back passthrough, preexisting cancellation, missing/ineffective/throwing cancellation, and handler false/throw are covered by the complete regression.
- Listener/history isolation: one script-lifecycle capture command listener, zero Smart Match `backbutton` listeners, zero dialog history mutation, no command `stopPropagation`, no timer/second fallback, and stable host route are asserted.
- Source exclusions: no Car probe override, marker, badge, trace state/hook, r1 `CloseWatcher`, Navigation API interception, animation-frame restoration, host-scroller selector, or responsive/touch platform heuristic.
- First authorized live attempt: FAILED and rolled back — the browser exposed real `HTMLCollection` children without Array `forEach`, so navigation capture threw before the secondary page opened. The verified 2.0.6 pair was immediately restored by hash and service health returned HTTP 200. The local compatibility correction replaces every new navigation-helper Array-only children traversal with deterministic length/index loops; no r1 live acceptance claim is made until an authorized retest.
- Second authorized live attempt: FAILED and rolled back — native activation focus moved the dialog body after pointer activation but before `click`, so click-time-only capture retained the wrong viewport geometry. The verified 2.0.6 pair was immediately restored by hash and service health returned HTTP 200. The local correction uses passive trigger-local preactivation sampling and atomic click consumption; 2.0.6r1 remains not live accepted pending a separately authorized retest.
- Trigger-local timing regression: PASS — content top 706 and preactivation scroll 326 are preserved after simulated native focus moves scroll to 426 for pointer mouse/touch, legacy Android touch followed by synthetic mouse, legacy desktop mouse, Enter, Space, and expanded mapping entry. Programmatic and untrusted clicks reject an older pending sample and use current 426. Pointer/touch cancellation, context menu/long-press, drag, blur, A-to-B replacement, and changed candidate create no stale context; detached repeats across all five entries leave body, request count, and stack depth unchanged. Static guards confirm passive internal-trigger listeners contain no input cancellation, focus/scroll writes, global listeners, timers, or animation frames.
- Third authorized live attempt: DESKTOP PASS — a fresh paired 2.0.6 rollback backup re-hashed successfully with preserved owner/mode before replacement. The reviewed DLL and then-current V29 asset were staged and re-hashed, the plugin configuration remained unchanged, V28/V29 marker counts became 0/1, service state returned active, and both local and external web health returned HTTP 200.
- Desktop large-library acceptance: PASS — in an 826-item virtual library, the visible scroller remained exactly 670 before opening the item menu, after opening Smart Match, and after closing it; the route and 48 rendered cards remained unchanged. There was no first-page reset, route traversal, or virtual-list rebuild.
- Desktop secondary navigation: PASS — ordinary candidate inspection opened at 0 and returned to the initiating action with 0 px error; a rematch child opened at 0, was scrolled to 310, and returned with 0 px error. With a disclosure expanded, the parent changed from height 1765/top 1199/action offset 475.59375 to a collapsed height 1004/top 437/action offset 476.34375, preserving the semantic action within 0.75 px rather than restoring the obsolete raw offset.
- Desktop activation-timing reproduction: PASS — the temporary-range manual action was measured at parent height 992, scroll 326, content top 706.8125, and viewport offset 380.8125. Its child opened at 0, was scrolled to 260, and returned to scroll 326 and the exact 380.8125 action offset. This is the live sequence that previously returned to the native-focus maximum 426. Enter activation also opened its child at 0 and returned to the focus-adjusted parent position; exact mouse/touch/keyboard preactivation timing remains locked by the deterministic matrix above.
- Bounded desktop diagnostics: PASS for the then-current V29 change — no Smart Match navigation/HTMLCollection error signature appeared in the bounded client/server windows and final health remained HTTP 200; unrelated CustomJS errors were not attributed to this asset.
- Android no-history diagnosis: replacing dialog-owned history restored overlay opening on the car head unit and stopped the same-route refresh, but a separate Smart Match `backbutton` listener then caused Smart Match and Emby to return together on both phone and car. This disproved the history/backbutton compatibility contract.
- Diagnostic C command trace: the host dispatched cancelable `command: "back"` before `backbutton`; canceling the command kept the Emby page fixed and prevented the downstream event, while the diagnostic intentionally left Smart Match unchanged.
- Diagnostic D command owner: after successful command cancellation, invoking the topmost Smart Match handler exactly once restored child-to-parent and top-level-close behavior without moving the Emby page. The user confirmed the expected behavior on both phone and car head unit, including normal host Back after the overlay was closed.
- Formal V30 deployment: ACTIVE — a fresh paired rollback of the D diagnostic frontend configuration, its extracted JavaScript, the deployed DLL, and plugin configuration re-hashed with owner/mode preserved; the prior formal-r1 rollback also re-read as a directly usable paired set. The first formal replacement attempt was rejected only because its gate compared the BOM-inclusive package hash with the established BOM-free embedded-script hash; automatic rollback restored D and HTTP 200 before retry. The corrected gate verified both representations, atomically installed V30, restarted Emby, and returned local/external HTTP 200. Package JavaScript SHA-256 is `86441706cec694fe4e6dbf976e41509177ff3a414b3c337ce7da8834ed35bcee`; deployed BOM-free readback is `80998112faed9606e2d79845d607487a5778a372dbf43e0dfa5840f6861b48eb`. V30/V29/Car marker counts are 1/0/0, the command listener is one, Smart Match `backbutton` and history mutation are zero, DLL and plugin configuration hashes are unchanged, five rollback sets remain, and deployment temporaries are zero.
- Formal V30 desktop smoke acceptance: PASS — the authenticated Series detail exposed exactly one Smart Match entry and no diagnostic trace UI; opening the formal overlay retained the exact Emby route, produced one connected overlay with the expected title, and visible X removed it while retaining the route. No binding, download, or metadata action was selected.
- Formal phone/car boundary: the formal V30 asset is active, but phone and car-head-unit command-back acceptance remains pending a cold-start device recheck; the earlier D result remains design evidence rather than formal acceptance.

## Identity and pairing

- Assembly version: `2.0.6.0`
- File version: `2.0.6.1`
- Informational/configuration/TMDB User-Agent: `2.0.6r1`
- Derived configuration cache token: `2-0-6r1`
- Frontend installation marker: V30 exactly once; V29 and every Car probe marker zero
- Mapping protocol: V22 unchanged
- Frontend source/review-package pairing: PASS — byte-identical V30 assets retain V22 exactly; the embedded configuration page/script and cache token remain unchanged at `2-0-6r1`.

## Backend and package slots

- Main backend regression: PASS — `Danmu plugin regression checks passed.` The first run correctly failed only because the TMDB User-Agent fixture still expected 2.0.6; after changing that version-only expectation to 2.0.6r1, the complete rerun passed.
- R4 identifier regression: PASS — `R4 identifier-free metamorphic regression checks passed for 9 identifier sets.`
- Title fidelity regression: PASS — `Title fidelity regression checks passed.`
- Search/navigation/cancellation/configuration assertions: PASS — R3 search-quality, search-term policy, bounded-search policy, Episode selection, R4 parent-season context, R5 target-season scope, temporary-range policy, MGTV search, full frontend workflow, and main generated configuration-resource checks all passed sequentially. One first specialty `--no-restore` launch stopped before testing because its local assets file was absent; standard restore completed and every specialty was then rerun successfully.
- Clean sequential Release build: PASS — `dotnet clean Emby.Plugin.Danmu.sln -c Release`, then `dotnet build Emby.Plugin.Danmu.sln -c Release --no-restore`; 131 existing warnings, 0 errors.
- Managed version and no-PDB/private-CodeView scan: PASS — Assembly `2.0.6.0`, File `2.0.6.1`, Product `2.0.6r1`; 0 PDB files, 0 PE CodeView entries, 0 rooted-PDB strings, and 0 private absolute-path matches.
- Final DLL: 1657856 bytes; SHA-256 `a23442fe796857d91d32d112ab7778fbb3ba29c3ffe96f16128b8919f121172f`
- Final JavaScript: 249568 bytes; SHA-256 `86441706cec694fe4e6dbf976e41509177ff3a414b3c337ce7da8834ed35bcee`
- Strict OpenSpec/diff/allowlist/credential scans: PASS — strict validation reports the change valid; `git diff --check` passes; the r1 delta is limited to frontend source/regression, version pairing, the TMDB version-only fixture, cumulative README/UPDATE, r1 OpenSpec task state, and `artifacts/2.0.6r1`. Frozen scorer, title-fidelity, R4 identifier, and every frozen 2.0.6 artifact remain byte-identical.
- Preservation checks: PASS — `artifacts/2.0.6` tree remains `39620afe3c634696f30670a1ecc0f93071521fed833ce687b0f5105f5ac7fd7c`; README media references remain 3/3; no prior UPDATE heading is missing; the original baseline branch ref remains at its recorded OID, while the current V30 worktree contains only the 13 listed allowlisted changes.
- Credential/package scans: PASS — no r1 net-new high-confidence credential assignment; the one matching test fixture existed at HEAD and its count remains 1/1. Review package contains 0 private absolute-path matches; DLL-specific PDB/CodeView/path scans are also 0. Values were never printed.
- Local review-package re-read: PASS — exactly DLL, matching V30 JavaScript, `SHA256SUMS.txt`, cumulative UPDATE, and this verification record; all three stable checksum entries re-hash successfully. VERIFICATION is intentionally not self-hashed because writing its digest into itself changes the file.

## Approval gates

- Local implementation and deterministic tests are authorized.
- NAS/Emby connection, paired backup, file replacement, restart, and authenticated desktop/Android testing are explicitly authorized. At this checkpoint the verified D diagnostic asset remains active with paired rollback sets retained; the formal V30 package has not yet replaced it.
- Push, merge, tag, publication, and release remain prohibited until separately authorized.
- Diagnostic D passed on the real modified Android phone and car head unit, but formal V30 acceptance remains incomplete until the packaged asset is deployed and rechecked.
