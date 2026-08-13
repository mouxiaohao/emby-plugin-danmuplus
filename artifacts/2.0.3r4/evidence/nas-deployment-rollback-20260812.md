# r4 NAS deployment and rollback evidence — 2026-08-12

## Backup

Absolute backup directory:

`/var/packages/EmbyServer/var/plugins/backups/danmu-2.0.3r4-predeploy-20260812-135933`

The backup contains the deployed r3 DLL, Danmu XML, CustomCssJS XML, raw
`library.db`/`library.db-wal`/`library.db-shm`, an online SQLite consistent
backup, and the complete `composite-seasons` directory. Its authoritative
manifest is `SHA256SUMS` in that directory.

Key SHA-256 values:

- r3 DLL: `9d95f7952bc19050b8d6f54002ea1807efa3b01303a19de0739736fb1784cf71`
- Danmu XML: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- CustomCssJS XML: `49f3f66b543c5d32fa7024cba6c9b28284454e65773756f0180aa2d9b5bf9f7c`
- raw library DB: `f3d8c1687fcfbd93885bf16df38a62a520a0862007bc60f39bde48237b8da06f`
- raw WAL: `b834aaa7d0adb4ca034b8b854a20844fd32fb0ecc31440a418b464ce8cc16a36`
- raw SHM: `1dab14bf9efe661383b7cd266d08def5f9494328fed8000d5d392e7c01e8254d`
- consistent SQLite backup: `b6e2269ca96129cd6e79c7c9e889367b0e1ea23ebd9cae44c5b1e38174c677f6`

## Attempted r4 validation

The paired V19-to-V20 updater completed atomically and Emby 4.9.3 restarted
with HTTP 200 and normal Danmu/CustomCssJS entry-point logs. No download,
binding, metadata-write, or library-refresh endpoint was invoked.

Read-only `MatchPreview` exposed a production blocker:

- One Punch Man Season 1 (`484299`) returned `invalid_request`; all seven
  placed S00 ItemIds were reported as `item-ownership-ambiguous`.
- Seitokai Yakuindomo Season 1 (`519628`) also returned `invalid_request`
  before discovery, so selected Dandan `7532` could not be previewed.
- The real Emby inventories did not provide a unique placement discriminator
  to the current target-ownership policy. The attempted Core-side
  pre-normalization therefore did not resolve the ambiguity.

## Rollback result

The paired r3 DLL, Danmu XML, and CustomCssJS XML were restored atomically from
the backup and Emby was restarted. Final observed state:

- HTTP: `200`, Emby `4.9.3.0`
- listener: EmbyServer PID `19874`, port `8096`
- deployed DLL SHA-256: `9d95f7952bc19050b8d6f54002ea1807efa3b01303a19de0739736fb1784cf71`
- deployed Danmu XML SHA-256: `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- deployed CustomCssJS XML SHA-256: `49f3f66b543c5d32fa7024cba6c9b28284454e65773756f0180aa2d9b5bf9f7c`
- markers: exactly one `__embyDanmuSmartMenuV19`
- startup log: Danmu ServiceRegistrator and CustomCssJS entry points completed;
  no plugin load error was observed.

The NAS is therefore back on the previously verified r3 paired state.
