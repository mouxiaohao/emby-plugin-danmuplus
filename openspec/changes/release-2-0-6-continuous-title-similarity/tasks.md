## 1. Baseline and characterization

- [x] 1.1 Verify the implementation worktree is `codex/release-2.0.6-continuous-title-similarity` at baseline `d22a1069524bd891c5b36c758f75f4112a19e1f4`, confirm the original checkout remains untouched, and record the initial changed-file allowlist.
- [x] 1.2 Add failing scorer fixtures that characterize `西行` / `之西行`, `西行` / `东行`, `西行` / `行西`, `西行` / `北斗`, exact equality, and the existing Season 1 empty-residual boundary with four-decimal component and final scores.
- [x] 1.3 Add failing parent-title fixtures for exact embedded-parent decomposition, empty/disjoint zero endpoints, and the normalized Bookworm pair at edit distance 3 over maximum length 21, expecting `ParentTitleScore = 0.8571` and approximately `51.43/60` parent points.
- [x] 1.4 Add failing named-Season marker fixtures for `第2季：西行` / `之西行`, correct-marker `西行` / `东行`, pure generic labels, pure-marker / named-Season mixing, empty/non-empty residuals, and an explicit wrong Season number.

## 2. Continuous Season title evidence

- [x] 2.1 Implement a Season-scoring-local, unit-cost, two-row Levenshtein calculation and normalized `1 - distance/maxLength` similarity with exact, empty, and bounded `0..1` behavior.
- [x] 2.2 Replace only the ordinary Season-name residual's Jaro-Winkler comparison with normalized Levenshtein while preserving the explicit conflict checks, Season 1 empty rule, strict complete-title fallback, and TMDB short-parent recovery.
- [x] 2.3 Derive descriptive Season forms only after validating every marker against the expected Season; remove only confirmed correct generic markers that leave descriptive text and never delete `之` or another lexical connector.
- [x] 2.4 Ensure shared correct marker text cannot inflate named residual similarity, a pure-marker / named-Season pair compares as empty / descriptive text, and only a pair with no descriptive text on either side continues through the established pure-generic-label behavior.

## 3. Continuous parent-title evidence

- [x] 3.1 Preserve exact parent containment as full evidence and exact residual decomposition within the same source-title channel.
- [x] 3.2 When no exact parent is contained, score the complete normalized source title continuously against eligible parent alternatives, retain the best same-channel pair, and do not remove an approximate parent span.
- [x] 3.3 Preserve ordinary-versus-TMDB parent-alias participation, weighted evidence selection, matched-parent tie behavior, source-title channel isolation, and non-additive maximum semantics.
- [x] 3.4 Keep candidate eligibility, `SimilarityAgainstTitle`, `StringExtension.Distance`, Jaro-Winkler consumers, fidelity evidence, and Movie scoring unchanged.

## 4. Scoring and selection regressions

- [x] 4.1 Prove exact parent/Season/year evidence remains 100 points and `第 2 季：西行` against `唐朝诡事录之西行` becomes `KeywordScore = 0.6667` and `MatchScore = 0.9333`, not full Season credit.
- [x] 4.2 Prove `西行` / `东行` yields `KeywordScore = 0.5000`, `MatchScore = 0.9000`, and remains automatically selectable when all existing uniqueness and eligibility conditions are satisfied; add no `KeywordScore` gate or strict `> 0.90` comparison.
- [x] 4.3 Prove ordinary `>= 0.90` and TMDB-alias `>= 0.80` comparisons, alias stopping behavior, and the 2.0.5r1 no-cap/no-veto handling of conflicting evidence remain unchanged.
- [x] 4.4 Prove reordered/disjoint short names score zero, wrong explicit Season numbers keep Season evidence at zero, and no marker or connector path bypasses the edit metric to supply unintended full credit.
- [x] 4.5 Re-run and extend TMDB short-parent, JOJO split-season, strict NFKC complete-title, Season 1, source-channel isolation, equal-score ordering, descending-score, Episode-neutral, and exact-year regressions.
- [x] 4.6 Freeze representative Movie, eligibility-floor, and direct Jaro-Winkler outputs to demonstrate that the continuous metric is isolated to Season parent/name scoring.

