# Emby.Plugin.Danmu 2.0.3r1 Verification

Verified on 2026-08-12 against Synology DSM 7.2.1 and Emby Server 4.9.3.0.

## Paired release assets

- `Emby.Plugin.Danmu.dll`
  - Size: 1,391,616 bytes
  - SHA-256: `AB8D9FE28CB73AB96D5ED88F113AD42AA3E44C9E9F893F4735DD8939E6465BDF`
  - Target framework: .NET Standard 2.0
  - Assembly/File/Informational versions: 2.0.3.0 / 2.0.3.1 / 2.0.3r1
- `DanmuSmartMatch.CustomCssJS.js`
  - Size: 130,465 bytes
  - SHA-256: `69130FC8CC76124BB4C0CB5076F06C0B10C3FFA6693DD2C8B5F66008F17A1696`
  - Cache/install marker: `__embyDanmuSmartMenuV17`
- `update_customcssjs.py`
  - SHA-256: `578999038251FAAFF48FD0899A80B77B4D5C4E82C2711588C1A30A94D21765AB`
- `restart_emby.sh`
  - SHA-256: `BE2465BDA563693A7E7D6397C8060E59EB0B254D318142063D6941E9648D6838`

The DLL and CustomCssJS file are one release pair. Do not deploy either asset with a script from a different release.

## Deterministic verification

The following checks passed from the isolated `v2.0.2r4` worktree:

- Release solution build: 0 errors (134 existing warnings).
- Full backend regression suite.
- Focused composite-season regression suite.
- Frontend JavaScript syntax check and deterministic regression suite.
- 2.0.3r1 scope gate against `v2.0.2r4`: 25 changed files including this verification record; no experimental r7 segment/collection files.
- Strict OpenSpec validation.
- `git diff --check` (only existing CRLF conversion notices).

Coverage includes Frieren 28+10 mapping, non-first-season composites, interior and multi-episode specials, split unmatched runs, direct Episode identity normalization, exact retry lookup, partial/all-failed persistence outcomes, provider cleanup, foreign-id preservation, generation barriers, restart markers, automatic import, Series/Season UI parity, iterative removal/rematch, and legacy single-source behavior.

## Pre-deployment backup

Backup directory:

`/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r1-predeploy-20260812-003550`

Backed-up files and SHA-256:

