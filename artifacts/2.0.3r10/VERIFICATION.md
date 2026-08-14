# 2.0.3r10 verification

Status: deterministic verification and task 5.2 live acceptance are complete. The final r10 behavior candidate is active and healthy. The live inventory contained no explicit non-main Movie unit, so deterministic task 4.5 is the accepted filtering evidence and this residual live-fixture gap is recorded without claiming a live non-main pass. Local 2.0.3r10 release assets are prepared for review; task 5.3 remains unchecked until the approved commit/tag/release succeeds.

## OpenSpec progress

- Change: `release-2-0-3-r10-source-metadata-search-matching`
- `openspec.cmd validate release-2-0-3-r10-source-metadata-search-matching --strict`: passed.
- `openspec.cmd instructions apply --change release-2-0-3-r10-source-metadata-search-matching --json`: expected after task update: `total=19`, `complete=18`, `remaining=1`.
- Task 5.2 is complete under the documented residual-risk decision and final healthy r10 deployment.
- Task 5.3 remains unchecked. Version metadata and local package assets are prepared, but no commit, push, tag, or release was made.

## Deterministic verification after the metadata, work-year, and Movie-part correction

The complete 18-entry local matrix passed:

1. Main regression project, default mode.
2. Main regression project, `--movie-part-core`.
3. Main regression project, `--bilibili-search`.
4. Main regression project, `--composite-season-state`.
5. Main regression project, `--composite-season-planner`.
6. Main regression project, `--composite-season`.
7. `SearchTermPolicy`.
8. `BoundedSearchPolicy`.
9. `TemporaryRangePolicy`.
10. `EpisodeSelectionPolicy`.
11. `R3SearchQuality`.
12. `R4ParentSeasonContext`.
13. `R4IdentifierMetamorphic`.
14. `R5TargetSeasonScope`.
15. `MgtvSearch`.
16. `TitleFidelity`.
17. `Frontend/DanmuSmartMatch.RegressionTests.js` under Node.js.
18. Release build.

The Release build completed with zero errors and 131 existing warnings. The year-specific fixtures prove: a Bilibili exact PGC publication timestamp in 2023 remains `Year=null` without candidate evidence; a Bourne candidate snapshot keeps work year 2002 instead of the 2023 publication time; Dandan snapshot year 2014 fills missing exact detail; a trustworthy exact production year wins a conflicting snapshot; and Tencent/Youku/Mgtv/Dandan/Iqiyi exact production-year contracts remain unchanged. Final diff and sensitive-data audit results are recorded after the current run below.

## Current corrected local candidate identity

These are verification inputs, not packaged or published 2.0.3r10 assets:

| Input | Size | SHA-256 |
| --- | ---: | --- |
| `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll` | 1,562,112 bytes | `98e498e2c546ee5811853f2b4e00d95454c828666d3e0e3b585bdc4be08c66c5` |
| `Frontend/DanmuSmartMatch.CustomCssJS.js` | 206,696 bytes | `0ff2df87ae87afe3b05e265d4b0aa4748d0a27ad09fdb96d97caad12348b6e46` |

The deployed behavior candidate above was built before release version stamping. Local task 5.3 preparation now stamps assembly/file/product versions as `2.0.3.0` / `2.0.3.10` / `2.0.3r10`; the resulting release DLL hash is recorded in the packaging section below. The code paths are unchanged by this stamping step and the complete deterministic matrix was rerun afterward.

## Verified r9 backup

The active r9 deployment was checked before mutation and copied as a three-file recoverable set to:

`/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r9-before-r10-20260814-130725`

The backup directory was made read-only and its manifest passed `sha256sum -c` for all three files:

| Backed-up asset | SHA-256 |
| --- | --- |
| Plugin DLL | `7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308` |
| CustomCssJS configuration | `abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb` |
| Danmu configuration | `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973` |

The established rollback procedure is `artifacts/2.0.3r10/rollback_r10_to_r9.sh`: stop Emby, stage the complete backup set beside each target, atomically replace all three files, restore ownership/modes, start Emby, then require the three r9 hashes and the public health endpoint.

## First live attempt and rollback

The first candidate was deployed only after the backup verified. Emby restarted successfully, reported version 4.9.5.0, and loaded the plugin entrypoint plus all six providers.

