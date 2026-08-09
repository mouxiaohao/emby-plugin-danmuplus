# Main Episode Selection Specification

## Purpose

Defines stable provider-to-library episode mapping that downloads danmu for main program episodes while excluding interleaved previews, trailers, PVs, featurettes, and other non-main videos.

## Requirements

### Requirement: Structured main-episode classification
The system SHALL use provider-supplied episode type, section, trailer, and badge metadata when available to exclude explicit non-main content before mapping source episodes to local episode numbers.

#### Scenario: Provider interleaves previews with main episodes
- **WHEN** a provider response contains main episodes and entries explicitly marked as previews or trailers
- **THEN** only the main episodes SHALL participate in source-to-local episode mapping

#### Scenario: Provider field names require explicit mapping
- **WHEN** provider JSON uses field names that do not map automatically to the plugin data model
- **THEN** the system SHALL deserialize the fields required for main-episode classification explicitly

### Requirement: Conservative fallback classification
The system SHALL use conservative title markers and duplicate-number evidence when structured metadata is absent or incomplete, and MUST NOT classify or reject an episode based only on a small downloaded XML file size. Download validity SHALL instead require at least one usable danmu comment and successful non-empty XML serialization.

#### Scenario: Obvious preview title lacks metadata
- **WHEN** an episode has no usable type metadata but its title explicitly identifies it as a preview, trailer, PV, featurette, or bonus clip
- **THEN** that entry SHALL be excluded from the main episode list

#### Scenario: Legitimate short or low-comment episode
- **WHEN** an entry is short or produces a small danmu XML but is not otherwise identified as non-main content and contains at least one usable comment
- **THEN** the system SHALL accept it regardless of serialized XML byte size

### Requirement: Stable duplicate episode resolution
The system SHALL normalize episode numbers and, when multiple source entries claim the same number, prefer the entry with explicit main-content metadata and then the longer program entry, while preserving ascending canonical episode order.

#### Scenario: Preview and full episode share a number
- **WHEN** a preview and a full episode both claim the same episode number
- **THEN** the full episode SHALL occupy that episode number exactly once

### Requirement: Safe unusable-list handling
The system MUST NOT silently restore an unfiltered provider list when filtering determines that no reliable main episode list remains.

#### Scenario: Every source entry is classified as non-main
- **WHEN** filtering removes every source entry
- **THEN** the download SHALL report an unusable provider episode list instead of mapping local episodes to the original unfiltered entries

### Requirement: Shared normalized list consumption
Manual bulk download, automatic library-import download, single-episode retry, and saved-binding download SHALL consume the same normalized provider episode list.

#### Scenario: Automatic and manual download use the same binding
- **WHEN** the same season and provider media identifier are processed manually and through automatic library import
- **THEN** both paths SHALL map each local episode number to the same normalized source episode

### Requirement: Existing files remain non-destructive
The system SHALL leave previously downloaded XML files outside the newly normalized source episode range unchanged unless the user explicitly requests their removal.

#### Scenario: Earlier faulty mapping created surplus XML files
- **WHEN** a corrected provider list contains fewer main episodes than an earlier unfiltered download
- **THEN** the plugin SHALL stop producing new mappings beyond the corrected range but SHALL NOT automatically delete the surplus existing files
