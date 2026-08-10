# 2.0.1-r5 candidate verification

## Baseline and candidate

- Baseline release: `2.0.1-r4`.
- Baseline DLL checksum: `6839e21f8def5ab1227143fea7d08d6be4331641f03302eb76a73d9f04d0faa5`
  (recorded in `releases/v2.0.1-r4/RELEASE.md`).
- Candidate version: `2.0.1-r5`.
- Candidate directory (relative): `artifacts/2.0.1-r5/`.
- Candidate directory (absolute):
  `C:\Users\mouxi\Documents\Codex\emby-plugin-danmuplus\artifacts\2.0.1-r5\`.

## Candidate files

| File | SHA-256 |
| --- | --- |
| `Emby.Plugin.Danmu.dll` | `c90b67b9ee9e72554beab79d07e239a639a6e4c816fb6893564344bc81dbcd97` |
| `DanmuSmartMatch.CustomCssJS.js` | `058ab6b2385ae10a5b2bd4b1ab7e172e742cf84e793e6d63e6f8e06114d223f1` |

The files were copied from the just-validated Release DLL and source frontend
asset. Their hashes were rechecked after the copy.

## Local verification

- Release build: passed with 0 errors (existing warnings remain).
- C# regression executable: passed.
- Frontend regression: passed.
- `git diff --check`: passed.
- Strict OpenSpec validation: passed for `add-dandan-api-proxy-mode`,
  `cache-bust-plugin-config-page`, `add-official-dandan-cors-option`, and
  `extend-smart-match-menus-and-movies`.
- Diff-scope and sensitive-literal scan: passed; only intended r5 candidate,
  implementation, regression, configuration, and OpenSpec files are present.
  No credentials, signatures, or authentication headers are recorded here.

## Remote deployment and rollback

Earlier Worker and Emby proxy feasibility evidence is recorded in
`openspec/changes/add-dandan-api-proxy-mode/verification.md`. It is a
precondition only and does **not** count as regression evidence for this r5
candidate.

- Final pre-deployment snapshot:
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.1-r5-final-20260810-094734`.
- The retained r4 rollback target is
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.1-r5-20260810-090632`.
- Final snapshot checksums before replacement:
  - DLL: `da84ef1e001ff38fdb90ab5537ac08ffc090c4044e532ed211ff3504607342f0`
  - Danmu configuration:
    `c70fe88144618758523197afebfe64a7af284002acce2336846f9fb72c5f04`
  - CustomCssJS configuration:
    `ef3b5833809044c51f63ffe1e1540bccf9bca04256b1ea18de53b65811f5b973`
- The final DLL was installed at
  `/volume2/@appdata/EmbyServer/plugins/Emby.Plugin.Danmu.dll`; its deployed
  SHA-256 exactly matched the packaged candidate
  (`c90b67b9ee9e72554beab79d07e239a639a6e4c816fb6893564344bc81dbcd97`).
- Emby was stopped for the paired replacement and restarted successfully.
- CustomCssJS was updated only by replacing the unique Smart Match
  `content` node in its configuration XML. The configuration retained two
  JavaScript entries in their original order, exactly one V9 installation
  flag, exact candidate script content, and unchanged raw content for the
  other entry.
- The CustomCssJS plugin binary was not replaced or modified. Its SHA-256 was
  `19934c5906fef5e4c925d69bc9f1d48f7cd188e417c2f682d1c46d4cc368abe1`
  both before and after deployment.
- The first attempt to update CustomCssJS through its configuration API
  cleared its JavaScript array. The just-created snapshot was restored before
  any DLL replacement, Emby was restarted, and the restored XML checksum
  matched `ef3b5833809044c51f63ffe1e1540bccf9bca04256b1ea18de53b65811f5b973`.
- Rollback procedure: stop Emby, restore the DLL and both configuration XML
  files from the retained r4 rollback directory, restore owner/mode, start
  Emby, then compare the three files with the recorded rollback checksums.

## Browser and live-smoke verification

- An existing signed-in Chrome profile opened the versioned
  `danmu-2-0-1-r5` configuration page without clearing browser data. It
  displayed version `2.0.1-r5`, the official-CORS checkbox, and the current
  configuration form.
- The saved legacy custom proxy value happens to equal the built-in route.
  The r5 build did not inject that value, but migration rules require
  preserving the user-saved custom value, so it remains visible in the custom
  input/configuration response. The strict no-literal live criterion is
  therefore not claimed.
- Official-CORS route: representative Episode preview was `matched` with 13
  candidates; Dandan media detail resolved source Episode 1; 11 candidates
  from other providers remained available; the tracked comment task completed
  with a successful outcome.
- Custom-CORS route: the same search/media-detail/comment path completed with
  the same candidate counts and a successful tracked comment outcome. The
  saved custom value was preserved.
- Each successful proxy route isolated one other-provider search diagnostic
  without losing successful provider candidates.
- Direct route reached preview/media-detail preparation, but tracked comment
  download ended `completed_with_errors/failed`. Presence-only inspection
  confirmed that AppId, AppSecret, and the legacy Secret were all absent.
  This is recorded as a `credential_config` environment blocker; no
  credentials were guessed or modified.
- ASS generation was temporarily enabled for the proxy smokes and restored to
  disabled in `finally`. The tracked comment outcomes succeeded, but the
  subsequent file-side XML/ASS path probe did not produce conclusive output;
  filesystem output is not claimed as verified.
- Configuration restoration was confirmed as semantically equivalent:
  proxy mode enabled, custom value retained, official-CORS selection
  explicitly persisted as false for the legacy custom configuration, and ASS
  generation disabled.
- Automatic candidate selection was observed. A destructive manual-binding
  mutation was not performed, so automatic/manual binding coverage is not
  claimed.
- A complete post-smoke sensitive-log scan was not completed. No credentials,
  tokens, signatures, authentication headers, or full proxy URL are recorded
  in this verification file.
