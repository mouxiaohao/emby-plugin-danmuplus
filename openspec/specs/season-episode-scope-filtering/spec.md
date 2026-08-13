# Season Episode Scope Filtering Specification

## Purpose

Defines one authoritative target-season Episode scope so normal seasons ignore foreign logical-season episodes while standalone Season 0 matching remains complete and safe.

## Requirements

### Requirement: Target-season eligibility is exact
Every batch Season plan SHALL include only Episodes with a valid ItemId and `ParentIndexNumber` exactly equal to the target Season's `IndexNumber`. Episodes with Season 0, another season number, or no parent season number SHALL be out of scope rather than unmatched.

#### Scenario: Normal season contains placed specials
- **WHEN** S1's Emby display inventory contains twelve Parent 1 Episodes and seven Parent 0 Episodes
- **THEN** the S1 eligible set SHALL contain exactly the twelve Parent 1 ItemIds and the seven Parent 0 ItemIds SHALL not enter mappings, unmatched runs, temporary seasons, downloads, or completeness counts

#### Scenario: Normal season contains another normal season
- **WHEN** target S2 contains an Episode whose `ParentIndexNumber` is 1 or 3
- **THEN** that Episode SHALL be excluded exactly like a Parent 0 Episode and SHALL not interrupt the continuous Parent 2 plan

#### Scenario: Parent season is unknown
- **WHEN** an Episode has a null or unusable `ParentIndexNumber`
- **THEN** it SHALL be excluded from batch Season planning and SHALL not be guessed into the target by position, filename, ItemId, or provider identifier

### Requirement: Standalone Season 0 remains matchable
An explicit single-Season operation targeting a real Season whose `IndexNumber` is 0 SHALL be permitted and SHALL include only Episodes whose `ParentIndexNumber` is 0.

#### Scenario: User opens Season 0 directly
- **WHEN** the user starts Season smart matching from the Season 0 item
- **THEN** the system SHALL read that Season 0 item's own Episode inventory, build a plan only from Parent 0 Episodes, and exclude Parent 1, other-season, and unknown-parent Episodes

#### Scenario: Season 0 Episodes are also displayed in a normal Season
- **WHEN** Parent 0 Episodes are additionally visible through another Season's display inventory
- **THEN** standalone Season 0 matching SHALL still use only the real Season 0 item's own inventory and SHALL not merge or borrow the other Season's display inventory

#### Scenario: Target Season inventory is unavailable
- **WHEN** the server cannot obtain a consistent Episode inventory from the explicitly selected Season item
- **THEN** the system SHALL return a structured inventory diagnostic and perform no search, mapping, download, or metadata write for that request

### Requirement: Whole-Series matching skips Season 0
Whole-Series smart matching SHALL enumerate only target Seasons with a known positive `IndexNumber`. Season 0 and unknown-number Seasons SHALL not be searched, scored, rendered as target cards, downloaded, or used for Season identifier writes.

#### Scenario: Series contains Season 0 and normal seasons
- **WHEN** the user starts whole-Series matching
- **THEN** the response SHALL contain only the positive-number Seasons and SHALL contain no Season 0 target result

#### Scenario: Parent 0 Episode is displayed inside S1
- **WHEN** the same Parent 0 ItemId appears in S0 and S1 display inventories during whole-Series planning
- **THEN** it SHALL be out of scope for S1 and the skipped S0 target, so it SHALL be executed zero times by that whole-Series task

### Requirement: Eligible remainders alone become temporary seasons
Explicit virtual mapping SHALL operate only on the target-season eligible set. If a verified source covers fewer eligible Episodes than remain, each maximal continuous eligible remainder SHALL become a temporary season; out-of-scope Episodes SHALL never become temporary seasons.

#### Scenario: Source is shorter than the eligible normal season
- **WHEN** S1 has twelve eligible Parent 1 Episodes and the selected source contains ten Episodes
- **THEN** ten Episodes SHALL be explicitly mapped and the remaining two Parent 1 Episodes SHALL form one temporary season regardless of any foreign Episodes in the display inventory

#### Scenario: All eligible Episodes are covered
- **WHEN** one verified source maps every eligible Parent 1 Episode while the Emby display also contains Parent 0 Episodes
- **THEN** the plan SHALL have no unmatched run and SHALL be complete for target S1

### Requirement: Every batch path uses the same eligible set
Interactive whole-Series and single-Season preview, confidence selection, manual selection, rematch, automatic Season processing, download rebuild, retry, partial confirmation, and Season display-mirror eligibility SHALL use the same target-season eligibility operation and inventory snapshot.

#### Scenario: Series and Season entry points process S1
- **WHEN** both entry points process the same S1 with the same current selections
- **THEN** their eligible ItemIds, search episode count, scores, mappings, temporary runs, executable downloads, and completeness result SHALL be semantically identical

#### Scenario: Automatic normal-season processing runs
- **WHEN** Emby triggers automatic processing for target Season N
- **THEN** it SHALL use only Parent N Episodes and SHALL not adopt foreign Episodes through a separate automatic mapping path

#### Scenario: Automatic Season 0 processing runs
- **WHEN** Emby triggers automatic processing specifically for Season 0
- **THEN** it SHALL use the same Season 0 own-inventory-derived Parent 0 scope as explicit standalone Season 0 matching, while whole-Series interactive matching continues to skip Season 0

### Requirement: Scope changes invalidate a captured plan
The authoritative plan fingerprint SHALL cover the selected Season inventory membership, each observed ItemId's parent season number, the resulting eligibility decision, and all explicit mappings. Download or retry SHALL rebuild and compare this scope before writing.

#### Scenario: Episode moves into the target season after preview
- **WHEN** an Episode changes from Parent 0 to Parent 1 after an S1 preview
- **THEN** the captured S1 plan SHALL be stale and the task SHALL perform zero file and metadata writes

#### Scenario: Episode moves out of the target season after preview
- **WHEN** an eligible Parent 1 Episode changes to Parent 0 or another season before download
- **THEN** the captured plan SHALL be stale and SHALL not silently download the reduced set

### Requirement: Ignored Episodes do not block a complete Season mirror
A terminal complete-single-source Season identifier mirror SHALL evaluate completeness only over the authoritative eligible target-season set. Ignored foreign and unknown-parent Episodes SHALL neither block nor qualify the mirror, and no identifier SHALL be cleared.

#### Scenario: S1 main Episodes complete while S00 is ignored
- **WHEN** every eligible Parent 1 Episode completes from one stable source and placed Parent 0 Episodes are out of scope
- **THEN** the verified target provider's S1 display key MAY be overwritten under the existing generation guard and every non-target key SHALL remain unchanged

#### Scenario: Eligible target Episodes remain unmatched
- **WHEN** any Parent N Episode remains unmatched, fails, is cancelled, or the scope fingerprint is stale
- **THEN** no Season identifier SHALL be written even if every foreign Episode was ignored

### Requirement: r5 rejects older cross-season drafts
The r5 batch protocol SHALL use a new version and frontend cache marker. A draft or download request created by r4/V20 that contains a supplemental foreign-season selection or lacks the r5 scope generation SHALL be rejected without writes.

#### Scenario: V20 draft contains S00 supplemental mapping
- **WHEN** the r5 server receives an older draft that attempts to map an S00 ItemId through an S1 plan
- **THEN** it SHALL return a structured stale-protocol diagnostic and perform no download, binding, or metadata write
