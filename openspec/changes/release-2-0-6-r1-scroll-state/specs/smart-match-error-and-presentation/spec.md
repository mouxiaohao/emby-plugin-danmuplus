## ADDED Requirements

### Requirement: Newly entered secondary pages begin at their vertical origin
Whenever Smart Match navigates from a visible parent page into a return-capable secondary page, the secondary page SHALL begin at its vertical origin after its initial controls and rows or empty-result content have been placed. This rule SHALL cover whole-Series Season candidate pages entered through view/rematch actions, unmatched or remainder-range manual matching pages, Episode source-selection pages, and Movie part/version-selection pages. Initial direct Season, Movie, and Episode candidate pages and replacement candidate results from an explicit search SHALL also begin at their vertical origin. Candidate order SHALL remain the existing server-authored order; the browser MUST NOT re-score, re-sort, filter, or otherwise change candidate participation to satisfy this presentation requirement.

#### Scenario: Failed whole-Series Season opens from a scrolled overview
- **WHEN** the user scrolls a whole-Series result overview downward and activates `查看候选` for an unmatched Season
- **THEN** the Season candidate page SHALL open at its top with the first server-ordered candidate visible

#### Scenario: Rematch passes through an asynchronous busy page
- **WHEN** the user activates a whole-Series `重新智能匹配` or `重新匹配` action below the top and the request temporarily replaces the dialog content
- **THEN** the resulting secondary candidate page SHALL begin at its top rather than inherit either the overview offset or the temporary busy-page geometry

#### Scenario: Remainder range opens manual matching
- **WHEN** the user activates `手动匹配` or `重新匹配` for an unmatched, manual, or mapped temporary range in a scrolled composite overview
- **THEN** the range candidate page SHALL begin at its top while retaining the current server candidate order

#### Scenario: Candidate opens a nested source or version page
- **WHEN** an Episode candidate opens its source-Episode selector or a Movie candidate opens its multi-part/version selector
- **THEN** the nested selector SHALL begin at its top without changing its source or version order

#### Scenario: Direct target candidate page opens
- **WHEN** the user enters an ordinary candidate list for a direct Season, Movie, or Episode target, including an ordinary search with no candidate rows
- **THEN** that candidate page SHALL begin at its top with its search controls and first row or empty result visible

#### Scenario: Candidate details expand in place
- **WHEN** the user expands or collapses read-only details beneath a candidate that is already visible
- **THEN** the dialog SHALL NOT force that page back to its top solely because of the inline state change

#### Scenario: Terminal or same-page surface renders
- **WHEN** Smart Match updates progress, busy, inline-detail, or other content that does not establish a return-capable child page
- **THEN** it SHALL NOT create a parent scroll-return context or impersonate a new secondary-page transition

### Requirement: Returning from a secondary page restores the parent logical viewport
Before a return-capable secondary transition mutates the parent DOM or displays an asynchronous busy state, Smart Match SHALL retain a transient parent viewport context for the open dialog. The exact internal navigation trigger SHALL passively sample geometry during trusted pointer or Enter/Space preactivation so native focus cannot replace the user's original viewport; without Pointer Events, Android SHALL observe touch and desktop SHALL observe mouse so a synthesized mouse event cannot overwrite touch geometry. The matching business click SHALL receive its actual event, clear and atomically consume a valid pending sample, while an untrusted/programmatic click SHALL reject any stale pending sample and sample at click time. Preactivation SHALL NOT push a navigation context, prevent or stop input, change focus or scroll, read host state, or schedule work. Cancelled, context-menu, drag, blur, detached, mismatched, or changed-candidate activation SHALL discard the pending sample. A business handler whose capture returns no context SHALL NOT render a child, rebuild parent state, or issue a request. Returning through the visible return action or Android command-owned parent-return path SHALL first render the parent's current data and then restore the equivalent initiating logical action at its prior viewport-relative position; if that action no longer exists, it SHALL restore the action's logical row instead. If the exact row no longer exists, Smart Match SHALL next use its surviving enclosing section and then a logical neighbor recorded before the transition. Only when none of those semantic anchors survives may it restore the old numeric offset, clamped to the parent's new valid scroll range; a parent with no scrollable range SHALL use zero. The old numeric offset MUST NOT override a surviving semantic anchor. The state MUST remain in memory only, MUST be isolated per dialog, and MUST be consumed last-in-first-out for nested child pages.

#### Scenario: Native activation focus moves the dialog before click
- **WHEN** mouse, pen, touch, Enter, or Space preactivation samples a trigger and native focus moves the Smart Match body before the matching click
- **THEN** return SHALL use the preactivation viewport geometry and the click SHALL create exactly one navigation context

#### Scenario: Activation is cancelled or never clicks
- **WHEN** preactivation is cancelled, becomes a context menu or drag, loses focus, is replaced by another trigger, changes candidate identity, detaches, or never produces a matching click
- **THEN** it SHALL create no navigation context and SHALL NOT leak geometry into a later trigger

