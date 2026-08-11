# 2.0.2r2 Verification

## Release identity

- Baseline commit: `bd748e7`
- User/informational version: `2.0.2r2`
- File version: `2.0.2.2`
- Assembly version line: `2.0.2.0`
- Frontend installation marker: `V13`

## Paired artifacts

- `Emby.Plugin.Danmu.dll`
  - SHA-256: `582552D182C66BA722C06607BDBFC975CDC2FEA4F8D5909046FE3E1AEFBF3C1A`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256: `6C1D0C9A2F9AEF06CFF4F57810A5B5E68A1A264E6A89CC5C2DEFB51B6DECD2D6`

## Reproducible local checks

Run from the repository root:

```powershell
dotnet run --project RegressionTests\Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-restore
dotnet build Emby.Plugin.Danmu.sln -c Release --no-restore -v:q
node --check Frontend\DanmuSmartMatch.CustomCssJS.js
node --check Frontend\DanmuSmartMatch.RegressionTests.js
node Frontend\DanmuSmartMatch.RegressionTests.js
openspec validate release-2-0-2-r2-provider-id-metadata --strict
.\RegressionTests\VerifyR2ReleaseScope.ps1
git diff --check
```

Result on 2026-08-11: all checks passed. The Release build completed with 0 errors and 134 pre-existing warnings.

## Deployment and rollback

- Target: Synology Emby Server 4.9.3.0.
- Pre-deployment backup:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r2-predeploy-20260811-124545`
- Backup DLL SHA-256:
  `B95A07D87441D7AACFFD4EE31732BFAFFCE05B93D4FF37BCD3B8BB9534369B9D`
- Backup Danmu configuration SHA-256:
  `DFF0C268653DB96279C5EBC666D0661F35A0C9ED37FDCB5ECFC2A60FE460640C2`
- Backup CustomCssJS configuration SHA-256:
  `BF4FF586C46730517C2A29CC527B6894644EAEAE9EF50C8A7DC401DC8A2F0EAD`
- Deployed DLL SHA-256:
  `582552D182C66BA722C06607BDBFC975CDC2FEA4F8D5909046FE3E1AEFBF3C1A`
- Deployed Danmu configuration remained unchanged:
  `DFF0C268653DB96279C5EBC666D0661F35A0C9ED37FDCB5ECFC2A60FE460640C2`
- Deployed V13 CustomCssJS configuration SHA-256:
  `9F3788573999B4800585DE9EDD477A7045466DE4D90E782E326A4F029619B294`
- Emby was fully restarted after validating the exact listener process executable.

## Live read-only acceptance

All acceptance requests used only `MatchPreview`; no bind or download endpoint was called.

Default provider-ID previews:

| Item | Result | Provider | Upstream title | Year | Count | Category |
| --- | --- | --- | --- | ---: | ---: | --- |
| Season | `matched / provider-id` | Bilibili | 葬送的芙莉莲 | 2023 | 28 | unknown |
| Series first Season | `matched / provider-id` | Bilibili | 葬送的芙莉莲 | 2023 | 28 | unknown |
| Episode | `matched / provider-id` | Bilibili | 葬送的芙莉莲 | 2023 | 28 | unknown |
| Movie | `matched / provider-id` | iQiyi | 火影忍者剧场版：忍者之路（普通话） | unknown | 1 | 电影 |

The exact-ID request log delta contained identifier-detail `GetMedia` calls and zero `SearchForApi`, `SearchAsync`, `DanmuMatchSearchEngine`, `StartTrackedDownload`, `BindMatch`, `SaveDanmu`, or `ForceSaveProviderId` entries.

Explicit rematch (`mode=rematch`, `rematch=true`, `force=true`) returned `matched / scored / confident-site-priority` with 60 candidates. Its log delta contained provider searches and `DanmuMatchSearchEngine`, while download, binding, XML save, and ProviderId write markers remained zero.
