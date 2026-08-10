## Why

DanmuPlus currently requires every Emby server to store Dandanplay credentials and call the official API directly. Administrators who already operate or trust a compatible Cloudflare Worker need a selectable proxy path that delegates signing to that Worker without changing DanmuPlus's existing title-, year-, season-, and episode-based matching behavior.

## What Changes

- Add mutually exclusive "proxy API" and "custom API" choices to the Dandanplay configuration section; existing installations default to custom API so their behavior does not change.
- Add a persisted proxy CORS-prefix field, compatible with the `https://worker.example/cors/` URL-concatenation contract used by the supplied `cf_worker.js`.
- Route the existing Dandanplay `/search/anime`, `/bangumi/{animeId}`, and `/comment/{episodeId}` requests through the configured proxy when proxy API mode is selected.
- In proxy mode, rely on the Worker to supply Dandanplay application authentication; do not require, expose, or send the Emby server's local API ID/secret.
- In custom API mode, preserve the current direct official API calls, credential precedence, and local `X-AppId`/`X-Signature`/`X-Timestamp` generation.
- Validate missing or malformed proxy configuration deterministically, preserve provider-level failure isolation, and keep inactive-mode settings when administrators switch modes.
- Add deterministic regressions plus a minimal live smoke check using an administrator-supplied Worker URL that is never embedded as a plugin default.
- Non-goals: deploy or manage a Cloudflare Worker; introduce Dandanplay `/match` or file-hash recognition; replace local title/year/episode scoring; add the dd-danmaku browser overlay; change other danmu providers.

## Capabilities

### New Capabilities

- `dandan-api-routing`: Defines mutually exclusive direct/custom and Cloudflare-compatible proxy routing, URL construction, validation, and unchanged endpoint/matching behavior.

### Modified Capabilities

- `dandan-api-credentials`: Makes local Dandanplay credentials mandatory only for custom/direct mode while proxy mode delegates application authentication to the configured Worker.

## Impact

- Affected configuration: `DandanOption`, embedded `configPage.html`, and `config.js` load/save and conditional-display behavior.
- Affected request path: `Scraper/Dandan/DandanApi.cs`; endpoint selection changes, while `Dandan.cs`, match scoring, saved bindings, XML/ASS output, retries, and provider ordering remain compatible.
- Affected validation: C# regression harness, embedded-page assertions, frontend configuration behavior, Release build, and one low-volume live proxy smoke check.
- Security: direct/custom mode continues storing the secret in Emby's plugin configuration XML or environment; proxy mode keeps Worker credentials outside Emby and must not emit local credential/signature material.
