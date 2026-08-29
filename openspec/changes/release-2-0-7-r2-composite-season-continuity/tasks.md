## 1. Baseline and Red Tests

- [x] 1.1 Verify the isolated worktree is based on exact 2.0.7r1 commit `2f351cd6f0a08ab707d1d87d53935d6c53c723e0`, inventory unrelated changes, and confirm no AGENTS file is modified or created.
- [x] 1.2 Add a focused 2.0.7r2 regression harness covering animated full-Series eligibility, explicit single-Season/non-animation/background exclusion, and S00 pre-search exclusion.
- [x] 1.3 Add failing coordinator tests for generic adjacent propagation, later-chain activation, gap reset, incomplete/cancelled/stale non-propagation, and continued propagation through a single-logical-Season target.
- [x] 1.4 Add failing property-style Part tests proving any positive N Parts retain one terminal logical Season, S1 Part1+Part2 makes local S2 target Season 2, and an active logical K target with N Parts makes the next target K+1 rather than K+N or Part N+1.
- [x] 1.5 Add failing same-Provider tests proving the chain-activating target uses normal global priority, continuation searches only its Provider, different media IDs remain eligible, and no cross-Provider/local-ordinal fallback occurs.
- [x] 1.6 Add a failing deterministic Bookworm/TMDB-alias orchestration test for local S1 selecting DandanPlay source Seasons 1-3 and local S2 selecting DandanPlay source Season 4 with zero continuation calls to other Providers.

## 2. Server-Owned Continuation Model

- [x] 2.1 Implement immutable server-only logical target context, continuation proof, and target outcome models with clone/validation behavior and serializer exclusion from V22 responses.
- [x] 2.2 Include Series/request eligibility, adjacent Season identities, generation/fingerprints, initial/terminal logical ordinals, exact ItemId coverage, logical-advance activation, and required ProviderId in proof validation.
- [x] 2.3 Implement terminal extraction that advances only on validated logical-Season decisions and never uses Part number, Part count, selection count, or source ordinal as Season arithmetic.

## 3. Sequential Whole-Series Orchestration

- [x] 3.1 Extend `CompositeSeasonTargetSetCoordinator` requests/callbacks to accept local Season number and a logical target context while preserving a local-only compatibility path.
- [x] 3.2 Implement operation-local chain activation, adjacent propagation, same-Provider retention, gap reset, and non-propagation from incomplete/unsafe results.
- [x] 3.3 Add controller gating so only user-initiated full-Series requests for server-recognized animation enable continuation; narrowed Series, explicit Season, manual-keyword, S00, and unattended paths remain local-only.

## 4. Search, Alias, and Provider Enforcement

- [x] 4.1 Add an initial Season search context/overload carrying expected logical Season and optional required Provider without changing existing callers' default behavior.
- [x] 4.2 Filter the scraper set to the required Provider before standard and TMDB-alias calls, score against the derived logical Season, and return unmatched without other-Provider or local-ordinal fallback.
- [x] 4.3 Preserve the chain-activating target's existing global site priority and TMDB alias ordering, and verify Bookworm aliases remain generic rather than hardcoded.

## 5. Evidence, Rebuild, and Zero-Write Fences

- [x] 5.1 Extend target-bound candidate evidence with plan generation, effective initial logical Season, required Provider, and cloned continuation proof; reject mismatched Provider/target/generation before upstream detail resolution.
- [x] 5.2 Initialize composite planning and interactive remainder recursion from server-owned logical context, retain the current local Season's own media identity/mappings, and carry its required Provider only as a search/remainder constraint.
- [x] 5.3 Add logical context, Provider lock, activation identity, and predecessor proof identity to authoritative plan fingerprint inputs and server-only build/result state.
- [x] 5.4 Revalidate animation/full-Series eligibility, predecessor generation/structure/plan fingerprints, exact coverage, logical ordinal, and Provider before queue/write; prove stale or forged plans perform zero XML, identifier, metadata, and forbidden-provider calls.

## 6. Deterministic Regression and Compatibility

- [x] 6.1 Make the new 2.0.7r2 harness pass, including Bookworm, arbitrary-N Part, provider-lock, gap/failure, ownership, browser-forgery, and stale-plan cases.
- [x] 6.2 Run and pass the main regression suite plus composite-season planner, TMDB alias, remainder orchestration, target-scope/S00, background-nonrecursive, bounded-search, and frontend regression suites sequentially.
- [x] 6.3 Verify public V22 response/request shapes, explicit single-Season 2.0.7 remainder behavior, Provider-specific downloads, partial mappings, STRM ownership, retry/replay, and seven-day duplicate behavior remain compatible.

## 7. Version, Documentation, and Package

- [x] 7.1 Update 2.0.7r2 version markers: keep AssemblyVersion `2.0.7.0` and mapping V22, set FileVersion `2.0.7.2`, update informational/config/User-Agent labels, and advance the frontend cache marker without retaining the prior marker.
- [x] 7.2 Update cumulative README/UPDATE notes with the animation full-Series, same-Provider, general adjacent continuation, arbitrary-N Part, and Bookworm acceptance boundaries.
- [x] 7.3 Produce the 2.0.7r2 review/deployment package and verification record with DLL/frontend hashes, version/marker inspection, source-scope manifest, and no credentials or AGENTS files.

## 8. Build, Deploy, and Live Acceptance

- [x] 8.1 Run a clean sequential Release build, strict OpenSpec validation, `git diff --check`, and scoped secret/credential/AGENTS audit with zero new errors.
- [x] 8.2 Inspect the current Synology deployment read-only, record active version/paths/hashes/owner/mode, and create a timestamped paired backup of the active DLL, frontend asset, and relevant configuration before replacement.
- [x] 8.3 Deploy the verified 2.0.7r2 DLL/frontend pair, preserve owner/mode, restart Emby safely, and verify HTTP health, plugin load/version, frontend marker uniqueness, and startup logs; restore the backup immediately if health/startup validation fails.
- [x] 8.4 Authenticate to Emby, locate the real animated `爱书的下克上` Series, run the plugin's read-only whole-Series preview, and capture server evidence that local S1 maps on DandanPlay to `小书痴的下克上` Seasons 1-3 while local S2 maps on DandanPlay to Season 4.
- [x] 8.5 Verify the live preview used one Provider across the active chain, did not classify local S2 as Part N+1 or Season 3, performed no download/metadata writes, and retain the rollback backup plus documented recovery command.
