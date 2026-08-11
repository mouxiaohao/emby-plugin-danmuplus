## ADDED Requirements

### Requirement: Provider-ID Season detail presentation
Before shared Season search and scoring, the system SHALL resolve eligible enabled-provider identifiers in configured provider and item-scope order. A successful exact resolution SHALL return one selected candidate populated from the identified upstream Season or Series detail response and SHALL bypass the scored Season candidate workflow.

#### Scenario: Season identifier exposes upstream details
- **WHEN** an enabled provider identifier on the Season resolves to upstream media with a title and episode list
- **THEN** the Season preview SHALL display that upstream title and the available upstream metadata
- **AND** the displayed episode count SHALL use the provider's positive declared count or otherwise the usable resolved episode-list count

#### Scenario: Parent Series identifier wins by configured order
- **WHEN** the existing provider/scope precedence selects a resolvable parent Series identifier for the Season preview
- **THEN** its upstream metadata SHALL populate the selected Season candidate without changing that precedence

#### Scenario: Exact Season detail lacks optional metadata
- **WHEN** the selected provider detail response omits year or category
- **THEN** the preview SHALL display those fields as unknown rather than copying local Season metadata or starting a search

#### Scenario: Automatic library processing uses an existing identifier
- **WHEN** automatic library processing encounters a resolvable enabled-provider identifier
- **THEN** it SHALL use the same exact identifier resolution and enriched detail object as manual preview
- **AND** metadata enrichment SHALL NOT change the existing download or identifier-persistence decisions

