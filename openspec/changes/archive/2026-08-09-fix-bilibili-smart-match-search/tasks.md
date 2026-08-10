## 1. Bilibili Provider Adapter

- [x] 1.1 Implement `SearchForApi` by mapping Bilibili PGC search results to provider-neutral candidates without applying a local title-score cutoff
- [x] 1.2 Select only positive identifiers consumable by the existing Bilibili media-detail path and preserve normalized title, category, year, and episode count
- [x] 1.3 Add concise diagnostic logging for returned, skipped, and mapped Bilibili search records

## 2. Deterministic Verification

- [x] 2.1 Add or extend regression checks covering anime results, live-action results, identifier fallback, and malformed-record omission
- [x] 2.2 Run the full Release build and strict OpenSpec validation

## 3. Live Emby Regression

- [x] 3.1 Back up and deploy the rebuilt DLL to the Synology Emby plugin directory, then restart Emby and verify healthy startup
- [x] 3.2 Force match preview for “葬送的芙莉莲” and verify Bilibili candidates contain the expected 28-episode seasons
- [x] 3.3 Force match preview for both “半泽直树” seasons and verify the corresponding 2013 and 2020 Bilibili candidates participate in descending global score order
- [x] 3.4 Verify a selected Bilibili candidate resolves through `GetMedia` and that existing non-Bilibili candidates and search-error isolation remain intact

## 4. Packaging

- [x] 4.1 Publish the verified DLL, updated source archive including OpenSpec artifacts, checksums, and deployment notes to the output directory
