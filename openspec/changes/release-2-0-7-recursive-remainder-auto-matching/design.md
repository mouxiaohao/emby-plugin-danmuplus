## Context

See `proposal.md` for motivation and the delta specs for observable behavior. The implementation baseline is the immutable `2.0.6r2` `develop` checkpoint `07fbb408d54ee1b6201d4f217122079070527c5a`; implementation and review must compare against that commit rather than later working-tree behavior.

At that baseline, `DanmuMatchSearchEngine` already retains a complete `CanonicalCandidates` set separately from the provider-fair 60-row presentation projection. `DanmuMatchScorer` owns title normalization, Season residual construction, explicit Season parsing, and the 60/20/20 parent-title/Season-title/exact-year score. A selected Season candidate is resolved to provider Episode details and converted into a compact `DanmuCompositeSeasonSelection`; `BuildCompositePlanAsync` re-resolves that compact intent, verifies target-bound candidate evidence, and delegates deterministic Episode alignment to `CompositeSeasonPlanner`. The planner is intentionally pure. `CompositeSelections`, server-owned source snapshots, plan generation, and the SHA-256 plan fingerprint already protect preview, download preflight, retry, and metadata writes.

The current first-source preview stops when `CompositePlan.UnmatchedRuns` remains non-empty and exposes those rows as temporary seasons. Adding recursive provider search inside `CompositeSeasonPlanner` would couple deterministic mapping with network state, cancellation, and evidence registration, and would make preview/download reconstruction diverge. The new behavior therefore needs an orchestration layer above the planner while preserving the planner and existing Season-ownership boundary (`ParentIndexNumber == target Season.IndexNumber`).

## Goals / Non-Goals

**Goals:**

- Make one server-owned, bounded remainder operation choose and verify consecutive Part, unique same-title metadata, or next logical-Season sources, then append each as an explicit composite selection.
- Lock the operation to the Provider of the first authoritative segment so cross-provider duplicates or combined releases cannot create false ambiguity or silently replace the confirmed source family.
- Use complete canonical search evidence, exact target-Season Episode ownership, server-resolved provider details, and deterministic title/ordinal analysis without letting the browser infer or author an automatic decision.
- Preserve one authoritative reconstruction path and fingerprint for initial and recursively added selections.
- Make every stop safe: already confirmed mappings remain usable, remaining rows remain temporary and unmatched, and no remainder-only failure becomes a top-level matching failure.

**Non-Goals:**

- Moving network search, detail resolution, recursion, evidence registration, or cancellation into `CompositeSeasonPlanner`.
- Changing Emby Season membership or Episode numbering, creating synthetic local Seasons/Episodes, or writing a composite Season-level ProviderId.
- Making library import, item-added events, retry, replay, or any other unattended path recursively discover a second source.
- Reading saved Series/Season/Episode identifiers as remainder evidence, weakening sparse alignment, changing provider adapters, or changing the existing manual temporary-range workflow.
- Treating candidates with three or fewer verified source Episodes as remainder sources, or treating search-result `EpisodeSize` alone as verified Episode inventory.

## Decisions

### 1. Add a policy-gated remainder coordinator above the pure planner

Add an independent provider-neutral remainder decision service, invoked by the Season preview coordinator only after an initial source has been server-verified and applied. Its inputs are the immutable `SeasonPlanningContext`, current authoritative plan and ordered compact selections, the immutable Provider identity of the first authoritative segment, initial search completion evidence and full canonical candidates, authoritative parent/Season title data, an operation policy, and callbacks for bounded provider search, detail resolution, evidence registration, and authoritative plan rebuild. Its output is the updated selections/plan plus an internal terminal reason; it does not write files, bind identifiers, or mutate Emby objects.

The first segment's server-resolved Provider identity becomes a closed operation-level lock. Every initial canonical pool, fresh logical-Season search result, detail-resolution request, uniqueness calculation, and recursive selection is filtered to that Provider before title-family, Part, metadata, score, or ambiguity analysis. A candidate from another Provider is out of scope, not a competing source; if the locked Provider has no eligible result, recursion stops safely and never falls back across Providers. This applies equally when the first segment was confidence-selected or explicitly confirmed by the user.

Use a closed operation policy rather than an optional Boolean:

- `InteractiveRecursive`: user-initiated whole-Series matching for positive-number Seasons and explicitly targeted single-Season matching, including a real S00 item.
- `BackgroundNonRecursive`: eligible positive-number media import, item-added events, retry/replay, and every other unattended positive-Season path; it may complete only the existing initial selection and never discovers a remainder source.

