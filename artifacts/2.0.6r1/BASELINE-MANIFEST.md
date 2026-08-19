# 2.0.6r1 frozen baseline manifest

Captured before any 2.0.6r1 application, version, or documentation edit.

- Immutable Git base: `d22a1069524bd891c5b36c758f75f4112a19e1f4`
- Source branch at capture: `codex/release-2.0.6-continuous-title-similarity`
- Completed predecessor OpenSpec change: `release-2-0-6-continuous-title-similarity`, 36/36 tasks complete, state `all_done`
- Original checkout: separate primary worktree, branch `codex/local-main-agents-20260818`, clean porcelain
- Excluded planning-only delta: `openspec/changes/release-2-0-6-r1-scroll-state/`
- This manifest and `BASELINE-TRACKED.patch` are capture evidence created after the status snapshot and are not members of the frozen pre-existing 2.0.6 file set.
- Credential/private-path scan of the tracked patch: 0 strong secret-assignment matches; 0 Windows user-profile absolute-path matches.

## Existing 2.0.6 review/deployed identity

- Review DLL: 1657856 bytes, SHA-256 `a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c`
- Deployed DLL recorded by 2.0.6 verification: SHA-256 `a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c`
- Review JS: 234032 bytes, SHA-256 `a447671b98f991075254665bf3c74d029fd0f3b6ddb5aecd661377d0bd1cd3a3`
- Review UPDATE: 19283 bytes, SHA-256 `68fe5ea6bdec05146584784e0dbb25943f46c77d08bf6e48c9393199fada9268`

## Full pre-r1 porcelain status

```text
 M Configuration/PluginConfiguration.cs
 M Emby.Plugin.Danmu.csproj
 M Frontend/DanmuSmartMatch.CustomCssJS.js
 M README.md
 M RegressionTests/R4IdentifierMetamorphic/Program.cs
 M RegressionTests/TitleFidelity/Program.cs
 M RegressionTests/TmdbAliasTests.cs
 M Scraper/DanmuMatchScorer.cs
 M Scraper/Tmdb/TmdbAliasClient.cs
 M UPDATE.md
?? artifacts/2.0.6/VERIFICATION.md
?? artifacts/2.0.6/review-package/DanmuSmartMatch.CustomCssJS.js
?? artifacts/2.0.6/review-package/Emby.Plugin.Danmu.dll
?? artifacts/2.0.6/review-package/SHA256SUMS.txt
?? artifacts/2.0.6/review-package/UPDATE.md
?? artifacts/2.0.6/review-package/VERIFICATION.md
?? openspec/changes/release-2-0-6-continuous-title-similarity/.openspec.yaml
?? openspec/changes/release-2-0-6-continuous-title-similarity/design.md
?? openspec/changes/release-2-0-6-continuous-title-similarity/proposal.md
?? openspec/changes/release-2-0-6-continuous-title-similarity/specs/season-danmu-matching/spec.md
?? openspec/changes/release-2-0-6-continuous-title-similarity/tasks.md
?? openspec/changes/release-2-0-6-r1-scroll-state/.openspec.yaml
?? openspec/changes/release-2-0-6-r1-scroll-state/design.md
?? openspec/changes/release-2-0-6-r1-scroll-state/proposal.md
?? openspec/changes/release-2-0-6-r1-scroll-state/specs/smart-match-error-and-presentation/spec.md
?? openspec/changes/release-2-0-6-r1-scroll-state/tasks.md
```

## Frozen pre-existing changed/untracked files

The r1 planning-only delta and the two capture-evidence files are excluded.

