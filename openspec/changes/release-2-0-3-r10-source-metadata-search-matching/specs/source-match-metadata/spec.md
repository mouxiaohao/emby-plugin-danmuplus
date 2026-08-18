## Purpose

Defines the provider-neutral metadata contract used to describe an exact source
identifier or an already selected source without weakening exact-match safety.

## ADDED Requirements

### Requirement: Exact identifiers return source metadata without discovery
When a target Episode or Movie has an enabled provider's exact identifier, the system SHALL resolve that identifier through that provider's detail capability and return the available source title, trustworthy work year, and category with the exact-match result. `Year` SHALL mean only the work's premiere, release, or first-broadcast year; provider page publication, upload, acquisition, or availability timestamps MUST NOT populate it. For an Episode, `SourceTitle` SHALL mean the upstream parent media/season title, not the single-episode title; this change SHALL NOT introduce a separate episode-title field unless an existing contract already provides one. The system MUST NOT issue a fuzzy title search to obtain this metadata. A missing optional metadata field MUST NOT invalidate an otherwise downloadable exact identifier.

#### Scenario: Exact Episode identifier has source details
- **WHEN** an Episode's enabled provider identifier resolves to a downloadable source Episode with source title, year, and category information
- **THEN** the preview SHALL report the exact identifier as selected and include the upstream parent media/season title as `SourceTitle` together with the available year and category

#### Scenario: Exact Movie identifier has source details
- **WHEN** a Movie's enabled provider identifier resolves to a downloadable source Movie with source title, year, and category information
- **THEN** the preview SHALL report the exact identifier as selected and include those source metadata values without performing title discovery

#### Scenario: Exact identifier lacks optional source metadata
- **WHEN** an exact identifier resolves to a downloadable source but its provider does not expose one or more metadata values
- **THEN** the preview SHALL retain the exact selection and represent unavailable metadata as unavailable rather than replacing it with local metadata or failing the match

#### Scenario: Exact detail has only provider publication time
- **WHEN** an exact provider detail has no trustworthy work year and there is no server-owned selected-candidate snapshot, but the provider exposes a page publication or upload timestamp
- **THEN** the exact result SHALL keep `Year` null and MUST NOT derive it from that timestamp

### Requirement: Selected source metadata remains attached to mapping output
The system SHALL carry available source title, trustworthy work year, and category from a selected provider candidate through authoritative segment creation and collection reconstruction into the corresponding mapping output so later presentation does not need to repeat provider search or detail lookup. This SHALL apply to automatic selection, manual selection, supplementary-segment selection, and direct temporary binding. During authoritative reconstruction, non-empty exact-detail title/category fields SHALL take precedence; a trustworthy exact work year SHALL take precedence over the server-owned selected-candidate snapshot, otherwise the snapshot work year SHALL fill the field, otherwise year SHALL remain null. Browser-submitted metadata MUST NOT be treated as authoritative.

#### Scenario: Automatic temporary collection is selected
- **WHEN** automatic matching selects a source for an unmatched temporary collection
- **THEN** the returned collection SHALL retain the selected source title and available year/category together with its existing provider and mapping data

#### Scenario: Non-automatic temporary collection is selected
- **WHEN** manual selection, supplementary-segment selection, or direct temporary binding selects a source for a temporary collection
- **THEN** the returned collection SHALL retain the same selected source title and available year/category after segment-to-collection reconstruction

#### Scenario: Candidate year fills a missing detail year
- **WHEN** the server-owned selected-candidate snapshot contains year 2014 and the exact provider detail supplies a non-empty title/category but no year
- **THEN** authoritative reconstruction SHALL retain the exact detail title/category and fill year 2014 from the server-owned snapshot

#### Scenario: Exact detail overrides candidate snapshot field by field
- **WHEN** exact provider detail supplies a trustworthy work year that conflicts with the server-owned candidate snapshot work year
- **THEN** the trustworthy exact work year SHALL be retained while snapshot fallback applies only to other missing fields

