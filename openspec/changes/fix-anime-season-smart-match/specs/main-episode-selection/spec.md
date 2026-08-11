## ADDED Requirements

### Requirement: Season-local normalization of provider-global numbering
When a provider detail response represents one standalone Season but numbers its main episodes cumulatively across prior seasons, the adapter SHALL expose the filtered main episode list as season-local ordinal numbers `1..N` while retaining each stable provider episode identifier for download and persistence.

#### Scenario: Second season uses cumulative source numbers
- **WHEN** a 12-episode standalone Season detail response contains main episodes numbered after the preceding season instead of `1..12`
- **THEN** local Season episodes `1..12` SHALL map in provider list order to those 12 stable provider EpisodeIds

#### Scenario: Third season uses cumulative source numbers
- **WHEN** a 10-episode standalone Season detail response uses cumulative source numbers
- **THEN** all ten local episodes SHALL receive unique season-local mappings rather than fail exact-number matching

#### Scenario: Explicit non-main entries are interleaved
- **WHEN** the provider response contains specials or other explicitly non-main entries among cumulatively numbered episodes
- **THEN** the system SHALL filter those entries before assigning season-local ordinal numbers

#### Scenario: Normalized count participates in matching
- **WHEN** filtering and normalization leave N usable main episodes
- **THEN** the provider media episode count used for matching and mapping SHALL be N

#### Scenario: No usable main episode remains
- **WHEN** filtering leaves zero usable provider episodes
- **THEN** the detail result SHALL be treated as unusable rather than fabricating a season-local collection

#### Scenario: Episode identifier persistence
- **WHEN** a normalized source episode downloads successfully
- **THEN** its original stable provider EpisodeId SHALL be written to the local Episode
- **AND** the synthetic season-local ordinal MUST NOT be written as an external identifier
