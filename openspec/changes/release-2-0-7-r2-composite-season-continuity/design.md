## Context

See `proposal.md` for motivation and the two delta specs for normative behavior.

The 2.0.7r1 baseline already has the required lower-level pieces, but they are scoped to one local Season:

- `CompositeSeasonTargetSetCoordinator` evaluates a Series target set sequentially, while every target callback currently derives its initial search Season independently from the Emby local Season.
- recursive remainder evidence distinguishes `part` from `logical-season`; `GetActiveLogicalSeason` advances only for a server-validated logical-Season decision. This distinction is the authoritative way to prevent any number of Parts from becoming Season arithmetic.
- `DanmuMatchSearchEngine` already accepts an explicit expected logical Season in its logical-remainder path, and TMDB aliases already pass through the common scorer. The public initial-Season entry point does not yet carry a logical Season/required-Provider context.
- the first selected source already locks all recursive remainder segments inside one local Season to one Provider.
- candidate tokens, plan generations, structure/plan fingerprints, and server-only remainder evidence already form the zero-write stale-data boundary.

The change crosses whole-Series orchestration, animation gating, search/scoring, source filtering, evidence, plan rebuilding, and release metadata. It must preserve C# 8/.NET Standard 2.0, mapping protocol V22, exact-parent Episode ownership, existing site priority for the chain-activating Season, and the separation between interactive and unattended behavior.

## Goals / Non-Goals

**Goals:**

- Enable a logical continuation chain only for a user-initiated full-Series match of a server-recognized animation Series.
- Seed the chain from a complete local physical Season that truly spans more than one logical Season, then propagate it through any later adjacent complete targets.
- Lock the active chain to one ProviderId while allowing each logical Season to use its own CandidateId/MediaId.
- Treat N Part segments inside logical Season K as terminal logical Season K for every positive N; never derive K+N or Part N+1 for the next local physical Season.
- Feed one server-owned expected logical Season/Provider pair through standard search, TMDB alias search, automatic/manual candidate evidence, composite rebuild, and remainder recursion.
- Preserve local Season/Episode ownership and fail with existing unmatched/stale behavior rather than cross-provider or local-ordinal fallback once a continuation attempt is active.

**Non-Goals:**

- Cross-local-Season continuation for an explicitly targeted Season, a Series request narrowed to one Season, manual-keyword single-target discovery, non-animation Series, Season 0, or an unattended path.
- Persisting offsets or Provider locks in Emby ProviderIds, plugin configuration, browser state, XML files, or download history.
- Renumbering/reparenting local Seasons/Episodes, copying predecessor media identities/mappings, or hardcoding Bookworm title synonyms.
- Carrying Part continuity across local physical Seasons.
- Changing mapping protocol V22 or the existing 2.0.7 recursive-remainder policy inside one target Season.

## Decisions

### 1. Gate continuation once at the full-Series controller boundary

Before building target requests, compute one immutable eligibility flag:

- the resolved root item is a `Series`;
- the request is an ordinary user-initiated whole-Series/rematch operation, not a manual-keyword request or a Series request narrowed by Season context;
- the existing server animation classifier recognizes the authoritative Series metadata;
- positive Seasons are enumerated as the full target set, after the existing S00/unknown filter.

Only an eligible target set allows the coordinator to create continuation state. All other calls use a disabled/local-only context and keep their current behavior. Animation is not inferred from candidate categories or browser fields. If authoritative animation metadata changes before execution, continuation evidence becomes stale.

Rationale: the controller knows request intent and authoritative item type. Gating here makes single-Season/background zero-preflight behavior directly testable and prevents shared search helpers from silently expanding work.

Alternative considered: enable the rule whenever a candidate category says Anime. Rejected because the candidate is discovered after the cross-target decision and is provider-authored rather than library authority.

### 2. Carry one immutable logical-Season/Provider proof

Introduce a server-only `SeasonLogicalContinuationProof` and a small `SeasonLogicalTargetContext` (final names may follow repository conventions). The proof records:

- Series ItemId and the full-Series/animation eligibility decision;
- predecessor/current Season ItemIds and positive local ordinals;
- predecessor generation, structure fingerprint, and plan fingerprint;
- predecessor initial and terminal logical Season ordinals;
- expected current logical Season (`terminal + 1`);
- required ProviderId;
- exact eligible/mapped ItemId coverage and a flag proving that a logical-Season advance, not a Part count, activated the chain.

The target context exposes an effective initial logical Season plus optional required Provider/proof. It is constructed only by server orchestration, cloned when stored in evidence/build state, and ignored by public serialization (`JsonIgnore`/`IgnoreDataMember` according to existing model conventions).

Rationale: logical ordinal and Provider are one atomic continuation constraint. A typed immutable object prevents search, selection, and rebuild from accepting one without the other.

Alternative considered: add fields to V22 browser JSON. Rejected because a client could manufacture an offset/site lock and because no new wire-authored fact is needed.

### 3. Activate and propagate the chain in the sequential target coordinator

Extend `CompositeSeasonTargetRequest` with local Season number and a callback that accepts `SeasonLogicalTargetContext`. Keep a compatibility/local-only overload for existing independent harnesses. For an eligible whole-Series target set, the coordinator holds operation-local state:

`active`, `previousLocalSeason`, `terminalLogicalSeason`, `requiredProviderId`, and the predecessor proof.

Processing rules:

1. With no active chain, build the target under current independent behavior across enabled Providers.
2. After a target completes, activate the chain only when its authoritative plan has full unique eligible coverage, current generation/fingerprints, one stable Provider, and at least one validated `logical-season` transition so `terminalLogicalSeason > initialLogicalSeason`.
3. For an active chain and exactly adjacent target, pass `terminal + 1` and the unchanged required Provider.
4. If that target completes authoritatively, update the terminal/proof and keep the chain active even when the target contains only one logical Season.
5. A gap clears the state before the non-adjacent target. Cancellation, ambiguity, staleness, partial coverage, Provider mismatch, or unmatched state prevents that target from advancing the chain. The failed continuation target remains unmatched; a later independent complete multi-logical-Season target may seed a new chain.

The controller populates a server-only result summary from the authoritative composite build; the coordinator consumes that summary rather than parsing public display fields or source titles.

Rationale: the coordinator already guarantees deterministic ascending evaluation and cancellation. Operation-local state cannot become a stale Series-wide cache.

Alternative considered: persist the last logical Season by Series/Season ID. Rejected because library inventory, aliases, providers, and partial plans can change and would require migration/invalidation semantics.

### 4. Derive terminal Season only from typed logical-Season decisions

Use the initial logical Season plus validated remainder evidence to calculate the terminal:

- `DanmuRemainderDecisionKinds.LogicalSeason` / logical-Season stage may set the next active logical Season after its existing `current + 1` validation;
- `Part`, metadata, count-warning, and every other segment keep the active logical Season unchanged;
- segment count, Part number, title suffix, and source ordinal are never added to the logical Season number;
- chain activation requires an actual logical-Season advance, not merely more than one selection.

Consequences are deliberately general:

- local S1 = logical S1 Part 1 + Part 2 -> terminal S1; no chain seed; local S2 uses ordinary logical S2 matching, not Part 3 or logical S3;
- local physical Season = N Parts of logical K -> terminal K for every positive N;
- if an earlier collection already activated a chain and current local Season starts at logical K but contains N Parts, the next adjacent local Season expects K+1, not K+N and not Part N+1.

Rationale: 2.0.7 already models Part and logical-Season continuation as different decision kinds. Reusing that closed evidence is stronger than reparsing titles or counting plan groups.

Alternative considered: infer terminal Season from selection count. Rejected because it creates exactly the Part 1/Part 2 -> Season 3 defect identified in the acceptance boundary.

### 5. Add an initial search context with a hard Provider filter