Live checks that passed:

- Exact Dandan Episode returned parent source title `妄想学生会＊` and TV-animation category.
- Exact Iqiyi Movie returned source title and movie category.
- Bilibili discovery returned `谍影重重` 1–5 with years 2002, 2004, 2007, 2012, and 2016; every result was classified as a movie and no ordinary-video result was included.

The blocking check failed: `妄想学生会＊` and its marker-less sibling both remained at base confidence 0.85, so automatic selection stayed ambiguous. The candidate was therefore rolled back immediately; task 5.2 was not claimed.

Post-rollback verification confirmed the active DLL, CustomCssJS configuration, and Danmu configuration exactly matched the three r9 hashes above. The service was active, the public health endpoint returned Emby 4.9.5.0, and plugin/provider load logs showed no load failure.

## Corrective change awaiting second live attempt

The defect was that fidelity evidence participated only after the 0.90 threshold filter. The correction separates source-title evidence by local role:

- Season primary/alias exact match is rank 2 only when it matches a distinct, identity-bearing local Season name or Season original title.
- Parent Series name/original-title exact match is rank 1 and can only break an equal-confidence tie.
- A unique rank-2 candidate in a same-provider, same-base competition group bridges from base confidence at least 0.85 but below 0.90 to exactly 0.90 only when the bounded 0.05 evidence reaches the threshold.
- Already-confident candidates keep their base confidence; rank evidence cannot make 0.98 overtake 0.99.
- Residual discovery keeps the Series title as its keyword override while suppressing duplicate Series titles from the Season-identity channel.
- Movie fidelity remains a tie-break and does not receive the Season bridge.

Targeted live-equivalent fixtures, true-tie cases, alias/parent paths, generic Season protection, NFKC, arbitrary punctuation, 0.8499, 0.98 versus 0.99, provider priority, serialization, main regression, and the complete 18-entry matrix all pass locally.

## Movie part selection correction

- Movie discovery and scoring retain the parent Movie identity; a selected downloadable leaf is separate server-owned state.
- Bilibili uses stable PGC `ep_id`; Tencent, Youku, Mgtv, Dandan, and Iqiyi expose choices only when their exact/download paths prove a stable independently downloadable leaf.
- Explicit trailers, previews, behind-the-scenes items, specials, clips, interviews, bonus/making-of content, and equivalent clear non-main units are removed before default-first selection and option construction. Remaining indistinguishable usable parts still auto-bind to the first stable-order unit.
- Movie part evidence is short-lived and scoped to item, provider, parent candidate, and parent token. Cross-scope, stale, tampered, unregistered, and explicitly excluded choices fail closed.
- An explicitly selected leaf that throws, disappears, or returns an empty CommentId fails closed and cannot fall back to the default first part. Legacy default fallback remains available only when no explicit leaf was selected.
- Usable choices are de-duplicated and bounded to the first 64 stable-order entries before tokens are registered. Raw leaf IDs are hidden from both System.Text.Json and IgnoreDataMember-based serializers.
- UI presentation separates parent source title/year/category from optional PartTitle, de-duplicates equivalent labels with NFKC/case/whitespace normalization, and never adds the selector to Season or Episode flows.

## Resume gate

Before task 5.2 can be checked, the newly corrected candidate must be deployed through the same verified backup/atomic-replacement process and all remaining live acceptance checks must pass, especially the Bilibili detail-year correction, candidate-year fallback, representative automatic/manual/supplementary/direct temporary collection metadata, explicit non-main filtering, and default/manual Movie part selection. No NAS connection or deployment occurred during the current work-year correction run; the active deployment therefore remains the verified r9 rollback recorded below.

## Latest live attempt and rollback (2026-08-14)

The latest candidate was staged only after the active r9 DLL, CustomCssJS configuration, Danmu configuration, and the read-only r9 backup manifest all matched their recorded SHA-256 values. The staged DLL and JavaScript matched the candidate identities above, the deployment script passed `sh -n`, atomic deployment completed, and Emby restarted healthy with the Danmu entry point and all six providers loaded.

Read-only live previews passed these checks:

