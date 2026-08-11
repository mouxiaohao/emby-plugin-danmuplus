## ADDED Requirements

### Requirement: Standalone and Series Season alias parity
The system SHALL apply the same bounded provider-local title-clause and provider-derived alias discovery rounds to a standalone Season and to each child Season processed from a Series. Search terms SHALL only discover candidates; ranking SHALL continue to use the original parent title, Season title, year, and episode count. Candidates newly discovered only by those alias rounds SHALL use title 35%, year 20%, and episode count 45%, subject to title relevance of at least 0.72 and the unchanged 0.90 confidence threshold; candidates found by standard rounds SHALL keep their existing scoring weights.

#### Scenario: Standalone fourth Season uses an alternate provider title
- **WHEN** a standalone fourth Season has no usable exact or manual identifier and its complete local title does not yield a confident result
- **THEN** it SHALL receive the same provider-local clause and alias rounds that it would receive inside a full-Series preview

#### Scenario: Alias candidate remains below confidence
- **WHEN** alias discovery returns candidates but none meets the normal automatic-selection rule
- **THEN** the system SHALL return them for manual selection rather than binding an arbitrary result

#### Scenario: Alternate title is rescued by exact year and episode count
- **WHEN** an alias-only candidate has title relevance 0.72 and both year and episode count match exactly
- **THEN** its weighted score SHALL be 0.902 and it SHALL enter the normal confidence selection pool
