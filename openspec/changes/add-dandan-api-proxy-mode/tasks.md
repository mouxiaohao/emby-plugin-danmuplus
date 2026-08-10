## 1. Configuration Model and Page

- [x] 1.1 Add backward-compatible `UseProxyApi` and `ProxyCorsUrl` values to `DandanOption` without changing existing Dandanplay option defaults.
- [x] 1.2 Add mutually exclusive proxy/custom API controls and the proxy CORS-prefix input to the embedded configuration page, with mode-specific descriptions and conditional visibility.
- [x] 1.3 Update configuration load/save logic to round-trip all proxy, credential, related-comment, and Chinese-conversion values without clearing inactive-mode fields.

## 2. Dandanplay Request Routing

- [x] 2.1 Centralize official Dandanplay URL routing, normalize and validate the configured proxy CORS prefix, and produce sanitized configuration failures.
- [x] 2.2 Route the existing search, bangumi, and comment endpoints through the selected transport while preserving their paths, query parameters, timeouts, caching, and response handling.
- [x] 2.3 Keep local credential resolution and official signature headers only in custom API mode; verify proxy mode neither requires nor emits local authentication material.
- [x] 2.4 Confirm the Dandan scraper still uses `/search/anime`, `/bangumi`, and `/comment` through the existing title/year/season/episode scoring and XML/ASS pipeline, with no `/match` or video hashing.

## 3. Deterministic Verification

- [x] 3.1 Extend C# regressions for legacy defaults, proxy prefix normalization/validation, exact routed URLs and queries, custom signing eligibility, proxy credential independence, and provider failure isolation.
- [x] 3.2 Extend embedded-page/frontend assertions for the radio choices, conditional sections, proxy CORS field, and lossless load/save behavior.
- [x] 3.3 Run the Release build, C# regression executable, frontend regression suite, and strict OpenSpec validation; resolve only failures attributable to this change.

## 4. Live Proxy and Emby Regression

- [x] 4.1 Perform a minimal read-only smoke request through an administrator-supplied Worker URL and confirm the existing official search endpoint and JSON response work without local credentials; do not record the concrete URL as a plugin default or public test fixture.
- [x] 4.2 Back up the deployed Synology DLL and Emby plugin configuration before replacement, then deploy the verified Release artifact and restart Emby.
- [ ] 4.3 Verify the existing saved configuration loads in custom mode and a direct signed Dandanplay search still works.
- [x] 4.4 Select proxy mode in the Emby settings page, save the supplied CORS prefix, restart, and exercise manual plus automatic title-based preview/download on representative series, season, episode, and movie items.
- [x] 4.5 Verify other enabled providers, existing manual bindings, STRM handling, retry/partial XML behavior, and descending-score decisions remain unchanged, and inspect logs for credential/signature leakage.

## 5. Packaging and Rollback Readiness

- [x] 5.1 Produce the release DLL/source artifacts, record hashes and verification results, and document the new mode and proxy-prefix configuration without embedding any test credentials.
- [x] 5.2 Confirm the timestamped server backup and configuration backup can restore the previous deployment, and record the exact rollback paths.
