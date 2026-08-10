# Verification Record

## Deterministic checks

- `dotnet restore Emby.Plugin.Danmu.sln`: passed.
- `dotnet build Emby.Plugin.Danmu.sln -c Release --no-restore`: passed with 0 errors.
- `dotnet run --project RegressionTests/Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-restore`: passed.
- `node Frontend/DanmuSmartMatch.RegressionTests.js`: passed.
- `node --check Configuration/config.js`: passed.
- `openspec validate add-dandan-api-proxy-mode --strict`: passed.

The routing regressions cover legacy defaults, proxy-prefix normalization and
sanitized rejection, exact search/bangumi/comment URLs and queries, direct-mode
authentication eligibility, proxy credential independence, configuration page
round-tripping, and provider failure isolation. The existing regression suite
also covers manual bindings, STRM episode handling, retry/partial XML behavior,
and deterministic descending-score selection.

## Live proxy and Emby checks

- Worker prefix: supplied out of band for deployment verification and intentionally omitted from repository artifacts.
- A read-only proxied `/api/v2/search/anime` request for `葬送的芙莉莲`
  returned HTTP 200 and Dandanplay anime ID `17617`.
- Emby 4.9.3.0 loaded the legacy configuration as custom mode, then persisted
  proxy mode and the normalized Worker prefix across a restart.
- Series, season, and episode previews for `葬送的芙莉莲` returned Dandanplay
  candidates. Both automatic and manual episode matching resolved episode 1.
- A forced manual download for episode 1 using `DandanID=17617` completed with
  1 success and 0 failures. The Emby item subsequently contained a `DandanID`
  provider value.
- Other enabled providers continued returning candidates. The latest Emby log
  contained no Dandanplay error lines and no `X-AppId`, `X-Signature`,
  `ApiSecret`, or `API Secret` text.

The Emby media path is a library mapping that is not resolvable from the
Synology SSH namespace, so the generated XML could not be independently opened
through SSH. The completed download result and persisted provider ID were
verified through Emby's plugin and item APIs.

## Direct-mode limitation

The deployed legacy configuration contains no Dandanplay API ID/secret pair.
Custom mode migration and deterministic signing behavior are verified, but a
live signed Dandanplay search cannot be completed without a valid credential
pair. Proxy mode does not require those credentials.

## Deployment and rollback

- Installed DLL SHA-256:
  `880537df21380e641c8840e7ed7e5f3446235cf72acfa48f7d2880624ed8ce89`.
- Backup directory:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-cfproxy-20260809-213233`.
- Previous DLL:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-cfproxy-20260809-213233/Emby.Plugin.Danmu.dll.before`.
- Previous DLL SHA-256:
  `af577d53db934516a8787e0bb0ec9aaa5be6f1d41f172d584b4edde3be1db787`.
- Previous configuration:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-cfproxy-20260809-213233/Emby.Plugin.Danmu.xml.before`.
- Previous configuration SHA-256:
  `6dea46df5bf6b05e345351bcc835ae1c86a99460de369ab97967661ffc76cd5d`.

Rollback: stop Emby, restore the two `.before` files to their original plugin
and configuration paths, set the DLL ownership to `emby:users` and mode to
`0644`, then start Emby. The backups and hashes were rechecked after deployment;
no rollback was needed.

## 2.0.1-r3 release artifact

- Release lineage: `2.0.1-r1` → `2.0.1-r2` smart-match/Movie/Episode change →
  `2.0.1-r3` Dandanplay CF Worker proxy change.
- Formal r3 DLL SHA-256:
  `353e615afce38a5c7f6f7027af9092a7af94d04423e740faa527ca94366261a0`.
- Formal r3 source archive SHA-256:
  `2a736f06de7fbacb55c268ba6f56e28734fe465ebf65dfa1dcf41b29d309aeac`.
- Packaged CustomCssJS SHA-256:
  `c1decff5e552c37cbbd6c54723b9c3b99eb2884a7e5a3475cfd547cf91973f80`.
- The earlier live deployment hash records the functionally equivalent mixed
  validation build. The formal r3 artifact was rebuilt from the clean r1 → r2
  → r3 commit sequence and passed the complete deterministic validation suite.
