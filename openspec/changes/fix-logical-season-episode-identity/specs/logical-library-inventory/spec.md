## Purpose

Ensure multiple physical video versions do not multiply logical season targets, episode matching slots, or downloads beyond the identities displayed by Emby.

## ADDED Requirements

### Requirement: Whole-Series targets use Emby logical seasons
Interactive and background whole-Series processing SHALL enumerate each Emby logical positive-number Season once. Different physical Season entries representing the same Emby presentation SHALL not produce duplicate cards or matching tasks. Known S00 and unknown-number targets SHALL retain the existing whole-Series exclusion.

#### Scenario: Multiple versions occupy duplicate Season entries
- **WHEN** a Series has three logical regular Seasons, with duplicate physical entries for its first two Seasons
- **THEN** whole-Series matching SHALL return three Season targets rather than five

### Requirement: Planning and execution use logical episodes
All Season preview, scoring, automatic processing, selection, rematch, remainder planning, download rebuild, and execution SHALL use one Emby-selected representative per logical Episode. The system SHALL preserve Emby ordering and SHALL NOT infer version equivalence solely from filenames, resolution, or equal episode numbers.

#### Scenario: Twelve episodes have two physical versions each
- **WHEN** an Emby logical Season contains twelve Episodes with two versions of each and a twelve-episode source is selected
- **THEN** the system SHALL count and map twelve Episodes one-to-one, produce no artificial remainder, and execute no more than twelve logical downloads

#### Scenario: Source covers only ten logical episodes
- **WHEN** that twelve-Episode Season is matched with a ten-Episode source
- **THEN** the remainder SHALL contain logical Episodes 11 and 12 once each, irrespective of their physical version counts

#### Scenario: Different identities share an episode number
- **WHEN** two items have different Emby logical presentation identities but the same season and episode numbers
- **THEN** the plugin SHALL NOT silently discard either item based only on those numbers

### Requirement: Logical grouping preserves scope and stale-plan protection
Grouping SHALL precede existing exact-parent Season eligibility. Explicit S00 matching SHALL remain available. Downloads SHALL use the same logical representatives as the verified plan and SHALL reject a stale representative or scope without writes.

#### Scenario: Normal Season includes placed specials
- **WHEN** S1 contains twelve logical Parent 1 Episodes and two logical Parent 0 Episodes
- **THEN** S1 SHALL map only the twelve Parent 1 Episodes, while explicit S00 matching SHALL process only its own Parent 0 inventory

#### Scenario: Representative changes after preview
- **WHEN** Emby changes the representative ItemId of a planned logical Episode before download
- **THEN** the plan fingerprint SHALL change and the previous plan SHALL not silently write to the replacement item

### Requirement: Existing XML persistence and player loading remain unchanged
Each logical Episode SHALL be sent to the existing download and XML-persistence path once. The fix SHALL NOT fan downloads out to physical versions or alter the server-XML loading rules of dd-danmaku.

#### Scenario: Either version plays using existing server XML
- **WHEN** the user plays either physical version of a logical Episode
- **THEN** the existing Emby/player XML-loading mechanism SHALL remain unchanged by this fix
