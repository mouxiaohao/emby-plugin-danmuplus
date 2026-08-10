# 2.0.1-r3

This release is based on `2.0.1-r2` and adds the complete
`add-dandan-api-proxy-mode` change. The `main` lineage therefore contains the
r1 XML-safety release, the complete r2 smart-match/Movie/Episode release, and
this r3 Dandanplay proxy release in order.

## Highlights

- Mutually exclusive proxy API and custom API choices in the Dandanplay
  configuration page.
- Cloudflare Worker-compatible CORS-prefix routing for the existing official
  `/search/anime`, `/bangumi`, and `/comment` endpoints.
- Worker-side signing in proxy mode with no local credential resolution or
  Dandanplay authentication headers.
- Existing official direct requests, local signatures, title/year/season/
  episode matching, bindings, and download pipeline retained in custom mode.
- No video hash calculation and no Dandanplay `/match` integration.

## Artifacts

- `Emby.Plugin.Danmu.dll`
  - SHA-256: `353e615afce38a5c7f6f7027af9092a7af94d04423e740faa527ca94366261a0`
- `emby-plugin-danmuplus-2.0.1-r3-source.zip`
  - SHA-256: `2a736f06de7fbacb55c268ba6f56e28734fe465ebf65dfa1dcf41b29d309aeac`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256: `c1decff5e552c37cbbd6c54723b9c3b99eb2884a7e5a3475cfd547cf91973f80`

## Verification

- Release build: passed with 0 errors and 134 existing warnings.
- C# regression executable: passed.
- Frontend deterministic regression script: passed.
- Configuration JavaScript syntax check: passed.
- Both r2 and r3 OpenSpec changes passed strict validation.
- Live proxy search, automatic/manual matching, and Episode download for
  `葬送的芙莉莲` succeeded through `https://ddplay-api.7o7o.cc/cors/`.
- Emby persisted `DandanID`; logs contained no Dandan errors or local
  credential/signature material.

A live direct signed request was not performed because the test Emby instance
does not contain a Dandanplay API ID/Secret pair. Direct-mode URL and signing
eligibility are covered by deterministic regressions.

## Rollback

The pre-proxy DLL and plugin configuration remain in
`/volume2/@appdata/EmbyServer/plugins/backups/danmu-cfproxy-20260809-213233`.
Stop Emby, restore the recorded `.before` DLL and configuration files with
ownership `emby:users` and DLL mode `0644`, then restart Emby.
