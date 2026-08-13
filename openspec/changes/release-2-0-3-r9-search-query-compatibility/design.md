## Context

Live testing against the deployed 2.0.3r8 DLL established that the browser, Emby request binding, proxy, and DandanPlay API preserve spaces and `+`. DandanPlay returns four `one punch man` records, but the manual MatchPreview path applies `IsEligibleSeasonCandidate` with the custom English query and removes all Chinese-titled DandanPlay records at a 0.58 title-evidence threshold. Youku survives only because its returned display titles contain English aliases.

## Goals / Non-Goals

**Goals:**

- Make explicit manual custom search trust provider discovery for structurally valid Season records.
- Keep scoring based on target library metadata so ranking and automatic confidence remain provider-neutral.
- Preserve strict title eligibility for automatic searches without a custom keyword.
- Verify spaces and literal `+` at the exact MatchPreview boundary.

**Non-Goals:**

- No provider request-encoding rewrite; official DandanPlay accepted `%20`, `+`, and `%2B` in live tests.
- No synonym database, translation service, or provider-specific alias mapping.
- No changes to persistence, episode mapping, download behavior, or provider result models.

## Decisions

1. **Separate discovery eligibility from confidence scoring for explicit manual queries.** A provider response proves only that the provider associated the query with a record; identifier/title/media-type validation establishes structural eligibility. Target metadata still supplies score and auto-selection confidence. This allows cross-language aliases without treating every result as a confident match.

2. **Preserve automatic-search filtering.** When no custom keyword is supplied, the existing identity-bearing Series title and title-evidence requirement remains unchanged. This keeps automatic library import fail-closed.

3. **Do not change query encoding without evidence.** Official DandanPlay direct tests returned the same complete English-alias results for `%20`, form-style `+`, and literal-plus `%2B`. Other audited adapters either encode query values or use JSON. Tests will protect semantic inputs, but production request construction will not be broadly rewritten.

4. **Test the predicate and the full search engine.** Predicate tests cover structural validity and custom/automatic differences. Engine tests use fake providers to prove a Chinese result returned for an English alias survives merge, ordering, and diagnostics.

## Risks / Trade-offs

- [Risk] A provider may return noisy results for a manual query. → Retain provider-neutral scoring, the 60-result bound, and confidence gating; the user explicitly controls final manual selection.
- [Risk] Relaxing the wrong path could weaken automatic import. → Branch only on a non-empty explicit custom keyword and add a negative automatic-search regression.
- [Risk] The current worktree and deployed r8 DLL may differ. → Locate and update the actual source used for r9 before building, then compare behavior against the decompiled r8 predicate and deployed MatchPreview response.

## Migration Plan

1. Update only manual custom-search eligibility and tests.
2. Run deterministic regression tests and Release build.
3. Deploy 2.0.3r9 over r8 and repeat Season 3 MatchPreview with `one punch`, `one punch man`, `one+punch`, `一拳 超人`, and `一拳+超人`.
4. Roll back to the existing r8 backup if candidate or automatic-import behavior regresses; no data migration is required.
