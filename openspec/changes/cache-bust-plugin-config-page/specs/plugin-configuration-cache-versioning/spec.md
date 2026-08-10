## Purpose

Ensures that an updated Danmu plugin configuration interface cannot be replaced by stale browser-cached resources from an earlier installed build.

## ADDED Requirements

### Requirement: Configuration resources have an installed-build-specific identity
The system SHALL expose the Danmu configuration page and its associated controller resource using identifiers that change when the installed plugin build changes.

#### Scenario: Browser opens the page after a plugin update
- **WHEN** an administrator opens the Danmu configuration page after the server has loaded a newer plugin build
- **THEN** the browser requests resources identified with that newer build rather than reusing resources identified with the prior build

### Requirement: Versioning preserves configuration-page behavior
The system SHALL preserve the existing Danmu dashboard menu entry, form controls, configuration load/save behavior, and plugin API identifier while versioning its browser-facing page/controller identities. The resulting `configurationpage?name=` value MAY change with the installed build and is the intended cache key.

#### Scenario: Administrator saves a configuration after an update
- **WHEN** an administrator loads the versioned configuration page and saves settings
- **THEN** the settings are sent to and persisted through the same plugin configuration API used before versioning

### Requirement: A single installed build uses matched UI resources
The system SHALL ensure that the configuration page requests the controller resource bearing the same installed-build identity as the page it renders. The released plugin SHALL contain the resolved controller reference for that build and MUST NOT expose an unresolved build-time placeholder.

#### Scenario: Current page loads its controller
- **WHEN** the dashboard loads a Danmu configuration page resource for an installed build
- **THEN** it loads the controller resource corresponding to that same build

#### Scenario: Release build embeds the resolved controller identity
- **WHEN** the combined plugin Release artifact is built
- **THEN** its embedded configuration page references the controller identity generated from that same build token
- **AND** the embedded page contains no unresolved cache-token placeholder
