## ADDED Requirements

### Requirement: Force refresh appears only on actionable pages
The smart-match dialog SHALL render its single `强制刷新` checkbox only after a pre-download page becomes actionable. A blocking server-request or busy page MUST render zero force-refresh checkboxes while preserving the dialog-scoped value unchanged for the next actionable page.

#### Scenario: Initial preview is loading
- **WHEN** the dialog is waiting for the initial server match result and has not rendered actionable results
- **THEN** it SHALL show no force-refresh checkbox

#### Scenario: Search, candidate resolution, or plan validation is loading
- **WHEN** the dialog temporarily enters a blocking request state from an actionable page
- **THEN** it SHALL show no force-refresh checkbox and SHALL restore exactly one checkbox with the previous value when an actionable page returns

#### Scenario: Busy search can be cancelled
- **WHEN** a blocking search supports cancellation
- **THEN** the footer MAY show the cancellation action but MUST NOT show force refresh

### Requirement: Temporary-season candidate score is singular
Each temporary-season candidate SHALL display its server-authored match score and provenance exactly once. Removing duplicate presentation MUST NOT recalculate, reorder, filter, or otherwise alter the server candidate list.

#### Scenario: Candidate supplies score and decision text
- **WHEN** both candidate metadata and server decision text contain score information
- **THEN** the visible candidate row SHALL contain one match-score label while retaining the non-duplicate decision information

## MODIFIED Requirements

### Requirement: Compact full-width mapping details
Expanded mapping details SHALL span the full virtual-season card width. Each row SHALL show the real local season/Episode label and local library Episode title together with the verified source Episode position or label and source Episode title. The row SHALL omit internal identifiers, score, provenance, and evidence; missing titles SHALL fall back cleanly to the existing public number labels.

#### Scenario: Mapping details are expanded
- **WHEN** the user expands a virtual group containing a mapped Episode with both titles
- **THEN** a row SHALL read in the form `本地 S01E01 · 库内标题 → 来源第 1 集 · 服务器标题` and SHALL omit ItemId, source EpisodeId, provider/internal source identity, score, provenance, and evidence

#### Scenario: Mapping title is unavailable
- **WHEN** the local or source title is unavailable
- **THEN** the row SHALL remain readable using the available title and the existing public Episode-number labels without showing an empty separator

#### Scenario: Narrow viewport renders details
- **WHEN** the dialog is rendered at 520 CSS pixels or less
- **THEN** the detail region SHALL occupy the card width without collapsing into a narrow vertical character column or obscuring rematch/remove actions
