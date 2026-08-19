## ADDED Requirements

### Requirement: Remainder recursion termination is presented as unmatched state
When the initial interactive Season segment has succeeded, termination of automatic remainder recursion SHALL preserve the visible successful mappings and SHALL present every remaining `UnmatchedRuns` group as unmatched or available for manual continuation. A determinate no-selection, candidate safety-gate rejection, ambiguity, exhausted candidate set, cancellation, timeout, provider failure, unavailable details, incomplete evidence, stale generation, or no-progress result arising only from remainder recursion MUST NOT change the whole Season or whole-Series result to a top-level failed state and MUST NOT trigger a top-level matching-failed dialog. This silent partial-state rule SHALL NOT suppress an error that prevents the initial requested Season match from producing any authoritative result.

#### Scenario: No later Part exists
- **WHEN** the first segment is successfully mapped and recursive search finds no eligible continuous next Part or other trustworthy fallback
- **THEN** the mapped segment SHALL remain visible, the remainder SHALL appear unmatched, and no top-level matching-failed state or error dialog SHALL appear

#### Scenario: Explicit next Part is ambiguous
- **WHEN** a successful partial Season is followed by more than one otherwise eligible next-Part candidate
- **THEN** the UI SHALL retain the partial mappings and unmatched remainder without displaying a top-level failure or showing either candidate as automatically bound

#### Scenario: Provider detail fails during recursion
- **WHEN** a provider failure, timeout, cancellation, or incomplete detail response stops a later recursive round after at least one segment is confirmed
- **THEN** the current partial result SHALL remain visible with its unmatched groups and remainder termination alone SHALL not render the result as failed

#### Scenario: Only another provider has a continuation
- **WHEN** the locked Provider has no trustworthy continuation and only another Provider exposes an otherwise matching remainder source
- **THEN** the confirmed prefix SHALL remain visible, the remainder SHALL stay unmatched, and the cross-Provider safety stop SHALL not produce a top-level failure

#### Scenario: Initial match fails
- **WHEN** the initial requested Season match cannot produce an authoritative result before any segment is confirmed
- **THEN** the existing initial-match error and diagnostic presentation rules SHALL remain applicable

### Requirement: Unique partless count mismatch is a yellow advisory
When the server authoritatively selects the sole eligible partless remainder candidate because its year matches while its verified source Episode count differs from the eligible local remainder count, the browser SHALL keep that segment in the successful matched state and SHALL show one localized Episode-count-mismatch advisory in the same yellow warning treatment used by other Season Episode-count guidance. The warning SHALL be based only on the authoritative selection state and MUST NOT be inferred from search-result metadata or browser-side counting. A rematch or rebuild SHALL replace the warning with the current authoritative state and MUST NOT retain or duplicate stale warnings.

#### Scenario: Sole year match has fewer source Episodes
- **WHEN** the sole eligible partless candidate is authoritatively selected with a matching year and a verified source count smaller than the local remainder count
- **THEN** the matched segment SHALL remain successful, one yellow count-mismatch advisory SHALL appear, and any still-unmapped Episodes SHALL remain available as an unmatched run

#### Scenario: Sole year match has more source Episodes
- **WHEN** the sole eligible partless candidate is authoritatively selected with a matching year and a verified source count larger than the local remainder count
- **THEN** the matched segment SHALL remain successful and one yellow count-mismatch advisory SHALL appear without creating synthetic local Episodes

#### Scenario: Candidate count matches
- **WHEN** the selected remainder candidate's verified source Episode count equals the eligible local remainder count
- **THEN** the unique-partless count-mismatch advisory SHALL not appear

#### Scenario: Browser sees only unverified count metadata
- **WHEN** search-result metadata suggests a count difference but no authoritative recursive selection establishes the mismatch state
- **THEN** the browser SHALL not display the unique-partless count-mismatch advisory

#### Scenario: Remainder selection is rebuilt
- **WHEN** a rematch or download-time rebuild changes or removes the authoritative count-mismatch state
- **THEN** the current result SHALL show exactly the warning implied by the rebuilt state and SHALL contain no warning retained from the superseded selection
