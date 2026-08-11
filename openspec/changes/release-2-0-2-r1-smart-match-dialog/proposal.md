## Why

The r6 smart-match dialog exposes backend origin and decision codes as English machine values and can lose the user's current context when a backdrop click closes it. Version `2.0.2r1` should make these explanations understandable in Chinese and make dialog dismissal intentional without changing matching or download policy.

## What Changes

- Translate every known smart-match origin and decision-reason code into a Chinese primary label while retaining the original wire code only for secondary diagnostics.
- Use a generic Chinese fallback for unknown future codes instead of presenting unexplained English as the main UI text.
- Ignore clicks on the dialog backdrop or any non-dialog area.
- Allow the top-right close action and `Escape` to close the dialog only when its current state is closable; active-download protection remains unchanged.
- Release the paired plugin/browser assets as version `2.0.2r1` from the verified r6 baseline.
- Explicit non-goals: changing backend matching/scoring, candidate order, provider identifiers, download persistence, Season segmentation, automatic import behavior, or introducing any unfinished r7/r8 functionality.

## Capabilities

### New Capabilities

- `smart-match-dialog-interaction`: Defines Chinese match explanations and intentional close behavior for the smart-match dialog.

### Modified Capabilities

None.

## Impact

- Frontend: `Frontend/DanmuSmartMatch.CustomCssJS.js` label mapping, dialog event handling, and listener cleanup.
- Tests: frontend deterministic regressions for known/unknown translations, backdrop clicks, close button, Escape, closable state, and listener disposal.
- Release: version/configuration markers, README, paired artifact packaging, backed-up Synology deployment, and live Emby verification.
- Backend APIs and matching contracts remain unchanged from r6.
