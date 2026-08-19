# 2.0.6r2 verification

## Boundary

This record covers local deterministic implementation, review-package verification, authorization-gated deployment, and desktop live acceptance. The reviewed r2 pair is active and healthy; Android phone and car-head-unit acceptance remains pending, so r2 is not yet fully live accepted. No commit, push, merge, tag, PR, release, publication, or cleanup was performed. The user explicitly authorized r2 deployment and live acceptance, while every Git/publication/cleanup action remains a separate approval gate. Tasks 5.2–5.4 are complete; tasks 5.5–5.6 remain pending.

Formal r1 V30 remains the rollback predecessor. Its checkout, index, artifact tree, OpenSpec change, deployed/rollback evidence, and the isolated r2 copy of all r1 historical evidence were treated as read-only. See `BASELINE-MANIFEST.md` for the exact 13-path seed inventory and `fefa366f16020da99af1b8d67863c542c433f2ab9a4be443f82e5d0e9259f2bd` digest.

## Live baseline and rollback checkpoint

- The active frontend contained V30 exactly once, V31 zero times, no Car probe marker, and mapping protocol V22 exactly once. Its extracted LF/no-BOM SHA-256 was `cce0393b562c68827db4e15361819881a04f01f95db439489df4ac907be35cc4`; adding the local source BOM after CRLF-to-LF normalization reproduced the formal r1 source hash, proving the apparent raw-hash difference was XML newline/BOM normalization rather than code drift.
- The active DLL SHA-256 was `a23442fe796857d91d32d112ab7778fbb3ba29c3ffe96f16128b8919f121172f`, matching formal r1. The active frontend configuration and plugin configuration were also hashed and their owner/mode recorded without storing host paths or configuration contents in this artifact.
- A new persistent four-file paired rollback was created and re-read: exact active frontend XML, DLL, plugin configuration, and extracted V30 JavaScript. All four hashes matched the active predecessor; copied file metadata was preserved and the rollback directory is owner-only.
- Four earlier diagnostic rollback sets were re-read; each retained a non-empty frontend XML, DLL, plugin configuration, and exactly one corresponding JavaScript asset, so all four remain directly usable. No active file was replaced during task 5.2.

## Deployment and desktop acceptance

- Traditional SCP was used only after the root SFTP subsystem closed without transferring data. The two staged files were accepted solely after exact size/SHA-256 and V31/V22/zero-probe gates passed.
- Emby was stopped, the reviewed frontend content and DLL were atomically replaced with an automatic paired-r1 rollback trap, and the service restarted successfully. Local and external health endpoints both returned HTTP 200. Readback reproduced the reviewed normalized frontend content and exact DLL SHA-256; V31 was present once, V30 and Car probes zero, V22 once, file owner/mode were preserved, plugin configuration was unchanged, and deployment temporaries were zero. The exact reviewed DLL hash provides the independently verified File/Product identity; the settings XML does not persist a release-version field.
- A cold desktop reload exposed exactly one Smart Match action. `CSS.supports("overscroll-behavior-y", "contain")` returned true; computed overlay/card/body Y-axis styles were all `contain`, overlay overflow was `hidden`, and body flex/min-height/overflow were `1 1 auto`, `0px`, and `auto`.
- With a scrollable detail host at 0, short-body wheel input from body, header, footer, card margin, and backdrop left the host at 0 and the route unchanged. A long 956/566 body scrolled to 280, then its 390 maximum, remained at 390 on further downward input, returned through 210 to 0, and remained at 0 on further upward input; the host stayed 0 throughout.
- Series whole-show, Season, Episode, and Movie workflows were opened without selecting download/bind/metadata actions. Detail-entry backgrounds remained fixed. A secondary candidate page opened at body scroll 0, scrolled internally to 520, and returned to the collapsed parent without changing the host or route.
- The media-library control was opened from a non-leading card at host scroll 900. Overlay input left the library at 900; X/Escape preserved it, host Back disposed the overlay before navigating, and after ordinary close the host scrolled normally from 900 to 1120. No horizontal route or window change was observed.

## Implemented contract