Expose an immutable initial-search context or overload on `DanmuMatchSearchEngine.SearchSeasonAsync` containing `ExpectedLogicalSeasonNumber` and optional `RequiredProviderId`. Existing callers default to local ordinal and all enabled Providers.

For a continuation target:

- filter the enabled scraper set to the exact required Provider before any standard or alias round;
- build Season-number scoring evidence from the logical ordinal (for example canonical `Season 4`) rather than the conflicting local display name (`Season 2`);
- retain the current local Series title/aliases, target year, eligible Episode count, original titles, bounded-search/cancellation policy, and automatic confidence threshold;
- run TMDB alias discovery only through the locked Provider's supported path;
- never consult another Provider or retry with the local ordinal if the locked Provider fails or lacks confidence.

The first chain-activating Season remains unchanged: all enabled Providers participate and current global score/site priority selects the winner. In the Bookworm acceptance case, DandanPlay wins S1 under existing priority, remains the recursive S1 Provider, and becomes the cross-target lock for S2. S2 may select a new DandanPlay CandidateId representing `小书痴的下克上 第四季`.

Rationale: filtering before provider calls is an auditable same-source guarantee and prevents a different-site candidate from being retained and later selected accidentally.

Alternative considered: search all Providers then filter only auto-selection. Rejected because manual/rebuild paths could still expose or reuse forbidden different-Provider evidence.

### 6. Bind continuation context to candidate evidence and composite fingerprints

Extend candidate evidence registration with target generation, effective initial logical Season, required ProviderId, and a cloned continuation proof. Automatic and reviewable candidates for a continuation target receive tokens only if their site matches the lock.

When a candidate is inspected, selected, or rebuilt:

- resolve the token against target ItemId, current generation, required Provider, site, and candidate ID before any upstream detail call;
- recover logical/Provider context from the server token rather than request JSON;
- initialize first-segment build and `ExtendInteractiveRemainderPlanAsync` from the evidence's logical Season;
- retain the same Provider as that current local Season's own first-segment/remainder lock;
- require every logical remainder to advance monotonically and every Part remainder to preserve the active logical Season.

Include initial/terminal logical Season, required Provider, chain activation identity, and predecessor proof identity in server-side `CompositePlanBuild` state and plan-fingerprint input. Public compact selections remain V22 and serialize none of the proof.

Rationale: the existing bounded candidate registry already protects source metadata and remainder decisions. Extending it keeps one trust boundary and makes browser Provider/ordinal substitution fail before source resolution.

Alternative considered: recompute the chain from titles at download time. Rejected because upstream results can change and because title parsing cannot author same-source or Part semantics.

### 7. Revalidate both targets without merging ownership

Before queueing or writing a continuation-adjusted plan, enforce all existing target generation, candidate expiry, live source rebuild, structure fingerprint, and plan fingerprint checks plus:

- the Series still qualifies as an animated whole-Series operation;
- predecessor/current ItemIds and local adjacency match the proof;
- predecessor generation, structure, plan fingerprint, complete ItemId coverage, terminal logical Season, and required Provider remain current;
- every current source/evidence token belongs to the required Provider;
- current initial/remainder logical evidence agrees with the proof;
- the rebuilt fingerprint including continuation fields matches preview.

Any mismatch returns stale/invalid-plan behavior with zero XML, identifier, or metadata writes. Fallback to another Provider or the local ordinal is allowed only for a fresh non-continuation target, never while executing a continuation proof.

Across targets, carry only logical ordinal and ProviderId. Do not copy CandidateId/MediaId, Episode mappings, exclusions, local ownership, or write eligibility. Each target keeps exact-parent scope and independently decides complete binding versus composite-safe writes.

Rationale: same Provider is a search constraint, not shared media ownership. Distinct source Seasons necessarily have distinct provider media records.

Alternative considered: reuse the predecessor's media ID as proof of same source. Rejected because that would point S2 back to the already consumed S1-S3 record instead of the provider's S4 record.

### 8. Preserve explicit single-Season and background behavior

