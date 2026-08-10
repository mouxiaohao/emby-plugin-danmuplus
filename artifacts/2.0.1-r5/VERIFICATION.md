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
| `Emby.Plugin.Danmu.dll` | `b08186751fec8a407d1ae8ffb9975a952f25f8960b7143dc0bf159d012515d5c` |
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
  (`b08186751fec8a407d1ae8ffb9975a952f25f8960b7143dc0bf159d012515d5c`).
- The post-margin-fix snapshot is
  `/volume2/@appdata/EmbyServer/plugins/backups/danmu-2.0.1-r5-final-margin-20260810-111000`.
  Its pre-replacement DLL checksum was
  `09837f136b4c8a908eb85b0af26942aa2454f7c9bb5bc5248b5edd2eab2e69f1`
  and its Danmu configuration checksum was
  `959757ac17b4faa63db3ac32495914308e043b4ea6b6980740ffdf2524546d26`.
- Synology's package stop/status wrapper left an older Emby process detached
  from the `active/exited` systemd unit. The exact process executable, command
  line, parent and start time were checked before sending that PID `TERM`.
  The old PID exited without `KILL`; the replacement process started after the
  final DLL timestamp, the systemd unit was active, and port 8096 returned
  HTTP 200.
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
- At the time of the proxy-route smokes, the saved legacy custom proxy value
  happened to equal the built-in route and was preserved. After the user later
  saved direct-route credentials, the final configuration had direct mode,
  official selection true, and an empty custom value. The direct smoke restored
  that later state exactly; no custom value was invented or cleared by the
  verification pass.
- Official-CORS route: representative Episode preview was `matched` with 13
  candidates; Dandan media detail resolved source Episode 1; 11 candidates
  from other providers remained available; the tracked comment task completed
  with a successful outcome.
- Custom-CORS route: the same search/media-detail/comment path completed with
  the same candidate counts and a successful tracked comment outcome. The
  saved custom value was preserved.
- Each successful proxy route isolated one other-provider search diagnostic
  without losing successful provider candidates.
- Direct route was rerun after the user configured AppId/AppSecret. A
  representative Dandan search returned a candidate, media-detail preparation
  succeeded, and the tracked comment task completed with one success and no
  failure. The file-side probe found a non-empty 309,656-byte XML and a
  non-empty 448,949-byte ASS for the tested Episode.
- ASS generation was temporarily enabled for the direct smoke and restored to
  disabled in `finally`.
- Configuration restoration was confirmed as semantically equivalent:
  direct mode remained selected, official selection remained true, the custom
  proxy value remained empty, and ASS generation was disabled.
- Automatic candidate selection was observed. A destructive manual-binding
  mutation was not performed, so automatic/manual binding coverage is not
  claimed.
- The reported `3年Z组银八老师` Season was previewed without binding or
  downloading it. Its unique Bilibili candidate scored `1.0000` at source
  order 0 and its Dandan runner-up scored `0.9679` at source order 1. The
  final loaded DLL returned `matched`, `AutoSelected=true`, and selected the
  Bilibili candidate. An initial result from the detached old process was
  discarded after the process-lifetime discrepancy was found.
- The final-process log segment was scanned without printing credential
  values. It contained none of the configured Dandan AppId/AppSecret, Emby
  access token, NAS password, saved custom proxy literal, signature query
  fields, or secret fields. Emby's request logger did record the
  `X-Emby-Authorization` header name and non-secret client metadata for login
  requests; therefore the stricter assertion that authentication-header text
  is entirely absent is not claimed.
