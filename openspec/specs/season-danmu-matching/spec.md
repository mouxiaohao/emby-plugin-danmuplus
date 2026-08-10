# Season Danmu Matching Specification

## Purpose

Defines predictable cross-provider season discovery and selection so manual and automatic danmu downloads bind the best globally scored media result rather than the first acceptable provider result.

## Requirements

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

### Requirement: Enabled-provider candidate participation
The system SHALL include usable search results from every enabled danmu provider in the shared candidate set used by manual match preview and automatic library-import matching. A usable result MUST contain a provider media identifier accepted by that provider's media-detail path, a non-empty display title, and the available year, category, and episode-count metadata.

#### Scenario: Bilibili anime result is available
- **WHEN** Bilibili is enabled and returns one or more valid anime seasons for the parent series title
- **THEN** the manual match preview SHALL include those Bilibili seasons in the globally scored candidate list

#### Scenario: Bilibili live-action result is available
- **WHEN** Bilibili is enabled and returns one or more valid live-action television seasons for the parent series title
- **THEN** the manual match preview SHALL include those Bilibili seasons with their titles, years, categories, and episode counts

#### Scenario: Automatic matching searches Bilibili
- **WHEN** a newly added non-special season is processed by automatic library-import matching and Bilibili is enabled
- **THEN** valid Bilibili results SHALL participate in the same global scoring and confidence rules as results from other enabled providers

#### Scenario: Bilibili result has no usable identifier
- **WHEN** Bilibili returns a search entry without a positive season or media identifier accepted by its media-detail path
- **THEN** the system SHALL omit that entry without preventing results from Bilibili or other providers from being ranked
