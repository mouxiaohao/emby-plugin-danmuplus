# DanmuPlus 2.0.7 local review verification

This is the title-family-corrected review package from the isolated 2.0.6r2 baseline worktree (`07fbb408d54ee1b6201d4f217122079070527c5a`). The preceding Provider-lock-only build was temporarily deployed for an authorized read-only live preview, failed that preview, and was rolled back. This replacement build has now passed its authorized controlled deployment and read-only live acceptance; it is not published, pushed, merged, tagged, or released.

## Contents

- `Emby.Plugin.Danmu.dll`: Release assembly/file version `2.0.7.0`, product version `2.0.7`, 1,725,952 bytes.
- `DanmuSmartMatch.CustomCssJS.js`: frontend installation flag `__embyDanmuSmartMenuV32` occurs once; the obsolete V31 flag occurs zero times; mapping protocol remains V22.
- `UPDATE.md`: cumulative 2.0.7 notes; 26,157 bytes; SHA-256 `0FD5C0D252E3FB6CA5AA7FC2CBD3F2CA00303252DF0C62E780B083408BDB91C8`.

## Verification completed

- `dotnet restore`, clean Release build, the full main regression suite, composite-season planner suite, and all 12 standalone regression projects passed sequentially.
- Provider-lock regressions passed: cross-provider duplicates and combined sources are excluded, same-provider ambiguity remains a rejection, a locked provider never falls back to another provider, and evidence/fingerprint rebuild rejects Provider drift fail closed.
- Title-family regressions passed: a legal Part ordinal cannot manufacture family identity; wrong-arc same-Provider details may be resolved but produce zero evidence registration, authoritative build, or committed mapping; the valid same-family metadata continuation remains selectable; and parent-only fallback stays limited to logical Season 1 without a confirmed non-parent core.
- Node syntax checks and the frontend Smart Match regression suite passed.
- Strict OpenSpec validation passed for `release-2-0-7-recursive-remainder-auto-matching` and `fix-sparse-episode-number-alignment`; Sol final review of the title-family repair returned PASS with no remaining P0/P1 finding.
- `git diff --check` and the changed-file allowlist audit passed. The clean Release build completed with 131 pre-existing compiler/package warnings and zero errors.
- The package contains no PDB file, private absolute path, deployment endpoint, Authorization material, or credential material.
- Documentation-only refresh: the 2.0.7 notes now explicitly record the first-segment Provider lock, locked-Provider completion gate, and strict title-family rule that a Part ordinal cannot manufacture family identity, including the accepted JOJO/Stone Ocean field correction. The reviewed DLL and JavaScript are unchanged; only the UPDATE payload and its manifest entry were recomputed.

## Initial pre-Provider-lock live acceptance and field finding

- The originally reviewed pre-Provider-lock 2.0.7 DLL, matching V32/V22 CustomCssJS content, CustomCssJS configuration, and Danmu configuration were deployed only after the active 2.0.6r2 pair/configuration had been copied to a root-only rollback set and verified. Emby restarted with HTTP 200, reported plugin version `2.0.7.0`, retained the reviewed frontend and configuration hashes/ownership/modes, and logged no Danmu error/exception/failure line.
- A read-only Frieren Season MatchPreview completed successfully: 38 local Episodes were mapped as the initial 28-Episode group plus a 10-Episode `remainder-logical-season` group for logical Season 2, using the remainder-first year 2026 and score 1.0, with no unmatched run.
- A read-only JOJO Season 2 preview safely retained the confirmed 24-Episode `星尘斗士` prefix and left the later 24 Episodes unmatched. Authoritative candidate inspection showed one same-Provider `星尘斗士 埃及篇` continuation and another Provider's candidate with the same 2015/24 tuple; the pre-lock implementation incorrectly counted both as ambiguity instead of restricting recursion to the first segment's Provider.
- A read-only JOJO Season 5 preview likewise stopped after the initial 12-Episode `石之海` prefix and left the next 12 local Episodes unmatched even though the initial Provider exposed Part 2/3 while another Provider exposed a 38-Episode combined source. This reinforced the need for an immutable first-segment Provider lock.
- These partial results were safe and showed no top-level matching failure. No confirmation, download, binding, import, background task, cancellation mutation, or metadata write was issued. The field findings were folded into the Provider-lock specification and deterministic regressions before the next reviewed build.

## Authorized live attempt and rollback

