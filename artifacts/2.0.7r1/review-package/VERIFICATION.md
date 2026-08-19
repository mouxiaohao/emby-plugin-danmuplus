# DanmuPlus 2.0.7r1 local review verification

This review package was rebuilt from the mandatory single-string-branch correction, received the replacement Sol final review, and was deployed under the user's explicit authorization for controlled live validation. It has not been pushed, merged, tagged, published, released, or archived.

## Reviewed payload identity

- `Emby.Plugin.Danmu.dll`: 1,725,952 bytes; SHA-256 `535CA3649E36459D7A1A0990F31191A6A8621F304222ED0DB248B3396C058F42`; Assembly/File/Product versions `2.0.7.0` / `2.0.7.1` / `2.0.7r1`.
- `DanmuSmartMatch.CustomCssJS.js`: 251,708 bytes; SHA-256 `43A10697A33CF590D7CFA9B2A27832E2142DB7D7D1028C6DFB82734F97CB01A2`; formal V33 flag once, V32 and older formal flags absent, mapping protocol V22.
- `UPDATE.md`: 27,521 bytes; SHA-256 `A714E8B898D69133FF97F3FC9EBDB282C1F0824732825E16AF997A7D9E0DC917`; the cumulative 2.0.7 and older history is retained.
- `SHA256SUMS.txt` contains exactly the three payload entries above. `VERIFICATION.md` remains intentionally outside the manifest contract.
- The package contains exactly five files. The DLL is byte-identical to the fresh Release output; JavaScript and UPDATE are byte-identical to their reviewed sources; all three manifest hashes match.
- Documentation-only refresh: the r1 notes now state that the fixed suffix is constructed inside the same `scopeSummaryLine` ignored-breakdown branch with no independent second gate, and the retained 2.0.7 notes now include the Provider-lock/completion and strict title-family field corrections. The reviewed DLL and JavaScript are unchanged; only the UPDATE payload and its manifest entry were recomputed.

## Fresh single-branch verification

- With no competing `dotnet` process, clean, forced restore, and Release build exited `0/0/0` with 131 pre-existing warnings and zero errors.
- Production JavaScript syntax, regression syntax, and the complete Smart Match frontend regression exited `0/0/0` after the clean build.
- The frontend suite verifies that `scopeSummaryLine` creates one indivisible positive branch containing the authoritative ignored breakdown and its immediately adjacent fixed safety suffix. The notice literal occurs once in production, `scopePresentationLine` has zero references, and composite, non-composite whole-Series, and direct single-Season renderers are the three direct consumers.
- The same suite covers each authoritative ignored category, mixed/zero/missing/invalid/non-finite/negative/fractional/casing inputs, strict rejection of booleans/arrays/objects/empty strings, positive-to-zero cleanup, whole-Series isolation, and zero selection/request/mapping/download behavior for ignored items.
- The main full Release regression and `--composite-season-planner` each exited `0`.
- All 12 independently enumerated projects were restored and run sequentially with `restore=0/run=0`: BoundedSearchPolicy, EpisodeSelectionPolicy, MgtvSearch, R207RemainderCore, R207RemainderOrchestration, R3SearchQuality, R4IdentifierMetamorphic, R4ParentSeasonContext, R5TargetSeasonScope, SearchTermPolicy, TemporaryRangePolicy, and TitleFidelity.

## Version, protocol, and managed-boundary audit

- Assembly/File/Product/configuration/User-Agent identities are `2.0.7.0` / `2.0.7.1` / `2.0.7r1`; the generated configuration cache token is `2-0-7r1`.
- Frontend formal markers V33/V32/older are `1/0/0`. Frontend and backend mapping protocol remain V22.
- Mono.Cecil comparison of the reviewed 2.0.7 DLL and this fresh r1 DLL found 6,255 methods on each side, zero additions/removals, and three expected method-body differences only: configuration version initialization, generated configuration page naming, and TMDB User-Agent use.
- All 863 managed type shapes are identical. Four expected version/cache/User-Agent constants changed, and only the generated configuration-page resource changed.
- Controller, Model, Season-planning, planner, download, and Provider matching scopes have zero changed method bodies. This is an assembly-boundary signature/body audit; it does not claim byte identity for version metadata or the explicitly expected configuration resource.

## OpenSpec, scope, documentation, and safety audit

