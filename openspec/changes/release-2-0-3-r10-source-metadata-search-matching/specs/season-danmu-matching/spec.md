## ADDED Requirements

### Requirement: Complete Bilibili typed media discovery
The system SHALL supplement Bilibili aggregate search with bounded typed Movie and Bangumi result retrieval, merge results by usable provider media identity, and preserve title, trustworthy work year, category, and episode-count metadata. Work year means the title's premiere/release/first-broadcast year; Bilibili page or part publication/upload timestamps MUST NOT replace it. A Bilibili Movie search candidate SHALL retain the parent movie identity; resolution of a default or manually selected independently downloadable `ep_id` SHALL be a separate Movie-part step and MUST NOT replace the parent candidate identity. Bilibili discovery SHALL expose one shared result contract (or an equivalent shared channel) containing both collected candidates and diagnostics, and both shared Movie and Season search paths SHALL consume it. A failed typed page MUST leave successfully obtained Bilibili and other-provider candidates usable and SHALL be surfaced as a diagnostic rather than turning the whole provider result into a failure.

#### Scenario: Aggregate Bilibili search omits older films
- **WHEN** Bilibili's aggregate response for `谍影重重` contains only films 3 through 5 while its typed Movie search exposes films 1 and 2
- **THEN** the shared Movie candidate set SHALL include usable results for films 1 through 5 without duplicate provider identities

#### Scenario: Typed Bilibili page fails
- **WHEN** one bounded Bilibili typed-result page fails after another Bilibili result page has succeeded
- **THEN** the shared candidates-plus-diagnostics result SHALL retain successful Bilibili results and candidates from other providers, and the typed-page failure SHALL be reported diagnostically to both Movie and Season consumers

#### Scenario: Automatic season matching uses typed Bangumi results
- **WHEN** automatic library-import matching searches an enabled Bilibili provider and a usable Bangumi result is available only through typed discovery
- **THEN** that result SHALL participate in the existing global ranking and confidence rules

#### Scenario: Bilibili Movie candidate retains parent identity
- **WHEN** a typed Bilibili Movie result resolves to several independently downloadable usable `ep_id` values after explicit non-main filtering
- **THEN** discovery SHALL return one parent Movie candidate, and default or manual part selection SHALL resolve a leaf without producing duplicate Movie candidates or replacing the parent identity

#### Scenario: Bilibili part ambiguity does not affect Movie matching
- **WHEN** a parent Bilibili Movie candidate is otherwise the unique automatic match and several remaining usable parts cannot be distinguished by language, title, or type
- **THEN** the parent Movie SHALL remain automatically selected and the first usable part in stable provider order SHALL become its default leaf without adding candidate ambiguity

#### Scenario: Season and Episode matching remain unchanged by Movie parts
- **WHEN** Bilibili Season or Episode matching runs after Movie part selection is enabled
- **THEN** it SHALL use the existing Season/Episode candidate, scoring, exact-identifier, and download flows without a Movie-part selector

### Requirement: Fidelity-preserving title evidence breaks otherwise artificial ties
The system SHALL derive two representations for every local title, local alias, source title, source alias, and applicable parent-media/season title used by matching. Loose normalization SHALL retain the existing punctuation-insensitive representation for recall and base scoring. Fidelity normalization SHALL apply Unicode NFKC compatibility normalization plus the established case and whitespace normalization while preserving arbitrary punctuation and symbols by type, count, and order. When loose forms match, an exact fidelity-form match SHALL be positive evidence in the final unique-highest-candidate decision; a fidelity mismatch MUST NOT subtract evidence or prevent loose matching. The implementation MUST NOT special-case selected punctuation or infer whether a symbol means a sequel. It MUST preserve single-character and symbol-only suffixes for fidelity comparison rather than discarding them through a minimum-length threshold. If no candidate becomes uniquely highest after all evidence, the system MUST require manual selection and MUST NOT select by source order.

#### Scenario: Unicode-compatible width variants are equivalent
- **WHEN** the local title uses `＊` and a source candidate uses `*`, or another punctuation/symbol pair is compatibility-equivalent under NFKC, with otherwise equivalent title content
- **THEN** their fidelity forms SHALL be equal without a symbol-specific rule

#### Scenario: Arbitrary symbol type, count, and order remain distinguishable
- **WHEN** loose normalization makes titles such as `Title!`, `Title!!`, and `Title!?` equal
- **THEN** fidelity normalization SHALL preserve and distinguish each punctuation/symbol sequence by type, count, and order

#### Scenario: Symbol-only suffix survives length filtering
- **WHEN** two loose-equivalent titles differ only by a one-character or symbol-only suffix
- **THEN** that suffix SHALL remain present in the fidelity form and available as positive exact-match evidence

#### Scenario: Fidelity match selects the unique highest candidate
- **WHEN** a Season named `妄想学生会＊` receives same-provider candidates named `妄想学生会` and `妄想学生会*` with the same non-title evidence
- **THEN** NFKC SHALL make the starred candidate's fidelity form match the local title and that candidate SHALL be the unique automatic selection if it otherwise meets the confidence rule

#### Scenario: Fidelity mismatch does not reduce loose title evidence
- **WHEN** a local and source title have equal loose forms but different fidelity forms
- **THEN** the fidelity mismatch SHALL add no positive evidence and SHALL NOT penalize or remove their existing loose title evidence

#### Scenario: Aliases and parent titles use the same two channels
- **WHEN** matching compares a local or source alias, or an applicable parent media/season title, rather than the primary title
- **THEN** it SHALL apply the same loose and fidelity normalization and evidence rules without path-specific symbol handling

#### Scenario: Candidates remain genuinely equal
- **WHEN** two candidates retain equal complete matching evidence after fidelity comparison across all applicable title and alias paths
- **THEN** the system SHALL not auto-select either candidate solely by source order
