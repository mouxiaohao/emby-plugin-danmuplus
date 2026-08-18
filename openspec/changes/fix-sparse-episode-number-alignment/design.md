## Context

See `proposal.md` for the observed sparse-inventory failure and the three delta specs for required behavior. Every Season candidate already enters one explicit virtual Episode plan; “temporary season” is only the UI name for an unmatched local run and is not a second Season model or a different planner.

The current source projection writes `episode.EpisodeNumber ?? index + 1` into one field, so downstream code cannot distinguish an explicit provider number from a synthesized ordinal. The planner then pairs `local[localStart + offset]` with `source[sourceStart + offset]`. Both initial owning-source helpers and later segment helpers inherit that positional compression. The controller also treats a failed number lookup as `number - 1`, which makes one input mean either a source number or an ordinal.

Preview and download do not execute the browser's displayed mappings directly. The server rebuilds provider details and an authoritative plan, then compares generation/fingerprint state. Automatic positive-Season processing and frozen retry/replay have additional entry points, so changing only the interactive preview loop would create divergent mappings.

## Goals / Non-Goals

**Goals:**

- Use one virtual-segment alignment resolver for default first segments, explicitly anchored first or residual segments, whole-Series/single-Season preview, automatic positive-Season processing, and authoritative rebuild.
- Preserve explicit local and provider Episode coordinates, including gaps, while retaining deterministic positional behavior for sources that genuinely lack reliable numbering.
- Make explicit user source-start changes override the default first-segment zero offset without letting the browser author exact mappings.
- Fail closed when preview/rebuild facts or alignment mode change, and preserve frozen exact retry/replay behavior.
- Preserve the confirmed entry-point boundary: explicit single-Season S0 remains available, while whole-Series and unattended/media-import paths skip S0.

**Non-Goals:**

- Changing candidate discovery, Season scoring, TMDB aliases, provider HTTP APIs, source Episode filtering, or the source-surplus warning.
- Changing Emby Season membership or Episode numbering, creating missing local Episodes, or treating unused source Episodes as local temporary groups.
- Reading local Episode/Season ProviderIds as batch matching evidence.
- Automatically deleting or rewriting existing XML files produced by an older mapping.
- Replacing exact source identity, CommentId validation, cancellation, generation checks, or persistence safety.

## Decisions

### 1. Preserve explicit numbering provenance separately from stable order

`CompositeSeasonSourceEpisode.EpisodeNumber` will mean only a provider-supplied positive/zero/negative value as observed; absence remains null. Add a separate stable source ordinal for deterministic display and positional fallback. `GetSourceEpisodes` must never write `index + 1` into the explicit-number field.

Local ItemId remains identity and local placement/order remains the authoritative sequence. Local `EpisodeNumber` is only a coordinate used by the alignment resolver; it never replaces ItemId identity.

This is preferred over inferring provenance from the numeric value because a synthesized `1` is indistinguishable from a provider's real E1 after projection.

### 2. Model every planned source application as one virtual segment with ordered windows

Do not create separate “ordinary Season” and “temporary Season” planners. A segment has an authoritative local start ItemId, a verified source start EpisodeId, a requested local-row limit, a selected source, and one closed alignment intent:

- `DefaultZeroOffset`: server-created first-source intent when the user has not changed its source start.
- `ExplicitAnchor`: user-confirmed interactive intent carrying the exact local/source anchors. It is valid for a first segment as well as a residual segment. Unattended/media-import processing never creates this intent by recursively searching a second source.

A virtual segment may contain multiple ordered segment windows. A window is the maximal run resolved for the selected source before an in-scope local ItemId that is already mapped by the current server plan to a different source. That different-source mapping is retained as a boundary: it is not remapped, does not belong to either adjacent window, and does not consume a coordinate or stable ordinal from the continuing source. Records excluded by the Season-scope filter are removed before window construction and therefore cannot create a boundary. An already verified mapping to the selected source may remain inside a window as direct evidence when its exact identity and that window's alignment are consistent.

If the selected source continues after a boundary, the next window uses the recorded source frontier, its exact next verified source EpisodeId (or exhaustion), and a new exact local anchor. The partitioner charges any real numeric gaps adjacent to the boundary exactly once while the boundary ItemId itself advances the frontier zero times. This is still one virtual source application, not a recursively discovered residual source or a second Season model.

The browser may report the user's intent to override a start, but it cannot choose the algorithm, numbering reliability, offset, window boundaries, source frontier, source identity, or final mappings. The server validates the current protocol/generation, target-bound candidate evidence, local ItemId, source EpisodeId, and provider details before honoring it.

An explicit source-start change always wins. For example, local E1 with an explicit source E5 anchor uses offset `+4`; local E1 maps E5 and local E4 maps E8 even if E3 is absent.

