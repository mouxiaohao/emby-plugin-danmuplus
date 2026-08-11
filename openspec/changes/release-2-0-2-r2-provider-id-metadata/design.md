## Context

See `proposal.md` for motivation. The exact-match resolver already calls each selected provider's `GetMedia` or direct-Episode detail path, but the shared `ScraperMedia` contract retains only identifiers and episodes. The resolver consequently builds a display candidate from the local Emby scope name and leaves all other candidate metadata unset. Search results use a separate richer model, but invoking search to enrich an exact identifier would weaken exact-match semantics.

Provider detail capabilities differ. Dandan can provide title, year, category, declared count, and episodes; Bilibili PGC/BVID can provide title, year, count, and episodes but not a consistently reliable category; iQiyi can provide title, category, count, and episodes but not year in the current detail DTO; current MGTV, Tencent, and Youku identifier paths reliably provide episode lists/counts while other fields require provider-specific detail evidence before they can be exposed.

## Goals / Non-Goals

**Goals:**

- Carry explicitly returned provider metadata through the existing exact-ID detail path into preview candidates.
- Keep exact-ID matching independent of keyword search and scoring.
- Represent unavailable metadata honestly and preserve all r6/r1 download and persistence contracts.

**Non-Goals:**

- Guess provider metadata from local Emby values, the first episode, or category heuristics.
- Normalize provider titles into local naming conventions.
- Redesign search scoring, provider priority, Episode mapping, or XML download behavior.

## Decisions

### Extend the resolved-media contract instead of joining search results

Add nullable/optional media-level `Title`, `Year`, `Category`, and `EpisodeCount` fields to `ScraperMedia`. Provider adapters populate only values supported by their identifier-specific response. `EpisodeCount` preserves a provider-declared total when it differs from the filtered usable episode list.

Alternative considered: run provider search and locate the supplied ID. Rejected because it adds latency and ambiguity, can fail even when the exact ID is valid, and would violate the requirement to bypass search/scoring.

### Populate metadata at provider adapter boundaries

Each provider's `GetMedia` mapping owns interpretation of its detail DTO. Existing detail calls should be reused; an additional provider call is acceptable only when it is keyed by the same identifier and fetches that object's details, never by title/keyword. Fields remain unset when the provider response does not establish them.

Alternative considered: have the generic resolver inspect provider DTOs. Rejected because it couples the resolver to provider-specific response shapes and makes field provenance unclear.

### Construct exact candidates solely from resolved upstream media

The resolver maps `ScraperMedia` fields into `DanmuMatchCandidate`. It uses a positive declared `EpisodeCount`; otherwise it uses the count of usable resolved episodes. It does not use `scope.Name` as the upstream title. A stable generic “resolved identifier” placeholder may be used only when the UI requires non-empty text, and must not claim to be the website title.

Direct Episode resolution maps the returned episode title to the resolved media title and retains the one-item episode list. Parent title/year/category remain unset unless the same identifier-specific response explicitly provides them.

### Preserve control-flow and persistence boundaries

The resolver's source-order and scope-order loops, `IsUsable` validation, diagnostics, selected ID, and provider-ID decision values remain authoritative. Successful resolution still returns immediately; failed resolution still continues through existing fallback behavior. Metadata fields are display-only and must not affect download lookup, episode mapping, write-back, or rematch.

### Verify metadata provenance and no-search behavior

Deterministic tests use provider fixtures or seam-level resolved media objects to prove field mapping, episode-count fallback, honest unknowns, and unchanged selected identifiers. A call-count/throwing search seam proves successful provider-ID preview never invokes search or scoring. Live read-only tests cover at least the known Bilibili Season case and one provider with richer metadata when available.

## Risks / Trade-offs

- [Provider APIs expose inconsistent metadata] → Keep fields optional and test each adapter only for data its detail response actually establishes.
- [Declared count differs from filtered usable episodes] → Preserve declared `EpisodeCount` separately and use actual episode count only as fallback.
- [An extra ID-specific detail request increases latency] → Reuse existing responses first and permit only requests keyed by the exact ID; no cross-provider fan-out occurs after success.
- [Provider DTO changes silently empty fields] → Keep downloads dependent on existing ID/episode usability, while regressions detect loss of known metadata without turning display enrichment into a download failure.
- [Version rollback is needed] → Package paired `2.0.2r2` DLL/frontend assets, back up `2.0.2r1` DLL and both configurations, and retain recorded hashes and restart procedure.

## Migration Plan

1. Add optional resolved-media fields and provider adapter mappings without changing serialized request contracts.
2. Update resolver candidate projection and deterministic regressions.
3. Bump plugin/frontend markers to `2.0.2r2`, build and package paired artifacts.
4. Back up deployed `2.0.2r1`, deploy, restart Emby, and perform read-only preview verification before any download test.
5. Roll back the DLL and CustomCssJS/Danmu configurations from the recorded backup if plugin load, exact selection, or preview behavior regresses.
