## Context

See `proposal.md` for motivation and `specs/season-danmu-matching/spec.md` for the observable contract. The implementation baseline is the clean `d22a1069524bd891c5b36c758f75f4112a19e1f4` 2.0.5r1 tree on `codex/release-2.0.6-continuous-title-similarity`.

`DanmuMatchScorer.Score` currently builds a normalized parent-title set, a normalized Season-title variant set, and per-source title channels from the provider name, source metadata title, and aliases. `GetBestSeasonTitleEvidence` awards parent evidence only when the normalized source contains a complete normalized parent; `GetSeasonSimilarity` compares the resulting Season residual with `StringExtension.Distance`, which is Jaro-Winkler. The final Season score is already `0.60 * ParentScore + 0.20 * KeywordScore + 0.20 * exactYear`, rounded to four decimals, with Episode count neutral.

The 2.0.5r1 baseline also contains narrowly bounded TMDB short-parent recovery, a strict NFKC complete-title fallback, an implicit empty Season 1 variant, ordinary automatic confidence at inclusive `0.90`, and TMDB-alias acceptance at inclusive `0.80`. Those contracts constrain this change.

## Goals / Non-Goals

**Goals:**

- Supply deterministic continuous parent-title and named-Season evidence with intuitive insertion, deletion, and substitution costs.
- Make `第 2 季：西行` / `唐朝诡事录之西行` compare the descriptive residuals `西行` / `之西行` without a connector dictionary.
- Give the two confirmed Bookworm parent titles `6/7` similarity and approximately `51.43/60` parent points.
- Preserve evidence provenance: one concrete source-title channel and one eligible local alternative form each candidate evidence pair; alternatives are maximized, never summed.
- Keep runtime and allocation bounded for short provider titles and preserve C# 8 / .NET Standard 2.0 compatibility.

**Non-Goals:**

- Changing ordinary, alias, eligibility, or fidelity thresholds, including adding an indirect Season-component safety gate.
- Reintroducing the removed contradiction cap or adding an automatic-selection veto for explicit Season conflicts.
- Replacing `StringExtension.Distance`, Movie title scoring, candidate eligibility similarity, TMDB short-parent validation, or other Jaro-Winkler consumers.
- Adding semantic similarity, token dictionaries, transliteration, connector removal, or language-specific synonym rules.
- Fuzzy removal of an approximate parent span, provider/search changes, DTO changes, stored-data migration, frontend protocol changes, deployment, publishing, or release creation.

## Decisions

### 1. Add a Season-scoring-local normalized Levenshtein helper

Implement unit-cost Levenshtein distance with a two-row dynamic-programming buffer and expose only the normalized `0..1` result inside the Season title-evidence implementation. Normalize through the existing loose `DanmuMatchScorer.Normalize` pipeline first, then calculate:

```text
sim(a, b) = 1 - levenshtein(a, b) / max(a.Length, b.Length)
```

Return zero when either input is empty, subject only to the existing explicit Season 1 empty/empty branch. Identical non-empty input returns one. Keep the established four-decimal candidate field and final-score rounding points rather than introducing a second presentation or decision precision policy.

This is preferred over changing `StringExtension.Distance`, because that would silently alter Movie scoring, eligibility filtering, short-parent guards, fidelity-adjacent behavior, and unrelated call sites. It is preferred over LCS/Indel normalization because LCS would score the reversed short title `西行` / `行西` above zero, and Indel normalization is more permissive for inserted prefixes and suffixes.

### 2. Preserve exact parent decomposition and add a partial fallback

Keep the current exact-containment branch: when a normalized source-title channel contains an eligible normalized parent, compare that identical parent span for full parent evidence and remove the exact span before Season comparison. This preserves full parent credit for `唐朝诡事录之西行` while leaving `之西行` as the real residual.

When a source-title channel contains no exact parent, compare the complete normalized source title with each parent alternative using normalized Levenshtein and retain the best pair in that same channel. Do not remove an approximate span: the unchanged full source value remains the Season comparison input. This conservative fallback makes the Bookworm pair continuous without fabricating a boundary between an approximate parent and a possible Season suffix.

Retain `SelectBetterSeasonTitleEvidence`'s weighted parent-plus-Season choice and existing tie rule so parent evidence from one source-title channel cannot be added to Season evidence from another. Ordinary scoring continues to use the authoritative parent only; the established TMDB-alias path may include its already-eligible local parent alternatives.

