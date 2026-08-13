## Context

The implementation baseline is the exact source that produced deployed r5 DLL SHA-256 `123EE755F22AE20A1A2492F4D616C4B6F8CD232BFC629FAC25F0A4C466B8D552`. The r6 workspace is a full copy of that r5 workspace, not the polluted general development tree.

r5 already owns target-season inventory, exact parent-season eligibility, Series Season-0 exclusion, temporary-range planning, authoritative mappings, and the post-plan mapping-detail UI. r6 is a presentation/read-only-detail delta and must not route around or replace any of those components.

## Decisions

### Treat r5 behavior as an immutable compatibility boundary

No r6 discovery, detail, footer, or rendering code may change the Episode set consumed by preview, scoring, planning, confirmation, download, retry, automatic processing, or metadata mirroring. Existing r5 deterministic fixtures for S0, cross-season filtering, temporary ranges, and Series/Season parity are mandatory deployment gates.

### Add one read-only candidate-detail operation

The authenticated operation accepts target identity plus a candidate identity issued by the current preview and returns presentation-safe source Episode number/title rows. It resolves only the named candidate and does not confirm a selection, rebuild a Season plan, save a binding, start a task, or write metadata.

Preview-issued short-lived evidence binds target, provider, and candidate so arbitrary provider media IDs cannot be resolved. Evidence is transport-only and is never rendered.

### Gate inspection by workflow intent

The shared detail control is mounted only on manual rematch/search candidate lists. An initial exact Episode match keeps the r5 candidate/source-number controls and contains no `解析并查看详情` action. Clicking `重新智能匹配` or completing a manual keyword search enters the manual candidate state and enables the action.

### Keep candidate inspection separate from mapping details

Candidate inspection answers “what Episodes does this unconfirmed source contain?” The existing `查看集映射详情` answers “how does the confirmed authoritative plan map local Episodes to source Episodes?” They use separate state and renderers. The latter remains available wherever r5 exposed it, regardless of temporary ranges or candidate-detail state.

### Use dialog-scoped, compact force-refresh state

All pre-download footers render one checkbox whose visible label is exactly `强制刷新`. The checkbox reads/writes `dialog.forceRefresh`; no explanatory paragraph or close hint is rendered. At execution entry the value is copied once and locked. A multi-Season process reuses the copy; a startup failure before any tracked task restores the prior editable screen.

### Limit changes to existing r5 extension points

r6 may add the detail endpoint/DTO, dialog detail cache, detail button, and shared footer helper. It must not introduce new collection/segment planners, change collection rematch semantics, replace r5 overview renderers, or modify target-season enumeration/filtering.

## Verification strategy

- First run and record the unmodified r5 frontend/backend/build suites.
- Add deterministic call-count and no-side-effect tests for the detail operation.
- Add DOM tests distinguishing initial exact Episode from manual search and preserving mapping details.
- Re-run all r5 target-season fixtures including One Punch Man and Seitokai Yakuindomo.
- Package only from this r5-derived workspace and compare the final diff against its copied baseline.

## Rollback

Restore the paired r5 DLL and CustomCssJS configuration together. No persistent-data migration is introduced by r6.

