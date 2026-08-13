# 2.0.3r6 verification

Verified on 2026-08-13 from the isolated r5-derived workspace on branch
`codex/2.0.3r6-lazy-candidate-details`.

## Automated gates

- Frontend syntax and deterministic regression suite: passed.
- Backend deterministic regression suite: passed.
- r5 target-season scope fixtures, including One Punch Man, Seitokai Yakuindomo,
  whole-Series S0/null exclusion, explicit S0, and Series/direct-Season parity: passed.
- Identifier metamorphic suite: passed.
- Release solution build: passed with 0 errors; 131 pre-existing warnings.
- r6 narrow-delta/source-baseline gate: passed.
- Strict OpenSpec validation and whitespace checks: passed.

## Package

- `Emby.Plugin.Danmu.dll`: `dc437aea76f1db9b437257a9829b4ebb958815f1065102307835bffc9cf52807`
- `DanmuSmartMatch.CustomCssJS.js`: `6f1a78e04397f377c0bd50129bc83857ee8cd3a8cd9a37d4bb7138a5946f397c`
- File version: `2.0.3.6`; informational version: `2.0.3r6`.
- Frontend install marker: V22; mapping protocol remains V21.

## Deployment

- Atomic deployment completed on the live Emby server.
- Paired read-only r5 rollback backup:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r6-final-20260813-040935`
- Active DLL hash matches the package.
- Active transformed CustomCssJS configuration hash:
  `8422f462814d6173341f872b28daa4e52880c22e59d54322f16520529d566a67`
- Danmu configuration remained unchanged:
  `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- Exactly one V22 marker and zero V21 install markers are active.
- Emby restarted successfully and reports version 4.9.5.0.

See `evidence/deployment-readonly-acceptance-20260813.md` for the live UI checks.
