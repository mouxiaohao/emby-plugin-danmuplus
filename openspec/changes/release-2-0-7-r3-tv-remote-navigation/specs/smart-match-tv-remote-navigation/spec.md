## Purpose

Defines predictable, visible, and dialog-contained Smart Match operation with television D-pad remotes while preserving native pointer, touch, text-entry, selection, Back, and Emby host behavior.

## ADDED Requirements

### Requirement: The topmost Smart Match dialog owns remote focus
While a Smart Match overlay is connected, its topmost dialog SHALL keep keyboard focus on an enabled, visible native control inside that overlay. Opening the dialog or replacing its current surface SHALL establish a deterministic focus target, and handled remote input MUST NOT move or activate the underlying Emby page. An underlying Smart Match dialog MUST NOT react while another Smart Match dialog is above it.

#### Scenario: Smart Match opens without an existing dialog focus
- **WHEN** Smart Match opens while focus belongs to the Emby page
- **THEN** focus SHALL move to a deterministic enabled control in the new overlay
- **AND** the focused control SHALL have a visible television focus indicator

#### Scenario: A nested Smart Match dialog is present
- **WHEN** a directional or confirm key is pressed while two Smart Match overlays are connected
- **THEN** only the topmost connected dialog SHALL process that key
- **AND** the underlying dialog and Emby page SHALL remain unchanged

#### Scenario: Focus is externally lost while the dialog remains open
- **WHEN** a directional key is pressed and the active element is absent, detached, or outside the topmost Smart Match overlay
- **THEN** the dialog SHALL recover focus to its deterministic entry target before any underlying Emby navigation occurs

### Requirement: D-pad directions move focus spatially and deterministically
The four standard directional keys SHALL move focus among enabled, visible Smart Match buttons, text fields, full-width candidate selection proxies, checkboxes, numeric fields, links, and disclosure summaries according to their rendered geometry. Each candidate proxy SHALL represent exactly one contained native radio and expose the complete rendered candidate-card rectangle to spatial scoring so a vertical beam cannot skip the candidate column for an aligned footer action. A candidate must lie in the requested half-plane; candidates whose projected bounds overlap the current control on the cross axis SHALL take priority, then the nearest primary-axis candidate with deterministic cross-axis and document-order tie breaking SHALL win. If no candidate exists in that direction, focus SHALL remain on the current control. Direction handling MUST NOT reorder candidates, change a selection, activate an action, or wrap across an edge.

#### Scenario: User moves vertically through candidate rows
- **WHEN** focus is above or within a candidate list and the user presses Down or Up
- **THEN** the full-width proxy for the nearest candidate card SHALL participate in the spatial beam and receive focus in server-authored order
- **AND** no candidate SHALL become selected until the user confirms it

#### Scenario: User moves horizontally through footer actions
- **WHEN** focus is on one footer action and another enabled footer action is rendered to its left or right
- **THEN** Left or Right SHALL move focus to the nearest action in that direction

#### Scenario: Directional edge has no eligible target
- **WHEN** the user presses a direction for which no enabled visible control lies in the requested half-plane
- **THEN** focus SHALL remain on the current control
- **AND** the underlying Emby page SHALL NOT receive the handled direction

#### Scenario: Disabled or hidden controls exist
- **WHEN** spatial navigation evaluates a disabled, hidden, disconnected, `aria-hidden`, or negative-tab-index control
- **THEN** that control MUST NOT be selected as a focus target

### Requirement: Confirm activates native controls exactly once
The television confirm/OK key SHALL use the focused control's native activation semantics. A focused candidate proxy SHALL select its one contained native radio through one trusted-equivalent click, and directly focused radio buttons or checkboxes SHALL use the same one-click bridge because Enter alone does not activate those controls consistently across target WebViews. Buttons, search fields, disclosure summaries, and other native controls MUST NOT receive a duplicate synthesized activation. Pointer selection and keyboard Space on the candidate proxy SHALL retain one-selection semantics.

#### Scenario: Confirm is pressed on a radio candidate
- **WHEN** a focused unselected candidate proxy receives Enter, Numpad Enter, or the standard Select key
- **THEN** that candidate SHALL become selected through exactly one change/click activation
- **AND** no download, bind, or transition action SHALL run merely from selecting it

