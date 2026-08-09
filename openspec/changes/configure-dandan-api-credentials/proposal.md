## Why

The enabled Dandanplay provider currently fails every signed request because both compiled credentials and Synology Emby environment variables are empty, while the plugin settings page offers no way to enter credentials. Administrators need a persistent, visible configuration path that survives package restarts.

## What Changes

- Add Dandanplay API ID and API Secret fields to the plugin configuration model and administrator settings page.
- Display the secret as a password input and never include either credential in plugin logs or error messages.
- Resolve credentials in the order plugin configuration, process environment variables, then legacy compiled constants.
- Trim accidental surrounding whitespace while preserving the credential contents otherwise.
- Keep existing Dandanplay options and all non-Dandan providers unchanged.
- Continue returning a clear missing-credentials error when either value is absent.
- Do not obtain, generate, or bundle third-party credentials automatically.

## Capabilities

### New Capabilities
- `dandan-api-credentials`: Defines administrator-managed Dandanplay signing credentials, precedence, persistence, and safe failure behavior.

### Modified Capabilities

None.

## Impact

Affected areas are the plugin XML configuration model, embedded configuration HTML/JavaScript, and Dandanplay request signing. Existing configuration files remain compatible because new string fields default to empty values; no saved binding or CustomCssJS contract changes are required.
