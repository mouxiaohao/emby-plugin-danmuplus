# Emby.Plugin.Danmu 2.0.3r2 Release Verification

Local release artifacts were prepared on 2026-08-11 and deployed to the authorized Synology/Emby 4.9.3.0 host on 2026-08-12. The live verification below used an isolated disposable TV library and did not modify the production Frieren season structure.

## Paired release assets

The DLL and CustomCssJS file are one inseparable release pair. Do not deploy either asset with a script from another release.

- `Emby.Plugin.Danmu.dll`
  - Size: 1,434,112 bytes
  - SHA-256: `617D4491D9B5726EA04B9571CC1B53EA9EA7D3AB7A3BD235A9A9002EDB493912`
  - Target framework: .NET Standard 2.0
  - Language version: C# 8.0
  - Assembly/File/Informational versions: 2.0.3.0 / 2.0.3.2 / 2.0.3r2
- `DanmuSmartMatch.CustomCssJS.js`
  - Size: 154,250 bytes
  - SHA-256: `2EC9A174638444370374C192660F099C60B522D333D62D8BA7F35613AF98F174`
  - Cache/install marker: `__embyDanmuSmartMenuV18` (exactly once)
- `update_customcssjs.py`
  - SHA-256: `5B7A50BF9155C9D3CC3F53FB924FFE975971868BB7EE1503911C7F4FD7AA2B22`
- `restart_emby.sh`
  - SHA-256: `BE2465BDA563693A7E7D6397C8060E59EB0B254D318142063D6941E9648D6838`
  - Line endings: LF only
  - Byte-identical to the live-verified r1 restart helper

Compatibility target: Emby Server 4.9.3.0 on Synology DSM, matching the verified r1 host. The project remains `netstandard2.0` with `LangVersion` 8.0.

## Bounded-search production constants

- Per-provider call timeout: 10 seconds.
- Interactive operation timeout: 30 seconds.
- Automatic operation timeout: 45 seconds.
- Global provider concurrency: 3.
- Per-site concurrency: 1.

Queue time is part of the enclosing 30/45-second operation budget. A timed-out legacy provider retains its gate lease until its actual task settles.

## Local deterministic verification

- Release solution build: passed, 0 errors and 131 existing warnings.
- Full backend regression suite: passed.
- Bounded-search/concrete-provider focused suite: passed.
- Frontend JavaScript syntax check and unique V18 marker check: passed.
- Atomic updater V17-to-V18 migration test passed; missing V17, duplicate V17, pre-existing V18, outside-content V17, and V17-bearing candidate refusal tests passed.
- Updater Python syntax check: passed.
- Restart helper LF check: passed; its SHA-256 is identical to the live-verified r1 helper. A local `sh -n` rerun was unavailable because WSL is not installed.
- `git diff --check` for implementation scope: passed with existing line-ending notices only.

## Live Synology deployment and smoke verification

- Pre-deployment backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r2-predeploy-20260812-043658`.
- The backup contains the r1 DLL (`AB8D9F...46BDF`), Danmu XML (`A3BE89...C973`), CustomCssJS XML (`21161A...18BAA`), a consistent `library.db` backup, and the pre-live composite-state archive.
- Installed r2 DLL SHA-256: `617D4491D9B5726EA04B9571CC1B53EA9EA7D3AB7A3BD235A9A9002EDB493912`.
- Installed CustomCssJS XML SHA-256 after the final frontend hotfix: `00D056C30D0B406551222524AEDC0A9F7107BD2B2B58F7D0872DEF648F11ED3D`.
- Live marker counts: V17 = 0, V18 = 1. The Danmu configuration remained byte-identical (`A3BE89...C973`).
- Emby restarted successfully, `/emby/System/Info/Public` returned HTTP 200, and the startup log loaded `Emby.Plugin.Danmu, Version=2.0.3.0` without a Danmu load error.

## Disposable composite-season live verification

- Isolated library IDs: library `521285`, Series `521288`, Season `521289`; 39 Episodes were `521290` through `521328` with indexes 1 through 39.
- Authoritative plan: 39 mappings, zero unmatched, four maximal contiguous cards: Dandan parent `17617` (E1-E28), Dandan parent `18886` (E29-E33), Bilibili exact Episode `816615` (E34), and Dandan parent `18886` (E35-E39).
- Both whole-Series and Season dialogs showed all four cards with `view episode mappings`, `rematch`, and `remove` actions.
- Removing E35-E39 returned only those five ItemIds to an unmatched temporary season. The final frontend hotfix preserved the `restore` action even when the server changed display season metadata; Restore rebuilt the original direct mapping without affecting the other cards.
- Rematching E35-E39 searched immediately with a range-scoped request. Selecting Dandan candidate `18886` with source start 6 produced a verified replacement mapping for source Episodes `188860006` through `188860010`.
- Forced exact replacement download finished with 38 successes and the expected one Bilibili-source failure at E34. The server log records the E35 request for `188860006` and the identical Episode ProviderId write.
- First persisted composite output cleared the seeded Dandan Season binding/manual binding while preserving foreign `Tvdb=fixture-keep-r2`. The composite marker contained only version, SeasonId, and fingerprint.
- Final membership remained unchanged: 39 Episodes, stable membership SHA-256 `3836ECF85A67486C764B5FD1C50CE3464B97ED72DFB639A0E3600D1A37479303`, first tuple `521290|1|521289|521288`, last tuple `521328|39|521289|521288`.
- A real production Episode without a plugin Episode ID but with a plugin-bound Season was forced through search: it did not claim Episode-local evidence, returned 60 ordered candidates in 5.6 seconds, and fetched details only for the one selected candidate (16 ms). Its Episode/Season metadata and membership were unchanged.
- Cancellation was verified through `CancelSearch`: a pre-cancelled operation terminated its subsequent request in approximately 103 ms instead of running to the 30-second interactive budget.
- Cleanup was ordered as library removal, API disappearance check, exact non-symlink root validation, fixture-only marker SHA/SeasonId validation, then deletion. The virtual library, `/volume1/NAS/__DanmuPlusFixture_2.0.3r2__`, and its marker are absent after cleanup.

## Live provider-call audit

- Default Season search: exactly two calls per enabled provider, using only `葬送的芙莉莲` and `第 1 季`; completed in 6.294 seconds.
- Explicit Season and Episode searches: exactly one call per provider, using only the explicit keyword.
- Default and explicit Movie searches: exactly one call per provider, using only the movie title.
- No punctuation-clause or upstream-result alias appeared in diagnostics. Provider failure (observed MGTV HTTP 403) marked the round incomplete, disabled automatic selection, and still returned promptly.
- The automatic-search focused regression uses the same production engine/coordinator and proves the 45-second budget, the same fixed terms/call counts, and zero bind/download for any incomplete round.

## Paired rollback dry run

The exact r1 rollback trio was copied to `.../staging/danmu-2.0.3r2-20260812-043658/rollback-dryrun` and verified without changing the live r2 installation:

- DLL: `AB8D9FE28CB73AB96D5ED88F113AD42AA3E44C9E9F893F4735DD8939E6465BDF`.
- Danmu XML: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`.
- CustomCssJS XML: `21161AAEA137F7CF80C7D3CCA41F8FC2FC248FAE2AFC1A97607A1153A1618BAA`, V17 = 1 and V18 = 0.

