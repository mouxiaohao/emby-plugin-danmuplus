# 2.0.1-r6 candidate verification

## Candidate files

| File | SHA-256 |
| --- | --- |
| `Emby.Plugin.Danmu.dll` | `5bcbd892832a1e1018197452d7300a243f53bc094b80fba267614dda76ee60c1` |
| `DanmuSmartMatch.CustomCssJS.js` | `7ded54097e1f5adf7d3b44ff85ae65211e47b4702dca9d330b81ff91966ffc67` |

The DLL was copied from `bin/Release/netstandard2.0` after the final Release
build. The browser asset was copied from the validated frontend source. Their
hashes were rechecked after packaging.

## Local verification

- Frontend syntax check: passed.
- Frontend deterministic regression suite: passed.
- C# deterministic regression executable: passed.
- Release build: passed with 0 errors; existing warnings remain.
- Strict OpenSpec validation: passed for
  `extend-smart-match-menus-and-movies`.
- `git diff --check`: passed.
- Sensitive-literal scan of the candidate diff: passed.

## Remote backup and rollback

- Pre-deployment snapshot:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.1-r6-20260810-131841`.
- Snapshot SHA-256:
  - DLL: `b08186751fec8a407d1ae8ffb9975a952f25f8960b7143dc0bf159d012515d5c`
  - Danmu configuration: `dff0c268653db96279c5eb666d0661f35a0c9ed37fdcb5ecfc2a60fe460640c2`
  - CustomCssJS configuration: `1273777b4eb868d0e74e5da9f3ed0608ce5c9c34a19fcfda12f247d446f3075f`
- Rollback: stop Emby, restore the three files from the snapshot while
  retaining their recorded owner and mode, start Emby, and verify the three
  snapshot checksums.

## Deployment and live verification

- The packaged pair was deployed to the authorized Synology Emby 4.9.3.0
  instance. Emby was stopped before replacement and restarted afterward.
- The old Emby PID exited. The replacement process started after the deployed
  files' timestamps, and the public system-info endpoint returned HTTP 200.
- Deployed DLL SHA-256 matched the packaged candidate:
  `5bcbd892832a1e1018197452d7300a243f53bc094b80fba267614dda76ee60c1`.
- Deployed CustomCssJS configuration SHA-256 was
  `41c04293cbf9fa113180f793027466b04e0d7119ac4c8a2eb4708f72f00977df`.
  It retained two JavaScript entries, retained the other entry's exact decoded
  content hash, contained exactly one V10 marker, and contained no V9 marker.
- The Danmu plugin configuration was not changed; its checksum remained
  `dff0c268653db96279c5eb666d0661f35a0c9ed37fdcb5ecfc2a60fe460640c2`.
- Emby loaded `Emby.Plugin.Danmu` and all six configured scraper types from the
  deployed DLL without a plugin-load failure.
- Default previews for representative Movie, Series, Season, and Episode items
  all returned `matched`, `provider-id`, and `AutoSelected=true` from their
  enabled local external identifiers.
- A forced Season rematch bypassed the local identifier, searched multiple
  enabled providers, and selected configured-priority Dandan at score `0.9679`
  over later Bilibili at `1.0000`, proving the r6 0.90/site-order rule live.
- In the Emby web client, the Movie menu action opened the r6 dialog with
  `匹配成功`, `来源：本地外部标识符`, and the right-side `重新智能匹配`
  action. Activating it entered the all-site search view without downloading or
  changing metadata.
- A scored single-Episode Dandan download completed successfully, wrote a
  non-empty 555,784-byte XML, and then added only the returned Episode-level
  `DandanID=179190001`.
- An intentionally invalid Youku candidate returned `failed`; `YoukuID`
  remained absent and the successful Dandan identifier was preserved.
- A direct Episode `BilibiliID` download completed successfully for STRM media,
  wrote a non-empty 1,079,428-byte XML, and left the complete ProviderIds map
  unchanged, confirming idempotent provider-id-origin behavior.
- Automatic-import parity is covered by the deterministic backend regressions
  and shared orchestration call path. No synthetic library item was added to the
  user's production library solely to fire an Add event.
- Expected isolated provider diagnostics were observed for an upstream Mgtv
  HTTP 403 during all-site rematch. No Danmu download or metadata-persistence
  error was recorded for the successful live cases.
