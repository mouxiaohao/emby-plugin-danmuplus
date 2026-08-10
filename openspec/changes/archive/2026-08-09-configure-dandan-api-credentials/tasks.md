## 1. Persistent configuration

- [x] 1.1 Add empty-default API ID and API Secret properties to the backward-compatible Dandan configuration model.
- [x] 1.2 Add administrator settings inputs with password masking and load/save integration that preserves existing Dandan options.

## 2. Credential resolution and signing

- [x] 2.1 Implement pair-based configured, environment, and legacy credential precedence with whitespace trimming.
- [x] 2.2 Use the resolved pair consistently for request headers and signatures without logging secret material.
- [x] 2.3 Return distinct missing and incomplete credential errors while preserving cross-provider failure isolation.

## 3. Verification

- [x] 3.1 Add deterministic regression tests for precedence, incomplete pairs, trimming, and secret-safe errors.
- [x] 3.2 Verify the embedded HTML contains a masked secret input and JavaScript round-trips both fields without dropping existing options.
- [x] 3.3 Run OpenSpec strict validation, regression tests, and a Release build.

## 4. Deployment and delivery

- [x] 4.1 Back up and deploy the Release DLL to Synology, restart Emby, and verify the plugin configuration schema exposes both new fields.
- [x] 4.2 Confirm missing credentials remain isolated to Dandanplay until a real pair is supplied.
- [x] 4.3 Package the DLL, source archive, hashes, backup path, and credential setup notes as user-facing outputs.
