## Why

Provider-ID-first matching currently verifies that an external identifier is downloadable but renders a synthetic local title and unknown year, episode count, and category. Users need the resolved candidate to describe the exact upstream object identified by the saved ID without invoking search or scoring.

## What Changes

- When an enabled provider identifier is present on a Movie, Series, Season, or Episode scope, request that provider's identifier-specific detail endpoint and return its upstream title and available metadata in the selected candidate.
- Keep provider-ID lookup as the highest-priority exact-match path; do not run keyword search, global scoring, or candidate competition after the identifier resolves successfully.
- Populate episode count from the resolved upstream episode collection when available, and preserve explicit unknown values only when the provider genuinely cannot supply a field.
- Preserve existing provider priority, item-scope priority, download mapping, metadata persistence, rematch behavior, and failure fallback to saved binding/search.
- Release the change as `2.0.2r2` with paired backend/frontend artifacts and live read-only preview verification.
- Non-goal: infer or fabricate metadata from the local Emby item when presenting the upstream candidate.

## Capabilities

### New Capabilities

- `provider-id-metadata-resolution`: Exact provider-ID resolution and upstream candidate metadata for Movie, Series, Season, and Episode previews.

### Modified Capabilities

- `season-danmu-matching`: Provider-ID-first Season matching must expose upstream detail metadata while preserving direct exact-match precedence.

## Impact

- Backend provider detail models and provider-specific `GetMedia` adapters.
- Provider-ID resolver candidate construction and deterministic regressions.
- Smart-match preview presentation consumes the enriched existing candidate fields without adding frontend search or scoring.
- Plugin/frontend version markers, package artifacts, Synology deployment, and rollback records.
