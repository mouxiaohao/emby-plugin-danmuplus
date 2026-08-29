# 2.0.7r3 Verification

## Baseline and scope

- Exact baseline: `9b8b7465f2487c7fcf9cc1f0d972676f946aa46c` (local reviewed 2.0.7r2 commit, parent `2f351cd6f0a08ab707d1d87d53935d6c53c723e0`).
- Development branch: `codex/release-2.0.7r3-tv-remote-navigation` in an isolated worktree.
- Scope: dialog-local television remote navigation and focus continuity, full-card candidate focus proxies, X-button body-top alignment, composite temporary-season suffix rematching, deterministic regressions, V36 identity, cumulative documentation, reviewed package, and paired Synology deployment.
- Candidate ordering, API/DTO, provider, recursion/S00, download, binding, identifier, metadata, and V22 mapping protocol remain unchanged. Composite rematch changes only the browser draft by pruning the clicked temporary group and every later temporary group back into the unmatched suffix; ordinary remove remains exact-group only.

## Reviewed payloads

- `Emby.Plugin.Danmu.dll`: 1,748,480 bytes; SHA-256 `3944A015B1A085E32A9DC6C9D4F99E9DF6EED8C7C3E4C52B09887C47B81DD8D8`.
- `DanmuSmartMatch.CustomCssJS.js`: 277,161 bytes; SHA-256 `3EAD13CBA0BA2319CCEE4F1E175BCDEAB01E458CAE2BBC99DF78AEB70B6772C3`.
- `UPDATE.md`: 33,209 bytes; SHA-256 `025CCA876AC41C1230810704F470CA7770D7238F7C7044D5239148D10052B2B6`.
- `SHA256SUMS.txt` lists exactly those three payloads and was generated from the copied review-package bytes.
- `.gitattributes` treats the complete 2.0.7r3 review package as binary.

## Identity and protocol

- Assembly/File/Product versions: `2.0.7.0` / `2.0.7.3` / `2.0.7r3`.
- Configuration/TMDB User-Agent/cache token: `2.0.7r3` / `DanmuPlus/2.0.7r3` / `2-0-7r3`.
- Frontend marker V36/V35/V34 counts: `1/0/0`; frontend and backend mapping protocol remain V22.
- Release output contains no PDB.

## Deterministic verification

- Frontend production and regression syntax checks: success.
- Complete Smart Match frontend regression: success, including the real dialog controller, topmost ownership, four-direction geometry, editable-field exceptions, native activation, Tab containment, pointer handoff, focus styles, rerender continuity, parent/child return, busy/empty recovery, body-only reveal, cleanup, and unchanged Android command Back behavior. Every production candidate renderer exposes the complete card as one focusable radio proxy while keeping the native radio out of the remote focus order; deterministic 1280x720 geometry proves Down from the right-aligned search action reaches a candidate instead of skipping to the footer, and Enter/Space activate exactly once.
- X-button regressions prove Up both when landing on X and while X is already focused resets only the dialog body's scroll position to zero, leaving the host scroller unchanged.
- A six-episode, three-temporary-group fixture proves ordinary removal remains non-cascading, rematching a middle group removes that group and all later groups, rematching the first group sends no superseded selections and searches the full suffix, Back restores the exact captured draft, and a partial replacement leaves the unconsumed later suffix unmatched without stale removed-run state.
- Static guards prove no dialog remote-controller `scrollIntoView`, host-scroller ownership, MutationObserver/timer/animation-frame correction, request/persistence, responsive television heuristic, private Emby focus API, new history/backbutton path, or matching/API branch.
- Complete backend regression suite: success.
- Clean sequential Release build: success with 131 baseline warnings and zero errors.
- Strict OpenSpec validation and `git diff --check`: success.

## Deployment acceptance

