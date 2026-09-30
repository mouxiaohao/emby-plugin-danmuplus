# Verification

## Scope

The patch requests Emby's logical presentation grouping at the Season/Episode inventory boundaries. The player, XML path policy, physical media, source ranking, and plugin version identifiers are unchanged. This is a develop patch/PR, not a replacement of the published release or its assets.

## Automated checks

- The new SDK fixture failed before the fix with `24 physical S1 files must yield 12 logical matching/download items`, then passed after grouping was enabled.
- New tests cover logical Season enumeration, twelve one-to-one mappings, ten-source/two-episode remainder, exclusions, explicit S00, equal numbers with different presentation identities, shared coordinator parity, and representative/plan-fingerprint drift.
- Full backend regression runner passed, including the new `--logical-inventory` subset.
- R5TargetSeasonScope, R4ParentSeasonContext, EpisodeSelectionPolicy, R207RemainderCore, R207RemainderOrchestration, and R207R2SeasonContinuity passed.
- Smart Match frontend regressions and JavaScript syntax check passed.
- Unmodified dd-danmaku bundle reproducibility check and all 39 player regressions passed.
- Strict OpenSpec validation and whitespace checks passed.
- Release build: 0 errors, 131 existing warnings.

The root OpenSpec context still mentions global descending-score order. Existing production `OrderCanonicalCandidates` and regression contracts instead specify provider priority first and descending score within a provider. This patch preserves that current behavior; it does not change ranking to satisfy the obsolete context sentence.

## Synology deployment, 2026-10-01

- Fresh rollback directory suffix: `20261001T063712+0800-pre-logical-episodes`; previous DLL and both configuration files retained.
- Replaced only the plugin DLL and restarted Emby; HTTP health returned 200 and the process changed. Startup log confirmed the plugin loaded.
- Readback SHA-256: `4f351331e00977a168548574a0532a76a81e54cac2d9fcb8494d9bf5535a60f3`.
- DLL owner/mode preserved. Both configuration files remained byte-identical, including Smart Match V37 and dd-danmaku.
- Affected Series preview: three regular Seasons, twelve eligible Episodes and twelve mappings in each, E01-E12 mapped to source E01-E12, zero unmatched runs. Previous behavior was five target cards and 108 episode entries.
- Explicit S1 preview: twelve mappings, zero unmatched runs, and exactly the same ordered Episode representatives as the whole-Series S1 result.
- Explicit S00 preview: two displayed/eligible logical Episodes, not included in whole-Series targets. Source discovery remains ambiguous and requires manual selection; this is not claimed as an automatic S00 match.
- Live candidate ordering passed provider-priority and within-provider descending-score checks.
- Independently resolved all twelve S1 plan ItemIds through Emby's item API and compared them with Emby's displayed Episode list: all twelve representatives matched, with 1080P/720P media sources belonging to those same logical Episodes.

Live checks use preview endpoints, not a forced download or metadata rewrite. Actual playback of both video versions using existing server XML was user-confirmed before this change; this deployment does not claim new physical-device playback acceptance.

## GitHub delivery

Implementation commit `3e2df52` was pushed to `develop`. PR #20 targets `main`, includes the earlier V37 touch-focus correction, and was attached to the task. The PR is left open; no main merge or existing release-asset replacement was performed.
