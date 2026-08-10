## MODIFIED Requirements

### Requirement: Missing credential failure isolation
In custom API mode, the system SHALL fail only Dandanplay operations when no complete credential pair is available, while cross-provider smart matching continues with other enabled providers. In proxy API mode, the system SHALL delegate application authentication to the configured proxy and SHALL NOT require a local Dandanplay credential pair.

#### Scenario: Dandanplay has no credentials during global matching
- **WHEN** a global match searches Dandanplay in custom API mode without configured credentials
- **THEN** Dandanplay SHALL appear in search diagnostics as failed and candidates from successful providers SHALL still be returned

#### Scenario: Proxy API has no local credentials
- **WHEN** a Dandanplay request is made in proxy API mode and the Emby plugin has no API ID or API Secret
- **THEN** the request SHALL proceed through the configured proxy without resolving local credentials

#### Scenario: Proxy API does not expose local authentication
- **WHEN** a Dandanplay request is made in proxy API mode
- **THEN** the plugin MUST NOT add its local `X-AppId`, `X-Signature`, `X-Timestamp`, or API Secret to the proxy request URL, headers, diagnostics, or user-facing errors
