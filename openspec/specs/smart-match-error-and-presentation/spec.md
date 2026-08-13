# Smart Match Error and Presentation Specification

## Purpose

Defines human-readable Smart Match failures and a compact, responsive virtual-season presentation that exposes useful mapping information without leaking internal identity tokens.

## Requirements

### Requirement: Structured browser response decoding
The browser workflow SHALL normalize successful JSON and JSON, text, empty HTTP, network, timeout, and cancellation failures before rendering. It MUST NOT concatenate or display a raw Fetch `Response`, promise, or transport object.

#### Scenario: Successful request returns a Fetch Response
- **WHEN** the Emby transport resolves to an HTTP 200 `Response` containing JSON
- **THEN** the workflow SHALL decode the body and render the returned Season results

#### Scenario: Server returns JSON error
- **WHEN** an HTTP failure body contains a structured code, message, and retryable flag
- **THEN** the dialog SHALL show the server-authored message and stable code without displaying `[object Response]`

#### Scenario: Server returns text or an empty error
- **WHEN** an HTTP failure contains bounded plain text or no body
- **THEN** the dialog SHALL show the bounded text or an `HTTP status statusText` fallback

#### Scenario: Network, timeout, or cancellation occurs
- **WHEN** transport fails without an HTTP response, exceeds its operation budget, or is explicitly cancelled
- **THEN** network and timeout failures SHALL be classified distinctly, cancellation SHALL remain non-failure UI state, and completed sibling Season results SHALL remain visible

### Requirement: Public virtual-season summary hides internal identities
A virtual-season summary SHALL show a localized provider name, mapping kind, and r3 match confidence/provenance, but SHALL NOT display provider tokens, MediaId/lookup values, ItemId, source EpisodeId, evidence tokens, or internal origin strings.

#### Scenario: Direct Youku Episode mappings are summarized
- **WHEN** a virtual group was rebuilt from exact Youku Episode identifiers
- **THEN** the summary SHALL contain a localized exact Episode-mapping label and provider name with its match score, and SHALL not contain `YoukuID`, `direct-episode-provider:YoukuID`, a media hash, or a source Episode identifier

#### Scenario: Manual Season selection is summarized
- **WHEN** a user confirms a provider candidate with an internal media identifier
- **THEN** the summary SHALL show the localized provider and mapping range without exposing that identifier

### Requirement: Compact full-width mapping details
Expanded mapping details SHALL span the full virtual-season card width and each row SHALL contain only the real local season/episode label and verified source episode position or label.

#### Scenario: Mapping details are expanded
- **WHEN** the user expands a virtual group containing a placed special
- **THEN** a row SHALL read in the form `local S00E01 -> source episode 1` and SHALL omit ItemId, source EpisodeId, provider/internal source identity, score, and provenance

#### Scenario: Narrow viewport renders details
- **WHEN** the dialog is rendered at 520 CSS pixels or less
- **THEN** the detail region SHALL occupy the card width without collapsing into a narrow vertical character column or obscuring rematch/remove actions

### Requirement: Hidden identities remain in the trusted protocol
Presentation filtering MUST NOT remove the ItemId, selection evidence, stable source identity, source EpisodeId, or other fields required by authoritative rebuild, remove, restore, retry, and download validation.

#### Scenario: User removes and restores a sanitized group
- **WHEN** the visible card contains no internal identifiers and the user removes then restores it
- **THEN** the requests SHALL retain the exact trusted identities and the server SHALL reconstruct the same ItemIds and mappings
