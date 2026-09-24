## Context

See `proposal.md` for motivation and the two delta specs for the behavioral contract. The inspected baseline is the standalone dd-danmaku v1.47 script at `Frontend/dd-danmaku/baselines/ede.v1.47.downloads.js` (266,934 bytes; SHA-256 `B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4`); it is not the DanmuPlus Smart Match asset and must remain untouched as recovery material.

The script binds Danmaku 2.0.8 to either the page's real video element or a hidden video whose `currentTime` and `playbackRate` mirror Emby's playback manager. Danmaku 2.0.8 uses `media.currentTime` to admit and remove comments, but its scrolling position and collision prediction also multiply wall-clock elapsed time by `media.playbackRate`. In a CustomCssJS call stack, v1.47 skips the already embedded engine and imports the URL-configured engine instead, so patching only the embedded minified blob would leave the deployed path unchanged.

All visible range settings pass through one `embySlider` helper. That helper currently lets native range events drive `input` and `change`, and stops matching direction keys only for Emby Theater. Android TV can therefore route Left/Right away from the focused slider before a reliable value commit.

## Goals / Non-Goals

**Goals:**

- Separate comment timestamp scheduling from scrolling movement without changing source comment times or Emby's playback state.
- Keep one auditable engine implementation and prove the same capability is active in every supported dd-danmaku loading path.
- Keep the disabled path behaviorally identical to v1.47 and require no saved-settings migration.
- Centralize remote slider adjustment so all existing visible sliders inherit identical step, callback, and focus behavior.
- Produce a standalone reviewed CustomCssJS asset and deterministic evidence before any live configuration is touched.

**Non-Goals:**

- Forking DanmuPlus backend behavior, matching, provider, XML, or Smart Match code.
- Re-timing top/bottom fixed comments or redefining the existing speed slider and timeline-offset setting.
- Making controller input work inside the explicitly unsupported embedded webpage.
- Silently replacing a user-configured third-party Danmaku engine that lacks the new timing capability.

## Decisions

### Use two clocks inside a controlled Danmaku engine

The engine will retain `media.currentTime` as the timeline clock for pending-comment eligibility, timestamp ordering, timeline offset, and seek position. Fixed-speed scrolling will use an independent monotonic motion clock derived from `performance.now()` and accumulated only while the media is actually playing.

Each active right-to-left or left-to-right comment in fixed-speed mode will own a motion anchor and accumulated playing time. Its position, remaining path, collision/track availability, and off-screen removal will all be computed from that same motion age and the user's configured 1x base speed. Pause/wait accumulates and freezes the age; play resumes from the same position with a new wall-clock anchor; a playback-rate event does not rewrite it. Seek keeps the legacy semantic boundary: clear active tracks and restart timestamp selection from the destination media time. Hide/show also keeps the legacy contract by clearing the hidden stage and seeking to the then-current media time when shown, rather than reviving a pre-hide motion state. Top/bottom fixed comments stay on their existing timeline-coupled lifetime path.

The option-disabled branch will continue through the original Danmaku 2.0.8 formulas and state transitions rather than approximating them through the new clock. This is required for an exact compatibility fallback.

Alternatives rejected:

- Dividing the public `speed` value by the current playback rate corrects only a constant-rate snapshot. After a rate transition, media age is the integral of all prior rates, so one current scalar cannot simultaneously repair movement, media-time removal, collision state, and pause/resume anchor reconstruction.
- Reporting a proxy playback rate of 1 fixes the position multiplier but still lets real media time remove a 2x comment before it crosses the screen and lets a 0.5x comment occupy a track too long.
- Recreating the Danmaku instance on every playback-rate event avoids mixed formulas but clears visible comments and violates the rate-change continuity contract.

### Ship one fixed-version engine source and verify its capability

Implementation will vendor the exact Danmaku 2.0.8 source needed by v1.47 as a readable source file, add the wall-clock-scrolling option there, and generate the distributable engine payload deterministically. The generated constructor will expose an immutable capability/version marker that dd-danmaku checks before enabling fixed-speed mode.

The default CustomCssJS path and the non-CustomCssJS fallback must resolve to the same generated engine logic. The preferred standalone delivery is to bundle that generated default into the dd-danmaku asset so the feature does not depend on an unpinned network response. If an existing explicit custom engine URL is retained for compatibility, legacy mode may continue to use it; fixed-speed mode may become effective only when the loaded constructor advertises the expected capability. A missing or mismatched marker leaves the saved requested value unchanged and visible, forces only the effective runtime to legacy mode, and presents a clear inactive diagnostic. When a later engine creation finds the capability, the saved request becomes effective automatically.

This avoids two independently hand-edited minified engines and gives deterministic tests one source of truth. Generation must retain the baseline's old Android WebView syntax target and licensing notices.

### Add an opt-in, fail-closed setting

Add a positive boolean setting labelled `倍速时保持弹幕滚动速度` beside the existing speed control. Its missing/invalid default is `false`; reading an old configuration does not write a new value until the user makes a choice. Export/import includes the boolean through the existing settings enumeration, while the existing base-speed value remains unchanged. The checkbox always reflects the persisted requested value; a separate diagnostic reports when that request is currently inactive because the loaded engine lacks the capability.