- `.danmuSmartOverlay`: retains the shared fixed flex overlay and adds `overflow:hidden;overscroll-behavior-y:contain`.
- `.danmuSmartCard`: retains dimensions, flex column, safe-area/mobile behavior and clipping, and adds `overscroll-behavior-y:contain`.
- `.danmuSmartBody`: retains padding, `overflow:auto`, line height, sticky summary and content layout, and adds `flex:1 1 auto;min-height:0;overscroll-behavior-y:contain`.
- The policy is Y-axis-only and shared by Series, Season, Episode, and Movie entry paths. It adds no global input listener, host scroll lock/read/write/fallback, `touch-action:none`, `contain:strict`, timer, animation frame, action-sheet delay, or Android/detail-page branch.
- V31 is present exactly once; V30 and all Car probe markers/hooks are absent; numeric mapping protocol V22 is present exactly once.
- Assembly/File/Product/configuration/TMDB/cache identities are `2.0.6.0`, `2.0.6.2`, `2.0.6r2`, `2.0.6r2`, `DanmuPlus/2.0.6r2`, and `2-0-6r2`.

## Deterministic verification

- Old-code-fails proof: after adding the new style contract but before changing production CSS, the frontend regression exited 1 at `the shared overlay must clip overflow and terminate its vertical scroll chain`. Both syntax checks passed.
- `node --check Frontend/DanmuSmartMatch.CustomCssJS.js`: PASS.
- `node --check Frontend/DanmuSmartMatch.RegressionTests.js`: PASS.
- Complete frontend regression: PASS, including shared style/static guards, detail/virtual-list topology fixtures, all four item labels and five gesture origins, short/top/middle/bottom modeled states, post-close host scrolling, all existing secondary-return behavior, Android command ownership, requests, mapping, binding and download fixtures.
- Main backend regression: PASS.
- Focused regressions, restored then run sequentially with Release `--no-restore`: BoundedSearchPolicy, EpisodeSelectionPolicy, MgtvSearch, R3SearchQuality, R4IdentifierMetamorphic, R4ParentSeasonContext, R5TargetSeasonScope, SearchTermPolicy, TemporaryRangePolicy, and TitleFidelity all PASS.
- Clean sequential Release solution build: PASS with 131 existing warnings and 0 errors.
- Strict OpenSpec validation, diff/allowlist/privacy checks, package pairing and final immutable-r1 re-read: PASS.

The fake DOM does not implement browser-native CSS scroll chaining. Its tests prove the exact stylesheet contract, entry neutrality, modeled topology/lifecycle invariants, and unchanged r1 workflow behavior. Desktop browser-native acceptance now passed with actual scroll-position evidence; phone and car-head-unit short/long/boundary, Back, safe-area, and post-close checks remain mandatory task 5.5. r2 is not fully live accepted at this checkpoint.

## Release build

- DLL: `Emby.Plugin.Danmu.dll`.
- Size: 1,657,856 bytes.
- SHA-256: `84f6b3ef71984e9bb507539ae78c3e2128b889e8297b708af952a8e43deb5f61`.
- Assembly/File/Product: `2.0.6.0` / `2.0.6.2` / `2.0.6r2`.
- PDB files: 0; PE CodeView/RSDS entries: 0; rooted PDB/private absolute paths: 0; diagnostic assets/Car markers: 0.
- Costura retains a dependency resource name ending in `.pdb.compressed`; it is not a PDB file, CodeView entry, rooted path, private path, or r2 diagnostic asset.

## Review package

The review package contains exactly five files: the reviewed DLL, matching V31 JavaScript, stable checksum manifest, cumulative UPDATE, and this VERIFICATION. `SHA256SUMS.txt` covers the three stable payload files (DLL, JavaScript, UPDATE); it intentionally does not hash itself or this self-describing verification file.

- JavaScript size/SHA-256: 249,722 bytes / `ad9d763926f105c60512b1e513bc9107c64d0791e776a2163aae1b468ebaabc5`.
- UPDATE size/SHA-256: 23,170 bytes / `5e2c6b97a781712a594a48cd1f1f2d9d27ff69750b1399e6bbb832b4e2b7c2eb`.
- The packaged DLL, JavaScript, and UPDATE are byte-identical to their reviewed source/build counterparts.
- No PDB, CodeView/private path, credentials, raw response, media identifier, temp, log, backup, or diagnostic asset is included.

Task 4.4 Sol-high review passed with no blocking findings. Tasks 5.2–5.4 baseline/rollback, deployment, and desktop acceptance passed. Android task 5.5, final bounded diagnostics task 5.6, and handoff 6.1 remain pending.
