## Emby 4.9.3.0 live verification

Verified on 2026-08-10 against the Synology Emby 4.9.3.0 test instance.

### Deployment and rollback

- Deployed DLL SHA-256: `61ac0f40dd7077b8c5bbb41e5a0952c0d7f454f373687e06f053cd0f6093591e`.
- Deployed CustomCssJS configuration contains exactly one `__embyDanmuSmartMenuV7` installation flag.
- Pre-deployment DLL and CustomCssJS configuration backup:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-smart-match-20260809-171740`.
- Backup DLL SHA-256: `636675edb9eb1f97f7b215f6b14adf495e62e966a60b4a2f226e0241d425a0f2`.
- Backup CustomCssJS configuration SHA-256: `9b0e37e73f43e1ed29cfbb950a099aa535bd55f8a132dd2c8783f81f2499b19c`.
- Emby was stopped before DLL replacement, file ownership/mode were restored to
  `emby:users 0644`, and the package restarted successfully.
- Rollback is to stop Emby, restore both backed-up files with their recorded
  ownership and modes, then start Emby and refresh the web client.

### Menu and search verification

- Series card actions were present in representative television and animation
  library rows.
- Season card actions were present inside a Series detail page.
- Episode actions were present in the Season episode row menu and Episode detail
  menu.
- Movie card action was present and was injected exactly once.
- A CollectionFolder/library-root card received no smart-match action.
- Movie, whole-Series, Season, and Episode manual-search inputs started with the
  required Movie or owning-Series title.
- An edited whole-Series Season search value was submitted and produced a new
  candidate result set.
- A live Emby card layout with no item id on the card exposed a compatibility
  issue. The frontend was corrected to use the authoritative action-sheet preview
  id and to reject mismatched or unresolved identities; the corrected script was
  redeployed and retested.

### Download verification

- Movie `黑洞频率` matched across Bilibili, iQIYI, Tencent, and Youku candidates.
- A tracked Bilibili Movie download completed with one success and produced a
  valid 250,877-byte XML containing 2,176 comments.
- A second non-forced Movie submission completed with one duplicate skip and no
  failure.
- The Episode candidate UI displayed local Episode 1, suggested source Episode 1,
  and an editable selected-row source input. Editing was verified before restoring
  the correct source number for submission.
- A tracked Youku single-Episode task returned a real provider failure
  (`弹幕来源未返回有效弹幕`) and the UI reported one failure without changing it
  into a successful result.
- Provider failures during Movie search were isolated: successful provider
  candidates remained available while failed providers were logged.

### Still pending

- A safe representative STRM write, non-Bilibili successful Movie download,
  forced-refresh write, in-flight cancellation, and existing Season-task retry
  should be exercised before release publication.
## 2026-08-10 Single-target reliability verification

- Strict OpenSpec validation passed after adding single-target progress parity, 120-second deadline, force-stop close, menu ordering, and provider-hardening requirements.
- Backend regression executable and frontend DOM regression script passed; Release build completed successfully.
- Deployed DLL SHA-256: `8f196570cc76221a68ef95fd10a5fae8f6f397adfaa9cf3f3f311dbd1e4b34fe`.
- Deployed CustomCssJS configuration SHA-256: `609cc536b935b5ebf3030624d1abc6a53c7dfe2505a07112e2cc66737c349366`; exactly one V8 installation flag is present.
- Rollback backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-smart-match-20260809-181322`.
- Movie action appeared before the native Identify/Refresh/Scan group rather than at the menu end.
- iQIYI `qips://tvid=243967400;...` was parsed successfully; `谍影重重` downloaded as one Movie row and wrote a 1,595,312-byte iQIYI XML file.
- Tencent duplicate detection rendered one Movie row with Retry; forced Movie retry fetched all barrage segments and completed successfully in about one minute.
- A second Tencent retry was force-stopped while running. The item became cancelled, the top-right close control removed the overlay immediately, and the provider's late work could not change the terminal result.
- The 120-second Movie/Episode deadline is enforced by the controller's provider-task/cancellation/deadline race. The successful Tencent run completed before the deadline, so no artificial 120-second live timeout was induced.

## 2026-08-10 Android long-press and 180-second deadline verification

- Increased the Movie/Episode single-target deadline from 120 seconds to 180 seconds while retaining the provider-task/cancellation/deadline race and immutable terminal-result protection.
- Added capture-phase `pointerdown`, `touchstart`, and `contextmenu` correlation for Android long-press action sheets, plus action-sheet identity bootstrap when no desktop overflow click was observed.
- The opened action sheet's own media id remains authoritative, so a Season long-pressed inside a Series detail page targets that Season rather than the page's Series; unresolved or mismatched menus are not guessed.
- Frontend deterministic regressions passed for long-pressed Season targets, authoritative menu-id replacement, menu-only identity bootstrap, and unidentified-menu rejection.
- In the live Emby web client, a simulated long press on the Movie card `黑洞频率` injected exactly one `智能匹配并下载电影弹幕` action without clicking its overflow button first.
- In the live Series detail page for `妄想学生会`, a simulated long press on the Season card injected exactly one `智能匹配并下载本季弹幕` action and used the Season action sheet rather than the Series page id.
- Desktop detail-page overflow regression passed: the Series menu still injected exactly one `智能匹配并下载整部剧弹幕` action.
- Strict OpenSpec validation, frontend DOM regressions, backend regression executable, and Release build all passed.
- Deployed DLL SHA-256: `af577d53db934516a8787e0bb0ec9aaa5be6f1d41f172d584b4edde3be1db787`.
- Deployed CustomCssJS configuration SHA-256: `ef3b5833809044c51f63ffe1e1540bccf9bca04256b1ea18de53b65811f5b973`; exactly one V9 installation flag is present.
- Rollback backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-smart-match-20260809-184827`.

## 2.0.1-r2 release artifact

- Clean release lineage: `2.0.1-r1` plus only the complete mixed-worktree
  `extend-smart-match-menus-and-movies` implementation.
- Release DLL SHA-256:
  `03b12feba16985f7f84780b766ff463af7bdc3befc5973f57b36e9fe4b27b8e1`.
- Source archive SHA-256:
  `db878826ac2ca713c8c5e75cfae34936b7efd1d0ab94332e3d6b176fe5105920`.
- Packaged CustomCssJS SHA-256:
  `c1decff5e552c37cbbd6c54723b9c3b99eb2884a7e5a3475cfd547cf91973f80`.
- The clean Release build, C# regressions, frontend regressions, and strict
  OpenSpec validation all passed before commit.