- Bilibili discovery returned `谍影重重` 1–5 as five parent Movie candidates with search years 2002, 2004, 2007, 2012, and 2016; no ordinary-video candidate entered the Bilibili Movie result set.
- `妄想学生会＊` became the unique automatic Dandan selection at 0.90 while the marker-less sibling remained at 0.85.
- Exact Dandan Episode preview returned the parent title `妄想学生会＊` and TV-animation category.
- Bilibili selected-candidate preview separated the parent title from `PartTitle=正片`, issued only opaque part evidence, and returned no raw leaf identity.

The blocking defect was that Bilibili selected-candidate detail returned year 2023 for every `谍影重重` Movie even though discovery had the correct production years. Because exact-detail non-empty fields correctly take precedence over the server candidate snapshot, the incorrect provider-detail year replaced the valid discovery year. The five available Bourne fixtures also exposed only one usable `正片` each, so they could not prove live non-default multi-part selection.

The candidate was immediately rolled back. Post-rollback verification confirmed the active r9 DLL hash `7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308`, CustomCssJS hash `abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb`, and Danmu configuration hash `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`. Emby is active and its public health endpoint reports version 4.9.5.0. Task 5.2 remains unchecked; no release, commit, push, or tag was made.

## Current work-year correction run (local only)

- OpenSpec now defines `Year` solely as premiere/release/first-broadcast year. Provider publication/upload timestamps are invalid; a trustworthy exact production year wins, otherwise a server-owned candidate snapshot fills the field, otherwise it remains null.
- Bilibili exact PGC `PubTime`, season `publish.pub_time`, episode publication time, and BVID `pubdate` no longer populate `Year`. Bilibili discovery mapping continues to preserve its trustworthy work-year search result.
- The full 18-entry matrix above passed again, followed by strict OpenSpec validation. Release build: 0 errors, 131 existing warnings.
- Candidate inputs now match the SHA-256 table above; `deploy_r10.sh` was updated to require the new DLL hash. Bash was unavailable locally for a fresh syntax invocation, so no claim beyond the prior syntax validation and current hash-gate text check is made.
- `git diff --check` passed (only expected LF-to-CRLF notices). The deploy-script DLL/JS hash-gate text matches the current inputs. A scan of all 52 changed/untracked files found zero private-key headers and zero credential assignments.
- No NAS, Emby, Git remote, package, tag, or release mutation occurred in this run. Task 5.2 and task 5.3 remain unchecked.

## Work-year and Movie-part live attempt (2026-08-14)

The candidate DLL `98e498e2c546ee5811853f2b4e00d95454c828666d3e0e3b585bdc4be08c66c5` was deployed only after the active r9 trio, read-only backup manifest, remote shell syntax, service, and public health checks passed. Emby restarted healthy and loaded the plugin entry point plus all six providers.

Live acceptance evidence:

- Bilibili rematch discovery and server-evidence selected previews returned Bourne 1–5 with final parent titles and work years 2002, 2004, 2007, 2012, and 2016. No result contained the erroneous 2023 detail year, no ordinary-video candidate entered the Movie set, and every selected preview separated `PartTitle=正片` from parent identity.
- `妄想学生会＊` was uniquely auto-selected from Dandan at effective confidence 0.90 while the marker-less sibling remained at 0.85.
- A Dandan exact Episode returned its upstream parent title and TV-animation category. An Iqiyi-only exact Movie returned its upstream title and Movie category. The selected Dandan candidate year 2014 survived into `SourceMetadata.Year=2014`.
- A Bourne single-part default download omitted `moviePartToken`, force-downloaded one non-empty XML with zero failures, and then restored the pre-test XML hash and timestamp state.
- Bilibili Movie `复仇者联盟2：奥创纪元` exposed two usable parts, `原版` and `中文`, with exactly one stable-order default and opaque tokens only. The non-default `中文` token force-downloaded one non-empty XML with zero failures; the task reported `PartTitle=中文`, and the original XML state/hash was restored afterward.
- The bounded live inventory contained 39 Bilibili-only PGC Movies. All 39 raw season details were inspected for authoritative badge/section classification and conservative explicit trailer/preview/behind-the-scenes/bonus labels, but none contained a clearly excluded non-main unit. Consequently no live explicit-extra fixture was reachable; deterministic task 4.5 remains the evidence for filtering before default selection.

