## Context

See proposal.md for motivation and the delta specs for behavior. Current exact-identifier resolution returns a download-shaped media object and builds a candidate from local scope metadata; the temporary collection planner similarly reduces the selected candidate to a provider key and opaque candidate ID. Live verification showed that this reduction discards a Dandan candidate year of 2014 when the later detail response has no year. Bilibili shared search consumes only the aggregate `all/v2` media groups; a live `谍影重重` response contained PGC film 3/4/5 but omitted 1/2. Bilibili Movie search also currently replaces the parent media identity with the first usable `ep_id`; the same provider may expose several independently downloadable main parts such as language versions. Other providers contain first-episode Movie fallbacks but do not all prove stable selectable version identities. Current generic normalization strips all punctuation, including `＊`.

## Goals / Non-Goals

**Goals:**

- Introduce an additive `SourceMetadata` value shared by discovery candidates, exact-detail resolution, segments, and collections across automatic, manual, supplementary-segment, and direct temporary binding paths.
- Keep exact identifier resolution and download lookup stable while enriching preview output.
- Make Bilibili typed retrieval bounded, mergeable, diagnosable, and safe under partial upstream failure.
- Add generic fidelity-preserving title evidence alongside the existing loose representation without turning punctuation into fuzzy semantic inference.
- Preserve server-owned candidate metadata as a field-by-field fallback when authoritative detail is incomplete.
- Separate Movie parent identity from verified downloadable main-part identity and support an optional secure Movie-only part choice.

**Non-Goals:**

- No provider-order, download format, persisted binding schema, or duplicate-policy change.
- No unbounded Bilibili crawl, universal title-alias database, or auto-selection of a remaining same-provider tie.
- No Season/Episode part selector, no assumption that every Movie collection member is a version, and no fabricated choice for providers whose stable independently downloadable leaf identity cannot be proven.

## Decisions

### One additive SourceMetadata contract

Add a provider-neutral optional metadata object/value containing title, nullable work year, and category. `Year` means only the work's premiere, release, or first-broadcast year; provider page publication, upload, acquisition, and availability timestamps are not valid substitutes. Populate it from `ScraperSearchInfo` whenever a candidate is selected and make authoritative segment creation plus collection reconstruction the single propagation path for automatic matching, manual selection, supplementary-segment selection, and direct temporary binding. Provider detail adapters enrich this same shape for exact Episode and Movie identifiers; they do not call shared title search. Presentation consumes only title/year and uses existing provider localization.

This is preferred over putting presentation strings directly on segments or re-querying on each render: it preserves structured values, avoids network work after selection, and prevents local metadata from being misrepresented as upstream data.  Optional fields preserve compatibility with legacy providers.

### Merge exact detail with a server-owned candidate snapshot

Extend the existing server-side evidence record to retain a cloned, provider-neutral candidate metadata snapshot under the same item/provider/candidate/token scope already used to prove selection. During authoritative segment or collection reconstruction, merge title, year, and category independently: non-empty exact-detail title/category fields win; a trustworthy exact work year wins over the snapshot, otherwise the server-owned candidate work year fills the field, otherwise year remains null. Never merge browser-submitted metadata. This directly preserves the live Dandan search year of 2014 when detail still supplies title/category but no trustworthy work year. Bilibili detail `publish`/`pub_time`, PGC episode publication timestamps, and BVID `pubdate` are upload/publication metadata and therefore MUST NOT be mapped to `Year`; a Bourne candidate's 2002 work year must not become 2023 merely because the current Bilibili page or part was published in 2023.

Field-by-field merge is preferred over replacing the entire detail object because providers expose uneven fields. Server-owned evidence is preferred over round-tripping metadata through the browser because the latter permits title/year/category forgery.

### Exact lookup metadata follows provider detail paths