## 5. DanmuPlus 2.0.6 metadata and cumulative documentation

- [x] 5.1 Set assembly/file version to `2.0.6.0` and informational/configuration/TMDB User-Agent version to `2.0.6`; update matching assertions without changing plugin/page identity, frontend installation marker V28, or mapping protocol V22.
- [x] 5.2 Prepend a cumulative 2.0.6 entry to `UPDATE.md` and update README/current pairing text while preserving all prior release history, compatibility notes, screenshots, demonstration images, and retained operational behavior.
- [x] 5.3 Create `artifacts/2.0.6/VERIFICATION.md` containing the immutable baseline, focused/full test matrix, version and hash slots, threshold decisions, approval gates, and no credentials, raw authenticated responses, or private deployment paths.
- [x] 5.4 Run version/resource/document consistency checks and verify the informational-version-derived configuration cache token changes without a frontend protocol-marker bump.

## 6. Complete local verification

- [x] 6.1 Run the main backend regression and affected specialized projects sequentially, including TMDB alias, title fidelity, search quality, search-term policy, candidate selection, bounded search, Episode scope/mapping, seven-day replay, and single-target download coverage.
- [x] 6.2 Run frontend/configuration regressions to prove response shape, server score ordering, V28/V22 protocol identity, saved configuration, and unrelated UI behavior remain unchanged.
- [x] 6.3 Run a clean sequential Release solution build, inspect assembly/file/product versions, and record the DLL size and SHA-256 without allowing competing .NET builds to create mapped-file locks.
- [x] 6.4 Run strict OpenSpec validation, final OpenSpec status, `git diff --check`, changed-file allowlist review, and a credential-safe source/artifact/package scan that never prints secret values.
- [x] 6.5 Assemble and re-read a local 2.0.6 review package containing only the verified DLL, matching frontend asset when required, checksums, cumulative notes, and verification record; exclude build trees, logs, backups, diagnostics, credentials, and private helpers.

## 7. Approval-gated live Emby verification

- [x] 7.1 Keep the verified package local and obtain explicit user authorization before any Synology file replacement, Emby restart, live match request, push, merge, tag, publication, or deployment.
- [x] 7.2 If deployment is authorized, back up the paired deployed DLL, configuration, and frontend asset with hashes, ownership, and modes before replacement, and retain a directly usable rollback copy.
- [x] 7.3 If live matching is authorized, verify Emby health and read-only previews for both Tang Dynasty Mystery seasons, the confirmed Bookworm parent-title pair, an exact title, a 90-point substitution boundary, a wrong Season marker, a TMDB-alias result, and an unaffected Movie without initiating download, binding, or metadata writes.
- [x] 7.4 Compare deployed hashes with the approved local package, inspect bounded redacted logs, record results, and restore the backup pair plus health-check Emby if any acceptance check fails.

## 8. Final review and handoff

- [x] 8.1 Have a Sol high-reasoning reviewer inspect the final diff, normalized-edit implementation, marker-before-removal invariant, parent evidence provenance, unchanged inclusive thresholds, isolation boundaries, documentation preservation, and verification evidence.
- [x] 8.2 Resolve every blocking review finding, repeat affected deterministic checks, and confirm the original checkout remains untouched and no external action occurred without its explicit gate.
- [x] 8.3 Present the local 2.0.6 implementation, exact representative scores, test/build/OpenSpec evidence, artifact paths and hashes, residual threshold trade-offs, and approval-gated live status for the user's next decision.

## 9. Identifier-free regression gate maintenance

- [x] 9.1 Narrow the `R4IdentifierMetamorphic` source gate by explicitly allowing only `DanmuProviderIdResolver.GetEnabledProviderIdKeys` metadata projection while continuing to reject every other resolver call and saved manual binding; add positive and negative guard self-checks without changing plugin business code.
- [x] 9.2 Run the corrected R4 regression and relevant deterministic checks, update both credential-safe verification records to replace the known baseline-failure caveat with the passing result, confirm the review-package hashes and deployed DLL remain unchanged, and obtain final read-only review.
