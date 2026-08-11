## Context

See `proposal.md` for motivation and the three delta specs for observable behavior. r3 already has one bounded alias implementation in `SearchSeasonAsync`, provider-first exact resolution in `DanmuProviderIdResolver`, and generation-gated metadata writes. The r4 work should generalize those primitives rather than add another independent matcher or persistence path.

## Goals / Non-Goals

**Goals:**

- Give Movie and Episode-context matching the same bounded discovery guarantees already available to Season matching.
- Allow structurally exact alias candidates to reach confidence through stronger year/episode evidence without promoting unrelated same-year/same-count titles.
- Make configured enabled-site order explicit and regression-tested at every exact-ID entry point.
- Make ordinary plugin IDs unique on successfully written Season/Episode items while protecting manual and unrelated metadata.

**Non-Goals:**

- Do not make Episode providers support a new standalone search API when they naturally expose episodes through a Season collection.
- Do not clear Movie or Series identifiers, rewrite old library metadata proactively, change normal-search score weights, or lower the 0.90 confidence threshold.
- Do not infer aliases across providers or from external metadata services.

## Decisions

### Extract a reusable provider search-round policy

Factor the existing bounded local-clause/provider-alias orchestration so both Season scoring and Movie scoring can use it with a media-specific search delegate and scoring delegate. Record whether each `(ProviderId, CandidateId)` was first discovered by a standard round or only by an automatic clause/alias round; de-duplication must retain standard provenance if both occur. Season and Episode-context matching continue to call provider collection search; Movie continues to call each provider's existing Movie-specific `Search(BaseItem)` implementation and non-Movie filter. Candidate keys remain `(ProviderId, CandidateId)`, aliases remain provider-local, and the original item metadata remains the scoring target.

Alternative considered: route Movie through `SearchForApi`. Rejected because this would bypass existing provider Movie filters and could admit television collections.

### Use an alias-specific evidence profile with a title floor

For candidates first discovered only in automatic clause/provider-alias rounds, Season and Episode-context collection scoring uses title 0.35, year 0.20, and episode count 0.45. Movie scoring uses title 0.70 and year 0.30. A title relevance floor of 0.72 is an eligibility gate before the unchanged 0.90 automatic confidence rule. The existing year and episode component functions remain authoritative, so missing values keep their current unknown score and mismatches are not treated as exact.

This makes an alias Season with title relevance 0.72 plus exact year/count score 0.902. It does not alter candidates found through standard rounds, remove category penalties, change configured-provider selection, or let a same-year/same-count candidate with title relevance below 0.72 auto-match.

Alternative considered: globally change the scorer weights. Rejected because ordinary full-title matching is already calibrated and would become unnecessarily permissive. Lowering the 0.90 threshold was also rejected because it would weaken every match type rather than improve evidence handling only for aliases.

### Keep Episode fallback collection-oriented

An Episode exact-ID miss continues through its containing Season preview/search, now benefiting from the generalized Season alias policy, then maps only the requested local episode. This provides alias parity without inventing provider APIs that cannot search a single Episode title reliably.

Alternative considered: search the Episode display title as a Movie-like item. Rejected because episode titles are often generic and provider identifiers are usually discoverable only inside a collection.

### Preserve site-first, scope-second exact resolution

The resolver keeps configured enabled providers as the outer loop and explicit item scopes as the inner loop. Thus provider priority is decisive when several valid IDs exist; within one provider, Episode precedes Season. Exact validation failures continue to the next scope/provider and only an entirely unresolved decision reaches binding/search.

Alternative considered: always prefer any Episode ID over every Season ID. Rejected because the user explicitly requires website priority when several provider identifiers coexist.

### Centralize success-gated plugin-ID replacement

Add a single metadata-dictionary operation used only by accepted persisted Season/Episode writes. It enumerates all registered scrapers (`AllWithNoEnabled`), removes each exact ordinary `ProviderId` except the selected key, and upserts the selected value. It never inspects, removes, or rewrites `ProviderIdManual` keys. The operation starts from the item-local provider dictionary so Emby-projected Series keys are not accidentally persisted into a Season.

Movie and Series items retain existing upsert-only behavior. The generation check and accepted file outcome remain outside/before mutation, and automatic-import and interactive paths share the same helper.

Alternative considered: remove only enabled-provider keys. Rejected because an old disabled-site ordinary ID would violate uniqueness and could become active again after configuration changes. Pattern-based deletion was also rejected because it could remove non-plugin keys that merely have a similar name.

### Treat metadata failures as diagnostic-only

Build the replacement dictionary before the repository update and assign it only in the established serialized write boundary. A repository exception remains a metadata diagnostic and never reverses persisted XML success. No cleanup occurs for failed, skipped, cancelled, timed-out, stale-generation, or non-persisted results.

## Risks / Trade-offs

- [Movie alias rounds increase provider requests] → Use the existing fixed clause/alias bounds, skip providers already at 0.90, and de-duplicate queries.
- [Broad provider title yields a weak alias] → Require the existing strong-relatedness gate and score against original metadata.
- [Exact year and count can coincide across unrelated works] → Require alias title relevance of at least 0.72 and retain the 0.90 total threshold.
- [A candidate appears in both standard and alias rounds] → Retain standard discovery provenance and normal weights so repeated queries cannot promote it.
- [Removing a disabled plugin ID surprises users] → Limit removal to exact registered ordinary keys and perform it only after a confirmed successful replacement download; all manual keys remain untouched.
- [Emby runtime objects contain inherited Series IDs] → Construct Season writes from the sanitized item-local dictionary before removing/replacing keys.
- [Concurrent tasks race] → Retain latest-started generation and per-item/provider serialization; stale tasks never enter cleanup.

## Migration Plan

1. Add deterministic regressions for cross-media aliases, multi-ID provider priority, exact-key cleanup, and failure/no-write cases.
2. Generalize search and persistence primitives, then verify interactive and automatic paths call the same implementations.
3. Bump/package `2.0.2r4`, record DLL/frontend hashes, and back up the deployed r3 DLL/configuration.
4. Deploy and fully restart Emby; verify standalone Season, Episode-context, and Movie previews plus a controlled successful Season download.
5. Confirm selected Season/Episode ordinary IDs are unique while every manual binding and all Series/TMDB/TVDB/IMDb/custom keys remain unchanged.
6. Roll back the DLL/frontend/configuration from the recorded backup if matching or metadata isolation fails; do not delete persisted XML automatically.
