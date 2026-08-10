## Why

Season matching previously stopped as soon as an earlier configured provider produced a locally acceptable result. A high-priority provider could therefore bind an unrelated title before a better result from another provider was searched, and automatic library-import downloads did not use the same optimized matching behavior as the manual UI.

## What Changes

- Search the parent series title across every enabled provider before considering season-specific fallback queries.
- Merge and de-duplicate provider results, then select and display candidates by one global composite score independent of provider priority.
- Run additional parent-plus-season, season-title, and season-keyword queries only when a completed global round cannot be selected with high confidence.
- Reuse the same search engine, score thresholds, and ordering in manual match previews and automatic new-season processing.
- Preserve explicit manual bindings as the highest-priority selection.
- Persist only the globally selected automatic provider ID so stale provider metadata cannot override it during download.
- Non-goals: changing individual provider download protocols, CustomCssJS interaction design, retry behavior, duplicate-download policy, or existing XML/ASS output formats.

## Capabilities

### New Capabilities

- `season-danmu-matching`: Cross-provider season discovery, deterministic global scoring, manual-binding precedence, and automatic library-import selection behavior.

### Modified Capabilities

None. This repository did not previously contain an OpenSpec behavioral baseline.

## Impact

- Affected code: `Scraper/DanmuMatchScorer.cs`, the new shared search engine, `Core/Controllers/DanmuController.cs`, and `LibraryManagerEventsHelper.cs`.
- Affected API: existing `MatchPreview` responses retain their schema but return a broader globally ranked candidate set.
- Affected runtime: manual season/series matching and automatic processing of newly added seasons.
- Compatibility: no API removal, configuration migration, CustomCssJS update, or provider protocol change is required.
