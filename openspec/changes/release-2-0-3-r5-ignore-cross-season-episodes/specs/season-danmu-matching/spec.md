## MODIFIED Requirements

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
