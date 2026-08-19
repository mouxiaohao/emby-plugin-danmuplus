## ADDED Requirements

### Requirement: Season title components use continuous normalized edit similarity
Season matching SHALL calculate parent-series and Season-name title similarity with unit-cost Levenshtein edit distance over the existing loosely normalized character sequences. For two non-empty comparison values `a` and `b`, similarity SHALL be `1 - distance(a,b) / max(length(a), length(b))`, bounded to `0..1`; insertions, deletions, and substitutions SHALL each cost one. The parent-title contribution SHALL be `60 * parent similarity` points and the Season-name contribution SHALL be `20 * Season similarity` points. Eligible local titles, aliases, source-title channels, and same-channel best-evidence selection SHALL retain their existing participation rules and MUST NOT be added together across alternatives.

An exact normalized parent span contained in a source title SHALL continue to receive full parent-title evidence and to define that source title's Season-name residual. A source title with no exact parent span SHALL be eligible for partial parent-title evidence rather than receiving an unconditional zero, but approximate parent evidence MUST NOT remove an invented source span before Season-name comparison.

Before a correct generic Season marker is excluded from a named Season comparison, every explicit marker in the original normalized pair SHALL be checked against the expected Season. If the pair is non-conflicting and marker removal leaves descriptive Season text on either side, the two descriptive residuals SHALL be compared, including an empty residual on the pure-marker side, without allowing shared marker text to inflate the result. Only when neither side has descriptive text SHALL the established pure-generic-label behavior remain available. No connector or lexical item, including `之`, SHALL be deleted by a language-specific rule. A conflicting explicit marker SHALL continue to produce zero Season-name evidence. Empty parent values and one-empty/one-non-empty Season residuals SHALL produce zero similarity; the established Season 1 implicit empty-residual exception and strict complete-title fallback SHALL remain unchanged.

#### Scenario: Exact parent and Season titles retain full credit
- **WHEN** the normalized parent title and named Season residual are each exactly equal and the known year is exactly equal
- **THEN** parent evidence SHALL contribute 60 points, Season-name evidence SHALL contribute 20 points, year evidence SHALL contribute 20 points, and the ordinary score SHALL be 100 points

#### Scenario: Short prefix insertion receives proportional Season credit
- **WHEN** the local parent is `唐朝诡事录`, local Season 2 title is `第 2 季：西行`, the source title is `唐朝诡事录之西行`, and the known years are equal
- **THEN** the parent SHALL contribute 60 points, the correct local Season marker SHALL be excluded from the descriptive residual, `西行` / `之西行` SHALL have similarity `2/3`, the Season name SHALL contribute approximately `13.33` points, and the ordinary score SHALL be approximately `93.33` points

#### Scenario: Related parent titles receive proportional parent credit
- **WHEN** the local parent is `爱书的下克上：为了成为图书管理员不择手段！` and a source parent title is `小书痴的下克上 〜为了成为图书管理员而不择手段〜`
- **THEN** loose normalization SHALL produce lengths 19 and 21 with edit distance 3, parent similarity SHALL be `6/7`, and the parent-title component SHALL contribute approximately `51.43` of 60 points

#### Scenario: One substitution remains halfway similar
- **WHEN** two descriptive Season residuals are `西行` and `东行`
- **THEN** their edit distance SHALL be 1, their similarity SHALL be `1/2`, and the Season-name component SHALL contribute 10 of 20 points

#### Scenario: Reordered or disjoint two-character names receive zero
- **WHEN** `西行` is compared with `行西` or with `北斗`
- **THEN** unit-cost Levenshtein distance SHALL equal the maximum input length, similarity SHALL be zero, and the Season-name component SHALL contribute zero points

#### Scenario: Correct markers do not hide different names
- **WHEN** expected Season 2 compares `第2季：西行` with `Season 2 东行`
- **THEN** marker conflict checking SHALL complete before removal, the descriptive comparison SHALL be `西行` / `东行`, and the common correct Season number MUST NOT raise the Season-name component above 10 points

#### Scenario: Pure marker does not match a named Season
- **WHEN** expected Season 2 compares the pure generic label `第2季` with `第2季：西行`
- **THEN** the descriptive comparison SHALL be empty / `西行`, similarity SHALL be zero, and the shared correct marker MUST NOT supply Season-name points

#### Scenario: Conflicting marker remains zero Season evidence
- **WHEN** expected Season 2 compares against a source residual containing an explicit Season 3 marker
- **THEN** the Season-name similarity and Season-name component SHALL be zero under the existing conflict rule

#### Scenario: Empty residual rules remain bounded
- **WHEN** one descriptive Season residual is empty and the other is non-empty, or both are empty outside the established Season 1 implicit-empty case
- **THEN** continuous edit similarity SHALL contribute zero Season-name points

### Requirement: Continuous evidence preserves existing selection policy
Candidate eligibility, ordering, confidence classification, and automatic selection SHALL consume the new continuous component values without changing their existing thresholds or comparison operators. Ordinary automatic confidence SHALL remain inclusive at `MatchScore >= 0.90`, the separate TMDB-alias acceptance threshold SHALL remain inclusive at `0.80`, and this change MUST NOT add a component-specific minimum, change either threshold to a strict comparison, or restore a contradiction cap or automatic veto. Existing score rounding and tie-resolution behavior SHALL remain unchanged. Exact-year evidence SHALL remain 0 or 20 points, Episode count SHALL remain neutral, and Movie scoring SHALL remain unchanged.

#### Scenario: Exact 90-point ordinary candidate remains threshold-eligible
- **WHEN** a unique otherwise eligible ordinary candidate has exact parent and year evidence and Season similarity `1/2`, producing `MatchScore = 0.9000`
- **THEN** it SHALL remain eligible for automatic selection under the existing inclusive threshold without a new Season-component gate

#### Scenario: Prefix-insertion candidate crosses the unchanged threshold
- **WHEN** exact parent and year evidence plus Season similarity `2/3` produce approximately `MatchScore = 0.9333`
- **THEN** the candidate SHALL be evaluated by the same ordinary automatic-selection and tie rules as any other candidate above `0.90`

#### Scenario: TMDB alias threshold remains unchanged
- **WHEN** a TMDB-alias candidate is classified after continuous parent and Season title scoring
- **THEN** the alias path SHALL continue using its existing inclusive `0.80` acceptance threshold without a new component-specific gate or cap

#### Scenario: Movie scoring is isolated
- **WHEN** the scorer evaluates a Movie candidate
- **THEN** its existing title metric, title/year weights, score, ordering, and automatic-selection behavior SHALL be unchanged by the Season-only continuous metric
