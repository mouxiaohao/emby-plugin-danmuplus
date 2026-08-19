## Why

The current Season scorer uses a short-string-sensitive Jaro-Winkler comparison for Season-name residuals and effectively binary containment for parent-title evidence. This makes visibly related Chinese titles such as `西行` / `之西行` or `爱书的下克上：为了成为图书管理员不择手段！` / `小书痴的下克上 〜为了成为图书管理员而不择手段〜` receive unintuitive zero-or-full title components and can leave an otherwise correct result at 80 points.

## What Changes

- Score normalized parent-series titles continuously from 0 to 60 points using standard equal-weight normalized Levenshtein similarity.
- Score normalized Season-name residuals continuously from 0 to 20 points with the same metric, so exact equality remains full credit and insertions, deletions, or substitutions reduce credit proportionally.
- Preserve the existing 60/20/20 parent-title, Season-name, and exact-year weights; year evidence remains 0 or 20 and Episode count remains neutral.
- Preserve generic Season-marker handling and explicit conflicting-Season evidence while allowing a confirmed correct marker to be excluded from the Season-name residual comparison.
- Keep every existing automatic threshold inclusive and unchanged, including the ordinary `>= 0.90` rule and the separate TMDB-alias rule.
- Do not add connector-specific rewrites such as deleting `之`, semantic synonym dictionaries, or language-specific title exceptions.
- Keep Movie scoring, provider eligibility/order, search rounds, public APIs, and the shared `StringExtension.Distance` behavior outside the Season title-evidence path unchanged.
- Stamp the implementation and cumulative documentation as DanmuPlus 2.0.6 without changing the frontend/mapping protocol versions.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-danmu-matching`: Define continuous normalized edit-similarity evidence for the 60-point parent-title component and 20-point Season-name component without changing their weights or selection thresholds.

## Impact

- Affects Season candidate title evidence and explanations in `DanmuMatchScorer`, plus a narrowly scoped normalized Levenshtein helper used by that evidence path.
- Adds deterministic scorer and selection regressions for exact, partial, unrelated, empty, reordered, correct-marker, and conflicting-marker title pairs, including ordinary and TMDB-alias paths.
- Updates assembly/product/configuration identity, TMDB User-Agent text, and cumulative 2.0.6 documentation while preserving prior release history and README assets.
- Does not change the saved configuration schema, stored media metadata, network/provider contracts, frontend response shape, Movie scoring, or external dependencies.
