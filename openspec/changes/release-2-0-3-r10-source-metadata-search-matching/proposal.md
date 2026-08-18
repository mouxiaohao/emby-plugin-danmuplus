## Why

The r9 smart-match contract loses source title/year after an automatic temporary-season selection, leaves exact Episode/Movie identifiers without source metadata, and Bilibili's aggregate search omits older Bourne films.  In addition, punctuation folding makes `妄想学生会` and `妄想学生会＊` indistinguishable, producing an avoidable same-provider tie. Live r10 verification also exposed two remaining identity gaps: candidate year can disappear when a temporary collection is rebuilt from a poorer detail response, and a Movie with several independently downloadable main-feature parts is silently reduced to one leaf without showing which version was chosen.

## What Changes

- Introduce one provider-neutral source-metadata contract for selected candidates, including display title, category, and a year that means only the work's premiere, release, or first-broadcast year. Provider page publication, upload, acquisition, and availability timestamps are not source years.
- Preserve that metadata through the authoritative segment-to-collection path for automatic, manual, supplementary-segment, and direct temporary bindings. Exact detail non-empty title/category fields take precedence; for year, a trustworthy exact work year takes precedence, otherwise the server-owned selected-candidate snapshot fills the field, otherwise it remains unavailable. Bilibili detail `publish`/`pub_time` and BVID `pubdate` values MUST NOT populate `Year`. Localized source title/year is rendered without exposing provider/internal identifiers or trusting browser-submitted metadata.
- Resolve exact target Episode and Movie identifiers through the owning provider's detail path and return source title, trustworthy work year when available, and category; for Episodes, source title means the upstream parent media/season title rather than the single-episode title. Exact identifiers remain authoritative and MUST NOT trigger fuzzy discovery.
- Add a provider-neutral, Movie-only optional main-part selector. A Movie candidate retains its parent movie identity; only providers that return more than one verified, independently downloadable usable leaf after explicit non-main filtering expose choices. The default remains the first remaining usable part in stable provider order, while an explicit preview/post-binding choice downloads the selected leaf. Part ambiguity MUST NOT block or weaken automatic parent-Movie binding. Bilibili uses its independent `ep_id`; Tencent, Youku, and every other provider participate only when stable downloadable leaf identity is proven. Clearly identified trailer/preview, behind-the-scenes, special, clip, interview, bonus/making-of units are excluded before both default selection and option construction, using authoritative provider flags/types/section semantics first and conservative title classification only as fallback. When no authoritative classification exists, only explicitly recognizable non-main units are removed; indistinguishable remaining parts stay usable.
- Separate parent `SourceMetadata` from optional `PartTitle` presentation, display Movie candidates as localized provider plus parent title/year and selected part when available, de-duplicate choices, and keep raw provider IDs and evidence tokens hidden. Bind every selectable part to a short-lived server-owned evidence record scoped to item, provider, parent candidate, and token; reject tampered, excluded, or stale selections.
- Supplement Bilibili shared search with typed `media_ft` and `media_bangumi` retrieval, bounded pagination, per-query de-duplication, and a shared candidates-plus-diagnostics result contract consumed by both Movie and Season search paths, so partial typed-page failure preserves successful results and aggregate truncation cannot hide valid films such as Bourne 1 and 2.
- Compare every local, alias, source, and parent-media/season title through two generic normalization channels: a loose form that keeps existing punctuation-insensitive recall/base scoring, and a fidelity form that applies Unicode compatibility normalization plus case/whitespace normalization while preserving every punctuation/symbol type, count, and order. When loose forms match, an exact fidelity match is positive evidence in the unique-highest decision (including full-width `＊` versus ASCII `*` through NFKC), while a fidelity mismatch is not a penalty; single-character or symbol-only suffixes remain available to fidelity comparison, and genuinely equal candidates remain manual-only. This is generic comparison evidence, not symbol-specific classification or fuzzy inference about sequel meaning.
- Add deterministic and live Emby regression coverage, package release `2.0.3r10`, record hashes/release notes, and retain an r9 rollback path.

Non-goals: changing provider order, general fuzzy-search scoring weights, saved-binding precedence, download formats, adding a part selector to Season/Episode matching, inventing part choices when a provider cannot prove stable downloadable leaves, or automatically resolving a true same-site/same-score ambiguity.

## Capabilities

### New Capabilities

- `source-match-metadata`: provider-neutral source metadata returned for exact Episode/Movie identifiers and selected mappings, server-owned metadata fallback, and verified Movie-only main-part identity.

### Modified Capabilities

- `season-danmu-matching`: complete Bilibili season/movie discovery, preserve parent Movie candidate identity while resolving usable parts after explicit non-main filtering, and apply generic fidelity-preserving title evidence without weakening punctuation-insensitive recall or ambiguous-match safety.
- `smart-match-error-and-presentation`: successful temporary-season cards display safe source title/year metadata, and Movie candidates optionally display a safe selected-part choice without changing Season/Episode UI.

## Impact

- Affected backend: provider resolver, shared match scorer/search engine, Bilibili API/mapper, provider Movie detail adapters, server-owned evidence registry, season collection planner, response models, and controller preview/download routes.
- Affected UI: CustomCssJS collection-card presentation and Movie-only optional part selection; raw candidate/part/provider/evidence IDs remain hidden.
- Affected tests: C# deterministic resolver/scorer/Bilibili/collection/evidence/classification tests, JavaScript rendering tests, release build, and narrowly scoped live Emby verification; genuine-tie behavior remains a deterministic-only test.
- Compatibility: C# 8/.NET Standard 2.0; preserve provider download paths, STRM support, persisted bindings, duplicate/partial/retry behavior, and safe fallback when metadata or a typed Bilibili page is unavailable.
