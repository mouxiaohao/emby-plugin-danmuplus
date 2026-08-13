## Context

See `proposal.md` for motivation. r6 has a dialog-scoped force-refresh value, an evidence-bound lazy candidate-detail operation, and authoritative composite Season plans. The temporary-run picker predates the shared lazy-detail row behavior: it mutates `season.Candidates`, automatically searches on entry, uses two titles depending on prior mapping state, and renders a score both inside the candidate summary and again in the reason line. Confirmed composite rows already carry local Episode names but not verified source Episode titles.

## Goals / Non-Goals

**Goals:**

- Make busy state a pure non-actionable rendering state while preserving force-refresh state.
- Make unmatched and mapped temporary-run entry converge on one range-scoped picker state.
- Reuse r6 candidate-detail security, caching, and stale-response handling.
- Transport public source titles from an already-resolved provider response into the preview without changing plan authority.

**Non-Goals:**

- No changes to r5 target-season eligibility, whole-Series S0 exclusion, explicit S0 support, group construction, scoring, ordering, confidence, confirmation, binding, persistence, or download execution.
- No real Emby temporary Seasons, collection/segment architecture, new provider resolution, or new candidate-detail endpoint.
- No change to Episode initial exact/manual behavior or mapping protocol 21.

## Decisions

### Busy renderers own no force-refresh control

`setBusy` will clear the footer and render only a cancel action when the current operation supports cancellation. It will not mutate, lock, snapshot, or reset `dialog.forceRefresh`. Every result, retry, cancellation, and failure renderer remains responsible for rendering the one compact checkbox when it becomes actionable again.

This is preferred over disabling the checkbox because the user explicitly does not want it visible before the dialog can be operated. The download-start progress state continues to use the existing force-refresh snapshot and lock semantics.

### One range-scoped picker, with authority preserved until apply

Both card actions will call the same temporary-run picker with the same fixed title `手动匹配未匹配临时季`. A mapped-run rematch adapter will derive the target run without permanently removing its existing selection; it will retain a rollback-safe draft until the user applies a replacement. Back, cancel, search failure, and candidate-detail failure return to the unchanged overview.

Candidate results will live in a range-keyed dialog store rather than overwriting the Season-wide candidate list. The key includes SeasonId, local run start ItemId, run length, authoritative plan generation/fingerprint, and search generation. This prevents a late response or another temporary run from leaking candidates into the current menu.

The temporary-range backend and apply semantics remain distinct from full-Season candidate confirmation even though the candidate row component is shared.

### Reuse the r6 lazy-detail contract

The picker will call `beginCandidateDetailGeneration` and mount the existing evidence-bound detail control for each candidate. Requests use the target SeasonId plus the candidate's site, id, and `SelectionEvidenceToken`. The range/menu generation is added to the client stale gate. A click stops radio-row propagation; errors stay local and retryable.

No candidate is parsed at render or automatic search completion. Provider resolution needed to apply a selection remains part of the existing authoritative confirmation path.

### Render score in one candidate metadata location

The shared row separates title metadata, score, and non-score decision information. It will not concatenate `candidateLine` with another `matchScoreLine`; a sanitizing presentation helper will suppress only the duplicated score fragment. Candidate order and numeric values remain server-authored.

### Source titles are response decoration, not plan authority

The composite build result will retain a bounded lookup from already-resolved source Episode identity to its title for the lifetime of the preview build. The public composite-group projection adds `SourceEpisodeName` beside the existing local `EpisodeName` and source Episode number. No additional `GetMedia` call is allowed.

Source titles are excluded from plan fingerprints, mapping identities, evidence, compact selections, task validation, and persisted metadata. If carrying a transient lookup through the build result proves incompatible with current return types, an additive non-authoritative mapping display field is acceptable only when every fingerprint/equality/persistence path explicitly ignores it.

All titles are bounded server-side and assigned with `textContent` client-side. The frontend formats `本地 <label> · <local title> → <source label> · <source title>` and omits separators for missing titles.

## Risks / Trade-offs

- [Mapped rematch accidentally removes the live mapping before confirmation] → Keep removal/replacement in a draft and restore the exact overview on back, cancel, or failure; add no-write and state-equivalence tests.
- [Late automatic range search paints another run] → Use a range/plan/search/evidence generation key and discard stale responses.
- [Candidate detail reuse crosses evidence rotation] → Retain SelectionEvidenceToken in the r6 fingerprint and add the range generation to the gate.
- [Titles change plan fingerprints or download validation] → Treat titles as bounded display decoration and assert identical fingerprint/selection/download sets with and without titles.
- [Shared row refactor changes full-Season or Episode behavior] → Reuse only the row-level presentation helper; keep workflow adapters and submission handlers separate.
- [Busy checkbox removal loses the user's value] → Test checked and unchecked values across initial search, rematch, detail, plan validation, cancellation, and recoverable failures.

## Migration Plan

1. Build r7 only from the verified r6 workspace and preserve the r6 scope/hash gates.
2. Add DTO/display fields additively while leaving mapping protocol 21 unchanged.
3. Raise plugin file/informational version and frontend cache marker to r7/V23.
4. Run r5/r6 scope, lazy-detail, mapping, force-refresh, backend, frontend, and Release gates.
5. Back up the active paired r6 DLL, CustomCssJS configuration, and plugin configuration; deploy atomically and restart Emby.
6. Roll back by restoring that paired r6 trio if health, hashes, logs, or live read-only acceptance fails.
