# 2.0.2r3 Verification

## Release identity

- Baseline release: `2.0.2r2` (`3a41be8`)
- User/informational version: `2.0.2r3`
- File version: `2.0.2.3`
- Assembly version line: `2.0.2.0`
- Frontend installation marker: `V14`

## Paired artifacts

- `Emby.Plugin.Danmu.dll`: SHA-256 `AEE3854C0F5AFFD02236529DEF383A553E750C6DCC94D501B18D973BD14C071F`
- `DanmuSmartMatch.CustomCssJS.js`: SHA-256 `65117D0AF81655BC1D33FE2DB564E2320B11E756E0187A8FEE430699A27C90F9`
- The paired frontend changes only the release marker; smart-match interaction and scoring behavior are unchanged from r2.

## Reproducible local checks

```powershell
dotnet build RegressionTests\Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-restore /p:WarningLevel=0
dotnet run --project RegressionTests\Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-build
dotnet build Emby.Plugin.Danmu.csproj -c Release --no-restore /p:WarningLevel=0
node --check Frontend\DanmuSmartMatch.CustomCssJS.js
node --check Frontend\DanmuSmartMatch.RegressionTests.js
node Frontend\DanmuSmartMatch.RegressionTests.js
openspec validate fix-anime-season-smart-match --strict
.\RegressionTests\VerifyR3ReleaseScope.ps1
git diff --check
```

Result: all checks passed with 0 errors. The only build warning under suppressed warning level is the pre-existing Costura.Fody package-reference warning.

## Pre-deployment state

- Deployed r2 DLL SHA-256: `582552D182C66BA722C06607BDBFC975CDC2FEA4F8D5909046FE3E1AEFBF3C1A`
- Danmu configuration SHA-256: `DFF0C268653DB96279C5EBC666D0661F35A0C9ED37FDCB5ECFC2A60FE460640C2`
- CustomCssJS configuration SHA-256: `9F3788573999B4800585DE9EDD477A7045466DE4D90E782E326A4F029619B294`
- Correct Season 1 and Season 4 Dandan XML hashes are recorded in `XML_BASELINE.md`.

## Bilibili PGC endpoint verification

- Exact `season_id=46089` returned code 0 and 46 upstream entries; the first usable entry was `ep_id=779775`.
- Exact `ep_id=779775` returned the same ID with positive `aid=450096728` and `cid=1339446971`.
- Code and regressions enforce durable `season_id` for Season, durable `ep_id` for Movie/Episode, and transient `aid,cid` only after `GetMediaEpisode` in the download path.

## Deployment and live acceptance

- Backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r3-predeploy-20260811-164021`.
- Backup hashes matched the recorded r2 DLL and both pre-deployment configurations.
- Deployed DLL SHA-256: `AEE3854C0F5AFFD02236529DEF383A553E750C6DCC94D501B18D973BD14C071F`.
- Deployed Danmu configuration remained unchanged: `DFF0C268653DB96279C5EBC666D0661F35A0C9ED37FDCB5ECFC2A60FE460640C2`.
- Deployed CustomCssJS configuration SHA-256: `AB279B1CF5F8A47E36C6F425E34CA4B5873460F7C48839A21D4F27A93DD6B1D3`; exactly one V14 marker is present.
- Verified full restarts replaced the original stale PID 25513 and every later candidate process; the final accepted process is PID 9929. The restart helper refuses to signal anything except the exact 8096-listening Emby executable.
- Read-only Series and direct-Season previews ignored runtime-materialized Series `DandanID=18302`. Season 2, which initially had no item-local danmu identifier, entered shared scoring and discovered Dandan AnimeIds 14727, 15293, 15634, and 18302 through the bounded provider-derived alias.
- Manual forced Season 2 download selected 15293 and completed 12/12; Season 3 selected 15634 and completed 10/10. Both had zero partial, failed, or skipped episodes.
- The database now stores Season 2 `DandanIDManual=15293|DandanID=15293` and Season 3 `DandanIDManual=15634|DandanID=15634`. Every Episode stores the corresponding real sequence `152930001..152930012` or `156340001..156340010`.
- Season 1 and Season 4 Dandan XML hashes remained byte-for-byte identical to `XML_BASELINE.md`. XML verification confirmed chatids `147270001..147270014`, `152930001..152930012`, `156340001..156340010`, and `183020001..183020016`, with positive comments in every file.
- The Series row remained unchanged (`DandanID=18302|DandanIDManual=18302`); Season 1 and Season 4 metadata was not rewritten.
- Emby's `ExternalIdInfos` endpoint displayed Bilibili and Mgtv fields on a Movie, Series, Season, and Episode.
- Actual Bilibili PGC verification resolved `season_id=46089` and exact `ep_id=779775` to positive `aid=450096728,cid=1339446971`; the corresponding protobuf danmu request returned HTTP 200 and 200427 bytes. Durable-ID source and regression checks reject BVID/CID/tuples and persist only `season_id` or `ep_id` by item type.

Rollback: stop Emby, restore the three `.before` files from the backup directory to their original locations, then use `restart_emby.sh` to verify the old listener exits before restart.
