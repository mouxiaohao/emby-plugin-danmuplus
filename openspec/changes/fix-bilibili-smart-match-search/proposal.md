## Why

The shared smart-match workflow calls each enabled provider through `SearchForApi`, but the Bilibili scraper inherits the base no-op implementation. As a result, valid Bilibili seasons such as “葬送的芙莉莲” and “半泽直树” never appear in manual or automatic matching even though the upstream API returns them.

## What Changes

- Add Bilibili support to the provider-neutral smart-match search entry point.
- Convert Bilibili `media_bangumi` and `media_ft` results into complete `ScraperSearchInfo` candidates containing a usable season identifier, clean title, category, year, and episode count.
- Ignore malformed Bilibili results that cannot be used by the existing `GetMedia` download path.
- Add regression coverage for anime and live-action series searches and verify the live Emby match-preview output.
- Preserve the existing Bilibili API session handling, download behavior, global scoring rules, provider order independence, and all non-Bilibili providers.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-danmu-matching`: Every enabled provider that exposes valid results, including Bilibili, participates in shared manual and automatic candidate discovery.

## Impact

- Affected code: `Scraper/Bilibili/Bilibili.cs` and matching regression tests.
- Affected APIs: existing match-preview and automatic library-import matching results; no endpoint shape changes.
- Dependencies: no new runtime dependency.
- Systems: Emby 4.9.3.0 on Synology and the existing CustomCssJS matching interface.
- Non-goals: changing composite scoring weights, Bilibili download segmentation, site priority behavior, or provider configuration semantics.
