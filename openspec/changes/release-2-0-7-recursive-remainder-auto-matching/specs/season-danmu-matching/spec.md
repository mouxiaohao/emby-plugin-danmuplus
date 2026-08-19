## MODIFIED Requirements

### Requirement: Shared manual and automatic matching behavior
Manual whole-Series preview, manual single-Season preview, confidence-selected and manually selected candidate confirmation, newly added positive-number Season processing, and download-time rebuild SHALL use the same identifier-free cross-provider search rules and the same target-season-scoped authoritative virtual Episode-plan operation. Whole-Series matching SHALL only enumerate and aggregate known positive-number target Seasons; it MUST NOT apply a different Episode ordering, grouping, alignment, mapping, or persistence path. No Season source, including a complete single-source result, may bypass explicit virtual mapping. Only an explicitly user-targeted standalone Season 0 operation SHALL process Season 0; whole-Series and unattended/media-import paths SHALL skip Season 0 before provider search, selection, planning, binding, download, or metadata write. Only user-initiated whole-Series matching for each positive-number Season and user-initiated explicit single-Season matching, including an explicitly targeted Season 0, SHALL recursively discover sources for an eligible remainder after the first segment succeeds. Media import, item-added handling, retries, replay, and every other unattended or background positive-Season path MUST stop after its initial Season selection and MUST NOT perform remainder discovery. Automatic processing SHALL remain fail-closed on incomplete, stale, or structurally ambiguous plans.

The shared planner SHALL preserve reliable sparse Episode coordinates. For the same target inventory and trusted selections, whole-Series, single-Season, automatic positive-Season processing, and rebuild SHALL choose the same zero-offset or explicit-anchor numeric alignment, the same whole-segment positional fallback, the same exact mappings, and the same unmatched runs. Recursive selection MUST operate only on the authoritative unmatched suffix produced by those rules and MUST NOT compress a sparse gap or reinterpret an internal unmatched coordinate as a continuation suffix.

#### Scenario: New season is added to the library
- **WHEN** Emby raises the add event for a positive-number Season and its initial source covers only part of the eligible local Episode sequence
- **THEN** the system SHALL use the shared global matcher over only Episodes whose parent season equals the target number, retain the initial explicit sparse-safe mapping, expose the authoritative remainder without recursively searching or selecting another source, and persist a Season display identifier only under the complete-single-source terminal policy

#### Scenario: New Season 0 is added to the library
- **WHEN** Emby raises an unattended add event for Season 0
- **THEN** the system SHALL skip provider search, selection, planning, mapping, binding, download, and metadata writes for that event while preserving explicitly user-targeted standalone Season 0 matching

#### Scenario: New season match is ambiguous
- **WHEN** a new season's global candidates do not satisfy automatic selection confidence
- **THEN** the system SHALL avoid persisting an arbitrary automatic provider binding and SHALL not start a download from a provider selected solely by configuration order

#### Scenario: Whole-Series and Season select the same candidate
- **WHEN** both interactive entry points confirm the same provider candidate for the same positive-number SeasonId
- **THEN** both SHALL return the same eligible ordered Episodes, alignment mode, mappings, temporary runs, safety decision, download set, and recursive remainder decisions

#### Scenario: Sparse coordinate is absent from the selected source
- **WHEN** reliable source numbering lacks a coordinate for an eligible local Episode but later numbered coordinates exist
- **THEN** every entry point SHALL leave only that local row unmatched, SHALL preserve later exact numbered mappings without shifting them, and SHALL NOT reinterpret the internal gap as a recursive continuation suffix

#### Scenario: Selected candidate resolves only part of the owning logical season
- **WHEN** reliable source numbering lacks a coordinate for an eligible local Episode or the verified source is exhausted before every eligible coordinate is mapped
- **THEN** every entry point SHALL leave only the corresponding eligible rows unmatched without shifting later numbered mappings; when those rows form the maximal authoritative suffix after the last mapped segment, an allowed interactive entry point SHALL recursively evaluate only that suffix, while a background or unattended entry point SHALL expose it without recursively selecting another source

#### Scenario: Confidence selection and manual selection choose the same source
- **WHEN** automatic confidence policy and a user selection independently choose the same candidate with identical target inventory and anchor intent
- **THEN** both SHALL produce the same eligible alignment mode, virtual mapping, and eligible temporary runs

#### Scenario: Season contains placed Episodes from another logical season
- **WHEN** an S1 display includes S00, another-season, or unknown-parent Episodes
- **THEN** the shared planner SHALL exclude those Episodes before scoring and mapping, and SHALL not render them as temporary or supplemental runs

