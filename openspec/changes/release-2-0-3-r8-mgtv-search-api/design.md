## Context

See `proposal.md` for motivation. The r7 Mango provider creates a private `HttpClient`, sends hard-coded browser headers, serializes searches through a 500 ms limiter, and caches non-empty legacy results for five minutes. `Search`, `SearchMediaId`, and `SearchForApi` all delegate to the same `SearchAsync`; therefore one provider-local migration covers manual and automatic discovery.

Read-only probes from the target Synology establish the compatibility boundary:

- `/msite/search/v2` returns HTTP 403 with `{code:403,msg:"Forbidden"}` for both `妄想学生会` and `你好星期六`.
- `/pc/suggest/v1?q=你好星期六&src=mgtv` returns HTTP 200 without a cookie, authorization value, or device identifier.
- A positive returned collection ID is accepted by the existing `pcweb.api.mgtv.com/variety/showlist` media-detail path.
- The current PC contract exposes a bounded suggestion set, not a paginated exhaustive catalogue, and its optional year/category/count metadata is sparse.

## Goals / Non-Goals

**Goals:**

- Restore safe Mango candidate participation through the provider's current PC suggestion contract.
- Preserve canonical collection IDs and plain titles while treating sparse metadata conservatively.
- Keep cancellation, rate limiting, caching, provider-local diagnostics, and lazy candidate detail intact.
- Make endpoint drift detectable with deterministic fixtures and a narrow source gate.

**Non-Goals:**

- Do not scrape Mango HTML, emulate login, acquire cookies, generate device fingerprints, or bypass access controls.
- Do not claim exhaustive or paginated search coverage beyond the endpoint's returned suggestion set.
- Do not fetch `showlist` or call `GetVideoAsync` during discovery to enrich optional candidate metadata.
- Do not change the global scorer, confidence rules, provider priority, r7 UI, target-season scope, S00 policy, mapping protocol 21, evidence, persistence, or download behavior.

## Decisions

### Use the verified PC suggestion endpoint as the discovery boundary

`SearchAsync` will issue one encoded GET to `https://mobileso.bz.mgtv.com/pc/suggest/v1` with `q` and `src=mgtv`. It will retain the existing bounded public browser headers and global 500 ms serialization but send no cookie, authorization, `did`, UUID, token, or synthetic device value.

The legacy endpoint will be removed without fallback. Retrying a deterministic 401/403 or guessing unobserved `/pc/search` routes would add latency and diagnostic noise. HTML scraping was rejected because it couples discovery to presentation markup and redirects.

### Treat the response as suggestions, not a complete search catalogue

The response DTO will model the top-level business `code` and `data.suggest`. HTTP 200 plus business code 200 plus an empty suggestion array is a successful empty result. Missing `data`/`suggest`, malformed JSON, or a non-200 business code is an incompatible provider response and remains a provider-local failure.

Only the complete returned `suggest` array is consumed; no pagination or hidden total is invented.

### Normalize candidates conservatively

Canonical ID resolution uses a bounded positive numeric identifier in this order: `cid`, `id`, an allowlisted same-domain `/b/{cid}/{vid}` or `/h/{cid}` URL, then the first valid same-domain URL in a returned nested video list if the current response schema supplies one. Zero, non-numeric, overlong, and off-domain identifiers are rejected.

Display title preference is `title`, then `showTitle`, then HTML-stripped `hit`; text is decoded, trimmed, bounded, and required. Year accepts only a reasonable four-digit value. Unknown type remains unknown and episode count remains zero. The number of suggestions or nested preview videos is never treated as a Season episode count.

Results are de-duplicated by canonical collection ID while preserving the first endpoint position; a later duplicate may enrich only safe missing display metadata and cannot reorder candidates.

### Preserve candidates with unknown media type

The current response does not reliably distinguish Movie from Series. `Search` and `SearchMediaId` will exclude only a result explicitly classified as the opposite local item type. Unknown type remains eligible. This prevents the migrated endpoint's sparse metadata from dropping every valid result and does not change the shared global scorer.

### Keep discovery lazy and bounded

Search must perform zero `showlist`, `GetVideoAsync`, `GetMedia`, or per-candidate detail calls. Existing click/selection-time detail resolution continues unchanged.

Successful non-empty normalized results use a versioned five-minute cache key. Successful empty results use a 60-second negative cache so a temporary absence is retried sooner. Transport, business-code, schema, and parse failures are not cached.

No new single-flight layer is introduced. At most one retry is permitted only for HTTP 429/502/503/504, remains cancellation-aware, honors a bounded `Retry-After`, and waits no more than two seconds. HTTP 401/403 and incompatible schemas are not retried.

### Keep diagnostics provider-local and secret-safe

Errors are classified by endpoint name, HTTP/business code, or response-shape category. Logs do not include response bodies, query identifiers, cookies, authorization, or device data. Existing bounded-search aggregation continues to expose Mango as one failed provider while successful providers return candidates; no frontend change is required.

### Enforce a narrow r7-derived release

r8 implementation will start from the verified r7 package/source hashes. Product changes are limited to Mango search API/DTO/type filtering, tests, version metadata, OpenSpec, and packaging. Hash gates freeze Controller, Frontend, Season scope/planning, evidence/fingerprint, mapping protocol, download/persistence, and Mango detail/danmu method regions.

## Risks / Trade-offs

- **[Suggestion results are less exhaustive than the retired search payload]** → State this contract explicitly, consume the complete bounded response, and do not fabricate pagination.
- **[Sparse year/type/count can reduce Mango candidate score]** → Preserve unknown values rather than inventing evidence; global scoring remains authoritative and manual selection remains available.
- **[Endpoint schema can drift again]** → Validate HTTP and business success plus required shape, keep fixtures for alternate ID/title forms, and fail provider-locally on incompatibility.
- **[Retry can amplify upstream throttling]** → Limit it to one bounded attempt for transient statuses and retain the global request interval.
- **[Highlight markup can reach UI/logs]** → Prefer plain fields, sanitize the fallback, and test encoded/malicious strings.

## Migration Plan

1. Create an isolated r8 workspace from the verified r7 release and record product/package hashes.
2. Implement and test only the Mango discovery adapter and conservative local-type filtering.
3. Run deterministic Mango fixtures, r7 full regressions, Release build, strict OpenSpec, and the r8 narrow-delta gate.
4. Package paired r8 DLL/config assets, back up the active r7 trio, and deploy atomically with automatic rollback.
5. Live-test known titles, punctuation/space variants, a Movie, and an empty result. Confirm one suggestion request on a cold search, no `MgtvID` failure for successful empty results, and no detail request before explicit candidate inspection/selection.
6. Roll back the paired r7 trio if health, hash, endpoint, mapping, or no-eager-detail checks fail.
