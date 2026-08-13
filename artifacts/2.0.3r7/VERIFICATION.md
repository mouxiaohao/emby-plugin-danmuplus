# 2.0.3r7 verification

Verified on 2026-08-13 against the recorded 2.0.3r6 sibling baseline.

## Automated gates

- Frontend syntax and `DanmuSmartMatch.RegressionTests.js`: passed.
- Main deterministic backend regression suite: passed.
- r5 target-season scope suite: passed, including Series S0/null exclusion, explicit S0, foreign/unknown parent filtering, One Punch Man, Seitokai Yakuindomo, direct-Season parity, and fingerprint invariants.
- r4 identifier metamorphic suite: passed for nine identifier sets.
- Release build: passed with 0 errors and 131 pre-existing warnings.
- r7 narrow-delta and normalized method-region hash gate: passed.
- Strict OpenSpec validation and `git diff --check`: passed.
- Sol final review after the rematch rollback fix: no P0, P1, or P2 findings.

## Package

- DLL SHA256: `7755c242bf6f68d38b4c062b8a542571dc66a33b578b706e4c4ba3c32c2a2c72`
- Frontend SHA256: `af10dffd6605a24ad19da777424e4dbc3afd12a17739f210bd3d96d065466feb`
- File/informational/config version: `2.0.3.7` / `2.0.3r7` / `2.0.3r7`
- Frontend marker: exactly one `__embyDanmuSmartMenuV23`
- Mapping protocol remains `21`.

## Deployment

- Target: Synology Emby Server 4.9.5.0.
- Active DLL SHA256 matches the package.
- Active transformed CustomCssJS XML SHA256: `abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb`.
- Active Danmu configuration SHA256 remained `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`.
- Permissions: DLL `emby:users 644`; CustomCssJS `emby:users 444`; Danmu config `emby:emby 444`.
- V23 marker count is 1; V22 count is 0; health endpoint and plugin startup entry point succeeded.

Live acceptance and rollback evidence are recorded under `evidence/`.
