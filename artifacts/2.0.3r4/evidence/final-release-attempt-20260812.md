# r4 final release attempt — 2026-08-12

## Candidate and backup

- Candidate DLL: `3e3a83ccd62d23814a81ba4709a44d20dfe4592e27556a336551a0cb3f3ba919`
- Candidate JavaScript: `bcbc796f05c0dbcc081bf6a234d85f4d8e9091c6ccaa5ec03d69f16dcaac3ff8`
- Absolute predeployment backup:
  `/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-final-predeploy-20260812-141458`
- Backup includes paired r3 DLL/two XML files, raw DB/WAL/SHM, online
  consistent SQLite backup, composite state, and `SHA256SUMS`.

The staged assets matched the local hashes. V19-to-V20 migration, atomic DLL
replacement, restart, HTTP 200, V20=1/V19=0, and plugin load smoke passed.
No download, binding, metadata-write, refresh, or library mutation API was used.

## Read-only preview evidence

One Punch Man Series `484296` and Season 1 `484299` produced equivalent S1
plans without `invalid_request`:

- 12 unique S01 mappings
- exactly one unmatched run containing seven unique S00 supplemental Episodes
- 19 unique ItemIds across mappings plus the run, with zero overlap
- protocol V20

Seitokai Yakuindomo Season 1 `519628` fresh discovery returned Dandan candidate
`7532` despite local E1 retaining old Dandan ID `75320001`. Selected-candidate
preview was valid and contained 13 unique mappings, exact source IDs
`75320001..75320013`, no `composite-plan-invalid`, and one remaining S00
temporary run. It therefore did not meet the requested `0 unmatched` release
gate.

The remaining run consists of these eight Episodes displayed by the S1 query:

| ItemId | Parent/Index | SeasonId | Name |
|---|---|---|---|
| `519649` | S00E01 | `519629` | OAD #14 |
| `519650` | S00E02 | `519629` | OAD #15 |
| `519651` | S00E03 | `519629` | OVA #16 |
| `519652` | S00E04 | `519629` | OAD #17 |
| `519653` | S00E05 | `519629` | OAD #18 |
| `519654` | S00E06 | `519629` | OVA #19 |
| `519655` | S00E07 | `519629` | OVA #20 |
| `519656` | S00E08 | `519629` | OAD #21 |

The S1 inventory has 21 Episodes (13 owning plus these eight inserted S00
Episodes). The separate ParentId=`519629` S0 display query has 18 Episodes but
does not include these eight ItemIds; nevertheless all eight retain Emby
`SeasonId=519629` and `ParentIndexNumber=0`. This is authoritative evidence that
they are S00 items placed into the S1 display inventory, so the selected Dandan
7532 plan is semantically correct as 13 mapped plus one S00 temporary run.

## Mandatory rollback

Because the requested acceptance gate explicitly required zero unmatched, the
candidate was rolled back immediately from the new backup. Final NAS state:

- HTTP 200, Emby 4.9.3.0, PID 7733 on port 8096
- r3 DLL: `9d95f7952bc19050b8d6f54002ea1807efa3b01303a19de0739736fb1784cf71`
- Danmu XML: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- CustomCssJS XML: `49f3f66b543c5d32fa7024cba6c9b28284454e65773756f0180aa2d9b5bf9f7c`
- exactly one V19 marker
- normal CustomCssJS and Danmu ServiceRegistrator startup logs; no load error

## Sol-approved final deployment

Sol subsequently approved the corrected Seitokai criterion: 13 owning mappings
plus the real eight-Episode S00 temporary run. The verified candidate was
deployed again and passed final read-only validation:

- One Punch Season and Series: 12 mapped + one S00 run of 7, 19 unique ItemIds,
  zero overlap, no `invalid_request`.
- Seitokai Dandan 7532: 13 unique mappings, source `75320001..75320013`, one
  S00 supplemental run of 8, no `composite-plan-invalid`.
- HTTP 200, Emby 4.9.3.0, clean plugin entry-point startup.
- DLL `3e3a83ccd62d23814a81ba4709a44d20dfe4592e27556a336551a0cb3f3ba919`.
- CustomCssJS XML `0638a61a16ebf50446c29d49c333916ff1672bca48c00c0c3e8435f59f4e37c6`.
- Exactly one V20 marker and no V19.

The server is intentionally left on the approved r4 candidate.