| Path | Bytes | SHA-256 |
|---|---:|---|
| `artifacts/2.0.6/review-package/DanmuSmartMatch.CustomCssJS.js` | 234032 | `a447671b98f991075254665bf3c74d029fd0f3b6ddb5aecd661377d0bd1cd3a3` |
| `artifacts/2.0.6/review-package/Emby.Plugin.Danmu.dll` | 1657856 | `a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c` |
| `artifacts/2.0.6/review-package/SHA256SUMS.txt` | 261 | `65dfb1a95774e9835cdb4ee99f342d6f3598384355f734309243f298835591ce` |
| `artifacts/2.0.6/review-package/UPDATE.md` | 19283 | `68fe5ea6bdec05146584784e0dbb25943f46c77d08bf6e48c9393199fada9268` |
| `artifacts/2.0.6/review-package/VERIFICATION.md` | 7830 | `52cf5be616200cf44141c640bf413d4548997080f7781b2ab8fc9a3fc3c63051` |
| `artifacts/2.0.6/VERIFICATION.md` | 7830 | `52cf5be616200cf44141c640bf413d4548997080f7781b2ab8fc9a3fc3c63051` |
| `Configuration/PluginConfiguration.cs` | 5597 | `8b4a15b53e55341af8aadae569c67f72ec762bcadc023a90061fcec8587b6204` |
| `Emby.Plugin.Danmu.csproj` | 5565 | `38c854f8d0eb0f2ffa78591372a1e07b189b3e9fc72b3d661d0a401a665ac1b1` |
| `Frontend/DanmuSmartMatch.CustomCssJS.js` | 234032 | `a447671b98f991075254665bf3c74d029fd0f3b6ddb5aecd661377d0bd1cd3a3` |
| `openspec/changes/release-2-0-6-continuous-title-similarity/.openspec.yaml` | 40 | `3179d101285ef2d071f85bc0a4e4681e2d7f8c74c5a4a3366233a0b9181d5f1e` |
| `openspec/changes/release-2-0-6-continuous-title-similarity/design.md` | 10294 | `0cf04093165eb1f1cfb3c7a3d67e88e431572968669baec3a7252193018411ff` |
| `openspec/changes/release-2-0-6-continuous-title-similarity/proposal.md` | 2767 | `7b7640c6f2ebd6ea6747cdb89d5a5bf3155cdd893ea6ca9679feae0fd296a88a` |
| `openspec/changes/release-2-0-6-continuous-title-similarity/specs/season-danmu-matching/spec.md` | 7416 | `acf7598de3309b08c7a91884ca277af756876a908bb91bf8a2fb53d60cbc5d94` |
| `openspec/changes/release-2-0-6-continuous-title-similarity/tasks.md` | 8612 | `d87ae62b3cbab83f0bab438efc7f7b9e094a36f18f8bdf00c0a321823df98ea1` |
| `README.md` | 12246 | `15253877a355e937d77c9cb9671a62e627750cae34761b475e695b5c0b681b2c` |
| `RegressionTests/R4IdentifierMetamorphic/Program.cs` | 16788 | `2bbe2fe9c22fd1ba3013cb0ec513c5c86bc27bcc54dc459109e91c5a69936e0f` |
| `RegressionTests/TitleFidelity/Program.cs` | 26860 | `1fd970064538d41e4899392ba6ac7c1db16fdd5c98c7d6a0c23a0e6ccd08c6ed` |
| `RegressionTests/TmdbAliasTests.cs` | 73497 | `1b8883046a210d59fb20035141e11b742eec1c5e5b82254704f9cce797c4b3b7` |
| `Scraper/DanmuMatchScorer.cs` | 50842 | `656830ba97424bd2c2e1de681bdffc47319bc846fdf6a5d7f1087b00021e6278` |
| `Scraper/Tmdb/TmdbAliasClient.cs` | 22208 | `07561a30fea95fef35b71ad2b772ecc62ae0ef93c4bda20012abbd1545ed494e` |
| `UPDATE.md` | 19283 | `68fe5ea6bdec05146584784e0dbb25943f46c77d08bf6e48c9393199fada9268` |

## Actual tracked binary patch

- Path: `artifacts/2.0.6r1/BASELINE-TRACKED.patch`
- Bytes: 28240
- SHA-256: `94b32b46a4f06473e4d7fa4ee170d70ca359ed9c51b152374f3d971d347003c1`
- Capture command: `git diff --binary -- . ':(exclude)openspec/changes/release-2-0-6-r1-scroll-state/**'`

## Immutable `artifacts/2.0.6` tree

Tree digest algorithm: recursively sort slash-normalized relative paths ordinally; for each file emit `<lowercase SHA-256>  <byte length>  <relative path>\n`; SHA-256 the UTF-8 bytes of the complete inventory.

- Tree SHA-256: `39620afe3c634696f30670a1ecc0f93071521fed833ce687b0f5105f5ac7fd7c`

```text
a447671b98f991075254665bf3c74d029fd0f3b6ddb5aecd661377d0bd1cd3a3  234032  review-package/DanmuSmartMatch.CustomCssJS.js
a9524b271ce4065eae348973c4f0047f0b9818d31ff92a87a45dae373e226f5c  1657856  review-package/Emby.Plugin.Danmu.dll
65dfb1a95774e9835cdb4ee99f342d6f3598384355f734309243f298835591ce  261  review-package/SHA256SUMS.txt
68fe5ea6bdec05146584784e0dbb25943f46c77d08bf6e48c9393199fada9268  19283  review-package/UPDATE.md
52cf5be616200cf44141c640bf413d4548997080f7781b2ab8fc9a3fc3c63051  7830  review-package/VERIFICATION.md
52cf5be616200cf44141c640bf413d4548997080f7781b2ab8fc9a3fc3c63051  7830  VERIFICATION.md
```