#### Scenario: Whole-Series matching enumerates targets
- **WHEN** the parent Series contains Season 0, positive-number Seasons, and an unknown-number Season
- **THEN** only the positive-number Seasons SHALL be searched, recursively processed, and returned as whole-Series targets

#### Scenario: Explicit Season 0 has a remainder
- **WHEN** the user explicitly starts single-Season matching for the real Season 0 item and its first confirmed source leaves eligible Parent 0 Episodes unmatched
- **THEN** the system SHALL apply the same recursive remainder rules used by an explicitly targeted positive-number Season

#### Scenario: Manual and automatic paths observe foreign Episodes
- **WHEN** a target Season display includes Episodes whose parent season differs from the target
- **THEN** both paths SHALL exclude the same foreign ItemIds before episode-count scoring, mapping, temporary-run construction, and execution

#### Scenario: Sparse positive Season is rebuilt before download
- **WHEN** preview maps local E1-E6 and E10-E13 to the same numbered source Episodes and the authoritative target/source facts remain unchanged at download time
- **THEN** rebuild SHALL reproduce those exact pairs and SHALL not compress local E10 to source E7

## ADDED Requirements

### Requirement: Remainder candidates require verified safe metadata
After a first segment succeeds in an eligible interactive operation, the system SHALL freeze that segment's server-resolved Provider identity as the immutable Provider lock for the complete remainder operation. It SHALL evaluate the complete canonical candidate set for the remaining run in deterministic priority order only after excluding every candidate whose Provider differs from that lock. The same Provider restriction MUST apply to fresh next-logical-Season search results, detail resolution, Part applicability, metadata tuple counts, score uniqueness, and every later recursive round; an out-of-Provider row is out of scope and MUST NOT create ambiguity. The system MUST NOT automatically fall back to another Provider when the locked Provider has no eligible continuation.

Within the locked Provider, before any remainder selection, the system MUST exclude a source already used by the current authoritative plan, a candidate whose title explicitly names a Season different from the Season currently evaluated by that decision stage, and a candidate whose server-verified source Episode count is three or fewer. A search result with an unknown Episode count MUST be resolved through authoritative source details and MUST NOT become eligible unless those details establish more than three usable source Episodes. Cancellation, timeout, provider failure, unavailable authoritative details, or an incomplete candidate set for the locked Provider SHALL stop remainder recursion without selecting from partial or unverified evidence.

#### Scenario: Cross-provider duplicate does not create ambiguity
- **WHEN** the first confirmed segment uses Provider A, Provider A has exactly one eligible continuation with the required year and Episode count, and Provider B exposes another candidate with the same tuple
- **THEN** the Provider B row SHALL be excluded before uniqueness is calculated and the Provider A continuation SHALL remain uniquely eligible

#### Scenario: Same-provider duplicate remains ambiguous
- **WHEN** the first confirmed segment uses Provider A and Provider A exposes two distinct eligible sources for the same next Part or metadata tuple
- **THEN** the system SHALL bind neither source and SHALL stop the remainder operation without considering another Provider

#### Scenario: Locked provider has no continuation
- **WHEN** the locked Provider has no eligible continuation but another Provider has an otherwise matching candidate
- **THEN** the system SHALL leave the remainder unmatched and SHALL NOT automatically cross the Provider boundary

#### Scenario: Candidate reports three Episodes
- **WHEN** a similar remainder candidate has a server-verified source Episode count of three
- **THEN** the system SHALL exclude it from Part, year-and-count, and next-logical-Season automatic selection

#### Scenario: Candidate count is initially unknown
- **WHEN** a candidate has no trustworthy Episode count in search results and authoritative source details verify four usable Episodes
- **THEN** the candidate SHALL be treated as count-qualified using the verified count of four while remaining subject to every other eligibility gate

#### Scenario: Candidate detail remains unavailable
- **WHEN** a candidate's Episode count or source identity cannot be authoritatively verified because detail lookup fails, is cancelled, times out, or returns incomplete data
- **THEN** recursive selection SHALL stop and SHALL NOT promote another candidate from the incomplete evidence set

#### Scenario: Similar title names a different Season
- **WHEN** a Part or partless decision is evaluating a remainder of Season 2 and a similar candidate explicitly names Season 3
- **THEN** that candidate SHALL be excluded before uniqueness, Part, year, or Episode-count evaluation

#### Scenario: Source was already bound
- **WHEN** a candidate resolves to the same stable provider source already used by an earlier confirmed segment
- **THEN** the system SHALL exclude that candidate and SHALL NOT use it to satisfy a later recursive round

