## MODIFIED Requirements

### Requirement: Logical-season-safe positional mapping
The system MUST restrict every batch Season candidate's virtual Episode mapping to the target Season item's own inventory records that have a valid ItemId and a `ParentIndexNumber` exactly equal to the target Season's `IndexNumber`. Episodes from another parent season or an unknown parent season SHALL be out of scope: they MUST NOT become mappings, unmatched runs, temporary seasons, supplemental selections, downloads, or completeness inputs for that target. A standalone explicitly targeted Season 0 operation SHALL apply the same equality rule to the real Season 0 item's own inventory. Existing local Episode ProviderIds are not smart-match evidence.

Every applied virtual segment MAY contain multiple ordered segment windows. A window is a maximal run resolved for the selected source before an in-scope local ItemId that is already mapped by the current server plan to a different source. That different-source mapping SHALL remain authoritative, SHALL bound the adjacent windows, and SHALL NOT consume a coordinate or stable ordinal from the continuing source. Records removed by the target-Season scope filter SHALL NOT form boundaries. An already verified mapping to the selected source MAY remain inside a window as direct evidence when its exact identity and that window's alignment are consistent. Any real numeric gaps adjacent to a boundary SHALL advance the continuing-source frontier exactly once, while the boundary ItemId itself advances it zero times. Continuing the selected source after a boundary is part of the same virtual source application and SHALL NOT trigger unattended residual-source discovery.

Every segment window SHALL use exactly one server-selected alignment mode. When every participating local Episode in that window has a unique positive explicit Episode number and every verified source Episode participating in that window has a unique positive provider-supplied Episode number, the window SHALL use number-aware alignment. A default first window whose source start was not explicitly changed SHALL use zero numeric offset (`source number = local number`). A window with an explicit trusted local/source anchor SHALL use the affine offset defined by those exact anchors (`source number = source anchor number + local number - local anchor number`). A user-selected source anchor SHALL override the default first-window zero offset. A later window SHALL use its first eligible local ItemId and the preceding window's outgoing source frontier as a server-derived exact anchor; its mode and affine offset MAY differ from the preceding window's mode and offset.

Number-aware alignment SHALL preserve gaps within each window. A source coordinate for which no eligible local ItemId exists remains unused; an eligible local coordinate for which no verified source Episode exists remains unmatched; neither condition may shift a later mapping in that window. Numeric gaps SHALL contribute to the effective outgoing cutoff even when they produce no mapping; positional continuation SHALL advance by considered local rows, never applied mappings. A different-source boundary SHALL advance neither frontier. When one server operation internally continues the same source across windows, it MUST carry that derived cutoff forward and MUST NOT reuse a source coordinate or ordinal behind it. When production windows are separate submitted selections, each selection's validated exact `SourceStartEpisodeId` SHALL be its incoming frontier; a separate serialized frontier property is not required. Source exhaustion SHALL be derived from the effective frontier rather than applied-mapping count. The requested Episode count retains its authoritative-local-row limit and does not become a numeric coordinate span or an applied-mapping count.

Existing server-verified mappings to the selected source inside one window MAY establish or verify that window's affine offset. If such mappings imply conflicting offsets inside the same window, planning SHALL fail closed. No global affine offset SHALL be imposed across a different-source boundary.

If either participating side contains a missing, zero, negative, or duplicate Episode number, the whole window SHALL use stable positional fallback from its exact local and source anchors. Numeric and positional pairing MUST NOT be mixed within one window. Empty or duplicate source Episode identities, missing CommentIds, out-of-scope local ItemIds, overlaps, and duplicate exact mappings remain structural failures and MUST NOT be converted into positional fallback.

`SourceStartEpisodeId` SHALL be the authoritative source anchor whenever present. A legacy number-only source start MAY resolve only when the entire verified source scope has reliable unique positive provider-supplied numbering and the requested number uniquely identifies one source Episode; otherwise the request SHALL fail closed and MUST NOT reinterpret that number as a one-based list position.

#### Scenario: One Punch Man has main Episodes and placed specials
- **WHEN** S1 contains S01E01-S01E12 and displayed S00E01-S00E07 and the user selects a 12-Episode S1 source
- **THEN** the source SHALL map exactly the twelve Parent 1 Episodes and the seven Parent 0 Episodes SHALL remain out of scope without creating an unmatched or temporary run

#### Scenario: Selected source claims 19 Episodes
- **WHEN** the same S1 display structure is matched to an ordinary upstream S1 candidate reporting 19 Episodes
- **THEN** mapping SHALL still stop after the twelve eligible Parent 1 Episodes and SHALL NOT consume or expose the Parent 0 Episodes as supplemental work

#### Scenario: Special source is selected
- **WHEN** the user explicitly targets the real Season 0 item and selects a verified special or OVA source
- **THEN** only valid Parent 0 ItemIds from that Season 0 item's own inventory SHALL be eligible for mapping, and the system SHALL not change any Episode's Emby Season membership or numbering

#### Scenario: Foreign normal-season Episode is displayed in the target
- **WHEN** an S2 display contains an Episode whose `ParentIndexNumber` is 1 or 3
- **THEN** that Episode SHALL be out of scope exactly like a Parent 0 or unknown-parent Episode and SHALL not create a supplemental selection or interrupt the eligible Parent 2 mapping

#### Scenario: Default first segment sees sparse local numbering
- **WHEN** the eligible local inventory is E1-E6 plus E10-E13, the verified source has explicit unique E1-E13 numbering, and the user has not changed the first source start
- **THEN** local E1-E6 SHALL map source E1-E6, local E10-E13 SHALL map source E10-E13, and source E7-E9 SHALL remain unused rather than shifting local E10 to source E7

