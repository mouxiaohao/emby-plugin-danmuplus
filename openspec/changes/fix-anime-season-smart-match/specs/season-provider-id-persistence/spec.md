## Purpose

Defines success-gated and concurrency-safe persistence of a selected provider season identifier separately from the per-episode identifiers used to download danmu.

## ADDED Requirements

### Requirement: Season media identifier persists after accepted download
For a Season download using a verified provider media candidate, the system SHALL write that candidate's provider media identifier to the Season after the first accepted `success` or `partial` episode result whose XML file was persisted. Per-Episode success SHALL continue to write the corresponding provider episode identifier to that Episode.

#### Scenario: First episode succeeds
- **WHEN** a verified Dandanplay AnimeId is selected and the first episode XML is successfully persisted
- **THEN** the Season `DandanID` SHALL become that AnimeId
- **AND** the Episode `DandanID` SHALL become its Dandanplay EpisodeId

#### Scenario: First episode fails and a later episode succeeds
- **WHEN** the sorted first main Episode does not persist XML but a later Episode produces the first accepted persisted `success` or `partial` result
- **THEN** that later success MAY perform the one-time Season identifier write
- **AND** only Episodes whose own XML files were successfully persisted SHALL receive provider EpisodeIds

#### Scenario: Bilibili PGC season download succeeds
- **WHEN** a verified Bilibili PGC `season_id` maps the Season and one of its `ep_id` episodes successfully persists XML
- **THEN** the Season `BilibiliID` SHALL become the `season_id`
- **AND** the successful Episode, including the first Episode when it succeeds, SHALL receive its own `ep_id`
- **AND** backend-resolved `aid,cid` transport values MUST NOT be persisted

#### Scenario: Some episodes fail after one succeeds
- **WHEN** at least one accepted episode persists XML and later episodes fail
- **THEN** the verified Season media identifier SHALL remain persisted

#### Scenario: Every episode fails or is skipped
- **WHEN** no episode produces an accepted persisted `success` or `partial` result
- **THEN** the Season provider identifier MUST NOT be changed

### Requirement: Season persistence is provider-isolated
Writing a Season media identifier SHALL update only the selected provider's ordinary identifier key. It MUST NOT delete or overwrite another provider's identifier or a saved manual-binding key.

#### Scenario: Season contains identifiers from other providers
- **WHEN** a Dandanplay Season download succeeds and the Season also contains a Bilibili identifier
- **THEN** only ordinary `DandanID` SHALL be upserted
- **AND** the Bilibili and manual-binding keys SHALL remain unchanged

### Requirement: Newer selections win concurrent writes
Each Season/provider download SHALL acquire its write generation when the task is created. An older task that completes after a newer selection MUST NOT overwrite the newer Season identifier.

#### Scenario: Older task finishes late
- **WHEN** task A selects provider media A, task B later selects provider media B for the same Season/provider, and task A completes last
- **THEN** task A's Season write SHALL be rejected
- **AND** the Season SHALL retain provider media B after task B succeeds

#### Scenario: Newer task fails after starting
- **WHEN** task B supersedes task A but every task B episode fails
- **THEN** task A MUST NOT later write its older Season identifier
- **AND** the Season's pre-task ordinary identifier SHALL remain unchanged

### Requirement: Validated manual selection is durable
When a user starts a Season download from a manually selected candidate, the system SHALL persist the validated manual binding through the same Season-scoped task path and SHALL use the verified provider media identifier rather than a provider episode identifier.

#### Scenario: Manual candidate detail resolves
- **WHEN** a manually selected provider candidate resolves to usable Season media
- **THEN** its provider media identifier SHALL be retained as the Season's manual binding for future previews
- **AND** the ordinary provider identifier SHALL remain unchanged until an accepted persisted episode result exists

#### Scenario: Manual candidate is invalid
- **WHEN** the selected identifier cannot be resolved to usable Season media
- **THEN** neither manual nor ordinary Season identifiers SHALL be changed

### Requirement: Retry obeys the original task generation
A retry SHALL use the generation of the tracked Season task that created it. It MAY perform the task's first successful Season write only while that generation is still current.

#### Scenario: Retry is first accepted success
- **WHEN** the original run had no accepted episode result and a retry later persists a valid XML file while its task remains current
- **THEN** the retry MAY persist the verified Season media identifier

#### Scenario: Retry belongs to a superseded task
- **WHEN** a newer Season/provider task has started before an older task's retry succeeds
- **THEN** the older retry MUST NOT modify the Season identifier

### Requirement: Metadata failure does not reverse file success
Season or Episode metadata persistence failure SHALL be reported diagnostically but MUST NOT change an already accepted persisted XML result into a download failure.

#### Scenario: Repository update throws after XML save
- **WHEN** a valid XML file is persisted but the Season identifier repository update fails
- **THEN** the episode result SHALL remain successful or partial
- **AND** the metadata failure SHALL be exposed separately
