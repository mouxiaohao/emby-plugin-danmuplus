## Why

The Smart Match dialog reuses one internal scroll container while replacing parent and child page content. A child can therefore inherit the parent's old offset, while returning can rebuild the parent at the top or at an unrelated pixel position; expanded episode mappings, rematch results, and changed row heights make a saved numeric offset alone unreliable. Native activation can also focus the clicked control and move the body between preactivation and `click`, so click-time geometry alone is already stale. Separately, the dialog installs an unconditional same-route browser-history guard: desktop Emby 4.9.5 treats that `pushState` as navigation, rebuilds the current virtual library list from its first page, and leaves the host view at the top even though menu close and fixed-overlay insertion do not independently change its position.

Authorized Android phone and car-head-unit probes then established the native return boundary. A command-trace probe proved that Emby's cancelable `command: "back"` event arrives before its downstream `backbutton`: canceling only that command kept the host route stable but also prevented the later event, so Smart Match did not move. A command-owner probe canceled the same command and immediately invoked the topmost Smart Match return state machine once; on both devices a child returned only to its parent, a top-level dialog only closed, busy/protected state stayed open, and Emby's route remained unchanged. These probes are causal design evidence, not acceptance of the final V30 release asset.

## What Changes

- Give every newly entered return-capable secondary Smart Match page an explicit top position after its content is rendered. This includes whole-Series Season candidates entered through `查看候选` or rematch, unmatched/remainder-range manual matching, Episode source selection, and Movie part/version selection; initial direct Season, Movie, and Episode candidate pages also begin at the top.
- Capture the parent page's semantic viewport anchor before any busy view, asynchronous search, or DOM replacement. Every explicit or Android return to that parent SHALL render current data and restore the equivalent logical action, or its row when that action no longer exists, at the prior viewport-relative position; if matching removes that row, restoration falls back through its surviving parent/neighbor and finally a clamped numeric position.
- Carry one parent context across child searches, re-renders, and recoverable submission failures instead of stacking duplicate contexts, and use a last-in-first-out context for genuinely nested child pages. Inline candidate details and recoverable busy/progress pages are not new parent-to-child navigation; a context is abandoned only after an accepted non-returnable task, dialog disposal, or another explicit exit from internal navigation.
- Stop creating, replacing, or traversing a dialog-owned browser-history entry on every browser runtime. Opening, returning within, and explicitly closing Smart Match must leave the current Emby route intact instead of repairing host state after a reload.
- On Android/WebView only, let one script-lifecycle capture listener own an exact cancelable `command: "back"` while a topmost connected Smart Match dialog is active. After cancellation succeeds, invoke that dialog's parent/top-level/protected-state return state machine exactly once; do not register or depend on a Smart Match `backbutton` listener, and do not cancel commands when no eligible dialog exists.
- Treat host `popstate` as Emby-owned on every platform: synchronously clean up all Smart Match overlays and their per-dialog listeners without internal parent return or a second history operation, while leaving the script-lifecycle command/popstate singleton listeners installed, then allow Emby navigation to continue.
- Keep the existing action-sheet close and immediate fixed-overlay insertion order because isolated desktop tests prove neither operation causes the host reset. Do not add host scroll snapshots, repeated restoration frames, or user-input cancellation machinery.
- Sample parent geometry passively on the exact internal navigation trigger during pointer or keyboard preactivation, then consume it atomically in the matching business `click`; cancelled, detached, changed, untrusted, or unmatched activations fall back safely without owning input.
- Preserve the current candidate set and ordering exactly. Scroll anchoring remains transient inside the open dialog, does not preserve obsolete DOM, and does not re-score or globally reorder candidates across providers.
- Stamp the complete plugin identity as 2.0.6r1: retain assembly version `2.0.6.0`, file version `2.0.6.1`, informational/configuration/TMDB User-Agent version `2.0.6r1`, advance the frontend installation marker from V29 to V30, and retain mapping protocol V22.
- Add deterministic coverage for every return-capable parent/child boundary, changed-height and missing-anchor restoration, zero dialog-owned history, Android command ownership and fail-safe cancellation, host-pop cleanup, plus desktop and final-V30 phone/car-head-unit live acceptance criteria.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `smart-match-error-and-presentation`: Define secondary-page initial position and parent-page semantic viewport restoration, prevent Smart Match dialog history from navigating or reloading the underlying Emby host view, and define single-owner Android command return behavior.

## Impact

- Primarily affects `Frontend/DanmuSmartMatch.CustomCssJS.js` and its JavaScript regression harness, including transient per-dialog parent navigation state, stable in-dialog row anchors, and Android command ownership; version/resource assertions and cumulative 2.0.6r1 documentation also change.
- Does not change backend matching, scoring, provider ordering, DTOs, saved bindings, downloads, metadata persistence, external dependencies, public routes, or mapping protocol V22.
- Does not add host-page scroll restoration, persist dialog navigation state, change Emby's action-sheet timing, or adopt unverified `CloseWatcher`/Navigation API behavior.
- A complete r1 package requires a cleanly verified version-stamped DLL and matching V30 frontend asset even though this corrective behavior is browser-side; no diagnostic probe marker, override, badge, trace state, or probe asset may enter that package.
