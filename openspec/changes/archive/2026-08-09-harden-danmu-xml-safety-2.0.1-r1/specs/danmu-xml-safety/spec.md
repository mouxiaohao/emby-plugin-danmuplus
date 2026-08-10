## ADDED Requirements

### Requirement: XML 1.0 Unicode scalar preservation

The system SHALL remove only data that XML 1.0 cannot represent while preserving legal BMP characters, TAB, LF, CR, and legal supplementary Unicode scalars represented by UTF-16 surrogate pairs.

#### Scenario: Valid multilingual text surrounds illegal data

- **WHEN** danmu text contains Chinese, line breaks, emoji, illegal control characters, U+FFFE/U+FFFF, or isolated surrogates
- **THEN** the system SHALL preserve the Chinese, line breaks, and emoji and SHALL remove the illegal XML data

#### Scenario: Numeric character references contain illegal scalars

- **WHEN** a provider XML document contains numeric character references to XML 1.0-forbidden values
- **THEN** the system SHALL remove the illegal references while preserving legal decimal and hexadecimal references

#### Scenario: CDATA contains reference-like text

- **WHEN** a CDATA section contains literal text such as `&#xFFFF;` or `&#0;`
- **THEN** document sanitization SHALL preserve that CDATA text unchanged rather than treating it as a character reference

### Requirement: Provider XML input recovery

The system SHALL first parse provider XML unchanged and MUST retry once with shared document sanitization when iQIYI or Bilibili raw XML fails because it contains XML 1.0-invalid data.

#### Scenario: iQIYI response contains U+FFFF

- **WHEN** an otherwise valid iQIYI danmu document contains U+FFFF inside a comment
- **THEN** the sanitized retry SHALL deserialize the document and preserve the legal text around U+FFFF

#### Scenario: Bilibili legacy XML contains invalid data

- **WHEN** the legacy Bilibili CID response contains an illegal XML scalar or numeric character reference
- **THEN** the sanitized retry SHALL parse its valid comments into the shared danmu model

### Requirement: Shared final XML defense

Every provider SHALL sanitize comment content and source-derived comment attribute text at the shared final XML-writing boundary before `XmlWriter` writes the value.

#### Scenario: JSON or protobuf comment contains invalid XML data

- **WHEN** any provider successfully decodes a comment whose text contains both a valid emoji and an illegal XML scalar
- **THEN** final XML serialization SHALL succeed, preserve the emoji, and omit the illegal scalar

### Requirement: Semantic downloadable-content validation

The system MUST decide whether danmu is downloadable from content and serialization semantics rather than serialized byte size.

#### Scenario: One-comment XML is below one kilobyte

- **WHEN** a provider returns one usable comment and the successfully serialized XML is smaller than 1 KB
- **THEN** manual, automatic, and retry download paths SHALL accept and save it

#### Scenario: Provider returns no usable comments

- **WHEN** the provider returns null content or a danmu model with no comments
- **THEN** the download SHALL fail with an empty-content error and MUST NOT write a header-only XML file

#### Scenario: Final XML serialization fails

- **WHEN** a non-empty danmu model cannot be serialized
- **THEN** the download SHALL report an XML serialization failure and MUST NOT replace an existing file

#### Scenario: Some segmented downloads fail

- **WHEN** at least one segment fails but another segment returns one or more usable comments
- **THEN** the system SHALL save the valid partial XML and retain the existing partial-result status
