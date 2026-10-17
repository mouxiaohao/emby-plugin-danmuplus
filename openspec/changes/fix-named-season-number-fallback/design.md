## Context

The shared search engine already accepts an authoritative Emby Season item and a separate logical-season override. Ordinary scoring intentionally does not give a numbered candidate full named-season evidence. Composite recursion starts only after an initial source is confirmed and mapped.

## Goals / Non-Goals

**Goals:** Recover the first source when an unavailable descriptive Season name is the only obstacle; retain original metadata and existing authoritative composite planning.

**Non-Goals:** Change scoring globally, guess missing Season numbers, override ambiguous named matches, reset logical progress, change import recursion policy, or change unrelated NAS configuration.

## Decisions

1. Add one staged retry in shared Season discovery, after normal matching and aliases. Retry only for an identity-bearing descriptive Season label, a positive authoritative Season ordinal, complete non-cancelled search, no selected candidate, and no high-confidence named ambiguity under the current ordinary/TMDB threshold. Reusing the existing ordinary search path avoids a second scoring algorithm. Explicit keyword requests and explicit logical-season searches stay untouched.
2. The retry uses `Season N` and omits local Season-name aliases for that stage only. Parent-title aliases, year, episode inventory, the actual context item, cancellation, and provider priority are retained. Never rename the Emby item or insert a synthetic alias into first-stage scoring.
3. Merge both stages' untruncated canonical candidates by provider/id, preferring retry-stage scoring for duplicate identities. Keep named-only rows as server-only discovery evidence, excluded from automatic selection so a retry alias cannot lower their admission threshold. Aggregate diagnostics and completion facts; cancellation clears provisional choices. The existing composite remainder resolver receives the entire merged pool and continues using server-resolved source identities.
4. Keep fallback provenance on the internal search result. No controller/frontend protocol changes are necessary.

## Risks / Trade-offs

- [Extra parent-title requests] -> At most one additional ordinary numbered stage; generic labels and logical searches cannot recursively trigger it.
- [A generic candidate hides a valid named match] -> Complete named selection and strong ambiguity take precedence.
- [A transport failure is mistaken for a name miss] -> Do not start the retry from incomplete or cancelled discovery.
- [Composite progression resets to the physical ordinal] -> Only initial named discovery can retry; existing logical overrides remain authoritative and are covered by Bookworm fixtures.

## Migration Plan

No data migration. The user authorized deployment and a PR after acceptance. Build in the existing isolated worktree from verified latest main plus the exact confirmed-download/retry patch currently on NAS, then add the fallback. Back up the live DLL and configuration before atomically replacing only the DLL; retain and verify a DLL-only rollback. Preserve the original dirty checkout, current Danmu configuration and all CustomCssJS bytes. Bookworm validates composite/logical continuation, not natural descriptive-name fallback: use a separate real named Season or an explicitly isolated simulated input without renaming production metadata. Create a non-draft PR only after successful verification; no merge, tag or release is included.
