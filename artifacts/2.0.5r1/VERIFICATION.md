# 2.0.5r1 local verification record

This repository-relative record is the release-verification template for
`release-2-0-5-r1-search-behavior-and-presentation`. It records only bounded,
credential-free evidence. It must not contain authenticated payloads, access
details, machine-specific absolute paths, server addresses, or deployment
credentials.

## Immutable references

- Implementation branch: `codex/2.0.5r1-matching-behavior`
- Exact implementation baseline: `f8a4356537dcf0c8f913bb970bb2bcdc689096fd`
- Behavioral candidate before release preparation:
  `5713498de45912384f013fa2180a1e7fadc4f002`
- Per-slice commits and focused evidence: [`COMMIT_MAP.md`](COMMIT_MAP.md)
- Release-preparation commit: `6e4c3c09df81150a3fa0d49fe9d5f11dc405d490`
- Post-slice Season score/source-surplus commit: `872aa157a39cd1b235b52f7a7b630305e7201a3d`
- Post-slice TMDB alias terminal-marker correction: `6d20fc0a7ac0027aea0309ac78b4f316256f7516`
- No push, tag, merge, GitHub Release, or package publication is authorized.
- Synology/Emby backup, replacement, restart, and live checks are user-authorized.
  The final V27 candidate was backed up, deployed, read back, and live-validated
  under that authorization without initiating a download.

## l1-l10 commit map

| Slice | Commit | Independent inverse |
| --- | --- | --- |
| l1 | `f90f779ce1219693fd2ea2ba0713451a893c9a02` | waived by user; not run |
| l2 | `d6070f60f9344476ea677c968fd62da87e79856c` | waived by user; not run |
| l3 | `720732398d12293db1e86009bbbfcd498917a1b8` | waived by user; not run |
| l4 | `9b008697274eb3cc12382eaf9b081101d3402e26` | waived by user; not run |
| l5 | `1594b53524614763d3d19a7084ef4f1fc45f87f9` | waived by user; not run |
| l6 | `5713498de45912384f013fa2180a1e7fadc4f002` | waived by user; not run |
| l7 | `9a9b5e6a111abc2ca03b87a732a0acd51e1b3610` | waived by user; not run |
| l8 | `67cd8f453ae2999e2e81406e5a5161b543e17792` | waived by user; not run |
| l9 | `ee09089a5460d2e13bb3424432366a500fa9296f` | waived by user; not run |
| l10 | `8f2dc5fd65df836e833712743deaaa6eb633e104` | waived by user; not run |

The release-preparation commit remains outside this behavioral rollback map.
The user explicitly waived the exhaustive inverse matrix and prioritized live
deployment testing. No resulting-tree or restoration evidence is claimed.

## Expected release metadata

| Field | Expected value | Status |
| --- | --- | --- |
| Assembly version | `2.0.5.1` | static PASS |
| File version | `2.0.5.1` | static PASS |
| Informational version | `2.0.5r1` | static PASS |
| Configuration version | `2.0.5r1` | static PASS |
| TMDB User-Agent | `DanmuPlus/2.0.5r1` | static PASS |
| Frontend installation marker | V27 | static PASS |
| Mapping protocol | V21, unchanged | static PASS |

## Planned local checks

All .NET commands must run sequentially to avoid shared output-file contention.
`PENDING` means the check has not run; it is not evidence of success.

| Check | Configuration | Result | Evidence note |
| --- | --- | --- | --- |
| Frontend smart-match regression | Node | PASS | Complete frontend suite and syntax checks |
| Version/document consistency | n/a | PASS | Metadata, V27/V21, images, cumulative history |
| Main backend regression | Release | PASS | Complete main executable, including retained reliability contracts |
| Bounded-search foundation/policy | Release | PASS | No shared 10/30/45-second deadline; explicit cancellation retained |
| R3 search quality | Release | PASS | Provider-local failure isolation |
| Search-term policy | Release | PASS | Scored manual-keyword discovery, merge/order/sixty-row and Movie-zero behavior |
| Title fidelity | Release | PASS | No restricted 0.85 promotion |
| TMDB alias regression | Release | PASS | Exhaustion/rematch and cancellation |
| Candidate evidence/detail | Release | PASS (focused) | Manual-keyword core target-bound selection validation |
| Composite-season planner | Release | PASS | Authoritative mapping invariants and response-only verified source surplus |
| Single-target download arbiter | Release | PASS | Retained 180-second boundary covered by complete main reliability contracts |
| Seven-day replay | Release | PASS | Retained skip/replay rules |
| Full plugin build | Release | PASS | Clean sequential build; 0 errors and 131 existing warnings |
| OpenSpec strict validation | n/a | PASS | Change is valid under strict validation |
| Changed-file allowlist and diff check | n/a | PASS | Exact implementation allowlist; no unrelated staged content or whitespace errors |
| Credential-safe artifact audit | n/a | PASS | Source, staged implementation, and final V27 review package contain no private addresses or credential assignments |
| l1-l10 independent inverse matrix | Release/focused | WAIVED | User requested live deployment testing first; no inverse result claimed |

