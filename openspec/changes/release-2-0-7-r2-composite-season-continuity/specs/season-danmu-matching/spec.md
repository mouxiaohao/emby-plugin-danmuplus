## ADDED Requirements

### Requirement: Animated whole-Series matching continues authoritative logical Season ordinals
For a user-initiated whole-Series match whose target is a server-recognized animated Series, the system SHALL process all known positive local Seasons in ascending order. Before a continuation chain is active, each local Season SHALL retain the existing independent local-ordinal search behavior. A complete current server-authored plan that contains two or more distinct logical Season ordinals SHALL activate a continuation chain. When that local Season ends at logical Season M, its immediately following local Season SHALL search and score its initial segment as logical Season M+1 while remaining the real Emby local Season.

Once activated, the chain SHALL remain active across every immediately adjacent target that completes authoritatively, even when an intermediate local Season contains only one logical Season segment. Each complete target SHALL pass its own terminal logical Season to the next target. The derived ordinal SHALL replace the local ordinal only for source discovery, Season-number scoring, automatic selection, candidate evidence, and the active logical-Season start of remainder recursion. The response and every local Episode scope SHALL continue to identify the real Emby local Season, and the current target's own year and eligible Episode count SHALL remain its year/count evidence.

#### Scenario: Bookworm first local Season contains three source Seasons
- **WHEN** an animated whole-Series match maps local `爱书的下克上` S1 completely and authoritatively to source logical Seasons 1, 2, and 3 and local S2 is the next positive Season
- **THEN** local S2 SHALL search and score its first segment as source logical Season 4 while remaining Emby local S2

#### Scenario: Continuation advances through later local Seasons
- **WHEN** an active chain gives local S2 initial logical Season 4, local S2 completes at logical Season 4, and local S3 is immediately adjacent
- **THEN** local S3 SHALL start at logical Season 5 even though local S2 itself contained only one logical Season segment

#### Scenario: A later local Season activates the chain
- **WHEN** earlier local Seasons were independently matched and local S4 later completes as a collection of logical Seasons 4 through 6
- **THEN** immediately adjacent local S5 SHALL start at logical Season 7 under the same general rule

#### Scenario: Parts do not activate logical Season continuation
- **WHEN** a local Season is completely mapped to any positive number of Part segments that all belong to one logical Season, without any later logical Season segment
- **THEN** that result MUST NOT activate a cross-local-Season continuation chain and the next local Season SHALL retain ordinary independent matching

#### Scenario: Arbitrary Part count never changes the terminal logical Season
- **WHEN** a local physical Season contains N confirmed Part segments and every Part belongs to logical Season K
- **THEN** the terminal logical Season SHALL remain K regardless of N; an already-active chain SHALL make the next adjacent local Season expect K+1, while an inactive chain SHALL leave the next local Season on ordinary local-ordinal matching, and neither path MAY infer logical Season K+N or Part N+1

#### Scenario: Two Parts in local S1 do not turn local S2 into Part 3 or Season 3
- **WHEN** animated whole-Series local S1 is completely mapped to logical Season 1 Part 1 and Part 2 and local S2 is immediately adjacent
- **THEN** S1's terminal logical Season SHALL remain 1, local S2 SHALL use ordinary logical Season 2 matching, and the system MUST NOT classify local S2 as Season 1 Part 3 or logical Season 3 merely because S1 contained two Part segments

#### Scenario: Positive local Season number is not consecutive
- **WHEN** an active result belongs to local S1 but the next processed local target is S3 because S2 is absent
- **THEN** the system MUST NOT bridge the gap, SHALL clear the continuation chain, and SHALL process S3 under the existing independent local-ordinal behavior

#### Scenario: Previous result cannot author continuation
- **WHEN** a would-be predecessor is cancelled, ambiguous, unmatched, partial, stale, or leaves any eligible local Episode unmatched
- **THEN** it MUST NOT activate or advance a continuation chain

### Requirement: Continuation is limited to animated full-Series interactive matching
Cross-local-Season continuation SHALL run only when the user explicitly targets the animated Series as a whole and the server enumerates its positive Seasons as one target set. Animation eligibility MUST come from server-owned media metadata recognized by the existing animation classification policy. A non-animated Series, a Series request narrowed to one Season context, an explicitly targeted Season, manual-keyword single-target discovery, Season 0, and every unattended path MUST NOT inspect another local Season or derive a cross-Season ordinal.

This limitation SHALL NOT remove 2.0.7 recursive remainder matching inside an explicitly targeted Season; it prevents only the new 2.0.7r2 cross-local-Season continuation.

#### Scenario: Animated Series whole-match enables the rule
- **WHEN** a user starts matching on a Series whose authoritative genre metadata is recognized as animation and the request targets all positive Seasons
- **THEN** complete multi-logical-Season results MAY activate the adjacent continuation chain

#### Scenario: Non-animated Series has the same Season shape
- **WHEN** a non-animated Series has a local Season whose plan contains multiple logical-looking segments
- **THEN** 2.0.7r2 MUST NOT carry an ordinal or Provider lock to another local Season

#### Scenario: Animation metadata is absent
- **WHEN** the Series has no server-recognized animation metadata at the start of the request
- **THEN** the system SHALL retain ordinary independent whole-Series matching and MUST NOT infer animation from a browser field or candidate category

