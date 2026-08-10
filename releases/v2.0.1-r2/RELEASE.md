# 2.0.1-r2

This release is based on `2.0.1-r1` and publishes the complete
`extend-smart-match-menus-and-movies` working-tree change.

## Highlights

- Smart-match actions for Series, Season, Episode, and Movie detail/card menus,
  including Android long-press action sheets.
- Movie-specific cross-provider ranking, binding, tracked download, timeout,
  cancellation, and retry handling.
- Single-Episode candidate suggestions and editable source Episode numbers
  without replacing the containing Season binding.
- Season-style one-row progress for Movie and Episode tasks.
- iQIYI `qips://tvid` Movie parsing and bounded Tencent barrage requests.

## Artifacts

- `Emby.Plugin.Danmu.dll`
  - SHA-256: `03b12feba16985f7f84780b766ff463af7bdc3befc5973f57b36e9fe4b27b8e1`
- `emby-plugin-danmuplus-2.0.1-r2-source.zip`
  - SHA-256: `db878826ac2ca713c8c5e75cfae34936b7efd1d0ab94332e3d6b176fe5105920`
- `DanmuSmartMatch.CustomCssJS.js`
  - SHA-256: `c1decff5e552c37cbbd6c54723b9c3b99eb2884a7e5a3475cfd547cf91973f80`

## Verification

- Release build: passed with 0 errors and 134 existing warnings.
- C# regression executable: passed.
- Frontend deterministic regression script: passed.
- Strict OpenSpec validation: passed.
- Live Emby 4.9.3.0 deployment, menu, Movie/Episode download, iQIYI,
  Tencent retry/cancellation, and Android long-press evidence is recorded in
  `openspec/changes/extend-smart-match-menus-and-movies/verification.md`.

The OpenSpec change intentionally retains unchecked items where the complete
live matrix (including every requested STRM/forced-refresh combination) was not
performed. No unsupported completion claim is made for those cases.

## Rollback

Restore the paired DLL and CustomCssJS configuration from the timestamped
backup recorded in the verification document, then restart Emby and refresh the
web client. Existing provider identifiers and danmu XML files require no schema
migration.
