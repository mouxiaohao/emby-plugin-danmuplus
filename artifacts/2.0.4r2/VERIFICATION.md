# 2.0.4r2 verification

Verified and deployed to the authorized Synology Emby instance on 2026-08-15.

## Release artifact

- DLL size: 1,613,824 bytes
- DLL SHA-256: `97e8e0d6baded7b1b9d4a780babbf133f9901bb0d61da392e0b1ec5b043a2065`
- Assembly/file line: `2.0.4.2`
- Informational version: `2.0.4r2`

## Automated verification

- Release build passed with zero errors and 131 existing warnings.
- TMDB alias and seven-day replay targeted regressions passed.
- Full backend regression executable passed.
- Frontend smart-match regression passed.
- `git diff --check` passed.
- Strict OpenSpec validation passed.
- Sol final review approved deployment after replay source-scope, late-provider lease,
  atomic XML replacement, and cross-language Season-scoring blockers were fixed.

## Deployment

- Rollback backup:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.4r2-predeploy-20260815-040854`
- The pre-Bilibili-fix backup is retained at
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.4r2-pre-bili-fix-20260815-0737`.
- Active DLL SHA-256 matches the release artifact.
- Active Danmu configuration SHA-256 remained
  `02519afd92022babacf9e6d516c44c0dde0117a2744593501d8cb29222536069`.
- Active CustomCssJS configuration SHA-256 is
  `0ecfd6105c49b8d512d8e0278e7affe5cb10645401a2058ceb3268755b9b8314`.
- DLL/config/CustomCssJS modes are `644`, `444`, and `444`; ownership was retained.
- Emby 4.9.5.0 restarted successfully and the public health endpoint returned HTTP 200.

## Live matching acceptance

- Bookworm Season 4 used `tmdb-alias`, selected DandanPlay `18302`, and scored
  `0.9000` for the Chinese short-title result.
- Bookworm Season 3 used `tmdb-alias`, selected DandanPlay `15634`, and scored
  `1.0000`.
- One Punch Man Season 3 selected DandanPlay `17576` at `0.8350` through the
  bounded cross-language primary-title fallback.
- One Punch Man Season 2 remained `0.5222`, Season 1 remained `0.3000`, and both
  stayed below the `0.80` automatic threshold.
- Final Bilibili typed-search acceptance completed without `partial-failure` for
  One Punch Man Season 3 and My Dress-Up Darling Season 1. Series searches stayed
  on `media_bangumi`; the Movie-only `media_ft` route retained bounded retry and
  silent fallback behavior.

## Seven-day replay acceptance

- A controlled 16-Episode Bookworm Season 4 task completed with 15
  `seven_day_recent_file` skips and one newly successful Episode.
- The origin advertised replay eligibility for exactly 15 Episodes.
- Two replay submissions returned the same child task ID.
- The child completed with 15 successes, zero skips, and zero failures.
- The previously skipped Episode 1 changed hash and timestamp.
- The current-run successful Episode 16 retained the exact pre-replay hash and
  timestamp.
- The acceptance copies of Episodes 1 and 16 and their checksums are retained
  under the rollback backup's `acceptance-replay` directory.

## Live UI and log acceptance

- The Season dialog displayed library Series/Season/year/local/mapped context.
- The removed server-authority paragraph was absent.
- After a 16-skip task settled, the enabled seven-day replay button with the
  expected 16-Episode count was present in the progress footer.
- Post-test Danmu/TMDB error scan was empty; no duplicate-write or atomic replace
  failure was observed.
- No credential values are included in this record.
