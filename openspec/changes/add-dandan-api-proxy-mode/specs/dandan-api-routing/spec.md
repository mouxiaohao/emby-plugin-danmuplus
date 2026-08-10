## Purpose

Defines how administrators select direct or Cloudflare-compatible proxy routing for Dandanplay while preserving DanmuPlus's existing server-side endpoints and matching behavior.

## ADDED Requirements

### Requirement: Mutually exclusive Dandanplay API modes
The Dandanplay configuration page SHALL offer mutually exclusive "proxy API" and "custom API" modes, persist the selected mode and proxy CORS prefix, and preserve values belonging to the inactive mode when the administrator switches modes.

#### Scenario: Existing configuration is upgraded
- **WHEN** an existing configuration without an API-mode value is loaded
- **THEN** custom API mode SHALL be selected and all existing Dandanplay behavior SHALL remain unchanged

#### Scenario: Administrator selects proxy API
- **WHEN** the administrator selects proxy API, enters a proxy CORS prefix, and saves
- **THEN** the selected mode and prefix SHALL persist across Emby restarts

#### Scenario: Administrator switches modes
- **WHEN** the administrator switches between proxy API and custom API
- **THEN** the page SHALL show the fields relevant to the selected mode without erasing values previously saved for the other mode

### Requirement: Cloudflare-compatible proxy routing
In proxy API mode, the plugin SHALL construct each request by concatenating the normalized configured CORS prefix with the absolute official Dandanplay URL, and SHALL delegate Dandanplay application signing to the proxy.

#### Scenario: Search request through proxy
- **WHEN** proxy API mode uses the prefix `https://worker.example/cors/` and DanmuPlus searches for a title
- **THEN** the request URL SHALL begin `https://worker.example/cors/https://api.dandanplay.net/api/v2/search/anime` and preserve the search query parameters

#### Scenario: Bangumi request through proxy
- **WHEN** proxy API mode retrieves a selected Dandanplay work
- **THEN** the proxy request SHALL target the existing official `/api/v2/bangumi/{animeId}` endpoint

#### Scenario: Comment request through proxy
- **WHEN** proxy API mode downloads comments for an episode
- **THEN** the proxy request SHALL target the existing official `/api/v2/comment/{episodeId}` endpoint and preserve `withRelated` and `chConvert`

#### Scenario: Proxy prefix is absent or invalid
- **WHEN** proxy API mode is selected with an empty, non-absolute, or non-HTTP(S) CORS prefix
- **THEN** the Dandanplay operation SHALL fail with a deterministic configuration error and SHALL NOT silently fall back to custom API mode

### Requirement: Existing custom API routing remains compatible
In custom API mode, the plugin SHALL continue calling the official Dandanplay API directly with the existing signed-request behavior.

#### Scenario: Existing installation remains in custom mode
- **WHEN** an upgraded installation performs a Dandanplay search, work lookup, or comment download without selecting proxy API
- **THEN** it SHALL use the same official endpoints, credential precedence, and signed request headers as before this change

### Requirement: Matching and download behavior remains server-side
Both API modes MUST continue using DanmuPlus's existing title, year, season, episode-count, and provider-priority matching flow and its existing XML/ASS download pipeline.

#### Scenario: Automatic library matching uses proxy API
- **WHEN** automatic library matching searches Dandanplay in proxy API mode
- **THEN** candidates SHALL be scored through the same provider-neutral DanmuPlus matching flow used by custom API mode

#### Scenario: Manual matching uses proxy API
- **WHEN** an administrator manually searches and selects a Dandanplay candidate in proxy API mode
- **THEN** the saved binding and subsequent comment download SHALL follow the existing DanmuPlus manual-binding and download behavior

#### Scenario: No hash recognition is introduced
- **WHEN** either API mode performs matching
- **THEN** the plugin MUST NOT invoke Dandanplay `/match`, compute a video-file hash for Dandanplay, or substitute dd-danmaku's frontend matching flow

### Requirement: Proxy failures remain provider-isolated
A proxy configuration or request failure SHALL fail only the Dandanplay provider operation and MUST NOT discard candidates returned by other enabled providers.

#### Scenario: Proxy fails during global matching
- **WHEN** the configured proxy is unavailable while another enabled provider returns candidates
- **THEN** Dandanplay SHALL appear as failed in diagnostics and the other provider's candidates SHALL remain available
