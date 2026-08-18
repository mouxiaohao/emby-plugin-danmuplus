## Purpose

Defines the established optimized, server-scored discovery contract for an explicit user-entered keyword while reserving selection, download, and persistence for an explicit evidence-validated user choice.

## ADDED Requirements

### Requirement: Explicit keyword uses the established manual-search normalization
When a user explicitly edits and submits a keyword containing at least one non-whitespace character from a supported smart-match entry point, the system SHALL identify the request as `manual-keyword`, apply the existing browser/server outer trim, and retain provider-owned normalization and required transport encoding. The system MUST NOT replace the explicit term with library metadata, append a Season marker or year, substitute a TMDB alias, or fall back to a parent Series title. This contract does not require byte-for-byte preservation of leading or trailing whitespace and does not remove existing provider-specific manual-search optimizations.

#### Scenario: Keyword contains internal spaces, punctuation, and a literal plus
- **WHEN** the user submits a valid edited keyword containing internal spaces, punctuation, non-ASCII text, or a literal `+`
- **THEN** the outer-trimmed semantic keyword SHALL enter each selected provider's existing normalization and transport path without a TMDB alias, Season/year suffix, or parent-title substitution

#### Scenario: Explicit keyword is empty or whitespace-only
- **WHEN** the user explicitly submits an empty keyword or a keyword made only of whitespace
- **THEN** the system SHALL report that a keyword is required and SHALL issue no provider search

### Requirement: Manual-keyword discovery preserves the optimized scored pipeline
Manual-keyword discovery SHALL use the enabled providers' established search implementations and candidate-eligibility rules. The system SHALL retain the existing `MergeSources` semantics, ordinary target-aware Season or Movie scoring and reasons, and the established `OrderCandidates` projection. That projection SHALL keep configured-provider `SourceOrder`, order each provider's canonical candidates by score and the existing tie breakers, allocate rows with the existing provider-fair policy, and expose at most sixty candidates. Movie manual-keyword discovery SHALL retain zero-score candidates for user review; no candidate may be removed merely because its score misses an automatic confidence threshold.

Common response projection, ordinary target-bound selection evidence, internal identity protection, secret redaction, and browser-safe text rendering SHALL remain mandatory protocol and security boundaries.

#### Scenario: Provider returns repeated identities
- **WHEN** providers return repeated candidate identities
- **THEN** existing `MergeSources` merge/de-duplication semantics SHALL apply, including keeping identities from different providers distinct

#### Scenario: Provider returns an ineligible row
- **WHEN** a provider result lacks the identity or media characteristics required by its established manual-search eligibility rules
- **THEN** the existing provider/candidate eligibility path MAY omit that row without changing l10 into an automatic match decision

#### Scenario: More than sixty scored candidates are available
- **WHEN** the canonical scored candidates exceed the established display limit
- **THEN** the provider-fair `OrderCandidates` projection SHALL return at most sixty rows in its existing deterministic order

#### Scenario: Movie candidate has a zero score
- **WHEN** an eligible Movie row receives an ordinary score of zero
- **THEN** it SHALL remain eligible for the manual-keyword sixty-row projection and explicit user review

### Requirement: Scored manual-keyword results require explicit selection
An explicit manual-keyword search SHALL be discovery-only even though its rows retain ordinary scores and reasons. The system MUST NOT invoke TMDB alias expansion, apply automatic confidence classification, call `ClassifyResult`, set `AutoSelected` or a selected candidate, start a download, persist a binding, or write metadata before the user explicitly selects a result. The browser SHALL show server-provided score/reason information without preselecting a row.

#### Scenario: One row satisfies automatic title rules
- **WHEN** a manual-keyword result is an exact or high-confidence title, year, Season, and episode-count match
- **THEN** its score and reason SHALL remain visible but it SHALL stay unselected until the user explicitly chooses it

#### Scenario: User selects a scored result
- **WHEN** the user explicitly selects a row returned by manual-keyword discovery
- **THEN** the server SHALL validate its ordinary target-bound evidence and then use the existing authoritative detail, mapping, download, and persistence workflow

### Requirement: Manual-keyword search isolates provider failures
Failure of one provider during an explicit manual-keyword search SHALL NOT discard or block usable scored candidates already returned by other providers. The failed provider SHALL produce a bounded public diagnostic, while an explicit parent/user cancellation SHALL terminate the whole requested search without converting cancellation into a provider fault.

#### Scenario: One provider fails and another returns candidates
- **WHEN** one enabled provider faults and another enabled provider returns usable candidates
- **THEN** the successful provider's merged, scored, and ordered candidates SHALL remain available for explicit selection and the failed provider SHALL be reported without blocking them

#### Scenario: User cancels manual search
- **WHEN** the user explicitly cancels an in-progress manual-keyword search
- **THEN** the system SHALL stop waiting through the cancellation path and SHALL NOT present the cancellation as a failed website
