# 2.0.7r2 Verification

## Baseline and scope

- Exact baseline: `2f351cd6f0a08ab707d1d87d53935d6c53c723e0` (2.0.7r1 `origin/develop`).
- Development branch: `codex/release-2.0.7r2-composite-season-continuity` in an isolated worktree.
- Scope: server-owned animated whole-Series logical-Season continuation, same-Provider search/evidence/rebuild fences, deterministic regressions, V34 frontend identity, cumulative documentation, and OpenSpec artifacts.
- No `AGENTS.md` or `AGENTS.override.md` was created or modified. No Bookworm title or alias was hardcoded in production code.

## Reviewed payloads

- `Emby.Plugin.Danmu.dll`: 1,748,480 bytes; SHA-256 `CC11B6552A670A024D452D754972C52D527C21F8A98B7C9AB8E9ED33ADD01643`.
- `DanmuSmartMatch.CustomCssJS.js`: 251,802 bytes; SHA-256 `B0EAA5D08113786A9785579A3398CE90975CC4EBF0CC66C9ABB6512DB3C02286`.
- `UPDATE.md`: 30,145 bytes; SHA-256 `94E6CB76274E16FE71A290F56A31A51B3D2C8B5F5FD676F23FE3200076993363`.
- `SHA256SUMS.txt` lists exactly those three payloads; every digest was generated from the copied review-package file.
- `.gitattributes` treats the complete 2.0.7r2 review package as binary; the staged Git blobs for all three payloads were read back byte-for-byte and matched the manifest.

## Identity and protocol

- Assembly/File/Product versions: `2.0.7.0` / `2.0.7.2` / `2.0.7r2`.
- Configuration/TMDB User-Agent/cache token: `2.0.7r2` / `DanmuPlus/2.0.7r2` / `2-0-7r2`.
- Frontend formal markers V34/V33: `1/0`; frontend and backend mapping protocol remain V22.
- Release output contains no PDB.

## Deterministic verification

- Clean/forced-restore/Release build: success, 131 pre-existing warnings, zero errors.
- Strict OpenSpec validation: success.
- Main backend regression suite: success after the final version update.
- 2.0.7r2 continuity harness: success for animation/full-Series gating, generic adjacent propagation, later activation, gap/failure reset, same-Provider prefilter, global first-target provider search, arbitrary Part counts, server-only serialization, evidence substitution, fingerprint identity, and Bookworm S1-S4 orchestration.
- Sequential focused regressions: 2.0.7 remainder core/orchestration, target-Season scope/S00, identifier-free metamorphic, bounded search, search terms, temporary ranges, r3 search quality, parent-Season context, title fidelity, Episode selection, and MGTV search all succeeded.
- Frontend production/test syntax and the complete Smart Match frontend regression succeeded.
- `git diff --check` succeeded. Changed-diff audit found no private endpoint, credential assignment, Authorization/Bearer material, or AGENTS-file change.

## Behavioral fences

- Only an ordinary user-initiated full-Series request for server-recognized animation can create cross-local-Season state.
- A chain activates only from a current, complete, exact-coverage plan with a real validated `logical-season` advance.
- Continuation filters providers before standard or TMDB-alias calls and never falls back to another Provider or the local ordinal.
- Part count and Part number never advance a logical Season. A logical K target with any positive number of Parts makes the next adjacent physical Season target K+1.
- Candidate tokens and plan fingerprints bind generation, effective logical Season, Provider, activation identity, predecessor identity/fingerprints, and exact coverage. Stale or substituted proof fails before forbidden-provider detail resolution or writes.
- V22 browser request/response objects expose none of the logical continuation proof.

## Deployment acceptance

- Read-only inventory found the active DLL at 1,725,952 bytes, SHA-256 `535CA3649E36459D7A1A0990F31191A6A8621F304222ED0DB248B3396C058F42`, owner/mode `emby:users 0644`; the active CustomCssJS configuration at 531,423 bytes, SHA-256 `2A0A29349719C893A25258273C445BF38451FE55B1319058DCE377A16C0EDC61`, owner/mode `emby:users 0444`; and the Danmu configuration at 1,593 bytes, SHA-256 `9C0C99BD109676C395C068410B6726F3C0E84A3EC5675279E6B6E3EBF78A15C4`, owner/mode `emby:emby 0644`.
- A timestamped root-only (`0700`) predeployment directory retains all three files. Every backup SHA-256 was compared with its active source before replacement and matched exactly.
- The reviewed DLL and a CustomCssJS configuration that changed only the Smart Match entry were staged and hash-checked before shutdown. The unrelated `danmuku` entry remained byte-for-byte equal after XML decode; the Danmu configuration was not replaced and retained its SHA-256.
- After paired replacement, Emby restarted successfully, package status was `running`, and the local public health endpoint returned HTTP 200. Startup logged `Loading Emby.Plugin.Danmu, Version=2.0.7.0`; the corrected acceptance request and all later log lines contained no `error`, `exception`, or `fail` entry.
- Active readback SHA-256 values are DLL `CC11B6552A670A024D452D754972C52D527C21F8A98B7C9AB8E9ED33ADD01643`, CustomCssJS XML `FA640EC74F2986528074CBEDD6354140811C0443CB7173C6D7175B24882E3899`, and unchanged Danmu configuration `9C0C99BD109676C395C068410B6726F3C0E84A3EC5675279E6B6E3EBF78A15C4`. Owner/mode remained `emby:users 0644`, `emby:users 0444`, and `emby:emby 0644` respectively.
- The decoded deployed Smart Match entry is 247,121 normalized-LF UTF-8 bytes with SHA-256 `0DE51FD71EDE3582175B84281F2DDE4D6654EDFB74941CE1BC1A1034EDE4FE33`; V34/V33 marker counts are `1/0`, both CustomCssJS entries parse successfully, and the unrelated entry is unchanged.
- The real library item `爱书的下克上：为了成为图书管理员不择手段！` was server-recognized as animation. Its read-only whole-Series preview returned only physical S1/S2 (S00 stayed excluded), both `matched`, with zero unmatched runs and complete `36/36` plus `18/18` authoritative mappings.
- Physical S1 used DandanPlay for three groups: logical Season 1 mapped local E1-E14 to source E1-E14; logical Season 2 mapped local E15-E26 to source E1-E12; logical Season 3 mapped local E27-E36 to source E1-E10. The latter two groups reported `remainder-logical-season` origin.
- Physical S2 used DandanPlay `小书痴的下克上 第四季` and mapped local E1-E18 to source E1-E18. It was not classified as Part 3 or Season 3.
- Provider diagnostics prove the activating S1 searched the normal six enabled providers, while the continuation S2 invoked only `DandanID`. Thus the same-provider gate applied before provider search, while the TMDB alias path remained available on that provider.
- Acceptance invoked `MatchPreview` only; it did not invoke bind, download, refresh, queue, or metadata-write routes. The active Danmu configuration hash remained unchanged after preview.
- One preliminary client-constructed URL was malformed before acceptance and produced the expected GUID format error. The URL was corrected, the whole-Series preview passed, and the post-correction log interval was clean; this was a test-client error rather than a plugin matching failure.
- Rollback material remains server-side. Recovery is: validate the retained backup directory, stop Emby, `cp -p` its DLL, CustomCssJS XML, and Danmu XML back to their inventoried active paths, start Emby, then repeat package-status, HTTP-health, plugin-load, marker, permission, and SHA-256 checks.
