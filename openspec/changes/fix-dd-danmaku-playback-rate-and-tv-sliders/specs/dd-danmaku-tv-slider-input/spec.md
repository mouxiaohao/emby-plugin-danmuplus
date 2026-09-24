## Purpose

Make dd-danmaku's visible range settings operable with a television directional pad while preserving the same values, callbacks, focus, and desktop interaction behavior used by pointer and keyboard clients.

## ADDED Requirements

### Requirement: Directional adjustment of every visible settings slider
Every visible horizontal range setting created by dd-danmaku's shared slider control MUST accept TV remote Left and Right actions while focused. Left SHALL decrement and Right SHALL increment by exactly the slider's declared step, with the resulting value clamped to its declared minimum and maximum. Controls explicitly documented as unavailable to controller input inside the embedded-webpage area are outside this requirement.

#### Scenario: Adjust an integer-step slider
- **WHEN** a focused TV slider has value 2, step 1, and receives one Right action
- **THEN** its value becomes 3 and not any other value

#### Scenario: Adjust a fractional-step slider
- **WHEN** a focused TV slider has value 0.9, step 0.1, and receives one Right action
- **THEN** its value becomes the valid normalized step value 1.0 without a floating-point display artifact

#### Scenario: Clamp at an endpoint
- **WHEN** a focused slider is at its minimum and receives Left, or is at its maximum and receives Right
- **THEN** its value remains at the endpoint, focus remains on that slider, and no input, commit, persistence, or reload callback is produced

#### Scenario: Use all visible settings groups
- **WHEN** the user focuses any visible slider in the main settings, font-style, advanced-filter, progress-chart, or Bangumi settings groups on a TV client
- **THEN** the applicable directional actions adjust that slider by its own declared step

### Requirement: One adjustment and one commit per changed remote action
A handled directional action that changes the value MUST cause exactly one value step and one logical setting commit. A handled outward action at an unchanged boundary MUST be consumed with zero value steps and zero commits. The script MUST prevent native range behavior, Emby navigation, and input-manager routing from applying a duplicate step for the same physical action. Repeated key events from a held remote button SHALL each apply at most one additional step until the declared boundary is reached.

#### Scenario: Single key press is not doubled
- **WHEN** a TV platform exposes the same physical Right press through both DOM range behavior and Emby input routing
- **THEN** the slider advances by one declared step and the setting callback is committed once

#### Scenario: Long press repeats deterministically
- **WHEN** a focused slider receives a sequence of repeated Right key actions from a held remote button
- **THEN** each delivered repeat advances at most one step, never exceeds the maximum, and does not trigger an additional navigation action

### Requirement: Slider feedback, persistence, and focus continuity
Each remote adjustment MUST update the visible value label immediately and MUST invoke the same persistence and reload policy as an equivalent pointer adjustment. The focused slider MUST retain focus after the commit, including when that setting reloads danmaku content, so the next directional action continues adjusting the same value.

#### Scenario: Remote adjustment updates a live label
- **WHEN** the user changes a focused slider with the directional pad
- **THEN** the displayed value reflects the normalized new value during that action and the saved setting contains the same value

#### Scenario: Adjustment invokes an existing reload policy
- **WHEN** a remotely adjusted slider is defined to reload danmaku after a committed value change
- **THEN** it performs one such reload and returns focus to the same slider without closing or navigating the settings dialog

#### Scenario: Slider does not require reload
- **WHEN** a remotely adjusted slider is defined not to reload danmaku
- **THEN** its value and label are committed without introducing a new reload

### Requirement: Existing non-TV interactions remain compatible
The directional-pad support MUST preserve existing mouse, touch, drag, native keyboard, and Emby Theater slider behavior. Unrelated direction keys MUST remain available for normal settings-dialog focus navigation, and a handled slider key MUST NOT activate another control or close the dialog.

#### Scenario: Pointer interaction remains unchanged
- **WHEN** a desktop or touch user drags or taps a slider
- **THEN** the slider retains its existing continuous feedback and commit behavior

#### Scenario: Navigate between horizontal slider rows
- **WHEN** a horizontal slider has focus and the user presses Up or Down
- **THEN** dd-danmaku does not treat that action as a horizontal value change and the host may perform its normal focus navigation