#### Scenario: Confirm is pressed on a button
- **WHEN** a focused Smart Match button receives the standard confirm key
- **THEN** its existing native click workflow SHALL run exactly once

#### Scenario: Confirm is pressed in a search field
- **WHEN** focus is in a Smart Match search field and the user presses Enter
- **THEN** the existing field-specific search handler SHALL run exactly once
- **AND** the dialog-level remote handler SHALL NOT synthesize a second click

### Requirement: Text editing and ordinary non-remote input remain usable
Horizontal arrows inside editable text or numeric fields SHALL retain their native caret/editing behavior. Up and Down SHALL allow the remote user to leave an editable field for the nearest dialog control. Pointer, touch, mouse, Space, ordinary Tab traversal, radio/checkbox clicks, and disclosure interaction SHALL retain their existing semantics. Tab and Shift+Tab SHALL be contained within and cycle through the topmost Smart Match overlay while it is connected.

#### Scenario: User edits a search keyword
- **WHEN** focus is in a search field and the user presses Left or Right
- **THEN** the browser SHALL retain native caret movement or text editing
- **AND** dialog focus SHALL NOT move to another control

#### Scenario: User leaves a search field with Down
- **WHEN** focus is in a search field and an eligible control is rendered below it
- **THEN** Down SHALL move focus to the nearest eligible control below

#### Scenario: Keyboard Tab reaches an overlay boundary
- **WHEN** Tab or Shift+Tab would leave the topmost connected Smart Match overlay
- **THEN** focus SHALL wrap to the opposite enabled control within that same overlay

#### Scenario: Pointer or touch selects a control
- **WHEN** the user activates a Smart Match control with pointer or touch input
- **THEN** its existing click, selection, scrolling, and hover behavior SHALL remain unchanged

### Requirement: Focus survives Smart Match surface transitions
Before a dialog surface is replaced, Smart Match SHALL capture a transient semantic focus identity without persisting media identifiers. After the replacement it SHALL restore an equivalent surviving control when possible, otherwise choose the first enabled control in the new body, then footer, then header. Parent return SHALL restore focus to the logical action or selection row associated with the existing navigation context. Focus movement SHALL reveal the control by scrolling only the Smart Match body and MUST NOT call a whole-page reveal that can move an Emby host scroller.

#### Scenario: Candidate results rerender after a search
- **WHEN** a search replaces the current candidate surface and an equivalent search or candidate control survives
- **THEN** focus SHALL return to the equivalent control
- **AND** the server-authored candidate order SHALL remain unchanged

#### Scenario: User enters and returns from a child picker
- **WHEN** a focused candidate action opens a source, part, Season, or temporary-range child picker and the user returns
- **THEN** focus SHALL return to the logical parent action or its selection row
- **AND** the existing semantic parent viewport restoration SHALL remain intact

#### Scenario: Focus moves to an off-screen dialog control
- **WHEN** D-pad movement selects a control outside the visible portion of the Smart Match body
- **THEN** only the Smart Match body SHALL scroll by the minimum amount needed to reveal it
- **AND** every underlying Emby scroll position SHALL remain unchanged

#### Scenario: Up reaches the header close control
- **WHEN** the Smart Match body is scrolled and an upward D-pad move reaches the header X close control
- **THEN** the Smart Match body SHALL be aligned to scroll position zero
- **AND** the Emby host page and every parent navigation context SHALL remain unchanged

#### Scenario: Busy content has no enabled action
- **WHEN** a protected busy surface contains no enabled focus target
- **THEN** Smart Match SHALL retain the surface without focusing an ineffective protected close control
- **AND** the next directional input after an enabled action appears SHALL recover to that action

### Requirement: Television focus is visually unambiguous
The focused Smart Match control and the containing candidate or option row SHALL expose a high-contrast outline or focus-within treatment distinguishable from hover, checked, matched, warning, and disabled states. The indication SHALL remain visible at television viewing distance and MUST NOT alter layout geometry or hide existing state colors.

#### Scenario: A candidate row has remote focus
- **WHEN** D-pad navigation focuses a candidate selection proxy
- **THEN** the complete candidate row SHALL display a high-contrast focus treatment
- **AND** its checked and match-state presentation SHALL remain independently readable

#### Scenario: A footer button has remote focus
- **WHEN** D-pad navigation focuses an enabled footer button
- **THEN** that button SHALL display a high-contrast focus ring without changing footer geometry