### Requirement: Continuous Part remainder selection has first priority
For an eligible remainder, the system SHALL first compare only locked-Provider titles by removing normalized parent-series and title text common to the confirmed source and candidate, ignoring repeated common text, and removing from the candidate any explicit Season phrase equal to the currently evaluated Season. It MUST establish a genuine shared identity-bearing non-parent title core before inspecting the residual title text for the keywords `part` or `部分` and their associated positive integer. A valid Part expression SHALL contribute only its ordinal; it MUST NOT replace or synthesize the candidate's title-family core or make two titles similar merely because both share the parent series. Arabic integers, strictly valid Roman numerals, and Chinese positive integers SHALL denote the same Part number; punctuation or separators between the keyword and number MUST NOT change the result. A confirmed first segment with no explicit Part marker SHALL be treated as Part 1. The system SHALL automatically select only a unique same-Provider, otherwise eligible candidate whose residual title denotes the immediately following Part number. It MUST NOT skip a Part or infer a Part number from digits outside the residual Part expression.

For Season 1, title similarity SHALL treat a confirmed source titled only with the parent series name as compatible with a later title formed from the parent name plus `Season 1`, `第1季`, `第一季`, or no Season phrase before the next Part marker. This parent-only exception SHALL apply only when none of the confirmed source's authoritative title channels contains an identity-bearing non-parent core. If any confirmed Name, source metadata title, resolved title, or alias contains such a core, a generic parent-only alias MUST NOT activate the exception for a candidate with a different core. A candidate that explicitly names another Season SHALL remain excluded. If an explicit next-Part result in the proven same family is present but is ambiguous, malformed, non-contiguous, or otherwise not trustworthy, the system MUST stop that remainder decision without falling back to the partless or next-logical-Season stages.

#### Scenario: Arabic Part punctuation varies
- **WHEN** the confirmed segment is treated as Part 1 and exactly one eligible residual title contains `part:.2`
- **THEN** that candidate SHALL be treated as Part 2 and automatically selected

#### Scenario: Roman and Chinese Part numbers are equivalent
- **WHEN** one recursive round has confirmed Part 2 and the next unique eligible title expresses `Part III` or `第三部分`
- **THEN** the expressed value SHALL be Part 3 and the candidate SHALL be eligible as the continuous next Part

#### Scenario: Roman numeral is not strict
- **WHEN** a residual title contains a Roman-looking token that is not a strictly valid positive Roman numeral
- **THEN** the system SHALL NOT interpret that token as a Part number

#### Scenario: Part sequence has a gap
- **WHEN** Part 2 is confirmed and the only explicit Part candidate is Part 4
- **THEN** the system SHALL stop remainder selection without binding Part 4 and without falling back to a lower-priority decision stage

#### Scenario: Next Part is ambiguous
- **WHEN** two otherwise eligible candidates both express the next continuous Part number
- **THEN** the system SHALL bind neither candidate and SHALL NOT downgrade to the partless or next-logical-Season stages

#### Scenario: First Season source uses only the parent title
- **WHEN** a Season 1 first segment is titled only with the parent series name and the unique next candidate is titled with the same parent plus `第一季 Part 2`
- **THEN** common-title removal SHALL preserve the Part 2 residual and the candidate SHALL remain eligible

#### Scenario: Stone Ocean has a cross-provider combined release
- **WHEN** the confirmed `石之海` segment uses one Provider, that Provider uniquely exposes consecutive `Part.2` and `Part.3` sources, and another Provider exposes a combined full-series candidate
- **THEN** the combined cross-Provider row SHALL be excluded and the same-Provider Part chain SHALL be evaluated recursively in order

#### Scenario: Part marker does not bridge different JOJO arcs
- **WHEN** the confirmed source has the non-parent core `星尘斗士` and a locked-Provider candidate has the different core `石之海` plus a valid `Part.2` marker
- **THEN** the candidate SHALL fail title-family eligibility before Part applicability and MUST NOT be selected as the next Part

#### Scenario: Parent aliases do not launder different arcs
- **WHEN** confirmed title channels include both `星尘斗士` identity and a parent-only alias while candidate channels include both `石之海 Part.2` identity and a parent-only alias
- **THEN** the parent-only aliases SHALL NOT activate the Season 1 exception and the two sources SHALL remain different title families

