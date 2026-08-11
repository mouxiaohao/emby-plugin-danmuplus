## 1. Movie Match Contracts and Ranking

- [x] 1.1 Add additive Movie preview and generic tracked-target fields to the smart-match response models while retaining all existing Series/Season fields.
- [x] 1.2 Implement movie-specific cross-provider search normalization, non-movie filtering, de-duplication, title/year scoring, deterministic ordering, diagnostics, and confidence gating.
- [x] 1.3 Add Movie preview routing with saved-manual-binding precedence and forced keyword search that remains read-only until confirmation.
- [x] 1.4 Add deterministic regression checks for descending Movie scores, ambiguous cross-site candidates below the high-confidence pool, provider failure isolation, non-movie filtering, manual binding, unchanged Season ranking, and layered site-priority selection (including a 0.98 preferred-site winner over 1.00, pool boundaries, cross-site 0.90 ties, same-site 0.90/0.89 selection, same-site top-score ambiguity, and intermediate-search behavior).

## 2. Movie Binding and Tracked Download

- [x] 2.1 Extract an outcome-returning single-Movie provider download operation from queued Movie processing and route existing automatic Movie events through it without changing duplicate, partial XML, STRM, or provider-ID behavior.
- [x] 2.2 Extend binding validation to accept Movie candidates, persist automatic versus manual provider identifiers correctly, and leave an existing binding intact when validation fails.
- [x] 2.3 Create serialized, cancellable single-Movie tracked tasks with generic target identity and queued/running/success/skipped/partial/failed/cancelled snapshots.
- [x] 2.4 Add regression checks for Bilibili and non-Bilibili identifier routing, invalid candidate preparation, duplicate skip, forced refresh, cancellation, and Series/Season task compatibility.

## 3. Frontend Menu Integration

- [x] 3.1 Recognize detail-page and card overflow clicks, derive card or page item-id candidates, correlate them with the currently open action sheet, and invalidate stale asynchronous contexts.
- [x] 3.2 Fetch the authoritative item and inject one correctly labelled action only for Series, Season, Episode, or Movie, including Series library cards, Season cards within Series details, and Episode card/detail menus.
- [x] 3.3 Add Movie-specific dialog text, preview/candidate search, automatic/manual confirmation, tracked progress, cancellation, and completion rendering without routing Movie data through Season fields.
- [x] 3.4 Add a shared manual-search default helper that uses the Movie title for Movie and the owning Series title for Series, Season, and Episode while preserving user edits in the active view.
- [x] 3.5 Bump the frontend installation flag and add deterministic DOM-level checks for repeated observer runs, two rapidly opened card menus, unresolved identity, unsupported item types, search defaults, and one workflow per click.

## 4. Episode Match and Single-Episode Download

- [x] 4.1 Extend preview contracts and routing for Episode identity, owning Series/Season context, local Episode number, and per-candidate suggested source Episode numbers.
- [x] 4.2 Resolve candidate media Episodes and deterministically suggest the best source Episode number without changing the containing Season's binding.
- [x] 4.3 Reuse the Season candidate-picker layout for Episode, show local and candidate source numbers, and render an editable source-number input only beside the selected candidate.
- [x] 4.4 Validate the submitted source Episode as a positive integer that exists in freshly resolved candidate media, then run a cancellable tracked download for only the target local Episode.
- [x] 4.5 Add regressions for automatic suggestion, manual override, invalid/missing source Episodes, specials or numbering gaps, sibling isolation, cancellation, and existing Season binding preservation.

## 5. Build, Documentation, and Live Verification

- [x] 5.1 Update README installation and usage guidance for Series/Season/Episode card menus, Episode source-number override, search defaults, and Movie detail/card smart matching.
- [x] 5.2 Run the regression executable and a Release build, and verify the packaged frontend asset and DLL are generated from the changed sources.
- [x] 5.3 In Emby 4.9.x, verify a Series card menu in television and animation libraries, a Season card menu within a Series detail page, both Episode menu locations, and ensure unsupported card menus receive no action.
- [ ] 5.4 Live-test Movie detail/card workflows and Episode detail/card workflows with representative Bilibili and non-Bilibili media, STRM media, ambiguous manual selection, edited source Episode number, saved binding, duplicate skip, force refresh, failure, and cancellation.
- [x] 5.5 Verify Movie, whole-Series, Season, and Episode manual-search inputs start with their specified media parent names and submit user-edited text.
- [ ] 5.6 Deploy the paired DLL and browser script to a test instance, refresh the web client, confirm existing Series/Season preview and retry behavior, and document rollback verification.