Extend the provider-facing detail result (or an adjacent provider-neutral detail metadata method) so `GetMediaEpisode` and `GetMedia` based exact paths can supply their upstream title/trustworthy work year/category. For Episode resolution, define `SourceTitle` as the upstream parent media/season title; do not add a separate single-episode-title field for this change unless one already exists in the contract. The resolver returns the same exact candidate ID and download-shaped media as before, merely attaching detail metadata when available. An unavailable metadata response is non-fatal if the existing exact download identity is usable. If an exact detail path has no trustworthy production-year field and there is no server-owned candidate snapshot, `Year` remains null rather than being inferred from an upload timestamp.

This is preferred over `SearchForApi(local title)`, which can choose the wrong localized title and violates exact-ID semantics.

### Keep Movie parent identity separate from downloadable part identity

Represent the selected Movie as a parent source metadata identity plus an optional `PartTitle`; do not overload source title or candidate identity with a leaf label such as `正片`. Discovery and scoring stay parent-scoped. After a parent candidate is automatically or manually selected, its provider detail adapter may enumerate downloadable leaf units. Remove explicitly recognized non-main units first, then make the default download target the first remaining usable leaf in stable provider order. More than one eligible leaf enables an optional preview/post-binding Movie selector; one eligible leaf remains implicit. Part ambiguity never feeds back into parent candidate scoring, confidence, uniqueness, or automatic binding. If no stable downloadable leaf remains, no selector is fabricated and the provider's pre-existing safe unavailable/failure behavior remains in force.

Bilibili can implement the contract with independently downloadable PGC `ep_id` values. Tencent may expose choices only when each returned unit has an independently downloadable `vid`. Youku may expose choices only when each version has a stable identity accepted by its exact detail/download path. Mgtv, Dandan, Iqiyi, and future providers use the same proof threshold. A collection position, display label, or inferred language alone is not sufficient. This provider-neutral gate lets r10 solve the proven Bilibili case without falsely claiming other providers can or cannot expose versions.

### Exclude explicit non-main units before selection

Filter units before computing the default and before presenting choices. Provider-authoritative content type, flags, badge, and section semantics take precedence. A conservative multilingual title classifier is fallback-only and rejects only clearly identified trailer/preview, behind-the-scenes, special, clip, interview, bonus, making-of, and equivalent non-main terms. If no authoritative classification exists, absence of an exclusion signal keeps a verified independently downloadable unit usable for compatibility; several indistinguishable parts therefore still auto-bind to the first stable-order unit and remain optionally selectable. De-duplicate usable units by verified provider download identity while preserving stable provider order.

Explicit exclusion before `First()` prevents a known trailer or extra from becoming the default. A positive-confirmation-only rule is rejected because weak provider metadata would block otherwise valid Movie automatic binding, contrary to compatibility. Part ambiguity is presentation/download-choice state, not match ambiguity.

### Bind Movie part choices to short-lived server evidence

Register each eligible leaf under a short-lived server-owned evidence snapshot scoped to local Movie item, provider, parent candidate, parent selection token, and an opaque part-choice token. Public payloads may include `PartTitle` and the opaque choice token but not raw provider leaf IDs. Selection and download revalidate the full scope and resolve the registered raw leaf server-side. Reject stale tokens, cross-item/provider/candidate reuse, unregistered raw IDs, and units excluded by classification; do not silently fall back after an invalid explicit choice.

This reuses the existing evidence-boundary pattern rather than trusting a client-supplied `ep_id`/`vid`. It also permits the default first eligible leaf and a manual choice to share one authoritative resolution path.

### Bilibili aggregate plus typed, bounded retrieval

Keep aggregate `all/v2` as an initial low-cost source, then request typed `media_ft` and `media_bangumi` pages with a fixed page/record budget and the established Bilibili session/header strategy. Map every response through the existing usable-ID mapper and deduplicate by canonical provider media identity. Return a shared result contract (or equivalent shared channel) containing both merged candidates and per-request diagnostics, and have both Movie and Season shared-search paths consume it. Individual typed-page errors append diagnostics without discarding candidates already collected from aggregate or successful typed pages; cross-provider aggregation likewise preserves other providers. A request-wide aggregate/session failure retains existing provider-failure behavior.

