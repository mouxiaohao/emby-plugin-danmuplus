## ADDED Requirements

### Requirement: Matched temporary collections show safe source identity
The smart-match collection overview SHALL show a successfully matched temporary collection's localized provider name, source title, and available source work year. The displayed year SHALL mean only premiere/release/first-broadcast year and MUST NOT substitute a provider page publication/upload time. Automatic matching, manual selection, supplementary-segment selection, and direct temporary binding SHALL all preserve the selected source metadata through the authoritative segment-to-collection reconstruction path and SHALL render it consistently. It MUST NOT show provider keys, candidate IDs, internal local/source IDs, evidence tokens, or raw internal decision codes.

#### Scenario: Automatic temporary collection has title and year
- **WHEN** automatic matching binds a temporary collection to a source titled `来源标题` from year 2024
- **THEN** its collection card SHALL show the localized provider, `来源标题`, and 2024 together with the local and temporary episode ranges

#### Scenario: Temporary collection has no source year
- **WHEN** a successfully matched temporary collection has a source title but no source year
- **THEN** its card SHALL show the source title and omit or localize the unavailable year without presenting local-season year as source metadata

#### Scenario: Every binding entry point preserves source identity
- **WHEN** a temporary collection is bound by automatic matching, manual selection, supplementary-segment selection, or direct temporary binding
- **THEN** segment-to-collection reconstruction SHALL retain and display the selected source title and available source year for that binding

### Requirement: Movie part choices are optional, safe, and distinct from source identity
The smart-match UI SHALL show a Movie-only part selector in preview or after binding only when the selected provider has returned more than one verified, independently downloadable usable part for the same parent Movie after explicitly identifiable non-main units have been removed. Each choice SHALL expose a safe `PartTitle` and SHALL keep raw provider part IDs, parent candidate IDs, and evidence tokens out of displayed text. The Movie candidate heading SHALL use the localized provider and parent source title with available source year/category, and SHALL append the selected `PartTitle` only as a distinct part label. The selector is an optional post-match leaf change and MUST NOT participate in parent-Movie candidate ranking, confidence, or ambiguity. The UI MUST NOT add this selector to Season or Episode candidates and MUST NOT fabricate options when the provider cannot prove a stable downloadable part identity.

#### Scenario: Movie has several usable parts
- **WHEN** a bound or previewed Movie candidate has two or more verified, independently downloadable usable parts after explicit non-main filtering
- **THEN** the UI SHALL show de-duplicated safe part choices, preselect the first usable part in stable provider order, and display localized provider, parent source title/year, and selected `PartTitle` without showing raw IDs

#### Scenario: Movie has only one usable part
- **WHEN** a Movie candidate has exactly one usable independently downloadable part after filtering and de-duplication
- **THEN** the UI SHALL retain that part as the default download target without showing a redundant selector

#### Scenario: Indistinguishable parts remain selectable after automatic binding
- **WHEN** several remaining usable Movie parts lack enough metadata to distinguish language or version but are not explicitly identifiable as non-main
- **THEN** automatic Movie binding SHALL still succeed with the first stable-order part and the UI SHALL offer the remaining usable parts for optional manual change

#### Scenario: Provider cannot prove selectable parts
- **WHEN** a provider returns no usable part after explicit non-main filtering or cannot prove that returned units have stable independently downloadable identities
- **THEN** the UI SHALL show no fabricated part choices and SHALL preserve the provider's existing safe fallback behavior

#### Scenario: Season and Episode never show Movie part controls
- **WHEN** a Season or Episode candidate is rendered
- **THEN** the UI SHALL not render or submit Movie part-selection state
