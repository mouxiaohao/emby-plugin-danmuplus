## Context

See `proposal.md` for motivation. The Dandanplay signer currently reads two empty compiled constants after checking process environment variables. The persisted `DandanOption` model and embedded settings page expose only related-comment and Chinese-conversion options. Synology package restarts do not currently inject the required environment variables.

## Goals / Non-Goals

**Goals:**

- Persist a complete credential pair through the existing Emby plugin configuration lifecycle.
- Make precedence pair-based and deterministic so credentials from different sources cannot be mixed.
- Keep credential values out of logs and present the secret with a password input.
- Preserve old XML configuration compatibility and all existing download behavior.

**Non-Goals:**

- Provision or distribute Dandanplay credentials.
- Encrypt the plugin configuration file at rest; access remains governed by Synology and Emby filesystem permissions.
- Add a new public API endpoint or expose credential values to CustomCssJS.

## Decisions

### Store credentials in the existing Dandan option

Add nullable-safe string properties with empty defaults to `DandanOption`. Emby's existing plugin configuration serializer handles migration and persistence, avoiding a second secrets file and new lifecycle code.

The secret input uses `type="password"` and `autocomplete="new-password"`. This prevents casual shoulder-surfing but is not claimed as encryption: authenticated plugin configuration APIs and the on-disk XML necessarily contain the saved value.

### Resolve complete pairs rather than individual fields

If either configured field is non-empty, the configuration source is selected and both fields are required. Otherwise the same rule is applied to environment variables, followed by legacy constants. Values are trimmed once at the boundary.

Per-field fallback was rejected because it could sign with an ID and secret belonging to different applications. Silently falling back after a partially entered higher-priority source was rejected because it would conceal an administrator error.

### Centralize validation before headers are added

Credential selection and validation return a pair used for both the `X-AppId` header and signature input. Exceptions describe whether credentials are missing or incomplete, never their values. Existing provider failure isolation continues to keep global matching operational.

## Risks / Trade-offs

- [Configuration XML stores the secret in plaintext] → Retain existing Emby/Synology permissions, mask it in the UI, and document the at-rest limitation.
- [Browser password managers may offer to save the secret] → Use a non-login field name and disable ordinary autocomplete behavior where supported.
- [Whitespace is accidentally pasted] → Trim leading and trailing whitespace before validation and use.
- [Invalid but non-empty credentials pass local validation] → Dandanplay returns an authorization failure; actual success requires a real credential pair supplied by the administrator.

## Migration Plan

1. Build and test empty, partial, configured, and environment credential resolution.
2. Deploy beside a timestamped backup and restart Emby.
3. Confirm the settings page loads both fields and existing Dandan options remain intact.
4. Leave credentials empty until the administrator supplies a valid pair; verify a real search afterward.
5. Roll back by restoring the timestamped DLL backup; old configuration readers ignore the additional XML elements.
