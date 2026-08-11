# 2.0.2r1 Verification

## Provenance

- Exact r6 source tree: `32fa2af0e97e12cdb5837b5874991d65137bf078`
- Isolated release branch: `codex/release-2.0.2-r1`
- User version: `2.0.2r1`
- Assembly/File/Informational versions: `2.0.2.0` / `2.0.2.1` / `2.0.2r1`
- Frontend installation marker: V12

## Build and deterministic checks

The following commands completed successfully from the isolated worktree:

```powershell
node --check Frontend\DanmuSmartMatch.CustomCssJS.js
node Frontend\DanmuSmartMatch.RegressionTests.js
dotnet run --project RegressionTests\Emby.Plugin.Danmu.RegressionTests.csproj -c Release --no-restore
dotnet build Emby.Plugin.Danmu.csproj -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File RegressionTests\VerifyR6ReleaseScope.ps1
openspec validate release-2-0-2-r1-smart-match-dialog --strict
git diff --check
```

The Release build completed with 0 errors and 134 pre-existing warnings. The scope gate compares the complete worktree against the exact r6 tree and was also proven to reject an unexpected probe file.

## Paired package hashes

- `Emby.Plugin.Danmu.dll`: `b95a07d87441d7aacffd4ee31732bfaffce05b93d4ff37bcd3b8bb9534369b9d`
- `DanmuSmartMatch.CustomCssJS.js`: `452fc38e354d5cc89fc4fcecf7e4581da9e64cc5a6d078f4a107f09b5fcd4f6b`

The packaged files exactly match the verified Release DLL and frontend source.

## Deployment and rollback

- r6 backup: `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.2r1-20260811-110730`
- Backed-up r6 DLL SHA-256: `5bcbd892832a1e1018197452d7300a243f53bc094b80fba267614dda76ee60c1`
- Backed-up Danmu configuration SHA-256: `dff0c268653db96279c5eb666d0661f35a0c9ed37fdcb5ecfc2a60fe460640c2`
- Backed-up CustomCssJS configuration SHA-256: `41c04293cbf9fa113180f793027466b04e0d7119ac4c8a2eb4708f72f00977df`
- Deployed DLL SHA-256: `b95a07d87441d7aacffd4ee31732bfaffce05b93d4ff37bcd3b8bb9534369b9d`
- Deployed Danmu configuration SHA-256: `dff0c268653db96279c5eb666d0661f35a0c9ed37fdcb5ecfc2a60fe460640c2`
- Deployed CustomCssJS configuration SHA-256: `bf4ff586c46730517c2a29cc527b6894644eaeae9ef50c8a7dc401dc8a2f0ead`

The Emby process was fully restarted after deployment. The log records successful Danmu service registration and the public server endpoint reports Emby 4.9.3.0.

## Live acceptance

- A read-only Series `MatchPreview` returned `matched`, one Season, 60 candidates, origin `scored`, and decision `confident-site-priority`.
- The rendered UI translated those values to `来源：智能评分匹配` and `决策：按站点优先级自动选择`.
- Clicking the backdrop left the dialog open.
- Candidate, overview, and terminal-progress footers do not expose a third ordinary cancel/close action.
- Escape closed the dialog.
- Reopening the dialog and clicking the top-right close button closed it.
- No tracked download, binding, or metadata-write endpoint was called.
- Unknown-code fallback, protected-state close rejection, stacked-dialog behavior, and Movie/Series/Season/Episode contract preservation are covered by deterministic frontend/backend regressions because producing those states live would require synthetic server responses or starting a download.
