## MODIFIED Requirements

### Requirement: Confidence-gated automatic selection
The system SHALL treat every candidate scoring at least `0.90` as confident. If confident candidates span sites, it MUST select the earliest configured site regardless of cross-site score differences and then select that site's unique highest-scoring confident candidate. A site-local highest-score tie SHALL remain ambiguous. Interactive matching and automatic library-import matching MUST use this same backend rule, and no legacy Danmu matching algorithm may participate in the decision.

#### Scenario: Global winner is sufficiently strong and distinct
- **WHEN** exactly one candidate reaches `0.90`
- **THEN** the match result SHALL identify that candidate as automatically selected

#### Scenario: Earlier site has the lower confident score
- **WHEN** an earlier configured site has a `0.90` candidate and a later site has a higher-scoring confident candidate
- **THEN** preview and automatic library-import matching SHALL select the earlier site's candidate

#### Scenario: Top score is tied across providers
- **WHEN** confident candidates from different sites share the global highest score
- **THEN** the system SHALL select the candidate from the earliest configured site, subject to site-local uniqueness

#### Scenario: Selected site remains internally ambiguous
- **WHEN** the earliest confident site has multiple candidates sharing its highest confident score
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary result

#### Scenario: Highest-priority provider remains internally ambiguous
- **WHEN** the earliest configured provider in the confident pool contains more than one candidate tied at that provider's highest score
- **THEN** the system SHALL remain ambiguous and MUST NOT use another provider to break the internal tie

#### Scenario: One site has multiple confident candidates
- **WHEN** the selected site has multiple confident candidates with different scores
- **THEN** the system SHALL select that site's unique highest-scoring candidate

#### Scenario: Search has not covered every enabled site
- **WHEN** fallback search rounds or enabled-site searches remain incomplete
- **THEN** the system MUST continue gathering candidates and MUST NOT finalize a cross-site confident selection early

#### Scenario: Global result is ambiguous
- **WHEN** no candidate reaches `0.90` under the explicit r6 low-confidence rule or the selected site's highest score is internally tied
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary provider result

## ADDED Requirements

### Requirement: Supported video matching is provider-identifier-first
For Series, Season, and Episode targets, the system SHALL resolve provider identifiers for enabled sites before plugin bindings or scored search. Site configuration order MUST take precedence over media hierarchy; within one site, the current item SHALL be checked before its Season and Series ancestors. The first resolvable identifier SHALL become the selected match with origin `provider-id`.

#### Scenario: Episode and parent identifiers span sites
- **WHEN** an Episode has an identifier for a later configured site and its parent Season or Series has a resolvable identifier for an earlier configured site
- **THEN** the earlier site's identifier SHALL be selected

#### Scenario: Multiple identifiers exist within one site
- **WHEN** the current item and one or more ancestors have identifiers for the same enabled site
- **THEN** the current item's resolvable identifier SHALL be preferred, followed by Season and then Series

#### Scenario: Identifier belongs to a disabled site
- **WHEN** an item or ancestor has an identifier for a disabled site
- **THEN** that identifier MUST NOT participate in matching

#### Scenario: Applicable identifiers are stale
- **WHEN** every applicable enabled-site identifier fails to resolve
- **THEN** the result SHALL record `provider-id-unresolved` diagnostics and continue through a compatible saved binding and then the unified scored search

#### Scenario: User requests rematch
- **WHEN** the user invokes `rematch` with an optional keyword
- **THEN** the backend SHALL bypass provider identifiers and saved bindings, search and score all enabled sites, and preserve old metadata unless a new download succeeds

### Requirement: Every matching entry point uses the same backend policy
Interactive Series, Season, and Episode matching and automatic library-import matching SHALL use the same backend identifier resolution, binding precedence, normalization, scoring, threshold, site-priority, and ambiguity rules. The frontend MUST NOT calculate scores or make an independent automatic-selection decision.

#### Scenario: Interactive and import matching receive the same evidence
- **WHEN** an interactive request and a library-import request evaluate the same target, enabled-site order, metadata, and provider responses
- **THEN** they SHALL produce the same selected candidate, match origin, and decision reason

#### Scenario: Legacy provider matcher is available
- **WHEN** the unified r6 backend policy cannot select a candidate
- **THEN** the system MUST return the r6 ambiguous or no-match state and MUST NOT fall back to an old provider-specific Danmu matcher

### Requirement: Successful downloads persist provider identifiers at the correct level
After a valid danmu file is actually persisted, the system SHALL overwrite only the selected site's identifier on the corresponding Movie, Series, Season, or Episode represented by the provider result. It MUST preserve identifiers belonging to other sites and MUST NOT invent or copy an identifier into a media level the provider did not return.

#### Scenario: Scored Season or Episode download succeeds
- **WHEN** a binding, scored, or manually selected candidate successfully persists danmu for a target with a provider identifier at that target's level
- **THEN** that site's identifier SHALL overwrite the old value on the corresponding target

#### Scenario: Match originated from the same existing identifier
- **WHEN** a successful download has match origin `provider-id` for the same site and identifier
- **THEN** the redundant identifier update MAY be skipped

#### Scenario: Batch download partially succeeds
- **WHEN** a Series or Season task persists danmu for only some targets
- **THEN** identifiers SHALL be updated only for the successfully persisted targets whose provider results expose identifiers at those levels

#### Scenario: Download is not successful
- **WHEN** a target is failed, cancelled, skipped, timed out, or does not persist a valid danmu file
- **THEN** its provider identifiers MUST remain unchanged

#### Scenario: Metadata update fails after file persistence
- **WHEN** a danmu file is persisted successfully but its provider identifier cannot be updated
- **THEN** the file result SHALL remain successful and the structured task result SHALL expose the metadata-update error
