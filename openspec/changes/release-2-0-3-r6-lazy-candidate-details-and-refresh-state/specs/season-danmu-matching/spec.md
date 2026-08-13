## ADDED Requirements

### Requirement: Manual rematch candidate details are resolved only on demand

Whole-Series per-Season and direct single-Season manual rematch/search candidate lists SHALL render without resolving candidate Episode details. Each manual candidate SHALL expose `解析并查看详情`; clicking it SHALL resolve and expand only that candidate and SHALL NOT select, confirm, map, bind, download, or persist it.

#### Scenario: Manual Season candidates initially render
- **WHEN** a manual Season rematch/search returns candidate cards
- **THEN** zero candidate-detail requests SHALL have occurred and every candidate SHALL remain collapsed

#### Scenario: One Season candidate is inspected
- **WHEN** the user clicks `解析并查看详情` for candidate A
- **THEN** only A SHALL be resolved and expanded with source Episode numbers/titles while B and C remain unresolved

### Requirement: r6 preserves the r5 target-season scope

Every r6 preview, manual rematch, detail, confirmation, download, retry, automatic, and metadata-mirror path SHALL retain r5 target-season eligibility. Whole-Series matching SHALL include only known positive Seasons. A normal Season SHALL include only Episodes whose `ParentIndexNumber` equals the target Season number; S00, foreign, and unknown-parent Episodes SHALL never become temporary ranges or trigger provider candidate/detail calls.

#### Scenario: Normal Season displays S00 Episodes
- **WHEN** S1 displays eligible Parent 1 Episodes plus Parent 0 Episodes
- **THEN** only Parent 1 Episodes SHALL affect count, score, mappings, temporary ranges, execution, and completeness, and the Parent 0 Episodes SHALL produce zero candidate/detail/download work

#### Scenario: Whole Series contains Season 0
- **WHEN** whole-Series matching enumerates a Series containing S0 and positive Seasons
- **THEN** S0 SHALL not be searched, rendered, or executed

#### Scenario: Explicit Season 0 is opened
- **WHEN** the real S0 item is matched directly
- **THEN** only that item's own Parent 0 Episodes SHALL participate exactly as in r5

### Requirement: Force refresh remains dialog-scoped until execution

The smart-match workflow SHALL maintain one force-refresh value for the dialog. It SHALL remain editable from every pre-download level, survive navigation, and be captured and locked only when download execution starts. It SHALL continue to affect only the existing seven-day XML freshness policy.

#### Scenario: Force refresh changes in a nested menu
- **WHEN** the user changes `强制刷新` and navigates forward or back
- **THEN** every pre-download screen SHALL show the same value without changing discovery, detail, selection, mapping, or ordering

#### Scenario: Download execution starts
- **WHEN** the user starts a single-target or multi-Season download
- **THEN** the current value SHALL be snapshotted once, locked, and reused for every task in that execution