### 3. Validate Season markers before deriving descriptive residuals

Run the existing explicit-marker conflict checks against the normalized, marker-bearing source and target values before any removal. A wrong, zero, mutually inconsistent, or currently recognized marker that cannot be resolved to one consistent positive Season number continues down the existing zero Season-evidence path; this change does not expand the marker grammar.

For a non-conflicting pair, derive both descriptive forms by removing only explicit markers confirmed as the expected Season. If either side retains descriptive text, compare the two descriptive forms and treat a pure-marker side as empty; do not also maximize over the marker-bearing originals. If both sides become empty pure markers, retain the existing generic-label comparison instead. This prevents a shared `season2` prefix from dominating `season2西行` / `season2东行`, makes `season2` / `season2西行` compare as empty / `西行`, and allows `season2西行` / `之西行` to become `西行` / `之西行`.

Do not remove `之`, punctuation-independent lexical text, or any other connector. Continue applying the existing Season 1 empty rule and strict complete-title fallback after ordinary residual evidence, taking their maximum exactly as in 2.0.5r1.

### 4. Keep selection and surrounding scoring behavior literal

Continue producing `ParentTitleScore`, `KeywordScore`, `MatchScore`, and reasons through the existing score assembly and four-decimal rounding. Do not add a hidden raw-score gate or a `KeywordScore` minimum. Therefore an otherwise unique ordinary candidate with exact parent/year evidence and `0.5` Season similarity remains exactly `0.90` and can be selected, while `西行` / `之西行` becomes approximately `0.9333`.

The TMDB-alias `>= 0.80` rule, alias stopping behavior, removed contradiction cap, fidelity tie evidence, provider ordering, candidate eligibility floor, exact-year binary evidence, neutral Episode count, and Movie formula remain unchanged. Focused regressions must prove isolation rather than relying only on the new positive examples.

### 5. Stamp 2.0.6 without changing protocol identities

Set assembly/file version to `2.0.6.0` and informational/configuration/TMDB User-Agent version to `2.0.6`. Update the cumulative `UPDATE.md` and README current-version text while preserving all earlier entries, installation notes, screenshots, and demonstration images. The frontend installation marker V28 and mapping protocol V22 remain unchanged because no frontend behavior or DTO changes; update only any human-readable pairing/version text required for consistency. The existing informational-version-derived configuration cache token may change naturally with the version stamp.

## Risks / Trade-offs

- [The unchanged inclusive threshold allows a one-character substitution in a two-character Season name to auto-select at exactly 90 points] → This is an explicit product decision; lock `>= 0.90` and the absence of a component gate in selection regressions.
- [The unchanged alias threshold can accept full parent/year evidence even when conflicting Season evidence contributes zero] → Preserve the 2.0.5r1 no-cap/no-veto contract and make the resulting component score visible and deterministic.
- [Partial parent similarity against a full source title may also include a Season suffix] → Keep exact containment as the full-credit decomposition path and never invent a fuzzy removal boundary; unmatched suffixes conservatively reduce partial parent evidence.
- [Removing a correct marker could inflate similarity by discarding shared structural text inconsistently] → Check conflicts first and use the descriptive form only when marker removal leaves actual descriptive text; add paired named-Season and pure-generic regressions.
- [Edit distance is quadratic in title length] → Use a two-row implementation; provider titles are bounded short strings and no new network or unbounded collection loop is introduced.
- [A global metric replacement could regress Movie and eligibility behavior] → Keep the helper local to Season title evidence and freeze representative Jaro-Winkler/Movie/eligibility results.
- [Version-only documentation edits could erase cumulative history or README media] → Patch only current-version text and prepend the new update entry; verify prior headings and image links remain present.

## Migration Plan

1. Implement and test the metric, partial-parent fallback, and marker-derived Season residuals in the isolated 2.0.6 worktree.
2. Run focused scorer/selection tests, the complete deterministic backend regression suite, strict OpenSpec validation, `git diff --check`, version consistency checks, credential-safe scope inspection, and a sequential clean Release build.
3. Stamp and document 2.0.6 only after behavior tests pass; record hashes and verification evidence without credentials or raw authenticated responses.
4. Present the local implementation and artifacts for user review. Do not merge, push, tag, publish, deploy, or perform live Emby matching without separate explicit authorization.
5. Roll back by reverting the isolated behavior/version changes or returning to immutable baseline `d22a1069524bd891c5b36c758f75f4112a19e1f4`; no data migration is required.
