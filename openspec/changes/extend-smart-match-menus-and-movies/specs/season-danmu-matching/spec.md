## MODIFIED Requirements

### Requirement: Confidence-gated automatic selection
The system SHALL automatically select a candidate when the globally ranked candidate set has one sufficiently strong result, has only a weak runner-up, has a unique highest result within one competing site, has a cross-site highest-score tie resolved by configured site priority, or contains a close high-confidence cross-site pool resolved by configured site priority. The close pool for different cross-site scores SHALL contain candidates scoring at least `0.9500` and no more than `0.0300` below the highest score. Within the selected site its highest-scoring candidate SHALL win unless multiple candidates share that same site-local highest score, which SHALL remain ambiguous. Candidate scoring and displayed descending-score order MUST remain provider-neutral even when the automatically selected candidate is not displayed first.

#### Scenario: Global winner is sufficiently strong and distinct
- **WHEN** the top global candidate meets the minimum score and no other candidate is sufficiently strong to compete
- **THEN** the match result SHALL identify that candidate as automatically selected

#### Scenario: Close high-confidence candidates span sites
- **WHEN** candidates from multiple sites score at least `0.9500` and no more than `0.0300` below the highest score
- **THEN** preview and automatic library-import matching SHALL select the sole pooled candidate from the earliest configured site

#### Scenario: Higher-priority site has the second displayed score
- **WHEN** a `1.0000` candidate and a `0.9800` candidate are in the close pool and the `0.9800` candidate's site is configured earlier
- **THEN** the system SHALL automatically select the `0.9800` candidate while retaining descending-score display order

#### Scenario: Candidate falls outside the close pool
- **WHEN** a candidate scores below `0.9500` or more than `0.0300` below the highest score
- **THEN** that candidate MUST NOT displace the higher-scoring candidate through site priority

#### Scenario: Top score is tied across providers
- **WHEN** the highest qualifying score is shared across sites
- **THEN** the system SHALL automatically select the unique highest-scoring candidate from the earliest configured site without requiring the tied score to meet the close-pool floor

#### Scenario: Highest-priority provider remains internally ambiguous
- **WHEN** the selected site has multiple candidates sharing its site-local highest score
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary result

#### Scenario: One site has a unique highest candidate
- **WHEN** all competing candidates are from one site and its highest score is unique
- **THEN** the system SHALL automatically select that highest-scoring candidate without applying the cross-site close-pool floor or site-priority resolution

#### Scenario: Intermediate parent-title round has multiple close candidates
- **WHEN** the parent-title search round requires cross-site site-priority resolution and fallback keywords remain
- **THEN** the system SHALL continue fallback search instead of applying site-priority resolution early

#### Scenario: Global result is ambiguous
- **WHEN** no candidate satisfies the standard confidence rules, no close high-confidence cross-site pool exists, or the selected site's highest score is internally tied
- **THEN** the system SHALL return candidates for manual selection without automatically binding an arbitrary provider result