Whole-Series enumeration and every unattended/background entry point exclude S00 before provider search, selection, planning, binding, download, or metadata write; no remainder policy instance is created for those S00 items. Only an explicitly user-targeted single-Season S00 uses its real Parent 0 inventory and may recurse exactly like another explicitly selected Season. Manual-keyword discovery remains selection-free; recursion begins only after the user explicitly confirms a candidate. A user-confirmed initial candidate and an automatically selected initial candidate therefore enter the same remainder service.

This is preferred over extending `CompositeSeasonPlanner`: provider calls and cancellation would destroy planner purity and prevent deterministic reconstruction. It is also preferred over duplicating a loop in whole-Series and single-Season controller branches because that would make policy and failure behavior drift.

### 2. Analyze title families with shared normalization but separate Part and Season grammars

Extract reusable title-analysis primitives from the scorer without changing ordinary scoring: loose normalization, identity-bearing title checks, parent/Season residual construction, localized ordinal conversion, and explicit conflicting-Season detection. The remainder service uses those same primitives over every provider title channel available in the canonical candidate snapshot.

Keep `PartTitleParser` and the explicit Season parser separate. The Part parser recognizes only a bounded `part` or `部分` marker with tolerant punctuation/spacing and a positive Arabic, Roman, or Chinese ordinal; examples include `Part 2`, `part:.2`, `Part II`, and `第二部分`. Arabic, Roman, and Chinese forms normalize to one integer. A shared ordinal converter is allowed, but the Part parser must not call the Season parser, treat `第N季` as a Part, or broaden matching to arbitrary bare numbers, Episode labels, `部`, `篇`, or `cour`.

For each comparison, remove normalized parent identity and the repeated title-family core before interpreting the remaining marker text. Part tokens themselves are retained during common-string removal. Family identity is decided before Part parsing: a valid Part marker contributes only an ordinal and must never replace, synthesize, or raise the candidate's identity core. A title family is similar only when the candidate and the last selected source genuinely share an identity-bearing normalized non-parent core under the existing Season title-eligibility floor, or when the authoritative confirmed source has no non-parent identity in any server-resolved title channel and both reduce to the same authoritative parent after removing a compatible current-Season marker.

The latter is the explicit first-Season parent-only exception: an initial source that is authoritatively titled only as the parent can be followed by `父剧名 + 第1季/第一季 + Part N` or `父剧名 + Part N`. Evaluate this exception across the complete confirmed title-channel set, not per alias pair. If any confirmed Name, source metadata title, resolved title, or alias carries a non-parent identity such as `星尘斗士`, a parent-only alias cannot activate the exception, and a candidate such as `石之海 Part.2` must prove a genuine shared non-parent core. This prevents `Any(title-pair)` evaluation from laundering two different arcs through generic parent aliases.

Before any Part or metadata decision, reject a candidate whose title channels contain a positive explicit Season marker different from the active logical Season. Conflicting or unparseable explicit Season markers fail closed rather than becoming “no marker.” Thus an otherwise similar `第3季` candidate cannot affect a logical S2 remainder decision.

This is preferred over adding Part syntax to `ParseExplicitSeasonNumber`: Part ordinals and Season identity have different safety meaning, and merging their regexes would reintroduce cross-season false positives. It is preferred over raw substring removal because punctuation, Unicode compatibility forms, Chinese ordinals, and the S1 parent-only case need the scorer's existing normalization semantics.

### 3. Use a strict three-tier state machine with no ambiguity downgrade

Each tier returns exactly one of `Selected`, `NotApplicable`, `Rejected`, or `Unknown`. Only `NotApplicable` may advance to the next tier. `Rejected` covers ambiguity, a non-consecutive Part, conflicting metadata, or an unsafe structural result; `Unknown` covers cancellation, timeout, provider/detail failure, incomplete provider coverage, expired evidence, or missing required metadata. Both are terminal for the current remainder operation. A lower tier can never rescue or override evidence that made a higher tier applicable but unsafe.

The ordered states are:

