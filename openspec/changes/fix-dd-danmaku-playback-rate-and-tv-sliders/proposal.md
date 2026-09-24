## Why

The dd-danmaku CustomCssJS script currently multiplies scrolling motion by the video's playback rate, so 1.5x or 2x playback also accelerates comments instead of preserving the viewer's configured on-screen speed. Its shared range-control helper also lacks a reliable Android TV remote path, leaving visible settings sliders focused but not adjustable with the directional pad.

## What Changes

- Add a persisted, opt-in setting that keeps scrolling danmaku at the configured 1x wall-clock speed while comment appearance, seeking, pause/resume, and timeline offset continue to follow the video's media timeline.
- Apply that setting consistently to real HTML5 video and the hidden Emby playback adapter, including playback-rate changes made while comments are already on screen.
- Preserve the current rate-coupled motion exactly when the new setting is disabled and preserve existing saved dd-danmaku settings without migration.
- Make every visible slider created by the shared `embySlider` helper adjustable by the relevant TV remote direction keys, one declared step per key action, with correct clamping, label refresh, persistence, and reload behavior.
- Add deterministic script-level checks plus live Emby/Android TV acceptance coverage for rate changes, seek/pause transitions, focus, repeat, and slider boundaries.
- Non-goals: changing DanmuPlus matching/download/XML APIs; changing comment timestamps, timeline-offset semantics, or user-selected base speed; redesigning the settings dialog; modifying the intentionally controller-unsupported embedded-webpage controls; or deploying/replacing a live CustomCssJS configuration without separate authorization and rollback material.

## Capabilities

### New Capabilities

- `dd-danmaku-playback-speed`: Optional wall-clock-stable scrolling motion that remains synchronized to the Emby media timeline across playback-rate and transport changes.
- `dd-danmaku-tv-slider-input`: Deterministic directional-pad adjustment for visible dd-danmaku range settings on TV-class clients.

### Modified Capabilities

None.

## Impact

- Primary implementation target: the source-controlled verified dd-danmaku v1.47 baseline at `Frontend/dd-danmaku/baselines/ede.v1.47.downloads.js` (266,934 bytes; SHA-256 `B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4`), rather than any historical DanmuPlus release artifact.
- Affected dd-danmaku areas include local-storage settings, Danmaku engine initialization/runtime timing, the real/virtual media adapter event lifecycle, settings rendering, and the shared slider helper.
- The CustomCssJS execution path currently loads the external Danmaku 2.0.8 dependency instead of executing the embedded copy; the implementation must therefore own or explicitly control the timing behavior used in both execution paths rather than patching only unreachable embedded minified code.
- DanmuPlus backend assemblies, Smart Match frontend protocol, provider behavior, saved bindings, XML files, and existing unrelated OpenSpec changes remain untouched.
- Any later CustomCssJS installation must back up and hash the active script/configuration first, deploy the reviewed standalone dd-danmaku asset only, and retain the original v1.47 file as the direct rollback path.
