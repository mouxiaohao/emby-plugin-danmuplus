## 1. Specification and shared safety

- [x] 1.1 Define XML 1.0 input recovery, output safety, and semantic content-validation requirements.
- [x] 1.2 Implement a shared Unicode-scalar and numeric-character-reference sanitizer compatible with .NET Standard 2.0.
- [x] 1.3 Apply the shared sanitizer to final comment content and generated comment attributes for every provider.

## 2. Provider input recovery and download semantics

- [x] 2.1 Retry iQIYI XML deserialization once with document sanitization after the original parse fails.
- [x] 2.2 Retry legacy Bilibili XML parsing once with document sanitization after the original parse fails.
- [x] 2.3 Replace both one-kilobyte gates with shared checks for usable items and successful non-empty serialization.
- [x] 2.4 Preserve all-segments-failed handling, partial-result saving, retry behavior, duplicate skipping, and provider-specific parsing.

## 3. Deterministic verification

- [x] 3.1 Add sanitizer tests for Chinese, XML whitespace, emoji, illegal controls, U+FFFE/U+FFFF, isolated surrogates, and numeric character references.
- [x] 3.2 Add iQIYI and Bilibili raw XML recovery regressions.
- [x] 3.3 Add every-provider final XML coverage and prove that a valid one-comment document below 1 KB is accepted.
- [x] 3.4 Run the regression harness and a clean Release rebuild.
- [x] 3.5 Run strict OpenSpec validation when the OpenSpec CLI is available.

## 4. Delivery verification

- [x] 4.1 Generate versioned `2.0.1-r1` DLL/source artifacts and record hashes.
- [x] 4.2 Back up and deploy the DLL to the Synology server, then restart Emby.
- [x] 4.3 Retry 唐朝诡事录 episode 35 and verify valid XML plus accurate logs.
- [x] 4.4 Run deterministic final-XML regressions for all six providers, complete the iQIYI episode-35 live verification, and record rollback details.