### 3. Select one alignment mode for each segment window

The resolver evaluates each ordered segment window independently. Numbering is reliable for a window only when every participating local row has a positive number unique on the local side and every verified source Episode participating in that window has a provider-supplied positive number unique on the source side. Gaps such as `1,3` are reliable; null, zero, negative, or duplicate numbers are not. A later window may select a different mode without changing the mode already selected for an earlier window.

If both sides are reliable, use numeric alignment:

- `DefaultZeroOffset`: `sourceNo = localNo`.
- `ExplicitAnchor`: `sourceNo = sourceAnchorNo + (localNo - localAnchorNo)` using checked wide arithmetic.

Build lookups by explicit number and map coordinate intersections. Within one window, a missing local coordinate leaves the projected source coordinate unused, and a missing source coordinate leaves that local ItemId unmatched; later coordinates retain the same per-window offset. These real numeric gaps still advance the outgoing source frontier through the complete projected coordinate span. A different-source boundary advances neither the numeric frontier nor the positional frontier.

If either side is unreliable, use positional fallback for the whole window from the exact local/source anchors and stable orders. Never map some rows numerically and later rows positionally inside one window. Invalid/duplicate EpisodeIds, blank CommentIds, unknown local ItemIds, overlaps, or repeated exact source identities remain structural errors and do not qualify for fallback.

`RequestedEpisodeCount` continues to mean at most that many authoritative local rows beginning at the local anchor, not a numeric coordinate span or an applied-mapping count. Each window resolution returns its considered local rows and applied mappings. Its effective outgoing frontier is deterministic rather than a persisted resolution field: numeric continuation derives it from the exact anchor, ordered considered local coordinates, and complete verified source numbering/order; positional continuation derives it from the exact source anchor and considered-row count. Internal same-source multi-window continuation carries that forward-only value only for the duration of planning and resolves the next verified source EpisodeId or exhaustion. A submitted production selection is already one window and carries its own server-validated exact `SourceStartEpisodeId` as its incoming frontier. Source coordinates or stable ordinals behind the effective frontier are deliberately bypassed and cannot be offered again to a later window.

### 4. Make the source EpisodeId the only authoritative anchor identity

When `SourceStartEpisodeId` is supplied, resolve that exact verified entry. A legacy number-only request is compatible only if it uniquely matches one provider-supplied positive number in a reliable source scope. Remove the `ElementAtOrDefault(number - 1)` interpretation. Positional fallback still works when an exact source EpisodeId is present even if source numbers are absent.

This prevents a sparse or duplicated source list from silently selecting a different Episode than the user saw.

### 5. Reuse one resolver across initial, residual, and reconstruction paths

Replace offset-by-applied-count loops in the remaining-source helpers with resolver calls that retain alignment intent and anchors. Initial owning-source application uses `DefaultZeroOffset` unless the validated interactive selection says the user changed the start. Manual or previously confirmed interactive residual selections use `ExplicitAnchor`; unattended/media-import matching applies only its initial positive-Season result and does not discover or append a residual source.

Normalization and continuation first partition a source application into the ordered windows defined above and then call the same resolver for each window. The first window uses the captured default or explicit anchor. Every later window uses the first eligible local ItemId after its boundary together with the exact next source EpisodeId carried by the prior window's outgoing frontier as a server-derived anchor; an exhausted frontier yields no later mapping. It does not restart at the first unused mapping by applied count. Thus local E29-E33 may map selected-source E1-E5, a local E34 already mapped to another source consumes nothing, and the next window maps local E35-E39 to selected-source E6-E10. By contrast, an absent local E30 inside a window advances the numeric frontier, so an E29→E1 anchor maps E31→E3 and E2 is never reused after a later boundary.

Automatic positive-Season snapshots retain the default initial selection used by their own authoritative build. Confirmed interactive snapshots retain the ordered selections, each selection's exact local/source anchors, resolved mode, ordered considered local ItemIds, complete verified source facts/order, and final exact mappings. No separate frontier snapshot is persisted. Download-time reconstruction must not ignore `SourceStartEpisodeId`. Existing server-verified mappings to the selected source may establish or verify an affine offset only inside their own window and only when consistent with that window's anchor; conflicting offsets inside one window fail closed. A different-source boundary permits the continuing source to use a new per-window affine offset at the carried frontier. Local Episode ProviderIds remain forbidden as batch evidence.

Exhaustion and residual-source decisions use the outgoing frontier plus the verified source order/coordinates, not the set or count of mappings already applied. A skipped source coordinate behind the frontier remains intentionally unavailable and must not be presented as residual work.

