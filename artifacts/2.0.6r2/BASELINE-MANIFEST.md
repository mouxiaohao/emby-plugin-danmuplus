# 2.0.6r2 isolated baseline manifest

Captured before r2 implementation on branch `codex/release-2.0.6r2-dialog-scroll-containment` at `60313cad1bb2679d0e6a68c25f79264f489830d6`.

## Scope and classification

- The inherited formal-r1 seed is exactly the 13 modified files listed below.
- `openspec/changes/release-2-0-6-r2-dialog-scroll-containment/` was already present as an untracked, planning-only r2 delta. It is not part of the 13-file seed or its digest.
- This manifest is new r2 evidence and is not part of the seed.
- No file in the formal r1 checkout or its `artifacts/2.0.6r1` and r1 OpenSpec trees was written while collecting this evidence.

## Complete pre-r2 porcelain state

```text
 M Frontend/DanmuSmartMatch.CustomCssJS.js
 M Frontend/DanmuSmartMatch.RegressionTests.js
 M README.md
 M UPDATE.md
 M artifacts/2.0.6r1/VERIFICATION.md
 M artifacts/2.0.6r1/review-package/DanmuSmartMatch.CustomCssJS.js
 M artifacts/2.0.6r1/review-package/SHA256SUMS.txt
 M artifacts/2.0.6r1/review-package/UPDATE.md
 M artifacts/2.0.6r1/review-package/VERIFICATION.md
 M openspec/changes/release-2-0-6-r1-scroll-state/design.md
 M openspec/changes/release-2-0-6-r1-scroll-state/proposal.md
 M openspec/changes/release-2-0-6-r1-scroll-state/specs/smart-match-error-and-presentation/spec.md
 M openspec/changes/release-2-0-6-r1-scroll-state/tasks.md
?? openspec/changes/release-2-0-6-r2-dialog-scroll-containment/
```

## Seed inventory and digest

Algorithm: sort slash-normalized relative paths with PowerShell `Sort-Object`; for each path emit `relativePath|invariantDecimalByteLength|lowercaseSHA256`; join with LF and append one final LF; hash the BOM-free UTF-8 bytes with SHA-256.

Digest: `fefa366f16020da99af1b8d67863c542c433f2ab9a4be443f82e5d0e9259f2bd`.

```text
artifacts/2.0.6r1/review-package/DanmuSmartMatch.CustomCssJS.js|249568|86441706cec694fe4e6dbf976e41509177ff3a414b3c337ce7da8834ed35bcee
artifacts/2.0.6r1/review-package/SHA256SUMS.txt|261|d885d31b2e2349928baeb7559787e528dfe90d81bf9e64fe13cd567605281b0b
artifacts/2.0.6r1/review-package/UPDATE.md|21668|783a0f565cecf4f3bcb8555c5af5585fdb1dcce67b877453447082175b307cc5
artifacts/2.0.6r1/review-package/VERIFICATION.md|15575|2964b7b73e7d69018b20c4ace8d4096dd312bc7a8ab3520d9b01cabf2dacc3e4
artifacts/2.0.6r1/VERIFICATION.md|15575|2964b7b73e7d69018b20c4ace8d4096dd312bc7a8ab3520d9b01cabf2dacc3e4
Frontend/DanmuSmartMatch.CustomCssJS.js|249568|86441706cec694fe4e6dbf976e41509177ff3a414b3c337ce7da8834ed35bcee
Frontend/DanmuSmartMatch.RegressionTests.js|230988|900c25d0397712bd77c95e9ebdd264bdae6d72c51128c4f392458a47c8fb6236
openspec/changes/release-2-0-6-r1-scroll-state/design.md|23139|c80389d1b257367ae2512119b3144f63bfa1b276056ec2d6a542203beb714807
openspec/changes/release-2-0-6-r1-scroll-state/proposal.md|6475|8e767226733fcf6e7469299d99acc9544e4672ea030c7af6403a5cb4e72de6b0
openspec/changes/release-2-0-6-r1-scroll-state/specs/smart-match-error-and-presentation/spec.md|17260|2bd2610e98db697c715b7cc4515a96e63727becbd28916b159072381df7b9037
openspec/changes/release-2-0-6-r1-scroll-state/tasks.md|14314|095df67fcf3e1e6294aaa3eb946815fc6d8107417de5838fc804a98a0461309b
README.md|14176|935b9d00e130415931456bace11b92fc1d44e50d92a831e452adabe354340ca4
UPDATE.md|21668|783a0f565cecf4f3bcb8555c5af5585fdb1dcce67b877453447082175b307cc5
```

The formal r1 checkout independently reproduced the same 13-path digest before and after this manifest was written.

## Formal-r1 read-only preservation proof

- Branch: `codex/release-2.0.6r1-v30-command-back`.
- HEAD: `60313cad1bb2679d0e6a68c25f79264f489830d6`.
- Index tree: `a0629724720b3940dff3f25a22ec441f84333e55`.
- Porcelain contains the same 13 modified paths and no r2 path; normalized porcelain digest before/after: `90fdb565c72555353496e56b4e391ee2c0c8cf8a7621d09a0562899d91df2c83`.
- Formal-r1 `artifacts/2.0.6r1` inventory: 8 files, digest `f1f2b8610fc894bc93459ef63d10cdd62d4e6a77f9eec55335159444076c8b01` before/after.
- Formal-r1 OpenSpec inventory: 5 files, digest `f97aac9cd5d62de79da91f5c5fdd4a4e9ed4f3d294dae4df78b82aec2edf3c3f` before/after.
- The formal-r1 verification record, including its deployed V30 rollback/readback evidence, remains covered by the artifact inventory above.
- The isolated r2 index tree remained `a0629724720b3940dff3f25a22ec441f84333e55`; neither checkout's index was modified.
