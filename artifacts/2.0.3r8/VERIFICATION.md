# 2.0.3r8 verification

Verified and deployed on 2026-08-13 to Synology `DS918`, Emby `4.9.5.0`.

## Final active state

- DLL SHA256: `0199880314a30675c7f3ca17ae72b324e735f2d7cd924ed9c22dc5f4720335ce`
- CustomCssJS XML SHA256 (preserved from r7): `abe0a92196f5e6b3c545d3967f6b86e148945b81930208e5cc46825c8eebf0fb`
- Danmu configuration SHA256 (preserved from r7): `a3be897f9fb84fa19cba5b226cac0b5e2f942a5b2117a5379cca851ca407c973`
- Frontend source/package SHA256 (preserved from r7): `af10dffd6605a24ad19da777424e4dbc3afd12a17739f210bd3d96d065466feb`
- Frontend marker: exactly one `__embyDanmuSmartMenuV23`
- Mapping protocol: `21`

The initial deployment candidate exposed a second public `Mgtv` constructor and was rejected by Emby's dependency injection. No download or metadata write was performed. The constructor was made internal for the focused test assembly, a one-public-constructor regression was added, all gates were rerun, and the final DLL above was deployed. Post-fix startup logs contain no MGTV activation/search error.

## Automated verification

- MGTV focused fixtures: pass, including numeric/string `type`, `typeName`, `videoList`, protocol-relative same-domain URLs, exact anonymous query, cache copies, bounded retry and one public scraper constructor.
- Main backend regression: pass.
- r3-r7 independent policy/scope regressions: pass.
- Frontend regression: pass.
- Release build: pass with zero errors (existing warnings only).
- r8 sibling-r7 narrow-delta gate: pass.
- Strict OpenSpec validation and `git diff --check`: pass.

## Live read-only acceptance

- Direct Synology endpoint probe: HTTP/business code 200 and ten suggestions for `你好星期六`.
- Emby manual smart-match cold search: nine `芒果TV` candidates for `你好星期六`; no `MgtvID` failure.
- `你好，星期六` and `你好 星期六`: nine MGTV candidates each, no provider failure.
- Unrelated keyword: legal empty MGTV result, no provider failure.
- Movie keyword `熊出没·重启未来`: one MGTV candidate.
- Candidate detail remained lazy; only after clicking one MGTV candidate did five source episodes and titles expand.
- Whole-Series acceptance still reported S00 records as read-only ignored (8 and 10 records for the two tested seasons).
- No selection was saved, no download was started, and no metadata write was performed.

## Rollback

Paired read-only r7 backup:

`/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.3r8-final-20260813-acceptance`

Its SHA256 manifest records the r7 DLL plus the exact CustomCssJS and Danmu configuration. To restore: stop `pkgctl-EmbyServer`, copy all three files back to their original plugin/configuration paths preserving owner/mode, start the service, verify the three hashes from the backup manifest, then verify `/emby/System/Info/Public`.

## Residual limitation

`/pc/suggest/v1` is a suggestion service, not an exhaustive catalogue search. A valid empty result means no suggestion was returned for that wording; it does not prove that MGTV has no matching programme. Sparse suggestions may also omit trustworthy year/category/episode-count metadata, which is intentionally shown as unknown until explicit detail inspection.
