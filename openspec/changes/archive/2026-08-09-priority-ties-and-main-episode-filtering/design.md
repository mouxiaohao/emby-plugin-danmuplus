## Context

See `proposal.md` for motivation. Candidate sorting currently uses score-component and textual tie breakers after the final score. Provider episode lists are built independently, and the Bilibili DTO uses serialization attributes that are not honored by `System.Text.Json`, leaving `badge_type` at its default value and allowing interleaved previews into positional episode mapping.

The plugin targets C# 8 and .NET Standard 2.0 and must preserve manual bindings, STRM libraries, seven-day skip behavior, partial XML results, retries, and the existing CustomCssJS workflow.

## Goals / Non-Goals

**Goals:**

- Make exact-score candidate ordering and cross-provider tie selection reflect the current provider configuration without changing score values or the ordinary score-gap threshold.
- Produce a canonical main-episode list before positional mapping, using the strongest provider evidence available.
- Keep manual, automatic, and retry paths behaviorally identical because they share provider media construction.
- Add deterministic regression coverage for JSON field mapping, filtering, duplicate resolution, and tie ordering.

**Non-Goals:**

- Arbitrarily choose among multiple equal-score candidates within the same highest-priority provider.
- Treat file size as proof that an episode is a preview.
- Delete or rename existing XML files created by an older faulty mapping.
- Invent a universal minimum duration that would break legitimate short-form series.

## Decisions

### Use configured provider index only after final score

Each candidate records the provider's index in the configured scraper list. Ordering is final score descending, configured provider index ascending, then existing component and textual tie breakers. This keeps all unequal scores provider-neutral. The confidence gate adds one explicit exception: when the minimum score is met and a highest-score tie spans providers, it selects the sole top-scoring candidate from the earliest configured provider. If that provider has multiple candidates at the same top score, the result remains ambiguous.

Priority-resolved ties do not trigger early termination after the parent-title search round. All season-specific fallback rounds complete first so later title, year, or episode evidence can replace the tie with a stronger winner; priority is consulted only for the final remaining tie.

Using each scraper's static default order was rejected because it would not reflect user configuration. Adding priority to the score was rejected because it could place a weaker match above a stronger one. Using title or identifier to auto-resolve a same-provider tie was rejected because those fields contain no confidence evidence.

### Normalize at provider episode-list construction

Filtering occurs before entries are copied into the shared media episode list, so all downstream download modes consume the same result without duplicating policy. Bilibili receives explicit `JsonPropertyName` mappings for underscored response fields and uses badge/section metadata as the primary classification signal.

A shared conservative classifier covers explicit non-main title markers for providers whose APIs lack structured flags. Existing structured provider rules (for example Tencent trailer flags and iQIYI content types) remain authoritative and are augmented rather than replaced.

### Resolve duplicate numeric episodes deterministically

When source titles expose a canonical numeric episode, entries are grouped by that number. Explicit main-content metadata wins; otherwise the longer entry wins, followed by stable source order. Entries without a trustworthy episode number retain stable order after explicit non-main filtering rather than being guessed into a number.

Duration is supporting evidence only in a duplicate group. There is no global short-duration cutoff because short-form programs are legitimate.

### Fail closed when provider metadata yields no main list

If a response contains entries but all are explicitly classified as non-main, the provider returns no usable media instead of falling back to the raw response. This converts silent wrong mapping into an actionable per-season failure.

## Risks / Trade-offs

- [Provider metadata semantics change] → Log raw/filtered counts and classification reasons; keep conservative title fallback and deterministic tests.
- [A legitimate special contains a marker such as “PV”] → Match explicit bounded markers and prefer structured metadata; do not use broad substring rules where ambiguity is high.
- [Some providers expose no episode number or duration] → Preserve their filtered source order rather than applying speculative re-numbering.
- [Surplus XML files from prior faulty runs remain visible] → Avoid destructive cleanup; report that they require explicit user cleanup after verification.
- [Current configured provider order is unavailable in isolated scoring tests] → Pass the search enumeration index into candidate metadata and test the comparer independently.

## Migration Plan

1. Build and run regression tests against synthetic Bilibili main/preview data and exact-score candidates.
2. Deploy the new DLL beside a timestamped backup of the current plugin and restart Emby.
3. Validate Bilibili season `46089` reports 28 main episodes and preserves ascending episode mapping.
4. Validate representative iQIYI, Tencent, Youku, Mgtv, and Dandan results do not regress.
5. Roll back by restoring the timestamped DLL backup if provider media construction or matching preview regresses; saved bindings require no migration.
