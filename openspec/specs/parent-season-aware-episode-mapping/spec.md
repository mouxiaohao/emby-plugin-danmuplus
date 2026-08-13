# Parent Season Aware Episode Mapping Specification

## Purpose

Defines safe, deterministic Episode identity, ordering, grouping, and source mapping when one Emby Season display contains Episodes whose metadata belongs to different logical seasons.

## Requirements

### Requirement: ItemId identity and explicit season coordinates
The system SHALL identify every local Episode by its immutable Emby ItemId and SHALL carry its `ParentIndexNumber` and `IndexNumber` as separate display coordinates. Episode numbers or sort positions MUST NOT be used as mapping identities.

#### Scenario: Main and special share an episode number
- **WHEN** one Season display contains S01E01 and S00E01
- **THEN** both Episodes SHALL remain present under distinct ItemIds and SHALL be displayed with their distinct season coordinates

#### Scenario: Parent season is unknown
- **WHEN** an Episode has no usable parent season number
- **THEN** it SHALL form an unknown logical-season boundary and SHALL NOT be automatically position-mapped into the owning Season's main run

### Requirement: Placement-aware stable ordering
The system SHALL produce one stable, placement-aware Episode order for preview, temporary ranges, confirmation, download, retry, and automatic processing. Placement metadata and the library's effective display order SHALL take precedence over episode-number-only sorting, with ItemId used only as a final deterministic tie-breaker.

#### Scenario: Specials are placed after the main run
- **WHEN** an Emby S1 display contains S01E01-S01E12 followed by placed S00E01-S00E07
- **THEN** the authoritative order SHALL contain all 19 ItemIds exactly once in that effective order

#### Scenario: Special is placed inside a main run
- **WHEN** one or more S00 Episodes are placed between two S01 Episodes
- **THEN** the authoritative order SHALL retain the placement and SHALL create logical boundaries on both sides of the special run

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

### Requirement: Series and Season smart matching ignores all local identifiers
Every interactive whole-Series search, single-Season search, rematch, confidence-selected candidate, manual candidate confirmation, and automatic Season planning operation SHALL construct its search decision and authoritative plan without reading local Series, Season, or Episode identifiers or saved bindings as candidate, score, mapping, source-boundary, or segmentation evidence. Identifiers MAY remain stored as metadata history and MAY participate after planning in duplicate-file policy, but MUST NOT change the plan.

#### Scenario: Library contains arbitrary local identifiers and bindings
- **WHEN** whole-Series or single-Season smart matching processes identical descriptive metadata and Episode structure with any empty, partial, complete, stale, cross-provider, Series-level, Season-level, Episode-level, manual, or foreign identifier set
- **THEN** search calls, confidence values, selected candidate, mappings, and temporary runs SHALL be identical to the result after all identifiers and saved bindings are removed

#### Scenario: User explicitly rematches a mapped group
- **WHEN** the user selects rematch and confirms a new source
- **THEN** the server SHALL rebuild the run from that new candidate and source start without re-adopting excluded Episode ProviderIds

#### Scenario: Retry follows a task snapshot
- **WHEN** a tracked download entry is retried
- **THEN** retry MAY use its captured exact source identity and SHALL NOT rerun smart matching from current Episode ProviderIds

### Requirement: Exact identifier evidence is single-item scoped
The system SHALL use a local Episode provider identifier only when matching that exact single Episode and SHALL use a local Movie provider identifier only when matching that exact Movie. A forced or explicit re-search SHALL bypass those identifiers. Single-item evidence MUST NOT be promoted into a parent Season or Series mapping decision.

#### Scenario: Single Episode has a provider identifier
- **WHEN** the user opens ordinary single-Episode smart matching without forcing search
- **THEN** that Episode's own verified identifier MAY produce an exact candidate for that Episode only

#### Scenario: Movie has a provider identifier
- **WHEN** the user opens ordinary Movie smart matching without forcing search
- **THEN** that Movie's own verified identifier MAY produce an exact candidate for that Movie only

#### Scenario: User forces a single-item search
- **WHEN** the user explicitly requests re-search for an Episode or Movie
- **THEN** the operation SHALL ignore the local identifier and perform fresh candidate discovery

#### Scenario: Season contains identified Episodes
- **WHEN** a whole-Series or single-Season operation sees Episode identifiers that would individually resolve exactly
- **THEN** none of them SHALL be read, grouped, or promoted as Season evidence

### Requirement: Every Season candidate is an explicit virtual mapping
Every confidence-selected and manually selected Season candidate SHALL be resolved to source Episodes and applied through the same explicit virtual-season planner. The system SHALL NOT use a raw Season bind or positional download shortcut, including when one source covers every local Episode.

#### Scenario: Selected source covers all local Episodes
- **WHEN** a confidence-selected or manual source maps every eligible local Episode
- **THEN** the authoritative plan SHALL contain its explicit mappings, no unmatched run SHALL remain, and download SHALL execute that plan

#### Scenario: Selected source covers only part of a Season
- **WHEN** source Episodes are exhausted while eligible local Episodes remain
- **THEN** every remaining maximal contiguous local run SHALL become a temporary season available for another automatic/manual source selection or optional skipping