## Safe CustomCssJS V17-to-V18 migration contract

`update_customcssjs.py CONFIG_XML SMART_MATCH_JS` refuses to run unless both conditions hold:

1. The deployed r1 configuration contains exactly one V17 marker inside one `<content>` entry and no V18 marker.
2. The candidate script contains exactly one V18 marker and no V17 marker.

It migrates only that V17 entry's escaped content to V18. It does not replace `Emby.CustomCssJS.dll` and does not use a prebuilt whole-XML configuration. The updated XML is written to a same-directory temporary file, flushed with `fsync`, and committed with `os.replace`; missing/duplicate V17, any pre-existing V18, or a marker outside `<content>` are hard failures.

## Exact paired r1 rollback baseline

Known verified r1 release pair:

- r1 DLL artifact SHA-256: `AB8D9FE28CB73AB96D5ED88F113AD42AA3E44C9E9F893F4735DD8939E6465BDF`.
- r1 CustomCssJS artifact SHA-256: `69130FC8CC76124BB4C0CB5076F06C0B10C3FFA6693DD2C8B5F66008F17A1696`.
- r1 marker: V17 exactly once; V15 absent.
- r1 installed Danmu configuration SHA-256: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`.
- r1 installed CustomCssJS configuration SHA-256: `21161AAEA137F7CF80C7D3CCA41F8FC2FC248FAE2AFC1A97607A1153A1618BAA`.

Verified rollback sources and paths:

- Full pre-r1 baseline directory: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r1-predeploy-20260812-003550`
  - `Emby.Plugin.Danmu.dll.before`: `2C31BD410A8A5CA1C74AD313A021CE5F5A4F5D68AE01809D5931CA99DB23B796`
  - `Emby.Plugin.Danmu.xml.before`: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`
  - `Emby.CustomCssJS.xml.before`: `84199C09C7C895DF5DC5FA6DB85BB49C8066E1CE1D86DC4CBade8177F6D8428C`
- Final verified r1 DLL rollback copy: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r1-dandan-direct-final-20260812-020000/Emby.Plugin.Danmu.dll.before`

Before any r2 deployment, create a new backup of the currently installed r1 DLL, Danmu XML, and CustomCssJS XML and record all three hashes. To roll back r2, stop Emby, restore the backed-up r1 DLL and r1 CustomCssJS content as a pair, restore the r1 Danmu configuration if required, restart Emby, then verify the expected r1 hashes, V17/V18 marker counts, startup log, and HTTP 200. Never restore only one member of the DLL/browser pair.

Season-level plugin bindings intentionally cleared by a successful composite download are not recreated by DLL rollback; their absence forces a fresh search. Foreign metadata identifiers are outside the cleanup set.

## Final packaging hashes

- `update_customcssjs.py`: `5B7A50BF9155C9D3CC3F53FB924FFE975971868BB7EE1503911C7F4FD7AA2B22`
- `restart_emby.sh`: `BE2465BDA563693A7E7D6397C8060E59EB0B254D318142063D6941E9648D6838`
