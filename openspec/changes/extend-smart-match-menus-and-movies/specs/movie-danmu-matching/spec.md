## Purpose

Defines predictable cross-provider movie discovery, user selection, binding, and downloadable danmu progress without routing movies through season-only semantics.

## ADDED Requirements

### Requirement: Movie match preview uses movie metadata and enabled providers
The system SHALL accept a Movie item for smart-match preview, search every enabled movie-capable danmu provider, merge usable candidates, and rank them by movie title, production year, and other available movie evidence.

#### Scenario: Multiple providers return movie candidates
- **WHEN** a Movie preview search returns usable candidates from more than one enabled provider
- **THEN** all candidates SHALL participate in one deterministic non-increasing score order

#### Scenario: Provider returns a television candidate
- **WHEN** a provider search result is identifiable as a television, season, or other non-movie item
- **THEN** that result MUST NOT be offered as a Movie candidate

#### Scenario: One movie provider fails
- **WHEN** one enabled provider fails during Movie search
- **THEN** candidates from successful providers SHALL still be returned and the failed provider SHALL be exposed in search diagnostics

### Requirement: Movie matching uses one provider-identifier-first backend policy
The system SHALL use the same backend decision policy for interactive Movie matching and library-import Movie matching. It MUST first resolve identifiers belonging to enabled providers in configured provider order, then consider a compatible saved binding, and only then search and score every enabled Movie-capable provider.

#### Scenario: Enabled local Movie identifier resolves
- **WHEN** a Movie has one or more identifiers for enabled providers and the earliest configured provider's identifier resolves
- **THEN** that provider object SHALL be selected immediately with match origin `provider-id` and no scored search SHALL run

#### Scenario: Disabled provider identifier exists
- **WHEN** a Movie identifier belongs to a provider that is not enabled
- **THEN** the identifier MUST be ignored

#### Scenario: Movie identifiers cannot be resolved
- **WHEN** every applicable local Movie identifier fails to resolve
- **THEN** diagnostics SHALL record `provider-id-unresolved` and matching SHALL continue through saved binding and backend scored search

#### Scenario: Saved Movie binding is available after identifier resolution
- **WHEN** no enabled provider identifier resolves and a compatible saved Movie binding exists
- **THEN** the saved binding SHALL be selected before scored search

#### Scenario: User requests Movie rematch
- **WHEN** a user requests `rematch` with an optional keyword
- **THEN** the system SHALL bypass all local provider identifiers and saved bindings, search and score every enabled Movie-capable provider, and preserve existing metadata until a new download succeeds

### Requirement: Movie automatic selection uses r6 confident-site priority
The system SHALL treat every Movie candidate with score `>= 0.90` as confident. When confident candidates span providers, it MUST choose the earliest configured provider without comparing score differences across providers, then choose that provider's unique highest-scoring candidate.

#### Scenario: Earlier provider has a lower confident score
- **WHEN** an earlier configured provider has a `0.90` candidate and a later provider has a higher-scoring confident candidate
- **THEN** the earlier provider's candidate SHALL be automatically selected

#### Scenario: Selected provider has multiple confident candidates
- **WHEN** the earliest provider represented in the confident pool has multiple candidates with different scores
- **THEN** its unique highest-scoring candidate SHALL be selected

#### Scenario: Selected provider has an internal top-score tie
- **WHEN** the selected provider has multiple candidates sharing its highest confident score
- **THEN** preview SHALL remain ambiguous for manual selection

#### Scenario: No Movie candidate is confident
- **WHEN** every Movie candidate scores below `0.90`
- **THEN** the backend SHALL apply only the explicitly defined r6 low-confidence result state and MUST NOT invoke a legacy Danmu matching algorithm

### Requirement: Selected Movie identifier is persisted only after successful download
The system SHALL validate the selected Movie candidate, execute the provider-specific tracked download, and overwrite only that provider's Movie identifier after a valid danmu file is persisted successfully.

#### Scenario: Scored or manually selected Movie download succeeds
- **WHEN** a scored, bound, or manually selected Movie candidate produces a successfully persisted danmu file
- **THEN** its provider identifier SHALL overwrite that provider's existing Movie identifier without removing other providers' identifiers

#### Scenario: Movie matched from existing identifier succeeds
- **WHEN** the successful Movie download originated from the same existing provider identifier
- **THEN** the redundant metadata write MAY be skipped

#### Scenario: Movie download does not succeed
- **WHEN** preparation fails, the download fails, is cancelled, is skipped, or does not persist a valid danmu file
- **THEN** no provider identifier SHALL be changed

#### Scenario: Movie metadata write fails after download
- **WHEN** the danmu file is persisted but updating the Movie identifier fails
- **THEN** the download SHALL remain successful and the result SHALL expose the metadata-update error

### Requirement: Movie download status is observable and compatible
The frontend SHALL display queued, running, successful, skipped, partial, failed, or cancelled Movie download outcomes, while existing Series and Season preview and tracked-download response behavior remains compatible.

#### Scenario: Movie download completes
- **WHEN** the selected provider writes a valid Movie danmu XML file
- **THEN** the tracked task SHALL complete successfully and identify the Movie as the processed target

#### Scenario: Existing duplicate policy skips download
- **WHEN** the Movie danmu file is covered by the existing duplicate-skipping policy and force refresh is not requested
- **THEN** the tracked task SHALL report a skipped outcome rather than a failure

#### Scenario: Existing Series client invokes preview
- **WHEN** an existing frontend requests Series or Season preview and download after Movie support is installed
- **THEN** its current fields, matching behavior, retry controls, and progress semantics SHALL remain available

#### Scenario: Movie progress is displayed
- **WHEN** a tracked Movie download is queued, running, or terminal
- **THEN** the frontend SHALL show the Season-style summary and exactly one detailed Movie item row with its status, diagnostic message, and retry control when retry is applicable

#### Scenario: Movie provider exceeds the deadline
- **WHEN** a Movie provider operation has not completed within 180 seconds
- **THEN** the tracked Movie item SHALL become skipped with a timeout diagnostic, the task SHALL become terminal and closable, and any later provider completion MUST NOT overwrite that result

#### Scenario: Failed movie item is retried
- **WHEN** the user retries the failed or timed-out Movie row
- **THEN** the system SHALL rerun the provider-specific Movie download path and update that row with the new outcome
