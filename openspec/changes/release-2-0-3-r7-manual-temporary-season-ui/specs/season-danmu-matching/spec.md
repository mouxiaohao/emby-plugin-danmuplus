## ADDED Requirements

### Requirement: Unified temporary-season manual matching
Whole-Series and direct single-Season smart matching SHALL use the same `手动匹配未匹配临时季` candidate menu when the user manually matches an unmatched temporary run or rematches an existing mapped temporary run. Entering or searching this menu MUST NOT change, remove, or replace the authoritative mapping until the user explicitly applies a selected candidate and the server validates the replacement.

#### Scenario: User matches an unmatched run
- **WHEN** the user clicks manual match for an unmatched temporary run
- **THEN** the dialog SHALL open `手动匹配未匹配临时季` for exactly that run and retain the existing authoritative mappings outside the run

#### Scenario: User rematches a mapped run
- **WHEN** the user clicks rematch for an existing mapped temporary run
- **THEN** the dialog SHALL open the same `手动匹配未匹配临时季` menu for exactly that run without changing the existing mapping before confirmation

#### Scenario: Entry points target the same run
- **WHEN** whole-Series and direct single-Season workflows open the same target Season and temporary run
- **THEN** the menu title, candidate presentation, lazy-detail behavior, search scope, and back navigation SHALL be equivalent

### Requirement: Lazy temporary-season candidate inspection
Every candidate in the shared temporary-season manual matching menu SHALL expose `解析并查看详情`. The menu MUST make zero candidate-detail requests before an explicit click, and clicking one control SHALL resolve and expand only that candidate's numbered source Episode titles without selecting, confirming, mapping, binding, persisting, or downloading it.

#### Scenario: Temporary-season candidates are first rendered
- **WHEN** a temporary-range search returns one or more candidates
- **THEN** every candidate SHALL have a detail control and no candidate source details SHALL have been requested

#### Scenario: User inspects one temporary-season candidate
- **WHEN** the user clicks candidate A's detail control
- **THEN** only candidate A SHALL be requested and expanded while every other candidate remains unresolved and collapsed

#### Scenario: User changes run or repeats search
- **WHEN** a detail response from an older run, plan generation, search, or evidence token completes after the menu context changed
- **THEN** the stale response SHALL NOT render into the current candidate list

### Requirement: Verified titles accompany temporary-season mappings
An authoritative temporary-season preview SHALL carry the existing local library Episode title and the verified source Episode title needed for public mapping details. Title transport MUST NOT cause an additional provider media-detail request and titles MUST NOT participate in candidate scoring, source identity, selection evidence, plan fingerprinting, target scope, or download validation.

#### Scenario: Provider resolution supplies Episode titles
- **WHEN** a confirmed temporary-season mapping is built from provider Episodes containing titles
- **THEN** the preview SHALL associate each mapped row with its local and source titles without a second provider resolution

#### Scenario: A title is missing
- **WHEN** either the local or source Episode title is empty or unavailable
- **THEN** the mapping SHALL remain valid and the public presentation SHALL fall back to its season/Episode and source-number labels
