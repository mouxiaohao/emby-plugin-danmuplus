## Why

The r3 provider-derived alias fallback is implemented in the shared Season search, but standalone Movie and exact Episode workflows do not consistently receive the same discovery behavior. In addition, successful Season/Episode downloads can leave several ordinary danmu-site identifiers on one item, which makes later exact-ID matching harder to reason about even though enabled-site order is intended to be authoritative.

## What Changes

- Apply the same bounded, provider-local alias discovery policy to Series-season, standalone Season, Episode, and Movie smart matching; preserve custom-keyword isolation, scoring thresholds, and ordinary no-identifier search behavior.
- Give candidates newly discovered through an automatic title-clause or provider-alias round an alias-specific evidence profile: Season/Episode-context candidates use title 35%, year 20%, and episode count 45%; Movie candidates use title 70% and year 30%. Require title relevance of at least 0.72 and retain the 0.90 confidence threshold, while complete-title search candidates keep their existing weights.
- When more than one valid local danmu-provider identifier is present, resolve exact identifiers strictly by the configured enabled-provider order for every Series-season, Season, Episode, and Movie entry point.
- After an accepted persisted Season/Episode download, keep the selected site's ordinary provider identifier and remove ordinary identifiers belonging to every other registered danmu provider from that same Season or Episode, including currently disabled sites.
- Preserve every `SiteIDManual` binding in all automatic and manual download paths; success-triggered cleanup applies only to ordinary provider identifiers.
- Always preserve Series identifiers and non-plugin Emby identifiers such as TMDB, TVDB, IMDb, and other metadata-provider keys.
- Keep failure, skip, cancellation, timeout, stale-generation, and metadata-write exception behavior unchanged: none of those outcomes may trigger identifier cleanup.
- Package the behavior as `2.0.2r4` without changing frontend scoring or manual-match semantics.
- Non-goals: clear identifiers on Movie or Series objects; migrate existing metadata without a successful download; modify or remove any `SiteIDManual` binding; query aliases from a different provider; loosen the 0.90 confidence rule.

## Capabilities

### New Capabilities

- `smart-match-alias-search`: Defines one bounded provider-local alias discovery policy for Series-season, Season, Episode, and Movie matching.
- `danmu-provider-id-policy`: Defines configured-site priority for competing exact identifiers and success-gated uniqueness of ordinary plugin IDs on Season and Episode items.

### Modified Capabilities

- `season-danmu-matching`: Requires standalone Season and Series-season matching to use the same alias discovery behavior and the confirmed alias-specific evidence weights while preserving normal-search scoring.

## Impact

- Shared match search engine, candidate discovery provenance/scoring, and controller entry points for Movie, Season, Series-season, and Episode previews.
- Exact provider-ID resolver ordering and deterministic regressions for multiple simultaneously valid identifiers.
- Success-gated Season/Episode metadata persistence in interactive and automatic-import download paths.
- Plugin version metadata, frontend compatibility marker, release artifact, README, and Synology acceptance checks.
