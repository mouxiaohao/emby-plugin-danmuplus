## Purpose

Defines deterministic resolution when several danmu identifiers coexist and success-gated uniqueness for ordinary plugin identifiers without altering unrelated Emby metadata.

## ADDED Requirements

### Requirement: Enabled-provider order resolves competing exact identifiers
When an item has ordinary identifiers for more than one enabled danmu provider, the system SHALL validate and select them in the current configured enabled-provider order. Provider order SHALL be evaluated before item-scope fallback; within one provider, an Episode identifier SHALL precede that provider's containing-Season identifier. Invalid identifiers SHALL be skipped without preventing validation of later enabled providers.

#### Scenario: Season has two valid provider identifiers
- **WHEN** a Season has valid ordinary identifiers for two enabled providers
- **THEN** the identifier belonging to the earlier configured provider SHALL be selected without scored search

#### Scenario: Episode and Season contain identifiers from different providers
- **WHEN** an Episode has a valid identifier from a later configured provider and its Season has a valid identifier from an earlier configured provider
- **THEN** the earlier configured provider SHALL be selected and its Episode-to-Season scope rules SHALL be applied

#### Scenario: Earlier provider identifier is invalid
- **WHEN** the earlier configured provider's identifier fails exact validation and a later provider's identifier is valid
- **THEN** the later valid provider SHALL be selected without entering scored search

#### Scenario: Series preview resolves child Seasons
- **WHEN** a Series preview contains child Seasons with several valid item-local identifiers
- **THEN** each child Season SHALL be resolved independently in configured enabled-provider order and the Series object's identifiers SHALL remain ignored

#### Scenario: Season identifier happens to equal the Series identifier
- **WHEN** a Season has an item-local provider identifier whose value happens to equal the ignored Series identifier for the same provider
- **THEN** the Season identifier SHALL remain eligible and SHALL NOT be discarded by value comparison
- **AND** configured enabled-provider order SHALL still determine the exact match

#### Scenario: Movie has competing identifiers
- **WHEN** a Movie has several valid enabled danmu-provider identifiers
- **THEN** the earlier configured provider's exact Movie result SHALL be selected

### Requirement: Successful Season and Episode writes make ordinary plugin IDs unique
After an accepted `success` or `partial` result with `FilePersisted=true`, writing the selected ordinary identifier to a Season or Episode SHALL remove ordinary identifier keys for every other registered danmu provider from that same item, including providers that are currently disabled. The successful Episode, including the first Episode, SHALL retain its exact provider Episode identifier; the Season SHALL retain its verified provider collection identifier.

#### Scenario: First Season episode succeeds
- **WHEN** the first accepted persisted Episode writes the selected Season identifier
- **THEN** the Season SHALL contain the selected provider's ordinary collection identifier
- **AND** ordinary identifiers for all other registered danmu providers SHALL be absent from that Season
- **AND** that Episode SHALL contain the selected provider's exact Episode identifier with all other registered ordinary plugin identifiers absent

#### Scenario: Later Season episode succeeds
- **WHEN** a later Episode persists XML after the Season identifier has already been committed
- **THEN** that Episode SHALL independently receive the selected provider's exact Episode identifier and remove its other ordinary plugin identifiers

#### Scenario: Automatic import succeeds
- **WHEN** automatic library processing persists a Season or Episode XML file
- **THEN** it SHALL apply the same identifier uniqueness behavior as interactive tracked download

### Requirement: Identifier cleanup is plugin-scoped and success-gated
Ordinary-ID cleanup MUST target only the exact registered danmu provider keys on the written Season or Episode. Every automatic and user-selected manual path MUST preserve every `SiteIDManual` key, all Series and Movie metadata, and all non-plugin identifiers. Failed, skipped, cancelled, timed-out, stale-generation, or non-persisted results MUST NOT write or remove any ordinary identifier.

#### Scenario: Successful result contains manual and metadata-provider identifiers
- **WHEN** a successful automatic or user-selected Dandanplay write processes an item that also contains `BilibiliIDManual`, `DandanIDManual`, TMDB, TVDB, IMDb, and custom metadata keys
- **THEN** every manual and non-plugin key SHALL remain byte-for-byte unchanged while the other ordinary registered danmu keys are removed

#### Scenario: Manual match fails before persistence
- **WHEN** the user manually selects a provider but no accepted result persists XML
- **THEN** no success-triggered ordinary-ID cleanup SHALL occur and every manual binding SHALL remain available

#### Scenario: Disabled provider has an old ordinary identifier
- **WHEN** a successful selected-provider write processes an item containing an ordinary identifier for a registered but disabled danmu provider
- **THEN** the disabled provider's ordinary identifier SHALL be removed

#### Scenario: Download does not persist a file
- **WHEN** the result fails, is skipped, is cancelled, times out, is stale, or does not persist XML
- **THEN** no success-triggered ordinary-ID cleanup SHALL occur and every manual binding SHALL remain unchanged

#### Scenario: Metadata repository update fails
- **WHEN** XML persistence succeeds but the identifier update throws
- **THEN** the file result SHALL remain successful or partial and the metadata failure SHALL be reported diagnostically
