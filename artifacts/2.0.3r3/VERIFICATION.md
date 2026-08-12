# Emby.Plugin.Danmu 2.0.3r3 release verification

## Source baseline and approval

- Release worktree: `C:\Users\mouxi\Documents\Codex\emby-plugin-danmuplus-2.0.3r3`.
- Baseline commit: `48fdaa986b5c10eca73bb692e0fe63ef123c2935`.
- Baseline Git tree: `1996849f6b8132af9cc0747f33af4279ec8ab210`.
- The packaged files were copied from the final reviewed r3 working-tree outputs; the baseline commit remains the branch anchor because the accepted r3 delta is not committed at packaging time.
- OpenSpec task 9.3 records Sol's read-only final design/code approval with every P0/P1 closed before packaging. No product or planning file was changed by task 9.4.
- Existing evidence is retained under `evidence/`.

## Paired release assets

- `Emby.Plugin.Danmu.dll`
  - Size: 1,447,936 bytes.
  - SHA-256: `9D95F7952BC19050B8D6F54002EA1807EFA3B01303A19DE0739736FB1784CF71`.
  - Byte-identical to `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll`.
  - Assembly version: `2.0.3.0`.
  - File version: `2.0.3.3`.
  - Product/informational version: `2.0.3r3`.
- `DanmuSmartMatch.CustomCssJS.js`
  - Size: 162,814 bytes.
  - SHA-256: `43046E3BC81A42C2365C4129B6FD01163634A26547BE7F7FD1312806DBE6CEC9`.
  - Byte-identical to `Frontend/DanmuSmartMatch.CustomCssJS.js`.
  - Cache/install marker: `__embyDanmuSmartMenuV19` exactly once; V18 and V17 are absent.
  - The updater reads the UTF-8 BOM/CRLF candidate with `utf-8-sig` and writes normalized LF content. The resulting unescaped script payload is 159,995 bytes with SHA-256 `4BBF773607A7BDBC5EDFCAE6803A164EA031432642A597E429B53B6CDFC19E3C`.
- `update_customcssjs.py`
  - Size: 2,744 bytes.
  - SHA-256: `F7BE82F3EA0E6710B695588D425E82A0AE8318FABCB13864C14D3199ABCDF42E`.
- `restart_emby.sh`
  - Size: 999 bytes.
  - SHA-256: `BE2465BDA563693A7E7D6397C8060E59EB0B254D318142063D6941E9648D6838`.
  - Byte-identical to the live-verified r2 restart helper and LF-only.

The DLL and V19 JavaScript are one release pair. Never deploy or roll back only one member of the pair.

## Compatibility

- Production target: Emby Server `4.9.3.0`.
- Target framework: `.NET Standard 2.0`.
- C# language version: `8.0`.
- The assembly identity remains `2.0.3.0` for Emby compatibility; `FileVersion=2.0.3.3` and `InformationalVersion=2.0.3r3` identify this release.

## Test and review matrix

OpenSpec task 9.2 was complete before packaging and records passing results for:

- Release solution build.
- Full backend regression suite.
- All focused search and mapping suites.
- Frontend syntax and regression suite.
- r3 scope gate.
- `git diff --check`.
- Strict OpenSpec validation.

Task 9.4 packaging checks also passed:

- Python syntax compilation for `update_customcssjs.py`.
- Node syntax check for the packaged JavaScript.
- Unique V19 marker check and absence of V18/V17.
- Source-to-asset byte equality for both DLL and JavaScript.
- Restart helper byte equality with the r2 live-verified helper and LF-only check.
- Positive V18-to-V19 migration, including preservation of unrelated `<content>` entries and exact decoded candidate content.
- Refusal without modification for missing V18, duplicate V18, pre-existing V19, a V18 marker outside `<content>`, and a candidate carrying the old V18 marker.

## Strict V18-to-V19 update contract

Run `update_customcssjs.py CONFIG_XML SMART_MATCH_JS` only after the r2 rollback trio below has been copied and hash-verified. The updater refuses to run unless:

1. The deployed configuration contains exactly one V18 marker, inside exactly one `<content>` entry, and contains no V19 marker.
2. The candidate script contains exactly one V19 marker and no V18 marker.

