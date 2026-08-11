## Context

See `proposal.md` for motivation. Live diagnosis on Emby 4.9.3.0 established four concrete facts:

- The Series for “爱书的下克上” stores `DandanID=18302`, which is the Dandanplay fourth-season AnimeId. Child Seasons 1–4 do not store their corresponding Dandanplay AnimeIds, so the current `Season -> Series` resolver fallback selects the fourth season for every Season.
- Neither the Emby item DTO nor its NFO contains “小书痴” as an alias. The complete localized title and its local clauses reveal only a related later-season result; that provider result exposes the reusable leading title “小书痴的下克上”, which returns all four seasons when queried on the same provider.
- Dandanplay AnimeIds 15293 and 15634 return the correct 12- and 10-episode standalone seasons, but their explicit episode numbers are cumulative across prior seasons. The shared matcher therefore rejects every local `1..N` episode instead of using the stable list order.
- Season 1 and Season 4 files are not cross-wired: all 14 Season 1 XML files carry chatids `147270001..147270014`, and all 16 Season 4 XML files carry `183020001..183020016`. Their `<datasize>` values are positive and their modification times match the tracked tasks. The defect is missing Season-level AnimeId persistence, not wrong XML content for those two completed seasons.

The implementation must preserve the r6/r2 rule that frontend code only presents server decisions; matching, mapping, persistence, and concurrency decisions stay in the backend.

## Goals / Non-Goals

**Goals:**

- Make identifier scope coincide with the Emby entity that actually owns the provider object.
- Discover alternate localized provider titles through conservative shared-title clauses without weakening scoring.
- Give standalone Dandanplay seasons a deterministic local `1..N` mapping while preserving real EpisodeIds.
- Persist a verified Season media ID only after accepted file success and prevent stale tasks from overwriting newer choices.

**Non-Goals:**

- No hard-coded title alias dictionary and no unbounded/cross-provider inference from candidate titles.
- No automatic cleanup or migration of historical Series identifiers, existing XML, or unrelated provider metadata.
- No change to scoring weights, the 90-point confidence threshold, or configured provider ordering.

## Decisions

### Use an explicit scope matrix and ignore Series plugin keys

The resolver will accept explicit scope lists per entry point rather than deriving an ancestor chain. Movie uses `Movie`; Season and each Season in a Series preview use `Season`; Episode uses `Episode -> Season` within each provider; automatic Season import uses `Season`. Both ordinary and `Manual` Series keys remain untouched and unread.

Alternative considered: keep Series fallback only when it can be proven to represent the same season. Rejected because provider Series IDs are not consistently distinguishable from season IDs and validation would reintroduce ambiguous cross-season inference.

### Use an explicit Bilibili PGC identifier contract

Bilibili matching for this plugin is PGC-only. The durable identifier matrix is `Season -> season_id`, `Movie -> ep_id`, and `Episode -> ep_id`. Series has no operational Bilibili identifier. An Episode without a usable `ep_id` may still be mapped from its containing Season's validated `season_id`, but the mapped Episode retains and persists its own `ep_id`.

Exact resolution uses the endpoint that corresponds to the owning item: Season validation calls the PGC season endpoint, while Movie and Episode validation call the PGC episode endpoint. Only after an exact Movie/Episode has been validated does the backend resolve `ep_id -> aid,cid` for the protobuf danmu request. The tuple is transient transport data and is never written to Emby metadata. BVID/UGC matching is outside this release's supported contract.

Alternative considered: persist the PGC movie `season_id` because search results are grouped by season. Rejected because a Movie is a directly playable item and its stable item-local identifier is the positive-content `ep_id`; persisting the collection ID would make exact Movie matching type-ambiguous.

### Register external-ID fields independently from resolver scope

Bilibili and Mgtv external-ID providers expose fields on Movie, Series, Season, and Episode editors so existing metadata is visible and editable. The Bilibili URL formatter does not attempt to build one public URL from the polymorphic values. Series exposure is display/edit only: resolver, preview, download, retry, and automatic-import code continue to ignore and never write Series plugin keys.

