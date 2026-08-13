# 2.0.3r4 baseline and read-only fixture evidence

## Frozen r3 source

- Source commit: `1604f56974bebc37d067b1e67db65d39bf3b8415`
- Source tree: `a0c153795b1796c4048d0f6d62be30753b1cb8bc`
- Branch: `codex/2.0.3r3-search-quality-partial-mapping`
- r3 DLL SHA-256: `9D95F7952BC19050B8D6F54002EA1807EFA3B01303A19DE0739736FB1784CF71`
- Deployed/verified r3 JavaScript SHA-256 is recorded in `artifacts/2.0.3r3/VERIFICATION.md`; the committed artifact checkout is subject to the repository's existing `core.autocrlf=true` normalization and must not be reused as an r4 hash.
- r4 worktree: `C:\Users\mouxi\Documents\Codex\emby-plugin-danmuplus-2.0.3r4`
- r4 branch: `codex/2.0.3r4-parent-season-aware-mapping-ui`

The r3 scope gate passed before the freeze commit with 34 changed files. No `DanmuSeasonSegment*` or `DanmuSeasonCollection*` experimental implementation is imported into r4.

## One Punch Man observation

The reported Emby first-Season display contains 19 local Episodes: ordinary `S01E01` through `S01E12` plus seven specials whose actual metadata coordinates are `S00E01` through `S00E07`. Emby placement metadata causes those S00 Episodes to display inside the first Season. r3 flattened this structure to `IndexNumber`, so 1–7 occurred twice and source application fragmented the remaining Episodes into several false temporary groups.

r4 acceptance: all 19 ItemIds remain unique and ordered by authoritative display/placement context; a normal S1 source may map only the 12 owning S01 Episodes; the seven S00 Episodes remain exactly one logical temporary run until a separate special source is explicitly selected.

## Seitokai Yakuindomo observation

- Series ItemId: `519626`
- Season 1 ItemId: `519628`
- Season structure: 13 Episodes, all `ParentIndexNumber=1`, `IndexNumber=1..13`
- Local E1 ItemId: `519633`
- Pre-existing local E1 Dandan identifier: `75320001`
- Selected Dandan Season media: `7532`, with 13 source Episodes

r3 adopted E1-to-source-E1 from the local Episode identifier, then started the newly selected Season mapping at local E2 but restarted the source at E1. The planner correctly rejected duplicate use of one verified source Episode (`A verified source episode may only be mapped once within a season plan.`). r4 Series/Season planning ignores the local identifier and freshly maps local E1..E13 to selected source E1..E13.

## Browser transport observation

In the reported whole-Series failure, Emby's browser transport resolved `ApiClient.ajax` to a Fetch-compatible `Response`. r3's synchronous `asJson` returned that object unchanged; later UI error concatenation converted it to `[object Response]`. r4 must asynchronously decode a successful Response body and normalize JSON/text/empty HTTP/network/timeout/cancel failures without ever rendering the raw transport object.

All observations in this document were read-only. No Bind, StartTrackedDownload, metadata mutation, or media-file write was performed.
