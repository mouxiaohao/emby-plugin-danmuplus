## Context

See `proposal.md` for motivation. The current browser script captures only detail-page `.btnMoreCommands` clicks and derives the target from the page URL, although card action sheets represent their own item. The API and UI accept only Series and Season, encode preview and task state in season-named fields, and initialize manual search with blank text. The provider and download layers already contain the primitives needed to resolve one Episode from a matched Season and to retrieve Movie danmu.

The design must remain compatible with Emby 4.9.x's dynamically rendered action sheets, C# 8/.NET Standard 2.0, saved manual bindings, STRM items, provider-specific identifiers, duplicate skipping, cancellation, and existing Series/Season clients.

## Goals / Non-Goals

**Goals:**

- Resolve each open action sheet to an authoritative Series, Season, Episode, or Movie item.
- Reuse one provider-neutral ranking policy while scoring Movie evidence separately from Season evidence.
- Represent Movie preview and tracked work explicitly without breaking existing season-shaped JSON fields.
- Resolve an Episode through its Series/Season context, expose a suggested source Episode number, and permit a validated per-download override.
- Initialize each manual-search input from one shared media-parent-name rule.
- Reuse the proven provider-specific Movie resolution/download path behind an outcome-returning operation.

**Non-Goals:**

- Supporting Folder, Collection, Person, or music item menus.
- Changing Emby's action-sheet implementation or requiring a custom Emby web build.
- Treating a Movie as a synthetic one-episode Season.
- Altering automatic library-import behavior for movies or seasons.

## Decisions

### Capture menu origin, then verify against the open action sheet

The click listener will recognize both the detail-page more button and card overflow actions. For cards it will capture an item-id candidate from the nearest card's stable item metadata or item link; for details it will capture the hash id. Once the action sheet opens, it will correlate that candidate with identity exposed by the sheet (including its preview image when present), fetch the item through `ApiClient.getItem`, and inject only after the type is validated. A monotonically increasing context token will invalidate older asynchronous resolutions.

This layered approach is preferred over relying only on preview-image CSS because some action sheets omit previews, and over a global “last clicked card” variable because delayed DOM creation can otherwise inject into the wrong menu.

### Keep one injected action implementation with type descriptors

Labels, dialog titles, preview renderer, and submit behavior will be selected from a Series/Season/Episode/Movie descriptor after authoritative type resolution. The action retains a per-menu marker and per-button running guard. This avoids four event pipelines that could diverge while keeping type-specific behavior explicit.

### Extend the API additively with item-oriented Movie and Episode results

`DanmuMatchPreviewResult.Seasons` remains unchanged for Series and Season. Movie previews add an explicit Movie match result (or an additive item-oriented result field) containing item id/name/year, candidate state, diagnostics, and selection. Episode previews add local Episode identity and number plus a suggested source Episode number for each Season candidate. Tracked task responses add generic target identity fields while retaining current season fields for compatibility; Movie and Episode tasks each contain one target outcome rather than synthetic Season contents.

Adding fields is preferred over renaming `Seasons`, `SeasonId`, or `Episodes`, which would break the deployed browser script and any external caller. Serializing a Movie or local Episode as a fake Season was rejected because it obscures type validation and makes retry/progress bugs likely.

### Resolve Episode candidates through Season media, then select one source Episode

An Episode preview will load its authoritative parent Season and owning Series, run the existing Season candidate discovery with Series-title defaults, and resolve each candidate's media details far enough to propose a source Episode number. The initial suggestion uses the best available mapping evidence, including the local Episode index and the candidate episode list. Candidate rows show that number explicitly. Only the selected row renders an editable positive-integer input initialized to the suggestion.

Confirmation submits the candidate media id and the user-visible source Episode number. The backend re-resolves the candidate, verifies that the requested source Episode exists, and calls the existing single-Episode progress download path for the target local Episode. The selection is scoped to the local Episode request and does not replace the containing Season's manual binding. This is preferred over mutating the Season binding because a one-off correction must not redirect sibling Episodes.

### Derive every manual-search default from authoritative context

A shared frontend helper will return the Movie's own title for Movie and the owning Series title for Series, Season, and Episode. Preview responses will carry the required parent name when the page item alone does not expose it. Each time a manual-search view is first opened, its input is initialized from that helper; subsequent user edits are preserved while that view remains active. This avoids inconsistent defaults between whole-Series per-Season search, standalone Season search, and Episode search.

### Add a movie-specific search entry point over shared candidate primitives

The search engine will query each enabled scraper with the actual Movie object and Movie title/keyword. It will normalize usable results into the existing candidate shape, filter non-movie results using provider metadata, de-duplicate provider/id pairs, and score title and year evidence deterministically. Common ordering, provider failure isolation, manual-binding lookup, and confidence thresholds will be shared with season matching where their semantics match; season-only parent-title, season-keyword, and episode-count evidence will not be applied to movies.

