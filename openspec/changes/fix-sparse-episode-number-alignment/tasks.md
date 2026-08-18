## 1. Baseline and scope

- [x] 1.1 Record the exact branch/HEAD, current dirty-worktree allowlist, V21/V27 protocol markers, and deployed artifact hash without modifying or staging unrelated user files.
- [x] 1.2 Run the composite planner, r5 target-scope, main backend, frontend smart-match, and seven-day replay baselines sequentially; record any pre-existing failure before implementation.
- [x] 1.3 Freeze a production/test file allowlist covering only source-number provenance, the shared virtual-segment planner and entry points, V22/V28 protocol assets, focused regressions, and this change's OpenSpec artifacts.

## 2. Source provenance and shared alignment resolver

- [x] 2.1 Preserve provider-supplied source Episode numbers as nullable explicit evidence and add a separate stable source ordinal; remove every `index + 1` synthesis that can be mistaken for an explicit number.
- [x] 2.2 Implement one pure segment-window resolver that selects numeric or whole-window positional mode, supports zero-offset and exact explicit/server-derived anchors, preserves local/source gaps, uses checked arithmetic, and reports considered local rows and applied mappings; internal continuation derives and carries a forward-only frontier from exact anchors, ordered considered rows, and complete verified source numbering/order, advancing by projected coordinate span in numeric mode or considered rows in positional mode, never applied-mapping count.
- [x] 2.3 Route initial owning-source and production reconstruction through the resolver with each submitted selection treated as one window, while internal normalization/continuation partitions ordered windows and carries the derived exact next source EpisodeId or exhaustion: an in-scope different-source mapping is a boundary that consumes no continuing-source coordinate, adjacent real numeric gaps still advance the frontier exactly once, excluded rows are not boundaries, and same-source direct evidence is validated inside its own window without reading local Episode ProviderIds as batch evidence or changing ItemId identity, source/CommentId validation, overlap rejection, grouping, source-surplus, or persistence safety.
- [x] 2.4 Make exact `SourceStartEpisodeId` resolution authoritative; permit number-only compatibility only when the entire verified source scope has reliable unique positive provider-supplied numbering and the target number uniquely matches, then remove ordinal fallback for every unresolved number.

## 3. Entry-point parity, protocol, and stale safety

- [x] 3.1 Add the minimum closed selection intent needed to distinguish a default first segment from an explicitly changed source start; validate target-bound evidence and anchors server-side so an explicit first-segment E5 start overrides zero-offset alignment.
- [x] 3.2 Make whole-Series preview, single-Season preview, manual confirmation, automatic positive-Season initial planning, confirmed interactive snapshots, and download-time rebuild retain and apply the same intent, ordered selections, exact per-selection anchors, resolved modes, ordered considered local ItemIds, complete source provenance/order, and exact mappings; prove unattended/media-import processing never discovers a residual source.
- [x] 3.3 Enforce the confirmed Season 0 boundary before provider access: explicit single-Season S0 remains supported, while whole-Series and unattended/media-import S0 perform zero search, planning, download, binding, and metadata writes.
- [x] 3.4 Advance the mapping protocol from V21 to V22 and the frontend installation marker from V27 to V28; reject V21 drafts and include alignment intent, ordered window selections, resolved mode, exact anchors, ordered considered local ItemIds, all boundary- and frontier-determining target/source provenance and order, and exact mappings in generation/fingerprint validation.
- [x] 3.5 Prove tracked retry and seven-day replay continue using the frozen exact local/source EpisodeId/CommentId tuple without entering the new resolver or substituting a source/CommentId by current number, position, or provider detail; changed revalidated CommentId fails stale.

## 4. Deterministic regression coverage

