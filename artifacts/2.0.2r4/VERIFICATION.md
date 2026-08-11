# 2.0.2r4 Verification

## Release identity

- Baseline release: `2.0.2r3` (deployed Synology candidate).
- User/informational version: `2.0.2r4`.
- File version: `2.0.2.4`.
- Assembly version: `2.0.2.0`.
- Frontend installation marker: `V15`.

## Candidate hashes

- `Emby.Plugin.Danmu.dll`: `2C31BD410A8A5CA1C74AD313A021CE5F5A4F5D68AE01809D5931CA99DB23B796`
- `DanmuSmartMatch.CustomCssJS.js`: `52154971B1E2845EAAA2E98A921373939EC87B005F41E33B3A87080F73AF979B`

## Local verification

- `dotnet run --project RegressionTests/Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-restore`: passed.
- `dotnet build Emby.Plugin.Danmu.csproj -c Release --no-restore -v:q`: passed with 0 errors (existing warnings retained).
- `node --check` for both frontend files: passed.
- `node Frontend/DanmuSmartMatch.RegressionTests.js`: passed.
- `RegressionTests/VerifyR4ReleaseScope.ps1`: passed.
- Alias-only Season/Episode-context scoring is `35/20/45`, Movie is `70/30`, title floor is `0.72`, and confidence remains `0.90`.
- Exact provider-ID resolution remains configured-provider outer / item-scope inner.
- Successful Season/Episode writes use exact registered ordinary keys from all registered scrapers; Manual and non-plugin keys remain opaque.

## Deployment and live acceptance

- Pre-deployment backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r4-predeploy-20260811-033127`.
- Corrected-r4 backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r4-hotfix-predeploy-20260811-185731`.
- Deployed corrected DLL SHA-256: `2C31BD410A8A5CA1C74AD313A021CE5F5A4F5D68AE01809D5931CA99DB23B796`.
- Android-navigation backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r4-android-predeploy-20260811-192256`.
- Deployed corrected CustomCssJS configuration SHA-256: `84199C09C7C895DF5DC5FA6DB85BB49C8066E1CE1D86DC4CBADE8177F6D8428C`; it contains exactly one V15 marker and no V14 marker.
- Danmu configuration remained unchanged at SHA-256 `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`.
- Full restart replaced PID 9929 with PID 12529. Port 8096 returned HTTP 200, and the startup log loaded `Emby.Plugin.Danmu` plus all six registered scrapers without plugin-load errors.
- Standalone Season 4 exact-ID preview saw both Bilibili and Dandan IDs and selected the earlier enabled Bilibili provider without entering scored search. The runtime-materialized Series Dandan ID did not override the Season-local decision.
- Forced Season 4 and Episode-through-Season previews discovered the provider-local alias `小书痴的下克上 〜为了成为图书管理员而不择手段〜 第四季`. The live upstream candidate scored 0.825 because its returned structural/title evidence did not meet the 0.90 confidence threshold, so it correctly remained a manual decision. Deterministic regression covers the exact 0.902 year/count boundary and the 0.72 title floor.
- A forced Movie preview discovered matching Dandan and Tencent movie candidates through the shared Movie-specific alias path; no metadata was written.
- Controlled manual Season 3 download selected Dandan ID 15634 and completed 10/10 with zero partial, skipped, or failed episodes. After the first accepted persisted file, the old Season `BilibiliID` was removed, `DandanID=15634` and `DandanIDManual=15634` remained, and every Episode retained its exact `156340001..156340010` ID while IMDb/TVDB remained unchanged.
- A deliberately invalid Dandan candidate for Season 1 failed before file persistence and left `BilibiliID`, `DandanID`, and TVDB byte-for-byte unchanged. Deterministic persistence tests cover automatic import, skip, cancel, timeout, stale generation, metadata exceptions, and disabled registered providers through the same success-gated write helper.
- Series and Movie provider dictionaries were unchanged. Season 1 and Season 4 Dandan XML hashes remained byte-for-byte equal to their r3 baselines. All four seasons contain the expected chatid ranges `147270001..147270014`, `152930001..152930012`, `156340001..156340010`, and `183020001..183020016`, with positive comment counts in every XML file.
- Acceptance correction: a Season-local identifier is no longer discarded merely because its value equals the ignored Series identifier. With the live configured order `Dandan -> Bilibili`, Season 4 now resolves exact `DandanID=18302` and returns the provider title `小书痴的下克上 〜为了成为图书管理员而不择手段〜 第四季`.
- Acceptance correction: an untouched default-title rematch omits `keyword`, so the live full alias flow returns the Dandan `18302` alternate-title candidate. Editing the field changes the action to `按关键词搜索` and retains isolated explicit-keyword semantics.
- Android navigation correction: the dialog installs one same-route history guard and consumes both WebView `popstate` and native `backbutton`. A full-Series Season candidate view returns to the Series overview; a top-level view closes; a protected download view restores the guard and remains open. Deterministic frontend tests also cover listener/history cleanup and preserve X, Escape, and backdrop behavior.
- Android safe-area correction: the narrow-screen header uses `calc(1.75rem + env(safe-area-inset-top,0px))`, keeping the title and close button below an edge-to-edge status bar without changing desktop layout.
- Search-time Android-back backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r4-searchlock-predeploy-20260811-044015`.
- Search-time Android-back correction: each busy preview/rematch request independently locks WebView history and native `backbutton` navigation while leaving the top-right X available. Rendering a candidate, overview, or progress view releases the lock and restores the existing parent/top-level back behavior. The final byte-identical release frontend was loaded after Emby restarted from PID 30008 to PID 25235; the deployed XML contains nine `androidBackLocked` references and port 8096 returned HTTP 200.

Rollback: stop Emby, restore the three backed-up files to their original locations, then run the verified full-restart helper through `/bin/sh`.