1. **Consecutive Part.** Work only over locked-Provider, unused, same-family, non-conflicting candidates. The initial source has implicit Part 1 only when it has no Part marker; afterward the last selected Part is authoritative. If any eligible candidate exposes a Part marker, this tier is applicable and accepts only the exact next integer. De-duplicate rows that resolve to the same stable source identity, then require exactly one distinct stable source in the exact-next subset. Two different same-Provider sources for the same next Part, a missing next Part, an invalid Part expression, or a Part gap is `Rejected`, not a path to metadata or logical-Season matching; another Provider's Part row is excluded before applicability or ambiguity is calculated.
2. **Similar-title metadata.** This tier is reached only when no eligible same-family candidate from the locked Provider has a Part marker. Compare a positive exact effective year and the current unmatched-run count against the verified provider Episode count. One unique same-Provider exact year-and-count result is selected. If the filtered same-Provider pool contains exactly one resolved candidate with an exact year but a different verified count, select it and carry a server-derived Episode-count mismatch warning. Two or more same-Provider exact year-and-count results, or more than one same-year residual candidate when only mismatches remain, are `Rejected`. No same-Provider candidate with the required year is `NotApplicable`; missing/failed details are `Unknown`. Rows from other Providers never contribute to tuple counts.
3. **Next logical Season.** This tier is reached only after the first two return `NotApplicable`. Synthesize an in-memory search descriptor from the authoritative parent title, `logicalSeason + 1`, the entire current suffix run, the production year of that run's first local Episode, and the immutable Provider lock. Do not mutate the real Emby Season or manufacture a `Season` entity. Search and score only candidates from the locked Provider with the existing parent 60 + Season 20 + exact year 20 components, hard-exclude other explicit Seasons, and require a unique score of at least `0.90`; the TMDB alias 0.80 selection threshold must not apply to this tier. A missing positive first-Episode year cannot earn the year component and therefore cannot reach 0.90. On success, advance the active logical Season number; a Part selection does not advance it.

The next-logical-Season descriptor requires an explicit expected Season number in the search/scoring API instead of reusing `contextItem.IndexNumber`, because the real local Season may still be S1 while the temporary suffix represents logical S2. Provider detail calls may still receive the real local Season as provider context.

This state machine is preferred over one weighted mega-score: a Part gap, duplicate metadata result, or unavailable provider is negative/unknown safety evidence and must not silently fall through to a different explanation. It also makes JOJO-style no-Part continuations and Frieren-style next official Seasons independently testable.

### 4. Verify Episode inventory before eligibility and cache detail work per operation

Search-result `EpisodeSize` remains a discovery hint only. Before a locked-Provider candidate can participate in a remainder decision, resolve its media details through the bounded provider gate and validate unique non-empty Episode IDs and CommentIds using the existing source projection rules. The verified source Episode count must be greater than three. Values of zero through three, invalid Episode inventories, or an unresolvable source cannot be promoted by title/year evidence.

Provider locking and title-family qualification intentionally have different detail boundaries. Cross-Provider rows are discarded before detail resolution. Rows from the locked Provider may be resolved first because authoritative family analysis needs the server-resolved Name, source metadata title, resolved title, and aliases rather than an incomplete search-row label. After those details are available, family identity MUST be established before Part applicability or metadata tuple analysis. A wrong-family resolved row may therefore consume one operation-local cached detail lookup, but it must produce zero remainder evidence registrations, compact selections, authoritative builds, or committed mappings. Do not add a search-label-only preflight that could suppress a valid authoritative alias.

Use one operation-local asynchronous detail cache keyed by target SeasonId plus `(providerId, candidate lookup id)`. Cache both completed verification and terminal failure so the same candidate is never requested twice across title filtering, metadata comparison, selection construction, and later rounds. After resolution, also track the stable `(providerId, canonical MediaId)` identity. Maintain used sets for both lookup identity and stable identity; exclude either form in later rounds so aliases or duplicate search rows cannot bind the same upstream source twice.

The service always receives `search.CanonicalCandidates`, never the 60-row `Candidates` projection. The browser continues to receive only the normal projection, so recursion cannot depend on viewport allocation and does not enlarge the public candidate payload.

This is preferred over trusting `EpisodeSize` because the warning and the safety gate must reflect the same provider Episode facts used for mapping. It is preferred over a process-wide media cache because one preview needs coherent cancellation/evidence lifetime while provider data may legitimately change between later previews.

### 5. Append server-authored ExplicitAnchor selections and rebuild after every round

Every selected remainder source becomes a normal `DanmuCompositeSeasonSelection` with:

- the current mapping protocol and plan generation;
- `AlignmentIntent = ExplicitAnchor`;
- the first ItemId of the current authoritative unmatched suffix;
- the full current suffix-run count as `RequestedEpisodeCount`;
- the exact first verified source EpisodeId and observed source Episode number (or zero when absent);
- a closed automatic origin such as `remainder-part`, `remainder-metadata`, `remainder-metadata-count-warning`, or `remainder-logical-season`;
- a fresh target-bound `SelectionEvidenceToken` registered from the chosen canonical candidate and verified server metadata.

