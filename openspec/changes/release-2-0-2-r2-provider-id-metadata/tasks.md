## 1. Resolved Media Contract

- [x] 1.1 Add optional upstream title, year, category, and declared episode-count fields to the shared resolved-media model without changing existing identifier or episode semantics.
- [x] 1.2 Update exact-candidate projection to use only resolved upstream metadata, fall back from missing declared count to usable resolved episodes, and never label a local Emby title as the website title.
- [x] 1.3 Preserve direct Episode identifier behavior while carrying its explicit upstream episode title and one-item resolved collection into the candidate.

## 2. Provider Detail Adapters

- [x] 2.1 Populate Dandan identifier-detail title, year, category, declared count, and episode metadata from the existing Anime response.
- [x] 2.2 Populate Bilibili PGC/BVID identifier-detail title, year, declared count, and episode metadata where explicitly available, without inventing category values.
- [x] 2.3 Populate iQiyi identifier-detail title, category, declared count, and episode metadata where explicitly available, leaving unavailable year unknown.
- [x] 2.4 Populate reliable MGTV, Tencent, and Youku identifier-detail metadata and actual episode counts from their current detail responses, leaving unsupported title/year/category fields unknown.
- [x] 2.5 Confirm any additional provider request is keyed only by the exact identifier and that successful provider-ID resolution never invokes keyword search or scoring.

## 3. Deterministic Verification

- [x] 3.1 Add resolved-media and resolver regressions for upstream field projection, declared-count precedence, episode-list fallback, unknown-field honesty, exact selected-ID preservation, and no local-title substitution.
- [x] 3.2 Add provider fixture or adapter regressions for each site's supported detail fields and for fields that must remain unknown.
- [x] 3.3 Add a no-search/no-scoring regression for successful Movie, Series, Season, and Episode provider-ID previews, plus unresolved-ID fallback coverage.
- [x] 3.4 Run frontend checks, backend regression executable, Release build, strict OpenSpec validation, release-scope review, and diff checks.

## 4. Release and Documentation

- [x] 4.1 Set the user version to `2.0.2r2`, file version to `2.0.2.2`, and frontend installation marker to V13 while preserving the `2.0.2.0` assembly line.
- [x] 4.2 Update README documentation to distinguish exact provider-detail metadata from searched/scored candidates and document honest unknown fields.
- [x] 4.3 Package paired `2.0.2r2` DLL/frontend artifacts with SHA-256 hashes and reproducible verification notes.

## 5. Live Deployment Verification

- [x] 5.1 Back up the deployed `2.0.2r1` DLL and Danmu/CustomCssJS configurations, deploy the paired `2.0.2r2` candidate, and fully restart Emby with rollback readiness.
- [x] 5.2 Live-test read-only provider-ID previews for representative Season and other available item types, confirming upstream title/metadata, exact-ID selection, and absence of search/scoring calls.
- [x] 5.3 Verify重新智能匹配 still enters the existing enabled-provider search/scoring workflow and that no download or metadata write is triggered during acceptance testing.
