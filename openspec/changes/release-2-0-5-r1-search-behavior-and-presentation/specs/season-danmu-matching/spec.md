## MODIFIED Requirements

### Requirement: Shared manual and automatic matching behavior
Manual whole-Series preview, manual single-Season preview, confidence-selected and manually selected candidate confirmation, newly added Season processing, and download-time rebuild SHALL use the same identifier-free cross-provider search rules and the same target-season-scoped authoritative virtual Episode-plan operation, except that an explicitly entered keyword SHALL use the separate scored `manual-provider-search` discovery contract before an explicit user selection. Whole-Series matching SHALL only enumerate and aggregate known positive-number target Seasons; it MUST NOT apply a different Episode ordering, grouping, mapping, or persistence path. No Season source, including a complete single-source result, may bypass explicit virtual mapping. Explicitly targeted or automatically processed Season 0 SHALL remain supported through the shared standalone Season operation.

Automatic processing SHALL remain fail-closed when the target inventory or authoritative mapping plan is structurally incomplete or ambiguous, when parent/user cancellation occurs, when no completed provider contributes an eligible candidate, or when the completed-provider candidates fail ordinary confidence selection. A provider-local fault diagnostic by itself MUST NOT make an otherwise complete high-confidence candidate from a successful provider provisional or block its normal automatic path.

#### Scenario: New season is added to the library
- **WHEN** Emby raises the add event for a positive-number Season
- **THEN** the system SHALL use the shared global matcher over only Episodes whose parent season equals the target number and SHALL persist a Season display identifier only under the complete-single-source terminal policy

#### Scenario: New Season 0 is added to the library
- **WHEN** Emby raises the add event specifically for Season 0
- **THEN** the system SHALL use the Season 0 item's own inventory and shared target-season matching rules rather than the whole-Series skip policy

#### Scenario: New season match is ambiguous
- **WHEN** a new season's completed-provider candidates do not satisfy automatic selection confidence
- **THEN** the system SHALL avoid persisting an arbitrary automatic provider binding and SHALL not start a download from a provider selected solely by configuration order

#### Scenario: Whole-Series and Season select the same candidate
- **WHEN** both entry points confirm the same provider candidate for the same positive-number SeasonId
- **THEN** both SHALL return the same eligible ordered Episodes, mappings, temporary runs, safety decision, and download set

#### Scenario: Selected candidate resolves only part of the owning logical season
- **WHEN** a candidate has fewer verified source Episodes than the target's eligible exact-parent Episode sequence
- **THEN** both entry points SHALL map only the verified eligible range and SHALL expose only the remaining maximal eligible target-season run for explicit continuation

#### Scenario: Confidence selection and manual selection choose the same source
- **WHEN** automatic confidence policy and a user selection independently choose the same candidate for identical target Season input
- **THEN** both SHALL produce the same eligible virtual mapping and the same eligible temporary runs

#### Scenario: Season contains placed Episodes from another logical season
- **WHEN** an S1 display includes S00, another-season, or unknown-parent Episodes
- **THEN** the shared planner SHALL exclude those Episodes before scoring and mapping, and SHALL not render them as temporary or supplemental runs

#### Scenario: Whole-Series matching enumerates targets
- **WHEN** the parent Series contains Season 0, positive-number Seasons, and an unknown-number Season
- **THEN** only the positive-number Seasons SHALL be searched and returned as whole-Series targets

#### Scenario: Manual and automatic paths observe foreign Episodes
- **WHEN** a target Season display includes Episodes whose parent season differs from the target
- **THEN** both paths SHALL exclude the same foreign ItemIds before ordinary Season scoring, neutral Episode-inventory inspection, mapping, temporary-run construction, and execution

#### Scenario: Successful provider remains automatically usable after sibling failure
- **WHEN** one provider fails but another completed provider supplies the unique ordinarily high-confidence candidate and its resolved mapping plan is structurally valid
- **THEN** preview and automatic library-import processing SHALL use that candidate normally while retaining the failed-provider diagnostic