The controller appends the selection and calls the existing authoritative `BuildCompositePlanAsync`; it never inserts browser-visible mappings directly. Reconstruction re-resolves every source, validates evidence, populates server-owned source Episodes/considered ItemIds/resolved modes, and recomputes the plan fingerprint. Download preflight and queued execution repeat the same reconstruction and current-generation/fingerprint checks. Automatic decisions therefore do not bypass `CompositeSelections`, `ExplicitAnchor`, CommentId validation, sparse alignment, or stale-plan protection.

Extend internal candidate evidence with the closed remainder decision kind, the immutable first-segment Provider lock, and the verified count/year facts needed to revalidate warning provenance. Rebuild must prove that every automatic remainder selection and its current canonical/detail identity still belong to the locked Provider; any missing or changed lock, cross-Provider selection, or Provider drift is stale. Expose only an additive response-only Boolean on the mapped composite group for an Episode-count mismatch warning; the browser cannot submit it as planning evidence. The existing compact selection already carries anchor, origin, and evidence token, and the existing fingerprint covers selection order, origin, evidence, verified source Episodes, considered local ItemIds, and exact mappings; add the Provider-lock fact to that server-owned fingerprint input. Therefore mapping protocol V22 remains unchanged. If implementation discovers a need for a new client-authored planning field, that is a contract change and must instead advance server and frontend protocol together; it must not be smuggled into V22.

This is preferred over storing recursive mappings in a new persistence model: the existing selection/rebuild/fingerprint chain is already the authoritative execution contract and fails closed when process-local evidence expires.

### 6. Bound recursion by monotonic authoritative progress

Capture the number of unmatched eligible local Episodes immediately after the initial source is applied. That number is the maximum number of successful recursive rounds; the existing maximum compact-selection count remains an independent payload guard. After each appended selection and rebuild, require all of the following:

- the plan generation is still current;
- total unmatched eligible ItemIds strictly decreases;
- at least one previously unmatched ItemId gains a verified mapping;
- no mapped ItemId becomes unmatched or changes to an already used source unexpectedly;
- the newly resolved lookup and stable source identities are added to the used sets.

If any invariant fails, discard only that attempted selection, retain the last valid plan, and stop. Recursion always operates on the first remaining maximal suffix run; an internal or reordered unmatched run that cannot be proven to be the continuation after the last mapped segment is `Rejected`. These rules prove termination without an arbitrary network-depth constant and prevent cycles caused by duplicate aliases or zero-consumption candidates.

This is preferred over “continue until search is empty”: provider results can repeat indefinitely, and a recursion count alone does not prove that local work is shrinking.

### 7. Preserve partial results and render remainder stops as unmatched state

Once the initial segment is confirmed, a remainder `NotApplicable`, `Rejected`, or `Unknown` stop returns the last valid composite plan. Its confirmed mappings and compact selections remain present, while all unconsumed ItemIds remain ordinary temporary unmatched groups. The Season result is `partial`/`composite-season`, not `no_match`, `failed`, or a remainder-specific error. Do not show a top-level failure banner or error popup and do not render a score on the unmatched temporary card.

For the unique same-year candidate whose verified Episode count differs from the local remainder, render one yellow advisory on its mapped group using the established composite warning styling. The warning is informational like the existing verified source-surplus notice: it neither blocks binding nor creates synthetic Episodes. All other automatic remainder groups use the existing matched-group presentation.

Operational cancellation, timeout, incomplete provider coverage, provider exceptions, evidence expiry, and invalid details may remain in credential-safe server diagnostics, but they must not be transformed into a confident lower-tier decision. Cancellation before any initial source is confirmed retains existing top-level cancellation behavior; cancellation during remainder recursion keeps the confirmed prefix and silently stops with temporary unmatched rows.

This is preferred over returning the existing temporary-range `no_match`/error strings because automatic recursion is opportunistic and its failure must not erase a valid initial match or force an error interaction.

### 8. Version 2.0.7 is an additive, stateless delivery

Set assembly/file version to `2.0.7.0`, informational and plugin configuration version to `2.0.7`, update the TMDB alias client User-Agent to `DanmuPlus/2.0.7`, and advance the frontend installation/cache marker from V31 for the changed script. Keep the existing plugin identity, configuration schema, mapping protocol V22, saved bindings, and composite-state format. No database, configuration, or on-disk mapping migration is required.

The response-only group warning and internal evidence additions are backward-tolerant: an old frontend ignores the advisory field, while the updated frontend still relies on server-authored compact selections and plan fingerprints. Deployment must nevertheless replace the Release DLL and CustomCssJS asset as a reviewed pair so the new warning and cache marker are visible.

## Risks / Trade-offs

