## Context

See `proposal.md` for motivation and `specs/smart-match-dialog-interaction/spec.md` for observable requirements. The deployed system has been rolled back to a verified r6 binary/browser pair. The current working tree contains later uncommitted r7/r8 experiments, while the archived r6 candidate contains a DLL, browser script, and verification record but no rebuildable C# source snapshot.

## Goals / Non-Goals

**Goals:**

- Establish a source baseline that is demonstrably equivalent to the verified r6 candidate before implementation begins.
- Add only normalized Chinese origin/decision labels and state-safe dialog dismissal.
- Preserve all r6 backend, menu, matching, download, persistence, and automatic-import behavior.
- Produce paired `2.0.2r1` assets with deterministic and live verification.

**Non-Goals:**

- Building from the current mixed r7/r8 working tree.
- Porting segment, collection, temporary-Season, or other later-version contracts.
- Changing matching scores, candidate ordering, provider logic, download behavior, or metadata persistence.

## Decisions

### Require a rebuildable r6 source baseline

Implementation starts only after locating or restoring the exact r6 source commit/source package in an isolated worktree. The archived r6 frontend script is the browser-script reference and the archived DLL/configuration hashes are the behavioral/deployment reference. If no r6 source snapshot exists, implementation pauses for user-provided source rather than treating r5 or the mixed r7/r8 tree as equivalent.

This is preferred over selectively deleting later code because the current dirty tree contains cross-cutting backend and frontend experiments whose complete provenance cannot be proven. It is also preferred over relabelling the existing r6 DLL because a `2.0.2r1` release must be reproducible from source and report the requested version.

### Normalize codes before fixed Chinese mapping

Origin and decision helpers trim and case-normalize non-empty machine codes before lookup. Known codes receive fixed Chinese labels. Unknown non-empty values receive `未知匹配来源` or `未知决策`; an optional raw value may appear only in secondary diagnostic text. Empty values produce no fragment.

The helpers remain presentation-only. Provider-id recognition, selected candidates, request payloads, and `重新智能匹配` behavior continue using the original wire values.

### Centralize idempotent dialog disposal

The backdrop has no close handler. The top-right action and document-level `Escape` listener both call normal `close`, which checks `dialog.closable`. The existing `forceClose` bypasses that check for the explicit background workflow. Both routes share an idempotent disposer that removes the overlay and unregisters the keyboard listener.

The Escape handler consumes the event only when it closes the topmost applicable dialog. This prevents one keypress from cascading through stacked dialogs. Global click suppression is rejected because it could interfere with Emby's surrounding UI.

### Keep release identifiers unambiguous

The user-facing plugin version is exactly `2.0.2r1`; the numeric file version uses `2.0.2.1`, and the frontend installation marker advances from r6 V10 to V12 so it cannot collide with the abandoned V11 experiment. Documentation and artifact directory use the same `2.0.2r1` spelling.

## Risks / Trade-offs

- [Exact r6 source is unavailable] → Stop implementation and request the corresponding source commit or archive; never build from the contaminated working tree.
- [Escape bypasses protected work] → Route Escape through normal `close`, never `forceClose`, and test both closable states.
- [Keyboard listeners leak or cascade] → Use idempotent shared disposal and a topmost-dialog guard with repeated-open/close regressions.
- [Unknown backend codes expose English] → Assert Chinese primary fallback and restrict raw values to diagnostics.
- [Later-version functionality enters the release] → Diff against the r6 reference, scan for r7/r8 segment/collection symbols, and review the candidate file list before packaging.

## Migration Plan

1. Recover the exact r6 source in an isolated worktree and verify its generated DLL/frontend behavior against the archived r6 references.
2. Apply only the scoped frontend changes, version markers, tests, and documentation.
3. Run frontend regressions, backend regression executable, Release build, strict OpenSpec validation, and forbidden-symbol/diff review.
4. Back up the currently deployed r6 DLL and both plugin configurations, deploy the paired `2.0.2r1` DLL/browser configuration, restart Emby, and live-test Chinese labels plus backdrop/×/Escape behavior without downloading or modifying metadata.
5. Roll back by restoring the paired pre-deployment r6 backup if any check fails.
