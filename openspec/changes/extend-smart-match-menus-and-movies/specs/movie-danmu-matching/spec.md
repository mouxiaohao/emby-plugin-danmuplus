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

### Requirement: Movie automatic selection is confidence-gated
The system SHALL automatically select only a sufficiently strong and unambiguous Movie candidate and SHALL otherwise return the ranked candidates for manual choice.

#### Scenario: One strong movie candidate is distinct
- **WHEN** the highest-ranked Movie candidate satisfies the configured confidence and separation rules
- **THEN** the preview SHALL mark it as automatically selected

#### Scenario: Movie candidates are ambiguous
- **WHEN** no Movie candidate is both sufficiently strong and unambiguous
- **THEN** the system SHALL avoid persisting an arbitrary binding and the frontend SHALL allow the user to select a candidate manually or search with another keyword

### Requirement: Saved movie manual binding has precedence
The system MUST preserve and use an existing explicit manual Movie binding unless the user requests a forced search.

#### Scenario: Movie has a saved manual binding
- **WHEN** a Movie preview is requested without a forced search and the Movie has a saved manual provider binding
- **THEN** the saved provider and media identifier SHALL be selected without replacing it through automatic search

#### Scenario: User forces a new movie search
- **WHEN** a user submits a forced Movie search with an optional keyword
- **THEN** the system SHALL return newly searched ranked candidates without deleting the saved binding until a new candidate is confirmed

### Requirement: Selected movie can be bound and downloaded
The system SHALL validate the selected provider candidate against the target Movie, persist the confirmed provider identifier, and start a tracked danmu download for that Movie using the provider-specific movie retrieval path.

#### Scenario: Automatically selected movie is confirmed
- **WHEN** the user confirms a high-confidence Movie candidate
- **THEN** the selected provider identifier SHALL be saved and a tracked single-Movie download SHALL start

#### Scenario: Manually selected movie is confirmed
- **WHEN** the user confirms a Movie candidate from the ranked list
- **THEN** the selection SHALL be saved as a manual binding and a tracked single-Movie download SHALL start

#### Scenario: Candidate no longer resolves
- **WHEN** the chosen provider can no longer resolve the Movie candidate before download starts
- **THEN** the system MUST report a failed preparation result and MUST NOT report a successful download

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