Alternative considered: hide the fields on Series. Rejected because historical plugin keys already exist and the user needs visibility, while matching safety is enforced by the backend scope matrix rather than by hiding metadata.

### Run title-clause fallback per provider

Standard keyword rounds remain first. If one provider has no candidate scoring at least 0.90 from standard rounds, search at most a small fixed number of clauses derived from explicit punctuation boundaries. Trim and normalize clauses, discard short/generic/equivalent fragments, and merge by `(ProviderId, CandidateId)`. If those rounds reveal a strongly related provider title but still no confident candidate, remove its terminal season designator and derive at most two provider-local alias clauses for one bounded second hop. Always score with the original full series/season title, year, and episode count.

This is provider-local rather than globally early-stopped: an earlier configured site must still get its clause fallback even if a later site already has a confident complete-title candidate, otherwise the existing confident-site priority rule would be bypassed.

Alternative considered: store a built-in “爱书/小书痴” synonym. Rejected because it fixes one title and cannot generalize. A bounded alias extracted from a strongly related result on the same provider generalizes without a static dictionary. Querying TMDB/TVDB aliases was also rejected for this change because the current public Emby item contract exposes no usable alias collection and adding a credentialed metadata dependency would materially expand scope.

### Normalize Dandanplay season collections after main-content filtering

For a Dandanplay Anime detail used as a Season collection, filter explicit non-main episodes first, preserve upstream stable order, set mapping `EpisodeNumber` to `index + 1`, retain the real `EpisodeId` in `Id/CommentId`, and expose the filtered count. Direct Episode identifiers and Movie handling are not changed.

Alternative considered: change the shared matcher to fall back by list position whenever exact numbers miss. Rejected because that would weaken providers whose numbering is genuinely reliable and could silently remap gaps.

### Split manual binding from ordinary success persistence

After the selected Season candidate resolves and contains at least one usable mapping, a manual selection may save only `SiteIDManual`. Ordinary `SiteID` is written from the verified collection ID after the first arbiter-accepted `success` or `partial` with `FilePersisted=true`. Each successful Episode independently receives its real provider EpisodeId. Failed, skipped, cancelled, timed-out, or stale results do not trigger ordinary Season persistence.

Alternative considered: write the Season ID before download. Rejected because it would label an all-failed candidate as successfully downloaded.

### Register a Season/provider generation before asynchronous work

Task creation registers the latest-started generation for the Season/provider before detail validation or download. Any Season commit or retry rechecks under the existing per-key serialization boundary that its generation is still current, the accepted result persisted a file, and the task was not cancelled or timed out. Once superseded, an older task can never write even if the newer task later fails.

The Season write occurs at most once per task; metadata exceptions become diagnostics and do not reverse file success. Provider keys and manual bindings from other sites are never cleared.

## Risks / Trade-offs

- [Title fragments or provider aliases can be broad] -> Require punctuation-derived, bounded, minimum-length clauses; permit provider aliases only from strongly related results on that same site; keep full-title scoring and the existing confidence gate.
- [More provider requests] -> Run clause fallback only for providers lacking a confident standard candidate and cache/de-duplicate existing provider searches.
- [Dandanplay list order changes] -> Preserve the exact filtered upstream order and cover real sequel fixtures plus stable EpisodeId assertions.
- [Historical Series IDs remain visible in Emby] -> Deliberately preserve them for rollback/user control while ignoring them in all matching paths.
- [Metadata repository write fails] -> Keep the XML result successful, report a separate diagnostic, and allow a safe later retry.

## Migration Plan

1. Add deterministic scope, clause, Dandan numbering, and generation/persistence regressions before production changes.
2. Implement the shared backend changes and verify manual preview and automatic import parity.
3. Build a paired release artifact and back up the deployed DLL plus Danmu/CustomCssJS configurations.
4. Deploy to Synology, restart Emby, and use read-only previews to prove four Seasons no longer inherit Series `18302`.
5. Re-run manual forced downloads for Seasons 2 and 3, verify XML chatids and Episode IDs, and confirm Season ordinary IDs become `15293` and `15634` only after accepted file success.
6. Roll back from the recorded backup if scope, mapping, or persistence validation fails; do not delete newly written XML automatically.
