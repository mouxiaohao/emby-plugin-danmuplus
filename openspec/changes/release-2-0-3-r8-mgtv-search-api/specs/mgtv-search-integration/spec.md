## Purpose

Defines reliable Mango TV candidate discovery against the provider's current public PC search contract while preserving shared smart-match scoring, privacy, and provider-local failure behavior.

## ADDED Requirements

### Requirement: Current Mango TV discovery contract
The Mango TV provider SHALL discover title candidates through a currently accepted Mango TV search contract and MUST NOT use the forbidden legacy `/msite/search/v2` request. The request SHALL include only the bounded public keyword and source parameters required by that contract and MUST NOT generate or transmit cookies, authorization, persistent device identifiers, or request signatures for discovery.

#### Scenario: Current endpoint accepts a normal title
- **WHEN** the provider searches a normal Mango TV title from the deployed Synology environment
- **THEN** the search contract SHALL return a successful response instead of the legacy endpoint's HTTP 403 result

#### Scenario: Unrelated title has no Mango TV result
- **WHEN** a valid search succeeds but no usable Mango TV media suggestion matches the keyword
- **THEN** the provider SHALL return an empty candidate list without recording `MgtvID` as a failed search

### Requirement: Canonical and safe candidate mapping
The provider SHALL map only media suggestions with a positive canonical collection identifier and a non-empty plain display title into the existing candidate contract. It MUST omit person, artist, navigation, synthetic zero-identifier, malformed, duplicate, and markup-only suggestions, and it MUST NOT expose highlight markup or request identifiers as candidate text.

#### Scenario: Search response mixes usable and unusable suggestions
- **WHEN** a response includes canonical media suggestions together with zero identifiers, duplicate identifiers, non-media types, or highlighted markup
- **THEN** only the distinct canonical media identifiers with bounded plain titles SHALL become candidates in provider order

#### Scenario: Optional metadata is absent
- **WHEN** a usable suggestion omits year, category, or episode-count metadata
- **THEN** the candidate SHALL retain the canonical identifier and title while unavailable metadata remains unknown rather than being invented or resolved through eager media-detail requests

### Requirement: Shared manual and automatic participation
Mango TV candidates SHALL participate through the existing provider abstraction in whole-Series, direct-Season, temporary-range, Episode/Movie manual re-search, and automatic library-import discovery. The migration MUST NOT change shared global score calculation, confidence thresholds, provider tie ordering, Season eligibility, or authoritative mapping semantics.

#### Scenario: Manual smart matching finds Mango TV candidates
- **WHEN** a manual smart-match keyword receives usable Mango TV suggestions
- **THEN** those candidates SHALL enter the same globally ranked candidate set as candidates from other enabled providers

#### Scenario: Automatic matching finds Mango TV candidates
- **WHEN** automatic library-import matching searches the same descriptive metadata
- **THEN** Mango TV SHALL use the same discovery and candidate normalization operation as manual matching

### Requirement: Provider-local and privacy-safe failures
Transport errors, forbidden responses, rate limits, malformed JSON, unsuccessful provider codes, and structurally unusable Mango TV responses SHALL fail only the Mango TV search operation. Diagnostics SHALL identify Mango TV and a bounded public failure category without logging response bodies, device identifiers, cookies, tokens, or other request secrets, while successful providers continue.

#### Scenario: Mango TV transport fails
- **WHEN** the current Mango TV search request times out or returns a non-success status
- **THEN** other enabled providers SHALL still return candidates and Mango TV SHALL appear once in search diagnostics

#### Scenario: Mango TV response is malformed
- **WHEN** the endpoint returns invalid JSON or an incompatible response shape
- **THEN** the provider SHALL return a controlled provider-local failure without producing partially trusted candidates

### Requirement: Bounded discovery traffic
Mango TV discovery SHALL retain bounded cancellation, request serialization, short-lived keyword caching, and request-rate controls. A single keyword search MUST NOT resolve every returned candidate's media details merely to populate optional score metadata.

#### Scenario: Same keyword is searched repeatedly
- **WHEN** the same normalized keyword is requested again within the configured short cache lifetime
- **THEN** the provider SHALL reuse the normalized discovery result without another upstream search request

#### Scenario: Candidate list is rendered
- **WHEN** discovery returns multiple usable Mango TV candidates
- **THEN** initial candidate rendering SHALL require one discovery operation and zero per-candidate media-detail operations