#### Scenario: Multiple rounds complete the Season
- **WHEN** the user repeatedly matches temporary seasons until every eligible ItemId is covered
- **THEN** all confirmed virtual mappings SHALL coexist in one authoritative plan and no temporary season SHALL remain

#### Scenario: User stops with unmatched runs
- **WHEN** the user chooses to proceed without matching one or more temporary seasons
- **THEN** only confirmed virtual mappings SHALL be downloaded and unmatched ItemIds SHALL receive no write

### Requirement: Complete single-source Season identifier is a write-only display mirror
The plugin SHALL never read a Season plugin identifier or historical manual-binding key as matching evidence and SHALL never clear or delete any identifier. After a complete, current-generation, single-source explicit plan reaches a successful terminal download state, the system MAY upsert only the verified target provider's allowlisted Season key with its canonical Season media identifier for Emby-client display. Successful Episode mappings SHALL continue to write their own provider identifiers for later single-Episode use.

#### Scenario: Complete single-source plan persists its first file
- **WHEN** one stable verified source explicitly maps every eligible Episode, every planned item reaches success or valid-existing-file terminal state, at least one item is successful or validly skipped, and the captured plan generation is still current
- **THEN** the system SHALL upsert only that provider's Season key with its verified canonical media identifier and SHALL leave every other provider, historical Manual, and foreign identifier unchanged

#### Scenario: Partial or multi-source plan persists its first file
- **WHEN** a plan has any unmatched ItemId, skipped-unmapped item, overlap, duplicate, multiple stable sources, or only Episode-level source identity
- **THEN** the system SHALL not write, clear, or delete any Season identifier

#### Scenario: No file is persisted
- **WHEN** validation fails, every download fails, or the task is cancelled before a file write
- **THEN** no Season identifier SHALL be written, cleared, or deleted and Episode identifier writes SHALL occur only for files that actually persisted

#### Scenario: Same provider is matched again
- **WHEN** the current complete single-source plan resolves the same provider as an existing Season key with a different value
- **THEN** only that provider's target key SHALL be overwritten with the new verified canonical value

#### Scenario: Another provider key or historical Manual key exists
- **WHEN** a complete plan writes its target provider key while other provider, Manual, or foreign keys already exist
- **THEN** all non-target keys SHALL remain byte-for-byte unchanged and no additional Manual key SHALL be created or updated

#### Scenario: Automatic and manual choices resolve the same complete source
- **WHEN** an automatic high-confidence choice and a user choice independently complete equivalent downloads
- **THEN** both SHALL be eligible to upsert the same ordinary provider Season key and manual choice SHALL be represented only in audit/provenance, not an extra Manual provider key

#### Scenario: Download task generation is stale
- **WHEN** a newer search, rematch, remove/restore, metadata change, or download intent supersedes the task's captured authoritative-plan generation
- **THEN** the older task SHALL not write, clear, or delete any Season identifier even if its file downloads finish later

#### Scenario: Season identifier upsert fails
- **WHEN** files complete but the target-key metadata upsert fails
- **THEN** downloads SHALL remain successful with an identifier-write warning, every existing identifier SHALL remain unchanged, and the plugin SHALL perform no cleanup or compensating rollback

### Requirement: r4 rejects local-identifier-derived batch selections
The r4 Series/Season protocol SHALL reject a client selection or restored draft whose origin claims `episode-provider-id`, `exact-binding`, or another local-identifier-derived batch mapping. Restore SHALL revalidate only explicit selections created by the current r4 dialog; otherwise the run SHALL return to unmatched.

#### Scenario: Cached r3 draft submits a direct Episode group
- **WHEN** an older browser draft submits a Series/Season selection derived from Episode or Season identifiers
- **THEN** the server SHALL return a structured stale-protocol diagnostic and SHALL perform no binding, download, identifier mutation, or metadata write

#### Scenario: User restores a removed r4 selection
- **WHEN** the current dialog restores a still-verifiable explicit search selection
- **THEN** the server MAY rebuild that selection without consulting local ProviderIds

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

### Requirement: Download-time structural revalidation
The system MUST revalidate ItemId membership, logical-season coordinates, ordering boundaries, selection evidence, and source continuity before writing any file.

#### Scenario: Episode metadata changes after preview
- **WHEN** an ItemId moves logical season, disappears, or invalidates a selected run before download begins
- **THEN** the request SHALL fail with a structured retryable diagnostic and SHALL perform zero metadata or XML writes

### Requirement: Selected Dandan Season maps through the authoritative plan
A manually selected Dandan Season SHALL resolve its verified source Episodes and enter the same authoritative mapping workflow as every other provider, without a positional binding shortcut.

#### Scenario: Seitokai Yakuindomo first Season is selected
- **WHEN** the user selects the Dandan first-Season candidate for the Emby first Season of Seitokai Yakuindomo / Student Council's Discretion
- **THEN** the system SHALL ignore the pre-existing E1 Dandan ProviderId, freshly map local E1 through E13 to selected source E1 through E13, return 13 unique mappings with no unmatched run, and SHALL NOT fail with a duplicate-source or unclassified mapping error
