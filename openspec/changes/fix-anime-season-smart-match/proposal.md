## Why

Season matching currently treats a parent Series provider identifier as a fallback for every child Season and Episode. A Series-level Dandanplay identifier that points to one season can therefore make all seasons resolve to that same provider object; meanwhile alternative localized titles are missed, Dandanplay sequel numbering can reject every local episode, and successful season downloads never persist the selected season media identifier.

## What Changes

- **BREAKING**: stop reading or writing danmu-provider identifiers on Series objects for Season and Episode matching. A Season exact match uses only that Season's identifier; an Episode uses its own identifier first and may fall back only to its containing Season.
- Define one Bilibili PGC-only external-ID contract: a Season owns `season_id`, while a Movie and an Episode own `ep_id`; Series does not participate in Bilibili identifier matching, and `aid,cid` is derived only inside the backend when requesting danmu.
- Expose Bilibili and Mgtv external-identifier fields on the Emby item types supported by their exact-ID contracts without making Series metadata a matching fallback.
- Add conservative, de-duplicated title-clause search rounds after the complete local title so localized titles with a shared distinctive subtitle can be discovered without a hard-coded per-title alias dictionary.
- Normalize Dandanplay episodes returned by a standalone season/Anime detail response to season-local ordinal numbers while retaining each stable EpisodeId for downloads and Episode metadata.
- After the first accepted successful or partial episode download, persist the verified provider media identifier (for Dandanplay, AnimeId) on the Season; continue persisting provider episode identifiers on Episode items. All-failed and skipped-only tasks do not update the Season identifier.
- Make manual season selections durable through the same validated task path and protect Season writes from older concurrent tasks overwriting a newer selection.
- Preserve explicit rematch, configured provider priority, scoring, duplicate-file skipping, XML output, retry, and other-provider behavior.
- Non-goals: infer arbitrary aliases that are absent from local metadata and share no searchable title clause; migrate, delete, or reinterpret existing Series provider identifiers; delete existing XML files.

## Capabilities

### New Capabilities

- `provider-id-scope`: Defines item-local provider identifier ownership and allowed Season/Episode fallback boundaries.
- `season-provider-id-persistence`: Defines success-gated, concurrency-safe persistence of selected season media identifiers independently from episode identifiers.

### Modified Capabilities

- `season-danmu-matching`: Adds conservative title-clause discovery and removes parent Series provider identifiers from Season matching.
- `main-episode-selection`: Normalizes provider-global sequel numbering into stable season-local episode mappings for standalone season detail responses.

## Impact

- Provider-ID resolver scopes used by preview and automatic library processing.
- Bilibili exact PGC resolution, download-time `ep_id -> aid,cid` conversion, and Emby external-ID field registration for Bilibili/Mgtv.
- Shared season keyword generation/search rounds and deterministic scoring regressions.
- Dandanplay detail adaptation and episode-number mapping.
- Tracked season download/manual-binding flow, success persistence, generation protection, and automatic import parity.
- Frontend behavior remains a display/interaction client; no frontend scoring or provider inference is introduced.
- Live validation will use the four seasons of “爱书的下克上” as a regression case while keeping all rules title- and media-agnostic.
