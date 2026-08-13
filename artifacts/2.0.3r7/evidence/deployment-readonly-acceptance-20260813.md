# r7 deployment and read-only acceptance — 2026-08-13

## Deployment evidence

- Atomic deployment completed after verifying the active r6 DLL, CustomCssJS XML, and Danmu configuration hashes.
- Paired r6 backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r7-final-20260813-final-123901`.
- Backup `SHA256SUMS` was verified with all three files reporting `OK`.
- Emby restarted successfully and `/emby/System/Info/Public` reported version 4.9.5.0.
- No tracked download was started during acceptance; the Danmu configuration hash remained unchanged.

## Live UI acceptance

Fixture: Series `妄想学生会` and its direct Season 1 entry.

- Whole-Series initial request showed the loading message and cancel action, with zero visible `强制刷新` controls.
- Result scope reported 13 eligible episodes and eight S00 episodes as read-only ignored; no S00 temporary-season card was created.
- Expanded mapping contained all 13 rows with both library Episode titles and server Episode titles.
- Whole-Series mapped temporary-season rematch opened `手动匹配未匹配临时季`.
- Direct Season mapped temporary-season rematch opened the same title and candidate presentation.
- Each of 11 live candidates displayed `匹配分` exactly once and exposed one `解析并查看详情` button.
- Before detail click there was no expanded detail panel. Clicking the first candidate produced exactly one detail panel; the other ten candidates remained unresolved.
- Returning from both Whole-Series and direct Season rematch restored the original exact mapping and original download entry. No apply or download button was used.
- `强制刷新` was present exactly on actionable result/menu pages; its value was not reset by request transitions.

Provider diagnostic note: the live fixture continued to report the existing Mango TV HTTP 403 search diagnostic. Other providers completed and this did not affect the r7 acceptance criteria.

## Verified rollback procedure

The paired backup is complete and read-only. If rollback is required:

1. Stop `pkgctl-EmbyServer`.
2. Copy the backup's `Emby.Plugin.Danmu.dll`, `Emby.CustomCssJS.xml`, and `Emby.Plugin.Danmu.xml` back to the active plugin/configuration paths as one paired set.
3. Restore their recorded ownership and modes.
4. Start `pkgctl-EmbyServer` and verify the public health endpoint.
5. Verify the restored hashes against the backup's `SHA256SUMS`; the expected frontend marker is V22.

The backup hashes and restart helper shell syntax were checked after deployment; rollback itself was intentionally not executed against the healthy r7 service.
