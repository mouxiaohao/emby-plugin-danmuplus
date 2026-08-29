## Context

See `proposal.md` for the user-facing defect and `specs/smart-match-tv-remote-navigation/spec.md` for the behavior contract. The 2.0.7r2 baseline injects one fixed Smart Match overlay, uses native HTML controls, restores parent/child body scroll through transient semantic navigation contexts, contains vertical scrolling with overlay-local CSS, and owns Android Back only through Emby's cancelable `command: "back"`. It has no Arrow-key routing and its V34 stylesheet only distinguishes pointer hover and state colors.

The live Synology target runs Emby 4.9.5.0. Its enabled Emby.CustomCssJS configuration contains both the Smart Match component and an unrelated `danmuku` component, so deployment must replace one named component instead of serializing a repository-only configuration over the active file. The first V35 deployment proved the controller and target-only replacement, then live Season testing exposed three follow-up gaps: narrow radio geometry lets a right-aligned footer beam skip every candidate card, reaching the header X does not align a scrolled body to its top, and rematching an early mapped temporary Season re-submits later mapped selections so the V22 backend correctly rejects them as no longer being the unique trailing suffix.

## Goals / Non-Goals

**Goals:**

- Make every current Smart Match surface operable with a conventional television D-pad and OK key without requiring Emby-private navigation APIs.
- Make the complete candidate card the remote selection target while preserving its contained native radio, pointer behavior, and one-selection semantics.
- Keep handled focus inside only the topmost overlay and prevent the host page from also consuming a handled direction.
- Restore focus meaningfully across candidate rerenders and existing parent/child transitions while preserving r1 semantic scroll restoration.
- Reveal newly focused content by changing only the Smart Match body's scroll offset, and align that body to zero when Up reaches the header close control.
- Rebuild the clicked temporary Season and all later temporary Seasons as one trailing unmatched suffix before an explicit rematch, while retaining earlier mappings and exact cancel/failure rollback.
- Preserve pointer, touch, native field editing, selection, disclosure, Escape, Android command Back, protected work, cancellation, retry, and disposal behavior.
- Produce deterministic fake-DOM evidence and an authenticated 1280x720 live Emby validation before retaining the deployment.

**Non-Goals:**

- Patching Emby dashboard assets or importing its private focus/navigation modules.
- Detecting a television from width, UA, touch capability, or device labels; direction handling is safe and available whenever the overlay receives the standard keys.
- Wrapping directional focus at an edge, changing selection merely because focus moved, or auto-activating a focused candidate.
- Adding a second Back channel, dialog history, global host focus traps, host-scroller lookup, whole-page `scrollIntoView`, timers, animation frames, or MutationObserver-based focus correction.
- Changing backend matching, APIs, persistence, V22 mapping payloads, downloads, provider behavior, unattended matching, or ordinary single-run Remove behavior.

## Decisions

### 1. Give each dialog one topmost-gated remote controller

`openDialog` creates one transient remote controller owned by that dialog. It installs a document capture `keydown` listener so direction input can recover even if Emby temporarily moved active focus outside the overlay, and an overlay capture pointer listener that clears the remote-only marker when input mode changes. The existing `isTopmost` check gates all work; a lower dialog and the Emby page are never queried or mutated after that gate fails. Disposal removes both listeners, clears the marker/reference, and remains idempotent.

The controller normalizes `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Enter`, `NumpadEnter`, and standard `Select`, with the conventional numeric key codes only as a legacy fallback when `key`/`code` is unavailable. It ignores composition. For an owned direction it calls `preventDefault` and `stopImmediatePropagation` when available, falling back to `stopPropagation`, so Emby's later host listener cannot move a second time. It does not handle Escape or Back; those stay on the existing desktop/Android paths.

This per-dialog listener is preferred over patching Emby's focus manager because the injected component has no stable dependency contract with private dashboard modules. It is preferred over an overlay-only bubbling listener because recovery must work when a client leaves active focus on the host immediately after fixed-overlay insertion.

### 2. Discover eligible native controls and candidate proxies with a bounded overlay tree walk