- Immediately before the V36 follow-up deployment, the active V35 DLL and complete CustomCssJS XML were frozen at `/volume2/@appdata/EmbyServer/danmuplus-backups/20260826T082756+0800-pre-2.0.7r3-v36`. The directory is mode `0700`, owned by `root:root`; its 1,748,480-byte DLL and 583,497-byte XML exactly match the then-active SHA-256 values `3944A015B1A085E32A9DC6C9D4F99E9DF6EED8C7C3E4C52B09887C47B81DD8D8` and `0821EF239D3105A63BEC5B511969D74D8E8C0FFB31863393D03C1D144B6DBC67E9`. The original pre-2.0.7r3 backup and two intermediate V35 backups remain retained as well.
- The target-only transformer reparsed the freshly downloaded XML, required one enabled named Smart Match component with one V35 marker, installed one V36 marker with no V35 marker, decoded the staged target back to the reviewed script, and proved all bytes outside the target content unchanged. The final XML is 588,136 bytes with SHA-256 `12C5E301EEF6DCFFFA1DE58D75C52C4AA6ED2B03B859C8C3E22CA7111D70E16B`; a post-acceptance SCP readback is byte-identical to the staged candidate.
- Runtime DLL: 1,748,480 bytes, mode `0644`, `emby:users`, SHA-256 `3944A015B1A085E32A9DC6C9D4F99E9DF6EED8C7C3E4C52B09887C47B81DD8D8`. Runtime complete CustomCssJS XML: 588,136 bytes, mode `0444`, `emby:users`, SHA-256 `12C5E301EEF6DCFFFA1DE58D75C52C4AA6ED2B03B859C8C3E22CA7111D70E16B`. The unique enabled Smart Match component decodes, after newline normalization, to 272,641 UTF-8 bytes with SHA-256 `9874B704DEFFD6EC3B5435E87F4F1E34879F024B9E7121FA3241DE5A716D0006`; V36/V35/V34 marker counts are `1/0/0`.
- The unrelated enabled `danmuku` component remains 301,818 normalized UTF-8 bytes with SHA-256 `64175BD3F553B0B80AB6DBA833C5497DDDC552F886765690BF55EBCC107570828`. The plugin configuration remains SHA-256 `02519AFD92022BABACF9E6D516C44C0DDE0117A2744593501D8CB29222536069`.
- Synology reports the Emby package running, public health returns HTTP 200 with Emby 4.9.5.0, and the current startup log loads `Emby.Plugin.Danmu, Version=2.0.7.0`, starts the task handler, and completes the entry point. No Danmu/CustomCssJS error, exception, failure, or fatal line appears after restart.
- Authenticated 1280x720 acceptance used the deployed web client and a real Season candidate picker with 23 candidates. The right-aligned `重新智能匹配` control occupied x=886–997, the first former narrow-radio candidate became a full-card proxy at x=268–997, and the footer began at x=794. ArrowDown selected the full `LABEL` proxy rather than skipping to the footer; it exposed `role=radio`, `tabindex=0`, kept its native radio at `tabindex=-1`, and rendered a white 3 px outline plus a 6 px blue ring. One Enter changed exactly one native radio and one proxy `aria-checked` state from zero to one.
- Ten real ArrowDown events scrolled only the Season dialog body from `0` to `350`; thirteen ArrowUp events landed on X and reset the body to `0`. On the real whole-series overview, 22 ArrowDown events scrolled the dialog body to `1405`; 17 ArrowUp events landed on X and reset it to `0`. In both cases host scroll stayed `0` and the item route was unchanged.
- The real whole-series result returned 5 physical Seasons and 190 local episodes. Its Stone Ocean physical Season contained three completed temporary groups: `S05E01–S05E12`, `S05E13–S05E24`, and `S05E25–S05E38`. Rematching temporary Season 1 opened one `S05E01–S05E38（38 集）` range, removed the old temporary Seasons 2 and 3 from the draft, and rendered 23 candidates without a unique-trailing-suffix rejection. Server request evidence records 38 excluded local episodes and zero carried composite selections. `返回总览` restored all three original ranges and all three original sources exactly, including the parent body-scroll context; no Apply action was used.
- The earlier real single-Season rematch likewise carried 26 excluded local episodes and zero composite selections. Across final acceptance the new server log contains only six `MatchPreview` options and zero bind, download, apply, or metadata-write pattern. Every dialog closed with Escape, routes and host scroll remained unchanged, the temporary browser tab was closed, and the 1280x720 viewport override was reset.
- All deployment and live gates passed, so rollback was not invoked. Physical television/remote hardware remains a separate compatibility observation and is not claimed by browser-emulated Arrow/Enter acceptance; the actual deployed web-event path, deterministic controller regressions, target-only runtime hashes, and real multi-temporary-Season suffix behavior are covered.
