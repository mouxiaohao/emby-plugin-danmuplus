# Confirmed Episode Download Specification

## Purpose

Execute the user's confirmed local-to-source Episode mappings without applying matching policy again during download or retry.

## Requirements

### Requirement: Confirmed mappings determine user downloads
After the user selects mapped Episodes and starts downloading, the system SHALL execute each selected source mapping using the server-retained local ItemId, source EpisodeId, and CommentId. It SHALL NOT reject, skip, or rematch these Episodes because of season number, missing local numbering, special/OVA/OAD identity, score, episode-count mismatch, incomplete Season coverage, or another preview generation. Normal seven-day duplicate skipping, its explicit force-refresh override, user cancellation, download timeouts, and actual provider or filesystem errors SHALL remain separate from matching policy.

#### Scenario: User downloads a special or an Episode with incomplete metadata
- **WHEN** the user downloads a mapped S00 Episode or an Episode whose local season or episode number is missing
- **THEN** the system SHALL request the selected source CommentId and persist its usable XML

#### Scenario: Preview or library metadata changes after selection
- **WHEN** another preview supersedes the generation or an Episode changes Season after its mapping was displayed
- **THEN** download and retry SHALL execute the original selected source tuple without rebuilding or revalidating the matching scope

#### Scenario: Selected Episode has recent XML
- **WHEN** the user starts a mapped Episode download with a file updated less than seven days ago
- **THEN** ordinary mode SHALL retain the normal seven-day skip and force-refresh mode SHALL request the selected source and atomically replace the XML after successful content retrieval

#### Scenario: Other local Episodes remain unmatched
- **WHEN** the user starts downloading a partial plan with at least one confirmed mapping
- **THEN** the system SHALL download the mapped Episodes without requiring a second backend partial-confirmation flag and SHALL leave unmatched Episodes untouched

### Requirement: Download errors describe actual unavailable inputs or I/O
Missing retained mappings after a server restart, nonexistent local items, unavailable provider implementations, absent source download identifiers, empty source content, and failed filesystem operations SHALL report their actual cause. The system SHALL NOT invent season, score, provenance, or count restrictions after a usable mapping has already been selected. Failure to update optional Season metadata SHALL NOT invalidate successful XML downloads.

#### Scenario: Optional Season metadata is unavailable
- **WHEN** a retained Episode mapping is retried after the Season item is unavailable
- **THEN** the system SHALL download the Episode's frozen source tuple and retain the XML even if optional metadata cannot be updated
