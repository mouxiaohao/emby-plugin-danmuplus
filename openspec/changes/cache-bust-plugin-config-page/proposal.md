## Why

After a plugin update, one Chrome profile can continue to render an older Danmu configuration page while other browsers already show the current page. The embedded configuration resources use stable names, so a browser can reuse a stale cached response; clearing site data and forcing a reload is an unreliable administrator workaround.

## What Changes

- Publish the Danmu configuration page and its controller script using versioned resource identifiers derived from the installed plugin build.
- Generate the embedded configuration-page HTML during the build from the source template and the same normalized build token compiled into the plugin, because Emby 4.9.3's plugin-page API cannot transform resource contents at runtime.
- Ensure the configuration page always requests the matching versioned controller script.
- Retain the existing dashboard menu entry, form behavior, and plugin configuration API; the browser-facing configuration-page `name` identity (and therefore its `configurationpage?name=` value) is intentionally versioned with the installed build.
- Include this cache fix in the combined `2.0.1-r5` plugin release rather than publishing it as a standalone build.

## Capabilities

### New Capabilities

- `plugin-configuration-cache-versioning`: Delivers configuration UI resources with an installed-build-specific identity so browsers fetch the current interface after an update.

### Modified Capabilities

- None.

## Impact

- Affected code: `Plugin.cs`, the project build target, embedded configuration-page resource generation, and focused regression checks.
- Affected system: Emby admin dashboard browser caching for the Danmu configuration UI.
- Release coordination: implementation, packaging, deployment, and live verification are deferred until the other `2.0.1-r5` changes are ready to integrate.
- No new dependencies, configuration migration, or change to danmu matching/download behavior.