At each handled key, walk only the topmost overlay subtree in DOM order and collect enabled native `button`, non-hidden `input`, `select`, `textarea`, `summary`, linked anchor, and explicit nonnegative-tab-index elements. Every radio candidate label becomes a single nonnegative-tab-index `role="radio"` proxy with synchronized `aria-checked`; its contained radio is removed from sequential focus but remains the real checked/form/click target. Exclude disabled, disconnected, hidden, `aria-hidden`, negative-tab-index, `display:none`, `visibility:hidden`, and geometry-less elements. Exclude the close button while the dialog is protected and `dialog.closable` is false, because focusing a control whose workflow intentionally does nothing is misleading.

Tree walking avoids a complex selector dependency in old WebViews and makes document-order tie breaking explicit. The list is recomputed per key rather than cached: Smart Match replaces surfaces, expands details, enables retry controls, and changes busy/protected state often enough that cache invalidation would be more fragile than the small bounded walk.

All candidate rows continue to contain native radio controls and preserve label click behavior, but the full-width label is now the one keyboard/remote stop. Space or pointer activation issues one native radio click/change, and the dialog confirm bridge does the same for Enter/Select. Candidate detail buttons remain separate actions, checkboxes remain native controls, and `summary` remains the native disclosure stop.

### 3. Use half-plane geometry with stable scoring

For each candidate, compare the centers of its rendered rectangle and the current control. A candidate is eligible only when its center lies strictly in the requested directional half-plane, with a one-pixel tolerance to avoid subpixel noise. Prefer candidates whose rectangle overlaps the current rectangle when projected onto the cross axis; within the same overlap class rank by primary-axis distance first, cross-axis distance second, Euclidean distance third, and original DOM order last. The cross-axis beam prevents Right on a footer button from jumping to a slightly nearer but vertically distant search control, while the remaining order keeps vertical lists and ties predictable without screen-specific row/column metadata. Giving a candidate proxy the complete card rectangle is essential: the original 13-pixel radio rectangle sat far left while search/footer actions sat right, so beam priority skipped every candidate even though the list was visually between them.

At an edge, retain the current focus and still consume the direction; do not wrap into a distant control or the host page. If active focus is missing, detached, ineligible, or outside the overlay, the first direction recovers to the surface entry target and does not take a second step. Entry priority is a surviving semantic identity, then the first body control, first footer control, and finally an eligible header control.

This geometry model is preferred over sequential Up/Down plus Tab-order Left/Right because Smart Match mixes body rows, nested detail actions, numeric fields, header close, and footer actions. It is preferred over author-maintained neighbor maps because every candidate/result count is dynamic and server-authored order must remain untouched.

### 4. Keep confirm native except where target WebViews need a choice bridge

Buttons, summaries, links, and the existing search inputs retain their native Enter behavior and current key/click handlers. The controller does not synthesize their click, preventing duplicate searches or transitions. For a focused candidate proxy, radio, or checkbox, Enter/NumpadEnter/Select is canceled and one click is issued to the underlying choice when `event.repeat` is false; this bridges WebViews where Enter does not toggle choice controls. A candidate proxy handles Space with the same non-repeating native-radio click so ordinary keyboard selection remains available after consolidating the row to one stop. Moving focus never checks a control.

Horizontal directions in editable text/search/number fields are not owned so caret/editing behavior stays native. Up/Down remain owned so a remote user can leave the field. Tab is trapped only at the overlay boundary and cycles through the same eligible list; interior Tab remains native. This preserves desktop keyboard behavior while guaranteeing a connected modal cannot strand focus on the host.

### 5. Make surface focus continuity synchronous and semantic

Introduce a narrow `beginDialogSurface` / `completeDialogSurface` pair around the existing eleven body/footer replacement sites. Begin captures the active control's transient semantic signature before removing nodes; complete restores an equivalent eligible control or applies body/footer/header entry priority. A signature combines only existing opaque presentation-anchor tokens and control-local semantics such as element type, input name/value, aria label, placeholder, and action text. It is dialog-memory-only, is cleared on disposal, and is never sent, persisted, logged, or placed into configuration.

The pair is synchronous and explicit. It does not use MutationObserver, a timer, a promise turn, or `requestAnimationFrame`, so a later correction cannot pull focus away from the user or overwrite r1 scroll geometry. Dynamically appearing progress/retry controls need no observer: if no eligible control existed, the next directional key recomputes the list and recovers to the new action.

Extend the existing navigation context return path without changing its scroll contract. Parent rendering temporarily defers generic completion focus; after `restoreParentViewport` resolves the semantic action/row/section/neighbor ladder, focus the corresponding action or first eligible descendant with `{preventScroll:true}` and then pop exactly one context. Child entry completes after `resetSecondaryViewport`, choosing its first actionable control. Same-page candidate rerenders attempt signature continuity rather than inventing a parent context.

