## MODIFIED Requirements

### Requirement: Every Season candidate is an explicit virtual mapping
Every confidence-selected, manually selected, and recursively auto-selected Season candidate SHALL be resolved to verified source Episodes and applied through the same authoritative explicit virtual-season planner. The system SHALL NOT use a raw Season bind or positional download shortcut, including when one source covers every local Episode. Each recursively auto-selected source SHALL use `ExplicitAnchor` semantics: the first eligible local Episode in the current unmatched run SHALL be anchored to the exact first verified source Episode selected for that segment, reliable numeric coordinates SHALL preserve the resulting number delta and gaps, and a segment with unreliable numbering SHALL use the existing deterministic positional behavior from those exact anchors. A recursively auto-selected segment MUST NOT inherit a default zero offset or a source position from the preceding source.

#### Scenario: Selected source covers all local Episodes
- **WHEN** a confidence-selected, manual, or recursively auto-selected source maps every eligible local Episode
- **THEN** the authoritative plan SHALL contain its explicit mappings, no unmatched run SHALL remain, and download SHALL execute that plan

#### Scenario: Selected source covers only part of a Season
- **WHEN** verified source Episodes are exhausted while eligible local Episodes remain
- **THEN** every remaining maximal contiguous local run SHALL become an `UnmatchedRuns` temporary season and an eligible interactive operation SHALL evaluate that run for another automatic or manual source selection

#### Scenario: Automatic remainder source starts from Episode 1
- **WHEN** a local remainder begins at E29 and its recursively selected verified source begins at source E1
- **THEN** the segment SHALL explicitly anchor local E29 to source E1 and SHALL map later reliable coordinates from that delta rather than treating source E1 as local E1

#### Scenario: Automatic remainder numbering is unreliable
- **WHEN** either side of a recursively selected segment lacks reliable unique positive Episode numbers
- **THEN** the whole segment SHALL use deterministic positional mapping from its exact local and source anchors and SHALL NOT mix numeric and positional pairs

#### Scenario: Multiple rounds complete the Season
- **WHEN** recursive automatic selection and any user-confirmed selections cover every eligible ItemId
- **THEN** all confirmed virtual mappings SHALL coexist in one authoritative plan and no temporary season SHALL remain

#### Scenario: User stops with unmatched runs
- **WHEN** the user proceeds without matching one or more temporary seasons or recursive automatic matching stops without a trustworthy selection
- **THEN** only confirmed virtual mappings SHALL be downloaded and unmatched ItemIds SHALL receive no write

## ADDED Requirements

### Requirement: Recursive selections carry authoritative evidence
Every recursively auto-selected remainder source SHALL be represented by server-owned evidence scoped to the current target Season, authoritative-plan generation, unmatched run, immutable first-segment Provider lock, stable provider source identity, verified source Episode inventory, selection stage, and the facts that justified that stage. Every automatic remainder source MUST belong to that locked Provider. Part evidence SHALL retain the parsed continuous Part number; partless evidence SHALL retain the comparison year, local remainder count, verified source count, same-Provider uniqueness result, and any count-mismatch state; next-logical-Season evidence SHALL retain the parent-title, logical Season number, remainder-first year, component scores, final score, and the same Provider lock. The resulting selection SHALL contain exact local and source anchors and exact per-Episode mappings. Client-supplied display values MUST NOT create or alter this evidence.

Before download or metadata writes, the system MUST rebuild every recursively selected segment from current authoritative local inventory and current verified source details. It MUST prove that the first segment still establishes the same Provider lock and that every recursive selection, lookup identity, stable identity, and source inventory still belongs to that Provider. Changed, missing, stale, incomplete, cross-Provider, or conflicting evidence or mappings SHALL invalidate the plan and SHALL produce zero writes for that stale execution.

