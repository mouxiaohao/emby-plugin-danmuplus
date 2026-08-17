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
- Release-preparation commit: pending
- No push, tag, merge, GitHub Release, or package publication is authorized.
- Synology/Emby backup, replacement, restart, and live checks are
  user-authorized, but the required Release build, deployment smoke checks, and
  backup prerequisites are not yet complete and no live action has run.

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
| Frontend installation marker | V24 | static PASS |
| Mapping protocol | V21, unchanged | static PASS |

## Planned local checks

All .NET commands must run sequentially to avoid shared output-file contention.
`PENDING` means the check has not run; it is not evidence of success.

| Check | Configuration | Result | Evidence note |
| --- | --- | --- | --- |
| Frontend smart-match regression | Node | PASS | Complete frontend suite and syntax checks |
| Version/document consistency | n/a | PASS | Metadata, V24/V21, images, cumulative history |
| Main backend regression | Release | PENDING | Complete main executable |
| Bounded-search foundation/policy | Release | PENDING | No shared 10/30/45-second deadline |
| R3 search quality | Release | PENDING | Provider-local failure isolation |
| Search-term policy | Release | PENDING | Scored manual-keyword discovery |
| Title fidelity | Release | PENDING | No restricted 0.85 promotion |
| TMDB alias regression | Release | PENDING | Exhaustion/rematch and cancellation |
| Candidate evidence/detail | Release | PENDING | Target-bound selection validation |
| Composite-season planner | Release | PENDING | Authoritative mapping invariants |
| Single-target download arbiter | Release | PENDING | Retained 180-second boundary |
| Seven-day replay | Release | PENDING | Retained skip/replay rules |
| Full plugin build | Release | PENDING | DLL metadata and embedded resources |
| OpenSpec strict validation | n/a | PENDING | Change-wide final validation |
| Changed-file allowlist and diff check | n/a | PASS (release-prep scope) | No unrelated content or whitespace errors |
| Credential-safe artifact audit | n/a | PENDING | Record findings only, never values |
| l1-l10 independent inverse matrix | Release/focused | WAIVED | User requested live deployment testing first; no inverse result claimed |

## Artifact and hash placeholders

These fields remain pending until a clean Release build and local review package
have actually been produced.

| Artifact | Repository-relative location | Size | SHA-256 |
| --- | --- | --- | --- |
| Release DLL | `bin/Release/netstandard2.0/Emby.Plugin.Danmu.dll` | pending | pending |
| Smart-match source | `Frontend/DanmuSmartMatch.CustomCssJS.js` | pending | pending |
| Cumulative update notes | `UPDATE.md` | pending | pending |
| Verification record | `artifacts/2.0.5r1/VERIFICATION.md` | pending | pending |
| Local review package | pending | pending | pending |

## Approval gates

- Local source edits and deterministic verification do not authorize external
  publication or live changes.
- Creating a local review package does not authorize pushing, tagging, merging,
  publishing a Release, or uploading any artifact.
- Live backup, replacement, restart, and representative checks are authorized
  after the Release build, deployment smoke checks, artifact checks, and usable
  rollback backup pass; they have not run.
- When the authorized live validation runs, record only bounded outcomes and hashes;
  do not place server locations, credentials, request signatures, headers, raw
  responses, or backup locations in this file.
- A live replacement must not begin until the currently deployed paired assets
  are backed up and a directly usable rollback is confirmed.

## Final sign-off placeholders

- Release-preparation commit: pending
- Reviewed candidate commit: pending
- Independent reviewer findings: pending
- Full local verification result: pending
- Local package result: pending
- External publication approval: not granted
- Live validation approval: granted after local verification and backup; not executed
