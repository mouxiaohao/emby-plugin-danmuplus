## Why

Formal 2.0.6r1 V30 fixed Android command-owned Back, but live phone and car-head-unit acceptance exposed a separate modal-scrolling defect: vertical panning inside Smart Match opened from Series, Season, or Episode detail pages can also move the underlying Emby detail view. The same workflow opened from a media-library card does not visibly move its virtual-list background, which isolates the problem to uncontained modal scroll propagation over different host scroller topologies rather than to matching logic or entry selection.

## What Changes

- Make every Smart Match overlay, card, and internal body terminate its own vertical scroll chain so short content and top/bottom overscroll cannot move the Emby host view, while the body remains the native scroll owner for long content.
- Apply one entry-neutral modal rule to Series, Season, Episode, and Movie workflows opened from detail pages, Season cards, Episode rows, or media-library card menus; do not add a detail-page or Android-only branch.
- Preserve the r1 action-sheet close/overlay insertion order, V30 command-owned Android Back design, zero dialog history, parent/child viewport restoration, server-authored candidate order, and all matching/download/persistence behavior.
- Exclude global or document-level touch/wheel ownership, `touch-action:none`, host `html/body` locking, host-scroller lookup, host scroll snapshots/restoration, timers, animation frames, and action-sheet delay from the primary fix.
- Release the correction independently as 2.0.6r2: retain Assembly `2.0.6.0` and mapping protocol V22; advance File to `2.0.6.2`, Product/configuration/TMDB User-Agent to `2.0.6r2`, cache token to `2-0-6r2`, and frontend installation marker to V31.
- Keep every 2.0.6r1 source snapshot, artifact, hash, verification record, deployed rollback set, and OpenSpec change immutable; create a separate r2 review package, verification record, deployment backup, and rollback path.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `smart-match-error-and-presentation`: require an open Smart Match dialog to contain touch, pointer-pan, and wheel scrolling within its own overlay/body without moving any underlying Emby detail, library, or virtual-list scroller, including short content and internal top/bottom boundaries.

## Impact

- Frontend modal CSS and deterministic Smart Match regression fixtures.
- Plugin/configuration/TMDB version identity, README/UPDATE history, and a new `artifacts/2.0.6r2` five-file review package.
- Live acceptance on desktop, Android phone, and Android car head unit, with r1 retained as the authoritative rollback predecessor.
- No backend scoring, provider order, candidate participation/order, DTO, route, API, binding, download, metadata, episode mapping, external dependency, or mapping-protocol change.
