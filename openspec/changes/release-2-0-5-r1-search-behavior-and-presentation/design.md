## Context

See `proposal.md` for motivation and the four delta specs for observable behavior. The implementation baseline is the clean `f8a4356` 2.0.4r2 tree on `codex/2.0.5r1-matching-behavior`.

The current search engine uses one `keywordOverride` branch for several different intents. Default metadata search builds and scores canonical candidates; an explicit keyword containing a non-whitespace character still passes through eligibility, merge, score, ordering, and the sixty-row projection; TMDB fallback mutates the canonical candidate set. Provider work already executes independently, but any non-completed diagnostic sets aggregate `IsComplete=false`, and automatic consumers treat that aggregate bit as a hard stop even when a completed provider supplied a unique high-confidence candidate.

Search timing is spread across the shared policy options, the operation registry, automatic search wrappers, and composite target coordination. The per-provider and global gates also protect providers that ignore cancellation, so removing elapsed-time deadlines must not release a gate while its underlying request is still running. The Movie/Episode download arbiter and seven-day duplicate/replay system are separate mechanisms and remain unchanged.

The browser currently creates the composite guidance inside every Season summary, labels every non-ProviderId failed Season action `查看候选`, and can surface backend alias provenance through generic decision/diagnostic rendering. Candidate detail, selection evidence, identifier redaction, authoritative mapping, download, and metadata persistence remain trusted server responsibilities.

## Goals / Non-Goals

**Goals:**

- Represent default automatic discovery, explicit scored manual-keyword discovery, and TMDB-exhaustion parent-title rematch as distinct request/result contracts so one path cannot silently inherit another path's policy.
- Derive automatic usability from completed-provider evidence plus structural validity, not from a single aggregate completeness bit.
- Remove only the shared 10/30/45-second search deadlines while preserving prompt explicit cancellation and concurrency ownership.
- Keep each l1-l10 behavior change independently revertible with its focused tests.

**Non-Goals:**

- Rewriting provider HTTP clients, request encoding, typed Bilibili retrieval, provider cache TTLs, retry/rate-limit policy, or download logic.
- Replacing provider-owned manual-search normalization, candidate eligibility, `MergeSources`, ordinary scoring/reasons, or the established provider-fair sixty-row `OrderCandidates` projection.
- Removing the 180-second tracked single-target download deadline, the seven-day duplicate/replay policy, or user cancellation.
- Weakening evidence validation, exposing internal identifiers or credentials, accepting browser-authored source metadata, or changing authoritative Episode mapping and persistence.
- Creating a synonym database, changing the standard automatic confidence threshold or ordinary evidence weights, or replacing the removed 79/85 behaviors with equivalent rules.
- Pushing, releasing, merging, or deploying without a later explicit authorization.

## Decisions

### 1. Use independent additive discriminators for l6 and l10

Preserve the existing default metadata/automatic-discovery contract without introducing a shared three-value enum, request type, endpoint, or implementation symbol owned by either behavior slice. l10 adds only its own exact `manual-keyword` request/result intent for a browser-edited keyword containing at least one non-whitespace character. l6 separately adds only its own `parent-title-rematch` action/request/state after TMDB alias exhaustion. No production or test symbol introduced by l6 may be required by l10, and no symbol introduced by l10 may be required by l6.

Default discovery continues through eligibility, canonical merge, scoring, ordering, confidence selection, and TMDB eligibility. l10 continues through the same provider discovery, eligibility, merge, score/reason, and visible projection stages, but stops before TMDB alias expansion and `ClassifyResult`. l6 parent-title rematch issues exactly one parent-title term and then rejoins the automatic Season scoring path with TMDB expansion disabled for that request. A request that combines the l6 rematch discriminator with an explicit keyword is invalid and issues no provider call, so parent-title rematch can never fall into l10 manual-keyword discovery.

