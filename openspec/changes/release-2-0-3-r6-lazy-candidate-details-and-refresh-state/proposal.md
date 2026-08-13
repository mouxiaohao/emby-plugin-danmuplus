## Why

r5 already provides the authoritative target-season scope, skips Season 0 during whole-Series matching, excludes foreign/unknown-parent Episodes from a normal Season, builds temporary ranges only from eligible target-season remainders, and exposes an existing post-plan Episode mapping-detail view. r6 must add comparison and refresh-state conveniences without replacing those behaviors.

The rejected first r6 package was built from a polluted experimental workspace. It reintroduced r4 collection/segment behavior, showed candidate inspection on an initial exact Episode match, hid the existing mapping-detail action, and added repeated helper text. This change is restarted from the exact deployed r5 source and package.

## What Changes

- On a single-Episode **manual rematch/search candidate page**, keep the local library Episode summary visible while the user inspects source Episode titles.
- Hide scope, ItemId, provider/media identifiers, and internal origin strings from initial exact Episode presentation. The initial exact page retains its r5 controls and does not show candidate-detail inspection.
- Add a per-candidate `解析并查看详情` action only to manual rematch/search candidate lists for Episode, whole-Series per-Season, and direct Season workflows. No candidate detail is fetched before that explicit click.
- Preserve the r5 `查看集映射详情` action and its post-plan per-Episode mapping display as a separate feature from candidate inspection.
- Move one dialog-scoped checkbox labelled exactly `强制刷新` to the lower-left footer. It remains editable across pre-download navigation and is snapshotted/locked only when download execution starts.
- Remove repeated seven-day explanations and Esc/close hints from the dialog.

## Preserved r5 invariants

- Whole-Series matching includes only known positive Season numbers; Season 0 and unknown-number Seasons are not searched, rendered, or executed.
- Direct Season 0 matching remains supported from the real Season 0 item and includes only its own Parent 0 Episodes.
- Every normal-Season batch path uses only Episodes whose `ParentIndexNumber` equals the target Season number. Foreign, S00, and unknown-parent Episodes cannot become temporary ranges, candidates, mappings, downloads, or completeness inputs.
- Candidate scoring, explicit mapping, temporary-range construction, confirmation, download/retry, automatic processing, metadata mirroring, and existing navigation remain unchanged by r6.

## Non-goals

- Replacing or redesigning r5 Season planning, collections, segments, temporary ranges, mapping confirmation, or mapping-detail presentation.
- Adding new candidate searches for ignored Episodes or creating a temporary Season from foreign display inventory.
- Adding candidate-detail inspection to initial exact Episode matches or Movies.
- Changing provider ranking, saved bindings, identifier precedence, download semantics, retry semantics, or seven-day freshness policy.

## Impact

- A narrow authenticated read-only candidate-detail operation and DTOs.
- Minimal additions to existing r5 candidate renderers and footer state.
- Regression gates for all r5 scope and mapping-detail behavior before packaging.