The candidate itself did not fail any reachable live check. However, before a later instruction classified the missing extra fixture as an acceptable reachability boundary, the prior fail-safe instruction had already caused the verified rollback to run. Post-rollback hashes are r9 DLL `7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308`, CustomCssJS `abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb`, and Danmu configuration `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`. The service is active and public health reports Emby 4.9.5.0. Test XML backups were removed only after both media files matched their original state. Task 5.2 remains unchecked pending review of the unreachable live-extra boundary and, if accepted, explicit authorization for a final r10 redeployment. Task 5.3 remains untouched.

## Accepted residual risk and final r10 deployment

Final review accepted the unavailable live explicit-extra fixture as a documented reachability boundary because task 4.5 deterministically covers filtering before first selection, excluded-token rejection, and mixed main/preview/behind-the-scenes/special/clip/interview/bonus/making-of inputs. This is **not** a claim that live non-main filtering passed: the 39-title live inventory contained no explicit non-main unit to exercise.

After that decision, the same candidate was redeployed through the verified r9 preflight, backup-manifest check, remote `sh -n`, staged SHA-256 checks, atomic replacement, restart, and health gate. Final active state:

| Active asset | SHA-256 |
| --- | --- |
| Plugin DLL | `98e498e2c546ee5811853f2b4e00d95454c828666d3e0e3b585bdc4be08c66c5` |
| CustomCssJS configuration | `1b8d163e856a207340bd0262688a77425052fda201342f06efe737565333eb4d` |
| Danmu configuration | `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973` |

The service is active, public health reports Emby 4.9.5.0, the plugin entry point started and completed, and all six provider registrations appeared in the startup log. Final read-only smoke confirmed Bourne 1 resolves to parent title `谍影重重`, final year 2002, and `PartTitle=正片`; `妄想学生会＊` remains the unique automatic Dandan choice at 0.90 versus 0.85 for the marker-less sibling. No download was repeated after final deployment. Task 5.2 is complete under the documented residual-risk decision.

## Published 2.0.3r10 review release

Release metadata is now `AssemblyVersion=2.0.3.0`, `FileVersion=2.0.3.10`, and `ProductVersion=2.0.3r10`. After version stamping, the complete 18-entry deterministic matrix passed again: all six main regression modes, ten focused .NET regression projects, the Node frontend suite, and the Release solution build. The build completed with zero errors and 131 existing warnings.

Published assets:

| Asset | Size | SHA-256 |
| --- | ---: | --- |
| `Emby.Plugin.Danmu.dll` | 1,562,112 bytes | `3b2dbf02f4ef1e47e07d5fc541425b87628bb933359c8af5afad5be13fbdf8d2` |
| `DanmuSmartMatch.CustomCssJS.js` | 206,696 bytes | `0ff2df87ae87afe3b05e265d4b0aa4748d0a27ad09fdb96d97caad12348b6e46` |
| `emby-plugin-danmuplus-2.0.3r10-source.zip` | 1,089,256 bytes | `ce520a350eb5aa724f41a2b967b690f3bee52672042f9ec53b2ca20c6df2281f` |

`SHA256SUMS` contains exactly these three standard release assets. The source archive contains 356 tracked or intended untracked source/documentation files beneath one `emby-plugin-danmuplus-2.0.3r10/` root and excludes `.git`, build output, historical `artifacts/`, `dist/`, and `releases/` binaries.

The release-stamped DLL differs from the active live behavior-candidate hash solely because task 5.3 updates assembly/product metadata and the generated configuration cache token; it has passed the full local matrix but has not been redeployed. The verified r9 rollback directory, three backup hashes, ownership/mode restoration, atomic replacement, restart, and health requirements remain unchanged above.

The reviewed release commit is `0e22325b3df90362fc9034469c3cb0f385182db3` on `develop`. Annotated tag `v2.0.3r10` peels to that exact commit, and the published GitHub Release contains the four expected assets: DLL, frontend JavaScript, source ZIP, and `SHA256SUMS`. GitHub's uploaded DLL, JavaScript, and source ZIP digests match the hashes recorded above. The Release is neither a draft nor a prerelease.

`main` remained unchanged during publication. The release is available for user review before the tagged `develop` commit and this completion record are merged to `main`. Task 5.3 is complete.
