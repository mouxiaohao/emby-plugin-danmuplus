## 1. Matching order

- [x] 1.1 Record each provider's current configured enumeration index on scored candidates without changing any score component.
- [x] 1.2 Sort exact final-score ties by configured provider index while retaining deterministic component and textual fallback ordering.
- [x] 1.3 Implement and verify priority-based automatic binding for cross-provider top-score ties while preserving same-provider ambiguity.

## 2. Main episode normalization

- [x] 2.1 Add explicit Bilibili JSON mappings for badge, section, title, publication, and duration fields used by episode classification.
- [x] 2.2 Implement Bilibili main-episode filtering and duplicate numeric-episode resolution using structured metadata, title markers, duration as duplicate evidence, and stable ordering.
- [x] 2.3 Add conservative reusable title classification and apply it where supported providers currently expose unfiltered episode lists, preserving provider-specific structured rules.
- [x] 2.4 Ensure empty/unusable normalized lists fail without restoring the raw provider list and that all download modes consume the normalized media list.

## 3. Deterministic verification

- [x] 3.1 Add regression tests for configured-priority exact-score ordering and binding, descending unequal scores, and preserved same-provider ambiguity.
- [x] 3.2 Add synthetic Bilibili JSON regression tests proving underscored fields deserialize and interleaved preview/full duplicates normalize to the expected main episode count and order.
- [x] 3.3 Add cross-provider classifier regression cases that retain legitimate short or low-comment episodes and exclude only explicit non-main titles.
- [x] 3.4 Run OpenSpec strict validation, regression tests, and a Release build.

## 4. Live validation and delivery

- [x] 4.1 Back up and deploy the Release DLL to the Synology Emby plugin directory and restart Emby.
- [x] 4.2 Confirm Bilibili season 46089 resolves to 28 ordered main episodes and no previews in live server diagnostics.
- [x] 4.3 Run live matching previews that verify descending global scores, configured-priority tie ordering, and no regression for representative enabled providers.
- [x] 4.4 Package the DLL, source archive, OpenSpec artifacts, hashes, backup path, and rollback notes as user-facing outputs.