This is preferred over calling the season search with empty season metadata, which would yield misleading scores and could admit television candidates.

### Extract an outcome-returning single-movie download operation

The provider-specific steps currently embedded in queued Movie event processing—resolve media, select the provider-specific playable/comment id, obtain movie episode details when required, and write XML—will be factored behind a method that returns the same status categories used by tracked downloads. Both event processing and the new controller task call that method, preserving Bilibili versus non-Bilibili identifier handling, duplicate skipping, partial XML rules, and force refresh.

The controller will validate and persist the selected binding, create a one-target tracked task, run it through the existing serialized download queue, honor cancellation, and snapshot status for polling. Reusing the event queue without a returned outcome was rejected because the frontend could not distinguish submission from actual success.

### Persist only confirmed selections

Preview and forced search are read-only. Automatic selection is persisted only after user confirmation; a manual choice is stored with the existing manual suffix convention. If candidate detail validation fails, no replacement binding is saved. Existing saved manual bindings remain authoritative until a new confirmed selection succeeds.

## Risks / Trade-offs

- [Emby card DOM selectors vary across web-client builds] → Use multiple identity sources, require server validation, omit the action when identity is not unique, and live-test library grid plus horizontal Season rows on Emby 4.9.x.
- [Action sheets render asynchronously and old API calls can finish late] → Associate every request with the current open sheet and a generation token before injecting.
- [Provider Movie identifiers differ from danmu comment identifiers] → Centralize the already established provider-specific conversion in the extracted Movie download operation and add provider-route regressions.
- [Additive task fields temporarily duplicate season and generic identity] → Keep compatibility fields documented and populate only their applicable meanings; do not overload them for Movie.
- [Movie scoring has less evidence than Season scoring] → Require title/year evidence and confidence separation; ambiguous results remain manual.
- [Episode numbering can differ because of specials, absolute numbering, or provider omissions] → Display the proposed source number, require an explicit valid number at confirmation, and validate it against freshly resolved candidate media.
- [Resolving media details for every Episode candidate adds provider requests] → Limit detail resolution to displayed candidates, reuse preview data within the dialog, and revalidate only the selected candidate on submit.
- [Refactoring existing Movie event processing could change automatic downloads] → Route old and new paths through the same extracted operation and compare representative Bilibili and non-Bilibili outcomes before release.

### Present every tracked single target as a one-row Season-style task

Movie and Episode tasks retain their explicit backend target types, but the frontend SHALL render their sole outcome with the same item-row component, status details, summary counts, and retry control used by Season progress. A Movie row uses the Movie name and a single-item ordinal; an Episode row includes its local episode number. Retry dispatch remains target-aware so a Movie re-enters the Movie provider path while an Episode re-enters the selected source-Episode path.

This presentation reuse is preferred over the current summary-only single-target view because it exposes provider failures and recovery controls without weakening the API's explicit Movie/Episode semantics.

### Race single-target work against cancellation and a 180-second deadline

The controller SHALL race each Movie or Episode provider operation against its task cancellation token and a 180-second deadline. Cancellation produces a cancelled terminal item; expiry produces a skipped terminal item with an explicit timeout message. Once either wins, polling becomes terminal and the dialog is closable; a late provider task is observed for logging but cannot overwrite the terminal result.

Because legacy scraper operations do not consistently accept cancellation tokens, this is a logical task deadline rather than a guarantee that every underlying network call is physically aborted. Provider HTTP paths implicated by live diagnosis SHALL additionally receive bounded request behavior where feasible.

### Use one stable menu insertion rule for all supported types

All four item types SHALL use the same ordered list of native action anchors and insert immediately before the first available anchor. The implementation SHALL not append Movie or Episode actions merely because a type-specific sheet omits one preferred anchor.

### Capture Android long-press context and bootstrap from newly opened sheets

The frontend SHALL capture media identity from `contextmenu`, pointer, or touch origins before Android opens its action sheet. Its mutation observer SHALL also detect a newly opened action sheet when no pending desktop click context exists and bootstrap injection from an authoritative item id exposed by that sheet. A captured card id takes precedence over the current detail-page id, preventing a long-pressed Season from being mistaken for its parent Series.

Gesture capture only records a short-lived candidate and never opens or modifies Emby's menu itself. Before injection, the candidate is still correlated with action-sheet identity and validated through `ApiClient.getItem`, preserving stale-context and unsupported-type protections.

## Migration Plan

1. Deploy the additive API/model and shared Movie download operation with Series/Season behavior unchanged.
2. Deploy the updated browser script and bump its installation flag so already loaded pages install the new listener after refresh.
3. Verify detail and card menus, then verify Movie preview/bind/download and Episode suggestion/override/download on at least Bilibili and one non-Bilibili provider plus STRM media.
4. Roll back by restoring the prior browser script and DLL together. Existing provider bindings and XML files require no data migration and remain usable.
