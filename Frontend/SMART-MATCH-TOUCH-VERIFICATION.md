# Smart Match V37 touch-focus follow-up

Date: 2026-09-27. Scope: frontend-only correction on develop; V22 protocol and the installed 2.0.7r3 DLL remain unchanged. Existing release tags/assets are not replaced.

## Cause and correction

V36 unconditionally enabled remote input during programmatic focus, and its high-contrast focus-within/focus-visible rules applied without an input-mode guard. Consequently touchscreen focus and subsequent rerenders could paint television rings.

V37 gates every television ring on `.danmuRemoteInput`. Direction/Tab and non-editable confirm/Space enable that mode; pointer/touch clears it across the active dialog stack. Initial focus, rerender and parent-return focus no longer infer input modality. Phone IME Enter/Space in an editable field preserves touch mode. Native focus, choice activation, geometry, scroll ownership and matching requests are unchanged.

## Verification

- Added a failing pre-fix entry-focus regression, then passed the full Smart Match regression suite and both JavaScript syntax checks.
- Regression covers pointerdown/touchstart/mousedown, focusin, rerender, parent-style restoration, nested touch handoff, interior Tab, directional edges, phone IME submission, and every high-contrast CSS selector.
- Actual browser computed-style fixture: nine checks passed for initial/touch focus, D-pad visibility, removal of ancestor and target rings, rerender, Tab, parent restoration, edge recovery, and final touch removal. Default `outline-width: medium` without an outline style is correctly treated as invisible.
- Existing backend regression passed. Sequential Release build: 0 errors, 131 existing warnings. Strict OpenSpec validation and diff whitespace checks passed.
- Authenticated deployed Emby web acceptance: real whole-Series preview loaded without rings; Down enabled the target ring; pointer entry into temporary-Season rematching, candidate completion and return to the original overview retained pointer mode with zero television rings. Returned without applying the draft or starting downloads.
- Final-deployment single-Season acceptance: completed preview had zero television rings, Down produced the expected white 3px target outline, pointer checkbox interaction cleared all rings, the local checkbox draft was restored, and closing removed the overlay. No download or binding was submitted.
- In-app browser does not support low-level touch injection. Browser synthetic touch events and deterministic event regressions are not physical Android touchscreen or television-remote acceptance.

## Deployment integrity

Only the enabled named Smart Match CustomCssJS content was replaced. Full pre-change CustomCssJS, DLL and plugin configuration backups are retained on the NAS. Independent readback proved exact staged/live XML equality, source/live equality after BOM and newline normalization, and unchanged bytes outside the target component, including dd-danmaku.

- Final CustomCssJS XML SHA-256: `52427d67de2e90bf80580068bc0964f2534f9c52a79988d8436b769da00bf022`.
- Normalized V37 script SHA-256: `0fa4eab8237791392d5dc0b7f96796c59255c0eadce5e194bccb36683d6eb37b`.
- One V37 marker; no V36 marker in the active component. XML permissions/ownership preserved.
- Emby restarted with a new PID, HTTP 200, version 4.9.5.0, one Danmu plugin load and zero related startup errors. DLL and plugin-configuration hashes unchanged.

Completely exit and reopen the client (or fully reload the web page) before checking the new behavior; already-loaded closures/styles are not hot-replaced.