#### Scenario: Programmatic activation has no preactivation sample
- **WHEN** a programmatic click reaches an allowed internal navigation trigger without a valid pending sample
- **THEN** Smart Match SHALL capture current click-time geometry and preserve the same single-context return behavior

#### Scenario: Candidate page returns to the whole-Series overview
- **WHEN** a user enters a Season candidate page from below the top of a whole-Series overview and returns with the visible button or Android command-owned back
- **THEN** the overview SHALL show the initiating Season row at the same viewport-relative position it occupied before entry

#### Scenario: Expanded episode mapping collapses during parent rebuild
- **WHEN** the user enters a child page from a position calculated while `逐集映射` details are expanded and those details are collapsed when the current overview is rendered again
- **THEN** restoration SHALL first locate the equivalent initiating action and otherwise its stable Season/range row in the new geometry instead of writing the offset measured against the expanded content

#### Scenario: Successful selection changes the parent height
- **WHEN** applying a Season candidate or temporary-range match returns to an overview whose rows, mapping coverage, or total scroll height have changed
- **THEN** the same logical Season or range SHALL remain at its prior viewport-relative position when it survives
- **AND** a removed or merged range SHALL fall back to its surviving Season or nearest logical neighbor without overscrolling the new content

#### Scenario: Nested selector returns to its candidate list
- **WHEN** an Episode source selector or Movie part/version selector returns through its button or Android command-owned back path
- **THEN** its parent candidate row SHALL return to the viewport-relative position captured before candidate resolution began

#### Scenario: Secondary page rerenders before returning
- **WHEN** a child page performs another search, handles an error, or rerenders while the same parent remains underneath it logically
- **THEN** Smart Match SHALL retain the original parent context without pushing or overwriting it with the child's current position

#### Scenario: Transition fails before a child page becomes available
- **WHEN** a Series rematch, composite-range rebuild, or Episode/Movie detail resolution is cancelled, fails, or completes without a return-capable child target
- **THEN** Smart Match SHALL render the applicable parent and restore the initiating action or its fallback anchor
- **AND** it SHALL consume exactly the provisional context for that transition without retaining stale state or consuming an older nested context

#### Scenario: Download submission fails and recovers the child
- **WHEN** an Episode source or Movie part/version child starts submission but the server does not accept a download task and Smart Match restores that child
- **THEN** the existing parent candidate context SHALL remain available without a duplicate context
- **AND** a subsequent return SHALL restore the initiating candidate action at its captured viewport-relative position

#### Scenario: No semantic anchor survives
- **WHEN** current parent data no longer contains the initiating row, a surviving parent section, or a logical neighboring row
- **THEN** Smart Match SHALL clamp the saved numeric position to the rebuilt parent's valid range and SHALL use zero when that range has no scrollable extent

#### Scenario: Dialog exits instead of returning
- **WHEN** Smart Match closes, the server accepts a non-returnable download task, or desktop browser Back transfers navigation ownership to Emby
- **THEN** the abandoned internal return context SHALL be discarded without a delayed scroll write

#### Scenario: Recoverable submission is not terminal
- **WHEN** Smart Match is displaying submission/busy progress from which a failed or cancelled start can restore a source or version child
- **THEN** it MUST NOT discard that child's parent context until the task is accepted or internal navigation is otherwise abandoned

### Requirement: Browser dialog lifecycle does not navigate or rebuild the Emby host view
Manual Smart Match entry on desktop or Android/WebView SHALL open, navigate internally, and explicitly close its dialog without creating, replacing, or traversing a dialog-owned session-history entry. Dialog open, visible close, Escape, force close, internal return, and ordinary disposal MUST NOT call `pushState`, `replaceState`, or `history.back`. The underlying Emby route, current virtual-library content window, and applicable host scroll position SHALL remain intact across action-sheet close and dialog presentation, and the dialog lifecycle MUST NOT cause a same-route navigation, virtual-list reconstruction, or first-page library query. The frontend MUST NOT use host-scroll capture or delayed restoration to conceal such a reconstruction. Android identity SHALL require an Android UA-CH platform or Android user-agent token; responsive width or touch capability alone MUST NOT select Android command ownership. Automatic library-import matching is not a browser-entry operation and SHALL remain unaffected.

