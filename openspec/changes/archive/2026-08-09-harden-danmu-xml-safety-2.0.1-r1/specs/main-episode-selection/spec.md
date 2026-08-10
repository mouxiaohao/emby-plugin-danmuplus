## MODIFIED Requirements

### Requirement: Conservative fallback classification

The system SHALL use conservative title markers and duplicate-number evidence when structured metadata is absent or incomplete, and MUST NOT classify or reject an episode based only on a small downloaded XML file size. Download validity SHALL instead require at least one usable danmu comment and successful non-empty XML serialization.

#### Scenario: Obvious preview title lacks metadata

- **WHEN** an episode has no usable type metadata but its title explicitly identifies it as a preview, trailer, PV, featurette, or bonus clip
- **THEN** that entry SHALL be excluded from the main episode list

#### Scenario: Legitimate short or low-comment episode

- **WHEN** an entry is short or produces a small danmu XML but is not otherwise identified as non-main content and contains at least one usable comment
- **THEN** the system SHALL accept it regardless of serialized XML byte size
