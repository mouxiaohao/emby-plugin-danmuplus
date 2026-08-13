# Deployment and read-only acceptance — 2026-08-13

The live checks below used match preview and candidate-detail operations only.
No binding, save-selection, tracked-download, or download action was started.

## Seitokai Yakuindomo whole-Series flow

- Only normal Seasons 1 and 2 appeared as whole-Series targets; S0 was not rendered.
- Season 1: 21 displayed, 13 eligible, 8 S00 Episodes explicitly ignored.
- Season 2: 23 displayed, 13 eligible, 10 S00 Episodes explicitly ignored.
- Ignored Episodes were read-only and did not create additional candidate groups.
- The existing `查看逐集映射（13 集）` view remained present. Expanding Season 1
  showed all local S01E01–S01E13 to source Episode 1–13 rows.

## Season candidate details

- The manual candidate list initially showed 11 `解析并查看详情` controls and no
  expanded detail rows.
- Clicking the second candidate expanded exactly that candidate into 13 numbered,
  titled source-Episode rows; the other 10 candidates remained unparsed/collapsed.
- The compact footer contained exactly one checkbox labelled `强制刷新`.

## Episode flow

- Initial exact-identifier match for library Episode 519633 displayed the original
  `解析所选候选的来源剧集` flow and zero per-row `解析并查看详情` controls.
- It displayed a presentation-safe success reason and no scope/item/internal IDs.
- After explicit `重新智能匹配`, 11 manual candidates displayed lazy-detail controls.
- Clicking one candidate expanded only that candidate. The library context remained
  visible with Series, Season, Episode number, and local Episode title.
- The compact force-refresh checkbox remained present and singular.

## Explicit S0 and One Punch Man

- Directly opening Seitokai Yakuindomo `特别篇` produced 18 displayed and 18 eligible
  Episodes, proving the explicit S0 scope uses its own inventory rather than borrowing
  Episodes from a normal Season.
- One Punch Man whole-Series preview rendered only Seasons 1–3 as targets. S00 Episodes
  were explicitly ignored (7 for Season 1 and 6 for Season 2), while each normal Season
  retained its 12-Episode mapping-detail view.

## Post-deployment health

- Active DLL, CustomCssJS, and Danmu configuration hashes matched the deployment record.
- Active frontend marker count: V22 = 1, V21 = 0.
- Emby public system information returned successfully with version 4.9.5.0.
- The live UI checks did not invoke a tracked-download action.

## Rollback

Stop Emby, restore the three paired files from
`/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r6-final-20260813-040935`,
preserve their recorded ownership/modes, and restart Emby. The backup directory contains
`SHA256SUMS` for verification before restoration.
