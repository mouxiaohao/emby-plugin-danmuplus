# Emby Plugin Danmu 2.0.3r4 release verification

## Source baseline

- Branch: `codex/2.0.3r4-parent-season-aware-mapping-ui`
- Baseline commit: `1604f56974bebc37d067b1e67db65d39bf3b8415`
- Baseline tree: `a0c153795b1796c4048d0f6d62be30753b1cb8bc`
- Candidate: the reviewed r4 working-tree delta over that frozen r3 baseline
- Target: Emby `4.9.3`, `netstandard2.0`, C# 8

## Paired release assets

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `Emby.Plugin.Danmu.dll` | 1,475,072 | `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be` |
| `DanmuSmartMatch.CustomCssJS.js` | 173,389 | `825d8ab5fdd7bdcb0f45bbc5a4faee97c0212f2d21b83174114eba9a0b167077` |
| `update_customcssjs.py` | 2,592 | `05d9d0fd07f462dc1e2453014d2f1d9a4fc9e8a1473bf54096ac8e02bc264f28` |
| `replace_v20_once.py` | 2,461 | `cbf9e8153aeafbf8f7234f8f4fb58dce6f950db9bd496f2411756c12aa7c294e` |
| `restart_emby.sh` | 1,035 | `1d5d6d644b76376ff7d2e7589a99b891736245024601daf493d57418551de68c` |

- Assembly version: `2.0.3.0`
- DLL is byte-identical to the final Release output.
- JavaScript is byte-identical to `Frontend/DanmuSmartMatch.CustomCssJS.js`.
- JavaScript syntax: `node --check` passed.
- Marker contract: exactly one `__embyDanmuSmartMenuV20`, zero V19.

## Updater and restart verification

- `python -m py_compile update_customcssjs.py`: passed.
- Positive test against a copy of the deployed r3 XML: V19 `1 -> 0`, V20 `0 -> 1`.
- Negative re-entry test against the already migrated copy: rejected with exit code `1`.
- The updater replaces only the unique V19 content entry using same-directory
  temporary-file plus `os.replace`, preserving mode and ownership.
- `replace_v20_once.py` passed syntax and positive replacement tests and rejected
  both zero-marker and duplicate-marker inputs. It replaces only the unique V20
  content entry, fsyncs the candidate, atomically replaces the XML, and fsyncs
  the containing directory where supported.
- The restart helper validates that port 8096 belongs to the expected Emby
  executable, stops the package, waits for exit, and starts the package.

## Test matrix

- Release plugin build: passed, zero errors (existing warnings only).
- Full `RegressionTests`: passed.
- `R4ParentSeasonContext` focused suite: passed.
- `R4IdentifierMetamorphic` focused suite: passed for nine identifier sets.
- Frontend JavaScript regression/source checks: passed by the paired candidate workflow.

## r3 rollback baseline

The verified r3 paired state is:

- DLL: `9d95f7952bc19050b8d6f54002ea1807efa3b01303a19de0739736fb1784cf71`
- Danmu XML: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- CustomCssJS XML: `49f3f66b543c5d32fa7024cba6c9b28284454e65773756f0180aa2d9b5bf9f7c`
- Rollback semantics: restore all three files as one paired set while Emby is
  stopped, then restart and require HTTP 200, V19=1/V20=0, and normal plugin
  entry-point logs.

Deployment and live read-only evidence is recorded separately under `evidence/`.

## Score-preservation candidate status

The paired assets above are the deployed score-preservation candidate. A live
read-only One Punch preview proved that every mapped `CompositeGroups` score
equals all corresponding `CompositePlan.Mappings`, including Dandan S1/S2 and
Youku at `MatchScore=1`, `ScoreOrigin=search-confidence`. Temporary groups had
no `MatchScore` member and therefore cannot render a fabricated zero.

## Approved live deployment

- Backup: `/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-score-approved-predeploy-20260812-153000`
- Deployed DLL: `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be`
- Frontend P1 refresh backup: `/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-v20-frontend-predeploy-20260812-143140`
- Pre-refresh CustomCssJS XML: `0638a61a16ebf50446c29d49c333916ff1672bca48c00c0c3e8435f59f4e37c6`
- Deployed CustomCssJS XML: `f8f6dd7876dec44d41f7c7b0764ad6be5cf84e0d263f16c76c46701ac5e1cf09`
- Emby 4.9.3 HTTP 200; V20=1/V19=0; clean plugin startup.
- One Punch Series/S1: 12 unique S01 mappings plus one seven-Episode S00 run.
- Seitokai Dandan 7532: 13 unique mappings plus one eight-Episode S00 run.
- Validation used read-only preview/library queries only.

## Authenticated browser smoke

- The V20 whole-Series menu and dialog loaded in the authenticated Emby web UI.
- One Punch S1 rendered `S01E01-S01E12` as one mapped group and
  `S00E01-S00E07` as exactly one unmatched temporary group.
- Mapped summaries displayed only localized provider names and authoritative
  `100 (title match)` scores; temporary groups displayed no score.
- No ProviderId token, MediaId/hash, source Episode ID, evidence token, or
  `[object Response]` appeared in the smart-match dialog.
- At a 500 px viewport, expanded mapping details spanned the full card grid
  (`grid-column: 1 / -1`, `min-width: 0`) and rendered only rows such as
  `Local S01E01 -> Source episode 1`; the viewport was reset afterward.
- The dialog was closed without starting download, binding, or metadata writes.

## Tasks 8.5/8.6 disposable-fixture attempt

The 2026-08-12 isolated live-fixture attempt is recorded in
`evidence/live-fixture-8.5-8.6-blocked-20260812.md`. Emby accepted the isolated
virtual folder but did not index any of its 25 disposable `.strm` episodes within
the bounded scan plus one bounded item refresh. Consequently no download or
metadata mutation was attempted and tasks 8.5/8.6 remain unchecked. The r3 trio
copy/hash dry-run and strict cleanup passed; current r4 hashes and HTTP 200 were
unchanged.