### Requirement: Provider failure isolation
The system SHALL continue matching other enabled providers when one provider search fails and SHALL expose the failed provider through a bounded public search diagnostic. Candidate discovery, ordinary scoring, confidence selection, authoritative mapping, and automatic processing SHALL be based on the providers that completed successfully; a sibling provider fault MUST NOT by itself mark their candidates as provisional or block an otherwise valid match.

#### Scenario: One provider search throws an error
- **WHEN** an enabled provider fails during a search round
- **THEN** candidates from successful providers SHALL still be ranked and the failed provider SHALL be recorded in the search-error list

#### Scenario: Failed site has no candidate contribution
- **WHEN** a provider faults before returning a usable result while another provider completes
- **THEN** the failed provider SHALL contribute no candidate, the successful provider's result SHALL remain eligible for ordinary selection, and the failure SHALL remain visible as a non-blocking site diagnostic

#### Scenario: Every provider fails
- **WHEN** no enabled provider completes with a usable candidate
- **THEN** the system SHALL return an overall retryable failure without selecting, downloading, binding, or writing metadata

#### Scenario: Parent cancellation occurs
- **WHEN** the user, host, or parent operation explicitly cancels the search
- **THEN** the entire operation SHALL follow the cancellation path and MUST NOT reinterpret cancellation as a collection of non-blocking provider faults

### Requirement: Enabled-provider candidate participation
For automatic and default metadata-driven searches, the system SHALL include usable search results from every enabled danmu provider in the shared candidate set used by preview and automatic library-import matching. A usable result MUST contain a provider media identifier accepted by that provider's media-detail path, a non-empty display title, and the available year, category, and episode-count metadata. Explicit user-entered keyword searches SHALL follow `manual-provider-search`: they retain the established provider/candidate eligibility, merge, score/reason, and provider-fair sixty-row ordering path, but bypass TMDB alias expansion, automatic-threshold classification, automatic selection, and automatic download before the user selects a row.

#### Scenario: Bilibili anime result is available
- **WHEN** Bilibili is enabled and returns one or more valid anime seasons for the parent series title during a metadata-driven search
- **THEN** the preview SHALL include those Bilibili seasons in the globally scored candidate list

#### Scenario: Bilibili live-action result is available
- **WHEN** Bilibili is enabled and returns one or more valid live-action television seasons for the parent series title during a metadata-driven search
- **THEN** the preview SHALL include those Bilibili seasons with their titles, years, categories, and episode counts

#### Scenario: Automatic matching searches Bilibili
- **WHEN** a newly added non-special season is processed by automatic library-import matching and Bilibili is enabled
- **THEN** valid Bilibili results SHALL participate in the same global scoring and confidence rules as results from other enabled providers

#### Scenario: Bilibili result has no usable identifier
- **WHEN** a metadata-driven Bilibili search returns an entry without a positive season or media identifier accepted by its media-detail path
- **THEN** the system SHALL omit that entry without preventing results from Bilibili or other providers from being ranked

#### Scenario: User enters an explicit keyword
- **WHEN** the user explicitly submits a manual search keyword from a supported smart-match entry point
- **THEN** eligible candidates SHALL retain ordinary merge, score/reason, and sixty-row ordering while remaining unselected and outside TMDB alias or automatic-classification processing

## ADDED Requirements

### Requirement: Shared smart-match search has no elapsed-time deadline
The shared smart-match layer MUST NOT stop, cancel, skip, or classify a provider call because it exceeds the former 10-second per-provider deadline, and MUST NOT stop an interactive or automatic search because it exceeds the former 30-second or 45-second operation deadline. It SHALL continue to enforce explicit parent/user cancellation, configured global and per-provider concurrency isolation, and safe ownership of a provider gate until the actual provider task settles. Provider-native transport safeguards MAY still surface as provider-local faults under the failure-isolation requirement.

This requirement MUST NOT change the 180-second tracked Movie/Episode download deadline or the seven-day XML freshness, force-refresh, and replay policies.

#### Scenario: Provider search exceeds ten seconds
- **WHEN** an enabled provider search remains active beyond ten seconds without a parent/user cancellation or provider-native failure
- **THEN** the shared matcher SHALL continue awaiting it and SHALL NOT emit a shared-layer timeout diagnostic