- The active pre-Provider-lock 2.0.7 DLL, matching V32/V22 CustomCssJS content, CustomCssJS configuration, and Danmu configuration were backed up together in a root-only location and verified before replacement. The older verified 2.0.6r2 rollback set was also retained and rechecked.
- The previous Provider-lock-only DLL SHA-256 was `B5E1667BBB5F11F7713C0555A63097F8121BAD4EA42999B1F2765752FFE9717D`. The reviewed CustomCssJS SHA-256 remained `40860D2EE2039F2DF123CF0FAFDBDD2D5A3706BBDDB668241395F7AAFE42FDCC`; its decoded deployed content was byte-identical after newline normalization, so the CustomCssJS XML was deliberately not rewritten.
- The corrected DLL was replaced atomically with a rollback trap. Emby restarted healthy, reported plugin version `2.0.7.0`, loaded with no Danmu error/exception/failure line, and retained the expected DLL/frontend/configuration hashes, V32 marker, V22 protocol, ownership, and modes.
- The first read-only JOJO Season 2 MatchPreview returned HTTP 200 in 18.072 seconds, but produced an unsafe false-positive 48/48 plan: the first 24 Episodes used `JOJO的奇妙冒险 星尘斗士`, while the remainder incorrectly used same-Provider but different-title-family `JOJO的奇妙冒险 石之海 Part.2` and `Part.3` groups of 12 Episodes each. Provider locking worked, but Part applicability did not keep the continuation inside the authoritative `星尘斗士` title family.
- All later JOJO Season 5 and Frieren previews were stopped immediately. No download, binding, import, background task, metadata write, or confirmation request was sent.
- The deployment was rolled back to the verified pre-Provider-lock 2.0.7 DLL (`12B869CE99CEC41CF289EBEBFDADE32DF41EE3EF168A39E678EE8A71AA1D9AB9`). Emby again returned HTTP 200 with clean Danmu startup logs; V32/V22 frontend content, Danmu configuration, ownership, modes, and their hashes remained unchanged.

The failed Provider-lock-only DLL must not be redeployed. Its same-title-family defect and exact false positive are corrected and covered by deterministic regressions in the replacement package.

## Final title-family live acceptance

- Before the final replacement, the active pre-lock 2.0.7 DLL, V32/V22 CustomCssJS content, CustomCssJS configuration, and Danmu configuration were copied to a new root-only backup and verified together. The independently retained 2.0.6r2 and pre-lock 2.0.7 rollback sets were also read back and passed their checksum manifests.
- The final DLL SHA-256 was `5598B4D3B33A7124AD6811D2CDAE5E14884A20347923B4F563914596FC97E752`. The reviewed CustomCssJS SHA-256 remained `40860D2EE2039F2DF123CF0FAFDBDD2D5A3706BBDDB668241395F7AAFE42FDCC`; the deployed decoded JavaScript was byte-identical after newline normalization, so the CustomCssJS XML was deliberately not rewritten.
- The DLL was replaced atomically with a rollback trap. Emby restarted with HTTP 200, reported plugin version `2.0.7.0`, loaded with zero Danmu error/exception/failure lines, and retained the expected DLL/frontend/configuration hashes, V32 marker, absence of V31, V22 protocol, ownership, and modes.
- JOJO Season 2 had 48 local Episodes. Its read-only MatchPreview returned HTTP 200 in 18.018 seconds and produced exactly two same-Provider groups with no unmatched run: `scored` `JOJO的奇妙冒险 星尘斗士` (24 Episodes, 2014) followed by `remainder-metadata` `JOJO的奇妙冒险 星尘斗士 埃及篇` (24 Episodes, 2015). No `石之海` or unrelated Part group entered the plan, and both warning flags were false.
- JOJO Season 5 had 24 local Episodes (`E1-E24`, all carrying local year 2022). Its read-only MatchPreview returned HTTP 200 in 6.018 seconds and mapped only the locally available remainder: `scored` `JOJO的奇妙冒险 石之海` (12 Episodes, 2021) followed by same-Provider `remainder-part` `JOJO的奇妙冒险 石之海 Part.2` (12 Episodes, 2022). There was no unmatched run; Part 3 and the cross-Provider 38-Episode combined source were not used, and both warning flags were false.
- Frieren had 38 local Episodes, with `E29-E38` carrying local year 2026. Its read-only MatchPreview returned HTTP 200 in 6.611 seconds and retained the expected same-Provider 28+10 plan with no unmatched run: `scored` `葬送的芙莉莲` (28 Episodes, 2023, score 1.0) followed by `remainder-logical-season` `葬送的芙莉莲 第二季` (10 Episodes, 2026, score 1.0). Both warning flags were false.
- Final readback again returned HTTP 200 with the exact final DLL, unchanged V32/V22 frontend and configuration hashes, original ownership/modes, a running package, and zero Danmu error/exception/failure lines. No confirmation, download, binding, import, background task, cancellation mutation, or metadata write was issued during live acceptance. Cancellation, background non-recursion, write fences, warning presentation, and silent-unmatched behavior remain covered by deterministic regressions rather than risky live mutation.

The title-family-corrected package is live-accepted and currently deployed. No push, merge, tag, GitHub Release, or OpenSpec archive was performed.
