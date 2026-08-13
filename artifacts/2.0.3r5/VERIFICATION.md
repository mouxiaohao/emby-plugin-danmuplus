# Emby Plugin Danmu 2.0.3r5 local release verification

## Frozen source baseline and scope

- Frozen deployed r4 commit: `5f980931370343af403fa4a3c3a011e747176abd`
- Frozen r4 tree: `2a9cc5efd19251023b85f569d9a0aea02c69d3b8`
- Candidate: reviewed r5 working-tree delta over that exact commit
- Scope gate: `RegressionTests/VerifyR203R5Scope.ps1`
- Target: user-approved live Emby 4.9.5.0, `netstandard2.0`, C# 8
- Assembly version: `2.0.3.0`
- File version: `2.0.3.5`
- Product/informational version: `2.0.3r5`
- Batch protocol/cache marker: V21 / `__embyDanmuSmartMenuV21`

The scope gate uses an explicit allowlist, rejects known experimental/dynamic
planner files, requires the exact target and version tuple, and proves the
product frontend has exactly one V21 marker with no V20/V19 marker.

## Paired candidate assets

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `Emby.Plugin.Danmu.dll` | 1,492,992 | `123ee755f22ae20a1a2492f4d616c4b6f8cd232bfc629fac25f0a4c466b8d552` |
| `DanmuSmartMatch.CustomCssJS.js` | 181,857 | `b457b4cbd4dc91a250230531cc8124bbd174872577963bf8976491d870546b9d` |
| `update_customcssjs.py` | 2,592 | `71884ef92c40893b2e098273e5839655ee3f9df0f39b421b95da174c61616376` |
| `restart_emby.sh` | 999 | `be2465bda563693a7e7d6397c8060e59eb0b254d318142063d6941e9648d6838` |

- DLL is byte-identical to `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll`.
- JavaScript is byte-identical to `Frontend/DanmuSmartMatch.CustomCssJS.js`.
- Reflection/FileVersionInfo verified the assembly/file/product versions above.
- `node --check` passed for the paired JavaScript.

## Atomic updater and restart helper

- `update_customcssjs.py` accepts only a configuration with one V20 entry and
  a candidate with one V21 marker and no V20 marker.
- Positive V20-to-V21 fixture migration passed; immediate re-entry was rejected.
- The updater replaces only the matching `<content>` entry using a same-directory
  temporary file, `fsync`, and `os.replace`, preserving mode and ownership.
- `restart_emby.sh` contains zero CR bytes (verified LF-only), uses POSIX `sh`,
  validates that port 8096 belongs to the expected Emby executable, waits for
  bounded clean exit, and refuses to start after a failed stop.

The helpers first passed local fixture checks. Their later live use and all
deployment safeguards are recorded in
`evidence/deployment-readonly-acceptance-20260813.md`.

## Cross-change specification audit

The r5 deltas were synced into the effective main specifications for:

- `parent-season-aware-episode-mapping`
- `season-danmu-matching`
- new `season-episode-scope-filtering`

`openspec validate --specs` passed all seven main capabilities and strict change
validation passed. The scope gate also rejects the obsolete r4 normative phrases
that made foreign/unknown Episodes supplemental or temporary work for a normal
Season, and requires the positive exact-parent exclusion clauses in main specs.

## Local verification matrix

- Release solution build: passed, zero errors (existing warnings only).
- Main regression suite: passed.
- R5 target-season scope suite: passed.
- R4 identifier metamorphic suite: passed for nine identifier sets.
- Frontend syntax and regression suite: passed.
- Strict OpenSpec change validation: passed.
- Main-spec validation and cross-change audit: passed.
- `git diff --check`: passed (line-ending conversion notices only).

## Rollback pair

The retained local r4 paired rollback assets are:

| Asset | Bytes | SHA-256 |
|---|---:|---|
| `artifacts/2.0.3r4/Emby.Plugin.Danmu.dll` | 1,475,072 | `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be` |
| `artifacts/2.0.3r4/DanmuSmartMatch.CustomCssJS.js` | 173,733 | `bc82e3d4d4a434e2c353edf7550e2050c43d3ec9c1bf543af1dda6c4da857818` |

Rollback requires restoring the r4 DLL and V20 frontend configuration as one
paired set while Emby is stopped, then performing a full restart. The live
absolute backup and verified rollback instructions are recorded in
`evidence/deployment-readonly-acceptance-20260813.md`.

## Live predeployment gate — resumed after approval

The initial 2026-08-13 read-only predeployment check is recorded in
`evidence/predeployment-blocked-20260813.md`. It stopped before mutation because
the live server reported Emby `4.9.5.0` while the then-approved target was
4.9.3, and the first supplied account did not have working SSH authentication.
The user subsequently approved Emby 4.9.5.0 as the deployment and acceptance
baseline and supplied a separate authenticated administrative channel. No
credential is retained in this repository.

The initial attempt made no remote mutation and left active r4 untouched.
Deployment evidence below or in a subsequent evidence file is authoritative
for the resumed attempt; tasks remain unchecked until their mandatory gates
actually pass.

## Approved deployment and read-only acceptance

The resumed 2026-08-13 deployment and authenticated read-only acceptance are
recorded in `evidence/deployment-readonly-acceptance-20260813.md`. The final
accepted state is Emby 4.9.5.0 with the packaged r5 DLL, exactly one V21 marker
and no V20 marker, clean Danmu/CustomCssJS entry-point loading, and unchanged
production Episode membership. Whole-Series previews excluded S0; explicit S1
previews excluded placed Parent0 Episodes from mapping/temp work; explicit real
S0 used only its own 18 Parent0 inventory records. No product-write API was
called. Authenticated browser verification also passed for the One Punch Man
whole-Series and S1 entry points: both rendered the same 12 eligible mappings,
the seven S00 records only as read-only ignored counts, server scores and source
labels correctly, and no V20 draft. No Smart Match console error was observed;
the known independent `danmuku` lifecycle error remained separately attributable.

## Isolated write-fixture attempt

The bounded disposable attempt is recorded in
`evidence/fixture-index-blocked-cleanup-20260813.md`. Emby accepted the isolated
TV library and one precise refresh, but indexed zero Series, Season, and Episode
items at 0/4/12 seconds. No plugin write-path request was made. The library was
removed, ItemId `521378` returned 404, and the exact non-symlink fixture/staging
paths were safely deleted after renewed production-ancestor checks. Final r5
health, hashes, marker, load status, and private plugin state were unchanged.
Tasks 10.4 and 10.5 remain unchecked.
