## MODIFIED Requirements

### Requirement: Shared manual and automatic matching behavior
Manual whole-Series preview, manual single-Season preview, confidence-selected and manually selected candidate confirmation, newly added positive-number Season processing, and download-time rebuild SHALL use the same identifier-free cross-provider search rules and the same target-season-scoped authoritative virtual Episode-plan operation. Whole-Series matching SHALL only enumerate and aggregate known positive-number target Seasons; it MUST NOT apply a different Episode ordering, grouping, alignment, mapping, or persistence path. No Season source, including a complete single-source result, may bypass explicit virtual mapping. Only an explicitly user-targeted standalone Season 0 operation SHALL process Season 0; whole-Series and unattended/media-import paths SHALL skip Season 0 before provider search, selection, planning, download, or metadata write. Automatic processing SHALL remain fail-closed on incomplete, stale, or structurally ambiguous plans.

The shared planner SHALL preserve reliable sparse Episode coordinates. For the same target inventory and trusted selections, whole-Series, single-Season, automatic positive-Season processing, and rebuild SHALL choose the same zero-offset or explicit-anchor numeric alignment, the same whole-segment positional fallback, the same exact mappings, and the same unmatched runs.

#### Scenario: New season is added to the library
- **WHEN** Emby raises the add event for a positive-number Season
- **THEN** the system SHALL use the shared global matcher over only Episodes whose parent season equals the target number and SHALL persist a Season display identifier only under the complete-single-source terminal policy

#### Scenario: New Season 0 is added to the library
- **WHEN** Emby raises an unattended add event for Season 0
- **THEN** the system SHALL skip provider search, selection, mapping, download, binding, and metadata writes for that event while preserving explicit standalone Season 0 matching

#### Scenario: New season match is ambiguous
- **WHEN** a new season's global candidates do not satisfy automatic selection confidence
- **THEN** the system SHALL avoid persisting an arbitrary automatic provider binding and SHALL not start a download from a provider selected solely by configuration order

#### Scenario: Whole-Series and Season select the same candidate
- **WHEN** both entry points confirm the same provider candidate for the same positive-number SeasonId
- **THEN** both SHALL return the same eligible ordered Episodes, alignment mode, mappings, temporary runs, safety decision, and download set

#### Scenario: Selected candidate resolves only part of the owning logical season
- **WHEN** reliable source numbering lacks a coordinate for an eligible local Episode or the verified source is exhausted before every eligible coordinate is mapped
- **THEN** both entry points SHALL leave only the corresponding eligible local rows unmatched and SHALL NOT shift later numbered mappings to fill the gap

#### Scenario: Confidence selection and manual selection choose the same source
- **WHEN** automatic confidence policy and a user selection independently choose the same candidate with identical target inventory and anchor intent
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

#### Scenario: Sparse positive Season is rebuilt before download
- **WHEN** preview maps local E1-E6 and E10-E13 to the same numbered source Episodes and the authoritative target/source facts remain unchanged at download time
- **THEN** rebuild SHALL reproduce those exact pairs and SHALL not compress local E10 to source E7
