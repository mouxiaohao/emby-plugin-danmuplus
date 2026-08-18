## Why

The authoritative virtual-season planner currently pairs local and source Episodes by list position and replaces a missing source Episode number with `index + 1`. A sparse Emby inventory can therefore shift every later mapping—for example, a library missing E7-E9 maps local E10 to source E7—and the same compression can occur inside an explicitly anchored virtual segment.

## What Changes

- Preserve whether a source Episode number was explicitly supplied instead of presenting a synthesized list ordinal as source numbering.
- Use one server-authoritative virtual-segment alignment policy for whole-Series preview, single-Season preview, automatic positive-Season processing, confirmation, and download-time rebuild.
- When the user has not explicitly changed the first segment's source start and both sides have reliable explicit positive numbering, align that segment with zero numeric offset so local E10 maps source E10 even when earlier local Episodes are absent.
- When a user-confirmed interactive continuation explicitly anchors a segment, preserve that exact local/source anchor and align later Episodes by number delta; local E29 mapped to source E1 therefore maps local E31 to source E3 when E30 is absent. Unattended/media-import processing remains non-recursive and never searches a residual source.
- Treat explicit local or source gaps as gaps rather than reasons to compress later mappings. A source coordinate with no local ItemId remains unused; a local coordinate with no source Episode remains unmatched.
- Use stable positional pairing for an entire segment only when either side cannot establish reliable unique positive numbering. Never mix numeric and positional pairing inside one segment.
- Make `SourceStartEpisodeId` the authoritative source anchor. A legacy number-only source start may resolve only when the entire verified source scope has reliable unique positive provider-supplied numbering and the requested number has one unique match; it MUST NOT silently fall back to `number - 1` as a list position.
- Version and fingerprint the changed mapping semantics so stale previews or drafts fail closed before download or metadata writes, while already frozen retry/seven-day replay entries continue using their exact captured local/source Episode and CommentId tuple; a changed revalidated CommentId fails closed rather than being substituted.
- Preserve Emby Season membership and numbering, target-season scope filtering, candidate scoring, source-surplus warning behavior, identifier/persistence safety, and the user's ability to leave an unmatched range unselected.
- Do not automatically repair or delete XML files produced by an older incorrect mapping. Any force refresh of affected Episodes remains a separately confirmed action after read-only preview verification.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `parent-season-aware-episode-mapping`: Replace unconditional list-position compression with reliable number-aware virtual-segment alignment, deterministic positional fallback, authoritative anchors, and stale-plan safety.
- `season-danmu-matching`: Require whole-Series, single-Season, automatic positive-Season processing, and rebuild paths to produce the same sparse-inventory mappings for the same target and selections.
- `season-episode-scope-filtering`: Preserve the confirmed entry-point boundary that only an explicitly targeted single Season may process Season 0; whole-Series and unattended/media-import paths skip it before search or planning.

## Impact

- Planning model and pure planner: `Model/CompositeSeasonMatch.cs`, `Scraper/CompositeSeasonMatchService.cs`, and `Scraper/CompositeSeasonPlanner.cs`.
- Shared entry points and stale-plan fencing: `Core/SeasonPlanGenerationCoordinator.cs`, `Core/Controllers/DanmuController.cs`, `LibraryManagerEventsHelper.cs`, and the existing plan fingerprint/state paths.
- Browser protocol/cache marker only as required to reject pre-change mapping drafts; the browser does not choose alignment mode or author mappings.
- Deterministic regressions: composite planner, target-season scope, controller/source contracts, automatic rebuild, frontend protocol, main backend, and seven-day frozen replay.
- Live validation is approval-gated and credential-free in repository artifacts: back up deployed assets first, deploy only a locally verified package, restart Emby, and use read-only previews before any force refresh.
- No provider API, search/scoring rule, Emby Season hierarchy, database migration, or external runtime dependency changes.
