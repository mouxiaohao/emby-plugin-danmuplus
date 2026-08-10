# Dandanplay API Credentials Specification

## Purpose

Allows an Emby administrator to persist the credentials required for signed Dandanplay API requests while preserving secure display, deterministic precedence, and clear failure behavior.

## Requirements

### Requirement: Administrator credential configuration
The plugin settings page SHALL allow an authenticated Emby administrator to enter and save a Dandanplay API ID and API Secret alongside the existing Dandanplay options.

#### Scenario: Administrator saves a complete credential pair
- **WHEN** the administrator enters both values and saves the plugin configuration
- **THEN** both values SHALL persist across Emby restarts and SHALL be used by subsequent Dandanplay requests

#### Scenario: Existing configuration is upgraded
- **WHEN** a configuration file created before credential fields existed is loaded
- **THEN** both new values SHALL default to empty without changing other provider or Dandanplay settings

### Requirement: Secret-safe presentation and diagnostics
The settings page SHALL mask the API Secret during entry, and the plugin MUST NOT write either credential value into application logs, request diagnostics, or user-facing errors.

#### Scenario: Administrator opens saved settings
- **WHEN** a saved API Secret is loaded into the settings page
- **THEN** the input SHALL visually mask its value

#### Scenario: Signed request fails
- **WHEN** a Dandanplay request cannot be signed or is rejected
- **THEN** diagnostics SHALL identify the configuration or request failure without including the API ID, API Secret, or signature material

### Requirement: Deterministic credential-source precedence
The system SHALL select the first complete credential pair in this order: plugin configuration, process environment variables, then legacy compiled defaults. It MUST NOT combine values from different sources.

#### Scenario: Plugin configuration and environment are both complete
- **WHEN** both sources contain a complete credential pair
- **THEN** the plugin configuration pair SHALL be used

#### Scenario: Plugin configuration is empty and environment is complete
- **WHEN** neither plugin configuration value is set and both supported environment variables are set
- **THEN** the environment credential pair SHALL be used

#### Scenario: A selected source is incomplete
- **WHEN** one value is present and the other is absent in the highest-precedence non-empty source
- **THEN** the request SHALL fail with a clear incomplete-credentials error instead of mixing in a value from a lower-precedence source

### Requirement: Missing credential failure isolation
The system SHALL fail only Dandanplay operations when no complete credential pair is available, while cross-provider smart matching continues with other enabled providers.

#### Scenario: Dandanplay has no credentials during global matching
- **WHEN** a global match searches Dandanplay without configured credentials
- **THEN** Dandanplay SHALL appear in search diagnostics as failed and candidates from successful providers SHALL still be returned