- Strict validation passed for `release-2-0-7-r1-conditional-ignore-notice`, `release-2-0-7-recursive-remainder-auto-matching`, and `fix-sparse-episode-number-alignment`. Their status/show planning artifacts are coherent; the parent change remains 71/71 and fix-sparse remains 27/28 with only its separately gated live item open.
- The r1 tool/edit trail is confined to the frozen frontend/version/assertion/documentation/change/package allowlist. The managed comparison above supplies the release-boundary evidence that excluded backend behavior scopes did not change.
- README retains all 17 baseline headings, all three demonstration-image references, and its installation/configuration sections. UPDATE retains all 28 baseline headings and the complete 2.0.7-and-older history.
- `git diff --check` passed. Changed release text contains zero production/documentation credential assignments, zero Authorization or Bearer credential material, zero private deployment endpoints, zero private local paths, and zero private NAS paths. Two credential-like assignments are deterministic regression placeholders only.
- The package contains no PDB file. PE scanning found zero RSDS records, private PDB/absolute paths, private deployment endpoints, credential assignments, Authorization material, or Bearer material. Three generic `.pdb` identifiers contain no path.

## Superseded candidates and live-state boundary

- The pre-P1 JavaScript and its paired build remain invalidated historical evidence.
- The later JavaScript `A3DF9F78B19FF122F50559B518A9B3947AE1455AD8C319A23090E1E81921242B`, its former review package pairing, and its isolated remote staging copy are obsolete after the single-string-branch requirement and MUST NOT be deployed. That obsolete hash occurs only in this historical record, never in the current DLL/JavaScript/UPDATE payloads or manifest.
- A separately created, verified root-only backup of the former reviewed 2.0.7 DLL/V32/CustomCssJS/Danmu configuration set remains retained and restorable. The corrected root-only r1 staging set is also retained; the obsolete `A3DF...` staging payload was not reused.
- No connection endpoint, account, credential, token, private local path, remote backup path, or staging path is recorded here.

## Controlled deployment, rollback, and final readback

- The initial atomic deployment reached replacement and restart, but the local post-deployment verifier incorrectly required new DLL/XML sizes to equal the old file sizes. That script-only metadata gate rejected the valid new sizes and triggered its automatic rollback. The paired 2.0.7 DLL/V32/CustomCssJS/Danmu files, ownership, modes, HTTP health, and V22 state were restored and revalidated successfully; this was a verifier defect, not a candidate failure.
- Before retrying, the metadata verifier was corrected and exercised with synthetic evidence: unchanged uid/gid/mode plus the staged DLL/XML sizes passed, while uid, mode, and staged-size counterexamples each failed. A complete read-only precheck then uniquely resolved the verified backup and corrected staging sets before the same-process atomic replacement and controlled restart.
- The final deployment and independent closing readback passed with HTTP 200, active DLL SHA-256 `535CA3649E36459D7A1A0990F31191A6A8621F304222ED0DB248B3396C058F42`, plugin/configuration `2.0.7.0` / `2.0.7r1`, cache token `2-0-7r1`, and unchanged Danmu configuration SHA-256 `02519AFD92022BABACF9E6D516C44C0DDE0117A2744593501D8CB29222536069`.
- The deployed target is unique and canonically identical to the reviewed JavaScript: normalized-LF SHA-256 `459DEC015075C85354F858C5B597FBF53F53098780846EEC2678C05B92D53D5C`, V33/V32/V22 counts `1/0/1`. Active owner/group/mode values match the predeployment manifest, and the latest startup segment has one Danmu load line and zero Danmu error/exception/failure lines.
- The verified 2.0.7 rollback set and corrected r1 staging set remain root-only and checksum-valid. The final closing readback issued zero MatchPreview calls and zero business writes, cleared transient authentication values, and closed SSH.

## Browser cache boundary and read-only live acceptance

- An already-open Chrome tab initially retained the predeployment in-memory frontend. Its first positive and zero-ignore previews therefore executed with the old script; the zero-ignore page still showed the sentence. That observation is cache-bound historical evidence and is not accepted as 2.0.7r1 behavior. A complete page reload was required before authoritative V33 validation.
- After the full reload, the positive whole-Series fixture `妄想学生会` exposed 26 local Episodes across two returned Seasons. Season 1 reported displayed/eligible `21/13`, authoritative S00 ignored count `8`, the fixed safety suffix exactly once, and 13 temporary-group Episodes. Season 2 reported displayed/eligible `23/13`, authoritative S00 ignored count `10`, the fixed suffix exactly once, and 13 temporary-group Episodes. Neither Season rendered an ignored Episode as a selection control.
- The zero-ignore `葬送的芙莉莲` fixture reported displayed/eligible `38/38`; its scope contained no `只读忽略` branch and the safety sentence occurred zero times. Its eligible inventory remained represented as two groups of 28 and 10 Episodes.
- Live interaction was limited to read-only MatchPreview, closing the dialog, and the complete page reload. No confirm, download, binding, import, background task, metadata write, composite submission, or download payload was created.

All 21 r1 tasks are now evidenced locally and live. Git publication remains separately gated: no push, merge, tag, release, publication, or OpenSpec archive was performed.
