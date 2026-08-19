## ADDED Requirements

### Requirement: Ignore safety notice reflects authoritative ignored scope
For each rendered Season result, the browser SHALL derive the current ignored-item total exclusively from the server-authored `IgnoredParentZeroEpisodeCount`, `IgnoredOtherSeasonEpisodeCount`, `IgnoredUnknownParentEpisodeCount`, and `IgnoredInvalidEpisodeCount` fields after treating missing, non-finite, negative, and otherwise invalid values as zero. The Season scope summary SHALL include the exact sentence `忽略项不可选择，也不会进入下载。` once only when that normalized total is greater than zero. That sentence SHALL be the fixed suffix of the same single returned summary-string branch that emits `只读忽略 N 集（分类）`; it MUST NOT be produced by a separate presentation helper or second ignored-total gate. When the ignored-breakdown branch is absent, the sentence SHALL also be absent. When the total is zero, the browser MAY still show displayed and eligible Episode counts, but it MUST NOT show that sentence or imply that ignored items exist.

The notice and the existing read-only ignored-count breakdown SHALL be rebuilt from the current authoritative Season response on every whole-Series render, single-Season render, rematch, and rebuild. Browser history, DOM state, candidate metadata, or client-authored selections MUST NOT create or retain the notice. Showing or hiding the notice MUST NOT change the existing safety behavior: ignored Episodes remain outside selection controls, composite requests, authoritative mappings, and downloads.

#### Scenario: Scope has no ignored items
- **WHEN** displayed and eligible counts are present and all four normalized ignored counts are zero
- **THEN** the browser SHALL render the normal scope summary without `忽略项不可选择，也不会进入下载。`

#### Scenario: Scope has one or more ignored items
- **WHEN** at least one authoritative ignored count is positive
- **THEN** the browser SHALL return and render one summary string whose `只读忽略 N 集（分类）` branch has `忽略项不可选择，也不会进入下载。` as its immediately adjacent fixed suffix exactly once for that Season

#### Scenario: Ignored counts are missing or invalid
- **WHEN** ignored-count fields are absent, null, non-numeric, non-finite, or negative and no other ignored count is positive
- **THEN** the browser SHALL normalize them to zero and SHALL not show the ignore safety notice

#### Scenario: Rematch removes the ignored scope
- **WHEN** an earlier response had a positive ignored total and a later authoritative rematch or rebuild response has a zero ignored total
- **THEN** the rerendered Season SHALL not retain the old ignore safety notice

#### Scenario: Whole-Series seasons have different ignored totals
- **WHEN** one Season in a whole-Series result has a positive ignored total and another Season has zero ignored items
- **THEN** the notice SHALL appear only in the first Season's own summary and SHALL not leak into the second Season

#### Scenario: Ignored items remain outside execution
- **WHEN** a Season response reports ignored Episodes and displays the notice
- **THEN** those ignored Episodes SHALL remain unavailable for selection and SHALL not enter a composite request, authoritative mapping, or download