At Danmaku creation time, the script passes the fixed-speed option only after validating the engine capability. Changing the option during playback uses the existing reload boundary as an atomic engine replacement: preserve `media.currentTime`, playback rate, paused state, parsed comments, and all unrelated settings; destroy the old instance; create one instance in the selected mode; and resume the same media state. This deliberate user action may clear comments already on the stage, unlike an ordinary playback-rate change. If construction fails, restore the previous saved value/mode and report the failure so UI and runtime cannot diverge.

### Route remote adjustment through one per-slider handler

`embySlider` will install one idempotent directional handler whenever it constructs a shared range input, without gating registration on the control's initial visibility or the current Emby-Theater identity. At event time the handler acts only on an enabled, user-focusable slider; a control revealed after a collapsed group opens therefore already has the handler. It will normalize `ArrowLeft`/`Left`/37 and `ArrowRight`/`Right`/39, use the element's effective `min`, `max`, and `step`, and round on the decimal step grid before clamping. Missing HTML range bounds use the platform defaults; an invalid or `any` step uses the explicit compatibility step of 1.

For a handled horizontal direction the handler calls `preventDefault()` before native range stepping and `stopPropagation()` before Emby navigation/input-manager routing. It assigns the value once, dispatches one `input` event for existing live-label behavior, then one `change` event for the existing persistence/reload callback; it never calls those callbacks directly. At a bound it still consumes the outward key and retains focus but emits no value events. System-generated repeated keydown events each produce at most one step, with no private repeat timer. Up/Down and unrelated keys remain available to dialog navigation. `stopImmediatePropagation()` is reserved only for a demonstrated same-node duplication because it could suppress the plugin's own listeners.

The same normalized adjustment routine will be used if a supported TV host exposes a per-element directional command instead of a DOM keydown; registration still remains local to the slider, and a short event identity guard prevents one physical action exposed through both paths from committing twice.

### Build deterministic timing and input harnesses before live testing

A Node-compatible harness will load the readable engine and dd-danmaku helpers with fake monotonic time, media state, animation frames, comment geometry, local storage, and event propagation. Timing tests will compare the disabled path against frozen v1.47 fixtures, then cover 0.5x/1x/1.5x/2x startup, 0.5→2, 2→0.5, and 1→1.5 transitions, pause/resume, forward/backward seek, real and hidden media adapters, top/bottom regression, invalid rates, three base-speed values, collision release, and high-density cleanup.

Slider tests will exercise every declared min/max/step shape, integer and decimal normalization, key aliases, repeats, boundaries, one input/one change, callback/reload counts, focus, and duplicate native/host routes. The final script will also receive syntax/static checks, a deterministic regeneration/hash check, the existing CustomCssJS loader/injection checks where available, the repository's sequential Release build as a no-backend-regression gate, and strict OpenSpec validation. Android TV and Emby Theater remain required live acceptance environments because their focus and input-manager behavior cannot be proven by the VM harness alone.

## Risks / Trade-offs

- [A rate fix changes only position but misses collision or removal] → Drive all rolling-comment lifecycle decisions from the same motion age and add early-removal, stale-track, and density stress fixtures.
- [Constant wall-clock lifetime at 2x increases simultaneously active comments] → Treat this as a consequence of media-time scheduling plus fixed screen speed; retain current filters/track limits and verify bounded cleanup under high density.
- [External engine URL or cache serves an incompatible build] → Prefer the bundled fixed build, pin/hash packaged assets, require a capability marker, and fail closed to legacy mode.
- [Generated embedded and module forms drift] → Generate them from one readable source and fail verification when byte/version/capability evidence differs.
- [Old Android WebView rejects new syntax or timing APIs] → Preserve the current ES target, use the existing monotonic-time fallback, and include syntax plus live client gates.
- [TV delivers one press through native DOM and Emby routing] → Cancel the native path, stop host propagation, deduplicate dual exposure, and assert one step/one commit.
- [Long-press reload-heavy sliders cause jank] → Use only system repeat events and existing reload semantics; measure on TV rather than coalescing steps and violating deterministic input behavior.
- [Runtime option rebuild fails after saving] → Construct transactionally and roll the saved option/runtime instance back together with an explicit diagnostic.

## Migration Plan

1. Freeze the verified v1.47 baseline in the source-controlled dd-danmaku working area, keep it immutable, and record the source hash in verification evidence.
2. Add the readable fixed-version engine source, deterministic generator, dd-danmaku changes, and automated harnesses. Produce a standalone candidate and record its file size, SHA-256, engine capability/version, and generation command.
3. Run focused timing/input suites, legacy fixtures, CustomCssJS loading checks, syntax/static checks, sequential repository regressions/build, strict OpenSpec validation, diff/scope checks, and a separate primary-Agent final review pass as required by the user's single-Agent instruction. Do not deploy during these steps.
4. Present the exact candidate and hashes for explicit deployment confirmation. After confirmation, back up and hash the active CustomCssJS script/configuration and retain the original v1.47 baseline as a directly usable rollback asset.
5. Deploy only the reviewed dd-danmaku asset, reload/restart the applicable clients, read back the installed hash, and execute the playback-rate plus Android TV/Emby Theater matrix. On capability, syntax, health, input, or timing failure, immediately restore the backed-up asset/configuration and verify the original hashes and legacy behavior.

Functional rollback is also immediate: leaving the new setting disabled uses the v1.47 timing path. Asset rollback remains authoritative for any broader script or client compatibility regression.
