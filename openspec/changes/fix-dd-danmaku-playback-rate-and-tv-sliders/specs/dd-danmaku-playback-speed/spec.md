## Purpose

Provide an optional dd-danmaku playback mode in which scrolling comments retain the user's configured physical screen speed while their appearance and transport behavior remain synchronized to the active Emby media timeline.

## ADDED Requirements

### Requirement: Persisted opt-in fixed scrolling speed
The script SHALL expose a per-client setting for keeping scrolling danmaku at the configured 1x wall-clock speed. A client with no saved value MUST default this setting to disabled, MUST preserve the existing rate-coupled behavior while disabled, and MUST include the setting in the existing settings persistence, export, and import flows without changing any previously saved value.

#### Scenario: Existing client opens the revised script
- **WHEN** a client has existing dd-danmaku settings but no saved fixed-speed setting
- **THEN** the new setting is shown as disabled and all prior settings and rate-coupled playback behavior remain unchanged

#### Scenario: User persists the fixed-speed choice
- **WHEN** the user enables or disables the fixed-speed setting and later reopens the player on the same client
- **THEN** the script restores that explicit choice together with the user's existing base-speed setting

#### Scenario: User imports and exports settings
- **WHEN** the user exports settings with an explicit fixed-speed value and imports that settings payload on a compatible client
- **THEN** the imported fixed-speed value is applied without discarding unrelated dd-danmaku settings

#### Scenario: Saved request meets an incompatible custom engine
- **WHEN** the saved fixed-speed request is true but the explicitly configured engine does not advertise the required timing capability
- **THEN** the script retains and displays the saved request without rewriting it, keeps the effective runtime in legacy mode with a clear inactive diagnostic, and applies the saved request automatically when a compatible engine is available at a later engine creation

### Requirement: Wall-clock-stable scrolling with media-time scheduling
While fixed-speed mode is enabled, right-to-left and left-to-right comments MUST move at the same wall-clock screen speed selected by the user at 1x playback for every supported finite positive video playback rate. Comment eligibility and appearance MUST continue to use the video's current media time and the configured timeline offset; increasing playback rate therefore reaches later timestamped comments sooner in wall time but MUST NOT make an individual scrolling comment cross the screen faster.

#### Scenario: Playback begins at a non-unit rate
- **WHEN** comments are loaded while the video is already playing at 0.5x, 1.5x, or 2x and fixed-speed mode is enabled
- **THEN** each scrolling comment uses the configured 1x wall-clock screen speed while appearing at its timestamp on the accelerated or decelerated media timeline

#### Scenario: Playback rate changes with comments on screen
- **WHEN** the video changes between supported rates while one or more scrolling comments are visible
- **THEN** each visible comment continues from its current screen position without a speed jump, position jump, premature disappearance, duplicate, or full-stage reset

#### Scenario: Rate changes alter timestamp density
- **WHEN** the video changes to 2x and several comments have distinct nearby media timestamps
- **THEN** those comments become eligible as the 2x media timeline reaches each timestamp while every individual comment retains the configured wall-clock screen speed

#### Scenario: Fixed-speed mode is disabled
- **WHEN** the video plays or changes rate while fixed-speed mode is disabled
- **THEN** scrolling motion continues to scale with the video's playback rate exactly as it did before this change

### Requirement: Transport continuity
Fixed-speed mode MUST preserve dd-danmaku's existing seek, pause, resume, visibility, reload, and timeline-offset contracts. Pausing MUST freeze visible scrolling comments, resuming MUST continue them from the frozen positions at the configured wall-clock speed, and seeking MUST select comments from the destination media time without rewriting their source timestamps.

#### Scenario: Pause and resume at accelerated playback
- **WHEN** the user pauses at 2x with a scrolling comment visible and later resumes at 2x
- **THEN** the comment remains frozen while paused and resumes from the same position at the configured wall-clock speed

#### Scenario: Seek while fixed-speed mode is enabled
- **WHEN** the user seeks forward or backward at a non-unit playback rate
- **THEN** stale visible comments are cleared according to the existing seek contract and subsequent comments are selected from the destination media time plus the configured timeline offset

#### Scenario: Hide and show while media time advances
- **WHEN** the user hides danmaku, the media timeline continues, and the user shows danmaku again
- **THEN** the existing visibility contract clears the old stage and resumes comment selection from the then-current media time instead of reviving frozen pre-hide comments

#### Scenario: Toggle mode during playback
- **WHEN** the user changes the fixed-speed setting while a video is playing
- **THEN** one atomic danmaku reload may clear comments currently on the stage, but the selected behavior takes effect without restarting the video, changing its playback rate, changing its current media time, duplicating an engine instance, or corrupting pending comment timestamps

### Requirement: Supported media-path parity and safe fallback
Fixed-speed mode SHALL behave equivalently when dd-danmaku is bound to a real HTML5 media element or to the Emby hidden media adapter, and in every supported dd-danmaku injection path. Missing, zero, negative, non-finite, or temporarily unavailable playback-rate data MUST fall back safely to 1x timing and MUST NOT produce invalid positions, a stalled render loop, or a script failure.

#### Scenario: Hidden Emby adapter tracks rate and time
- **WHEN** an Emby client without a native page video reports a valid media time and changes its playback rate through the hidden adapter
- **THEN** comment scheduling follows the reported media time and fixed-speed scrolling remains wall-clock stable

#### Scenario: Playback-rate data is temporarily invalid
- **WHEN** the active media path exposes an absent or invalid playback-rate value during initialization or player replacement
- **THEN** dd-danmaku uses a safe 1x timing value until a valid rate is available and continues rendering without corrupting comment state
