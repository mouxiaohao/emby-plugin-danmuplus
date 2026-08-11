## Purpose

Defines understandable Chinese smart-match explanations and deliberate, state-safe dismissal behavior for the injected Emby smart-match dialog.

## ADDED Requirements

### Requirement: Match source and decision are displayed in Chinese
The frontend SHALL translate every known backend match-origin and decision-reason code into a Chinese primary label without changing the wire code, backend selection, candidate order, or rematch request.

#### Scenario: Known match origin is displayed
- **WHEN** the backend returns a known origin including `provider-id`, `binding`, `scored`, or `manual`, regardless of surrounding whitespace or letter case
- **THEN** the dialog SHALL display the configured Chinese source label

#### Scenario: Known decision reason is displayed
- **WHEN** the backend returns a known provider-id, binding, site-priority, unresolved-provider, no-candidate, low-confidence, or manual decision code
- **THEN** the dialog SHALL display the configured Chinese decision explanation

#### Scenario: Unknown non-empty code is displayed
- **WHEN** the backend returns an origin or decision code not known to the installed frontend
- **THEN** the primary label SHALL use a generic Chinese fallback and MUST NOT expose the raw English machine code as the primary explanation

#### Scenario: Empty explanation is returned
- **WHEN** the backend omits an origin or decision value
- **THEN** the dialog SHALL omit that explanation fragment rather than displaying an unknown-value warning

#### Scenario: Provider-identifier result remains actionable
- **WHEN** a provider-identifier match is displayed with its Chinese source and decision labels
- **THEN** the existing `重新智能匹配` action and provider-identifier detection SHALL behave exactly as in r6

### Requirement: Backdrop clicks never dismiss the dialog
Clicking the dialog backdrop or another non-dialog area SHALL NOT close the smart-match dialog, regardless of whether the dialog is currently closable.

#### Scenario: Closable dialog backdrop is clicked
- **WHEN** a user clicks the backdrop while the dialog is closable
- **THEN** the dialog SHALL remain open with its current selections and state intact

#### Scenario: Protected dialog backdrop is clicked
- **WHEN** a user clicks the backdrop while protected download work makes the dialog non-closable
- **THEN** the dialog SHALL remain open and MUST NOT cancel, stop, or alter the task

### Requirement: Close action and Escape respect protected state
The top-right close action and `Escape` SHALL be the only ordinary dismissal controls and SHALL close only the topmost smart-match dialog whose state is closable. Candidate and overview footers SHALL NOT expose an additional cancel/dismiss action. Forced background dismissal SHALL remain available to the existing explicit workflow and SHALL clean up all dialog event listeners.

#### Scenario: Closable candidate or overview page is shown
- **WHEN** a smart-match candidate or overview page renders its footer
- **THEN** the footer SHALL NOT contain a cancel/dismiss button
- **AND** the user SHALL close the dialog with the top-right close action or `Escape`

#### Scenario: Close action is used while closable
- **WHEN** the dialog is closable and the user activates the top-right close action
- **THEN** the dialog SHALL close and unregister its keyboard listener

#### Scenario: Escape is pressed while closable
- **WHEN** the topmost smart-match dialog is closable and the user presses `Escape`
- **THEN** that dialog SHALL close and no underlying dialog SHALL also close from the same key event

#### Scenario: Protected dialog receives close input
- **WHEN** the dialog is non-closable and the user activates the close action or presses `Escape`
- **THEN** the dialog SHALL remain open and the protected task state SHALL remain unchanged

#### Scenario: Existing workflow forces background dismissal
- **WHEN** the existing workflow explicitly moves protected work to the background through its force-close path
- **THEN** the dialog SHALL close even if normal close is disabled and SHALL unregister its keyboard listener

#### Scenario: Dialog cleanup repeats
- **WHEN** close or force-close cleanup is invoked more than once
- **THEN** cleanup SHALL be idempotent and MUST NOT raise an error or leave an active Escape listener

### Requirement: Release scope remains compatible with r6
Version `2.0.2r1` SHALL preserve the verified r6 backend contracts and runtime behavior except for the specified frontend labels and dismissal behavior.

#### Scenario: Candidate and download workflows run after upgrade
- **WHEN** a Movie, Series, Season, or Episode smart-match workflow runs on `2.0.2r1`
- **THEN** r6 matching, rematch, binding, progress, cancellation, retry, provider-identifier persistence, and automatic-import behavior SHALL remain unchanged

#### Scenario: Candidate package is inspected
- **WHEN** the `2.0.2r1` source and paired artifacts are reviewed
- **THEN** they MUST NOT contain unfinished r7/r8 segment, collection, or temporary-Season functionality
