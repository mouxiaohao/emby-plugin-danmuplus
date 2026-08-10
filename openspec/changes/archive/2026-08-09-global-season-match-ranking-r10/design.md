## Context

See `proposal.md` for motivation. Manual preview previously contained provider-loop and scoring logic directly, while the library-import path called each provider's legacy media-ID search and later downloaded according to configured provider order. Emby season metadata can contain persisted manual and automatic provider IDs, and some seasons are backed by STRM episodes.

## Goals / Non-Goals

**Goals:**

- Establish one reusable provider-neutral search engine for both entry points.
- Guarantee that a search round covers all enabled providers before confidence evaluation.
- Make ranking deterministic and independent of configured provider priority.
- Preserve manual bindings and existing provider-specific download implementations.
- Avoid leaving stale automatic provider IDs that could affect later download selection.

**Non-Goals:**

- Parallelizing provider requests, which could increase rate-limit risk.
- Replacing provider APIs or changing danmu XML/ASS serialization.
- Changing CustomCssJS dialogs or the download queue protocol.
- Migrating existing manually selected bindings.

## Decisions

### Shared search engine with keyword-round outer loop

A provider-neutral engine owns querying, de-duplication, scoring, error isolation, and global ordering. Keywords form the outer loop and providers form the inner loop. This ensures the parent-title round reaches every provider before confidence is evaluated.

Alternative considered: retain provider as the outer loop and remove only the final break. That still allows one provider to run season-specific searches before later providers receive the parent-title query, produces avoidable latency, and makes early convergence unsafe.

### Parent-title first with confidence-based fallback

The parent series title is the first keyword. After every provider completes that round, the system scores the global candidate set. It stops only when the global confidence rule is satisfied; otherwise it proceeds through parent-plus-keyword, raw season name, and extracted season keyword.

Alternative considered: always run every query. This returns more low-relevance candidates but made a representative preview take about 41 seconds and increases provider request volume. Global-round convergence reduced that case to about 11 seconds without changing the selected result.

### Global deterministic sort without source order

Candidates are sorted by composite score, title evidence, parent and season evidence, episode and year evidence, then stable textual identifiers. `SourceOrder` remains available in the response model for compatibility but is not a ranking key.

Alternative considered: use provider order only for score ties. This was rejected because configured priority would remain an observable candidate-order influence and could reintroduce accidental provider preference.

### One automatic provider binding per season

Automatic library-import matching removes existing automatic danmu provider IDs from the current repository season, preserves `*Manual` keys, and writes only the global winner. The ensuing update/download event therefore cannot fall back to a stale earlier provider.

Alternative considered: keep all automatic provider IDs and add a separate winner marker. That would require a metadata migration and changes to every later consumer of provider IDs.

### Manual bindings short-circuit normal automatic matching

An existing `*Manual` identifier is resolved before search and remains authoritative. A forced manual preview bypasses that shortcut to let users inspect and replace the selection.

## Risks / Trade-offs

- [Sequential all-provider rounds increase preview latency] → Stop after a completed global round reaches unique high confidence; cache behavior in providers remains available.
- [A provider failure reduces available evidence] → Isolate exceptions per provider and expose search diagnostics while continuing other providers.
- [Incomplete season metadata weakens year or episode scoring during very early import] → Treat missing evidence neutrally and refuse automatic binding when the global confidence margin is insufficient.
- [Removing stale automatic IDs changes old multi-source metadata] → Preserve every manual key and change only automatic danmu-provider keys during the automatic season-add path.
- [Emby repository updates can trigger follow-up events] → Retain the existing update-driven download lifecycle and persist a single unambiguous provider before it runs.

## Migration Plan

1. Build the .NET Standard plugin in Release mode.
2. Back up the deployed DLL and stop the Emby package.
3. Replace the DLL, preserve ownership and permissions, and restart Emby.
4. Verify plugin loading in `embyserver.txt`.
5. Force match-preview regression for representative seasons and assert descending scores and expected selections.
6. Compare the deployed DLL SHA-256 with the packaged artifact.

Rollback consists of stopping Emby, restoring the timestamped pre-r10 DLL backup, restoring its ownership and permissions, and starting Emby. No data-schema rollback is necessary; persisted provider identifiers remain compatible with earlier plugin versions.