The server derives each behavior from its own additive input and never trusts a client-supplied score or provenance. Empty or whitespace-only manual input is rejected before provider access. A valid keyword follows the existing browser/server outer trim, provider-owned normalization, and transport encoding; l10 does not promise byte-for-byte preservation or replace those established optimizations. The independent branches guarantee that reverting either l6 or l10 leaves the other branch compilable and behaviorally isolated.

### 2. Keep manual-keyword discovery on the scored pipeline and stop automatic decisions

Manual-keyword discovery uses the established provider adapters and candidate eligibility, then applies the existing `MergeSources`, ordinary Season/Movie scoring and reasons, and `OrderCandidates`. That projection retains its existing semantics: candidates are canonically ordered by score and existing tie breakers inside each configured provider `SourceOrder`, then the provider-fair allocator exposes at most sixty rows. Movie explicitly retains zero-score candidates so a user can still review them; Season scoring already retains its scored canonical rows.

The l10-owned `manual-keyword` result intent tells the frontend to show the server-provided score and reason while leaving every row unselected. The branch MUST NOT invoke TMDB aliases, remove a row merely because it misses an automatic confidence threshold, call `ClassifyResult`, set `AutoSelected` or a selected candidate, start a download, persist a binding, or write metadata. Even an exact-looking or high-scoring row waits for explicit user selection.

Reuse the ordinary server-stamped target-bound candidate evidence and the existing detail, authoritative mapping, download, and persistence paths after selection. Internal evidence and identity values remain redacted from the public protocol. Because the established sixty-row projection and evidence registry remain in force, l10 does not add an unbounded response or an atomic evidence-batch subsystem.

l10 depends only on search and evidence interfaces that predate l7. Reverting l7 may restore the former shared 10/30/45-second deadlines but cannot change manual-keyword scoring, ordering, or explicit-selection behavior. Reverting l10 cannot change l6 parent-title rematch or l7 deadline policy.

### 3. Separate provider coverage from parent cancellation and structural safety

Track provider outcomes so consumers can distinguish:

- providers that completed and contributed zero or more candidates;
- provider-local transport/adapter faults;
- explicit parent/user cancellation;
- target inventory or authoritative-plan invalidity.

Provider-local faults keep `SearchErrors`/public diagnostics but do not invalidate completed-provider candidates. Default automatic selection runs against only the completed-provider canonical set. Automatic download still requires the selected candidate's authoritative plan to be current and structurally valid. All-provider failure, parent cancellation, no ordinarily confident candidate, or a structurally incomplete plan remains fail-closed.

This replaces using aggregate `IsComplete=false` as both a coverage diagnostic and an automatic safety decision. The aggregate field may remain for wire compatibility, but business gating must use the more specific outcome facts.

### 4. Remove elapsed-time races without removing cancellation or gates

The l7 slice removes the production defaults and `CancelAfter`/deadline-token creation that enforce 10 seconds per provider, 30 seconds interactive, and 45 seconds automatic. The shared execution coordinator continues to:

- acquire one per-provider lease and the global concurrency lease;
- race the underlying request only against explicit caller/parent cancellation;
- return cancellation promptly when possible;
- retain the provider lease until a non-cooperative underlying task actually settles;
- observe late faults for logging without leaking response bodies or secrets.

Provider-native HTTP timeouts or failures are ordinary provider-local faults under l3. The download arbiter's 180-second deadline and every seven-day freshness/replay call site remain covered by unchanged regression tests.

Keeping the existing policy type as a concurrency coordinator, with timing options removed or made non-operative only at the shared search layer, is preferred over deleting it: it centralizes gate ownership and prevents concurrent pressure on one provider.

### 5. Model TMDB exhaustion as a terminal alias state, not a candidate merge

Alias orchestration keeps its existing eligibility and deterministic Chinese/English/Japanese attempt order. Track whether an alias was attempted and whether any alias reached the automatic threshold. Only a threshold-reaching alias may replace the automatic canonical set. A request fault for one alias remains server-local and does not prevent later eligible aliases from running; exhaustion exists only after every eligible alias either faults or completes below the threshold.

