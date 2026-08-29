## Why

The injected Smart Match dialog is usable with pointer and touch input, but its custom controls do not participate in Emby's television focus system and the dialog has no D-pad navigation owner of its own. On television clients, a remote therefore cannot reliably reach, identify, or activate Smart Match controls even though the same workflow works in a desktop browser.

## What Changes

- Add dialog-local television remote navigation for the four directional keys and the confirm/OK key across Smart Match header, body, full-width candidate selection rows, search fields, disclosure controls, and footer actions.
- Give every newly rendered dialog surface a deterministic initial or continuity focus target, keep the active target visible inside the dialog body, contain focus within the topmost connected Smart Match overlay, and align the dialog body to its top when an upward move reaches the header close control.
- Add an unmistakable high-contrast focus indicator without changing pointer hover, touch selection, disabled-control behavior, or native text editing.
- When a user explicitly rematches an already mapped temporary Season, invalidate that temporary Season and every later temporary Season in the same physical Season, rebuild them as the authoritative trailing unmatched remainder, and preserve the exact prior draft if the user returns or rebuilding fails. Ordinary per-run removal remains non-cascading.
- Preserve the existing Android command-owned Back path, desktop Escape behavior, parent/child scroll restoration, server-authored order, and all matching, binding, download, retry, and metadata semantics.
- Add deterministic focus-geometry, candidate-row activation, header-boundary scrolling, suffix-rematch rollback, key-routing, rerender, cleanup, and non-regression coverage plus live 1280x720 television-style validation against Emby 4.9.5.0.
- Non-goals: intercepting Emby navigation outside Smart Match, depending on private Emby focus-manager APIs, synthesizing mouse input, changing backend APIs/protocol/persistence or unattended matching, cascading the ordinary Remove action, or treating viewport width or touch support alone as proof of a television client.

## Capabilities

### New Capabilities

- `smart-match-tv-remote-navigation`: Defines topmost-dialog D-pad focus movement, full-row candidate activation, header-boundary scroll alignment, confirm activation, render-time focus continuity, visible focus, input exceptions, focus containment, cleanup, and interactive temporary-Season suffix rematching.

### Modified Capabilities

- None.

## Impact

- Primary implementation and regression surface: `Frontend/DanmuSmartMatch.CustomCssJS.js` and `Frontend/DanmuSmartMatch.RegressionTests.js`.
- Release identity/documentation and deployable review assets advance from 2.0.7r2/V34 to 2.0.7r3/V36 while retaining mapping protocol V22 and the 2.0.7r2 backend; V36 explicitly invalidates the already deployed early V35 closure so long-lived television pages load the follow-up fixes.
- Deployment changes only the named Smart Match entry inside the existing Emby.CustomCssJS configuration; unrelated CustomCssJS entries and plugin configuration remain byte-for-byte preserved.
- No public server API, persisted mapping schema, provider behavior, library-import matching path, or automatic download policy changes; the only mapping behavior change is the explicitly initiated browser rematch draft pruning later temporary Seasons before the existing authoritative preview request.