## 6. Single-Target Reliability and Progress Parity

- [x] 6.1 Reproduce or trace the iQIYI Movie failure and Tencent Movie hang, identify the provider-specific causes, and harden the affected paths without regressing Bilibili.
- [x] 6.2 Add a 180-second Movie/Episode task deadline, cancellation race, immutable terminal result, and target-aware retry support for both Movie and Episode.
- [x] 6.3 Replace the summary-only Movie/Episode progress view with the Season-style one-item detail view, including concrete status, diagnostic text, and retry.
- [x] 6.4 Make force-stop immediately enable the dialog close control and apply one stable action-sheet insertion anchor order to Series, Season, Episode, and Movie.
- [x] 6.5 Add deterministic backend and frontend regressions for timeout, cancellation, late completion, Movie retry, Episode retry, close-after-stop, one-row rendering, and consistent menu ordering.
- [ ] 6.6 Run strict OpenSpec validation, regression tests, and Release build; deploy a backed-up pair to Emby and live-verify iQIYI/Tencent failure handling, timeout/stop closing, retry, and existing Series/Season behavior.

## 7. Android Long-Press Integration

- [x] 7.1 Capture Android long-press media context from contextmenu/pointer/touch origins and bootstrap injection when an opened action sheet exposes its own media id without a prior desktop click.
- [x] 7.2 Preserve authoritative target correlation for a long-pressed Season inside a Series detail page and reject stale, mismatched, or unsupported action sheets.
- [x] 7.3 Bump the frontend installation flag, add deterministic long-press/action-sheet regressions, build, deploy with backup, and verify desktop menu injection remains functional.

## 8. r6 Unified Match Decision

- [x] 8.1 Add explicit default/rematch intent, match-origin and decision-reason fields, plus enabled-provider external-id key mapping to the backend match contracts.
- [x] 8.2 Implement provider-id resolution in configured site order, with current-item then Season/Series fallback inside one site, disabled-site filtering, detail validation, and unresolved diagnostics.
- [x] 8.3 Make compatible saved bindings secondary to provider identifiers and ensure rematch bypasses both without deleting persisted metadata.
- [x] 8.4 Replace the r5 selector with the single r6 `score >= 0.90` confident pool: choose earliest site across providers, unique highest score within that site, and ambiguous on a site-local top tie.
- [x] 8.5 Route Movie, Series, Season, and Episode preview plus automatic library-import matching through the same backend orchestration and remove legacy provider-specific match decisions from runtime paths.
- [x] 8.6 Add deterministic regressions for provider-id/site/media-level precedence, disabled and stale identifiers, binding conflict, rematch bypass, the 0.90 boundary, earlier-site 0.90 over later-site 1.00, site-local ties, and identical interactive/import conclusions.

## 9. r6 Successful-Download Provider Identifier Persistence

- [x] 9.1 Replace all-sites-clearing metadata helpers with an idempotent single-site, exact-media-level ProviderId upsert that preserves every other provider key.
- [x] 9.2 Carry match origin and level-correct provider identifiers through tracked Movie, Series, Season, and Episode download work without inventing identifiers for absent levels.
- [x] 9.3 Move ProviderId persistence after valid danmu-file success; skip redundant writes only for same-value `provider-id` origin, and do not write for failed, cancelled, skipped, timed-out, empty, or non-persisted outcomes.
- [x] 9.4 Apply identifier updates per successful target in batch/partial work and expose metadata-update errors separately without rolling back a successful file.
- [x] 9.5 Add regressions proving failed downloads preserve metadata, successful downloads overwrite only the selected site's old value, provider-id-origin downloads can skip writes, and partial batches update only successful targets.

## 10. r6 Frontend and Release Verification

- [x] 10.1 Render backend `provider-id`, `binding`, `scored`, and `manual` origins and decision reasons without frontend scoring, reordering, or independent automatic selection.
- [x] 10.2 Show provider-id resolution as matching success with a right-side `重新智能匹配` action; send explicit rematch intent and preserve metadata until download success.
- [x] 10.3 Bump frontend installation/version markers and document r6 ProviderId precedence, 0.90 site-priority behavior, rematch semantics, and successful-download writeback.
- [x] 10.4 Run strict OpenSpec validation, deterministic regression tests, and a Release build; verify packaged DLL and frontend assets are generated from the changed sources.
- [x] 10.5 Back up and deploy the paired r6 DLL and browser script to the authorized Synology Emby test instance, refresh the client, and live-test ProviderId success/rematch, Movie/Series/Season/Episode behavior, automatic import parity, successful/failed writeback, and rollback readiness.
