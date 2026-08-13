## ADDED Requirements

### Requirement: Manual custom-search result eligibility
When a user explicitly supplies a custom Season search keyword, the system SHALL use that keyword to discover results from every enabled provider and SHALL retain every returned result that has a usable provider identifier, a non-empty title, and a Season-compatible media type. The system MUST NOT reject such a result solely because its displayed title lacks textual similarity to the custom keyword. Retained results SHALL continue through the existing provider-neutral scoring, ordering, confidence, and failure-isolation rules using the target library metadata.

#### Scenario: English alias discovers Chinese DandanPlay titles
- **WHEN** the user manually searches `one punch` or `one punch man` and DandanPlay returns Chinese-titled results for all available seasons
- **THEN** every structurally valid returned Season, including the third season, SHALL remain eligible for the manual candidate list even though its displayed title does not contain the English alias

#### Scenario: Custom keyword contains a space
- **WHEN** the user manually searches with a multi-word keyword containing internal spaces
- **THEN** the provider SHALL receive the complete semantic keyword and valid returned candidates SHALL not be removed by a second title-similarity gate

#### Scenario: Custom keyword contains a literal plus
- **WHEN** the user manually searches with a keyword containing a literal `+`
- **THEN** the `+` SHALL remain query data at the provider boundary and valid returned candidates SHALL participate in the same manual result flow

#### Scenario: Provider returns an unusable record
- **WHEN** a manual custom search returns a record without a usable provider identifier, without a display title, or with an identifiable Movie media type for a Season target
- **THEN** the system SHALL omit that record

#### Scenario: Manual custom result remains low confidence
- **WHEN** a structurally valid provider result has weak title, year, season, or episode-count evidence against the target library Season
- **THEN** the system SHALL retain it for manual selection but MUST NOT bypass the existing confidence rules to bind it automatically

### Requirement: Automatic-search eligibility remains strict
Season searches without an explicit user-entered custom keyword SHALL continue to require identity-bearing target metadata and title evidence before provider results enter automatic matching.

#### Scenario: Automatic provider result is unrelated
- **WHEN** an automatic library-import search returns a result without sufficient title evidence for the target Series
- **THEN** the system SHALL exclude that result from automatic selection

#### Scenario: Provider fails during manual custom search
- **WHEN** one enabled provider fails while processing a manual custom keyword
- **THEN** successful providers SHALL continue contributing candidates and the existing diagnostics SHALL report the failed provider
