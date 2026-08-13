# 2.0.3r6 frozen r5 baseline

The r6 implementation starts from the exact source workspace that produced the
accepted 2.0.3r5 deployment.  The sibling directory
`C:\Users\mouxi\Documents\Codex\emby-plugin-danmuplus-2.0.3r5` remains the
read-only, diffable source reference for every r6 product-file review.

## Paired accepted assets

| Asset | SHA-256 |
|---|---|
| `artifacts/2.0.3r5/Emby.Plugin.Danmu.dll` | `123ee755f22ae20a1a2492f4d616c4b6f8cd232bfc629fac25f0a4c466b8d552` |
| `artifacts/2.0.3r5/DanmuSmartMatch.CustomCssJS.js` | `b457b4cbd4dc91a250230531cc8124bbd174872577963bf8976491d870546b9d` |

The source frontend has the same JavaScript hash.  The batch protocol/cache
pair is V21 / `__embyDanmuSmartMenuV21`.

## Product-file source hashes before r6 edits

| File | SHA-256 |
|---|---|
| `Core/Controllers/DanmuController.cs` | `9901be70e6a565d8a66db89aed10f2b7cc2fb13854c493f1e4de8741a0f101b8` |
| `Model/DanmuMatchResult.cs` | `0f16343baa4b22df8201aa218b4b1fb5ab01728cb3de230c668080fb0115197f` |
| `Frontend/DanmuSmartMatch.CustomCssJS.js` | `b457b4cbd4dc91a250230531cc8124bbd174872577963bf8976491d870546b9d` |
| `Frontend/DanmuSmartMatch.RegressionTests.js` | `b5b500ee947a666eaa966edc329533850ad0f9a3559848e5c6eda17f4aa1907d` |
| `RegressionTests/Program.cs` | `1ad3e194bc3d887b41259eb584b9433856205ef29acbe254d65e798a35212952` |

## Baseline verification

Before product edits, the following checks passed on 2026-08-13:

- frozen r5 scope/spec verification against commit
  `5f980931370343af403fa4a3c3a011e747176abd` (42 allowed changed files);
- main backend deterministic regression suite;
- r5 target-season scope regression suite;
- frontend smart-match regression suite;
- Release solution build with 0 errors (131 existing warnings).

The active implementation branch is
`codex/2.0.3r6-lazy-candidate-details`.  A release is invalid if built outside
this r5-derived workspace or if it introduces the later
`DanmuSeasonCollection` / `DanmuSeasonSegment` protocols.
