## Purpose

Defines consistent smart-match action availability, target resolution, Episode source selection, and manual-search defaults across Emby detail pages and card-based views.

## ADDED Requirements

### Requirement: Smart-match actions appear for every supported menu context
The frontend SHALL add the item-appropriate smart-match action to the detail-page or card three-dot action sheet for Series, Season, Episode, and Movie items, and MUST NOT add it for unsupported item types.

#### Scenario: Series card in a library view
- **WHEN** a user opens the three-dot action sheet on a Series card in a television or animation library
- **THEN** the action sheet SHALL contain “智能匹配并下载整部剧弹幕”

#### Scenario: Season card in a series detail view
- **WHEN** a user opens the three-dot action sheet on a Season card within a Series detail view
- **THEN** the action sheet SHALL contain “智能匹配并下载本季弹幕”

#### Scenario: Movie detail or card menu
- **WHEN** a user opens the three-dot action sheet for a Movie from either its detail page or a card-based view
- **THEN** the action sheet SHALL contain “智能匹配并下载电影弹幕”

#### Scenario: Episode card in a Season detail view
- **WHEN** a user opens the three-dot action sheet on an Episode row or card within a Season detail view
- **THEN** the action sheet SHALL contain “智能匹配并下载本集弹幕”

#### Scenario: Episode detail menu
- **WHEN** a user opens the three-dot action sheet on an Episode detail page
- **THEN** the action sheet SHALL contain “智能匹配并下载本集弹幕”

#### Scenario: Unsupported card type
- **WHEN** a user opens the action sheet for a Folder, Collection, or other unsupported item type
- **THEN** the frontend MUST NOT add a smart-match download action

### Requirement: Action targets the item whose menu was opened
The frontend MUST resolve and validate the media item associated with the current action sheet before injecting or running an action, regardless of whether the menu originated from a detail page or a card.

#### Scenario: Two card menus are opened in succession
- **WHEN** a user closes one card action sheet and opens another card action sheet before the first context expires
- **THEN** the displayed action and subsequent smart-match request SHALL target only the item associated with the currently open action sheet

#### Scenario: Menu identity cannot be established
- **WHEN** the frontend cannot uniquely resolve the current action sheet to a media item
- **THEN** it MUST omit the smart-match action instead of reusing a stale or guessed item identifier

#### Scenario: Item type is read from the server
- **WHEN** a candidate item identifier is found from page or menu context
- **THEN** the frontend SHALL obtain the authoritative item type before selecting the action label and enabling the workflow

### Requirement: Episode matching exposes and controls the source episode number
The Episode workflow SHALL reuse the Season candidate-picker presentation while clearly identifying the local Episode number and each candidate's suggested source Episode number. It SHALL apply an editable source Episode number only to the currently selected candidate and download only the target local Episode.

#### Scenario: Episode preview is displayed
- **WHEN** an Episode smart-match preview returns candidates
- **THEN** the library-information area SHALL show the local Episode number and every candidate SHALL show its suggested source Episode number

#### Scenario: Candidate becomes selected
- **WHEN** a candidate is automatically selected or the user selects it manually
- **THEN** a source-Episode-number input SHALL appear beside that selected candidate and SHALL initially contain its smart-match suggested Episode number

#### Scenario: User overrides the suggested Episode number
- **WHEN** the user replaces the selected candidate's suggested Episode number with another valid source Episode number and confirms the download
- **THEN** the system SHALL retrieve that source Episode and write its danmu for only the selected local Episode

#### Scenario: Source Episode number is invalid
- **WHEN** the source-Episode-number input is empty, is not a positive integer, or identifies no Episode in the selected candidate
- **THEN** the workflow MUST prevent submission and SHALL display a validation error without changing any binding

#### Scenario: Single-Episode selection is confirmed
- **WHEN** a user confirms an Episode candidate and source Episode number
- **THEN** that selection MUST NOT replace the manual binding of the containing Season or affect downloads for sibling Episodes

### Requirement: Manual search inputs start with the media parent name
Every Movie, Series, Season, and Episode manual-search input SHALL be editable and SHALL be pre-filled from the resolved item context rather than initially blank.

#### Scenario: Movie manual search opens
- **WHEN** a Movie manual-search interface is shown
- **THEN** its search input SHALL initially contain the Movie title

#### Scenario: Whole-Series manual search opens
- **WHEN** a manual-search interface is shown for a Series or for one of its Season candidates in the whole-Series workflow
- **THEN** its search input SHALL initially contain the Series title

#### Scenario: Season manual search opens
- **WHEN** a Season manual-search interface is shown
- **THEN** its search input SHALL initially contain the owning Series title

#### Scenario: Episode manual search opens
- **WHEN** an Episode manual-search interface is shown
- **THEN** its search input SHALL initially contain the owning Series title

#### Scenario: User edits the default search text
- **WHEN** the user changes a pre-filled manual-search value and submits it
- **THEN** the workflow SHALL search using the user's edited value

### Requirement: Menu injection is idempotent
The frontend SHALL add at most one smart-match action to each open action sheet and SHALL remain functional as Emby dynamically creates and removes action-sheet DOM nodes.

#### Scenario: Action-sheet DOM mutates repeatedly
- **WHEN** Emby updates an already open action sheet or the injection observer runs more than once
- **THEN** exactly one smart-match action SHALL be present and exactly one workflow SHALL start per user click

### Requirement: Single-target progress has Season feature parity
The frontend SHALL render an Episode or Movie tracked download as one detailed item using the same progress presentation and retry affordance as a Season task.

#### Scenario: Episode progress is displayed
- **WHEN** an Episode tracked task is queued, running, or terminal
- **THEN** the dialog SHALL show exactly one detailed row identifying the local Episode number, its concrete status and message, and a retry control when retry is applicable

#### Scenario: Single-target operation exceeds the deadline
- **WHEN** an Episode or Movie download remains incomplete for 180 seconds
- **THEN** the item SHALL be automatically skipped with a timeout explanation and the progress dialog SHALL become closable

#### Scenario: User force-stops a running task
- **WHEN** the user confirms force-stop for a running smart-match download
- **THEN** the dialog close control SHALL become usable without waiting for the underlying provider request to return

### Requirement: Supported actions share one menu position
The injected Series, Season, Episode, and Movie actions SHALL use the same stable native action anchor order.

#### Scenario: Movie or Episode menu has a different native command set
- **WHEN** the preferred native anchor is absent but another configured anchor exists
- **THEN** the smart-match action SHALL be inserted before that anchor rather than appended to the end of the menu

### Requirement: Android long-press action sheets are supported
The frontend SHALL inject the same supported smart-match action when Android CustomJSS opens an Emby action sheet through a long press, without requiring a desktop more-button click first.

#### Scenario: User long-presses a media-library card
- **WHEN** an Android user long-presses a supported Series, Movie, Season, or Episode card and Emby opens its action sheet
- **THEN** the frontend SHALL resolve that card's authoritative item and inject the item-appropriate smart-match action

#### Scenario: User long-presses a Season inside a Series detail page
- **WHEN** an Android user long-presses a Season card while the current detail page belongs to its parent Series
- **THEN** the injected action SHALL target the pressed Season rather than the Series represented by the page URL

#### Scenario: Action sheet appears without a captured gesture target
- **WHEN** an Android action sheet appears without a preceding recognized click, contextmenu, pointer, or touch target but exposes an authoritative media item id itself
- **THEN** the frontend SHALL initialize injection from the action-sheet identity and MUST NOT reuse a stale target
