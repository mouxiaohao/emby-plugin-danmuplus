## Why

r6 restored the safe r5 target-season scope and added lazy candidate inspection, but its temporary-season workflow still exposes pre-download controls while no action is possible, omits Episode titles from confirmed mapping rows, and renders mapped-group rematch differently from unmatched-group manual matching. r7 should make these surfaces consistent without changing discovery, authoritative planning, target scope, confirmation, or download behavior.

## What Changes

- Hide the force-refresh checkbox during every blocking server-request/busy state; restore it only after the dialog reaches an actionable pre-download page.
- Extend confirmed temporary-season mapping rows to show both the local library Episode title and the verified source Episode title, while continuing to hide internal identifiers.
- Route both `重新匹配` on an existing temporary-season mapping and `手动匹配` on an unmatched temporary Season into one menu titled `手动匹配未匹配临时季`.
- Render each candidate's match score exactly once in that shared menu.
- Give every temporary-season candidate an r6-style `解析并查看详情` action that performs no source-detail request until clicked and expands only that candidate.
- Preserve r5/r6 target-season scope, S0 rules, temporary-run construction, mapping confirmation, selection evidence, navigation, force-refresh download snapshot, and tracked-download semantics.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-danmu-matching`: unify temporary-season manual/rematch candidate inspection and preserve verified source titles in authoritative mapping output.
- `smart-match-error-and-presentation`: hide force refresh in non-actionable busy states, display public local/source titles in mapping details, and remove duplicate score presentation.

## Impact

- Frontend smart-match dialog rendering and regression tests.
- Composite Season planning DTOs and mapping projection so verified source titles survive planning without an extra provider request.
- Existing `MatchCandidateDetails` and short-lived `SelectionEvidenceToken` contract is reused; no new candidate-detail endpoint is required.
- Release version/cache marker and paired r7 package/deployment evidence.
- No changes to Emby library Season membership, identifier policy, candidate scoring, provider priority, target Episode eligibility, automatic selection, persistence, or download execution.
