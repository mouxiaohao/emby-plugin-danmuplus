## 1. Logical inventory

- [x] 1.1 Add a failing multi-version SDK fixture covering the production Season coordinator and Series enumeration contracts.
- [x] 1.2 Request Emby presentation grouping for Season enumeration, rematch resolution, and authoritative Episode planning without changing XML persistence.
- [x] 1.3 Verify full and short mappings, explicit specials, different identities, representative drift, and background/shared-path parity.

## 2. Verification and deployment

- [x] 2.1 Run backend, frontend, remainder/scope regression suites, strict OpenSpec validation, and Release build; inspect the final diff.
- [x] 2.2 Back up and replace only the Synology DLL, restart Emby, and verify service health plus unchanged configuration files.
- [x] 2.3 Validate live logical Season/Episode counts and mappings for the affected series and explicit Season/S00 previews; document evidence and limits.

## 3. GitHub delivery

- [x] 3.1 Commit the scoped patch and validation record, push develop, and create/attach a PR to main without merging it.
