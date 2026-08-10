## Purpose

Defines predictable cross-provider season discovery and selection so manual and automatic danmu downloads bind the best globally scored media result rather than the first acceptable provider result.

## ADDED Requirements

### Requirement: Parent-series-first provider search
The system SHALL search the parent series title on every enabled danmu provider before it evaluates any season-specific fallback search round.

#### Scenario: Earlier provider returns an unrelated local match
- **WHEN** an earlier configured provider returns a season-keyword result and a later provider returns a better parent-series result
- **THEN** the system completes the parent-title search across all enabled providers before selecting either result

#### Scenario: Parent search is not uniquely selectable
- **WHEN** the completed parent-title round does not produce a globally unique high-confidence candidate
- **THEN** the system SHALL search progressively more season-specific keywords across every enabled provider

#### Scenario: Parent search is uniquely selectable
- **WHEN** the completed parent-title round produces a globally unique high-confidence candidate
- **THEN** the system MAY stop before unnecessary fallback search rounds without skipping any provider in the completed round

### Requirement: Provider-neutral global ranking
The system SHALL merge and de-duplicate candidates from all searched providers and rank them by composite matching evidence, including title, parent title, season keyword, year, and episode count. Provider configuration priority MUST NOT affect candidate score, automatic selection, or displayed order.

#### Scenario: Better candidate is on a lower-priority provider
- **WHEN** a lower-priority provider candidate has a higher composite score than every higher-priority provider candidate
- **THEN** the lower-priority provider candidate SHALL appear first and be evaluated as the automatic selection

#### Scenario: Candidates have different scores
- **WHEN** the match API returns multiple candidates
- **THEN** their scores SHALL be monotonically non-increasing in the returned list

#### Scenario: Candidates have equal scores
- **WHEN** two candidates have identical composite scoring fields
- **THEN** the system SHALL apply deterministic provider-name, title, and identifier tie breakers that do not depend on configured provider order

### Requirement: Confidence-gated automatic selection
The system SHALL automatically select a candidate only when the globally ranked candidate set satisfies the configured minimum score and separation requirements.

#### Scenario: Global winner is sufficiently strong and distinct
- **WHEN** the top global candidate meets the minimum score and is sufficiently separated from the runner-up
- **THEN** the match result SHALL identify that candidate as automatically selected

#### Scenario: Global result is ambiguous
- **WHEN** no candidate satisfies the global confidence rules
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary provider result

### Requirement: Manual binding precedence
The system MUST preserve an existing explicit manual binding and use it ahead of automatic search unless the caller explicitly requests a forced re-search.

#### Scenario: Season has a saved manual binding
- **WHEN** a manual match preview or automatic library-import operation processes that season without forced search
- **THEN** the manually bound provider and media identifier SHALL remain selected

#### Scenario: User forces a new search
- **WHEN** the user requests a forced match search
- **THEN** the system SHALL return newly searched globally ranked candidates so the automatic result can be adjusted

### Requirement: Shared manual and automatic matching behavior
The manual match-preview path and newly added season processing path SHALL use the same cross-provider search, scoring, ordering, and confidence rules.

#### Scenario: New season is added to the library
- **WHEN** Emby raises the add event for a non-special season without a manual binding
- **THEN** the system SHALL use the shared global matcher and persist only the globally selected automatic provider identifier

#### Scenario: New season match is ambiguous
- **WHEN** a new season's global candidates do not satisfy automatic selection confidence
- **THEN** the system SHALL avoid persisting an arbitrary automatic provider binding and SHALL not start a download from a provider selected solely by configuration order

### Requirement: Provider failure isolation
The system SHALL continue matching other enabled providers when one provider search fails and SHALL expose the failed provider in search diagnostics.

#### Scenario: One provider search throws an error
- **WHEN** an enabled provider fails during a search round
- **THEN** candidates from successful providers SHALL still be ranked and the failed provider SHALL be recorded in the search-error list