- [Broad normalized common strings can join unrelated sequels] → Require an identity-bearing family core or the explicit S1 parent-only fallback, reuse the existing eligibility floor, and hard-exclude conflicting explicit Seasons before Part parsing.
- [A valid Part token can accidentally be treated as family evidence] → Decide family identity from resolved title channels before parsing the ordinal; never overwrite the candidate core with the previous core merely because Part syntax is valid, and test mixed parent-only aliases across distinct arcs. Locked-Provider detail may be cached first, but wrong-family rows must cause zero evidence registrations/builds/commits.
- [Roman or Chinese digits can be confused with Season/Episode labels] → Keep a bounded Part grammar tied to `part`/`部分`, keep the Season grammar separate, and reject bare ordinals.
- [A provider reports a plausible search count but unusable detail] → Treat search `EpisodeSize` as non-authoritative; require valid resolved Episode details and more than three verified Episodes.
- [Incomplete provider coverage could make an apparently unique candidate unsafe] → Return `Unknown` and stop recursion; never downgrade to a later tier or bind from partial coverage.
- [Several providers expose the same Part or same-year/count source] → Exclude all Providers except the first segment's locked Provider before detail and ambiguity work; within that Provider, de-duplicate rows that resolve to one stable identity but reject two distinct remaining sources.
- [The locked Provider lacks a later segment that another Provider has] → Stop with a silent unmatched remainder; never cross providers automatically because the first segment established the source-family trust boundary.
- [The first Episode of a logical remainder has missing or incorrect year] → Use only that Episode's positive year as specified; missing year cannot earn the 20-point component, favoring a conservative non-match over a Season-level fallback.
- [Provider details change between preview and download] → Re-resolve compact selections, compare current generation and the canonical fingerprint, and perform zero writes on stale evidence or mappings.
- [Recursive network work increases interactive latency] → Reuse canonical candidates, de-duplicate detail calls in one operation-local cache, stop on unknown coverage, and bound successful rounds by the initial unmatched count.
- [A candidate applies zero rows or repeats a prior source] → Require strict unmatched-count reduction and both lookup/stable used-identity checks before committing the round.
- [A yellow count warning could be mistaken for failure] → Render it only on the bound mapped group with established advisory styling; keep the remaining temporary group and download confirmation behavior unchanged.

## Migration Plan

1. Create the implementation branch/worktree from exact commit `07fbb408d54ee1b6201d4f217122079070527c5a`; verify ancestry and keep unrelated/user-maintained files out of the change. Before production edits, compare the active `fix-sparse-episode-number-alignment` delta with this change's already merged `Shared manual and automatic matching behavior` Requirement, record the dependency/synchronization order, and prove the merged sparse, S00, and recursion semantics remain intact.
2. Add pure title-family/Part parsing and state-machine tests first, including Arabic/Roman/Chinese forms, punctuation, S1 parent-only variants, explicit other-Season rejection, Part gaps, ambiguity, and no-downgrade outcomes.
3. Add the policy-gated remainder service and operation-local detail/evidence caches, then wire it after initial interactive Season plan construction for whole-Series positive Seasons and explicit single-Seasons including S00. Keep import/events/retry/replay on `BackgroundNonRecursive` with zero recursive calls.
4. Extend authoritative selection evidence, group advisory presentation, and fingerprint/rebuild regressions. Preserve protocol V22 unless a client-authored planning field is actually introduced; if so, advance both ends and reject V22 drafts rather than accepting mixed semantics.
5. Verify deterministic JOJO no-Part selection and Stone Ocean Part 2/3 recursion with cross-provider duplicates excluded and cross-arc Part candidates rejected, mixed parent-only aliases unable to bridge `星尘斗士` to `石之海`, same-Provider duplicates still ambiguous, Frieren E29-first-year logical S2 selection on the locked Provider, sequential Part 2/3/4, three-Episode rejection, singleton count-warning binding, used-source de-duplication, cancellation/timeout/incomplete coverage, strict progress bounds, Provider-lock drift, partial unmatched presentation, and download-time stale rebuild.
6. Run affected backend suites and frontend regressions, then strict OpenSpec validation, `git diff --check`, credential-safe scope scans, and one sequential clean Release build. Update release metadata to 2.0.7 and preserve cumulative release history and README demonstration assets.
7. Present the reviewed DLL/CustomCssJS pair and hashes for explicit approval before deployment, release, merge, or tag. After approval, back up the deployed pair/configuration, deploy atomically, restart Emby, and read back version, frontend cache marker, protocol, health, and representative previews.
8. Roll back by restoring the backed-up `2.0.6r2` DLL and V31 CustomCssJS pair and restarting Emby. Because the feature adds no durable schema and partial plans remain ordinary compact selections, no data migration or destructive cleanup is required.