Whole-Series invokes the same per-Season operation as explicit positive-Season matching. Media import remains non-recursive and may apply only its ordinary initial positive-Season result; it does not gain interactive residual recursion. Explicit single-Season S0 uses the same resolver, while automatic/unattended and whole-Series S0 are skipped before search.

### 6. Advance the mapping protocol and fingerprint the semantic inputs

Advance the server/browser mapping protocol from V21 to V22 and the frontend installation marker from V27 to V28. Add only the minimum closed intent needed to distinguish a default first source from an explicit start override. Old V21 drafts are rejected and require a fresh preview.

The plan fingerprint covers the canonical facts that uniquely determine every submitted window and any effective frontier: protocol, target inventory, selection order, alignment intent/mode, exact anchors, ordered considered local ItemIds, explicit-number provenance and values, complete source identity/order, and final exact mappings. Any change capable of moving a boundary or derived frontier therefore changes the fingerprint and yields stale-plan failure with zero writes; rebuild must not reuse a bypassed source coordinate or downgrade a window from numeric mode to positional fallback.

Exact mappings remain the execution authority. Tracked retry and seven-day replay keep their frozen local ItemId/provider/media/source EpisodeId/CommentId tuple and do not enter the alignment resolver again. They execute with the captured CommentId when no provider-detail revalidation is required. If a path revalidates provider details and either the exact source EpisodeId is absent or its current CommentId differs from the captured non-empty CommentId, it fails stale instead of substituting a current CommentId or a number-/position-matched Episode.

### 7. Preserve advisory and persistence boundaries

The source-surplus warning continues to compare verified source inventory with eligible local inventory after an authoritative plan applies at least one mapping. Sparse local coordinates do not create synthetic unmatched Episodes; in the Spy Family example all existing local ItemIds may be mapped while source E7-E9 remain unused, so the existing source-surplus warning remains the user-visible notice.

Season display-identifier and Episode identifier writes remain behind the existing successful current-generation terminal policy. A stale, structurally invalid, cancelled, or zero-write plan performs no metadata mutation.

## Risks / Trade-offs

- [Some provider adapters expose positional Episodes without explicit numbers] → Preserve null provenance and use whole-segment positional fallback from exact IDs; do not fabricate numbers.
- [A single duplicate or missing number switches a long window to positional fallback] → Use one mode per segment window to keep preview/rebuild deterministic; fingerprint each decision and surface the chosen fallback in internal diagnostics/tests rather than mixing modes.
- [A boundary reset could reuse a source coordinate skipped by a real gap] → Derive a forward-only frontier from canonical window facts, carry it only inside internal multi-window continuation, and fingerprint all frontier-determining inputs and exact outputs; never advance by applied-mapping count.
- [A provider detail response changes between preview and download] → Fingerprint provenance, anchors, stable order, and exact mappings; reject stale rebuilds with zero writes.
- [Protocol V22 requires the matching V28 frontend asset] → Build, package, back up, deploy, and read back the DLL and CustomCssJS as a pair; reject V21 rather than accepting mixed versions.
- [Old incorrect XML remains on disk] → Do not perform automatic destructive cleanup; identify affected existing Episodes after read-only preview and require an explicit force-refresh action.
- [Changing automatic S0 behavior could trigger unwanted provider work] → Enforce the previously confirmed boundary before provider search and cover zero-call/zero-write behavior; explicit single-S0 remains unchanged.
- [Numeric difference could overflow] → Use checked wide arithmetic and fail closed before constructing mappings.

## Migration Plan

1. Implement provenance and the pure resolver first, with deterministic unit fixtures for zero-offset, explicit anchors, per-window local/source gaps and frontiers, unreliable numbering, and structural failures.
2. Wire every preview, automatic positive-Season, continuation, and rebuild path to the same window partitioner and resolver; preserve different-source boundaries, advance protocol V22/V28, and add stale/fingerprint coverage.
3. Run affected deterministic suites sequentially, then the complete backend/frontend regressions, strict OpenSpec validation, scope/credential checks, and a clean Release build. Do not run competing .NET builds.
4. Have a Sol high-reasoning reviewer inspect mapping semantics, entry-point parity, frozen replay, persistence safety, and the final package.
5. Present the reviewed package paths and hashes and obtain a fresh explicit confirmation for that exact package before replacing the DLL/CustomCssJS pair or restarting Emby. Then back up the deployed DLL, CustomCssJS, and plugin configuration with hashes/modes, deploy only the confirmed pair, restart Emby, and verify health/readback.
6. Use read-only smart-match previews to verify Spy Family S3 sparse local numbering and a Frieren anchored segment with an interior local gap. Do not initiate a download during acceptance preview.
7. Only after mappings are verified, separately identify any existing wrongly mapped XML and request/record confirmation before force-refreshing those Episodes. Roll back the backed-up pair if health or preview acceptance fails.
