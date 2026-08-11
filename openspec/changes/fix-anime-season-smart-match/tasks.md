## 1. Deterministic Regression Coverage

- [x] 1.1 Add provider-ID scope matrix regressions for Movie, Season, Episode-to-Season fallback, Series preview, automatic Season import, ordinary IDs, and manual-binding IDs; assert that no Movie/Season/Episode path reads, writes, copies, deletes, or migrates Series plugin keys.
- [x] 1.2 Add title-clause and provider-derived alias regressions for explicit punctuation, terminal season suffix removal, normalization, minimum length, bounded count, custom-keyword isolation, provider-local 0.90 fallback, `(ProviderId, CandidateId)` de-duplication, full-title scoring, and configured-site priority.
- [x] 1.3 Add Dandanplay season-detail fixtures proving main-content filtering precedes ordinal normalization, AnimeIds 15293/15634 map to local `1..12`/`1..10`, filtered count is used, and real EpisodeIds remain unchanged.
- [x] 1.4 Add Season persistence regressions for manual-only binding before download, first accepted success/partial ordinary-ID write, all-failed/skipped/cancelled/timed-out no-write, metadata-failure isolation, at-most-once commit, and per-Episode real-ID writes.
- [x] 1.5 Add latest-started generation regressions covering old-task late success, newer all-failed task, concurrent episode successes, first-success retry, and superseded retry rejection.
- [x] 1.6 Add Bilibili PGC fixtures proving `Season -> season_id`, `Movie/Episode -> ep_id`, Episode-before-Season priority, exact endpoint validation, transient `ep_id -> aid,cid` conversion, and durable metadata never receiving BVID/CID/`aid,cid` values.
- [x] 1.7 Add external-ID registration regressions proving Bilibili and Mgtv fields appear for Movie, Series, Season, and Episode while Series values remain display/edit-only and are absent from resolver and persistence inputs.

## 2. Identifier Scope and Search

- [x] 2.1 Replace ancestor-derived exact-ID scopes with the explicit entry-point matrix: Movie only; Season only; Series preview per Season only; Episode then containing Season; automatic import Season only.
- [x] 2.2 Remove all Series ordinary/manual binding participation from resolver, preview, retry, and automatic library paths while preserving historical Series metadata untouched.
- [x] 2.3 Implement bounded local-title clauses plus at most two same-provider aliases derived from strongly related returned titles, without a title-specific dictionary, and retain exact custom-keyword behavior.
- [x] 2.4 Extend shared Season search to run bounded clause/alias fallback per provider only while that provider lacks a candidate at or above 0.90, then merge and score all candidates with the original metadata.
- [x] 2.5 Restrict Bilibili matching to PGC and implement type-specific exact resolution: validate Season identifiers as `season_id`, validate Movie/Episode identifiers as `ep_id`, map an Episode from its containing Season only when its own `ep_id` is absent or invalid, and resolve `aid,cid` only inside the download path.
- [x] 2.6 Add or correct Bilibili and Mgtv `IExternalId` registrations for Movie, Series, Season, and Episode; keep Bilibili URL formatting neutral for polymorphic values and do not connect Series fields to matching or automatic writes.

## 3. Dandanplay Season Mapping

- [x] 3.1 Normalize filtered Dandanplay Season collections to ordinal `1..N` mappings and set the usable episode count to N without changing AnimeId, EpisodeId, CommentId, Movie, or direct-Episode behavior.
- [x] 3.2 Verify manual preview, tracked bulk download, retry, and automatic import consume the same normalized Dandanplay collection.

## 4. Season Binding and Success Persistence

- [x] 4.1 Save a manually selected, exact-detail-validated Season candidate only to `SiteIDManual` before download and reject candidates without a usable Season mapping.
- [x] 4.2 Register the latest-started Season/provider generation before asynchronous validation/download and carry it through tracked task snapshots and retries.
- [x] 4.3 Process the sorted first main Episode through the same tracked path; after the first current-generation accepted `success`/`partial` with `FilePersisted=true`, upsert the verified provider collection ID to the Season exactly once, and write the real provider EpisodeId to that successful Episode, including the first Episode itself and every later successful Episode. If the first Episode fails, the first later accepted persisted Episode may perform the Season write.
- [x] 4.4 Keep metadata exceptions diagnostic-only, preserve other provider/manual keys, remove duplicate update dispatch, and ensure no failed/stale task can write ordinary Season metadata.
- [x] 4.5 Apply the same mapping and persistence policy to automatic library-import Season downloads without reintroducing Series fallback.

## 5. Local Verification and Release

- [x] 5.1 Run backend regressions, frontend checks, Release build, strict OpenSpec validation, release-scope review, and diff checks.
- [x] 5.2 Bump and package the next patch release as `2.0.2r3` with paired DLL/frontend artifacts, SHA-256 hashes, README notes, and reproducible verification records.
- [x] 5.3 Record hashes for the currently correct Season 1 and Season 4 Dandan XML files so later acceptance can prove they were not cross-written or unexpectedly modified.
- [x] 5.4 Verify Bilibili PGC Movie exact matching persists `ep_id`, Season matching persists `season_id`, Episode matching persists `ep_id`, and generated danmu is fetched through backend-resolved `aid,cid` without persisting the tuple.

## 6. Synology Deployment and Live Acceptance

- [x] 6.1 Back up the deployed `2.0.2r2` DLL and Danmu/CustomCssJS configurations, deploy the paired candidate, fully restart Emby, and retain rollback instructions and hashes.
- [x] 6.2 Verify read-only Series/Season previews ignore Series `DandanID=18302`, discover Dandanplay through bounded derived title clauses/aliases, and independently identify Seasons 1–4 as AnimeIds 14727, 15293, 15634, and 18302.
- [x] 6.3 Force-refresh Seasons 2 and 3 through the tracked manual path, confirm all local episodes map to real Dandan EpisodeIds, validate non-empty XML chatids/content, and confirm ordinary Season IDs become 15293 and 15634 only after accepted persistence.
- [x] 6.4 Re-check Season 1 and Season 4 XML hashes, chatid sequences `147270001..147270014` and `183020001..183020016`, positive comment counts, Episode identifiers, and Season ordinary identifiers without deleting or redownloading correct files unnecessarily.
- [x] 6.5 Confirm no Series provider key was modified, explicit rematch still uses shared search/scoring, automatic library behavior uses the same rules, and no unrelated metadata or XML file changed.
- [x] 6.6 Confirm the Emby metadata editor displays Bilibili and Mgtv fields on Movie, Series, Season, and Episode items, and verify editing a Series field does not affect any smart-match or download decision.
