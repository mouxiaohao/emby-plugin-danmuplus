## 1. Freeze the dd-danmaku baseline

- [x] 1.1 Re-read the frozen downloaded baseline, verify the v1.47 header, 266,934-byte size, and SHA-256 `B6E8DF2ED50C3B2B924A8181950D3A058F40EC4C34B93EDEAF088EEC0CE843C4`, then record the check without modifying the source file.
- [x] 1.2 Create a dedicated source-controlled dd-danmaku working area from that exact baseline, keep it distinct from `Frontend/DanmuSmartMatch.CustomCssJS.js` and historical release artifacts, and add an origin/license notice plus baseline verification record.
- [x] 1.3 Vendor the readable Danmaku 2.0.8 source and license corresponding to the embedded v1.47 engine, pin its provenance, and add a deterministic generation entry point for the engine payload used by the standalone script.
- [x] 1.4 Add a Node-compatible test harness with fake monotonic time, animation frames, media events/state, comment geometry, local storage, focus, and event propagation; freeze legacy timing and slider callback fixtures before changing behavior.

## 2. Implement wall-clock scrolling in the controlled engine

- [x] 2.1 Add an explicit fixed-speed scrolling option and immutable capability/version marker while preserving the original code path byte-for-behavior when the option is absent or false.
- [x] 2.2 Give active `rtl` and `ltr` comments a monotonic playing-time motion state and use it consistently for horizontal position, remaining path, track collision/availability, and off-screen removal.
- [x] 2.3 Preserve media `currentTime` for timestamp eligibility and seek positioning, initialize newly eligible rolling comments without rewriting source timestamps, and leave top/bottom fixed-comment lifetime behavior on the legacy media-time path.
- [x] 2.4 Freeze motion state on pause/wait and resume it from the same position on play; preserve legacy hide/show by clearing then seeking to current media time, clear motion state on seek/clear/destroy, and prove playback-rate changes do not rewrite active rolling-comment anchors.
- [x] 2.5 Add safe monotonic-time and invalid-rate fallbacks plus bounded cleanup for dense accelerated timelines so no invalid coordinate, leaked track, or permanently active comment can remain.
- [x] 2.6 Generate the distributable engine from the readable source and verify that the bundled/default CustomCssJS and fallback forms expose the same capability, version, timing results, and license notice.

## 3. Integrate the fixed-speed option into dd-danmaku

- [x] 3.1 Add the positive `倍速时保持弹幕滚动速度` boolean to the existing settings registry with a missing/invalid default of false, without eagerly rewriting old local storage or changing the saved base-speed value.
- [x] 3.2 Render the option beside the speed setting and include it in existing persistence, JSON export, and JSON import flows while preserving all unrelated keys and old payload compatibility.
- [x] 3.3 Validate the controlled engine capability before passing fixed-speed mode into Danmaku creation; retain the saved requested boolean while an incompatible configured engine forces effective legacy mode with a clear inactive diagnostic, then honor that request automatically when a compatible engine is later created.
- [x] 3.4 Make real HTML5 video and the hidden Emby media adapter share the same option and media-time behavior, including initialization at a non-unit rate and rate changes reported after player replacement.
- [x] 3.5 Implement transactional runtime mode switching: preserve media time/rate/paused state and parsed comments, replace exactly one Danmaku instance, and restore the previous saved/runtime mode if creation fails.
- [x] 3.6 Replace the divergent unpinned default-engine loading behavior with the deterministic controlled build while retaining an explicitly user-configured custom engine for legacy mode and preserving the old Android WebView syntax target.

## 4. Make shared sliders TV-remote operable

- [x] 4.1 Add one reusable direction normalizer and decimal step-grid calculator covering key aliases/codes, HTML range defaults, nonzero minima, fractional steps, `step=any`, clamping, and display-safe rounding.
- [x] 4.2 Install one idempotent handler on every shared slider at construction, act only when it is enabled and user-focusable, cancel native/host handling, apply exactly one changed value step, dispatch one `input` then one `change`, and never call persistence or reload callbacks directly.
- [x] 4.3 Consume outward boundary and repeated Left/Right actions while retaining focus, emit no value events at an unchanged boundary, and leave Up/Down plus unrelated keys available for normal dialog navigation.
- [x] 4.4 Route any supported per-element Emby TV direction command through the same adjustment routine and deduplicate a physical action also observed as DOM keydown without adding a second global listener or timer.
- [x] 4.5 Preserve pointer drag/tap, desktop range keys, Emby Theater behavior, label updates, existing per-slider reload policies, and the controller exclusion for embedded-webpage controls.