### Requirement: Unique partless remainder selection has second priority
Only when no eligible similar candidate from the locked Provider exposes an explicit Part expression SHALL the system evaluate the remaining eligible same-Provider, similar, partless candidates against the local run. The comparison year SHALL be the trustworthy premiere year of the first eligible unmatched local Episode, and the comparison count SHALL be the number of eligible Episodes in that run. A candidate SHALL be automatically selected when exactly one eligible same-Provider similar candidate has both that year and that server-verified source Episode count. Multiple distinct candidates from the locked Provider with the same matching year-and-count tuple SHALL remain ambiguous and MUST NOT be bound; candidates from other Providers MUST NOT contribute to that tuple count. If the filtered same-Provider similar-candidate pool contains exactly one candidate, the system SHALL also select it when its year matches but its verified Episode count differs, and SHALL attach the authoritative count-mismatch warning state. A missing or different candidate year MUST NOT be accepted by this single-candidate exception.

#### Scenario: JOJO continuation has a unique tuple
- **WHEN** a similar title without a Part marker has the same remainder-first premiere year and verified Episode count as the local remainder and no other candidate has that tuple
- **THEN** the system SHALL automatically select that candidate

#### Scenario: Same year-and-count tuple is duplicated
- **WHEN** two eligible partless candidates from the locked Provider share the local remainder's comparison year and Episode count
- **THEN** the system SHALL bind neither candidate

#### Scenario: JOJO tuple is duplicated only across providers
- **WHEN** the confirmed `星尘斗士` segment and exactly one `埃及篇` continuation share Provider A while Provider B also exposes the same comparison year and Episode count
- **THEN** the Provider B row SHALL be excluded and the unique Provider A continuation SHALL be automatically selected

#### Scenario: Wrong-arc Part is removed before metadata fallback
- **WHEN** a locked-Provider `石之海 Part.2` candidate fails family eligibility for confirmed `星尘斗士` and exactly one same-Provider `星尘斗士 埃及篇` candidate matches the remainder year and count
- **THEN** the wrong-arc Part SHALL NOT make the Part tier applicable and the eligible same-family metadata continuation SHALL be selected

#### Scenario: Only candidate has a different count
- **WHEN** the filtered partless pool contains exactly one candidate, its year matches the remainder-first premiere year, and its verified Episode count differs from the local remainder count
- **THEN** the system SHALL automatically select it and SHALL return an authoritative Episode-count-mismatch warning state

#### Scenario: Only candidate has no matching year
- **WHEN** the filtered partless pool contains exactly one candidate but its year is missing or differs from the remainder-first premiere year
- **THEN** the system SHALL NOT automatically select it through the single-candidate exception

### Requirement: Next logical Season selection has third priority
The system SHALL attempt the next-logical-Season stage only after the Part and partless stages both finish with determinate no-selection outcomes. It MUST NOT enter this stage after cancellation, timeout, provider failure, incomplete evidence, or an explicit Part ambiguity or trust failure. The remaining eligible run SHALL be evaluated as the logical Season immediately following the last confirmed segment, and its scoring year SHALL be the trustworthy premiere year of the run's first eligible local Episode. Fresh search results for this stage MUST be restricted to the immutable first-segment Provider before scoring or uniqueness selection. Locked-Provider candidates SHALL be scored from parent-series title at 60 percent, the incremented logical Season number at 20 percent, and the scoring year at 20 percent; only an otherwise eligible candidate with a total score of at least 0.90 and an unambiguous automatic-selection result within that Provider SHALL be selected. Each later successful logical Season SHALL increment the Season number once and repeat the same ordered remainder decision while eligible Episodes remain without changing the Provider lock.

#### Scenario: Frieren remainder becomes logical Season 2
- **WHEN** Season 1 Episodes E1-E28 are confirmed, the remaining run begins at E29 with premiere year 2026, and a candidate for the same parent title and Season 2 reaches at least 0.90 under the 60/20/20 rule
- **THEN** that candidate SHALL be automatically selected for the run beginning at E29

#### Scenario: A later remainder becomes logical Season 3
- **WHEN** the logical Season 2 segment succeeds and another eligible remainder begins later
- **THEN** the system SHALL use that remainder's first-Episode premiere year, target logical Season 3, and apply the same 0.90 threshold

#### Scenario: Remainder-first year is unavailable
- **WHEN** the first eligible local Episode in the remainder has no trustworthy premiere year
- **THEN** next-logical-Season selection SHALL stop without substituting the owning Season year or another Episode's year

#### Scenario: Next logical Season score is below threshold
- **WHEN** the best complete candidate for the incremented logical Season scores below 0.90
- **THEN** the system SHALL leave the remainder unmatched and SHALL NOT continue to a later logical Season

#### Scenario: Higher-priority stage is indeterminate
- **WHEN** Part evaluation or partless evaluation ends with ambiguous, cancelled, failed, or incomplete evidence
- **THEN** the system SHALL NOT attempt next-logical-Season selection