#### Scenario: Explicit anchor overrides the default first-segment offset
- **WHEN** the user maps a first segment beginning at local E1 and explicitly selects source E5 as its source anchor
- **THEN** local E1 SHALL map source E5 and each later numbered local Episode SHALL preserve that anchor delta

#### Scenario: Anchored segment contains a missing local Episode
- **WHEN** a segment is explicitly anchored local E29 to source E1, local E30 is absent, local E31 exists, and the source has explicit unique E1-E3 numbering
- **THEN** local E29 SHALL map source E1, source E2 SHALL remain unused, and local E31 SHALL map source E3

#### Scenario: A different-source mapping splits a continuing source into windows
- **WHEN** one selected source has E1-E10, local E29-E33 map that source's E1-E5, local E34 is already mapped by the current server plan to a different special source, and local E35-E39 remain eligible for the selected source
- **THEN** local E34 SHALL be a window boundary that consumes none of the continuing source, the next window SHALL map local E35-E39 to source E6-E10, and that source SHALL be exhausted

#### Scenario: A real gap advances the frontier before a different-source boundary
- **WHEN** a numeric window is anchored local E29 to source E1, local E30 is absent, local E31 exists, local E32 is mapped to a different source, and the selected source continues at local E33
- **THEN** local E31 SHALL map source E3, source E2 SHALL remain deliberately unused behind the outgoing frontier, local E32 SHALL consume no selected-source coordinate, and the next window SHALL begin local E33 at source E4

#### Scenario: Numbered source has an internal gap
- **WHEN** a segment is anchored local E29 to source E1, local E29-E31 exist, and the verified source has explicit unique E1 and E3 but no E2
- **THEN** local E29 SHALL map source E1, local E30 SHALL remain unmatched, and local E31 SHALL map source E3 without positional compression

#### Scenario: Numbering is unreliable for one participating side
- **WHEN** either the participating local rows or participating verified source rows for one window contain a null, zero, negative, or duplicate Episode number
- **THEN** the entire affected segment window SHALL use deterministic positional pairing from its exact anchors and SHALL NOT mix positional and numeric results inside that window

#### Scenario: Legacy source number is not uniquely authoritative
- **WHEN** a selection omits `SourceStartEpisodeId` and its number-only source start is missing, duplicated, not provider-supplied, or belongs to a source scope containing another null, zero, negative, or duplicate number
- **THEN** the request SHALL fail without mapping that number to a list ordinal or writing any file or metadata

#### Scenario: Excluded foreign Episodes have unreliable numbering
- **WHEN** an S1 target has reliable unique Parent 1 coordinates but its display inventory also contains excluded Parent 0 or foreign Episodes with null or duplicate numbers
- **THEN** only the eligible S1 mapping unit SHALL determine numeric reliability and excluded records SHALL NOT force positional fallback

## ADDED Requirements

### Requirement: Alignment provenance is versioned and revalidated
The authoritative plan generation SHALL cover the canonical facts that uniquely determine window boundaries and effective frontiers: mapping protocol version, selection order, alignment intent/mode, exact anchors, ordered considered local ItemIds, target numbering/order, complete source identity/numbering/order, and final exact mappings. A distinct serialized frontier field is not required. Preview, confirmation, automatic positive-Season processing, and download-time rebuild MUST resolve the same windows and alignment from the same authoritative facts. A rebuild that changes any frontier-determining fact or exact mapping SHALL be stale and SHALL perform zero download or metadata writes.

Already frozen retry and seven-day replay entries SHALL continue to use their captured exact local ItemId, provider/media identity, source EpisodeId, and non-empty CommentId tuple. They MUST NOT be silently realigned by current numbering or source list position. When a replay path revalidates provider details, a missing exact source EpisodeId or a changed CommentId SHALL make the entry stale; the system MUST NOT substitute the current CommentId. Existing XML files created by an older incorrect mapping SHALL not be automatically deleted or rewritten.

#### Scenario: Source numbering changes after preview
- **WHEN** a provider detail response changes an Episode number, explicit-number availability, duplicate status, or source anchor after the user confirms a preview
- **THEN** download-time rebuild SHALL reject the stale plan and perform zero XML and metadata writes

#### Scenario: A segment boundary or frontier changes after preview
- **WHEN** download-time reconstruction moves or removes a different-source boundary, or changes any boundary- or frontier-determining anchor, considered span, target inventory, or source inventory/order from the confirmed preview
- **THEN** download-time rebuild SHALL reject the stale plan and SHALL NOT reuse a bypassed source coordinate or perform any XML or metadata write

#### Scenario: Old mapping protocol draft is submitted
- **WHEN** a browser submits a virtual-segment draft from the pre-alignment mapping protocol
- **THEN** the server SHALL require a fresh preview and SHALL NOT reinterpret the old selection under the new alignment rules

#### Scenario: Frozen retry sees reordered source details
- **WHEN** an existing tracked entry retries after the provider reorders or renumbers its source list but the captured exact source EpisodeId remains valid
- **THEN** retry SHALL keep the captured exact pair; if that exact identity is no longer valid, retry SHALL fail rather than select a replacement by number or position

#### Scenario: Frozen retry sees a different CommentId
- **WHEN** a replay path revalidates the captured source EpisodeId and the provider now reports a different CommentId
- **THEN** the entry SHALL fail stale and SHALL NOT substitute the changed CommentId or another Episode

#### Scenario: Incorrect older XML already exists
- **WHEN** an earlier version wrote an XML file from a compressed sparse mapping
- **THEN** installing this change SHALL not delete or rewrite that file until the user separately requests an eligible force refresh
