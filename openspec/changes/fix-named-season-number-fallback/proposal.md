## Why

An Emby Season can have an arc-specific display name while danmu providers expose only a numbered Season. The missing named-to-numbered fallback prevents the first source from being selected and therefore also prevents existing composite-season remainder matching from starting.

## What Changes

- Preserve named-season matching as the first stage, including existing aliases and confidence rules.
- After a complete unsuccessful named stage, retry ordinary Season matching with the authoritative positive Emby ordinal and a standard Season label.
- Do not bypass strong named ambiguity, explicit keywords, cancellation, incomplete evidence, or logical-season continuation contexts.
- Preserve full canonical candidates, provider diagnostics, composite mappings, Part continuation, and logical-season advancement.
- No Emby metadata edits, provider-ID migration, or threshold changes. The authorized rollout retains the already-deployed confirmed-download/retry hotfix and its frontend; unrelated configuration is preserved.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-danmu-matching`: Add a staged standard-season fallback without changing remainder matching.

## Impact

Shared Season candidate discovery used by interactive previews and automatic imports; deterministic search/composite-continuity tests; matching documentation. No new dependencies or public request fields.
