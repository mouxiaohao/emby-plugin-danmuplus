## ADDED Requirements

### Requirement: Conservative title-clause fallback discovery
After searching the complete parent-series title across all enabled providers, the system SHALL derive a bounded, de-duplicated set of searchable clauses from the same local title by splitting on explicit Chinese and English title punctuation. For each enabled provider that has no standard-round candidate at or above the confidence threshold, it SHALL search meaningful clauses that meet a conservative minimum length. Candidate scoring SHALL continue to compare results against the complete original local title, year, season context, and episode count.

#### Scenario: Localized leading title differs but subtitle is shared
- **WHEN** a provider uses a different localized leading title but shares a distinctive clause such as “为了成为图书管理员” with the local title
- **THEN** a clause fallback round SHALL make that provider candidate discoverable
- **AND** the candidate SHALL be scored against the complete local metadata rather than the clause alone

#### Scenario: Later provider has a complete-title result
- **WHEN** a later configured provider has a confident complete-title candidate but an earlier configured provider has no confident standard-round candidate
- **THEN** clause fallback SHALL still run for the earlier provider
- **AND** final automatic selection SHALL retain the configured confident-site priority rule

#### Scenario: Provider already has a confident standard result
- **WHEN** one provider already has a candidate at or above the confidence threshold from standard rounds
- **THEN** the system MAY skip clause fallback for that provider only

#### Scenario: Provider reveals a different localized leading title
- **WHEN** a provider's first-round result is strongly related to the complete local title but exposes a different localized leading title and no candidate is yet confident
- **THEN** the system MAY remove a terminal season designator from that returned title
- **AND** it MAY search at most two meaningful provider-local alias clauses derived from the result
- **AND** it SHALL still score all candidates against the original local metadata

#### Scenario: Clause is too short or generic
- **WHEN** splitting a title produces an empty, punctuation-only, or short generic clause
- **THEN** that clause MUST NOT cause an additional provider search round

#### Scenario: Clause results duplicate standard results
- **WHEN** a clause returns a provider media identifier already present from another round
- **THEN** the merged candidate set SHALL contain that provider/identifier pair only once

#### Scenario: User supplies a custom keyword
- **WHEN** the user explicitly enters a custom search keyword
- **THEN** the system SHALL search only that keyword and SHALL NOT silently add local-title clauses or provider-derived aliases

### Requirement: Title-clause behavior is shared
Manual smart-match preview and automatic library-import matching SHALL use the same complete-title and clause-fallback keyword sequence, provider fan-out, de-duplication, scoring, and automatic-selection rules.

#### Scenario: New Season requires a clause fallback
- **WHEN** automatic library processing cannot uniquely select from the complete title but a clause round returns a qualifying provider candidate
- **THEN** that candidate SHALL participate under the same confidence rules as in manual preview
