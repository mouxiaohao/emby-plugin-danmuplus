## ADDED Requirements

### Requirement: Single-Episode manual comparison context remains visible

On a single-Episode manual rematch/search candidate page, the target library Episode summary SHALL remain visible above the candidates before and after a candidate is inspected. It SHALL show user-facing series, season, Episode number, and title information.

#### Scenario: A manual Episode candidate is inspected
- **WHEN** the user clicks `解析并查看详情` after manual rematch/search
- **THEN** the unchanged local Episode summary SHALL remain above the expanded source Episode titles

### Requirement: Initial exact Episode presentation remains unchanged except identifier redaction

An initial Episode match obtained from its verified stored identifier SHALL retain the r5 exact-match selection and source-Episode-number controls. It MUST NOT show `解析并查看详情`, scope, ItemId, provider/media/source identifiers, evidence tokens, or internal origin strings. Candidate inspection becomes available only after explicit manual rematch/search.

#### Scenario: Initial exact Episode match opens
- **WHEN** the stored Episode identifier resolves successfully before any manual rematch/search
- **THEN** the r5 exact-match controls SHALL be visible, internal identifiers SHALL be absent, and no candidate-detail action/request SHALL exist

#### Scenario: User explicitly rematches the Episode
- **WHEN** the user clicks `重新智能匹配` or completes a manual keyword search
- **THEN** the returned manual candidates SHALL expose independent on-demand detail actions with zero initial detail requests

### Requirement: Candidate details expand beneath only the manual candidate

A successful manual candidate inspection SHALL expand source Episode number/title rows beneath that same candidate without replacing the candidate list, selecting the row, or hiding local comparison context.

#### Scenario: Multiple candidates are inspected
- **WHEN** the user inspects candidates A and B separately
- **THEN** each candidate SHALL retain independent loaded/error/collapse state and no other candidate SHALL be resolved

### Requirement: Confirmed Episode mapping details remain available

The existing r5 `查看集映射详情` action and post-plan per-Episode mapping display SHALL remain available in whole-Series and direct Season overviews. Candidate inspection MUST NOT replace, rename, hide, or reuse that action.

#### Scenario: A Season already has an authoritative plan
- **WHEN** its overview is rendered before or after manual candidate inspection
- **THEN** `查看集映射详情` SHALL remain present and SHALL show the confirmed local-to-source Episode mappings

### Requirement: Force-refresh presentation is compact

Every pre-download smart-match footer SHALL show a lower-left checkbox with visible text exactly `强制刷新`. The dialog SHALL NOT render a repeated seven-day explanation or an Esc/X close hint.

#### Scenario: Any pre-download level renders
- **WHEN** a top-level, nested, loading, retry, or back-navigation screen is shown
- **THEN** the footer SHALL contain the shared `强制刷新` checkbox and SHALL contain no freshness paragraph or close hint