- [x] 4.1 Add pure planner fixtures for Spy Family S3 local E1-E6/E10-E13 against source E1-E13, local/source internal gaps, local inventory beginning at E10, and unchanged dense zero-offset mapping.
- [x] 4.2 Add explicit-anchor and window fixtures for Frieren E29→source E1 with missing local E30 (E31→source E3 and source E2 never reused), explicit first-window E1→source E5, a non-first source start, requested-local-row limits, checked overflow, same-window affine continuity/conflict, and the existing reentrant case where local E29-E33→source E1-E5, local E34 is mapped to a different special source, and the next window maps local E35-E39→source E6-E10 with exhaustion; prove a new affine offset across that boundary is valid.
- [x] 4.3 Add fallback/structural fixtures for null, zero, negative, duplicate local/source numbers, source ordinal stability, one stable positional-fallback reason/diagnostic, no mixed modes, duplicate/blank source identity, blank CommentId, overlap, unresolved number-only anchors, and a unique requested number inside an otherwise unreliable source scope.
- [x] 4.4 Add controller, whole-Series/single-Season, automatic import, continuation, and rebuild fixtures proving parity of ordered selections and all boundary/frontier-determining facts, fingerprint staleness when any such fact changes, zero-write failure, internal continuation never reusing source coordinates bypassed by real gaps, source-surplus warning retention, and no synthetic local Episodes.
- [x] 4.5 Add frontend/protocol fixtures for V22/V28, default versus explicit-anchor intent, stale V21 rejection, source-start serialization, rerender/reset behavior, and absence of browser-authored exact mappings.
- [x] 4.6 Add Season 0/scope fixtures proving explicit single-S0 remains functional, whole-Series/unattended S0 invokes providers and persistence zero times, and excluded S00/foreign rows with duplicate or missing numbers do not change an eligible normal Season segment's reliability mode.

## 5. Local verification and review

- [x] 5.1 Run composite planner and r5 target-scope focused tests, then main backend, automatic/rebuild, candidate-evidence, seven-day replay, and complete frontend regressions sequentially with no competing .NET process.
- [x] 5.2 Run a clean Release build, record DLL/file versions, size, and SHA-256, and assemble a local review package containing only the verified DLL, matching V28 CustomCssJS asset, checksums, notes, and no credentials/deployment helpers.
- [x] 5.3 Run strict OpenSpec validation, `git diff --check`, changed-file/staged-file allowlist checks, and credential scans that report only pass/fail without printing secrets.
- [x] 5.4 Have a Sol high-reasoning reviewer inspect numbering provenance, one-mode-per-segment-window behavior, boundary/frontier continuation, explicit-anchor priority, every entry point, V22 stale safety, S0 boundaries, frozen replay, persistence, regressions, and package scope; resolve all blocking findings.

## 6. Approval-gated Synology and Emby validation

- [x] 6.1 Present the reviewed DLL/V28 package paths and hashes and obtain/record fresh explicit confirmation for that exact package before any file replacement or Emby restart; previously supplied access details authorize connectivity testing but are not stored in repository artifacts or output.
- [x] 6.2 After package confirmation, use the separately supplied access only at runtime to read health and back up the deployed DLL, CustomCssJS, and plugin configuration with hashes, ownership, modes, and a directly usable rollback path; store no credential in repository artifacts, scripts, or command output.
- [x] 6.3 Deploy only the explicitly confirmed DLL/V28 CustomCssJS pair, restart Emby, verify process/API/plugin health, and read back deployed hashes before functional testing; immediately restore the backup pair if health or hash verification fails.
- [x] 6.4 Perform read-only smart-match previews for Spy Family S3 and available representative anchored composite content; verify local E10 maps source E10, explicit E29→source E1 retains numeric gaps, the source-surplus warning remains advisory, and no preview starts a download or writes metadata/XML.
- [x] 6.5 Inspect bounded redacted logs for mapping/protocol failures, record live evidence without credentials or raw authenticated payloads, and either retain the verified deployment or restore the backup on any acceptance failure.
- [ ] 6.6 Do not delete or overwrite older suspect XML automatically. After successful preview, report the exact affected local Episodes and obtain separate confirmation before any force refresh.
