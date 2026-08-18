## MODIFIED Requirements

### Requirement: Every batch path uses the same eligible set
Interactive whole-Series and single-Season preview, confidence selection, manual selection, rematch, automatic positive-Season processing, download rebuild, retry, partial confirmation, and Season display-mirror eligibility SHALL use the same target-season eligibility operation and inventory snapshot. Only an explicitly user-targeted standalone Season 0 request SHALL process Season 0. Whole-Series and unattended/media-import paths SHALL skip Season 0 before provider search, planning, download, or metadata writes.

#### Scenario: Series and Season entry points process S1
- **WHEN** both entry points process the same S1 with the same current selections
- **THEN** their eligible ItemIds, search episode count, scores, mappings, temporary runs, executable downloads, and completeness result SHALL be semantically identical

#### Scenario: Automatic normal-season processing runs
- **WHEN** Emby triggers automatic processing for positive target Season N
- **THEN** it SHALL use only Parent N Episodes and SHALL not adopt foreign Episodes through a separate automatic mapping path

#### Scenario: Automatic Season 0 processing runs
- **WHEN** Emby triggers unattended/media-import processing for Season 0
- **THEN** the operation SHALL skip Season 0 before provider search, selection, mapping, download, binding, or metadata writes

#### Scenario: User explicitly targets Season 0
- **WHEN** the user starts single-Season smart matching from the real Season 0 item
- **THEN** the existing standalone Season 0 requirement SHALL remain available and SHALL scope the plan only to that item's Parent 0 inventory
