using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>Bounded, process-local proof for carrying a server score across dialog requests.</summary>
    public sealed class DanmuCandidateEvidenceRegistry
    {
        private const int MaximumEntries = 2048;
        private const int MaximumMoviePartEntries = MaximumEntries * 4;
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, DanmuCandidateEvidence> _entries =
            new ConcurrentDictionary<string, DanmuCandidateEvidence>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, DanmuMoviePartEvidence> _movieParts =
            new ConcurrentDictionary<string, DanmuMoviePartEvidence>(StringComparer.Ordinal);

        public string Register(string seasonId, string site, string candidateId,
            double matchScore, string scoreOrigin, SourceMetadata sourceMetadata = null,
            DanmuRemainderDecisionEvidence remainderDecision = null)
        {
            PurgeExpired();
            while (_entries.Count >= MaximumEntries)
            {
                var oldest = _entries.OrderBy(pair => pair.Value.ExpiresUtc).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(oldest.Key)) break;
                _entries.TryRemove(oldest.Key, out _);
            }
            var token = Guid.NewGuid().ToString("N");
            _entries[token] = new DanmuCandidateEvidence
            {
                SeasonId = seasonId ?? string.Empty,
                Site = site ?? string.Empty,
                CandidateId = candidateId ?? string.Empty,
                MatchScore = Math.Max(0, Math.Min(1, matchScore)),
                ScoreOrigin = scoreOrigin ?? string.Empty,
                SourceMetadata = sourceMetadata?.Clone(),
                RemainderDecision = remainderDecision?.Clone(),
                ExpiresUtc = DateTime.UtcNow.Add(Lifetime),
            };
            return token;
        }

        public bool TryResolve(string token, string seasonId, string site, string candidateId,
            out DanmuCandidateEvidence evidence)
        {
            evidence = null;
            if (string.IsNullOrWhiteSpace(token) || !_entries.TryGetValue(token, out var found)) return false;
            if (found.ExpiresUtc <= DateTime.UtcNow)
            {
                _entries.TryRemove(token, out _);
                return false;
            }
            if (!string.Equals(found.SeasonId, seasonId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.Site, site, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase)) return false;
            evidence = found;
            return true;
        }

        public string RegisterRemainder(
            string seasonId,
            string site,
            string candidateId,
            double matchScore,
            SourceMetadata sourceMetadata,
            DanmuRemainderDecisionEvidence remainderDecision)
        {
            if (remainderDecision == null || !remainderDecision.IsValid()) return string.Empty;
            return Register(seasonId, site, candidateId, matchScore,
                remainderDecision.DecisionKind, sourceMetadata, remainderDecision);
        }

        public string RegisterMoviePart(
            string parentToken,
            string itemId,
            string site,
            string candidateId,
            ScraperMoviePart part)
        {
            if (part == null || !part.IsDownloadable || part.IsExplicitNonMain ||
                string.IsNullOrWhiteSpace(part.Id) ||
                !TryResolve(parentToken, itemId, site, candidateId, out _))
            {
                return string.Empty;
            }

            PurgeExpired();
            while (_movieParts.Count >= MaximumMoviePartEntries)
            {
                var oldest = _movieParts.OrderBy(pair => pair.Value.ExpiresUtc).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(oldest.Key)) break;
                _movieParts.TryRemove(oldest.Key, out _);
            }
            var token = Guid.NewGuid().ToString("N");
            _movieParts[token] = new DanmuMoviePartEvidence
            {
                ParentToken = parentToken ?? string.Empty,
                ItemId = itemId ?? string.Empty,
                Site = site ?? string.Empty,
                CandidateId = candidateId ?? string.Empty,
                PartId = part.Id,
                PartTitle = part.Title ?? string.Empty,
                Index = part.Index,
                ExpiresUtc = DateTime.UtcNow.Add(Lifetime),
            };
            return token;
        }

        public bool TryResolveMoviePart(
            string token,
            string parentToken,
            string itemId,
            string site,
            string candidateId,
            out DanmuMoviePartEvidence evidence)
        {
            evidence = null;
            if (string.IsNullOrWhiteSpace(token) ||
                !_movieParts.TryGetValue(token, out var found)) return false;
            if (found.ExpiresUtc <= DateTime.UtcNow)
            {
                _movieParts.TryRemove(token, out _);
                return false;
            }
            if (!TryResolve(parentToken, itemId, site, candidateId, out _) ||
                !string.Equals(found.ParentToken, parentToken, StringComparison.Ordinal) ||
                !string.Equals(found.ItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.Site, site, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase)) return false;
            evidence = found;
            return true;
        }

        private void PurgeExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var pair in _entries.Where(pair => pair.Value.ExpiresUtc <= now).ToList())
                _entries.TryRemove(pair.Key, out _);
            foreach (var pair in _movieParts.Where(pair => pair.Value.ExpiresUtc <= now).ToList())
                _movieParts.TryRemove(pair.Key, out _);
        }
    }

    public sealed class DanmuCandidateEvidence
    {
        public string SeasonId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public string ScoreOrigin { get; set; } = string.Empty;
        public SourceMetadata SourceMetadata { get; set; }
        // Server-authored proof for a recursive remainder selection. This is
        // process-local and target-bound by the enclosing evidence token.
        public DanmuRemainderDecisionEvidence RemainderDecision { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }

    /// <summary>
    /// Closed, server-owned facts used to explain and revalidate one remainder
    /// decision. It is deliberately not a request/JSON model: callers must
    /// obtain it only from target-bound candidate evidence.
    /// </summary>
    public sealed class DanmuRemainderDecisionEvidence
    {
        public string DecisionKind { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public int? PartNumber { get; set; }
        public int? ComparisonYear { get; set; }
        public int? SourceYear { get; set; }
        public int LocalEpisodeCount { get; set; }
        public int VerifiedSourceEpisodeCount { get; set; }
        public int? LogicalSeasonNumber { get; set; }
        public int ActiveLogicalSeasonNumber { get; set; }
        public double FinalScore { get; set; }
        public int SimilarCandidateCount { get; set; }
        public int MatchingTupleCount { get; set; }
        public string AuthoritativeParentTitle { get; set; } = string.Empty;
        public double? ParentTitleScore { get; set; }
        public double? SeasonNumberScore { get; set; }
        public double? YearScore { get; set; }
        // Immutable first-segment provider for the entire recursive operation.
        // This field is server-owned and is not part of the V22 request model.
        public string ProviderLock { get; set; } = string.Empty;
        public string StableProviderId { get; set; } = string.Empty;
        public string StableMediaId { get; set; } = string.Empty;
        public string RunStartItemId { get; set; } = string.Empty;
        public List<string> RunItemIds { get; set; } = new List<string>();
        public long PlanGeneration { get; set; }
        public bool EpisodeCountMismatchWarning { get; set; }
        public List<CompositeSeasonSourceEpisode> VerifiedSourceEpisodes { get; set; } = new List<CompositeSeasonSourceEpisode>();

        public DanmuRemainderDecisionEvidence Clone()
        {
            return new DanmuRemainderDecisionEvidence
            {
                DecisionKind = DecisionKind ?? string.Empty,
                Stage = Stage ?? string.Empty,
                PartNumber = PartNumber,
                ComparisonYear = ComparisonYear,
                SourceYear = SourceYear,
                LocalEpisodeCount = LocalEpisodeCount,
                VerifiedSourceEpisodeCount = VerifiedSourceEpisodeCount,
                LogicalSeasonNumber = LogicalSeasonNumber,
                ActiveLogicalSeasonNumber = ActiveLogicalSeasonNumber,
                FinalScore = FinalScore,
                SimilarCandidateCount = SimilarCandidateCount,
                MatchingTupleCount = MatchingTupleCount,
                AuthoritativeParentTitle = AuthoritativeParentTitle ?? string.Empty,
                ParentTitleScore = ParentTitleScore,
                SeasonNumberScore = SeasonNumberScore,
                YearScore = YearScore,
                ProviderLock = ProviderLock ?? string.Empty,
                StableProviderId = StableProviderId ?? string.Empty,
                StableMediaId = StableMediaId ?? string.Empty,
                RunStartItemId = RunStartItemId ?? string.Empty,
                RunItemIds = (RunItemIds ?? new List<string>()).ToList(),
                PlanGeneration = PlanGeneration,
                EpisodeCountMismatchWarning = EpisodeCountMismatchWarning,
                VerifiedSourceEpisodes = (VerifiedSourceEpisodes ?? new List<CompositeSeasonSourceEpisode>()).Select(item => new CompositeSeasonSourceEpisode { EpisodeId = item.EpisodeId, CommentId = item.CommentId, EpisodeNumber = item.EpisodeNumber, SourceOrdinal = item.SourceOrdinal }).ToList(),
            };
        }

        public bool IsValid()
        {
            if (!DanmuRemainderDecisionKinds.IsKnown(DecisionKind) ||
                !DanmuRemainderDecisionStages.IsKnown(Stage) ||
                string.IsNullOrWhiteSpace(ProviderLock) ||
                string.IsNullOrWhiteSpace(StableProviderId) ||
                !string.Equals(ProviderLock, StableProviderId, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(StableMediaId) ||
                string.IsNullOrWhiteSpace(RunStartItemId) ||
                LocalEpisodeCount <= 0 || VerifiedSourceEpisodeCount <= 3 ||
                RunItemIds == null || RunItemIds.Count != LocalEpisodeCount ||
                RunItemIds.Any(string.IsNullOrWhiteSpace) ||
                !string.Equals(RunStartItemId, RunItemIds[0], StringComparison.OrdinalIgnoreCase) ||
                RunItemIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != RunItemIds.Count ||
                VerifiedSourceEpisodes == null || VerifiedSourceEpisodes.Count != VerifiedSourceEpisodeCount ||
                VerifiedSourceEpisodes.Any(item => item == null || string.IsNullOrWhiteSpace(item.EpisodeId) || string.IsNullOrWhiteSpace(item.CommentId)) ||
                VerifiedSourceEpisodes.Select(item => item.EpisodeId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != VerifiedSourceEpisodes.Count ||
                VerifiedSourceEpisodes.Select(item => item.CommentId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != VerifiedSourceEpisodes.Count ||
                PlanGeneration <= 0 || ActiveLogicalSeasonNumber < 0 || FinalScore < 0 || FinalScore > 1) return false;

            if (string.Equals(DecisionKind, DanmuRemainderDecisionKinds.Part, StringComparison.Ordinal))
                return string.Equals(Stage, DanmuRemainderDecisionStages.Part, StringComparison.Ordinal) &&
                       PartNumber.GetValueOrDefault() > 0 && !EpisodeCountMismatchWarning && FinalScore == 0;
            if (string.Equals(DecisionKind, DanmuRemainderDecisionKinds.Metadata, StringComparison.Ordinal) ||
                string.Equals(DecisionKind, DanmuRemainderDecisionKinds.MetadataCountWarning, StringComparison.Ordinal))
                return string.Equals(Stage, DanmuRemainderDecisionStages.Metadata, StringComparison.Ordinal) &&
                       ComparisonYear.GetValueOrDefault() > 0 &&
                       EpisodeCountMismatchWarning == string.Equals(DecisionKind,
                           DanmuRemainderDecisionKinds.MetadataCountWarning, StringComparison.Ordinal) && FinalScore == 0 &&
                       SourceYear == ComparisonYear &&
                       (string.Equals(DecisionKind, DanmuRemainderDecisionKinds.Metadata, StringComparison.Ordinal)
                           ? VerifiedSourceEpisodeCount == LocalEpisodeCount && MatchingTupleCount == 1 && SimilarCandidateCount > 0
                           : VerifiedSourceEpisodeCount != LocalEpisodeCount && SimilarCandidateCount == 1 && MatchingTupleCount == 0);
            return string.Equals(Stage, DanmuRemainderDecisionStages.LogicalSeason, StringComparison.Ordinal) &&
                   LogicalSeasonNumber.GetValueOrDefault() > 0 &&
                   ComparisonYear.GetValueOrDefault() > 0 && SourceYear == ComparisonYear && ParentTitleScore.HasValue && SeasonNumberScore.HasValue && YearScore.HasValue &&
                   SimilarCandidateCount == 1 && MatchingTupleCount == 0 && !string.IsNullOrWhiteSpace(AuthoritativeParentTitle) &&
                   !EpisodeCountMismatchWarning && LogicalSeasonNumber == ActiveLogicalSeasonNumber + 1 &&
                   ParentTitleScore.Value >= 0 && ParentTitleScore.Value <= 1 && SeasonNumberScore.Value >= 0 && SeasonNumberScore.Value <= 1 && YearScore.Value == 1 &&
                   Math.Abs(FinalScore - (ParentTitleScore.Value * .60 + SeasonNumberScore.Value * .20 + YearScore.Value * .20)) < .0001 &&
                   FinalScore >= .90;
        }
    }

    public static class DanmuRemainderDecisionStages
    {
        public const string Part = "part";
        public const string Metadata = "metadata";
        public const string LogicalSeason = "logical-season";

        public static bool IsKnown(string value)
        {
            return string.Equals(value, Part, StringComparison.Ordinal) ||
                   string.Equals(value, Metadata, StringComparison.Ordinal) ||
                   string.Equals(value, LogicalSeason, StringComparison.Ordinal);
        }
    }

    public static class DanmuRemainderDecisionKinds
    {
        public const string Part = DanmuMatchOrigin.RemainderPart;
        public const string Metadata = DanmuMatchOrigin.RemainderMetadata;
        public const string MetadataCountWarning = DanmuMatchOrigin.RemainderMetadataCountWarning;
        public const string LogicalSeason = DanmuMatchOrigin.RemainderLogicalSeason;

        public static bool IsKnown(string value)
        {
            return string.Equals(value, Part, StringComparison.Ordinal) ||
                   string.Equals(value, Metadata, StringComparison.Ordinal) ||
                   string.Equals(value, MetadataCountWarning, StringComparison.Ordinal) ||
                   string.Equals(value, LogicalSeason, StringComparison.Ordinal);
        }
    }

    public sealed class DanmuMoviePartEvidence
    {
        public string ParentToken { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string PartId { get; set; } = string.Empty;
        public string PartTitle { get; set; } = string.Empty;
        public int? Index { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }
}
