## 1. Shared Matching Engine

- [x] 1.1 Reorder generated search keywords so the parent series title is the first round and season-specific terms are fallbacks.
- [x] 1.2 Implement a reusable cross-provider search engine that de-duplicates candidates, isolates provider errors, and evaluates confidence only after a complete provider round.
- [x] 1.3 Apply deterministic global score ordering without provider configuration priority as a sort or selection key.

## 2. Entry-Point Integration and Persistence

- [x] 2.1 Replace manual match-preview provider early-exit logic with the shared global search engine.
- [x] 2.2 Replace new-season library-import legacy per-provider matching with the shared global search engine and the same confidence gate.
- [x] 2.3 Preserve saved manual bindings and persist only the selected automatic provider ID while removing stale automatic danmu-provider IDs.

## 3. Verification

- [x] 3.1 Build the plugin in Release mode with zero compilation errors.
- [x] 3.2 Verify that 唐朝诡事录 season 3 selects 爱奇艺《唐朝诡事录之长安》 and season 4 selects 爱奇艺《唐诡奇谭》.
- [x] 3.3 Assert that preview candidate scores are monotonically non-increasing and that manual and automatic entry points reference the same matcher.
- [x] 3.4 Verify provider search failures do not suppress candidates from successful providers and are returned as diagnostics.

## 4. Packaging and Deployment

- [x] 4.1 Package the r10 DLL, source archive, and release notes with reproducible SHA-256 values.
- [x] 4.2 Back up the deployed DLL, install r10, restart Emby, verify plugin loading, and confirm the deployed DLL hash matches the packaged artifact.