#### Scenario: Bilibili publication year does not overwrite Bourne production year
- **WHEN** a server-owned Bilibili Movie candidate snapshot contains the trustworthy work year 2002 but exact detail exposes only a 2023 `publish`/`pub_time` or BVID `pubdate`
- **THEN** authoritative reconstruction SHALL retain year 2002 from the snapshot and MUST NOT replace it with 2023

#### Scenario: Browser metadata is ignored
- **WHEN** a client submits source title, year, or category values that differ from the server-owned evidence
- **THEN** authoritative reconstruction SHALL ignore those values and use only exact detail plus the server-owned candidate snapshot

### Requirement: Movie main-part selection uses verified server-owned evidence
For Movie targets only, the system SHALL keep the parent Movie source identity separate from an optional `PartTitle` and downloadable leaf identity. It SHALL offer manual part selection in preview or after binding only when provider detail supplies more than one de-duplicated, independently downloadable usable leaf after explicitly identifiable non-main content is removed. The default SHALL be the first remaining usable part in stable provider order. Part count or ambiguity MUST NOT alter parent-Movie candidate ranking, confidence, uniqueness, or automatic binding. Provider-authoritative type, flag, and section semantics SHALL take precedence when classifying a unit; conservative title classification MAY be used only as fallback to identify explicit non-main content. Trailer/preview, behind-the-scenes, special, clip, interview, bonus/making-of, and equivalent clearly identified units MUST be excluded before default selection and before manual options are created. When authoritative classification is absent or remaining units cannot be distinguished by language/title/type, the system SHALL retain every independently downloadable unit not explicitly identified as non-main, automatically choose the first, and allow optional selection among the rest. Bilibili independent `ep_id` values SHALL be eligible; Tencent `vid`, Youku version identity, and every other provider leaf SHALL be eligible only when that provider proves a stable independently downloadable identity. No provider SHALL fabricate choices from an unverified collection position or a label that lacks a stable downloadable leaf.

Every selectable part SHALL be represented by server-owned evidence scoped to the target item, provider, parent candidate, and short-lived selection token. A client-visible choice SHALL expose safe presentation metadata such as `PartTitle`, not a raw provider leaf ID. On selection or download, the server SHALL resolve the chosen option back to its registered leaf and MUST reject a mismatched item/provider/candidate/token, a stale token, an explicitly excluded unit, and any unregistered or tampered part identity. The chosen registered leaf SHALL be the download target. This contract MUST NOT change Season or Episode matching, selection, or exact-identifier behavior.

#### Scenario: Mixed Movie detail is filtered before first selection
- **WHEN** provider detail contains usable feature units together with clearly identified trailer/preview, behind-the-scenes, special, clip, interview, bonus, or making-of units
- **THEN** excluded units SHALL be removed before determining the first default part and before constructing manual choices

#### Scenario: All remaining Movie units are indistinguishable
- **WHEN** provider detail returns several verified independently downloadable units, none has authoritative classification, and none is explicitly identifiable as non-main
- **THEN** automatic parent-Movie binding SHALL succeed, the first unit in stable provider order SHALL be the default, and the remaining usable units SHALL be available for optional manual selection

#### Scenario: One usable Movie unit remains
- **WHEN** filtering and de-duplication leave exactly one verified independently downloadable unit not explicitly identified as non-main
- **THEN** the system SHALL use it as the default leaf without presenting a manual selector

#### Scenario: Several usable Movie units remain
- **WHEN** filtering and de-duplication leave more than one verified independently downloadable unit not explicitly identified as non-main
- **THEN** the system SHALL default to the first unit in stable provider order and SHALL expose all remaining usable units as Movie-only manual choices with safe `PartTitle` values

#### Scenario: Selected Movie part downloads its registered leaf
- **WHEN** a user selects a valid non-default Movie part using current evidence scoped to the same item, provider, and parent candidate
- **THEN** the download SHALL use that registered leaf identity while preview presentation keeps the parent source title separate from `PartTitle`

#### Scenario: Tampered or excluded Movie part is rejected
- **WHEN** a client submits a stale token, changes its item/provider/parent candidate scope, supplies an unregistered raw part ID, or attempts to select a filtered non-main unit
- **THEN** the server SHALL reject the selection and MUST NOT download that unit or silently substitute another unit
