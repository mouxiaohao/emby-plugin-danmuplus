# 2.0.1-r4

This maintenance release removes the concrete shared test Worker address from
the repository, documentation, verification records, and packaged source.

## Behavior

- New installations remain in custom/direct Dandanplay API mode by default.
- `UseProxyApi` defaults to `false`.
- `ProxyCorsUrl` defaults to an empty string.
- The settings page does not prefill or embed any public proxy URL.
- Administrators can still enter their own trusted `cf_worker.js`-compatible
  CORS prefix when explicitly selecting proxy mode.

## Artifacts

- `Emby.Plugin.Danmu.dll`
  - SHA-256: `6839e21f8def5ab1227143fea7d08d6be4331641f03302eb76a73d9f04d0faa5`
- `emby-plugin-danmuplus-2.0.1-r4-source.zip`
  - SHA-256: `1adafd9211660f42b2ddcc7189b58365bc3b609c344fc807fe4c44a49262cc20`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256: `6ee79653b903288b1f1dcfa98ddc133be301a1806060ffd6b0829fe088d2e484`

## Verification

- Release build passed with 0 errors.
- C# regression executable passed.
- Frontend deterministic regression and configuration syntax checks passed.
- Both active OpenSpec changes passed strict validation.
- Text sources, packaged source, DLL, and frontend artifact were scanned for
  the removed concrete Worker host.
