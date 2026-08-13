# Live fixture attempt for tasks 8.5 and 8.6 (blocked)

Date: 2026-08-12

## Safety boundary and backup

- Disposable virtual-folder name: `DanmuPlus 2.0.3r4 Live Fixture`.
- Disposable root: `/volume1/__DanmuPlusFixture_2.0.3r4__`.
- Disposable staging: `/volume1/__DanmuPlusFixture_2.0.3r4_stage__`.
- Before creation, both paths were absent. `/volume1` resolved to itself and was
  not a symbolic link. Every production media root was below `/volume1/NAS`, so
  neither disposable path was an ancestor or descendant of a production root.
- Full pre-fixture backup:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r4-live-fixture-20260812-150638`.
  It contains the active DLL and both plugin XML files, raw DB/WAL/SHM, an online
  consistent SQLite backup, server configuration, composite state, and a
  58-file `SHA256SUMS` manifest. The consistent DB backup SHA-256 is
  `ea3079c4283ff95703e0cc1ba7b6df792cb4aaa51db0bdbcf168f9a7256fa998`.

## Fixture creation and blocker

- The isolated root was populated with 25 `.strm` files and 27 NFO files:
  twelve ordinary episodes in each of two test series and one S00 special in the
  placed-special series. No production media file was changed.
- Emby accepted creation of the isolated TV virtual folder as ItemId `521358`.
- A bounded initial scan and one bounded item-specific refresh were attempted.
  After the final 60-second window the virtual folder still contained zero
  Series, zero Season, and zero Episode items.
- The item-specific refresh also reported that this Emby 4.9.3 instance did not
  accept the requested `ImageRefreshMode=None` value.
- Because Emby never created a disposable Season, no download, retry, ProviderId
  update, metadata mutation, restart reconstruction, or membership assertion was
  attempted. Task 8.5 therefore remains incomplete and must not be checked.

## Rollback dry-run and cleanup

- The r3 trio was copied only into the disposable staging directory and verified
  without replacing active files:
  - DLL: `9d95f7952bc19050b8d6f54002ea1807efa3b01303a19de0739736fb1784cf71`
  - Danmu XML: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
  - CustomCssJS XML: `49f3f66b543c5d32fa7024cba6c9b28284454e65773756f0180aa2d9b5bf9f7c`
- The virtual folder was removed through the Emby 4.9.3 API. It no longer
  appeared in `Library/VirtualFolders`, and ItemId `521358` returned HTTP 404.
- Immediately before deletion, both disposable paths resolved exactly to their
  literal absolute paths, were directories, were not symbolic links, and were
  rejected if equal to or below `/volume1/NAS`.
- Only the two exact disposable paths were recursively removed. Both are absent.
- No fixture SeasonId was ever allocated, so no marker was eligible for deletion.
  The two pre-existing composite marker files and their SHA-256 values remained
  unchanged (`f5a0cd10...b10428` and `bf92a10d...75e75`).
- Task 8.6 is only partially demonstrated (trio dry-run and safe cleanup); it
  remains unchecked because the task's required successful fixture workflow was
  blocked upstream.

## Final live state

- HTTP: 200.
- Plugin startup scan: no Danmu load/assembly error in the latest log tail.
- Active DLL SHA-256: `e933fe9734cc000ebaa177058398d8301c70a06394fd5195a1f884071b5f97be`.
- Active Danmu XML SHA-256: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`.
- Active CustomCssJS XML SHA-256: `f8f6dd7876dec44d41f7c7b0764ad6be5cf84e0d263f16c76c46701ac5e1cf09`.
- No product binary/configuration was replaced during this attempt; current r4
  remained active throughout.
