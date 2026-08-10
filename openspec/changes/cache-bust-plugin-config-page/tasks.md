## 1. Versioned configuration resources

- [x] 1.1 Derive a URL-safe cache token from build metadata, then generate a compiled token constant and transformed configuration-page HTML from that single build value.
- [x] 1.2 Register matched, versioned Danmu configuration-page and controller identifiers from the generated constant, and embed the transformed page under the existing logical resource name while retaining the existing configuration API usage.

## 2. Verification

- [x] 2.1 Add or extend deterministic regression checks covering cache-token normalization, matching page/controller identities, the transformed embedded page, and absence of the unresolved placeholder.
- [x] 2.2 Integrate this change with the other `2.0.1-r5` modifications, then build the combined Release plugin and run the relevant regression suite.
- [x] 2.3 Deploy the combined `2.0.1-r5` build to the Emby test server, restart Emby, and confirm a Chrome profile with a previously cached page displays the current configuration UI without clearing cache.

## 3. Packaging and handoff

- [x] 3.1 Produce the combined `2.0.1-r5` Release artifact and record its version, checksum, deployment location, and pre-r5 rollback target; do not publish a standalone cache-fix artifact. (Candidate, deployment, backup, and rollback evidence is recorded in `artifacts/2.0.1-r5/VERIFICATION.md`.)
