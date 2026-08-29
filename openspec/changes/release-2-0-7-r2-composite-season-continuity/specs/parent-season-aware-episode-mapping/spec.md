## ADDED Requirements

### Requirement: Cross-target continuation is server-authored animated whole-Series evidence
Every derived cross-local-Season logical ordinal and Provider lock SHALL be backed by server-owned evidence scoped to one animated whole-Series operation. The evidence SHALL identify the Series, the whole-Series/animation eligibility decision, the immediately preceding and current positive local Season ItemIds and ordinals, the predecessor plan generation and fingerprints, the predecessor initial and terminal logical ordinals, the ProviderId that owns the active chain, and proof that the predecessor plan covers every eligible local ItemId exactly once.

The evidence SHALL distinguish a chain activated by two or more logical Season ordinals from any number of Part segments inside one logical Season. Part ordinal and segment count MUST NOT participate in logical Season arithmetic. The derived initial logical ordinal and required ProviderId SHALL be retained with every current-target candidate evidence token and SHALL be used when rebuilding that target's first segment and recursive remainder chain. Browser JSON, display fields, local ProviderIds, saved bindings, historical downloads, candidate categories, and single-target requests MUST NOT create, add, remove, or change those facts.

#### Scenario: Browser submits a different continuation ordinal
- **WHEN** a client payload claims that the current local Season starts at a logical Season number not present in its target-bound server evidence
- **THEN** the server SHALL reject the selection as stale or invalid and SHALL perform zero downloads and zero metadata writes

#### Scenario: Browser submits a different Provider
- **WHEN** target-bound continuation evidence requires Provider A but a client submits a Provider B candidate or token
- **THEN** the server SHALL reject the selection before source resolution and SHALL perform zero upstream Provider B calls, downloads, and metadata writes

#### Scenario: Single-Season request cannot reuse whole-Series evidence
- **WHEN** a user later opens one local Season and submits a token or display value copied from an earlier whole-Series continuation
- **THEN** the single-target operation MUST NOT derive or accept cross-Season continuation from that browser state

#### Scenario: Two Parts do not author a logical offset
- **WHEN** a predecessor plan contains logical Season 1 Part 1 and Part 2 but no logical Season 2 segment
- **THEN** its evidence SHALL retain terminal logical Season 1, MUST NOT mark the cross-Season chain active, and MUST NOT author an expectation for Part 3 or logical Season 3 on local S2

#### Scenario: N Parts retain one logical terminal
- **WHEN** a predecessor plan contains N Part decisions and all N decisions identify the same active logical Season K
- **THEN** authoritative evidence SHALL retain terminal logical Season K independently of N and MUST NOT derive K+N or Part N+1 as the next local Season target

#### Scenario: Ordinary whole-Series candidate has no active chain
- **WHEN** no complete multi-logical-Season predecessor has activated continuation for a target
- **THEN** its candidate evidence SHALL use the real positive local Season ordinal, SHALL have no required cross-Season Provider, and SHALL not manufacture a different logical Season number

### Requirement: Active continuation evidence propagates only through complete adjacent targets
After a complete multi-logical-Season target activates a chain, each complete immediately adjacent continuation target SHALL produce a new server-owned proof containing its actual terminal logical Season and the unchanged required ProviderId. This propagation SHALL remain active even when that target contains only one logical Season segment. A gap, cancelled/failed/ambiguous/stale result, unmatched eligible ItemId, contradictory logical evidence, or Provider mismatch MUST prevent that target from authoring the next proof.

#### Scenario: Single-segment continuation advances the chain
- **WHEN** local S1 activates a Provider-A chain by ending at logical Season 3 and local S2 completes on Provider A with only logical Season 4
- **THEN** local S2 SHALL author a proof that starts and ends at logical Season 4 so immediately adjacent local S3 can expect logical Season 5 on Provider A