When the topmost connected Android Smart Match dialog receives exact `command: "back"`, Smart Match SHALL call `preventDefault` before changing dialog state if the event is not already canceled, and SHALL invoke that dialog's return state machine exactly once only after `defaultPrevented` is true, whether cancellation was preexisting or was just established. It MUST NOT stop command propagation, register or depend on a Smart Match native `backbutton` listener, mutate history, schedule a fallback, or try a second return channel. An already canceled command SHALL still invoke the eligible topmost handler once. A noncancelable event, missing/ineffective/throwing cancellation, absent eligible overlay, desktop dialog, or non-back command SHALL NOT change Smart Match state. If the handler returns false or throws after successful cancellation, the command SHALL remain canceled and Smart Match MUST NOT retry or hand the gesture to history or `backbutton`; any diagnostic MUST omit media data. Any host `popstate` SHALL synchronously clean up all Smart Match overlays and their per-dialog listeners without invoking an internal parent return or issuing another history operation, SHALL leave the script-lifecycle command/popstate singleton listeners installed, and SHALL then leave navigation to Emby.

#### Scenario: Scrolled desktop library item opens whole-Series Smart Match
- **WHEN** a desktop user scrolls a virtual media library, opens an item action sheet away from the top, and activates whole-Series Smart Match
- **THEN** the underlying route, loaded virtual-library window, and vertical position SHALL remain unchanged while the dialog opens
- **AND** opening the dialog SHALL NOT trigger a same-route navigation or restart the library query at its first page

#### Scenario: Desktop dialog is explicitly dismissed
- **WHEN** a desktop user closes Smart Match with its close button or Escape
- **THEN** the dialog SHALL be removed without an additional host-history traversal, route change, virtual-list reconstruction, or scroll rewrite

#### Scenario: Desktop action sheet closes asynchronously
- **WHEN** the Emby action sheet remains connected briefly after Smart Match is activated
- **THEN** the fixed Smart Match dialog MAY open immediately and the host view SHALL remain intact without capturing or restoring a host scroll offset

#### Scenario: Desktop browser back is used
- **WHEN** a desktop user invokes browser Back while Smart Match is open
- **THEN** Smart Match SHALL clean up every active Smart Match overlay and its per-dialog listeners without issuing another history operation, while leaving the script-lifecycle command/popstate singleton listeners installed
- **AND** the resulting host navigation SHALL remain owned by Emby rather than being converted into Smart Match parent navigation

#### Scenario: Android back returns from a secondary Smart Match view
- **WHEN** a real Android/WebView user produces a cancelable `command: "back"` from a whole-Series Season candidate view
- **THEN** Smart Match SHALL return to its whole-Series overview at the retained logical viewport and the underlying Emby route SHALL remain unchanged

#### Scenario: Android back closes or protects the top-level dialog
- **WHEN** a real Android/WebView user produces a cancelable `command: "back"` from a top-level Smart Match view or while matching is protected
- **THEN** a closable top-level dialog SHALL close, while a protected or busy dialog SHALL remain open
- **AND** the underlying Emby route SHALL remain unchanged in either case

#### Scenario: Nested Android dialogs handle only the topmost command
- **WHEN** more than one connected Android Smart Match overlay exists and the host dispatches one cancelable `command: "back"`
- **THEN** only the topmost eligible dialog SHALL handle it once and lower overlays SHALL remain unchanged

#### Scenario: Android command is already canceled
- **WHEN** an exact Android back command reaches Smart Match with `defaultPrevented` already true and a topmost eligible dialog exists
- **THEN** Smart Match SHALL invoke that dialog's return state machine once without attempting another fallback

#### Scenario: Android command ownership cannot be established
- **WHEN** an exact Android back command is noncancelable, has no usable `preventDefault`, remains uncanceled, or cancellation throws
- **THEN** Smart Match SHALL leave its internal page unchanged and SHALL NOT try history or native `backbutton`

#### Scenario: Android handler declines or throws
- **WHEN** command cancellation succeeds but the topmost return state machine returns false or throws
- **THEN** Smart Match SHALL keep the command canceled, SHALL NOT retry through another channel, and SHALL NOT expose media data in diagnostics

#### Scenario: Back command has no eligible Smart Match overlay
- **WHEN** Android dispatches `command: "back"` with no connected Android Smart Match dialog, or desktop dispatches the same command
- **THEN** Smart Match SHALL NOT cancel or handle it and Emby SHALL retain navigation ownership

#### Scenario: A non-back command passes through
- **WHEN** any runtime dispatches a command whose exact value is not `back`
- **THEN** Smart Match SHALL NOT cancel it, inspect media state, or change dialog state

#### Scenario: Host popstate cleans the complete overlay stack
- **WHEN** Emby dispatches `popstate` while one or more Smart Match overlays are connected
- **THEN** Smart Match SHALL remove every overlay and its dialog listeners without invoking a parent-return handler or another history operation

#### Scenario: Responsive or touch-capable desktop remains desktop
- **WHEN** desktop Emby uses a narrow viewport, touch input, or responsive device emulation without an Android runtime identity
- **THEN** Smart Match SHALL use the desktop host-owned lifecycle and MUST NOT enable Android command ownership

#### Scenario: Automatic library import runs
- **WHEN** Smart Match is invoked by automatic library-import processing without a browser action sheet
- **THEN** its matching, selection, and download behavior SHALL remain unchanged
