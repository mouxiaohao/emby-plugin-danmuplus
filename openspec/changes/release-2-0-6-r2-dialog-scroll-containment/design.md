## Context

See `proposal.md` for motivation and the delta spec for the observable contract. Formal r1 V30 is currently active and its Android command-owned Back behavior has passed user acceptance. The new defect is independent: the custom overlay is fixed above Emby, its card already clips content, but its body only declares `overflow:auto`; overlay, card, and body all retain the default vertical overscroll behavior.

Read-only live topology measurements explain the entry-dependent symptom. On a Series detail page, the active Emby page was a vertical scroller with `scrollHeight/clientHeight` 2079/919 while the just-opened Smart Match body was short at 115/115. On the media-library control path, the host page was 919/919 while the populated Smart Match body was the active scroller at 1004/566. Both paths use the same `closeMenu` then synchronous `openDialog` flow and the same overlay; only the host scroller topology differs. The plugin has no touchmove/wheel handler and does not write host scroll state. This supports native scroll chaining as the primary cause and does not justify changing action-sheet timing.

The r2 worktree is seeded byte-for-byte from the current 13-file formal r1 V30 state, but remains on a separate branch. The already-created `openspec/changes/release-2-0-6-r2-dialog-scroll-containment/` directory is a planning-only r2 delta and is not one of those 13 seed paths. The baseline manifest shall record the complete porcelain state, classify that directory separately, and compute the seed digest over only the 13 inherited r1 paths. After the manifest is created, the formal r1 checkout, artifacts, OpenSpec, branch/index, and rollback evidence must be proved unchanged. r1 artifacts, verification, OpenSpec, and rollback assets are historical evidence and must not be rewritten by r2 packaging.

## Goals / Non-Goals

**Goals:**

- Establish a complete scroll boundary inside every Smart Match overlay while retaining native internal-body scrolling.
- Use one modal rule for all entry types and host page topologies.
- Preserve r1 V30 command ownership, zero-history behavior, parent/child logical viewport restoration, safe-area layout, and server-authored ordering.
- Produce independently identifiable, reviewable, deployable, and directly reversible 2.0.6r2 artifacts.

**Non-Goals:**

- Owning document/window touch, pointer, or wheel input.
- Locking, inspecting, snapshotting, restoring, or repeatedly correcting any Emby host scroller.
- Changing action-sheet close timing, overlay insertion timing, Android platform detection, or Back behavior.
- Adding an unsupported-WebView touch cancellation fallback in the same release.
- Changing matching, providers, candidate participation/order, binding, download, metadata, API, DTO, persistence, or mapping protocol V22.

## Decisions

### 1. Terminate the scroll chain with overlay-local CSS

Extend the existing generated Smart Match stylesheet as one atomic modal policy:

```css
.danmuSmartOverlay {
    overflow: hidden;
    overscroll-behavior-y: contain;
}

.danmuSmartCard {
    overscroll-behavior-y: contain;
}

.danmuSmartBody {
    flex: 1 1 auto;
    min-height: 0;
    overflow: auto;
    overscroll-behavior-y: contain;
}
```

The body becomes the explicit shrinkable flex scroll owner. Its vertical boundary consumes remaining pan/wheel delta; the already-clipping card and newly clipping overlay provide vertical containment for short content and input beginning on the header, footer, card margin, or backdrop. The policy is deliberately Y-axis-only so r2 does not alter horizontal touchpad, edge-gesture, or control behavior. Existing dimensions, safe-area rules, padding, colors, and responsive behavior remain unchanged.

This is preferred over `touch-action:none` because the latter would suppress native body panning and can interfere with fields and controls. It is preferred over `html/body` locking or Emby scroller lookup because those couple the plugin to private host topology and recreate the host-state risks r1 removed. It is preferred over reusing Emby's internal dialog helper because Smart Match has its own navigation, busy/protected, and Android command lifecycle and must not acquire a second dialog owner.

Do not add `contain:strict` in r2: it is broader than the observed scroll-boundary need and could alter size/paint behavior. Do not add a touch listener as insurance. If the target WebView does not honor the CSS boundary, formal acceptance fails and deployment rolls back to r1; any narrowly scoped event fallback requires a separate design and tests.

### 2. Keep the policy entry-neutral and lifecycle-neutral