Only the matching entry's escaped content is replaced. The updater preserves file mode and ownership, writes and `fsync`s a same-directory temporary file, commits with `os.replace`, and `fsync`s the directory on POSIX. It does not replace `Emby.CustomCssJS.dll` and does not install a prebuilt whole-XML configuration.

## Exact paired r2 rollback contract

The read-only pre-r3 r2 baseline consists of these three active files:

- `/volume2/@appdata/EmbyServer/plugins/Emby.Plugin.Danmu.dll`
  - Expected SHA-256: `617D4491D9B5726EA04B9571CC1B53EA9EA7D3AB7A3BD235A9A9002EDB493912`.
- `/volume2/@appdata/EmbyServer/plugins/configurations/Emby.Plugin.Danmu.xml`
  - Expected SHA-256: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`.
- `/volume2/@appdata/EmbyServer/plugins/configurations/Emby.CustomCssJS.xml`
  - Expected SHA-256: `00D056C30D0B406551222524AEDC0A9F7107BD2B2B58F7D0872DEF648F11ED3D`.
  - Expected markers: V18 exactly once and V17 absent.

Task 9.5 must create the following exact immutable rollback copies before changing any active file, and must abort if the source hashes differ or any destination already exists:

- `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r3-predeploy-r2-baseline/Emby.Plugin.Danmu.dll.r2`
- `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r3-predeploy-r2-baseline/Emby.Plugin.Danmu.xml.r2`
- `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r3-predeploy-r2-baseline/Emby.CustomCssJS.xml.r2`

The three copies must retain the corresponding hashes above. They are a coherent rollback set: stop Emby, restore all three to their active paths, verify all hashes plus V18=1/V19=0, restart with the supplied helper, require HTTP 200, and confirm the startup log loads `Emby.Plugin.Danmu, Version=2.0.3.0` without plugin load errors. Do not treat the current active paths as rollback storage, and never restore only the DLL or only the browser configuration.

Rollback restores release binaries/configuration only. It does not recreate season-level plugin bindings intentionally cleared after a successful composite download; their absence continues to force a fresh search. Foreign metadata identifiers remain outside the cleanup set.

## Packaging boundary

Task 9.4 performed no NAS write, deployment, restart, metadata mutation, or download. Live backup/deployment/verification remains task 9.5 and later.

## Synology deployment and live verification

Deployment completed on Emby Server 4.9.3.0. Before stopping the service, the active r2 trio matched the expected hashes above and the browser configuration contained V18 exactly once and no V19. After stopping Emby, a consistent backup was created at:

`/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r3-predeploy-r2-baseline`

The backup contains the immutable r2 trio, `library.db` (SHA-256 `F862AF96E9AFB7D11680A36803FDDE3DC409337133328695A46E6D31CE8B3464`), the server configuration tree, and the complete pre-deployment composite-state snapshot. WAL/SHM were absent after the clean stop. The r2 trio hashes were verified before and after copying. The exact rollback dry-run copied all three backup members to an isolated temporary directory, verified their three expected hashes plus V18=1/V19=0, and removed only that temporary directory without switching the active files.

The deployed state after two restarts is:

- DLL SHA-256: `9D95F7952BC19050B8D6F54002EA1807EFA3B01303A19DE0739736FB1784CF71`.
- Danmu XML SHA-256: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`.
- CustomCssJS XML SHA-256: `49F3F66B543C5D32FA7024CBA6C9B28284454E65773756F0180AA2D9B5BF9F7C`.
- Browser markers: V19=1 and V18=0.
- HTTP health: 200; reported Emby version: 4.9.3.0.
- Startup log: `Emby.Plugin.Danmu.ServiceRegistrator` starts and completes without a plugin-load error.

### Real Spy x Family read-only preview

The production Series `453808` and its Seasons `453907`, `453809`, and `503419` were previewed without binding or download. The first whole-Series preview completed in 17,528 ms. Every Season returned `partial-confident` rather than `unknown/search-incomplete`; Dandan, Bilibili, Youku, Iqiyi, and Tencent completed, while the existing Mgtv 403 remained an explicit failed-provider diagnostic.

All Series and singleton-Season previews used the same configured candidate-bearing display group order (Mgtv produced no candidates because its requests failed explicitly):

`DandanID > BilibiliID > YoukuID > IqiyiID > TencentID`

