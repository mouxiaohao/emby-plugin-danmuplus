## Purpose

Defines one bounded alternate-title discovery contract across every smart-match media entry point while keeping provider searches local, deterministic, and fully backend-scored.

## ADDED Requirements

### Requirement: Shared alias discovery across supported media types
The system SHALL apply the same bounded local-title-clause and provider-derived alias discovery policy when scored matching is required for a Series child Season, a standalone Season, an Episode, or a Movie. An Episode SHALL use its containing Season context when the provider exposes season collections rather than searchable standalone episodes.

#### Scenario: Standalone Season needs a provider alias
- **WHEN** a standalone Season's complete local title does not yield a confident candidate but a related result on that provider exposes a usable alternate title
- **THEN** the system SHALL perform the bounded provider-local alias search and score all results against the original Season metadata

#### Scenario: Episode has no resolvable identifier
- **WHEN** neither the Episode nor its containing Season has a usable exact identifier and scored matching is required
- **THEN** the system SHALL use the shared Season-context alias discovery and map the requested local Episode only after a Season candidate is selected

#### Scenario: Movie needs a provider alias
- **WHEN** a Movie's complete local title does not yield a confident candidate but a strongly related result on that provider exposes a usable alternate title
- **THEN** the system SHALL query that bounded alias through the provider's Movie-specific search path and retain Movie-specific filtering

#### Scenario: Series preview contains several Seasons
- **WHEN** the user smart-matches a Series
- **THEN** each child Season SHALL independently receive the same alias discovery behavior as a standalone Season

### Requirement: Alias search remains bounded and provider-local
Alias discovery MUST use only conservative clauses from local titles or strongly related candidate titles returned by the same provider. It MUST preserve candidate de-duplication, the 0.90 confidence rule, original-metadata scoring, configured provider priority, and provider failure isolation.

#### Scenario: Candidate title belongs to another provider
- **WHEN** one provider returns an alternate title
- **THEN** that title MUST NOT become a search keyword on a different provider

#### Scenario: Explicit custom keyword is supplied
- **WHEN** the user performs a custom-keyword search
- **THEN** the system SHALL search only that explicit keyword and MUST NOT append automatic clause or alias rounds

#### Scenario: User repeats smart matching without editing the default title
- **WHEN** the candidate screen shows its default title and the user clicks the ordinary rematch action without editing that title
- **THEN** the frontend SHALL omit the custom `keyword` parameter
- **AND** the backend SHALL execute the complete standard, clause, and provider-local alias rounds

#### Scenario: User edits the search input
- **WHEN** the user changes the candidate-screen search input before submitting it
- **THEN** the frontend SHALL send the edited value as an explicit custom `keyword`
- **AND** automatic alias rounds SHALL remain disabled for that deliberate keyword search

#### Scenario: Provider already has a confident result
- **WHEN** a provider has a candidate scoring at least 0.90 from earlier rounds
- **THEN** the system SHALL omit unnecessary alias rounds for that provider

#### Scenario: One alias request fails
- **WHEN** a provider fails during an alias round
- **THEN** results from other enabled providers SHALL remain available and the failure SHALL be diagnostic rather than fatal

### Requirement: Alias-discovered candidates emphasize year and episode evidence
A candidate newly discovered through an automatic local-title-clause or provider-derived alias round SHALL use an alias-specific evidence profile. A Season candidate, including one used to map an Episode, SHALL use title relevance 35%, year 20%, and episode-count 45%. A Movie candidate SHALL use title relevance 70% and year 30%. Alias-specific automatic selection MUST require title relevance of at least 0.72 and the unchanged total confidence score of at least 0.90. Candidates already discovered by complete-title standard rounds SHALL retain the normal scoring profile even if a later alias query returns the same candidate.

#### Scenario: Alternate Season name has exact structural evidence
- **WHEN** an alias-only Season candidate has title relevance 0.72, an exact year, and an exact episode count
- **THEN** its composite score SHALL be `0.72 × 0.35 + 1.00 × 0.20 + 1.00 × 0.45 = 0.902`
- **AND** it SHALL be eligible for the normal 0.90 confidence decision

#### Scenario: Alias Season title is insufficiently related
- **WHEN** an alias-only Season candidate has title relevance below 0.72 even though year and episode count match exactly
- **THEN** it MUST NOT be automatically selected

#### Scenario: Alias Movie has matching year
- **WHEN** an alias-only Movie candidate passes the 0.72 title-relevance floor
- **THEN** its score SHALL use title 70% and year 30%
- **AND** it SHALL still need a total score of at least 0.90 for automatic selection

#### Scenario: Candidate was already found by complete title
- **WHEN** a candidate first appears in a standard complete-title search and is returned again by an alias round
- **THEN** it SHALL keep the normal media scoring profile and MUST NOT be promoted merely because it reappeared

#### Scenario: Alias evidence is incomplete
- **WHEN** the provider omits or disagrees on year or episode count
- **THEN** the existing year and episode evidence functions SHALL supply their normal partial, unknown, or mismatch values rather than treating the field as an exact match

### Requirement: Manual matching and automatic processing share discovery
Interactive smart matching and automatic library-import matching SHALL invoke the same backend alias discovery, scoring, and selection rules for equivalent media context.

#### Scenario: Same Season is processed by two entry points
- **WHEN** a Season without usable identifiers is previewed interactively and later processed by automatic library import with unchanged metadata and provider responses
- **THEN** both paths SHALL produce the same candidate ordering and automatic-selection decision

### Requirement: Android back navigation remains inside the smart-match workflow
While a smart-match dialog is open, the frontend SHALL consume Android/WebView back navigation before the underlying Emby page. A secondary candidate view SHALL return to its smart-match parent view, while a top-level smart-match view SHALL close the dialog. A protected in-progress download view SHALL remain open until its existing close policy permits dismissal.

#### Scenario: Back from a Series Season candidate view
- **WHEN** the user opens one Season's candidate view from the full-Series overview and invokes Android back
- **THEN** the frontend SHALL render the full-Series overview
- **AND** the underlying Emby route SHALL remain unchanged

#### Scenario: Back from the top-level smart-match view
- **WHEN** the user invokes Android back from the top-level Series, Season, Episode, or Movie smart-match view
- **THEN** the smart-match dialog SHALL close
- **AND** the underlying Emby route SHALL remain unchanged

#### Scenario: Back while close is protected
- **WHEN** a tracked download view is not closable and Android back is invoked
- **THEN** the dialog SHALL remain open and the history guard SHALL be restored

#### Scenario: Android status bar overlaps the dialog header
- **WHEN** the smart-match dialog is rendered in the narrow-screen Android layout
- **THEN** the header SHALL include the platform top safe-area inset plus usable spacing
- **AND** the title and top-right close button SHALL remain below the status bar

#### Scenario: Android back is invoked during a match request
- **WHEN** the dialog is waiting for an initial, rematch, alias, or custom-keyword preview response
- **THEN** Android/WebView back SHALL be consumed and the dialog SHALL remain open
- **AND** the top-right close button SHALL remain able to close the dialog immediately
- **AND** normal parent/top-level back behavior SHALL resume after a result view is rendered