## Frontend P1 V20 refresh

The corrected frontend was byte-copied from the reviewed source without a DLL
change:

- JavaScript: 173,148 bytes,
  `f6d7fb0bce2cb51b66a0547d5b7d993cf21afb1495b8ab27b29c8a4e26dadd86`
- DLL unchanged:
  `3e3a83ccd62d23814a81ba4709a44d20dfe4592e27556a336551a0cb3f3ba919`
- One-time V20 replacer:
  `cbf9e8153aeafbf8f7234f8f4fb58dce6f950db9bd496f2411756c12aa7c294e`

Before replacement, the live CustomCssJS XML was copied to:
`/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-v20-frontend-predeploy-20260812-143140`.
Its pre-refresh hash was
`0638a61a16ebf50446c29d49c333916ff1672bca48c00c0c3e8435f59f4e37c6`.

The dedicated updater required exactly one deployed V20 marker and exactly one
candidate V20 marker, replaced only that content entry using a same-directory
temporary file and atomic replace, and rejected zero/duplicate-marker fixtures.
After restart, the live XML hash is
`7bcddf5b2eddfd647b13f4734430058b7ffec616a99aafe6cd75e2a31cbfcf9c`,
with V20=1 and V19=0. Emby 4.9.3 returned HTTP 200; CustomCssJS and Danmu entry
points completed normally. No download, binding, metadata, or library mutation
operation was performed.

## Score-preservation release gate and rollback

The score-preservation candidate was rebuilt and paired with the current
frontend:

- DLL: 1,475,584 bytes,
  `d731947e8b78a06bfbfdd61d050295c2d2bd92cd29460a8cab680c0a267a0539`
- JavaScript: 173,389 bytes,
  `825d8ab5fdd7bdcb0f45bbc5a4faee97c0212f2d21b83174114eba9a0b167077`

The live r4 pair was backed up before deployment at
`/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-score-predeploy-20260812-150300`.
Deployment, restart, HTTP 200, plugin entry-point smoke, candidate hashes, and
V20=1/V19=0 passed.

The read-only One Punch Series preview then failed the score wire gate. Its
authoritative `CompositePlan.Mappings` contained nonzero closed server evidence
(for example Dandan S1 `MatchScore=1`, `ScoreOrigin=search-confidence`), but the
corresponding mapped `CompositeGroups` entry omitted `MatchScore`. Temporary
groups correctly omitted it. This isolates the remaining defect to mapped-group
DTO serialization rather than search scoring, evidence registration, planning,
or the frontend.

No download, binding, metadata, refresh, or library mutation call was made. The
server was immediately restored to the backup pair and restarted. Final state:

- DLL `3e3a83ccd62d23814a81ba4709a44d20dfe4592e27556a336551a0cb3f3ba919`
- CustomCssJS XML `7bcddf5b2eddfd647b13f4734430058b7ffec616a99aafe6cd75e2a31cbfcf9c`
- Danmu XML `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- HTTP 200, V20=1/V19=0, clean CustomCssJS and Danmu entry-point completion

## Score-preservation approved deployment

After isolating both incompatible field annotations, the final group DTO uses
an unannotated nullable score. The approved paired assets are:

- DLL: 1,475,072 bytes,
  `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be`
- JavaScript: 173,389 bytes,
  `825d8ab5fdd7bdcb0f45bbc5a4faee97c0212f2d21b83174114eba9a0b167077`

The immediately preceding live r4 state is backed up at
`/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-score-approved-predeploy-20260812-153000`.
Atomic DLL and unique-V20 content replacement, restart, HTTP 200, and plugin
entry-point loading passed. Final live hashes are:

- DLL `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be`
- CustomCssJS XML `f8f6dd7876dec44d41f7c7b0764ad6be5cf84e0d263f16c76c46701ac5e1cf09`
- Danmu XML `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- V20=1, V19=0

The final read-only One Punch Series preview returned HTTP 200. Dandan S1,
Dandan S2, and Youku mapped groups each contained `MatchScore=1` with
`ScoreOrigin=search-confidence`; every group value equaled all corresponding
mapping values. The S1 seven-Episode and S2 six-Episode temporary groups omitted
`MatchScore`. The paired frontend regression proves mapped values render as a
score line while missing/null temporary values render no score line. No
download, binding, refresh, metadata, or library mutation call was made.