If the plan exhausts without one, do not call the current alias-candidate replacement helper. Return an additive server state indicating `parent-title-rematch-available`, with no alias candidate rows in the public candidate list. Keep credential-safe operational failures in server logs, but do not serialize alias values or TMDB-specific completion diagnostics for browser rendering.

The frontend maps this state to `重新匹配`. Its request carries the dedicated l6-owned parent-title-rematch discriminator and no manual keyword; the server resolves the authoritative parent title and performs a fresh single-term automatic search. Missing parent title is retryable and issues no provider request.

This is preferred over frontend filtering of repeated candidates: the browser cannot safely identify alias provenance or reconstruct the correct automatic scoring input, and hidden duplicate rows would still remain in selection evidence.

### 6. Remove the two scoring overrides as separate pure-rule slices

l8 removes the contradiction cap application and its now-unused helper/tests while leaving ordinary year and Season evidence weights intact. l9 changes effective confidence to the ordinary candidate score and removes only the 0.85-to-threshold fidelity promotion; fidelity normalization/evidence and exact-score tie resolution remain.

The two edits and their fixtures stay in separate commits even though they touch the same scorer. l8 tests prove a conflict can cross 0.79 according to ordinary evidence. l9 tests prove a base 0.85 candidate remains 0.85 while equal-score fidelity tie behavior still works. Neither slice changes the standard automatic threshold.

### 7. Keep presentation edits literal and rollback-friendly

l1 changes the guidance text source to the remaining sentence only. l2 moves the single reusable guidance element from per-Season composite rendering to the result container without editing its text. Therefore reverting l1 after l2 restores the old full sentence once, and reverting l2 after l1 restores the shortened sentence per card; either tree remains functional.

l4 changes only the heading text node; l5 changes only the `源码` href. Focused regression assertions freeze plugin page/resource names, assembly and plugin identity, saved configuration fields, and all unrelated links.

l6 uses explicit backend state for action labeling and diagnostic suppression; generic non-TMDB provider diagnostics remain rendered. The browser never re-scores, reorders, or infers alias exhaustion.

### 8. Treat l1-l10 and release metadata as explicit delivery slices

Implementation commits SHALL be labeled by l number and include their own production and test changes. Recommended dependency-safe order is l1, l2, l3, l4, l5, l10, l7, l8, l9, l6, followed by one version/documentation commit. l6 and l10 MUST use independent additive DTO fields or endpoints, branches, result states, and tests; they MUST NOT share an enum, request type, endpoint, or other implementation symbol introduced by either slice. Reverting either slice from the completed tree SHALL leave the other operational without a compatibility shim.

The final metadata slice sets assembly/file version `2.0.5.1`, informational/configuration version `2.0.5r1`, advances the frontend installation/cache marker needed for the changed JavaScript, and adds a cumulative 2.0.5r1 update entry without removing prior release history or README demonstration assets.

Record every l commit hash and its focused evidence without rewriting or squashing those commits. The user has explicitly waived the exhaustive ten-tree inverse test matrix for this cycle and requested live deployment testing first; the independent commit boundaries and rollback commands remain documented.

### 9. Make Episode-count differences advisory and server-authoritative

The current Season scorer is refined to 60 percent parent-title evidence, 20 percent Season-name evidence, and 20 percent exact known-year evidence. Episode count contributes zero and cannot affect confidence, candidate order, or threshold classification. It remains part of the candidate explanation only as neutral metadata and remains an input to the authoritative Episode planner. Movie scoring is not changed.

Do not let the browser compare candidate `EpisodeSize` with a displayed or mapped count. Search metadata can be stale, incomplete, or describe a different provider projection. Add one response-only Season result flag, `HasVerifiedSourceEpisodeSurplus`, whose only authority is the provider Episode detail actually consumed while building the current `CompositePlan`. The planner compares every successfully applied source independently with the full `SeasonPlanningContext.LocalEpisodes` eligible inventory and ORs the result; it never sums multiple sources. A missing, failed, cancelled, stale, evidence-invalid, or zero-mapping plan leaves the flag false.