## 5. Add deterministic regression coverage

- [x] 5.1 Prove option-disabled 0.5x/1x/1.5x/2x position, collision, removal, pause/resume, and seek fixtures match the frozen v1.47 behavior.
- [x] 5.2 Prove option-enabled startup at 0.5x/1x/1.5x/2x and live 0.5→2, 2→0.5, and 1→1.5 transitions retain equal wall-clock slope with no position jump, reset, duplicate, premature removal, or stale track.
- [x] 5.3 Cover pause/resume, wait/play, show/hide, forward/backward and paused seek, timeline offset, invalid rate, three user base-speed levels, top/bottom regression, high-density cleanup, and source timestamp immutability.
- [x] 5.4 Run the same timing cases through a real-media fake and hidden Emby-adapter fake, prove controlled default/fallback engine forms have identical capability and results, and prove an incompatible custom engine retains requested=true while effective mode fails closed until compatibility returns.
- [x] 5.5 Cover setting absence/true/false/invalid values, export/import, reopen persistence, successful playing/paused runtime toggles, and transactional rollback after simulated engine-construction failure.
- [x] 5.6 Cover every visible slider definition at minimum/middle/maximum with integer/fractional/nonzero-min steps, aliases, repeats, boundary consumption, focus continuity, one input/one change, one-or-zero reload, no native double-step, and no media seek.

## 6. Verify build, compatibility, and review scope

- [x] 6.1 Run the focused dd-danmaku timing, settings, adapter, and slider suites plus syntax/static checks and a clean deterministic engine/script regeneration comparison.
- [x] 6.2 Run the available adjacent CustomCssJS loader/injection checks to prove the standalone script executes in the server/local injection paths without duplicate engine instances.
- [x] 6.3 Run existing DanmuPlus frontend/backend regressions sequentially and a clean Release build as a no-backend-regression gate; record that no DLL, provider, matching, XML, or Smart Match behavior changed.
- [x] 6.4 Run strict OpenSpec validation, `git diff --check`, source-baseline/hash verification, generated-file drift checks, scope/credential/private-path review, and confirm the unrelated active OpenSpec worktree content remains untouched.
- [x] 6.5 Per the user's single-Agent instruction, have the primary Agent perform a separate final design/code/test-evidence review pass, resolve every blocking finding, and rerun affected gates.

## 7. Package and live-verify the standalone asset

- [x] 7.1 Assemble a review package containing only the standalone dd-danmaku candidate, required license/source notices, SHA-256 and size, controlled-engine capability/version, baseline hash, deterministic commands/results, compatibility notes, and rollback instructions.
- [x] 7.2 Present the exact package and hashes and obtain explicit confirmation before changing any live CustomCssJS script/configuration, client asset, or Emby process.
- [x] 7.3 After confirmation, back up and hash the active dd-danmaku/CustomCssJS configuration with ownership and modes, verify a directly usable rollback copy, and deploy only the reviewed standalone asset.
- [x] 7.4 Reload or restart only the required client/server surface, read back the installed hash and capability marker, verify Emby/player health, and immediately restore the backup if syntax, load, capability, or health checks fail.
- [ ] 7.5 Live-test Web/Emby Theater real-video and hidden-adapter playback at 0.5x/1x/1.5x/2x, all three live rate transitions, pause/resume, seek, timeline offset, runtime toggle, disabled legacy behavior, and top/bottom regression.
- [ ] 7.6 On Android TV, verify focus entry and one-step Left/Right operation for every visible settings group, decimal labels, long-press repeats, boundaries, persistence after reopening, reload-heavy controls, Up/Down navigation, and absence of video seek or duplicate instances.
- [x] 7.7 Record deployed/read-back hashes, completed live results, untouched DanmuPlus artifacts, and the exact rollback path; if any required live case fails, restore the original asset/configuration and verify its hashes and v1.47 behavior.

Live evidence for 7.5 covers the actual Web player, all eight Emby menu rates,
all three transitions, pause/wait/play, forward and backward paused seek,
runtime toggle, disabled legacy behavior, and a real top comment. Deterministic
coverage supplies the hidden-adapter and unavailable live mode variants.

Live evidence for 7.6 covers real DOM `keydown` and `emby-direction` paths,
focus, steps, decimals, boundaries, repeats, persistence, reload stress,
Up/Down ownership, no paused-media seek, and single-instance settlement. It
remains unchecked because no physical Android TV device was connected.
