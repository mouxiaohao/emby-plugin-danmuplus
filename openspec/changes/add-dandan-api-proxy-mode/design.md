## Context

See `proposal.md` for motivation. The current server-side `DandanApi` builds three absolute official URLs, resolves a complete local credential pair, signs the official path, and adds authentication headers before every request. The embedded configuration page persists Dandanplay values through Emby's existing XML serializer, while CustomCssJS talks only to same-origin DanmuPlus endpoints.

The supplied Worker contract accepts URLs shaped as `<cors-prefix>https://api.dandanplay.net/api/v2/...`, allowlists the official host, adds its own application signature, and returns the upstream response. Although the path is named `cors`, DanmuPlus uses it as a server-side forwarding convention rather than for browser CORS.

## Goals / Non-Goals

**Goals:**

- Preserve existing installations and direct signed requests by default.
- Make proxy routing a single configuration decision shared by search, metadata, and comment requests.
- Keep the Worker and Emby credential domains separate.
- Retain current cache durations, response parsing, provider isolation, matching, bindings, and XML/ASS output.

**Non-Goals:**

- General-purpose configurable Dandanplay-compatible API servers or arbitrary upstream hosts.
- Duplicating the Worker implementation inside Emby.
- Adding file hashing, `/match`, or a live browser-overlay player.
- Encrypting Emby's existing configuration XML or provisioning Worker secrets.

## Decisions

### Persist a boolean mode and one CORS-prefix value

Add `UseProxyApi` with a default of `false` and `ProxyCorsUrl` with an empty default to `DandanOption`. A boolean is sufficient for the requested two modes and makes old serialized configurations deterministically retain custom/direct behavior. A string enum was considered but would add invalid states without providing a third supported mode.

The settings page presents radio controls labelled "使用代理 API" and "使用自定义 API". Proxy mode shows the CORS-prefix input and explanatory text; custom mode shows the existing API ID and Secret fields. Load and save always round-trip all fields, including currently hidden fields, so mode switching is reversible.

### Keep the official API URL separate from the transport URL

Each operation first builds the same official absolute URL it uses today. A centralized routing helper then either:

- returns the official URL for custom mode and applies the existing local signature; or
- validates and normalizes `ProxyCorsUrl`, appends the complete official URL, and skips local credential resolution and authentication headers.

Normalization trims whitespace and ensures exactly one trailing slash. Validation requires an absolute HTTP or HTTPS prefix. Invalid proxy configuration raises a sanitized configuration exception instead of falling back to direct access, because fallback would contradict the administrator's selected trust boundary.

An arbitrary custom upstream API base was considered and rejected: the request requires only the existing cf_worker prefix contract, while the target API and endpoint family remain official Dandanplay v2.

### Do not locally sign proxy requests

The Worker extracts the official target path and signs it with credentials stored by Cloudflare. Signing the outer Worker URL would use the wrong path, and forwarding Emby's signature or credentials would blur ownership and unnecessarily expose local authentication material. Proxy mode therefore does not call the credential resolver and adds no local Dandanplay authentication headers.

Ordinary request properties such as method, content negotiation, user agent, timeout, body, and query parameters remain unchanged.

### Preserve the existing Dandan scraper contract

Only request routing changes. `Dandan` continues searching `/search/anime`, loading `/bangumi/{animeId}`, selecting episode IDs, and downloading `/comment/{episodeId}`. The shared matching engine, manual and automatic bindings, provider priority, retry behavior, and stored output are unchanged.

### Verify routing with deterministic helpers and a minimal live smoke check

Expose or isolate pure routing decisions sufficiently for the existing regression executable to assert default migration, prefix normalization, exact endpoint/query preservation, direct-mode signing eligibility, proxy-mode credential independence, and sanitized invalid-input failures. Embedded-page assertions cover radio controls plus load/save round-tripping.

After deterministic checks pass, issue only a small number of read-only requests through `https://ddplay-api.7o7o.cc/cors/`. The live Synology/Emby verification uses credentials supplied out of band and never records them in source, artifacts, command output, or diagnostics.

## Risks / Trade-offs

- [A public Worker can be unavailable, altered, or quota-limited] → Surface the Dandanplay provider failure without silently bypassing the selected proxy; keep other providers operational.
- [Malformed slash handling can produce an invalid target URL] → Centralize prefix normalization and cover empty, whitespace, missing-slash, and repeated-slash inputs deterministically.
- [Proxy mode may accidentally resolve or log local credentials] → Branch before credential resolution and assert that proxy routing succeeds with empty local credentials and emits no authentication material.
- [Old configuration XML lacks the new fields] → Use nullable-safe defaults that select custom mode and preserve the current behavior.
- [Conditional UI can erase hidden values] → Always load and save all fields; visibility changes presentation only.
- [Using the shared test Worker consumes someone else's quota] → Limit the live check to the smallest useful read-only search/comment sequence and rely primarily on deterministic tests.

## Migration Plan

1. Build and run deterministic C# and frontend regressions without changing deployed configuration.
2. Back up the currently deployed DLL and Emby plugin configuration XML on Synology.
3. Deploy the Release DLL, restart Emby, and confirm old configuration loads in custom mode with existing credentials and options intact.
4. Confirm a direct custom-mode Dandanplay search remains functional.
5. Enter the supplied test CORS prefix, select proxy mode, save, restart, and run a low-volume live preview/download through the existing DanmuPlus title-based flow.
6. Confirm other providers and existing manual bindings still work and no credential/signature values appear in logs.
7. Roll back by restoring the backed-up DLL and configuration XML if any validation fails.
