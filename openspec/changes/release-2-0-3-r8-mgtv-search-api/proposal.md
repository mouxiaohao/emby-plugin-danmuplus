## Why

The enabled Mango TV provider currently sends every title search to the retired `mobileso` `/msite/search/v2` endpoint, which now returns HTTP 403 for unrelated keywords while Mango TV's current PC search endpoints remain available. As a result, `MgtvID` is always reported as a failed provider during smart matching and can no longer contribute valid candidates.

## What Changes

- Replace the forbidden Mango TV search request with a current, verified Mango TV PC search flow that returns canonical media identifiers accepted by the existing media-detail path.
- Supply only the current endpoint's required bounded public keyword/source parameters and map its response into the existing `ScraperSearchInfo` contract without changing global score, ordering, confidence, or provider-priority rules.
- Keep search failure provider-local: malformed, forbidden, rate-limited, or structurally unusable Mango TV responses remain diagnostics while other enabled providers continue.
- Add deterministic response fixtures and live read-only probes that distinguish endpoint compatibility from keyword-specific empty results.
- Release the narrow fix as 2.0.3r8 while keeping the r7 frontend asset, V23 cache marker, and mapping protocol 21 byte-for-byte unchanged.
- Preserve r7 Season scope, S00 handling, temporary-season UI, lazy candidate details, mapping protocol 21, evidence, confirmation, download, persistence, and retry behavior as explicit non-goals.

## Capabilities

### New Capabilities

- `mgtv-search-integration`: Defines the supported Mango TV discovery contract, canonical candidate mapping, provider-local failure handling, and endpoint compatibility diagnostics.

### Modified Capabilities

- None.

## Impact

- Primary product code: `Scraper/Mgtv/MgtvApi.cs`, Mango TV response DTOs, and narrowly related provider tests.
- Existing consumers (`Search`, `SearchMediaId`, and `SearchForApi`) continue to use the same provider abstraction and candidate contract.
- No new public Emby route, browser request field, selection field, mapping field, saved binding, or dependency is introduced.
- Deployment replaces the plugin DLL atomically, preserves the active frontend/configuration assets, and retains a verified paired r7 rollback set.