Each Season returned 21 eligible candidates. Youku contributed only four relevant Spy x Family titles per Season instead of the r2 baseline's 17-37 results with 13-33 obvious unrelated entries. Logs show the identity-bearing term `间谍过家家`; no bare `第 N 季`, `Season N`, dynamic clause, or upstream-returned alias term was issued. The three real Seasons retained their 25/12/10 Episode counts, and all 47 real Episodes still had zero plugin-owned ProviderIds after the read-only previews.

Selecting Dandan `16947` for the real first Season produced 12 exact mappings and one 13-Episode unmatched run. The plan reported `SeasonBindingUnsafe=true`, `CanPersistCompleteSeasonBinding=false`, and retained match score `0.844` with `search-confidence` provenance. No production metadata was written.

### Authenticated browser UI smoke

The signed-in Emby web client was opened in the in-app browser on the production Spy x Family Series page. The main Series **More** menu contained the injected V19 action `智能匹配并下载整部剧弹幕`. Opening it loaded the `整部剧弹幕智能匹配` dialog without starting a download. The completed dialog showed all three Seasons, the explicit Mgtv failure diagnostic, per-mapping confidence and provenance, `查看逐集映射`, `重新匹配`, and `移除` controls, plus the final download button. The dialog was then closed without invoking download.

The browser console contained a pre-existing CustomCssJS lifecycle error at anonymous line 5347 (`onViewShow` writing `itemId`). A read-only inspection of the live XML traced it to the separate `<name>danmuku</name>` entry: that script creates `window.ede` only for a `video-osd` view but writes `window.ede.itemId` for every `viewshow`. The deployed Smart Match entry is only 3,026 source lines and contains no `onViewShow`, `viewState`, or `itemId` assignment; opening and completing the V19 dialog produced no Smart Match stack frame. This unrelated user script does not prevent the V19 menu injection, preview, rendering, or dialog close demonstrated above, and it was not modified as part of this release.

### Disposable 25-Episode Part 1/Part 2 fixture

An isolated virtual TV library `521329` was created under the verified non-symlink root `/volume1/NAS/__DanmuPlusFixture_2.0.3r3__`. It contained Series `521331`, Season `521332`, and Episodes `521333`-`521357` with IndexNumber 1-25.

- Series and singleton-Season selection of Dandan Part 1 `16947` both produced 12 mappings plus one 13-Episode unmatched temporary group.
- Adding Dandan Part 2 `17061` at local E13 produced 25 mappings, zero unmatched runs, and two groups (12 + 13). Local E13 mapped to source `170610001` / source E1.
- The selected Part 1 and Part 2 groups preserved their own server scores (`0.7615` and `0.7735`) and `search-confidence` provenance.
- A malformed single-object selection payload was rejected before task creation with “复合季选择参数必须是 JSON 数组”; it wrote nothing and did not fall back to positional Season download.
- Explicit partial confirmation processed exactly E1-E12: task `e70390e759194757ab5ffb83f895ed17` completed 12/12, wrote 12 XML files and 12 exact Dandan Episode ids, and left E13-E25 untouched.
- On the first persisted partial file, all six plugin Season keys and all six Manual keys were logged and removed with their exact old values. `ForeignPluginID=keep-r3` remained unchanged.
- The iterative full task `3fbca39c1501498a88de4854b367b247` rebuilt a 25-mapping/two-source plan, skipped the existing first 12 files, and successfully downloaded only Part 2 E1-E13 to local E13-E25. Final exact Episode ids were 12 from parent `16947` and 13 from parent `17061`.

After restarting Emby, marker restoration rebuilt 25 mappings, zero unmatched runs, and two exact groups. Both groups displayed score 1 with `exact-episode-id` provenance; the invalid Season binding was not recreated and the foreign id remained. The Episode Id/IndexNumber membership hash was unchanged before and after download/restart: `ECB7FC5E822B859B01461FB2CA849660031370D3FEE82E5BA247EB6E050CD802`; every ParentId remained `521332`.

Cleanup occurred in the required order: delete virtual library `521329`, confirm Season `521332` returns 404, validate the root with `realpath` and reject symlinks, validate exactly one marker with fixture Season GUID `683802d49be16e37ebd02b7a08ba56a8`, then remove only that marker and the exact fixture root. The pre-existing non-fixture live marker remains, the fixture root and staging directory are absent, and final Emby health remains HTTP 200.