## Artifact and hash placeholders

The Release artifacts below were rebuilt from the reviewed post-slice commit.

| Artifact | Repository-relative location | Size | SHA-256 |
| --- | --- | --- | --- |
| Release DLL | `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll` | 1632768 | `127d3ca0938d1307482cc7dd827353589221385b4061e9e13b628d65f239fe9f` |
| Smart-match source | `Frontend/DanmuSmartMatch.CustomCssJS.js` | 224910 | `c8fd263c6cff1c3c9b93a6d95b721c4ba07886ee091f34fb085ce4b63448c55f` |
| Cumulative update notes | `UPDATE.md` | 16325 | `2c6f6f8d9a0bb9a1af40f9e3070d33ac344b942ea0ace4b83c40a69c070be604` |
| Verification record | `artifacts/2.0.5r1/VERIFICATION.md` | self-record | verified by package `SHA256SUMS` |
| Local review package | `artifacts/2.0.5r1/review-package-v27-final` | 6 allowlisted files | all entries verified by `SHA256SUMS` |

## Approval gates

- Local source edits and deterministic verification do not authorize external
  publication or live changes.
- Creating a local review package does not authorize pushing, tagging, merging,
  publishing a Release, or uploading any artifact.
- Live backup, replacement, restart, and representative checks are authorized
  after the Release build, deployment smoke checks, artifact checks, and usable
  rollback backup pass. Those gates passed for the final V27 candidate, including
  server-side hash readback, service health, and rollback-backup checksum readback.
- When the authorized live validation runs, record only bounded outcomes and hashes;
  do not place server locations, credentials, request signatures, headers, raw
  responses, or backup locations in this file.
- A live replacement must not begin until the currently deployed paired assets
  are backed up and a directly usable rollback is confirmed.

## Authorized live validation

- Deployed DLL SHA-256: `127d3ca0938d1307482cc7dd827353589221385b4061e9e13b628d65f239fe9f`.
- Deployed CustomCssJS configuration SHA-256: `552f384a3b4cc19785e8f39c0ce4e735930b16a9ab0b96b1fe9edfbc8b4e636b`.
- Deployed plugin configuration SHA-256: `02519afd92022babacf9e6d516c44c0dde0117a2744593501d8cb29222536069`.
- V27 installation marker count was exactly one; service health and the directly
  usable three-file rollback backup checksum readback passed after restart.
- Whole-Series preview for Bookworm returned four Seasons and 52 local Episodes.
  S1 matched at 80; S2, S3, and S4 matched their corresponding Season titles at
  100; all four Seasons were selected. S4 alone displayed exactly one visible
  yellow `库内集数少于来源集数` notice.
- Single-Season S4 preview matched the corresponding 2026 source at 100 and
  displayed the same notice exactly once in yellow.
- No download button was activated during either live preview.

## Final sign-off placeholders

- Release-preparation commit: `6e4c3c09df81150a3fa0d49fe9d5f11dc405d490`
- Reviewed candidate commit: `6d20fc0a7ac0027aea0309ac78b4f316256f7516`
- Independent reviewer findings: final Sol-high review reported no P0 or P1 finding and approved deployment
- Full local verification result: PASS; one initial `--no-restore` missing-assets invocation was rerun successfully after normal restore
- Local package result: PASS; six allowlisted final V27 files, checksum readback, source/package equality, and credential audit verified
- External publication approval: not granted
- Live validation result: PASS; final V27 deployed and read back, rollback backup verified, whole-Series and single-Season previews passed, and no download was initiated
