## Why

Emby exposes multiple physical versions of one logical season or episode through raw library queries. Smart Match currently counts and maps those versions independently, turning three twelve-episode seasons into five season cards and 108 episodes and consuming source episodes twice.

## What Changes

- Build Series targets and Season planning inventories from Emby's logical season/episode identities, retaining one canonical representative for execution.
- Use that same inventory for counts, matching, remainders, manual selections, download rebuilds, and background Season processing.
- Keep XML persistence and player loading unchanged: one download per logical episode, without writing one XML per physical version.
- Preserve exact parent-season eligibility, explicit S00 matching, source ranking, and stale-plan protection.
- Add deterministic regression fixtures and verify the affected series on the live server before pushing develop and opening a main PR.

## Capabilities

### New Capabilities

- `logical-library-inventory`: Logical season and episode identity for matching and downloads across multiple physical versions.

### Modified Capabilities

None.

## Impact

Backend season enumeration and authoritative planning context, regression tests, and a DLL-only Synology deployment. No media-file changes, dd-danmaku changes, XML fan-out, library metadata repair, release-tag replacement, or automatic PR merge.
