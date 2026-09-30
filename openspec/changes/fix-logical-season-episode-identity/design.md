## Context

See proposal.md for the observed duplicate-season and duplicate-episode failure. Raw library queries currently omit Emby's presentation grouping. The installed compile SDK exposes `InternalItemsQuery.GroupByPresentationUniqueKey`; Season's query-taking `GetEpisodes` overload preserves that setting while establishing the selected Season's scope and ordering.

## Goals / Non-Goals

Use the same logical presentation identities as Emby, not filename, resolution, ItemId sorting, or a guessed season/episode-number deduplication rule. Continue to execute one server-selected representative through the existing XML writer. Do not merge arbitrary same-number episodes or assemble one Season by borrowing other Season inventories.

## Decisions

1. Request presentation grouping explicitly in both direct and fallback Series Season queries, including the shared Season enumeration helper used by background refresh and context resolution. Retain full library objects and their metadata.
2. Request presentation grouping in the single authoritative Season planning coordinator. Let Emby choose the representative and order. This corrects counts and every downstream planner/executor without adding another physical-version algorithm. Raw-source numbering and the player's working XML lookup remain untouched.
3. Keep the existing exact-parent scope filter after grouping. Keep representative ItemIds in mappings and fingerprints, so a changed representative or scope makes an already captured plan stale rather than silently redirecting a write.
4. Test the production coordinator against an SDK library-manager fixture that distinguishes grouped logical results from raw physical results. Verify twelve unique mappings, short-source remainders, S00 exclusion/inclusion, representative change detection, and no per-version download expansion. Add source contracts for direct/fallback queries and run existing planner/orchestration regressions. Live preview is the acceptance check for actual Emby grouping behavior.

## Risks / Trade-offs

- Emby metadata defines logical identity; unrelated entries with equal numbers must not be collapsed by our own heuristic. Bad library identity metadata remains an Emby-library issue.
- A changed canonical representative invalidates a pending plan. Reopen matching instead of executing obsolete physical ItemIds.
- SDK fixtures cannot prove production database grouping; compare live affected-series preview to Emby's displayed Season/Episode inventory after deployment.

## Migration Plan

No persistent-state migration or media/XML rewrite. Build the existing plugin version with this patch, back up the active DLL and both configuration files, stage and verify the new DLL, stop Emby, replace only the DLL, and restart. Retain a rollback copy and verify HTTP health, plugin initialization, live preview counts/mappings, and unchanged configuration hashes. Push develop and create a PR to main without merging or replacing release tags/assets.
