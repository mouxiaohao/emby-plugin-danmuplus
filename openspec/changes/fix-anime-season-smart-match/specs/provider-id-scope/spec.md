## Purpose

Defines which Emby item owns each danmu-provider identifier so a Series-level value cannot silently select one season for every child Season or Episode.

## ADDED Requirements

### Requirement: Item-local provider identifier ownership
The system SHALL treat Movie, Season, and Episode provider identifiers as identifiers owned by that exact item type. It MUST NOT inspect, modify, migrate, or delete ordinary or manual-binding danmu-provider identifiers stored on a Series while matching or downloading a Season or Episode.

#### Scenario: Series identifier points to one provider season
- **WHEN** a Series contains a provider identifier that resolves to its fourth season and a child Season has no identifier for that provider
- **THEN** matching that child Season SHALL NOT select the Series identifier
- **AND** the Season SHALL continue to saved manual binding or shared scored search

#### Scenario: Emby materializes a Series identifier onto a Season object
- **WHEN** the Season database row does not own a plugin identifier but the runtime Season object exposes the same key/value as its parent Series
- **THEN** the inherited ordinary and manual values SHALL be removed from the matching and persistence view
- **AND** a distinct value genuinely owned by the Season SHALL remain eligible

#### Scenario: Series contains a manual binding
- **WHEN** historical metadata contains either an ordinary or manual-binding provider identifier on a Series
- **THEN** both Series keys SHALL be retained but ignored by Season and Episode matching

#### Scenario: Series preview contains multiple seasons
- **WHEN** the user opens smart matching for a Series whose child Seasons have different provider identifiers
- **THEN** each Season SHALL be resolved independently from its own identifier
- **AND** the Series identifier SHALL not participate

### Requirement: Episode-to-Season fallback boundary
An Episode exact match SHALL first attempt enabled provider identifiers stored on that Episode and MAY then use an identifier stored on its containing Season to resolve and map the Episode. It MUST NOT fall back to a Series provider identifier.

#### Scenario: Episode has a valid identifier
- **WHEN** an Episode and its containing Season both contain resolvable identifiers
- **THEN** the Episode identifier SHALL be attempted before the Season identifier

#### Scenario: Episode has no identifier but Season does
- **WHEN** an Episode has no usable identifier and its containing Season has a resolvable provider media identifier
- **THEN** the system MAY use that Season object and the local episode number to resolve the Episode

#### Scenario: Only Series has an identifier
- **WHEN** neither the Episode nor its Season has a usable identifier but the Series does
- **THEN** provider-ID resolution SHALL remain unresolved and continue to the normal binding or search workflow

### Requirement: Automatic processing uses identical scopes
Manual preview, manual download, retry, and automatic library processing SHALL use the same provider identifier ownership and fallback boundaries.

#### Scenario: New Season is imported under a Series identifier
- **WHEN** automatic library processing handles a new Season whose parent Series has a provider identifier but the Season does not
- **THEN** it SHALL not download from or persist a match derived from the Series identifier

#### Scenario: Successful Season write follows an inherited runtime projection
- **WHEN** a successful download writes a Season provider identifier through an Emby object that also exposes parent Series plugin keys
- **THEN** the write SHALL use an independent item-local provider dictionary
- **AND** it SHALL not copy the inherited Series values into the Season row

### Requirement: Bilibili PGC identifiers have item-specific meanings
The Bilibili adapter SHALL treat only PGC identifiers as durable Emby external identifiers. A Season `BilibiliID` SHALL be a `season_id`; a Movie `BilibiliID` and an Episode `BilibiliID` SHALL each be the exact playable `ep_id`. A Series `BilibiliID` MUST NOT participate in matching, download selection, fallback, retry, or automatic processing.

#### Scenario: Season has a Bilibili identifier
- **WHEN** a Season contains a numeric `BilibiliID`
- **THEN** exact matching SHALL validate it through the PGC season endpoint as a `season_id`
- **AND** the returned playable episodes SHALL retain their individual `ep_id` values

#### Scenario: Episode has a Bilibili identifier
- **WHEN** an Episode contains a numeric `BilibiliID`
- **THEN** exact matching SHALL validate it through the PGC episode endpoint as an `ep_id`
- **AND** it SHALL take priority over resolving the Episode from its containing Season `season_id`

#### Scenario: Movie has a Bilibili identifier
- **WHEN** a Movie contains a numeric `BilibiliID`
- **THEN** exact matching SHALL validate it directly as the playable PGC movie `ep_id`
- **AND** it MUST NOT reinterpret or persist that Movie identifier as a `season_id`

#### Scenario: Backend requests Bilibili danmu
- **WHEN** a validated Movie or Episode `ep_id` is downloaded
- **THEN** the backend SHALL resolve the corresponding `aid,cid` and use it only for the danmu request
- **AND** neither `aid`, `cid`, nor the `aid,cid` tuple SHALL replace the durable Movie or Episode `BilibiliID`

### Requirement: Bilibili and Mgtv identifiers are visible without changing Series scope
The plugin SHALL register Bilibili and Mgtv external-identifier fields for Movie, Series, Season, and Episode metadata views. A value shown on a Series is historical/display-edit metadata only and MUST remain excluded from provider matching and automatic writes.

#### Scenario: User opens an Emby metadata editor
- **WHEN** the edited item is a Movie, Series, Season, or Episode
- **THEN** the supported Bilibili and Mgtv external-identifier fields SHALL be visible
- **AND** exposing a Series field SHALL NOT authorize the matcher to read or update that Series identifier
