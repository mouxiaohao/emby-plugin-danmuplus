## Why

Equal-scoring search candidates currently ignore the user's configured provider priority, while provider episode lists can include trailers, PVs, previews, and bonus clips that shift episode-to-danmu mapping. Bilibili season 46089 demonstrates the latter defect: 28 main episodes are interleaved with 18 previews and are exposed as 46 downloadable episodes.

## What Changes

- Keep composite match scoring provider-neutral, but order candidates with exactly equal final scores by the current configured provider priority.
- When the highest final score is tied across providers, automatically bind the tied candidate from the highest-priority configured provider; retain ambiguity if that provider itself has multiple top-scoring candidates.
- Introduce provider episode normalization that prefers structured trailer/section metadata, removes explicit non-main content, de-duplicates repeated episode numbers, and preserves canonical episode order.
- Add conservative cross-provider fallback rules for obvious preview/PV/trailer/bonus titles where structured metadata is unavailable.
- Reject or report an unusable episode list instead of silently falling back to an unfiltered list of extras.
- Apply the same normalized episode list to manual bulk download, automatic library-import download, retry, and saved-binding paths.
- Do not use XML byte size alone to decide whether an episode is valid, and do not automatically delete previously downloaded XML files.

## Capabilities

### New Capabilities
- `main-episode-selection`: Defines how provider episode lists exclude non-main videos and produce stable source-episode mappings.

### Modified Capabilities
- `season-danmu-matching`: Changes the deterministic ordering rule for candidates with equal final composite scores.

## Impact

Affected areas include the shared global matcher, provider episode DTO deserialization and episode-list construction (especially Bilibili), conservative filters for other supported providers, regression tests, and the manual/automatic download paths that consume `ScraperMedia.Episodes`. No API contract or saved binding format changes are required.