#### Scenario: User opens only Bookworm local S2
- **WHEN** the user explicitly targets local `爱书的下克上` S2 instead of starting a whole-Series match
- **THEN** the request MUST NOT search or preview S1 and SHALL retain the existing target-local S2 behavior

#### Scenario: Series request is narrowed to one Season
- **WHEN** a Series request carries authoritative Season context that selects exactly one local Season
- **THEN** it SHALL be treated as a single-target request for this rule and MUST NOT evaluate predecessor Seasons

### Requirement: Continuation-adjusted Seasons use the same source Provider
When a complete multi-logical-Season result activates continuation, the system SHALL bind the chain to that result's server-verified ProviderId. Every continuation-adjusted target SHALL search, score, retain, and select candidates only from that Provider, including standard and TMDB-alias discovery rounds. A different CandidateId or media identifier on the locked Provider SHALL be allowed because each source Season is a distinct provider record; only the ProviderId is shared across local Season boundaries.

The initial chain-activating Season SHALL still use the existing global candidate scoring and configured site-priority rules. If the locked Provider is disabled, fails, is cancelled, returns no valid candidate, or returns no candidate meeting the existing automatic confidence rules, the continuation target SHALL remain unmatched. It MUST NOT retry another Provider, use a different-Provider candidate retained from an earlier round, or fall back to the local Season ordinal within that continuation attempt.

#### Scenario: Bookworm continues on DandanPlay
- **WHEN** DandanPlay wins the existing priority/ranking rules for animated `爱书的下克上` local S1, TMDB alias discovery finds `小书痴的下克上`, and S1 completes on DandanPlay as logical Seasons 1 through 3
- **THEN** local S2 SHALL search only DandanPlay for logical Season 4 and SHALL auto-select DandanPlay `小书痴的下克上 第四季` when it meets the existing confidence rule

#### Scenario: Fourth Season uses a different media identifier
- **WHEN** the locked Provider represents `小书痴的下克上 第四季` with a CandidateId/MediaId different from the first three source Seasons
- **THEN** the new identifier SHALL be eligible because it belongs to the same locked Provider and the current target retains independent source identity

#### Scenario: Another Provider has a stronger-looking candidate
- **WHEN** a continuation chain is locked to Provider A and Provider B could return a title/year/count candidate with an equal or higher score
- **THEN** Provider B MUST NOT participate in the continuation target's search or selection and only Provider A MAY supply the result

#### Scenario: Locked Provider cannot confirm the continuation
- **WHEN** a continuation target expects logical Season 4 on Provider A but Provider A cannot produce one high-confidence valid candidate
- **THEN** the target SHALL remain unmatched and MUST NOT select Provider B or retry the target as its local Season number

#### Scenario: Wrong logical Season is returned by the locked Provider
- **WHEN** the locked Provider's standard or TMDB-alias round returns an otherwise related candidate that explicitly identifies Season 2 or Season 3 while the target expects Season 4
- **THEN** the candidate SHALL fail the existing explicit-Season compatibility rules and MUST NOT be selected

#### Scenario: No locked-Provider alias reaches confidence
- **WHEN** standard and TMDB-alias rounds on the locked Provider complete for the derived logical Season but no candidate satisfies the automatic-selection rule
- **THEN** only same-Provider candidates MAY remain available for existing review behavior and the target MUST remain unmatched

### Requirement: Cross-Season continuation failure remains non-destructive
Failure to activate or advance a continuation chain SHALL NOT create a top-level synthetic matching failure, bind an arbitrary candidate, or write any cross-Season state. The affected target SHALL use the existing unmatched/partial presentation appropriate to its authoritative plan. A failed continuation target MUST NOT author a terminal ordinal for the next local Season; subsequent processing has no continuation proof unless a later independent complete multi-logical-Season target activates a new chain.

#### Scenario: Active continuation target is partial
- **WHEN** a locked-Provider continuation target confirms only part of its eligible Episode inventory
- **THEN** its confirmed target-local mappings MAY be retained under existing partial behavior, its remainder SHALL stay unmatched, and it MUST NOT advance the chain

#### Scenario: Continuation target is cancelled
- **WHEN** the user cancels while a continuation-adjusted target is being searched or rebuilt
- **THEN** no provisional logical ordinal or Provider lock from that target SHALL be used for a following local Season

### Requirement: Background paths remain independent and non-recursive across local Seasons
Media import, item-added handling, automatic download, retry, replay, explicitly targeted Season matching, and every other non-whole-Series path MUST NOT evaluate another local Season to derive a logical ordinal or Provider lock. Those paths SHALL retain their existing target-local Season number, Season 0 policy, first-segment provider behavior, and existing 2.0.7 remainder-recursion policy for their own target.

#### Scenario: New local S2 is added after a composite S1
- **WHEN** Emby raises an unattended item-added event for local S2 while local S1 could represent several source Seasons
- **THEN** the event MUST NOT search or preview S1 and MUST process only S2 under the existing background-local rules

#### Scenario: Whole-Series contains Season 0
- **WHEN** an interactive animated whole-Series request contains Season 0 and positive Seasons
- **THEN** Season 0 SHALL remain excluded before search and SHALL NOT participate in, activate, carry, or reset the positive-Season continuation chain
