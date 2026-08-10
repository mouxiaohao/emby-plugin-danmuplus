## Why

The smart-match workflow is currently exposed only from Series and Season detail-page menus, so the same media cannot be handled from the card menus users encounter in library and series views. Movies and individual Episodes are excluded, and blank manual-search inputs make users repeatedly re-enter media titles that are already known to Emby.

## What Changes

- Expose the existing whole-series action in the three-dot card menu for Series items in library views.
- Expose the existing whole-season action in the three-dot card menu for Season items shown inside a series detail view.
- Add Movie to the supported smart-match item types and provide a movie-specific preview, candidate selection, binding, tracked download, progress, and result flow.
- Expose the movie smart-match action from both movie detail-page and movie card three-dot menus.
- Expose “智能匹配并下载本集弹幕” from Episode card menus in Season details and from Episode detail-page menus.
- Reuse the Season candidate-picker layout for Episode matching while showing the local episode number and every candidate's suggested source episode number.
- Add an editable source-episode-number input beside the selected Episode candidate, initialized from the smart-match suggestion and validated before downloading only the selected local Episode.
- Pre-fill every Movie, Series, Season, and Episode manual-search input with its media parent name: the Movie title for Movies and the owning Series title for television content.
- Resolve the target item reliably from both detail and card action sheets without leaking the previously opened card's identity into a later menu.
- Preserve current Series/Season matching, manual-binding precedence, provider-specific download behavior, retry behavior, and duplicate-skipping semantics.
- Render Movie and Episode tracked downloads with the same per-item detail, status, and retry presentation as a Season download, with exactly one item row and Movie treated as a single downloadable item in the presentation layer.
- Automatically mark a single Movie or Episode download as skipped after 180 seconds, allow force-stopped progress dialogs to close immediately, and keep late provider completion from changing the terminal task result.
- Inject the same action on Android CustomJSS when an action sheet is opened by long-pressing a media card or a Season inside a detail page, even when no desktop-style more-button click occurs.
- Place Movie and Episode actions at the same stable action-sheet position used by Series and Season actions.
- Diagnose and harden non-Bilibili Movie downloads, including iQIYI failures and Tencent requests that otherwise remain running indefinitely.
- Non-goals: changing provider ranking configuration, adding Folder/Collection-level actions, or redesigning Emby's native action sheet.

## Capabilities

### New Capabilities

- `smart-match-menu-integration`: Defines where Series, Season, Episode, and Movie smart-match actions appear, how their item context is resolved, how Episode source numbers are selected, and how manual-search defaults are initialized.
- `movie-danmu-matching`: Defines cross-provider movie preview, confidence-gated selection, manual binding, and tracked single-movie danmu download behavior.

### Modified Capabilities

- None.

## Impact

- Frontend: `Frontend/DanmuSmartMatch.CustomCssJS.js` action-sheet detection, item-type labels, dialog rendering, and progress handling.
- API/model: smart-match preview, binding, and tracked-download request/response contracts must represent Movies and Episodes without pretending either is a Season task.
- Backend: `DanmuController`, match search/scoring helpers, provider binding, Movie download orchestration, and single-Episode source-number routing.
- Tests/docs: deterministic Movie ranking, Episode mapping, search-default, and type-routing regression coverage plus updated installation/usage documentation.
- No new runtime dependency or breaking API removal is expected; existing Series/Season clients remain compatible.
