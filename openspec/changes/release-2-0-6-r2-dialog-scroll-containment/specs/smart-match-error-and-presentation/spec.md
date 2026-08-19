## ADDED Requirements

### Requirement: An open Smart Match dialog contains its own scrolling
While one or more Smart Match overlays are connected, vertical touch panning, pointer panning, or wheel input that begins within the topmost overlay SHALL either scroll that dialog's active internal body or terminate within the overlay without changing any underlying Emby detail-page, library-page, virtual-list, document, or body scroll position. This behavior SHALL cover short or non-scrollable dialog content, long content at its top, middle, or bottom, and input beginning on the dialog body, header, footer, card, or backdrop. It SHALL apply uniformly to Series, Season, Episode, and Movie Smart Match entered from detail-page controls, Season or Episode rows, and media-library cards. Native scrolling within a long dialog body, focus, text input, selection controls, candidate actions, parent/child navigation, Android command-owned Back, and host scrolling after the overlay closes MUST remain usable. Scroll containment MUST NOT change the Emby route, loaded virtual-library window, candidate participation or server-authored order, matching, binding, download, metadata, or automatic library-import behavior.

#### Scenario: Short dialog opens over a scrollable detail page
- **WHEN** a user opens Series, Season, Episode, or Movie Smart Match from a scrollable detail page while the dialog body is shorter than its viewport and pans vertically anywhere within the overlay
- **THEN** the underlying Emby detail-page scroll position SHALL remain unchanged
- **AND** the current Emby route SHALL remain unchanged

#### Scenario: Long dialog body scrolls in its middle
- **WHEN** a user pans vertically inside a long Smart Match body while that body can scroll in the requested direction
- **THEN** the dialog body SHALL scroll natively
- **AND** every underlying Emby scroll position and virtual-library window SHALL remain unchanged

#### Scenario: Long dialog reaches either boundary
- **WHEN** a user continues panning upward at the top or downward at the bottom of a Smart Match body
- **THEN** the remaining gesture SHALL terminate within the dialog rather than moving an underlying Emby scroller

#### Scenario: Gesture begins outside the dialog body
- **WHEN** vertical touch, pointer-pan, or wheel input begins on the Smart Match header, footer, card margin, or backdrop
- **THEN** the input SHALL NOT scroll the underlying Emby page
- **AND** dialog buttons, fields, and selection controls SHALL retain their ordinary interaction behavior

#### Scenario: Media-library card entry uses the same containment
- **WHEN** Smart Match is opened from a media-library card action menu rather than a detail page
- **THEN** the same dialog-scroll containment SHALL apply without an entry-specific branch
- **AND** the loaded virtual-list position and server-authored candidate order SHALL remain unchanged

#### Scenario: Secondary page returns after contained scrolling
- **WHEN** a user scrolls a secondary candidate/source/version page and returns through its visible action or Android command-owned Back
- **THEN** the existing logical parent-viewport restoration SHALL remain authoritative
- **AND** no underlying Emby scroll position SHALL have changed while either Smart Match page was open

#### Scenario: Dialog closes and host scrolling resumes
- **WHEN** the topmost Smart Match overlay is explicitly closed, command-closed, or disposed for host navigation
- **THEN** ordinary Emby page scrolling outside the removed overlay SHALL work without a delayed scroll write or retained input owner

#### Scenario: Automatic library import runs without an overlay
- **WHEN** automatic library-import matching runs without opening a browser dialog
- **THEN** its search, selection, binding, download, and metadata behavior SHALL remain unchanged