Explicit Season, narrowed Series, manual-keyword single-target, import, item-added, automatic download, retry, and replay calls never construct a cross-target context or enumerate a predecessor. They continue to use their local Season ordinal and their existing 2.0.7 within-target remainder policy. Whole-Series still removes S00 before creating the positive target set, so S00 neither activates nor resets the chain.

Rationale: this implements the confirmed “animation + whole-Series only” boundary and prevents hidden provider work outside the visible user operation.

Alternative considered: precompute S1 when a user opens Bookworm S2 directly. Rejected because the user explicitly limited this feature to whole-Series matching.

### 9. Preserve release and wire compatibility

2.0.7r2 keeps `AssemblyVersion` at `2.0.7.0` and `DanmuMappingProtocol.CurrentVersion` at V22. Update `FileVersion` to `2.0.7.2`, informational/config/User-Agent labels to `2.0.7r2`, and advance the frontend cache marker according to repository sequence. README/UPDATE and the review package describe the animated whole-Series/same-Provider boundary and the Part-N rule.

Rationale: all new facts are server-owned and process-local, so no request protocol or persisted-data migration is required.

Alternative considered: bump V22 for continuation fields. Rejected because those fields must not be client-authored or round-tripped.

## Risks / Trade-offs

- [Animation metadata is absent or mislabeled] -> Treat the Series as ineligible and preserve ordinary matching; never infer eligibility from a candidate or browser. Users can correct authoritative library metadata and retry.
- [The locked Provider has no valid next Season while another Provider does] -> Leave the continuation target unmatched by design; same-source correctness takes precedence over coverage.
- [Part count is mistaken for logical Season progress] -> Derive terminal state only from typed `logical-season` evidence and add property-style tests across multiple N values plus explicit Part1/Part2 acceptance.
- [A provider disappears between preview and execution] -> Fail the token/rebuild as stale with zero writes and require a new whole-Series preview.
- [A complete-looking plan contains duplicate or foreign Episode ownership] -> Require exact-parent unique eligible ItemId coverage and matching structure/plan fingerprints before it can seed or advance a chain.
- [A stale predecessor changes the current target's intended Season] -> Revalidate predecessor generation/fingerprints/terminal/provider at execution; never downgrade an active proof.
- [Live DandanPlay/TMDB data changes] -> Keep deterministic fake-provider/alias regressions authoritative and use the real Bookworm library as a final deployment acceptance check, recording returned IDs/titles/Provider and plan fingerprints.
- [New proof fields leak into V22 JSON] -> Apply serializer-ignore attributes and assert response shape/protocol compatibility.

## Migration Plan

1. Implement on the exact 2.0.7r1 `origin/develop` baseline in the isolated 2.0.7r2 worktree, without modifying repository/local AGENTS files or unrelated OpenSpec state.
2. Add red deterministic tests first for animation/full-Series gating, general adjacent propagation, same-Provider filtering, gaps/failures/staleness, and Part counts including N=1, 2, 3, and a larger value.
3. Add a deterministic Bookworm TMDB-alias orchestration regression: local S1 selects DandanPlay and maps logical S1-S3; local S2 performs no other-Provider call and selects DandanPlay logical S4.
4. Introduce the server-only context/proof and search overload behind local-only defaults, then wire only the eligible whole-Series controller path and rebuild validation.
5. Run sequential Release build, all matching/composite/TMDB/S00/background/stale-zero-write regressions, strict OpenSpec validation, diff/credential/AGENTS scope audit, and package inspection.
6. Update paired backend/frontend version markers and cumulative README/UPDATE evidence. With explicit deployment authorization, deploy the paired assets and use the user's real `爱书的下克上` Series as the final acceptance test: local S1 must show DandanPlay `小书痴的下克上` S1/S2/S3 and local S2 must show DandanPlay S4.

Rollback is a paired return to the 2.0.7r1 plugin DLL and frontend asset. There is no schema/data migration and no new persisted field to remove; restarting the process clears in-memory proof. Existing ProviderIds, XML files, mappings, and local Season ownership remain untouched.