#### Scenario: Remainder provider drifts after preview
- **WHEN** rebuild finds that the initial Provider lock changed or any recursive source now resolves under a different Provider
- **THEN** the plan SHALL be stale and execution SHALL perform zero XML and metadata writes

#### Scenario: Browser changes a displayed score
- **WHEN** a submitted remainder selection changes a score, Part number, year, Episode count, or source label that is not backed by the server-owned selection evidence
- **THEN** the server SHALL reject the selection and SHALL perform no download or metadata write

#### Scenario: Provider details change after preview
- **WHEN** download-time rebuild finds a different source Episode inventory, source anchor, stable source identity, or exact mapping from the confirmed recursive preview
- **THEN** the plan SHALL be stale and the execution SHALL perform zero XML and metadata writes

#### Scenario: Count-mismatch selection is rebuilt
- **WHEN** a unique partless remainder source was selected with matching year and mismatched Episode count and all authoritative facts remain unchanged
- **THEN** rebuild SHALL reproduce the same explicit mappings and the same count-mismatch warning state

### Requirement: Recursive plans enforce source uniqueness and strict progress
A stable provider source identity SHALL appear at most once in an authoritative Season plan. A recursive round SHALL be committed only when its exact new mappings contain at least one previously unmatched eligible ItemId and make the count of unmatched eligible ItemIds strictly smaller. The next round SHALL operate only on the newly rebuilt `UnmatchedRuns`; it MUST NOT remap a confirmed ItemId, consume an out-of-scope ItemId, reuse a source, or continue after a no-progress result. These invariants SHALL bound recursion to no more successful rounds than the number of eligible local Episodes.

#### Scenario: Candidate source was used by the first segment
- **WHEN** a later recursive decision resolves to the same stable provider source identity as the first segment
- **THEN** the source SHALL be rejected and the existing mappings SHALL remain unchanged

#### Scenario: Selection maps no new ItemId
- **WHEN** a candidate produces zero exact mappings for the current unmatched run
- **THEN** the round SHALL NOT be committed and recursion SHALL stop without repeating that run

#### Scenario: Successful round reduces the remainder
- **WHEN** a recursive segment maps four previously unmatched eligible ItemIds and leaves six eligible ItemIds unmatched
- **THEN** the authoritative plan SHALL retain the four new mappings and the next round SHALL evaluate only the rebuilt six-ItemId `UnmatchedRuns`

#### Scenario: Candidate overlaps a confirmed mapping
- **WHEN** a recursive candidate would remap an ItemId already owned by a confirmed segment
- **THEN** the candidate SHALL fail closed and SHALL NOT replace, duplicate, or shift the confirmed mapping

### Requirement: Recursive termination preserves partial authoritative plans
A determinate no-selection, safety-gate rejection, ambiguity, exhausted candidate set, cancellation, timeout, provider failure, unavailable detail, incomplete evidence, stale generation, or no-progress result SHALL terminate recursive remainder processing. Termination MUST NOT discard or rewrite any earlier confirmed mapping. The returned authoritative plan SHALL preserve all confirmed mappings and SHALL expose every still-eligible uncovered maximal run through `UnmatchedRuns`; no unmatched ItemId SHALL be synthesized into a binding or download entry.

#### Scenario: Part ambiguity occurs after two successful segments
- **WHEN** two segments are already confirmed and the next continuous Part is ambiguous
- **THEN** both confirmed segments SHALL remain in the plan and the uncovered eligible remainder SHALL remain in `UnmatchedRuns`

#### Scenario: Provider fails during a later round
- **WHEN** authoritative source verification fails after an earlier recursive round succeeded
- **THEN** the earlier mappings SHALL remain confirmed, recursion SHALL stop, and the remaining eligible ItemIds SHALL stay unmatched

#### Scenario: Recursion exhausts all eligible Episodes
- **WHEN** a successful round leaves zero unmatched eligible ItemIds
- **THEN** recursion SHALL terminate with the complete authoritative plan and an empty `UnmatchedRuns` collection
