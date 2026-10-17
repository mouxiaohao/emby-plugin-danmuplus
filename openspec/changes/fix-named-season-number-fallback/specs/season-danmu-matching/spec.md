## ADDED Requirements

### Requirement: Staged named-season ordinal fallback
The system SHALL prefer a confident descriptive Season-name match. If complete ordinary discovery and existing alias matching produce neither a selected candidate nor unresolved high-confidence ambiguity under the existing ordinary or TMDB-alias rules, it SHALL retry standard Season matching using the target Emby Season's authoritative positive ordinal. The retry SHALL retain parent-title evidence, year, provider ordering, and existing confidence rules, but MUST NOT retain descriptive Season aliases as retry-stage Season identity. The stored Emby metadata MUST remain unchanged. Interactive Season discovery and automatic import discovery SHALL share this behavior.

#### Scenario: Provider has only a standard Season label
- **WHEN** an Emby Season has a descriptive name and ordinal 2 and complete named discovery cannot confirm a candidate while a provider exposes the matching series as Season 2
- **THEN** ordinary standard-Season matching SHALL evaluate that candidate without the unavailable descriptive label blocking Season evidence

#### Scenario: Named match already succeeds
- **WHEN** named discovery selects a candidate under existing confidence rules
- **THEN** the system MUST NOT perform or mix in the numbered fallback stage

#### Scenario: Named candidates remain strongly ambiguous
- **WHEN** multiple high-confidence named candidates cannot be resolved by existing selection rules
- **THEN** the system SHALL preserve manual selection rather than bypass ambiguity with numbered fallback

#### Scenario: Metadata or execution does not authorize fallback
- **WHEN** the target Season ordinal is missing or nonpositive, the request uses an explicit keyword, or named discovery is cancelled or incomplete
- **THEN** the system MUST NOT infer an ordinal or relax matching through numbered fallback

### Requirement: Numbered fallback preserves composite and logical continuation
A successful numbered fallback SHALL feed the existing authoritative composite planning operation with the full de-duplicated canonical candidate pool and original local inventory. It MUST NOT limit a physical Season to one provider Season, rewrite confirmed mappings, reset Part or logical-season progression, or replace an explicit logical-season override with the physical Season ordinal.

#### Scenario: Bookworm physical first Season covers three source Seasons
- **WHEN** a descriptive physical Season 1 falls back to source Season 1 and verified mapping leaves a unique unmatched suffix
- **THEN** existing remainder rules SHALL remain able to map source Seasons 2 and 3 without resetting to physical Season 1

#### Scenario: Whole-Series continuation has advanced past the physical ordinal
- **WHEN** an eligible whole-Series operation has completely mapped physical Season 1 to logical Seasons 1 through 3 and advances to physical Season 2
- **THEN** its proven logical Season 4 and locked provider SHALL remain authoritative and MUST NOT fall back to physical Season 2

#### Scenario: Existing recursion safety boundaries still apply
- **WHEN** remainder candidates are incomplete or ambiguous or a proposed mapping makes no valid progress
- **THEN** the system SHALL retain the last confirmed mapping and leave unmatched Episodes for manual action

#### Scenario: Retry aliases do not promote first-stage-only evidence
- **WHEN** the standard retry uses the existing TMDB alias confidence threshold and a candidate was observed only during unsuccessful named discovery
- **THEN** that candidate SHALL remain available for manual or remainder discovery but MUST NOT become automatically eligible solely because of the retry-stage alias threshold