### 6. Reveal focus by writing only the dialog body

Focus with `element.focus({preventScroll:true})`, falling back to plain `focus()` only for older engines. When the target is inside `dialog.body`, compare its rectangle with the body's visible rectangle and adjust only `body.scrollTop` by the minimum top or bottom delta, clamped to its scrollable range. Header/footer targets ordinarily do not write body scroll. The one explicit boundary rule is an upward spatial move that lands on the header close button (or another Up while it is already there): synchronously set only `dialog.body.scrollTop = 0`, matching the user's mental model that X is the top boundary. Never call `element.scrollIntoView`, read a host scroller, or write document/body/page scroll state.

The focused element receives a transient `.danmuTvFocused` class. Pointer/touch down clears that class and remote-input mode; the next owned direction restores it. CSS combines that class, `:focus-visible`, `:focus`, and row `:focus-within` selectors to provide a three-pixel high-contrast outline plus an offset accent without changing borders or box dimensions. Disabled styling remains visually dominant and disabled controls are not eligible.

### 7. Rebuild an explicit temporary-Season rematch from its clicked suffix

The V22 backend accepts a manual remainder selection only when the target run is the unique maximal unmatched suffix. When mapped temporary Seasons 1/2/3 already cover one physical Season, removing only temporary Season 1 while re-submitting 2/3 violates that invariant by construction. For the explicit Rematch action, find the clicked group's first item in the Season's authoritative eligible `OrderedEpisodes`, derive every item from that point to the end, filter both server-authored and browser-manual selections overlapping the suffix, add all suffix item identifiers to the draft exclusions, and issue the existing authoritative preview request with only earlier selections. Do not create user-visible removed-run restore entries for this transient operation.

The accepted preview must expose one unmatched run containing the clicked start, and that exact run becomes the child picker/search scope. The pre-action snapshot already covers the Season plan, selections, exclusion/removed-run draft, keywords, candidates, details, and navigation transition; Back, cancellation, a missing suffix, or any request failure restores it exactly. A successful replacement clears the rematch snapshot: if it covers only the beginning of the suffix, the server-authored remainder stays unmatched and the superseded later temporary-Season mappings are not revived. The ordinary Remove action keeps its existing clicked-run-only filtering, exclusions, and Restore affordance.

This is a browser draft/request-shaping correction, not a backend or V22 contract change. The existing suffix rejection remains an important server guard and is tested unchanged.

### 8. Keep version and packaging boundaries explicit

Advance the final frontend installation marker from the r2 V34 baseline through the early r3 V35 deployment to V36, so long-lived clients cannot retain either older closure after the follow-up fixes. Keep mapping protocol V22. Bump file/informational/configuration/TMDB User-Agent identity to 2.0.7r3 while keeping assembly compatibility at 2.0.7.0. Backend source behavior remains byte-for-byte equivalent outside version identity.

The review package contains the clean Release DLL, V36 frontend, cumulative UPDATE, verification record, and SHA-256 manifest under `artifacts/2.0.7r3/review-package`. Existing r1/r2 evidence and every README demonstration image remain untouched.

### 9. Verify controller and suffix-rematch behavior at three layers

Extend the fake DOM with active-element ownership, focus/blur/focusin, class-list state, geometry, computed visibility, prevent-scroll recording, propagation cancellation, pointer-mode events, and body-only reveal geometry. Drive real dialog renderers and real key dispatch for initial focus, lost-focus recovery, nested topmost ownership, every direction, tie/edge behavior, disabled/hidden exclusion, full-width candidate proxy traversal and one-shot confirm/Space, native button/search behavior, editable Left/Right, Tab boundaries, pointer handoff, surface rerender continuity, child return, busy-without-target recovery, off-screen reveal, Up-to-X body alignment, and idempotent cleanup. A three-mapped-run composite fixture must prove suffix request pruning/exclusions, merged picker scope, partial replacement remainder, exact Back/failure rollback, and unchanged non-cascading Remove.

Static guards require V36/V22 with V35 and earlier install flags absent, focus styles, no `scrollIntoView`, no host-scroller selectors/writes, no MutationObserver/timer/animation-frame focus correction, no new history/backbutton/Back path, and no backend/API/protocol changes. Then run the complete existing frontend regression, strict OpenSpec validation, main regression suite, and a clean sequential Release build.

