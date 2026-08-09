## MODIFIED Requirements

### Requirement: Provider-neutral global ranking
The system SHALL merge and de-duplicate candidates from all searched providers and rank them by composite matching evidence, including title, parent title, season keyword, year, and episode count. Provider configuration priority MUST NOT affect candidate score or the ordering of candidates with different final scores, but SHALL determine the displayed order of candidates whose final composite scores are exactly equal.

#### Scenario: Better candidate is on a lower-priority provider
- **WHEN** a lower-priority provider candidate has a higher composite score than every higher-priority provider candidate
- **THEN** the lower-priority provider candidate SHALL appear first and be evaluated as the automatic selection

#### Scenario: Candidates have different scores
- **WHEN** the match API returns multiple candidates
- **THEN** their scores SHALL be monotonically non-increasing in the returned list

#### Scenario: Candidates have equal scores
- **WHEN** two candidates have exactly equal final composite scores
- **THEN** the candidate from the provider earlier in the current configured provider order SHALL appear first

#### Scenario: Equal-score ordering supplies a provider winner
- **WHEN** the highest final composite score is shared by candidates from different providers
- **THEN** the candidate from the earliest configured provider SHALL be evaluated first for priority-based automatic selection

### Requirement: Confidence-gated automatic selection
The system SHALL automatically select a candidate when the globally ranked candidate set either satisfies the configured minimum score and separation requirements or has a highest-score tie that is uniquely resolved by configured provider priority. Provider priority MUST NOT resolve a tie between multiple highest-scoring candidates from the same highest-priority provider.

#### Scenario: Global winner is sufficiently strong and distinct
- **WHEN** the top global candidate meets the minimum score and is sufficiently separated from the runner-up
- **THEN** the match result SHALL identify that candidate as automatically selected

#### Scenario: Top score is tied across providers
- **WHEN** the top score meets the minimum score and the earliest configured provider among the tied candidates has exactly one top-scoring candidate
- **THEN** the system SHALL automatically select that candidate

#### Scenario: Highest-priority provider remains internally ambiguous
- **WHEN** the earliest configured provider among the top-scoring candidates has multiple candidates with that same score
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary result

#### Scenario: Global result is ambiguous
- **WHEN** no candidate satisfies the standard confidence rules or the priority-resolved tie rule
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary provider result
