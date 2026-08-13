## Why

Emby may expose episodes from Season 0 or another logical season inside a normal Season's display inventory. Treating those foreign episodes as supplemental temporary seasons makes the normal season plan noisy and can interrupt otherwise continuous main-episode mapping; r5 scopes every batch Season operation to the target season's own logical episodes.

## What Changes

- **BREAKING**: Whole-Series smart matching no longer includes Season 0 as a target season.
- **BREAKING**: Matching a normal Season includes only episodes whose `ParentIndexNumber` equals that Season's `IndexNumber`; S00, other-season, and unknown-parent episodes mixed into its inventory are ignored rather than emitted as temporary virtual seasons.
- Preserve standalone Season 0 matching: when the user explicitly opens the real Season 0 item, its own Episode inventory is filtered to `ParentIndexNumber == 0` and matched normally.
- Apply the same target-season filter to interactive Series/Season preview, candidate confirmation, temporary-run rebuild, download preflight/retry, and automatic Season processing so no secondary path can reintroduce foreign episodes.
- Preserve ItemId-based identity, explicit mappings, identifier-free batch matching, single-item exact matching, score display, and complete-single-source Season display-mirror behavior for the eligible target-season episodes.
- Non-goals: r5 does not move episodes between Emby seasons, rewrite `ParentIndexNumber`, infer missing parent season numbers, or delete any ProviderId.

## Capabilities

### New Capabilities
- `season-episode-scope-filtering`: Defines the target-season eligibility filter, Season 0 standalone behavior, cross-season exclusions, and consistency across preview, execution, retry, and automatic processing.

### Modified Capabilities
- `season-danmu-matching`: Changes whole-Series enumeration to skip Season 0 and requires all Season matching entry points to use the same target-season-only episode scope.

## Impact

- Affects the shared target-Season planning context/coordinator, whole-Series target enumeration, interactive and automatic plan construction, download/retry preflight, frontend counts and virtual-group rendering, and deterministic/live regression fixtures.
- Requires a new r5 protocol/cache generation so an open V20/r4 draft containing S00 or cross-season episodes cannot be submitted to r5.
- Existing stored metadata remains untouched. Complete single-source target-season downloads may continue to overwrite only the verified provider's Season display identifier after terminal success; ignored foreign episodes do not participate in completeness.
- Depends on the r4 identifier-free explicit-mapping framework and supersedes r4's behavior of exposing placed foreign-season episodes as temporary virtual seasons.