The style is attached to the shared overlay/card/body classes in `ensureStyles`, not to detail-page selectors, Android identity, item type, or menu provenance. Series, Season, Episode, Movie, nested candidate/source/version pages, busy views, and short error/empty views therefore receive the same boundary.

`closeMenu`, `runButtonWorkflow`, `openDialog`, command ownership, host-pop cleanup, navigation-context capture/restoration, and dialog disposal remain byte-for-byte behaviorally unchanged apart from the shared CSS. Removing the overlay automatically removes the input hit region; there is no listener or delayed cleanup to retain.

### 3. Verify static policy separately from real scroll-engine behavior

The JavaScript regression shall assert the exact required style declarations, a single shared overlay construction path, and absence of document/window touchmove/wheel handlers, `touch-action:none`, host scroll reads/writes, host scroller selectors, timers, or animation-frame restoration in the r2 slice. Existing complete frontend regression remains authoritative for dialog navigation, input, ordering, command Back, and lifecycle behavior.

Because the fake DOM does not implement browser-native scroll chaining, live acceptance is required rather than claiming CSS behavior from a synthetic event alone. Desktop browser and both real Android targets shall record the dialog body and applicable host scroll positions before/after short-content, body-middle, top-boundary, bottom-boundary, header/footer/card-margin/backdrop, secondary-return, and post-close gestures. Record `CSS.supports("overscroll-behavior-y", "contain")` and the three computed Y-axis styles as diagnostic evidence, but treat actual scroll-position behavior as the acceptance gate. The media-library card path remains a required control even though it did not reproduce r1.

### 4. Give r2 an independent version and artifact identity

- Assembly: `2.0.6.0`.
- File: `2.0.6.2`.
- Product/informational/configuration/TMDB User-Agent: `2.0.6r2`.
- Generated configuration cache token: `2-0-6r2`.
- Frontend install marker: V31 exactly once; V30 and all Car probe markers zero in the formal r2 asset.
- Mapping protocol: V22 unchanged.

Create only `artifacts/2.0.6r2` and its five-file review package: versioned DLL, matching V31 JavaScript, stable checksum manifest, cumulative UPDATE, and verification record. r1 files are read-only inputs whose pre-r2 inventory must be rechecked before and after packaging.

## Risks / Trade-offs

- [A target Android WebView ignores or incompletely implements overscroll containment] → Treat phone or car-head-unit movement as acceptance failure, restore the paired r1 predecessor, and design an explicitly reviewed overlay-local event fallback separately; do not silently add one to r2.
- [Overlay clipping changes backdrop or safe-area behavior] → Retain all existing dimensions and mobile media rules; test phone/car portrait and car-head-unit wide layouts, header/footer/card-margin/backdrop input, fields, candidate selection, and safe areas.
- [Body flex sizing changes long-page geometry or sticky summaries] → Add `flex:1 1 auto` and `min-height:0` together, retain existing padding/overflow, and run long Series/composite/progress plus nested Episode/Movie fixtures.
- [Containment traps input after close] → Use CSS only with no persistent input listener; verify host scroll immediately after X, Escape, Android command close, force close, and host-pop disposal.
- [r2 packaging rewrites r1 evidence] → Work only in the isolated r2 worktree, write a new r2 artifact tree, compare the frozen r1 13-file inventory and package hashes before/after, and fail the gate on any drift.

## Migration Plan

1. Preserve the isolated r2 seed inventory proving byte identity with the current formal r1 V30 worktree; do not modify the r1 checkout.
2. Implement the shared CSS policy, V31, and complete r2 version identity; update only cumulative top-level documentation and new r2 artifacts.
3. Run frontend syntax/full regression, relevant backend/configuration/version tests, clean sequential Release build, strict OpenSpec, diff/allowlist/privacy checks, package pairing/hash/CodeView checks, and Sol high-reasoning review.
4. Before live replacement, create and re-read a paired backup of the active r1 V30 frontend configuration, r1 DLL, and plugin configuration with hashes, owner, and mode; retain all earlier rollback sets.
5. Deploy the reviewed r2 DLL and V31 asset together, restart Emby, require local/external HTTP 200, and read back exact deployed hashes and marker counts.
6. Run bounded desktop, phone, and car-head-unit acceptance without selecting download/bind/metadata mutations. If any background scroll, input, command Back, route, or health criterion fails, immediately restore the paired r1 predecessor and recheck HTTP 200.
7. Keep commit, push, merge, tag, PR, and publication as separate explicit approval gates.