- `Emby.Plugin.Danmu.dll.before`: `2C31BD410A8A5CA1C74AD313A021CE5F5A4F5D68AE01809D5931CA99DB23B796`
- `Emby.Plugin.Danmu.xml.before`: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`
- `Emby.CustomCssJS.xml.before`: `84199C09C7C895DF5DC5FA6DB85BB49C8066E1CE1D86DC4CBade8177F6D8428C`

The deployed `Emby.CustomCssJS.dll` was not replaced. The updater atomically replaced only the unique smart-match `<content>` entry in `Emby.CustomCssJS.xml`.

## Deployment and rollback proof

Two compatibility problems were caught during live validation before any download or media write:

1. The first restart helper upload had CRLF line endings. Deployment stopped safely; the three backed-up files were restored and Emby returned HTTP 200 with all three baseline hashes.
2. Emby 4.9.3 binds GET query parameters by the CLR property name rather than `DataMember.Name`. The first DTO shape therefore ignored `compositeSelections`. The server was again fully rolled back; HTTP 200 and all three baseline hashes were reverified before applying the compatible scalar-property fix.

The final candidate was then deployed from remote staging after uploaded hashes were compared with the local assets. Emby restarted successfully and reported version 4.9.3.0 over HTTP 200. Startup logs recorded `Loading Emby.Plugin.Danmu` without a Danmu load failure. Final installed state:

- Danmu DLL: `AB8D9FE28CB73AB96D5ED88F113AD42AA3E44C9E9F893F4735DD8939E6465BDF`
- Danmu configuration: `A3BE897F9FB84FA19CBA5B2117A5379CCA851CA407C973`
- CustomCssJS configuration: `21161AAEA137F7CF80C7D3CCA41F8FC2FC248FAE2AFC1A97607A1153A1618BAA`
- Smart-match markers: V17 = 1, V15 = 0

The paired rollback path has therefore been executed twice, including a full service stop/start and post-rollback hash/HTTP verification.

## Live regression

Target library item: `葬送的芙莉莲`, Emby Season 1, 38 Episodes.

- Whole-Series and single-Season dialogs returned the same authoritative virtual/temporary grouping.
- Existing exact Episode evidence produced matched groups for local Episodes 2-28 and 31-33, with temporary groups for Episode 1, Episodes 29-30, and Episodes 34-38.
- Searching the Episodes 29-30 temporary group showed `弹弹play｜葬送的芙莉莲 第二季｜2026｜10 集`.
- Applying that candidate returned an authoritative composite preview with 32 mapped Episodes and 6 skipped Episodes.
- The exact mapping included local Episode 29 -> Dandan season 2 source Episode 1.
- Removing the selection returned Episodes 29-30 to an unmatched temporary group and restored the 30-mapped/8-skipped summary.
- A malformed `compositeSelections` value returned HTTP 200 with structured `invalid_request`, not HTTP 500.
- Replaying the exact valid query through the API returned `Composite=true`, 32 mappings, and the two remaining unmatched groups (Episode 1 and Episodes 34-38).
- No BindMatch or StartTrackedDownload request was sent. Emby's actual Season membership and numbering were not modified.

### Disposable-library coverage

A dedicated TV library named `DanmuPlus 2.0.3r1 Fixture` was created outside every production library root. Its three synthetic Series used isolated `.strm` and metadata files; no production media path was writable through the fixture. Emby assigned library id `521191`, Main Season id `521196`, Cancel Season id `521273`, and Interior Season id `521278`.

- Main/Frieren-style preview produced 38 exact mappings: local Episodes 1-28 to Dandan parent `17617`, Episodes 29-38 to parent `18886`, no unmatched runs, and `IsComposite=true`. Episode 29 resolved to exact source/lookup id `188860001`. Two repeated API previews were identical.
- The whole-Series and single-Season dialogs both rendered the same two virtual cards (28 + 10), no temporary unmatched card, and a 38/38 download action.
- A permission-isolated first run completed with persisted successes plus one failed Episode. The first persisted file created the private marker and cleared the Season plugin keys. Episode 38 had no new provider id while its file failed; after permissions were restored, retry task `ea5de48e81fd465d83808aad930ac78d` used exact candidate `18886`, source id `188860010`, source number 10, persisted the file, and then wrote that Episode id.
- Cancellation task `1c3802118a814ccf84c19b1d8c45a4cb` was stopped before any file persisted. Both entries ended cancelled, no XML or marker was created, and the Season plugin keys remained present.
- The same two-Episode fixture then completed as one stable Dandan source. It remained non-composite, persisted Season `DandanID=17617`, preserved TVDB metadata, and did not create another composite marker.
- The Interior fixture preview mapped source identities in the order S1 E1, S2 E1, S1 E2. It produced two stable virtual groups with the middle Episode independently reserved and no local shift.
- Production preview exercised iterative apply, remove, and repeat-preview behavior. Closing and reopening discarded the browser draft. The disposable marked Season was restarted and previewed again to verify durable-state recovery.
- Before and after download, retry, restart, and repeated preview, the Main Season retained the same Season item and the same ordered 38 Episode item ids and IndexNumbers (first `521235:1`, last `521272:38`). Emby's real Season membership was never rewritten.

### Provider metadata and restart evidence

- The composite Season was seeded with Dandan, Bilibili, Tencent, Iqiyi, Youku, and Mgtv keys plus every exact `Manual` marker and a foreign TVDB id.
- At the first persisted file, the live log recorded every removed key and its prior value, including all six providers and all six manual markers. The post-write Season retained only its TVDB id.
- There were 37 persisted XML files before the intentionally failed Episode 38 retry and 38 after retry. The failed Episode had no new provider id before its successful exact retry. The all-cancelled fixture produced zero XML files and no Episode id writes.
- The private marker was stored under `plugins/Emby.Plugin.Danmu/composite-seasons`, contained only its version, Season id, and ownership fingerprint, and had SHA-256 `A431A97BDE696A8E00C7DD99D4DC55F567DB58BA37B5DC35079DF79C2A283351`.
- After restart, a deliberately reintroduced stale Dandan Season key and manual marker were removed by the next marked preview while TVDB remained. The response still searched current candidates and, with the final Dandan direct resolver, reconstructed all 38 exact Episode mappings into stable parents `17617` and `18886`; it did not promote Episode ids into one Season binding.
- The final candidate was deployed from an LF-normalized staging helper. The prior live DLL was backed up at `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r1-dandan-direct-final-20260812-020000/Emby.Plugin.Danmu.dll.before`. Emby returned HTTP 200 as version 4.9.3.0, the loading log contained no Danmu load error, and V17/V15 marker counts were 1/0.

### Fixture cleanup

The disposable virtual-library definition was deleted first and its Season item then returned HTTP 404. The exact non-symlink root `/volume1/NAS/__DanmuPlusFixture_2.0.3r1__` and the marker whose Season id matched the fixture were subsequently removed after resolving and validating both targets. No other marker existed. The fixture is recoverable from the consistency backup at `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r1-fixture-20260812-0135`; production Frieren remained 38 Episodes with first/last items `486096:1` and `518398:38`.

## Rollback semantics

Restore the three `.before` files from the backup directory while Emby is stopped, then restart Emby and verify the three baseline hashes and HTTP 200. Restore the DLL and browser script as a pair.

Season-level plugin bindings already cleared by a successful composite download are intentionally not recreated by DLL rollback. Missing bindings force a fresh search. The cleanup log records exact removed key/value pairs if an operator must restore a prior manual binding. Foreign metadata identifiers are never part of the cleanup set.
