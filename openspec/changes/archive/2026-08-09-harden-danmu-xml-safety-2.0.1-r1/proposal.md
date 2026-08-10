## Why

Some provider responses contain characters that XML 1.0 cannot represent. A real iQIYI response for episode 35 of 唐朝诡事录 contained U+FFFF, causing both the initial and fallback parses to fail. The resulting empty danmu object was then reported as “less than 1 KB”, hiding the actual problem and rejecting legitimate low-comment episodes solely because their serialized files were small.

## What Changes

- Add one shared XML 1.0 Unicode-scalar sanitizer that preserves valid Chinese text, XML whitespace, and supplementary characters such as emoji while removing illegal controls, U+FFFE/U+FFFF, isolated UTF-16 surrogates, and illegal numeric character references.
- Retry iQIYI and Bilibili raw XML parsing once after document sanitization when the original parse fails.
- Sanitize every provider's final comment text and generated comment attribute at the shared `ScraperDanmaku` XML output boundary.
- Replace both one-kilobyte download gates with semantic checks for a non-null danmu result, at least one usable comment, successful XML serialization, and non-empty serialized output.
- Publish the code version as `2.0.1-r1` without changing bindings, provider identifiers, output schema, download scheduling, or partial-segment behavior.

## Capabilities

### New Capabilities

- `danmu-xml-safety`: Defines provider-input recovery, shared XML output safety, and semantic download-content validation.

### Modified Capabilities

- `main-episode-selection`: Removes the remaining file-size-only rejection from manual, automatic, and retry download paths.

## Impact

Affected code is limited to shared XML sanitization/serialization, iQIYI and Bilibili raw XML parsing, the two shared save paths, deterministic regression tests, and version metadata. JSON and protobuf provider parsing is unchanged. Delivery includes versioned DLL/source artifacts and a backed-up Synology deployment with a recorded rollback path; it does not migrate configuration or provider bindings.