#### Scenario: Input mode changes to pointer
- **WHEN** pointer or touch input occurs after remote navigation
- **THEN** the remote-only focus marker SHALL be cleared
- **AND** ordinary browser focus-visible and hover behavior SHALL remain available

### Requirement: Remote support preserves dialog and matching boundaries
The remote-navigation layer SHALL reuse the existing close, Escape, Android command-owned Back, host-pop cleanup, protected-state, parent/child navigation, cancellation, retry, binding, download, and metadata workflows. It MUST NOT add dialog history, a Smart Match `backbutton` listener, host-scroller ownership, private Emby focus-manager dependencies, responsive-width television detection, server requests, stored focus state, or mapping protocol changes. Closing or disposing a dialog SHALL remove its per-dialog remote listeners and transient focus state exactly once.

#### Scenario: Android remote Back is pressed
- **WHEN** the target Android client emits Emby's cancelable `command: "back"` while a Smart Match dialog is connected
- **THEN** the existing command-owned parent return, top-level close, or busy/protected retention behavior SHALL run unchanged
- **AND** the directional focus handler SHALL NOT create a second Back path

#### Scenario: Dialog is disposed
- **WHEN** Smart Match closes by X, Escape, Android command Back, force-close, or host navigation
- **THEN** all per-dialog direction, Tab, pointer-mode, and focus listeners SHALL be removed idempotently
- **AND** subsequent remote input SHALL belong to Emby

#### Scenario: Automatic library-import matching runs
- **WHEN** matching runs without opening a Smart Match browser dialog
- **THEN** no remote-navigation code SHALL execute or alter matching, mapping, binding, download, retry, or metadata behavior

### Requirement: Rematching a temporary Season rebuilds its trailing remainder
When a user explicitly chooses Rematch for an already mapped temporary Season inside one physical Season, Smart Match SHALL snapshot the complete current interactive draft, derive the eligible local-episode suffix beginning at that temporary Season, remove every authoritative or manual selection overlapping that suffix from the outgoing preview, and exclude all suffix item identifiers before requesting the existing authoritative composite plan. Every earlier temporary Season SHALL remain selected. The ordinary Remove action SHALL continue to affect only its clicked run.

#### Scenario: First of several mapped temporary Seasons is rematched
- **GIVEN** one physical Season is represented by mapped temporary Seasons 1, 2, and 3 in local episode order
- **WHEN** the user chooses Rematch on temporary Season 1
- **THEN** the authoritative preview request SHALL retain no selection from temporary Seasons 1, 2, or 3
- **AND** the exact combined local suffix SHALL be submitted as exclusions and reopened as one unmatched temporary range

#### Scenario: A middle temporary Season is rematched
- **GIVEN** one physical Season is represented by mapped temporary Seasons 1, 2, and 3 in local episode order
- **WHEN** the user chooses Rematch on temporary Season 2
- **THEN** temporary Season 1 SHALL remain mapped
- **AND** temporary Seasons 2 and 3 SHALL return to the trailing unmatched remainder available to the picker

#### Scenario: Suffix rematch is cancelled or rejected
- **WHEN** the user returns from the suffix picker without applying a replacement, or the authoritative rebuild/search fails
- **THEN** the exact pre-rematch Season, selections, exclusions, removed-run snapshots, keywords, candidates, details, focus context, and scroll context SHALL be restored

#### Scenario: Replacement covers only part of the rebuilt suffix
- **WHEN** the user applies a valid replacement whose requested count covers fewer episodes than the rebuilt suffix
- **THEN** that replacement SHALL become the mapped leading part of the suffix
- **AND** every later episode SHALL remain in the authoritative unmatched remainder without restoring the superseded later temporary-Season selections

#### Scenario: Release compatibility is inspected
- **WHEN** the 2.0.7r3 package is compared with 2.0.7r2
- **THEN** mapping protocol V22, the 2.0.7r2 backend behavior, saved manual bindings, provider behavior, and unattended matching policies SHALL remain compatible
- **AND** only explicitly initiated temporary-Season rematching SHALL prune later browser-draft selections before the existing preview endpoint is called
- **AND** the final V36 installation marker SHALL prevent an already loaded early V35 closure from suppressing the follow-up frontend