Both whole-Series and single-Season flows already converge on the same composite Season summary. Render the yellow `库内集数少于来源集数` notice there exactly once and only when the response flag is true. The flag is advisory: local shortages do not create synthetic Episodes or block matching, mapping, download, or persistence. Equal counts produce no notice. A larger local inventory also produces no notice and continues through the existing `UnmatchedRuns` temporary-Season behavior.

## Risks / Trade-offs

- [A provider can remain hung indefinitely after shared deadlines are removed] → Preserve explicit cancellation, provider-native transport safeguards, per-provider gate ownership, and visible in-progress state; document that the user may need to cancel a non-cooperative search.
- [A non-cooperative task can retain a provider gate after user cancellation] → Keep settlement tracking and release the lease only when the underlying task ends; other providers continue independently.
- [Provider-local failure can now permit an automatic match that was formerly blocked] → Require ordinary confidence plus current authoritative-plan validation, retain the failed-site diagnostic, and keep all-provider failure and structural ambiguity fail-closed.
- [A high-scoring manual-keyword result can look automatically acceptable] → Show its ordinary score/reason but never preselect it, call `ClassifyResult`, download, bind, or write metadata before explicit evidence-validated selection.
- [The established sixty-row projection can omit lower-ranked manual candidates] → Preserve the existing provider-fair algorithm and provider-side search optimizations rather than inventing an unbounded result path; the user may refine the keyword and search again.
- [Removing the 79 cap can raise a contradictory candidate above automatic confidence] → This is intentional; retain visible ordinary evidence and deterministic conflict fixtures so the new result is explainable.
- [Removing the fidelity bridge lowers recall for symbol-sensitive near-threshold matches] → This is intentional; preserve fidelity as tie evidence and require ordinary scoring to reach the threshold.
- [l6 and l10 touch adjacent search plumbing and may make a simple Git revert conflict] → Give each slice independent additive discriminators and result states, prohibit cross-references to symbols owned by the other slice, avoid opportunistic refactors, and record exact per-l commit hashes and rollback commands.
- [Configuration-page resource caching can mask l4/l5 during live checks] → advance the established configuration cache token/build resource and verify both source HTML and generated Release resource without changing the page route.
- [Search metadata can exaggerate or understate source Episode count] → compute the warning only from the provider Episode details used by the authoritative plan and expose a response-only Boolean; never infer it in the browser.
- [A source surplus could be mistaken for a structural mapping failure] → keep the state advisory and require only that the current plan already has an applied mapping; do not change scoring, confidence, mapping, download, or persistence gates.

## Migration Plan

1. Implement and test each l slice on the isolated `f8a4356` worktree, committing production and focused regression changes together under its l label.
2. After every slice, run its focused regressions and a compile check before starting a later slice that touches the same file.
3. Apply the separate 2.0.5r1 metadata/documentation slice, then run frontend regressions, backend deterministic suites, strict OpenSpec validation, `git diff --check`, credential-safe scope scans, and a sequential Release build.
4. Record the ten independent commit hashes and rollback commands. Do not spend this cycle on the waived ten-tree inverse matrix before live testing.
5. Under the user's existing live-validation authorization, back up the paired DLL/configuration and CustomCssJS asset, deploy the locally verified Release pair, restart Emby, and exercise representative Series/Season, JOJO alias-exhaustion, provider-failure, long-running/cancelled search, scored manual keywords, seven-day replay, and the 180-second download boundary.
6. Roll back a single l with its recorded revert commit when only that behavior must be undone; restore the full pre-2.0.5r1 backup if a cross-cutting deployment failure occurs. No data migration is required.
7. Do not push, merge, tag, publish a GitHub Release, or perform any external action beyond the specifically authorized Synology/Emby live validation.