Live validation uses the authenticated Emby 4.9.5.0 web client at a 1280x720 television viewport. Open Series/Season/Episode/Movie Smart Match from real UI paths, send only keyboard D-pad/Enter/Tab/Escape equivalents, record focus target/rectangle/body scroll/host scroll/route and request counts, and prove that a real Season candidate list no longer jumps directly from search to the footer. Exercise selection, Up-to-X body alignment, and reversible child navigation without starting a bind/download except for a bounded composite rematch fixture whose original state is restored or whose expected unmatched remainder is independently verified. Reload after deployment to prove V36 exactly once and V35/V34 inactive. Browser emulation validates the deployed web asset and event semantics; it does not falsely claim physical remote hardware or a different vendor WebView was tested.

## Risks / Trade-offs

- [A vendor remote does not emit standard web Arrow/Enter events] → Record the actual event shape on that device before adding aliases; do not guess from device identity or capture unrelated media keys.
- [Emby has an earlier document-capture listener] → Stop immediate propagation for owned directions and verify host route/focus stays fixed in the deployed client; if ownership still cannot be established, fail live acceptance and roll back.
- [A control reports zero geometry before layout] → Synchronous surface completion falls back to the next eligible rendered control; the first later direction recomputes live geometry without scheduled correction.
- [Native focus fallback scrolls a host in an older WebView] → Verify `preventScroll` support and host scroll in live acceptance; compensate only with dialog-body writes, never host restoration. Roll back if host movement remains.
- [Geometry picks a nested detail action instead of the next candidate] → Full-card candidate proxies and primary-axis/cross-axis fixtures cover mixed row/detail layouts; adjust the generic score only with evidence, not page-specific neighbor maps.
- [Surface completion conflicts with parent viewport restoration] → Defer generic focus during parent rendering, restore scroll first, then focus the resolved semantic target with prevent-scroll.
- [Candidate or radio confirm fires twice] → Own only candidate-proxy/radio/checkbox confirm, cancel the key, ignore repeats, and assert one click/change; leave every other native control unsynthesized.
- [Suffix rematch drops an earlier mapping or cannot be cancelled safely] → Derive the boundary from authoritative ordered item IDs, keep every earlier selection, snapshot the complete interactive draft before mutation, and restore it on Back, missing-run, cancellation, or request failure.
- [The active CustomCssJS file changes during implementation] → Freeze a fresh predeployment copy immediately before replacement and compare every non-target component name/state/content hash after serialization. Abort rather than overwrite concurrent user changes.
- [A frontend-only functional change is paired with a stale install guard] → Build and deploy the versioned DLL and V36 component as one reviewed pair; verify V35 cannot suppress the new closure, then verify DLL/config hashes and runtime version after restart.

## Migration Plan

1. Work only in the isolated 2.0.7r3 worktree based on commit `9b8b7465f2487c7fcf9cc1f0d972676f946aa46c`; do not modify the dirty primary checkout or its `dd-danmaku` work.
2. Add failing remote-focus and suffix-rematch regressions, implement the controller/surface hooks/styles and explicit suffix draft pruning, advance the frontend guard to V36 while retaining V22 and 2.0.7r3 identities, then run syntax, complete frontend, strict OpenSpec, backend regression, Release build, diff, credential, and package checks.
3. Immediately before live deployment, copy the active DLL and complete Emby.CustomCssJS XML into a new timestamped rollback directory on the shared volume; record SHA-256, size, owner, mode, Emby health, component inventory, and target/non-target content hashes.
4. Stage the reviewed DLL and transform only the enabled named Smart Match `<Custom>` content in a copy of the just-frozen XML. Reparse it, require the unrelated `danmuku` component's name/state/content hash and all other configuration fields unchanged, then atomically install the pair with original ownership/mode.
5. Restart Emby, require HTTP 200 and version 4.9.5.0, verify the new plugin identity and V36 marker with V35/V34 absent, authenticate through the normal client, and run the bounded 1280x720 D-pad acceptance. Retain the deployment only if focus, route, host-scroll, control, request, and unrelated-component checks all pass.
6. On any staging, restart, health, hash, focus, route, host-scroll, interaction, or unrelated-configuration failure, atomically restore the paired predeployment DLL/XML, restart, require HTTP 200, and verify their exact hashes. No media database or stored mapping migration is required.
