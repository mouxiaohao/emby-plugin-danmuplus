# 2.0.1-r1

This patch hardens XML 1.0 handling for every danmu provider without changing matching, binding, queueing, duplicate-skip, partial-download, or file naming behavior.

## Artifacts

- `Emby.Plugin.Danmu.dll`
  - SHA-256: `636675edb9eb1f97f7b215f6b14adf495e62e966a60b4a2f226e0241d425a0f2`
- `emby-plugin-danmuplus-2.0.1-r1-source.zip`
  - SHA-256: `671a61908738672d7a26a9fd7110338dd6a2a2eeca5b5d4c22a60eac1b6a7821`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256: `1cd2cfeb5d4c46b15816dc16e6831fe6df254398766c5ae568e8ba3cde094ed3`

## Verification

- Release rebuild: 0 errors; 134 pre-existing warnings.
- Regression harness: passed.
- OpenSpec strict validation: passed with OpenSpec CLI 1.8.0.

## Deployment and rollback

Before deployment, preserve the currently installed DLL and plugin configuration with timestamps, permissions, owners, and SHA-256 hashes. Do not delete or overwrite existing danmu XML during validation.

If validation fails, stop Emby, restore the timestamped DLL backup with its original ownership and permissions, restore the previous XML only if it changed, and restart Emby. This release requires no configuration or provider-binding migration.

### Dandanplay credential setup

Obtain an API ID and API Secret from the Dandanplay open platform; this project does not provision or bundle credentials. Enter both values in Emby administration → Danmu configuration and save. A complete saved pair takes precedence over `DANDAN_API_ID`/`DANDAN_API_SECRET`; incomplete pairs fail without mixing sources. The secret is masked in the settings UI but stored as plaintext in the Emby plugin configuration XML, so protect that file with Synology/Emby permissions. Credential and signature values are excluded from plugin diagnostics.

### Synology validation record

- Emby: `4.9.3.0`
- Installed DLL SHA-256: `636675edb9eb1f97f7b215f6b14adf495e62e966a60b4a2f226e0241d425a0f2`
- Backup directory: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.1-r1-20260809-104452`
- Previous DLL SHA-256: `811858125f1d922fbe24eb4a7b482079326ae7c73ccc3c5d92e2b07e16652789`
- Validation item: 唐朝诡事录 S01E35, iQIYI tvId `5915672316071600`
- Result: success; 35 recent episodes skipped, episode 35 downloaded, 0 failures
- Output: 761,465 bytes, 6,850 comments, strict XML parse passed, no U+FFFE/U+FFFF
- Output SHA-256: `76d77bfa2fcc2bbf08404c7df23e96083bfabb7a8216777c33d4507ba163e32b`
