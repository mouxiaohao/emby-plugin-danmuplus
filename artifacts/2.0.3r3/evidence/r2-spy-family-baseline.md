# 2.0.3r2 deployment and Spy x Family baseline

Captured read-only on Emby 4.9.3.0 before r3 implementation. No binding,
download, metadata write, restart, or NAS file mutation was performed.

## Frozen source and paired r2 artifacts

- Source commit: `48fdaa986b5c10eca73bb692e0fe63ef123c2935`
- Source tree: `1996849f6b8132af9cc0747f33af4279ec8ab210`
- DLL: `617D4491D9B5726EA04B9571CC1B53EA9EA7D3AB7A3BD235A9A9002EDB493912`
- CustomCssJS artifact: `2EC9A174638444370374C192660F099C60B522D333D62D8BA7F35613AF98F174`
- Atomic updater: `5B7A50BF9155C9D3CC3F53FB924FFE975971868BB7EE1503911C7F4FD7AA2B22`
- Restart helper: `BE2465BDA563693A7E7D6397C8060E59EB0B254D318142063D6941E9648D6838`

## Installed r2 pair

- Danmu DLL: `617D4491D9B5726EA04B9571CC1B53EA9EA7D3AB7A3BD235A9A9002EDB493912`
- Danmu XML: `A3BE897F9FB84FA19CBA5B226CAC0B5E2F942A5B2117A5379CCA851CA407C973`
- CustomCssJS XML: `00D056C30D0B406551222524AEDC0A9F7107BD2B2B58F7D0872DEF648F11ED3D`
- CustomCssJS plugin DLL (not replaced): `19934C5906FEFE4C925D69BC9F1D48F7CD188E417C2F682D1C46D4CC368ABE1`
- XML-decoded LF script: 152020 bytes,
  `8B6989C39C8BAFAC0806FA3CAB3E2D43BD03B8483F365C72A75D6D66B1185CA8`
- Frontend markers: V18=1, V17=0.

## Library inventory

- Series `间谍过家家`: API ItemId `453808`; Tmdb `120089`, Imdb
  `tt13706018`, Tvdb `405920`.
- Season 1: API `453907`, preview
  `1ca7e0f7-896c-780b-ef4f-c70e3e15e044`, 25 Episodes, Tvdb `1938867`.
- Season 2: API `453809`, preview
  `a837b170-d280-fbcb-0cd1-e62dcbe2b927`, 12 Episodes, Tvdb `2083661`.
- Season 3: API `503419`, preview
  `17da1a79-0b14-6e9d-b783-034b00b28400`, 10 existing Episodes, Tvdb
  `2137972`. Existing indices are 1-6 and 10-13; 7-9 are absent.
- All 47 Episodes have only IMDb/Tvdb identifiers and no plugin-owned id.

Enabled provider order: Dandan, Bilibili, Youku, Iqiyi, Tencent, Mgtv.

## Reproduced r2 failure

The first Series MatchPreview took 24299 ms. Top-level response incorrectly
reported `no_match` and “all seasons completed”; every child Season actually
reported `incomplete`, `search-incomplete`, no automatic selection, and 60
candidates. Mgtv returned HTTP 403 for both terms on each Season. The terms
were the parent identity `间谍过家家` plus the unsafe bare `第 1 季`, `第 2 季`, or
`第 3 季`.

Candidate distribution and obvious Youku noise:

| Season | Provider counts | Obviously unrelated Youku |
| --- | --- | --- |
| S1 | Youku 37, Iqiyi 6, Tencent 7, Dandan 5, Bilibili 5 | 33 |
| S2 | Dandan 9, Bilibili 9, Youku 17, Iqiyi 8, Tencent 17 | 13 |
| S3 | Dandan 6, Bilibili 9, Youku 20, Iqiyi 8, Tencent 17 | 16 |

Examples include `VIP宠物 第二季`, `史努比秀 第二季`, `甜心格格 第三季`, and
`老友记 第一季`. The returned array was not consistently grouped by configured
provider order.