#### Scenario: Interactive search exceeds thirty seconds
- **WHEN** a user-started search remains active beyond thirty seconds
- **THEN** the shared matcher SHALL continue until its providers settle or the user explicitly cancels

#### Scenario: Automatic search exceeds forty-five seconds
- **WHEN** an automatic library-import search remains active beyond forty-five seconds
- **THEN** the shared matcher SHALL continue until its providers settle or its parent operation explicitly cancels

#### Scenario: User cancels a long search
- **WHEN** the user explicitly cancels a provider search that has no elapsed-time deadline
- **THEN** the operation SHALL terminate through the cancellation path while preserving concurrency and provider-gate safety

#### Scenario: Download or recent-file policy runs
- **WHEN** a tracked Movie/Episode download exceeds its download deadline or an XML file is subject to the seven-day duplicate policy
- **THEN** the existing download timeout and seven-day skip/replay behaviors SHALL remain unchanged

### Requirement: TMDB alias exhaustion exposes a parent-title automatic rematch
TMDB alias assistance SHALL stop when an alias produces an automatically acceptable result under the current Season scoring threshold. A fault in one alias request SHALL remain server-local and SHALL NOT prevent later eligible aliases from being attempted in the existing deterministic order. Alias exhaustion occurs only after every eligible Chinese, English, and Japanese alias either faults or completes below the threshold. At exhaustion, the response MUST NOT expose or reuse the accumulated alias candidate list as the Season's manual candidates and SHALL instead mark the Season as eligible for a `重新匹配` action.

Activating that action SHALL issue one fresh search using exactly the parent Series title, without a Season suffix, year, alias, translation, or manual-keyword mode, and SHALL score the returned candidates with the target Season's ordinary automatic scoring rules.

#### Scenario: Every alias remains below automatic confidence
- **WHEN** all eligible TMDB alias terms are attempted and none produces an automatically acceptable Season candidate
- **THEN** the result SHALL expose `重新匹配`, omit the accumulated alias candidates, and wait for the user to request the parent-title automatic search

#### Scenario: One alias request faults before later eligible aliases
- **WHEN** one eligible alias request faults and later eligible aliases remain in the deterministic attempt plan
- **THEN** alias assistance SHALL record the fault for server-side diagnostics, continue with the later aliases, and SHALL enter exhaustion only if every remaining eligible alias also faults or completes below the automatic threshold

#### Scenario: User requests parent-title rematch
- **WHEN** the user activates `重新匹配` after alias exhaustion for `JOJO的奇妙冒险` Season 1
- **THEN** providers SHALL receive exactly the parent Series title and the returned candidates SHALL be scored and classified as Season 1 automatic candidates rather than as explicit manual-keyword discovery

#### Scenario: User submits an explicit keyword
- **WHEN** the request carries the l10-owned `manual-keyword` intent
- **THEN** alias orchestration SHALL NOT run and the response SHALL NOT expose l6 parent-title-rematch state

#### Scenario: One alias reaches automatic confidence
- **WHEN** any eligible alias produces an automatically acceptable candidate
- **THEN** alias orchestration MAY stop and SHALL retain the successful automatic alias result instead of entering parent-title rematch state

#### Scenario: Parent Series title is unavailable
- **WHEN** alias exhaustion occurs but the authoritative parent Series title is empty
- **THEN** the system SHALL return a retryable no-title state without issuing an empty provider request or restoring alias candidates

### Requirement: Automatic confidence excludes the removed score overrides
Season automatic confidence SHALL use the ordinary composite score without capping a candidate at `0.79` because of contradictory explicit Season or year evidence. Contradictory evidence MAY remain available as a reason or diagnostic. The later explicit Season-evidence distribution requirement defines the current ordinary weights.

Fidelity evidence MAY continue to distinguish otherwise equal-score candidates, but it MUST NOT promote a candidate whose ordinary composite score is `0.85` or otherwise below the standard automatic-confidence threshold to that threshold. No equivalent replacement bonus or alternate bridge SHALL be introduced.

#### Scenario: Candidate contains contradictory evidence
- **WHEN** a candidate's ordinary title and year evidence produces a score above `0.79` while its explicit Season number or known year conflicts with the target
- **THEN** the candidate SHALL retain its ordinary composite score instead of being capped at `0.79`