#### Scenario: Gap resets authority
- **WHEN** the last proof belongs to local S1 and the next enumerated target is local S3
- **THEN** the S1 proof MUST NOT be accepted for S3 and S3 SHALL have no active continuation context

#### Scenario: Partial continuation cannot advance
- **WHEN** a continuation-adjusted current target maps only part of its eligible inventory
- **THEN** its confirmed mappings SHALL remain target-local, its unmatched ItemIds SHALL remain unmatched, and it MUST NOT author a terminal continuation for the next local Season

### Requirement: Continuation proof is revalidated before plan execution
Before a continuation-adjusted preview is committed to a download task or any file/metadata write occurs, the system MUST verify that the operation still qualifies as an animated whole-Series target, the candidate evidence belongs to the current target and generation, the predecessor generation/fingerprints and exact-parent inventory remain current, and every selected/rebuilt source belongs to the proof's required ProviderId. A changed animation classification, target inventory, expired or mismatched candidate token, contradictory initial ordinal, changed predecessor proof, different Provider, or inconsistent recursive logical-Season evidence SHALL make the plan stale and SHALL produce zero writes.

#### Scenario: Target generation changes after preview
- **WHEN** a continuation-adjusted candidate was selected under one target generation and a newer search or metadata change supersedes that generation before download
- **THEN** the older task SHALL be rejected and SHALL perform zero XML, identifier, or metadata writes

#### Scenario: Predecessor proof changes after preview
- **WHEN** the predecessor structure, generation, plan fingerprint, terminal logical Season, or required Provider changes before the current target executes
- **THEN** the current continuation plan SHALL be stale and no write SHALL occur

#### Scenario: Animation eligibility changes after preview
- **WHEN** the Series no longer satisfies the server-owned animation classification used to authorize the continuation preview
- **THEN** the continuation plan SHALL be stale and MUST be previewed again under ordinary behavior

#### Scenario: Remainder evidence starts from the local ordinal
- **WHEN** a current target was proven to begin at logical Season 4 but submitted or rebuilt remainder evidence begins from local logical Season 2
- **THEN** the plan SHALL be invalid and no mapping or write SHALL be committed

#### Scenario: Complete proof remains unchanged
- **WHEN** whole-Series/animation eligibility, predecessor proof, required Provider, current target generation, candidate evidence, source details, anchors, and target inventory all remain authoritative
- **THEN** preview and download-time rebuild SHALL reproduce the same initial logical ordinal, Provider, exact mappings, remainder decisions, and plan fingerprint

### Requirement: Cross-Season continuation carries Provider identity but not source ownership
An active chain SHALL carry exactly two cross-target matching constraints: the next logical Season ordinal and the required ProviderId. It MUST NOT copy the predecessor's CandidateId, media identity, local-to-source mappings, Episode identifiers, exclusions, Emby ownership, or write decision into the current target. Each local Season SHALL retain its own exact-parent Episode scope, plan generation, candidate/media validation, composite plan, download set, and identifier-write decision.

#### Scenario: Adjacent Seasons contain duplicate Episode numbers
- **WHEN** the predecessor and current local Seasons both contain E1 through E12
- **THEN** continuation MAY change the current source logical ordinal and constrain its Provider, but every mapping SHALL still use only the current Season's own eligible ItemIds

#### Scenario: Same Provider uses a new media identity
- **WHEN** the predecessor ends on Provider A media ID 100 and the next logical Season is Provider A media ID 200
- **THEN** media ID 200 MAY be selected after independent current-target validation and media ID 100 MUST NOT be copied into the target

#### Scenario: Different Provider candidate is available
- **WHEN** an active continuation is locked to Provider A and a Provider B candidate otherwise resembles the expected Season
- **THEN** Provider B MUST remain ineligible for that target and MUST NOT become its first-segment or remainder Provider

#### Scenario: Current Season remains independently writable
- **WHEN** a continuation-adjusted target completes on the required Provider
- **THEN** its binding/composite safety decision SHALL be based only on its own mappings and source identities, not the predecessor's write eligibility