Typed retrieval is preferred over raising the aggregate display limit because the live omission occurs in the aggregate's result composition, not downstream candidate truncation.  Bounded pages avoid latency/rate-limit amplification.

### Compare titles through loose and fidelity channels

Retain the current loose normalization, which folds punctuation for recall and base scoring. Alongside it, derive a fidelity form using Unicode NFKC compatibility normalization and the established case/whitespace normalization while preserving every remaining punctuation and symbol code point by type, count, and order. Apply both channels uniformly to primary titles, aliases, and applicable parent-media/season titles on both local and source sides. Do not subject one-character or symbol-only suffixes to the minimum-length filtering used for loose aliases/tokens; they remain part of their containing fidelity title.

Only compare fidelity forms as additional positive evidence after the corresponding loose forms match. An exact fidelity match participates in the final unique-highest decision; a mismatch neither subtracts score nor blocks the existing loose match. NFKC supplies generic width equivalence such as `＊` and `*`, while sequences such as `!`, `!!`, and arbitrary previously unseen combinations remain distinguishable. If fidelity evidence does not produce a unique highest candidate, preserve ambiguity and never choose by source order.

This is preferred over symbol-specific classification because titles may use arbitrary punctuation without stable sequel semantics, and over disabling punctuation normalization because loose recall must continue tolerating presentation differences. The fidelity channel compares exact normalized form only; it does not infer that any symbol denotes a sequel or otherwise expand fuzzy matching.

## Risks / Trade-offs

- [Bilibili typed endpoint blocks or changes contracts] → Reuse current session/header acquisition, bound requests, retain aggregate results, record diagnostic evidence, and verify live before release.
- [Provider detail exposes publication time but not production year] → Treat `Year` as optional, reject publication/upload timestamps (including Bilibili `publish`/`pub_time`/`pubdate`), prefer a trustworthy exact work year over the server-owned snapshot, and preserve the existing downloadable exact path when neither exists.
- [Metadata leaks opaque IDs] → Model only title/year/category as public fields and extend frontend assertions against IDs/evidence/provider keys.
- [Movie part selection leaks raw leaf IDs or accepts tampering] → Expose only safe `PartTitle` plus opaque scoped tokens; resolve raw leaf identity from short-lived server evidence and fail closed on mismatch/staleness.
- [A trailer or bonus item becomes the default first part] → Prefer authoritative provider semantics, conservatively recognize explicit non-main content, and filter before any first-item selection.
- [Weak classification metadata blocks a valid Movie match] → Keep independently downloadable units usable unless explicitly identified as non-main; select the first stable-order unit and keep part ambiguity outside candidate matching.
- [A provider appears to have several versions but cannot download them independently] → Keep the selector disabled and retain the existing safe fallback until stable leaf identity is proven by deterministic/provider fixtures.
- [Candidate snapshot overwrites newer exact metadata] → Merge per field with non-empty exact detail always taking precedence.
- [Fidelity evidence overfits presentation punctuation] → Make it positive-only after a loose match, apply no mismatch penalty, and test arbitrary symbol sequences plus a true unresolved tie.
- [Additional Bilibili calls increase latency] → Apply a strict page budget, per-query de-duplication, cancellation/timeout behavior, and no repeated render-time calls.

## Migration Plan

1. Implement additive models and adapters without changing persisted identifiers or task schema semantics.
2. Run deterministic C# and CustomCssJS regression tests, including mixed extras, all-indistinguishable, single/multiple usable-part fixtures, evidence-tampering rejection, candidate-snapshot fallback, and unchanged Season/Episode behavior, then a Release build.
3. Back up the deployed r9 DLL/assets; deploy r10 and live-test temporary collection metadata, exact Episode/Movie metadata, Bilibili `谍影重重` films 1–5, a Bilibili Movie default/manual part choice where the provider exposes several usable parts after explicit non-main filtering, and `妄想学生会＊` automatic selection. Verify genuine-tie behavior only with deterministic fixtures, not a live title.
4. Record DLL/asset hashes and release notes.  Roll back by restoring the r9 DLL/assets and restarting Emby; no metadata migration is required.
