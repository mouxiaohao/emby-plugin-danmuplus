# 2.0.3r9 verification

Verified and deployed on 2026-08-13.

## Release artifact

- `Emby.Plugin.Danmu.dll`
- Size: 1,512,960 bytes
- SHA-256: `7cac270b68de84c34233880bdd08103ba2a9c5bfcc70d509d0c32a5646f98308`
- Assembly version: `2.0.3.0`
- File version: `2.0.3.9`
- Product version: `2.0.3r9`

## Automated verification

- Main plugin regression project passed.
- Search-term, temporary-range, bounded-search, episode-selection, r3 search-quality,
  r4 parent-season, r4 identifier metamorphic, r5 target-season, and MGTV search
  regression projects passed.
- Frontend smart-match regression passed.
- `VerifyR203R9Scope.ps1` passed, froze provider request/encoding sources, and
  proved that only `IsEligibleSeasonCandidate` changed within the scorer.
- OpenSpec strict validation passed.
- Release build completed with zero errors. Existing compiler warnings remain.
- The NAS `/bin/sh` syntax check passed for `deploy_r9.sh`; its failure path
  stops Emby, atomically restores and hash-checks r8, then restarts and waits.

## Live MatchPreview verification

The output-dialog custom Season search was exercised against the deployed Emby
instance for One Punch Man Season 3. No candidate was bound and no danmaku was
downloaded.

| Query | DandanPlay results | Third season |
| --- | ---: | --- |
| `one punch` | 4 | `17576: 一拳超人 第三季` |
| `one punch man` | 4 | `17576: 一拳超人 第三季` |
| `one+punch` (literal plus) | 4 | `17576: 一拳超人 第三季` |
| `一拳超人` | 3 | Not returned upstream |
| `一拳 超人` | 3 | Not returned upstream |
| `一拳+超人` (literal plus) | 3 | Not returned upstream |

The English alias results also contained Season 1, Season 2, and OAD. The three
Chinese forms consistently returned only those three records; this matches the
verified DandanPlay upstream keyword behavior.

### Enabled-provider punctuation audit

All six enabled providers (`DandanID`, `BilibiliID`, `YoukuID`, `IqiyiID`,
`TencentID`, and `MgtvID`) completed without timeout, cancellation, or request
failure for both internal-space and literal-plus requests. Result counts were:

| Query | DandanPlay | Youku | iQIYI | Bilibili / Tencent / MGTV |
| --- | ---: | ---: | ---: | ---: |
| `one punch` | 4 | 3 | 0 | 0 |
| `one+punch` | 4 | 2 | 0 | 0 |
| `一拳 超人` | 3 | 3 | 1 | 0 |
| `一拳+超人` | 3 | 3 | 1 | 0 |

The equal Chinese space/plus counts and successful completion diagnostics show
that literal `+` is not being rejected or turned into a transport error. English
space/plus counts differ only at Youku (3 versus 2), reflecting provider search
ranking/recall rather than plugin-side candidate loss: both forms return usable
results, including the third season. Providers with zero results completed
normally, so no encoding exception was observed for their request paths.

An automatic preview with no custom keyword retained the strict metadata-title
eligibility path. Its candidates were identity-bearing One Punch Man titles;
the deterministic regression additionally proves an unrelated title is rejected.

## Deployment and rollback

- Pre-deployment active r8 SHA-256 was
  `0199880314a30675c7f3ca17ae72b324e735f2d7cd924ed9c22dc5f4720335ce`.
- The active r9 DLL SHA-256 matches the packaged artifact.
- Emby restarted successfully and served MatchPreview requests from the new DLL.
- CSS and plugin configuration hashes remained unchanged.
- Read-only rollback backup:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r8-before-r9-20260813-implementation`
