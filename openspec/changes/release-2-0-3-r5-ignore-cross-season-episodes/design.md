## Context

See `proposal.md` for motivation. r4 introduced a placement-aware explicit mapping planner that retained foreign logical-season Episodes as supplemental temporary runs. r5 deliberately changes that product behavior: every batch Season operation reads the selected Emby Season's own Episode inventory and scopes matching to exactly that Season number. Standalone Season 0 reads the real Season 0 item's own inventory and remains independently matchable.

The implementation must remain compatible with the user-approved live Emby
4.9.5.0 baseline, .NET Standard 2.0, C# 8, identifier-free Series/Season
search, ItemId identity, explicit mappings, bounded provider search, retry
snapshots, and write-only complete-single-source Season display identifiers.

## Goals / Non-Goals

**Goals:**

- Produce one authoritative target-Season Episode inventory and one eligibility decision used by every batch path.
- Keep normal-season plans continuous by physically removing Parent 0, other-season, and unknown-parent Episodes before scoring and planning.
- Keep standalone Season 0 independently matchable from its own Parent 0 Episode inventory.
- Make preview, download, retry, automatic processing, and Season identifier mirroring detect scope drift and agree on the same eligible set.
- Provide an r5 protocol fence that prevents r4 supplemental drafts from executing.

**Non-Goals:**

- Moving media between Emby Season items or rewriting episode metadata.
- Guessing an unknown parent season from filenames, numbering, position, or provider IDs.
- Matching ignored foreign Episodes from the normal-season dialog.
- Removing Season 0 support from its own Season page or automatic Season 0 event.
- Changing single-Episode or Movie exact-identifier behavior.

## Decisions

### 1. Build inventory from the selected Season, then filter by its season number

The coordinator will enumerate `targetSeason.GetEpisodes()`, retain one record per valid ItemId, preserve its observed display/placement order, and select eligible records with `ParentIndexNumber == target.IndexNumber`.

The target Season's `IndexNumber` is the authoritative logical-season value. A normal Season therefore filters out placed S00 and other-season records, while an explicitly selected Season 0 accepts only Parent 0 records returned by Season 0 itself. r5 does not scan other Seasons to supplement Season 0 and does not infer membership from placement, filenames, provider identifiers, or episode-number patterns.

If the selected Season inventory cannot be obtained or contains irreconcilable duplicate metadata for one ItemId, planning fails with a structured inventory diagnostic. It must not silently score, map, download, or write metadata from a partial or inconsistent target inventory.

### 2. Separate inventory, eligibility, and presentation

The authoritative context will carry:

- the selected Season's complete observed ItemId-to-parent-season snapshot;
- the filtered eligible local Episode list;
- stable local ordering within that logical season;
- out-of-scope counts grouped by Parent 0, other-season, and unknown parent for diagnostics only.

Out-of-scope records are not planner inputs and cannot become unmatched runs. The UI may show a read-only summary such as “displayed 19, matching 12, ignored 7 cross-season episodes,” but must expose no match/download controls for ignored items.

### 3. Whole-Series target selection and standalone Season selection are distinct policies

Whole-Series enumeration accepts only known positive Season numbers. Explicit single-Season and automatic per-Season operations accept zero or positive known numbers and pass the selected number into the same context builder.

This keeps the planner shared while satisfying the deliberate product difference: Series skips S0, but the S0 page remains usable.

### 4. Search and mapping consume only eligible Episodes

Expected episode count, candidate scoring context, explicit source application, temporary-run generation, partial confirmation counts, execution entries, retry summaries, and complete-single-source eligibility all derive from the filtered eligible list.

The planner remains capable of multiple virtual sources within one logical season. A short source creates temporary runs only from remaining eligible Episodes. No special-case supplemental path may re-add foreign ItemIds.

### 5. Scope fingerprint includes excluded observations from the selected Season

The r5 structure fingerprint will hash the Series identity, target Season identity/number, every ItemId and `ParentIndexNumber` observed in the selected Season inventory (including excluded records), eligible ordering, selections, and mappings. A parent-season change therefore invalidates the captured plan even when the final eligible count happens to remain equal.

This is preferred over hashing only the filtered list, which could miss a foreign Episode becoming newly eligible or an eligible Episode moving out of scope.

### 6. Season display identifiers use eligible completeness

The existing terminal, current-generation, complete-single-source mirror remains write-only. It evaluates completeness over eligible target Episodes only and overwrites only the verified provider's target Season key. Ignored records do not block the mirror and cannot cause identifier cleanup; partial eligible coverage, multiple sources, failure, cancellation, or stale structure remain no-op.

### 7. Upgrade the batch protocol and frontend cache marker

r5 will use the next numeric mapping protocol version and frontend install/cache marker. The server rejects r4/V20 drafts, selections, and download requests rather than attempting to strip supplemental groups client-side. Closing/reopening the dialog performs a fresh search under the new scope.

### 8. Automatic import uses the same coordinator

Automatic normal-Season and automatic Season 0 processing call the same target-Season-own-inventory context builder as interactive matching. Whole-Series skip behavior is applied only when enumerating a user-initiated Series target set, not as a hidden prohibition on an explicit automatic S0 event.

## Risks / Trade-offs

- **[Target Season inventory is unavailable or inconsistent]** → Fail closed with a structured inventory diagnostic and zero writes instead of guessing or borrowing Episodes from another Season.
- **[Users may not understand why displayed specials disappeared]** → Show a read-only ignored-count/coordinate summary without presenting ignored items as matchable temporary seasons.
- **[Existing V20 dialogs submit foreign selections]** → Reject through the protocol/generation fence and require a fresh r5 preview.
- **[Normal Season can be considered complete while display inventory contains ignored items]** → State explicitly in UI/logs that completeness is based on the target logical season, and retain the no-cleanup/target-key-only mirror policy.
- **[Season 0 ordering can contain duplicate or sparse episode numbers]** → Preserve the selected Season 0 inventory's observed display/original ordinal and use ItemId only as the final tie-break, never as season evidence.
- **[r4's deployed delta is not yet synchronized into main specs]** → Before r5 implementation, sync the r4 delta specs without archiving r4, then add an r5 delta for `parent-season-aware-episode-mapping` that replaces supplemental foreign-season behavior with exact target-season exclusion. Strict cross-change validation must prove the final requirements contain no remaining SHALL that exposes S00/other-season Episodes as normal-season temporary runs.

## Migration Plan

1. Freeze the deployed r4 DLL/V20/source and live One Punch/Seitokai inventories.
2. Implement the target-Season inventory and exact season-number scope as pure domain logic, then route all batch paths through it.
3. Upgrade assembly/file version and frontend/server protocol marker together.
4. Run deterministic normal-season, cross-season, Season 0, stale-scope, identifier-metamorphic, mirror, and frontend regressions.
5. Back up the active r4 DLL and both plugin configurations, atomically deploy the paired r5 assets, restart Emby, and perform authenticated read-only Series/S1/S0 previews before any disposable write test.
6. Roll back the r4 trio if plugin loading, protocol, target filtering, or Season 0 inventory validation fails. Metadata already written by prior versions is retained because r5 does not clear identifiers.
