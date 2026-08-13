## MODIFIED Requirements

### Requirement: Logical-season-safe positional mapping
The system MUST restrict every batch Season candidate's positional Episode mapping to the target Season item's own inventory records that have a valid ItemId and a `ParentIndexNumber` exactly equal to the target Season's `IndexNumber`. Episodes from another parent season or an unknown parent season SHALL be out of scope: they MUST NOT become mappings, unmatched runs, temporary seasons, supplemental selections, downloads, or completeness inputs for that target. A standalone Season 0 operation SHALL apply the same equality rule to the real Season 0 item's own inventory. Existing local Episode ProviderIds are not smart-match evidence.

#### Scenario: One Punch Man has main Episodes and placed specials
- **WHEN** S1 contains S01E01-S01E12 and displayed S00E01-S00E07 and the user selects a 12-Episode S1 source
- **THEN** the source SHALL map exactly the twelve Parent 1 Episodes and the seven Parent 0 Episodes SHALL remain out of scope without creating an unmatched or temporary run

#### Scenario: Selected source claims 19 Episodes
- **WHEN** the same S1 display structure is matched to an ordinary upstream S1 candidate reporting 19 Episodes
- **THEN** positional mapping SHALL still stop after the twelve eligible Parent 1 Episodes and SHALL NOT consume or expose the Parent 0 Episodes as supplemental work

#### Scenario: Special source is selected
- **WHEN** the user explicitly targets the real Season 0 item and selects a verified special or OVA source
- **THEN** only valid Parent 0 ItemIds from that Season 0 item's own inventory SHALL be eligible for mapping, and the system SHALL not change any Episode's Emby Season membership or numbering

#### Scenario: Foreign normal-season Episode is displayed in the target
- **WHEN** an S2 display contains an Episode whose `ParentIndexNumber` is 1 or 3
- **THEN** that Episode SHALL be out of scope exactly like a Parent 0 or unknown-parent Episode and SHALL not create a supplemental selection or interrupt the eligible Parent 2 mapping

### Requirement: Maximal contiguous virtual groups
The system SHALL construct virtual groups only from the target Season's eligible Episode sequence after exact parent-season filtering. It SHALL split that eligible sequence at mapped/unmatched state, stable source identity, or source-continuity boundaries. Out-of-scope foreign-season and unknown-parent Episodes MUST NOT create groups, temporary seasons, or boundaries between otherwise continuous eligible Episodes.

#### Scenario: Duplicate local episode numbers exist across logical seasons
- **WHEN** an S1 display contains S01E01-S01E12 and S00E01-S00E07
- **THEN** the S1 result SHALL contain only the eligible twelve-Episode Parent 1 group, while all seven Parent 0 ItemIds SHALL be absent from mappings and temporary groups

#### Scenario: Same source appears around a special run
- **WHEN** one stable main source maps eligible target-season Episodes displayed before and after an out-of-scope foreign-season Episode and source continuity is otherwise preserved
- **THEN** the eligible mappings SHALL remain one maximal group and the ignored Episode SHALL not split or alter the group

#### Scenario: Eligible source is shorter than the target run
- **WHEN** a source maps ten of twelve eligible Parent 1 Episodes and foreign Episodes are interleaved or appended in the display inventory
- **THEN** exactly the remaining two Parent 1 Episodes SHALL form the temporary group and no foreign Episode SHALL join or split it

### Requirement: Cross-target ItemId ownership and entry-point parity
Whole-Series and single-Season matching SHALL invoke the same authoritative Season-plan operation. Whole-Series matching MUST enumerate only known positive-number target Seasons. Within every target plan, an ItemId SHALL be eligible only when it comes from that target Season item's inventory and its parent season number exactly equals the target number; appearance in another Season's display inventory SHALL NOT confer ownership. Explicit standalone Season 0 matching SHALL use only the real Season 0 item's own Parent 0 inventory.

#### Scenario: Placed S00 Episode is returned by S0 and S1 queries
- **WHEN** the same Parent 0 ItemId appears in both the Season 0 and Season 1 display inventories
- **THEN** whole-Series matching SHALL execute it zero times because Season 0 is skipped and it is out of scope for S1, while an explicit standalone Season 0 operation SHALL include it exactly once from the Season 0 inventory

#### Scenario: Foreign normal-season Episode appears in two target inventories
- **WHEN** a Parent 2 ItemId is displayed by both S1 and S2
- **THEN** it SHALL be excluded from the S1 plan and eligible only for the S2 plan when it is present in the S2 item's own inventory

#### Scenario: Series and Season entry points receive identical input
- **WHEN** whole-Series and explicit single-Season entry points process the same positive-number SeasonId, selections, and current Season inventory
- **THEN** their eligible ordered Episodes, mappings, unmatched runs, and executable download set SHALL be semantically identical
