## ADDED Requirements

### Requirement: Mapping-only guidance is rendered once per smart-match result
An applicable Series/Season smart-match result that renders download-mapping cards covered by this guidance SHALL show the exact visible text `下列卡片仅用于本次下载映射，不会改变Emby 的季归属。` exactly once in the current result view. A result with no applicable mapping cards SHALL show zero instances. The guidance MUST NOT repeat inside each Season or virtual-season card, and the removed clause `该季包含多个来源或存在未识别区间；` MUST NOT render anywhere in the workflow.

#### Scenario: Whole-Series result contains several composite Seasons
- **WHEN** a whole-Series smart match renders multiple Seasons with virtual mapping cards
- **THEN** the mapping-only guidance SHALL appear exactly once above the result collection and the removed clause SHALL appear zero times

#### Scenario: Single-Season result contains several virtual groups
- **WHEN** a single-Season smart match renders more than one virtual group
- **THEN** the mapping-only guidance SHALL appear exactly once rather than once per group

#### Scenario: Result view is rendered again after rematch
- **WHEN** the user starts a new rematch and the result view is rebuilt
- **THEN** the newly rendered result SHALL contain exactly one fresh guidance instance when applicable, zero when not applicable, and no duplicate left from the previous render

#### Scenario: Result contains no applicable mapping card
- **WHEN** a Series/Season smart-match result contains no download-mapping card covered by the guidance
- **THEN** the guidance SHALL appear zero times

### Requirement: Alias-exhaustion presentation contains no TMDB internals
When TMDB alias assistance finishes without an automatically acceptable candidate, the browser SHALL render the Season as a failed/unmatched result with a right-side `重新匹配` action. It MUST NOT render the accumulated per-alias candidate rows, searched alias values, TMDB provenance/decision strings, or TMDB-specific completion diagnostics. Server-side operational logging MAY retain credential-safe failure context.

Non-TMDB provider failure diagnostics SHALL remain visible under the provider-failure isolation contract.

#### Scenario: Several aliases return the same Dandanplay source
- **WHEN** every attempted TMDB alias returns the same low-confidence Dandanplay source and alias assistance exhausts its plan
- **THEN** the browser SHALL show no repeated source rows or TMDB diagnostic text and SHALL show one `重新匹配` action

#### Scenario: Alias request fails while another provider fails
- **WHEN** TMDB alias assistance exhausts or faults and an unrelated enabled provider also reports a search failure
- **THEN** the browser SHALL hide TMDB-specific detail while retaining the unrelated provider's bounded public failure diagnostic

#### Scenario: User activates parent-title rematch
- **WHEN** the user activates `重新匹配` from the alias-exhaustion state
- **THEN** the subsequent parent-title automatic candidate result SHALL replace that state without restoring stale alias rows or diagnostics

### Requirement: Verified source Episode surplus is shown once in yellow
After a successful authoritative Season mapping, the browser SHALL show the exact visible text `库内集数少于来源集数` in a yellow warning when the server reports that a selected source's verified Episode inventory exceeds the target Season's full eligible local inventory. Whole-Series and single-Season smart matching SHALL use the same Season-level presentation. The warning MUST NOT be inferred from search-candidate `EpisodeSize`, mapping-row count, displayed library count, or browser-authored values, and MUST NOT appear before an authoritative plan exists.

#### Scenario: Whole-Series matching binds an updating Season
- **WHEN** a whole-Series result contains a successfully mapped Season whose server-authoritative source-surplus state is true
- **THEN** that Season card SHALL show `库内集数少于来源集数` exactly once in yellow without changing its successful state

#### Scenario: Single-Season matching binds an updating Season
- **WHEN** a single-Season result contains a successfully mapped Season whose server-authoritative source-surplus state is true
- **THEN** the same yellow warning SHALL appear exactly once in the Season summary

#### Scenario: Browser sees only candidate Episode metadata
- **WHEN** a candidate advertises more Episodes but no authoritative mapping has established the source-surplus state
- **THEN** the browser SHALL show no source-surplus warning

#### Scenario: Local inventory is equal to or larger than source inventory
- **WHEN** the server-authoritative source-surplus state is false or absent
- **THEN** the warning SHALL not render, including when remaining local Episodes form an existing temporary Season

#### Scenario: Result is rebuilt
- **WHEN** a rematch or composite-plan rebuild changes the authoritative source-surplus state
- **THEN** the current Season summary SHALL reflect only the new state without retaining or duplicating a stale warning
