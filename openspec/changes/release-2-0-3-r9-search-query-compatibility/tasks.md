## 1. Confirmed diagnosis and source alignment

- [x] 1.1 Record the deployed r8 MatchPreview reproduction proving DandanPlay returns four English-alias records before candidate eligibility removes them.
- [x] 1.2 Locate the r9 build source corresponding to the deployed `IsEligibleSeasonCandidate` and bounded-search pipeline without overwriting unrelated worktree changes.
- [x] 1.3 Record the provider compatibility audit: DandanPlay accepts `%20`, form-style `+`, and literal-plus `%2B`; identify any verified provider-specific exception.

## 2. Manual eligibility implementation

- [x] 2.1 Change explicit manual custom Season search eligibility to require only usable identifier, non-empty title, and Season-compatible media type.
- [x] 2.2 Preserve metadata title-evidence eligibility for searches without an explicit custom keyword.
- [x] 2.3 Preserve target-metadata scoring, provider-neutral ordering, confidence gating, result bounds, cancellation, and provider failure isolation.

## 3. Automated verification

- [x] 3.1 Add predicate tests for an English alias returning a Chinese Season title, plus invalid identifier/title and Movie exclusions.
- [x] 3.2 Add full search-engine regression proving `one punch` and `one punch man` retain DandanPlay Season 3 and do not suppress successful providers.
- [x] 3.3 Add regressions for internal spaces, literal `+`, non-ASCII queries, and strict automatic-search rejection of unrelated results.
- [x] 3.4 Run the full available regression suite and confirm deterministic ordering and automatic confidence behavior remain unchanged.

## 4. Live regression and release verification

- [x] 4.1 Produce the 2.0.3r9 Release build/package and verify its version metadata.
  - [x] 4.1.1 Update the r9 source version metadata while retaining AssemblyVersion 2.0.3.0.
  - [x] 4.1.2 Add and run the r9 narrow-delta scope verification.
  - [x] 4.1.3 Produce the Release build/package and verify compiled version metadata.
- [x] 4.2 Back up r8, deploy r9 to Emby, restart safely, and verify plugin startup.
- [x] 4.3 Repeat the Season 3 manual MatchPreview with `one punch`, `one punch man`, `one+punch`, `一拳超人`, `一拳 超人`, and `一拳+超人`, confirming DandanPlay candidates and third-season availability according to the provider response.
- [x] 4.4 Verify an automatic library-import search still rejects unrelated provider results and record rollback readiness.
