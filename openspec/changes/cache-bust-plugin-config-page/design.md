## Context

See `proposal.md` for the motivation. Emby exposes plugin embedded resources under stable plugin page names. The Danmu configuration HTML identifies its JavaScript controller by a second stable page name, so Chrome can retain an older response even after the server plugin DLL is replaced. In MediaBrowser.Model 4.8.5, `PluginPageInfo` can name an embedded resource but provides no stream, content factory, or response callback, so the plugin cannot rewrite the HTML when `GetPages()` runs.

## Goals / Non-Goals

**Goals:**

- Derive a deterministic resource identity from the plugin build and compile that identity into the loaded plugin.
- Make the page and controller identities match without changing configuration persistence or the dashboard menu entry; the browser-facing page `name` URL is expected to change with the installed build.
- Materialize the embedded configuration HTML at build time without committing a release-specific generated copy.
- Provide deterministic source-level regression checks for resource naming and page/controller pairing.

**Non-Goals:**

- Clearing browser caches, changing global Emby cache headers, or modifying Emby server binaries.
- Changing the Smart Match CustomCssJS integration, danmu provider requests, or saved configuration schema.
- Producing or deploying a standalone cache-fix release before the combined `2.0.1-r5` build.

## Decisions

### Generate one URL-safe cache token during the build

The project build will normalize the informational version into a conservative URL-safe cache token. One build target will use that single token to generate both a compiled C# constant and the embedded configuration-page HTML under `obj`; the source template remains the editable file in `Configuration`. `GetPages()` will use the generated constant in both the configuration page name and controller page name. This moves the cache key whenever a released plugin build changes while leaving the logical embedded-resource paths and configuration API unchanged.

Generating both outputs from one build property avoids duplicated manual version strings and prevents the C# registration name from drifting from the HTML reference. Using a query-string cache buster was considered, but Emby's page registry is keyed by `PluginPageInfo.Name`; versioning that name is explicit and does not rely on a server preserving arbitrary query parameters. Setting response cache headers was rejected because the plugin page API does not own Emby's response pipeline and it would be broader than this UI-specific issue.

### Transform the HTML template before embedding it

The source HTML will retain a cache-token placeholder in its `data-controller` value. Before compilation, the build target will replace that placeholder with the controller name derived from the same token, write the transformed page below `obj`, and embed the transformed file under the existing logical manifest resource name. The JavaScript remains the existing embedded resource and is registered under the matching versioned `PluginPageInfo.Name`. A Release build must fail or its regressions must fail if the placeholder remains unresolved.

Runtime stream replacement was rejected after inspecting MediaBrowser.Model 4.8.5 because `PluginPageInfo` has no dynamic content hook. Maintaining manually edited version strings in `configPage.html` was also rejected because it is easy to forget during a release and would reintroduce the problem.

### Keep compatibility and rollback simple

The stable logical page name and plugin GUID/configuration API are unchanged. A new plugin build merely causes the dashboard to see different page-resource identifiers; rollback to a prior DLL exposes its prior identifiers and configuration behavior with no data migration.

## Risks / Trade-offs

- [An Emby dashboard implementation assumes a stable controller name] → Preserve the existing `__plugin/` controller convention and validate the generated page/controller pair against the current Emby 4.9.3.0 build.
- [Build metadata contains unsafe URL characters] → Normalize the token to a conservative identifier alphabet and test the normalization.
- [The build-time HTML or C# generation drifts or is skipped] → Generate both from one MSBuild token, embed only the transformed HTML under the established logical resource name, and assert the built assembly contains matching identifiers with no placeholder.
- [Old cached dashboard metadata remains briefly available] → The first page response from the newer server publishes a new controller identifier; administrators can still perform one manual reload when the dashboard itself has not yet refreshed its plugin list.

## Migration Plan

1. Integrate the build-time cache-token generation, transformed embedded page, and runtime page registration with the other planned `2.0.1-r5` changes before producing an artifact.
2. Run the combined r5 regression suite and build the `2.0.1-r5` Release DLL; inspect the built embedded page for matching identifiers and no unresolved placeholder.
3. Deploy that combined DLL using the existing plugin replacement procedure and restart Emby so it registers the new resource identifiers.
4. Open the Danmu configuration page in a Chrome profile that previously held a stale copy and verify the new controls appear without clearing browser data.
5. Roll back by restoring the pre-r5 DLL and restarting Emby; no configuration restore is necessary for this cache change.