#### Scenario: Unique fidelity candidate has a base score of 0.85
- **WHEN** one same-provider candidate has unique exact fidelity evidence but its ordinary composite score is `0.85`
- **THEN** its effective automatic confidence SHALL remain `0.85` and SHALL NOT be promoted to the automatic threshold

#### Scenario: Equal-score candidates differ in fidelity
- **WHEN** candidates have the same ordinary composite score but different fidelity evidence
- **THEN** fidelity MAY resolve their ordering or uniqueness without changing either candidate's confidence score

### Requirement: Season confidence uses 60-20-20 title and year evidence
Season matching SHALL calculate its ordinary confidence from parent-title evidence worth 60 points, Season-name evidence worth 20 points, and exact known-year evidence worth 20 points. Episode-count evidence SHALL contribute zero points, SHALL NOT break an otherwise eligible automatic match, and SHALL NOT alter candidate ordering through a hidden equivalent bonus. The Episode count MAY remain visible as neutral evidence and SHALL remain available to the authoritative mapping operation. Movie scoring SHALL remain unchanged.

For Season 1, the Season-name target set SHALL continue to include an empty Season name in addition to the authoritative Season title and a Season-1 label, so a source result containing only the parent title can receive the Season-name component. TMDB Chinese-alias, English-title, and Japanese-title rounds SHALL continue comparing their active parent term with the original parent Series title and eligible local aliases, taking the best parent-title result without adding scores across alternatives.

Only in a TMDB alias parent-maximum round, when every usable real authoritative Season-title remainder is generic and at least one equals the expected positive Season label, the scorer MAY recognize an unconsumed continuation of a short parent alias before the source's Season marker. This recovery SHALL operate within one source-title channel, SHALL require exactly one correct terminal generic Season marker, and SHALL require a prefix of at least four characters containing at least one letter whose best strictly equal-length window similarity against a known parent title is at least `0.90`. It SHALL only reduce the residual used for the 20-point Season-name comparison and MUST NOT add parent-title points, combine separate source-title channels, activate for a named Season or ordinary search, accept a prefix longer than the compared parent, or alter Movie scoring or global title normalization.

When the ordinary Season-name comparison for one concrete library Season title, one source-title item, and one matched parent does not produce full Season-name evidence and both parent-stripped loose Season remainders are empty, the scorer SHALL attempt a strict complete-title fallback. It SHALL compare the complete library Season title and that same complete source title after Unicode NFKC, case folding, and whitespace removal, without removing the parent, deleting punctuation, rewriting Season markers, or applying fuzzy similarity. Exact equality SHALL supply the existing 20-point Season-name component only when the complete library Season title is not fidelity-equivalent to any applicable complete parent title and no explicit Season marker conflicts with the expected Season. Ordinary remainder evidence and this fallback SHALL be combined by maximum, never addition. The fallback MUST NOT combine different source-title items, restore the removed fidelity bridge, change the Season 1 empty-name rule, or affect Movie scoring.

#### Scenario: Candidate has the correct titles and year but a different Episode count
- **WHEN** a Season candidate matches the parent-title and Season-name evidence and its known year exactly matches the target while its Episode count is smaller or larger
- **THEN** it SHALL receive the same 100-point ordinary score as the equal-count candidate and Episode-count difference SHALL NOT block automatic confidence

#### Scenario: Only the parent title and year match
- **WHEN** a candidate earns the full parent-title component and exact known-year component but earns no Season-name component
- **THEN** it SHALL receive 80 points regardless of its Episode count; this MAY satisfy the separate TMDB-alias acceptance threshold where applicable, while the ordinary automatic threshold remains unchanged

#### Scenario: Short alias leaves a verified parent-title continuation
- **WHEN** a TMDB alias round targets a generic Season 2 label and one source title contains the matched short parent alias, a four-or-more-character continuation containing a letter and verified at `0.90` or higher against a strictly equal-length window of a known parent title, and one terminal `第二季` marker
- **THEN** that source-title channel SHALL receive the full Season-name component while the parent-title component remains capped at 60 points

