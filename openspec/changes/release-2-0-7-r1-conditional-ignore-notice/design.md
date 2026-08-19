## Context

See `proposal.md` for motivation and the delta spec for observable behavior. The 2.0.7 frontend already receives four server-authored ignored-scope counts and `scopeSummaryLine` uses them to render the optional `只读忽略 N 集` breakdown. The first r1 repair introduced a separate `scopePresentationLine` that recomputed the ignored total and appended the safety sentence after `scopeSummaryLine`; although its observable positive/zero behavior passed regression, that second presentation gate allows the breakdown and its mandatory suffix to diverge. The corrected design makes them one indivisible string branch.

The backend ownership filter, planner, composite request reconstruction, and download task builder already exclude ignored Episodes. This change must not duplicate those rules in presentation state or introduce a client-authored ignored flag.

## Goals / Non-Goals

**Goals:**

- Derive one normalized ignored total from the current Season response and use it consistently for both the ignored breakdown and the safety sentence.
- Ensure every render is stateless: a later zero-ignore response removes a notice shown by an earlier response.
- Deliver the browser fix as the complete 2.0.7r1 identity while preserving protocol and persisted data compatibility.

**Non-Goals:**

- Changing Episode ownership classification, ignored-count production, selectable virtual groups, authoritative mappings, or download filtering.
- Adding DTO fields, client-submitted planning evidence, configuration schema, persistence, or a mapping protocol revision.
- Inferring ignored items from displayed-minus-eligible arithmetic, rendered text, DOM nodes, candidate metadata, or historical UI state.

## Decisions

### 1. Normalize the four authoritative counts in one frontend helper

Add a narrow count helper that reads only `IgnoredParentZeroEpisodeCount`, `IgnoredOtherSeasonEpisodeCount`, `IgnoredUnknownParentEpisodeCount`, and `IgnoredInvalidEpisodeCount`, accepts Pascal/camel JSON casing through the existing value accessor, converts only finite positive values to non-negative integer counts, and returns their sum. Reuse the same normalized component values or helper semantics in the existing scope breakdown and in the sentence gate.

This is preferred over testing whether `scopeLine` contains `只读忽略`, parsing localized text, or comparing displayed and eligible counts. Those alternatives couple behavior to wording and can create false notices when counts are missing or when displayed inventory includes other presentation-only differences.

### 2. Build one complete ignored-summary branch

Continue rendering the scope summary whenever its existing displayed/eligible/ignored data makes it non-empty. When normalized ignored total is positive, the same `scopeSummaryLine` branch that constructs `只读忽略 N 集（分类）` must construct `只读忽略 N 集（分类）。忽略项不可选择，也不会进入下载。` as one final string. When there is no ignored breakdown branch, the safety suffix cannot exist. There is no separate `scopePresentationLine`, no second ignored-total gate, and no renderer-specific suffix append.

Composite, non-composite whole-Series, and direct single-Season renderers consume that one `scopeSummaryLine` result verbatim. Normal displayed/eligible punctuation remains unchanged, so this structural correction does not alter the observable positive-once/zero-never contract.

Do not persist a notice flag on the dialog or Season. Whole-Series, single-Season, rematch, and rebuild already replace the rendered result from the current response; a pure response-derived gate guarantees stale notices disappear with the old DOM.

### 3. Verify existing safety independently from presentation

Frontend regression shall prove ignored rows remain absent from rendered selectable virtual groups and submitted composite selections. Existing backend scope/planner/download regressions remain the authority for zero execution. Hiding the sentence when the count is zero does not weaken a server fence, and showing it when positive does not create one.

This is preferred over modifying the controller or response model because the current counts and execution filters are already authoritative and deployed.

### 4. Stamp a complete 2.0.7r1 pair without protocol migration

Keep assembly version `2.0.7.0`; set file version `2.0.7.1`; set informational, configuration, and TMDB User-Agent versions to `2.0.7r1`; advance the frontend installation marker from V32 to V33; expect generated configuration cache token `2-0-7r1`; retain mapping protocol V22. Update cumulative documentation and version assertions together so the DLL and frontend asset are reviewed as one pair.

No data migration is required. Rollback restores the previously reviewed 2.0.7 DLL/V32 pair; the response shape and V22 drafts remain compatible in both directions.

## Risks / Trade-offs

- [Invalid numeric values accidentally show the notice] → Treat missing, null, non-numeric, non-finite, negative, and zero values as zero before summing.
- [The breakdown and sentence disagree] → Construct both as one indivisible `scopeSummaryLine` branch, keep the literal in that branch only, and test every individual category plus mixed categories.
- [A notice survives rematch] → Rebuild it only from the current response and test positive-to-zero rerender.
- [A browser-only change is served from cache] → Advance the installation marker to V33 and the generated configuration cache token to `2-0-7r1`.
- [Version-only backend edits regress matching] → Keep backend behavior files outside the implementation allowlist and run existing target-scope/download regressions plus a clean Release build.

## Migration Plan

1. Apply the frontend and deterministic regression change before version stamping.
2. Update the complete r1 identity, cumulative docs, and version assertions; build and audit a paired DLL/V33 asset.
3. Keep the pair local until separately approved. If deployment is later approved, back up the active 2.0.7 DLL/V32/configuration pair, replace atomically, restart, and verify health/version/V33/V22 plus one Season with ignored items and one without.
4. Roll back by restoring the verified 2.0.7 DLL/V32 pair and restarting; no database or configuration migration is needed.
