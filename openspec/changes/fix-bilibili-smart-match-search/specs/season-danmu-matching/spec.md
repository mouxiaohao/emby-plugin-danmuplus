## ADDED Requirements

### Requirement: Enabled-provider candidate participation
The system SHALL include usable search results from every enabled danmu provider in the shared candidate set used by manual match preview and automatic library-import matching. A usable result MUST contain a provider media identifier accepted by that provider's media-detail path, a non-empty display title, and the available year, category, and episode-count metadata.

#### Scenario: Bilibili anime result is available
- **WHEN** Bilibili is enabled and returns one or more valid anime seasons for the parent series title
- **THEN** the manual match preview SHALL include those Bilibili seasons in the globally scored candidate list

#### Scenario: Bilibili live-action result is available
- **WHEN** Bilibili is enabled and returns one or more valid live-action television seasons for the parent series title
- **THEN** the manual match preview SHALL include those Bilibili seasons with their titles, years, categories, and episode counts

#### Scenario: Automatic matching searches Bilibili
- **WHEN** a newly added non-special season is processed by automatic library-import matching and Bilibili is enabled
- **THEN** valid Bilibili results SHALL participate in the same global scoring and confidence rules as results from other enabled providers

#### Scenario: Bilibili result has no usable identifier
- **WHEN** Bilibili returns a search entry without a positive season or media identifier accepted by its media-detail path
- **THEN** the system SHALL omit that entry without preventing results from Bilibili or other providers from being ranked
