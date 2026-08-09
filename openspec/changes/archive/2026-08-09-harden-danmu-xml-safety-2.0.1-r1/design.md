## Context

All providers ultimately serialize `ScraperDanmakuText` through `ScraperDanmaku.ToXml`, but only iQIYI and the legacy Bilibili CID path parse provider-supplied XML before reaching that shared model. The existing output fallback checks UTF-16 code units individually, which can remove a valid emoji surrogate pair when another illegal character causes sanitization. The iQIYI input regex does not remove U+FFFE/U+FFFF, and neither download path can distinguish an empty result from a small but valid result.

The plugin targets C# 8 and .NET Standard 2.0. Existing provider matching, saved bindings, seven-day skipping, retry controls, segment sampling, partial Tencent results, XML element names, and ASS conversion must remain compatible.

## Goals / Non-Goals

**Goals:**

- Apply the XML 1.0 legal Unicode scalar ranges correctly without damaging valid supplementary characters.
- Recover iQIYI and Bilibili source XML only after the unchanged first parse fails.
- Guarantee XML-safe final comment output for every provider at one shared boundary.
- Accept a valid serialized document containing even one comment regardless of byte size.
- Return distinct empty-content and serialization-failure errors.

**Non-Goals:**

- Repair arbitrary malformed XML structure, namespaces, or unescaped markup.
- Change JSON/protobuf decoding or provider download protocols.
- Change comment sampling, ordering, color, timing, IDs, or output XML schema.
- Generate release archives or deploy to Emby in this implementation step.

## Decisions

### Sanitize Unicode scalars rather than UTF-16 code units

The shared sanitizer scans UTF-16 while recognizing valid high/low surrogate pairs. XML 1.0 permits TAB, LF, CR, U+0020–U+D7FF, U+E000–U+FFFD, and supplementary scalars U+10000–U+10FFFF. Illegal BMP controls, U+FFFE/U+FFFF, and isolated surrogates are removed. This preserves Chinese, line breaks, and emoji.

Document sanitization additionally removes numeric character references whose decoded scalar is illegal. Valid references remain unchanged, and reference-like text inside CDATA remains literal and unchanged. It does not attempt general markup recovery.

### Use input recovery and output defense as separate layers

iQIYI `XmlSerializer` and Bilibili `XmlDocument` first parse the untouched response. Only an XML parse failure triggers one sanitized retry, preserving the established fast path and provider semantics. JSON and protobuf providers require no input retry.

All providers pass through `ScraperDanmakuText.WriteXml`. Comment content and the generated `p` attribute are sanitized there immediately before `XmlWriter` escapes and writes them. This prevents one bad source field from aborting the complete output while retaining the existing XML schema.

### Validate content semantically, not by byte length

Both save paths use one pure shared check and serializer. A null result or zero comments is reported as no valid danmu; serialization exceptions are reported as XML serialization failures; an impossible null/empty byte result is reported separately. One or more comments are accepted even when the complete XML is below 1 KB.

The existing all-segments-failed check remains before serialization. A partial segmented result with at least one comment is still saved and reported as partial.

## Risks / Trade-offs

- [Removing an illegal scalar changes one comment's text] → Remove only XML 1.0-forbidden data and preserve every legal scalar around it.
- [A structurally malformed provider document still fails] → Retry only once, then preserve the original exception path and diagnostic logging.
- [Empty legitimate episodes no longer create header-only files] → Report them explicitly as having no valid danmu; do not overwrite an existing file.
- [Small documents were previously treated as empty] → Deterministic tests require a valid one-comment document below 1 KB to be accepted.

## Migration Plan

1. Run strict OpenSpec validation, the regression harness, and a Release build.
2. In a later authorized delivery step, create a versioned `2.0.1-r1` artifact and record its hash.
3. Back up the deployed DLL before installation, restart Emby, and retry 唐朝诡事录 episode 35.
4. Verify representative iQIYI, Bilibili XML/protobuf, Tencent partial, Youku, Mgtv, and Dandan downloads.
5. Roll back by restoring the timestamped DLL backup; no data or binding migration is required.
