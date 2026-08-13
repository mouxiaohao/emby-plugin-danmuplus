## 1. Freeze the r7 release boundary

- [x] 1.1 Create an isolated r8 workspace and branch from the verified r7 source and record r7 product, deployed DLL/CustomCssJS/configuration, package, V23 marker, and protocol 21 hashes.
- [x] 1.2 Add an r8 narrow-delta gate that permits only Mango search API/DTO/type-filtering code, focused tests, version metadata, OpenSpec, and packaging while requiring every other r7 product file to remain hash-identical.
- [x] 1.3 Add method-region guards proving Mango media detail, `GetVideoAsync`, danmu download, Controller, Frontend, Season scope/planning, evidence/fingerprint, persistence, and download execution remain r7-identical.

## 2. Implement the current Mango discovery contract

- [x] 2.1 Replace the forbidden legacy request with one encoded `/pc/suggest/v1` GET containing only `q` and `src=mgtv`, retaining cancellation, safe headers, the 500 ms provider limiter, and zero cookie/authorization/device/signature values.
- [x] 2.2 Model and validate HTTP success, business code 200, and the required `data.suggest` shape; treat a legal empty array as success and classify transport, business-code, malformed JSON, and incompatible-schema responses as uncached provider failures.
- [x] 2.3 Normalize canonical positive collection IDs from bounded `cid`, `id`, and allowlisted same-domain URL fallbacks; reject zero, non-numeric, overlong, off-domain, duplicate, and non-media suggestions while preserving first endpoint order.
- [x] 2.4 Produce bounded plain titles using `title`, `showTitle`, then sanitized `hit`; accept only reasonable four-digit years and keep category/episode count unknown when the endpoint does not supply trustworthy values.
- [x] 2.5 Update Movie/TV filtering so only an explicitly opposite provider type is excluded and unknown type remains eligible across `Search`, `SearchMediaId`, and `SearchForApi`, without changing shared scoring.

## 3. Bound retries, caching, and diagnostics

- [x] 3.1 Version the normalized-keyword cache, retain five-minute positive caching, add a 60-second successful-empty cache, and prove failures are not cached.
- [x] 3.2 Add at most one cancellation-aware retry for HTTP 429/502/503/504 with a bounded `Retry-After` delay no greater than two seconds; do not retry 401/403 or schema failures.
- [x] 3.3 Emit provider-local bounded diagnostics containing only the endpoint label and public status/category, never response bodies, query/device identifiers, cookies, authorization, or tokens.
- [x] 3.4 Prove a cold search performs exactly one discovery operation and zero `showlist`, `GetVideoAsync`, `GetMedia`, or candidate-detail operations before explicit candidate inspection/selection.

## 4. Add deterministic and integration regressions

- [x] 4.1 Add fixtures for `cid`, `id`, `/b/`, `/h/`, nested same-domain URL fallback, zero/invalid/overlong/off-domain IDs, duplicates, response ordering, HTML titles, missing fields, unknown types, and valid/invalid years.
- [x] 4.2 Add transport fixtures for successful empty results, business-code failures, 401/403, retryable 429/5xx with bounded retry, malformed JSON, incompatible schema, cancellation, positive/negative cache hits, and failure non-caching.
- [x] 4.3 Assert exact URL encoding and safe headers, one `/pc/suggest/v1` product literal, zero legacy/guessed search endpoints, and no cookies, authorization, random/persistent device parameters, or leaked response payloads.
- [x] 4.4 Verify Movie/TV unknown-type preservation, explicit-opposite filtering, canonical IDs accepted by the existing detail path, and identical normalized results for `Search`, `SearchMediaId`, and `SearchForApi` consumers.
- [x] 4.5 Re-run r7 frontend, backend, S0/null/foreign Season scope, identifier metamorphic, temporary-season rollback, lazy detail, title transport, force-refresh, download, and persistence regressions.

## 5. Version and package the narrow release

- [x] 5.1 Raise file/informational/config version to `2.0.3.8` / `2.0.3r8` while retaining assembly `2.0.3.0`, frontend V23, and mapping protocol 21.
- [x] 5.2 Run Mango fixtures, all deterministic suites, Release build, r8 narrow-delta gate, strict OpenSpec validation, and whitespace checks.
- [x] 5.3 Package the r8 DLL with the hash-identical r7 frontend asset, record hashes, and prepare an atomic DLL deployment that verifies and preserves the active r7 CustomCssJS and Danmu configuration.

## 6. Deploy and verify on Synology

- [x] 6.1 Back up the active r7 DLL, CustomCssJS XML, and Danmu configuration as one read-only paired set; deploy the r8 DLL atomically with rollback, restart Emby, and verify health, ownership, hashes, V23 count, protocol, and startup logs.
- [x] 6.2 Perform live read-only cold/warm searches for a known Mango program, Chinese punctuation/space variants, a Movie, and an unrelated empty keyword; confirm successful searches no longer report `MgtvID` failure.
- [x] 6.3 Verify each cold keyword produces only the bounded suggestion request, cache hits avoid another request, no candidate detail/showlist occurs before explicit inspection, and selection-time detail still resolves a canonical collection ID.
- [x] 6.4 Confirm r7 Whole-Series/direct-Season/S00/temporary-season UI and download state are unchanged, no acceptance download or metadata write occurs, and the active Danmu/CustomCssJS hashes remain unchanged.
- [x] 6.5 Document the paired r7 rollback directory, verified restore procedure, endpoint probe evidence, residual suggestion-coverage limitation, and final package/deployment hashes.
