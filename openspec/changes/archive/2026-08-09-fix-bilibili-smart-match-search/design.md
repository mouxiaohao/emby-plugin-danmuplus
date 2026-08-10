## Context

See `proposal.md` for motivation. The shared matching engine invokes `SearchForApi` for every configured scraper. Bilibili already has a working upstream search client and a `GetMedia` implementation that accepts PGC season identifiers, but it does not adapt upstream search results to the provider-neutral API model. The change must remain compatible with C# 8 and .NET Standard 2.0 and must not fork manual and automatic matching behavior.

## Goals / Non-Goals

**Goals:**

- Expose valid Bilibili PGC search entries through the same provider-neutral candidate contract as other sites.
- Preserve enough metadata for the existing global scorer and manual selection UI.
- Ensure the selected identifier can be consumed directly by Bilibili's existing media-detail and download path.
- Keep one implementation shared by manual preview and automatic library import.

**Non-Goals:**

- Changing Bilibili HTTP endpoints, cookie acquisition, danmu segmentation, or retry behavior.
- Adding UGC video search results that do not represent a season.
- Changing scoring thresholds, weights, tie-breakers, binding persistence, or duplicate-download policy.

## Decisions

### Add a Bilibili-specific `SearchForApi` adapter

The scraper will call its existing `SearchAsync(keyword)` client and map the returned PGC media records to `ScraperSearchInfo`. This keeps Bilibili response knowledge inside its provider implementation and lets `DanmuMatchSearchEngine` remain provider-neutral.

Alternative considered: make the matching engine call each scraper's item-based `Search` method. That would require synthesizing or mutating Emby items for arbitrary search keywords and would reintroduce provider-specific behavior into the shared engine.

### Prefer `season_id`, then `pgc_season_id`, then a positive `media_id`

The adapter will select the first positive identifier in that order because Bilibili's existing `GetMedia` numeric path resolves a PGC season. Entries with no positive supported identifier will be ignored.

Alternative considered: expose every returned ID. That can produce candidates the subsequent download path cannot resolve and would turn a successful manual selection into a deterministic failure.

### Reuse normalized entity metadata

The existing Bilibili media entity already removes search-highlight HTML from titles. The adapter will derive the year from `pub_date`, falling back to `pubtime`, and pass through `season_type_name` and `ep_size`. It will not apply the older item-based 0.7 title pre-filter because global scoring is the single authority for ranking and confidence.

Alternative considered: copy the old local similarity threshold. That would discard potentially useful candidates before parent-title, season-keyword, year, and episode-count evidence can be combined.

### Isolate malformed records without masking provider failures

Malformed entries are skipped individually. HTTP, session, and deserialization failures continue to propagate so the shared engine records Bilibili in `SearchErrors`, consistent with provider failure isolation.

## Risks / Trade-offs

- [Bilibili occasionally returns `media_id` that is not accepted as a season ID] → Prefer season identifiers and validate the fallback with the existing `GetMedia` path during live regression.
- [More candidates can make previously empty searches ambiguous] → This is intentional; candidates remain globally ranked and confidence-gated instead of being chosen by provider priority.
- [Bilibili response fields may change later] → Retain provider-level diagnostic logging for identifiers and mapped result counts.

## Migration Plan

1. Build the Release DLL and verify deterministic candidate ordering locally.
2. Back up the currently deployed plugin DLL.
3. Deploy the replacement DLL and restart Emby.
4. Run forced match previews for “葬送的芙莉莲” and both “半泽直树” seasons, verifying Bilibili metadata and descending global scores.
5. Roll back by restoring the pre-change DLL and restarting Emby if startup, preview, or download regression fails.