#### Scenario: Prefix or Season marker is not safely recoverable
- **WHEN** the search is ordinary, the target is named rather than generic, the prefix is short, numeric, longer than the compared parent, unrelated to every known parent title, the marker is followed by text, or the source contains a wrong or conflicting Season marker
- **THEN** the scorer SHALL NOT use short-parent recovery and SHALL retain the ordinary whole-residual comparison or conflicting-marker zero result

#### Scenario: Compatibility-equivalent complete titles recover symbol-only Season identity
- **WHEN** Season 2 is named `妄想学生会＊`, the matched source title is `妄想学生会*`, both loose parent-stripped remainders are empty, the complete parent title is `妄想学生会`, and the known years match
- **THEN** NFKC strict complete-title equality SHALL supply the Season-name component and the candidate SHALL receive 100 points

#### Scenario: Markerless or different-symbol source does not use complete-title fallback
- **WHEN** Season 2 is named `妄想学生会＊` and the source title is `妄想学生会`, `妄想学生会!`, `妄想学生会**`, or `妄想学生会★`
- **THEN** the complete-title fallback SHALL supply zero Season-name points and an otherwise exact parent/year candidate SHALL remain at 80 points

#### Scenario: Only one loose Season remainder is empty
- **WHEN** one side of a concrete library/source/matched-parent pair has an empty loose Season remainder but the other side does not
- **THEN** the complete-title fallback SHALL fail closed and SHALL NOT supply Season-name points

#### Scenario: Complete local Season title is only the parent title
- **WHEN** the complete library Season title is fidelity-equivalent to an applicable complete parent title
- **THEN** the strict complete-title fallback SHALL be disabled so the parent cannot supply both the parent-title and Season-name components; the existing Season 1 empty-name exception remains unchanged

#### Scenario: Evidence exists in different source-title items
- **WHEN** parent evidence is available from one source title item while strict complete-title equality is available only from another source title item
- **THEN** the scorer SHALL NOT combine those items into one parent-plus-Season score

#### Scenario: Candidate year differs or is unavailable
- **WHEN** the candidate and target years are known but differ, or exact-year evidence is unavailable
- **THEN** the candidate SHALL receive zero year points rather than a partial year score

#### Scenario: Movie matching runs
- **WHEN** the target is a Movie rather than a Season
- **THEN** this Season-only distribution SHALL NOT change the Movie scoring formula

### Requirement: Source Episode surplus is advisory after authoritative mapping
Episode-count differences SHALL be evaluated only from the authoritative local Season scope and the actual provider Episode details used to build the current mapping plan. Search-candidate Episode metadata MUST NOT be trusted to produce this result. When an authoritative plan has at least one applied mapping and any selected source contains more verified Episodes than the target Season's full eligible local inventory, the Season result SHALL expose an advisory source-surplus state. This state MUST NOT change the score, confidence classification, selection, mapping, download eligibility, or persistence behavior.

#### Scenario: Verified source has more Episodes than the local Season
- **WHEN** a valid authoritative mapping is built for 16 eligible local Episodes from a selected source whose resolved media contains 24 usable Episodes
- **THEN** matching SHALL remain successful and the Season result SHALL expose the source-surplus advisory state

#### Scenario: Local and source Episode counts are equal
- **WHEN** the authoritative local and source Episode counts are equal
- **THEN** the Season result SHALL NOT expose the source-surplus advisory state

#### Scenario: Local Season has more Episodes than the selected source
- **WHEN** the eligible local inventory exceeds the verified source inventory
- **THEN** the Season result SHALL NOT expose the source-surplus advisory state and the existing unmatched-run temporary-Season workflow SHALL remain unchanged

#### Scenario: Mapping authority is unavailable
- **WHEN** the plan is missing, cancelled, stale, evidence-invalid, provider-invalid, or contains no applied mapping
- **THEN** the system SHALL fail closed for that plan and SHALL NOT infer a source-surplus advisory from candidate metadata

#### Scenario: Composite mapping uses several sources
- **WHEN** an authoritative plan uses more than one selected source
- **THEN** each source's verified Episode inventory SHALL be compared independently with the local eligible inventory and source counts MUST NOT be summed into a synthetic total
