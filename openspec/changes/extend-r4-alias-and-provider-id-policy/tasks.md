## 1. Deterministic Regression Coverage

- [x] 1.1 Add shared alias-search regressions for standalone Season, Series child Season, Episode-through-Season context, and Movie, including provider-local aliases, discovery provenance, original-metadata scoring, custom-keyword isolation, query bounds, de-duplication, and provider failure isolation.
- [x] 1.2 Add exact-ID regressions proving configured enabled-provider order is evaluated before scope order for Series child Seasons, standalone Season, Episode-to-Season fallback, and Movie; cover invalid earlier IDs and ignored Series IDs.
- [x] 1.3 Add pure metadata-policy regressions for Season/Episode ordinary-ID uniqueness across every registered provider, including disabled providers, first and later Episode success, and first successful Season commit.
- [x] 1.4 Add cleanup regressions proving automatic and manual-match success remove only other-site ordinary provider IDs, retain the selected ordinary ID, and do not mistake `SiteIDManual` suffix keys for ordinary external identifiers.
- [x] 1.5 Add safety regressions proving TMDB, TVDB, IMDb, custom keys, Series/Movie metadata, failures, skips, cancellations, timeouts, stale generations, non-persisted outcomes, and metadata exceptions are not destructively changed.
- [x] 1.6 Add exact alias-weight regressions for Season/Episode-context `35/20/45`, Movie `70/30`, title relevance floor `0.72`, the `0.902` exact-year/count boundary, unchanged `0.90` confidence, missing/mismatched evidence, and standard-round candidates retaining normal weights.

## 2. Shared Alias Discovery

- [x] 2.1 Extract the bounded provider-local clause/alias orchestration into reusable backend logic and retain standard-versus-alias-first discovery provenance through `(ProviderId, CandidateId)` de-duplication.
- [x] 2.2 Implement alias-only Season/Episode-context scoring at title 35%, year 20%, episode count 45%, with title relevance at least 0.72 and confidence still at 0.90; keep all standard-round weights and category safeguards unchanged.
- [x] 2.3 Route Series child Season and standalone Season search through the shared logic and verify both use identical original metadata and candidate decisions.
- [x] 2.4 Route an unresolved Episode through the same containing-Season search and preserve exact Episode mapping/download behavior after selection.
- [x] 2.5 Extend Movie matching with bounded local clauses and same-provider derived aliases, alias-only title 70%/year 30% scoring, and the 0.72 title floor while retaining each provider's Movie-specific search/filter path.
- [x] 2.6 Confirm manual previews and automatic library-import processing invoke the same backend search/scoring implementations and that frontend code remains display/interaction only.

## 3. Provider Priority and Identifier Uniqueness

- [x] 3.1 Audit and enforce provider-outer/scope-inner exact resolution for every entry point using the configured enabled-provider order, with invalid IDs falling through to later providers.
- [x] 3.2 Implement an exact registered-plugin-key replacement policy based on all registered scrapers, avoiding suffix or name-pattern deletion.
- [x] 3.3 Apply success-gated ordinary-ID uniqueness to Season writes after the first accepted persisted Episode and to every accepted persisted Episode write, including automatic import and retry.
- [x] 3.4 Keep `SiteIDManual` handling on its existing internal binding lifecycle and ensure ordinary external-ID replacement neither depends on nor accidentally mutates those suffix keys.
- [x] 3.5 Preserve sanitized item-local Season dictionaries, generation/arbiter checks, metadata-failure isolation, and no-write outcomes before any cleanup mutation.

## 4. Release and Local Verification

- [x] 4.1 Bump configuration, assembly informational/file versions, frontend compatibility marker, and documentation to `2.0.2r4` without changing AssemblyVersion compatibility.
- [x] 4.2 Run backend regression tests, frontend syntax/regression tests, Release build, strict OpenSpec validation, release-scope checks, and whitespace/diff checks.
- [x] 4.3 Package paired r4 DLL/frontend artifacts with hashes, verification notes, and a safe Emby full-restart/rollback helper.

## 5. Synology Deployment and Live Acceptance

- [x] 5.1 Back up the deployed r3 DLL and Danmu/CustomCssJS configurations, deploy the paired r4 artifacts, fully restart Emby, and verify the loaded DLL/frontend hashes.
- [x] 5.2 Verify read-only previews for a standalone alternate-title Season, an Episode without usable IDs, and a Movie; confirm aliases remain provider-local, exact year/count can raise a 0.72-related Season alias to 0.902, and ordinary complete-title/manual search behavior is unchanged.
- [x] 5.3 Create or identify controlled Season/Episode fixtures with multiple valid plugin IDs and verify the earliest enabled provider wins exact matching without scored search, including invalid-earlier-ID fallback.
- [x] 5.4 Complete a controlled manual Season download and verify first accepted success writes the Season ID, every successful Episode writes its exact Episode ID, other-site ordinary plugin IDs including disabled sites are removed, and all manual plus TMDB/TVDB/IMDb/custom keys remain.
- [x] 5.5 Verify automatic processing removes other ordinary plugin IDs but preserves all manual bindings, and confirm failed/skipped/cancelled/stale tasks perform no success-triggered cleanup.
- [x] 5.6 Re-check previously validated XML content/hashes for unrelated seasons and confirm no Series, Movie, non-plugin metadata, configuration, or unrelated media files changed.

## 6. r4 Acceptance Fixes

- [x] 6.1 Add a resolver regression proving an item-local Season identifier remains eligible when its value equals the ignored Series identifier, and that configured provider order still wins.
- [x] 6.2 Remove value-equality inference from Season identifier sanitation while retaining item-only Season scope and complete Series-scope exclusion.
- [x] 6.3 Add frontend regressions and request wiring so an untouched default-title rematch omits `keyword` and runs automatic alias discovery, while an edited input remains an isolated custom-keyword search.
- [x] 6.4 Rebuild/package/redeploy the corrected r4 pair and verify Dandan-first exact matching plus alias discovery on the affected fourth Season.

## 7. Android Back Navigation

- [x] 7.1 Add deterministic frontend regressions for history/backbutton navigation: secondary view returns to its parent, top-level closes, protected state remains open, and listeners/history guards are cleaned up.
- [x] 7.2 Implement one dialog-scoped Android/WebView history guard and back-handler contract without changing backdrop, Escape, close-button, or download protection semantics.
- [x] 7.3 Wire the full-Series overview and Season candidate view as parent/child navigation, while keeping standalone Season, Episode, Movie, and progress views top-level.
- [x] 7.4 Repackage and redeploy the r4 frontend, verify the installed script contains exactly one current marker and the Android back contract, then rerun full validation.
- [x] 7.5 Add Android narrow-screen safe-area spacing so the header and close button remain below the status bar, with a frontend source regression.

## 8. Search-Time Android Back Lock

- [x] 8.1 Add frontend regressions proving history/native back is consumed during a busy match request while X remains effective, and that result rendering restores normal back navigation.
- [x] 8.2 Add a dialog-scoped Android-back lock, activate it in busy search state, and clear it in every candidate, overview, and progress renderer without changing `closable`.
- [x] 8.3 Repackage/redeploy the r4 frontend and rerun full validation.
