## Purpose

Defines exact external-identifier resolution that presents metadata from the identified danmu-provider object without keyword search, scoring, or locally fabricated candidate details.

## ADDED Requirements

### Requirement: Identifier-specific upstream detail lookup
When an enabled danmu-provider identifier exists on an eligible Movie, Series, Season, or Episode scope, the system SHALL request the corresponding provider's identifier-specific detail path before declaring the identifier matched. It MUST NOT use keyword search or score-based candidate selection when that detail lookup succeeds.

#### Scenario: Enabled identifier resolves successfully
- **WHEN** an eligible item scope contains an identifier for an enabled provider and that provider resolves it to usable media
- **THEN** the system SHALL select that exact provider object with provider-ID origin and decision
- **AND** it SHALL NOT invoke keyword search, global scoring, or cross-provider candidate competition

#### Scenario: Higher-priority identifier fails to resolve
- **WHEN** an identifier exists but its provider detail request fails or returns unusable media
- **THEN** the system SHALL record an unresolved-provider diagnostic and continue the existing provider/scope resolution order
- **AND** it MAY reach saved binding or scored search only after no eligible provider identifier resolves

### Requirement: Upstream candidate metadata
The candidate created from a resolved identifier SHALL use the upstream title, year, category, declared episode count, and resolved episode-list count when those values are explicitly available from the provider detail response. It MUST NOT substitute the local Emby title, year, category, or declared episode count as if those values came from the provider.

#### Scenario: Provider supplies complete metadata
- **WHEN** the identifier-specific detail response supplies title, year, category, and episode count
- **THEN** the selected candidate SHALL expose those exact upstream values

#### Scenario: Provider supplies an episode list but no declared count
- **WHEN** the provider detail response supplies a resolved episode list and no positive declared episode count
- **THEN** the candidate episode count SHALL equal the number of usable resolved upstream episodes

#### Scenario: Provider omits a field
- **WHEN** an upstream field cannot be obtained from identifier-specific provider responses
- **THEN** that field SHALL remain unknown
- **AND** the system MUST NOT run keyword search or copy local metadata solely to fill it

#### Scenario: Direct Episode identifier resolves
- **WHEN** an Episode-scoped identifier resolves to one exact upstream episode
- **THEN** the candidate SHALL expose the upstream episode title when available and an exact one-item resolved episode collection
- **AND** unavailable parent-media metadata SHALL remain unknown

### Requirement: Exact-match compatibility
Metadata enrichment SHALL NOT change provider configuration priority, eligible item-scope priority, resolved identifier values, episode mapping, download behavior, persistence behavior, or explicit rematch semantics.

#### Scenario: User requests重新智能匹配
- **WHEN** the caller explicitly requests rematch
- **THEN** the system SHALL skip provider-ID-first selection and execute the existing enabled-provider search and scoring workflow

#### Scenario: Enriched exact match is downloaded
- **WHEN** a user downloads the provider-ID-resolved candidate
- **THEN** the system SHALL use the same resolved provider identifier and episode mapping that existed before metadata enrichment

