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
Whole-Series and single-Season smart matching SHALL ignore every saved Series/Season manual binding and provider identifier and SHALL always perform fresh candidate discovery from descriptive metadata. Saved bindings MUST NOT suppress search, select a provider, or prefill Episode mappings. Single-Episode and Movie matching MAY use only the target item's own exact identifier unless the caller explicitly forces re-search.

#### Scenario: Season has a saved manual binding
- **WHEN** whole-Series, single-Season, or automatic Season planning processes that Season
- **THEN** the saved provider/media identifier SHALL be ignored and the same fresh search and virtual mapping workflow SHALL run as if the binding were absent

#### Scenario: User reopens a fully identified Season
- **WHEN** all local Episodes and the Season contain plugin identifiers from an earlier download
- **THEN** the new smart-match result SHALL not adopt them and SHALL depend only on current descriptive metadata, local structure, and current source selection

#### Scenario: User forces a new single-item search
- **WHEN** the user forces Episode or Movie re-search
- **THEN** that target item's own identifier SHALL also be bypassed

#### Scenario: User forces a new search
- **WHEN** the user requests forced whole-Series or single-Season search
- **THEN** the system SHALL use the same identifier-free discovery as ordinary Series/Season smart matching and SHALL not restore any saved binding

#### Scenario: User toggles force refresh
- **WHEN** the user enables or disables force refresh for a Series/Season download
- **THEN** only the seven-day XML freshness/duplicate policy SHALL change and candidate discovery, confidence, virtual mappings, and temporary runs SHALL remain identical

### Requirement: Shared manual and automatic matching behavior
Manual whole-Series preview, manual single-Season preview, confidence-selected and manually selected candidate confirmation, newly added Season processing, and download-time rebuild SHALL use the same identifier-free cross-provider search rules and the same target-season-scoped authoritative virtual Episode-plan operation. Whole-Series matching SHALL only enumerate and aggregate known positive-number target Seasons; it MUST NOT apply a different Episode ordering, grouping, mapping, or persistence path. No Season source, including a complete single-source result, may bypass explicit virtual mapping. Explicitly targeted or automatically processed Season 0 SHALL remain supported through the shared standalone Season operation. Automatic processing SHALL remain fail-closed on incomplete or structurally ambiguous plans.

#### Scenario: New season is added to the library
- **WHEN** Emby raises the add event for a positive-number Season
- **THEN** the system SHALL use the shared global matcher over only Episodes whose parent season equals the target number and SHALL persist a Season display identifier only under the complete-single-source terminal policy

#### Scenario: New Season 0 is added to the library
- **WHEN** Emby raises the add event specifically for Season 0
- **THEN** the system SHALL use the Season 0 item's own inventory and shared target-season matching rules rather than the whole-Series skip policy

#### Scenario: New season match is ambiguous
- **WHEN** a new season's global candidates do not satisfy automatic selection confidence
- **THEN** the system SHALL avoid persisting an arbitrary automatic provider binding and SHALL not start a download from a provider selected solely by configuration order

#### Scenario: Whole-Series and Season select the same candidate
- **WHEN** both entry points confirm the same provider candidate for the same positive-number SeasonId
- **THEN** both SHALL return the same eligible ordered Episodes, mappings, temporary runs, safety decision, and download set

#### Scenario: Selected candidate resolves only part of the owning logical season
- **WHEN** a candidate has fewer verified source Episodes than the target's eligible exact-parent Episode sequence
- **THEN** both entry points SHALL map only the verified eligible range and SHALL expose only the remaining maximal eligible target-season run for explicit continuation

#### Scenario: Confidence selection and manual selection choose the same source
- **WHEN** automatic confidence policy and a user selection independently choose the same candidate for identical target Season input
- **THEN** both SHALL produce the same eligible virtual mapping and the same eligible temporary runs

#### Scenario: Season contains placed Episodes from another logical season
- **WHEN** an S1 display includes S00, another-season, or unknown-parent Episodes
- **THEN** the shared planner SHALL exclude those Episodes before scoring and mapping, and SHALL not render them as temporary or supplemental runs

#### Scenario: Whole-Series matching enumerates targets
- **WHEN** the parent Series contains Season 0, positive-number Seasons, and an unknown-number Season
- **THEN** only the positive-number Seasons SHALL be searched and returned as whole-Series targets

#### Scenario: Manual and automatic paths observe foreign Episodes
- **WHEN** a target Season display includes Episodes whose parent season differs from the target
- **THEN** both paths SHALL exclude the same foreign ItemIds before episode-count scoring, mapping, temporary-run construction, and execution

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
