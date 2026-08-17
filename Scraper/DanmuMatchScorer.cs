using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Scores search results without depending on a particular danmu provider.
    /// The episode count intentionally has a high weight because some providers
    /// use sequel titles that no longer contain the TMDB parent-series name.
    /// </summary>
    public static class DanmuMatchScorer
    {
        public const double SeasonCandidateTitleEligibilityFloor = 0.58;
        public const double AutomaticConfidenceThreshold = 0.90;

        private static readonly Regex SeasonNumberRegex = new Regex(
            @"(?:第\s*[0-9一二三四五六七八九十百零〇两]+\s*季|season\s*\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GenericSeasonLabelRegex = new Regex(
            @"^(?:(?:第\s*)?[0-9一二三四五六七八九十百零〇两]+\s*季|season\s*\d+|s\s*\d+|part\s*\d+|第?\s*[0-9一二三四五六七八九十百零〇两]+\s*(?:部|篇))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GenericSeasonNumberRegex = new Regex(
            @"^(?:(?:第\s*)?[0-9一二三四五六七八九十百零〇两]+\s*季|season\s*\d+|s\s*\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ExplicitSeasonMarkerRegex = new Regex(
            @"(?:第\s*(?<chinese>[0-9一二三四五六七八九十百零〇两]+)\s*(?:季|期)|season\s*(?<number>\d+)|\bs\s*0?(?<short>\d{1,2})\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SeparatorRegex = new Regex(
            @"[\s\p{P}\p{S}]+",
            RegexOptions.Compiled);

        private static readonly Regex WhitespaceRegex = new Regex(
            @"\s+",
            RegexOptions.Compiled);

        public static List<string> BuildSearchKeywords(
            string seriesName,
            string seasonName,
            string keywordOverride)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parentTitle = SanitizeSearchTerm(seriesName);
            var keyword = ExtractSeasonKeyword(parentTitle, seasonName);

            if (!string.IsNullOrWhiteSpace(keywordOverride))
            {
                AddKeyword(result, seen, SanitizeSearchTerm(keywordOverride));
                return result;
            }

            // The parent title is deliberately the first search round.  Search callers
            // run each round against every enabled provider before moving on, so a
            // provider near the front of the configured list cannot win merely because
            // it happened to return a season-keyword result first.
            if (!IsIdentityBearingTitle(parentTitle))
            {
                return result;
            }

            AddKeyword(result, seen, parentTitle);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                AddKeyword(result, seen, parentTitle + " " + keyword);
            }
            return result;
        }

        public static string ExtractSeasonKeyword(string seriesName, string seasonName)
        {
            var value = SanitizeSearchTerm(seasonName);
            if (!string.IsNullOrWhiteSpace(seriesName))
            {
                value = Regex.Replace(value, Regex.Escape(seriesName), string.Empty, RegexOptions.IgnoreCase);
            }

            value = SeasonNumberRegex.Replace(value, string.Empty);
            value = value.Trim(' ', ':', '：', '-', '–', '—', '_', '·', '.', '。');
            var normalized = Normalize(value);
            if (normalized.Length < 2 || normalized == "正片" || normalized == "本篇" ||
                GenericSeasonNumberRegex.IsMatch(value))
            {
                return string.Empty;
            }

            return value;
        }

        /// <summary>
        /// Returns a positive Season number only when all explicit markers agree.
        /// Bare numbers, episode labels, part/cour labels, and Season 0 are not
        /// identity evidence and deliberately return null.
        /// </summary>
        public static int? ParseExplicitSeasonNumber(string value)
        {
            return ParseExplicitSeasonNumber(new[] { value });
        }

        public static int? ParseExplicitSeasonNumber(IEnumerable<string> values)
        {
            var seasons = new HashSet<int>();
            var hasZeroOrUnparseableMarker = false;
            foreach (var value in values ?? Enumerable.Empty<string>())
            {
                foreach (Match match in ExplicitSeasonMarkerRegex.Matches(value ?? string.Empty))
                {
                    var parsed = ParseSeasonNumber(match.Groups["number"].Value);
                    if (!parsed.HasValue)
                    {
                        parsed = ParseSeasonNumber(match.Groups["short"].Value);
                    }
                    if (!parsed.HasValue)
                    {
                        parsed = ParseChineseSeasonNumber(match.Groups["chinese"].Value);
                    }

                    if (parsed.GetValueOrDefault() > 0)
                    {
                        seasons.Add(parsed.Value);
                    }
                    else
                    {
                        hasZeroOrUnparseableMarker = true;
                    }
                }
            }

            return !hasZeroOrUnparseableMarker && seasons.Count == 1
                ? seasons.First()
                : (int?)null;
        }

        public static bool IsEligibleSeasonCandidate(
            ScraperSearchInfo source,
            string seriesName,
            string seasonName,
            string keywordOverride,
            IEnumerable<string> localSeriesTitleAliases = null,
            IEnumerable<string> localSeasonTitleAliases = null)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Id) ||
                string.IsNullOrWhiteSpace(source.Name) || IsIdentifiableMovie(source.Category))
            {
                return false;
            }

            var sourceTitles = GetSourceTitles(source).Select(Normalize)
                .Where(value => value.Length > 0).ToList();
            if (!string.IsNullOrWhiteSpace(keywordOverride))
            {
                // An explicit manual keyword is provider-discovery input.  Once a
                // provider returns a structurally usable Season record, retain it
                // for target-metadata scoring and manual selection; aliases and
                // cross-language titles cannot be required to resemble the query.
                return true;
            }

            var parent = SanitizeSearchTerm(seriesName);
            if (!IsIdentityBearingTitle(parent))
            {
                return false;
            }

            var parentEvidence = BestSimilarity(
                new[] { parent }
                    .Concat(localSeriesTitleAliases ?? Enumerable.Empty<string>())
                    .Concat(localSeasonTitleAliases ?? Enumerable.Empty<string>()),
                sourceTitles);
            if (parentEvidence >= SeasonCandidateTitleEligibilityFloor)
            {
                return true;
            }

            // A shared bare Part/Season number can never rescue insufficient
            // parent-title evidence. Unusual translations remain discoverable
            // through their explicit user keyword.
            return false;
        }

        public static bool IsIdentifiableMovie(string category)
        {
            var normalized = Normalize(category);
            return normalized.Contains("电影") || normalized.Contains("影片") ||
                   normalized.Contains("movie") || normalized.Contains("film") ||
                   normalized.Contains("cinema");
        }

        public static DanmuMatchCandidate Score(
            ScraperSearchInfo source,
            string site,
            string siteName,
            int sourceOrder,
            string seriesName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes,
            IEnumerable<string> localSeriesTitleAliases = null,
            IEnumerable<string> localSeasonTitleAliases = null,
            bool applyContradictionCap = true,
            int? expectedSeasonNumber = null,
            bool includeLocalSeriesAliasesForParentScoring = false)
        {
            // Retained for source compatibility with existing callers; contradiction
            // evidence no longer changes the ordinary composite score.
            _ = applyContradictionCap;
            var targetSeasonNumber = expectedSeasonNumber ?? ParseExplicitSeasonNumber(seasonName);
            var seasonParentTitles = BuildNormalizedTitleSet(
                new[] { seriesName }.Concat(localSeriesTitleAliases ?? Enumerable.Empty<string>()));
            var parentTitles = includeLocalSeriesAliasesForParentScoring
                ? seasonParentTitles
                : BuildNormalizedTitleSet(new[] { seriesName });
            var seasonTitles = BuildSeasonTitleVariants(
                seasonName, localSeasonTitleAliases, seasonParentTitles, targetSeasonNumber);
            var titleEvidence = GetBestSeasonTitleEvidence(
                GetSourceTitles(source), parentTitles, seasonTitles, targetSeasonNumber);
            var parentScore = titleEvidence.ParentScore;
            var seasonScore = titleEvidence.SeasonScore;
            var titleScore = (parentScore * 0.60 + seasonScore * 0.20) / 0.80;
            var yearScore = GetExactYearScore(expectedYear, source.Year);
            var episodeScore = GetExactEpisodeScore(expectedEpisodes, source.EpisodeSize);
            var score = Clamp(parentScore * 0.60 + seasonScore * 0.20 +
                              yearScore * 0.10 + episodeScore * 0.10);
            var fidelityTitleEvidence = GetSeasonFidelityTitleEvidence(
                seriesName,
                seasonName,
                localSeriesTitleAliases,
                localSeasonTitleAliases,
                GetSourceTitles(source));
            return new DanmuMatchCandidate
            {
                Id = source.Id ?? string.Empty,
                Site = site ?? string.Empty,
                SiteName = siteName ?? string.Empty,
                SourceOrder = sourceOrder,
                Name = source.Name ?? string.Empty,
                Category = source.Category ?? string.Empty,
                Year = source.Year,
                EpisodeSize = source.EpisodeSize,
                Score = Round(score),
                MatchScore = Round(score),
                ScoreOrigin = "search-confidence",
                TitleScore = Round(titleScore),
                ParentTitleScore = Round(parentScore),
                KeywordScore = Round(seasonScore),
                YearScore = Round(yearScore),
                EpisodeScore = Round(episodeScore),
                Reason = BuildReason(parentScore, seasonScore, yearScore, episodeScore),
                MatchOrigin = string.IsNullOrWhiteSpace(source.SearchAlias) ? string.Empty : "tmdb-alias",
                DecisionReason = string.IsNullOrWhiteSpace(source.SearchAlias) ? string.Empty : "tmdb-alias:" + source.SearchAlias,
                FidelityTitleEvidence = fidelityTitleEvidence,
                SourceMetadata = CloneSourceMetadata(source),
            };
        }

        public static DanmuMatchCandidate ScoreMovie(
            ScraperSearchInfo source,
            string site,
            string siteName,
            int sourceOrder,
            string movieName,
            int? expectedYear,
            IEnumerable<string> localTitleAliases = null)
        {
            var titleScore = BestSimilarity(
                new[] { movieName }.Concat(localTitleAliases ?? Enumerable.Empty<string>()),
                GetSourceTitles(source).Select(Normalize));
            var yearScore = GetYearScore(expectedYear, source.Year);
            var score = titleScore * 0.82 + yearScore * 0.18;
            if (IsIdentifiableNonMovie(source.Category))
            {
                score = 0;
            }

            var fidelityTitleEvidence = Math.Min(1, GetFidelityTitleEvidence(
                new[] { movieName }.Concat(localTitleAliases ?? Enumerable.Empty<string>()),
                GetSourceTitles(source)));

            return new DanmuMatchCandidate
            {
                Id = source.Id ?? string.Empty,
                Site = site ?? string.Empty,
                SiteName = siteName ?? string.Empty,
                SourceOrder = sourceOrder,
                Name = source.Name ?? string.Empty,
                Category = source.Category ?? string.Empty,
                Year = source.Year,
                EpisodeSize = source.EpisodeSize,
                Score = Round(Clamp(score)),
                MatchScore = Round(Clamp(score)),
                ScoreOrigin = "search-confidence",
                TitleScore = Round(titleScore),
                ParentTitleScore = Round(titleScore),
                YearScore = Round(yearScore),
                Reason = titleScore >= 0.95 && yearScore >= 0.95
                    ? "电影名和年份吻合"
                    : titleScore >= 0.95 ? "电影名吻合" : "需要人工确认",
                MatchOrigin = string.IsNullOrWhiteSpace(source.SearchAlias) ? string.Empty : "tmdb-alias",
                DecisionReason = string.IsNullOrWhiteSpace(source.SearchAlias) ? string.Empty : "tmdb-alias:" + source.SearchAlias,
                FidelityTitleEvidence = fidelityTitleEvidence,
                SourceMetadata = CloneSourceMetadata(source),
            };
        }

        public static bool IsIdentifiableNonMovie(string category)
        {
            var normalized = (category ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0)
            {
                return false;
            }

            if (normalized.Contains("电影") || normalized.Contains("movie") || normalized.Contains("film"))
            {
                return false;
            }

            return normalized.Contains("电视剧") || normalized.Contains("番剧") ||
                   normalized.Contains("动漫") || normalized.Contains("动画") ||
                   normalized.Contains("综艺") || normalized.Contains("season") ||
                   normalized == "tv" || normalized.Contains("series") || normalized.Contains("anime");
        }

        public static DanmuMatchCandidate SelectAutoCandidate(
            IList<DanmuMatchCandidate> candidates,
            bool allowProviderPriorityTie = true)
        {
            // r6 deliberately has one confidence rule.  Provider order decides the
            // winning site, then score decides only within that one site.  The
            // compatibility parameter is retained for callers compiled against r5.
            // Execution completeness is classified independently by the search
            // result; this pure selector only evaluates the canonical evidence.
            var available = (candidates ?? new List<DanmuMatchCandidate>())
                .Where(x => x != null)
                .ToList();
            foreach (var candidate in available)
            {
                candidate.MatchScore = Round(GetEffectiveConfidence(candidate, available));
            }

            var threshold = available.Any(x => string.Equals(x.MatchOrigin, "tmdb-alias", StringComparison.OrdinalIgnoreCase))
                ? 0.80
                : AutomaticConfidenceThreshold;
            var confident = available
                .Where(x => x.MatchScore >= threshold)
                .ToList();
            if (confident.Count == 0)
            {
                return null;
            }

            var sourceOrder = confident.Min(x => x.SourceOrder);
            var preferredSite = confident
                .Where(x => x.SourceOrder == sourceOrder)
                .ToList();
            return SelectUniqueHighest(preferredSite);
        }

        public static bool CanAutoSelect(IList<DanmuMatchCandidate> candidates, bool allowProviderPriorityTie = true)
        {
            return SelectAutoCandidate(candidates, allowProviderPriorityTie) != null;
        }

        private static DanmuMatchCandidate SelectUniqueHighest(IList<DanmuMatchCandidate> candidates)
        {
            var highestScore = candidates.Max(x => x.MatchScore);
            var scoreWinners = candidates.Where(x => x.MatchScore == highestScore).ToList();
            var highestFidelityEvidence = scoreWinners.Max(x => x.FidelityTitleEvidence);
            var winners = scoreWinners
                .Where(x => x.FidelityTitleEvidence == highestFidelityEvidence)
                .ToList();
            return winners.Count == 1 ? winners[0] : null;
        }

        public static double GetEffectiveConfidence(
            DanmuMatchCandidate candidate,
            IEnumerable<DanmuMatchCandidate> candidates)
        {
            // Fidelity remains positive evidence for resolving an otherwise
            // equal-score tie, but it never changes ordinary confidence.
            return Clamp(candidate?.Score ?? 0);
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Providers and TMDB occasionally mix these homophones/variants.
            var normalized = NormalizeExplicitSeasonMarkers(SanitizeSearchTerm(value)).ToLowerInvariant()
                .Replace('谭', '潭')
                .Replace('臺', '台')
                .Replace('裏', '里');
            return SeparatorRegex.Replace(normalized, string.Empty);
        }

        /// <summary>
        /// Produces the exact-comparison title channel. Compatibility-equivalent
        /// Unicode characters share a representation, while punctuation and symbols
        /// retain their normalized type, count, and order.
        /// </summary>
        public static string NormalizeFidelity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = SanitizeSearchTerm(value)
                .Normalize(NormalizationForm.FormKC)
                .ToLowerInvariant();
            return WhitespaceRegex.Replace(normalized, string.Empty);
        }

        private static int GetFidelityTitleEvidence(
            IEnumerable<string> localTitles,
            IEnumerable<string> sourceTitles)
        {
            var localForms = BuildTitleForms(localTitles);
            var sourceForms = BuildTitleForms(sourceTitles);
            return localForms
                .Join(
                    sourceForms,
                    local => local.Loose,
                    source => source.Loose,
                    (local, source) => string.Equals(
                        local.Fidelity,
                        source.Fidelity,
                        StringComparison.Ordinal)
                        ? local.Fidelity
                        : string.Empty,
                    StringComparer.Ordinal)
                .Where(fidelity => fidelity.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        private static int GetSeasonFidelityTitleEvidence(
            string seriesName,
            string seasonName,
            IEnumerable<string> localSeriesTitleAliases,
            IEnumerable<string> localSeasonTitleAliases,
            IEnumerable<string> sourceTitles)
        {
            var seasonTitles = new[] { seasonName }
                .Concat(localSeasonTitleAliases ?? Enumerable.Empty<string>())
                .Where(IsIdentityBearingTitle)
                .ToList();
            if (GetFidelityTitleEvidence(seasonTitles, sourceTitles) > 0)
            {
                return 2;
            }

            var parentTitles = new[] { seriesName }
                .Concat(localSeriesTitleAliases ?? Enumerable.Empty<string>())
                .Where(IsIdentityBearingTitle);
            return GetFidelityTitleEvidence(parentTitles, sourceTitles) > 0 ? 1 : 0;
        }

        private static List<TitleForm> BuildTitleForms(IEnumerable<string> titles)
        {
            return (titles ?? Enumerable.Empty<string>())
                .Select(title => new TitleForm
                {
                    Loose = Normalize(title),
                    Fidelity = NormalizeFidelity(title),
                })
                .Where(form => form.Loose.Length > 0 && form.Fidelity.Length > 0)
                .GroupBy(form => form.Fidelity, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<string> GetSourceTitles(ScraperSearchInfo source)
        {
            if (source == null)
            {
                return Enumerable.Empty<string>();
            }

            return new[] { source.Name, source.SourceMetadata?.Title }
                .Concat(source.Aliases ?? new List<string>());
        }

        private static List<string> BuildNormalizedTitleSet(IEnumerable<string> titles)
        {
            return (titles ?? Enumerable.Empty<string>())
                .Select(Normalize)
                .Where(title => title.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> BuildSeasonTitleVariants(
            string seasonName,
            IEnumerable<string> localSeasonTitleAliases,
            IList<string> parentTitles,
            int? expectedSeasonNumber)
        {
            var variants = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var title in new[] { seasonName }
                         .Concat(localSeasonTitleAliases ?? Enumerable.Empty<string>()))
            {
                var normalized = Normalize(title);
                if (normalized.Length == 0)
                {
                    continue;
                }

                var matchedParents = (parentTitles ?? new List<string>())
                    .Where(parent => normalized.Contains(parent))
                    .OrderByDescending(parent => parent.Length)
                    .ToList();
                if (matchedParents.Count == 0)
                {
                    AddSeasonVariant(variants, seen, normalized, expectedSeasonNumber);
                    continue;
                }

                foreach (var parent in matchedParents)
                {
                    AddSeasonVariant(
                        variants, seen, normalized.Replace(parent, string.Empty), expectedSeasonNumber);
                }
            }

            if (expectedSeasonNumber.GetValueOrDefault() > 0)
            {
                AddSeasonVariant(variants, seen,
                    Normalize("第" + expectedSeasonNumber.Value.ToString(CultureInfo.InvariantCulture) + "季"),
                    expectedSeasonNumber);
            }
            if (expectedSeasonNumber == 1)
            {
                AddSeasonVariant(variants, seen, string.Empty, expectedSeasonNumber);
            }
            return variants;
        }

        private static void AddSeasonVariant(
            ICollection<string> variants,
            ISet<string> seen,
            string value,
            int? expectedSeasonNumber)
        {
            var normalized = value ?? string.Empty;
            var variantSeasonNumber = ParseExplicitSeasonNumber(normalized);
            if (expectedSeasonNumber.GetValueOrDefault() > 0 &&
                variantSeasonNumber.HasValue &&
                variantSeasonNumber.Value != expectedSeasonNumber.Value)
            {
                return;
            }
            if ((normalized.Length > 0 || expectedSeasonNumber == 1) && seen.Add(normalized))
            {
                variants.Add(normalized);
            }
        }

        private static SeasonTitleEvidence GetBestSeasonTitleEvidence(
            IEnumerable<string> sourceTitles,
            IList<string> parentTitles,
            IList<string> seasonTitles,
            int? expectedSeasonNumber)
        {
            var best = new SeasonTitleEvidence();
            foreach (var sourceTitle in sourceTitles ?? Enumerable.Empty<string>())
            {
                var normalizedSource = Normalize(sourceTitle);
                if (normalizedSource.Length == 0)
                {
                    continue;
                }

                var matchedParents = (parentTitles ?? new List<string>())
                    .Where(parent => normalizedSource.Contains(parent))
                    .OrderByDescending(parent => parent.Length)
                    .ToList();
                if (matchedParents.Count == 0)
                {
                    SelectBetterSeasonTitleEvidence(best, new SeasonTitleEvidence
                    {
                        ParentScore = 0,
                        SeasonScore = BestSeasonSimilarity(
                            normalizedSource, seasonTitles, expectedSeasonNumber),
                    });
                    continue;
                }

                foreach (var parent in matchedParents)
                {
                    SelectBetterSeasonTitleEvidence(best, new SeasonTitleEvidence
                    {
                        ParentScore = 1,
                        SeasonScore = BestSeasonSimilarity(
                            normalizedSource.Replace(parent, string.Empty),
                            seasonTitles,
                            expectedSeasonNumber),
                        MatchedParentLength = parent.Length,
                    });
                }
            }
            return best;
        }

        private static void SelectBetterSeasonTitleEvidence(
            SeasonTitleEvidence current,
            SeasonTitleEvidence candidate)
        {
            var currentScore = current.ParentScore * 0.60 + current.SeasonScore * 0.20;
            var candidateScore = candidate.ParentScore * 0.60 + candidate.SeasonScore * 0.20;
            if (candidateScore > currentScore ||
                Math.Abs(candidateScore - currentScore) < 0.0000001 &&
                candidate.MatchedParentLength > current.MatchedParentLength)
            {
                current.ParentScore = candidate.ParentScore;
                current.SeasonScore = candidate.SeasonScore;
                current.MatchedParentLength = candidate.MatchedParentLength;
            }
        }

        private static double BestSeasonSimilarity(
            string sourceTitle,
            IEnumerable<string> seasonTitles,
            int? expectedSeasonNumber)
        {
            return (seasonTitles ?? Enumerable.Empty<string>())
                .Select(target => GetSeasonSimilarity(sourceTitle, target, expectedSeasonNumber))
                .DefaultIfEmpty(0)
                .Max();
        }

        private static double GetSeasonSimilarity(
            string sourceTitle,
            string targetTitle,
            int? expectedSeasonNumber)
        {
            var source = sourceTitle ?? string.Empty;
            var target = targetTitle ?? string.Empty;
            var sourceSeason = ParseExplicitSeasonNumber(source);
            var targetSeason = ParseExplicitSeasonNumber(target);
            if (expectedSeasonNumber.GetValueOrDefault() > 0 &&
                (sourceSeason.HasValue && sourceSeason.Value != expectedSeasonNumber.Value ||
                 targetSeason.HasValue && targetSeason.Value != expectedSeasonNumber.Value))
            {
                return 0;
            }
            if (source.Length == 0 || target.Length == 0)
            {
                return source.Length == 0 && target.Length == 0 && expectedSeasonNumber == 1 ? 1 : 0;
            }

            if (sourceSeason.HasValue && targetSeason.HasValue &&
                sourceSeason.Value != targetSeason.Value)
            {
                return 0;
            }
            return Clamp(source.Distance(target));
        }

        private static double BestSimilarity(
            IEnumerable<string> localTitles,
            IEnumerable<string> normalizedSourceTitles)
        {
            var local = (localTitles ?? Enumerable.Empty<string>())
                .Select(Normalize).Where(value => value.Length > 0).ToList();
            var source = (normalizedSourceTitles ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrEmpty(value)).ToList();
            return local.Count == 0 || source.Count == 0
                ? 0
                : local.Max(left => source.Max(right => SimilarityAgainstTitle(left, right)));
        }

        private static SourceMetadata CloneSourceMetadata(ScraperSearchInfo source)
        {
            if (source?.SourceMetadata != null)
            {
                return source.SourceMetadata.Clone();
            }

            return new SourceMetadata
            {
                Title = source?.Name ?? string.Empty,
                Year = source?.Year,
                Category = source?.Category ?? string.Empty,
            };
        }

        private sealed class TitleForm
        {
            public string Loose { get; set; } = string.Empty;
            public string Fidelity { get; set; } = string.Empty;
        }

        private sealed class SeasonTitleEvidence
        {
            public double ParentScore { get; set; }
            public double SeasonScore { get; set; }
            public int MatchedParentLength { get; set; }
        }

        private static string SanitizeSearchTerm(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value.Where(character =>
                    character != '\uFFFD' && character != '\u0000' && !char.IsControl(character))
                .ToArray()).Trim();
        }

        private static bool IsIdentityBearingTitle(string value)
        {
            var normalized = Normalize(value);
            return normalized.Length >= 2 && !GenericSeasonLabelRegex.IsMatch(value ?? string.Empty);
        }

        private static bool HasTitleEvidence(string identity, string normalizedTitle, double floor)
        {
            if (!IsIdentityBearingTitle(identity) || string.IsNullOrEmpty(normalizedTitle))
            {
                return false;
            }

            return GetTitleEvidence(identity, normalizedTitle) >= floor;
        }

        private static double GetTitleEvidence(string identity, string normalizedTitle)
        {
            var normalizedIdentity = Normalize(identity);
            if (normalizedIdentity.Length == 0 || string.IsNullOrEmpty(normalizedTitle))
            {
                return 0;
            }

            return normalizedTitle.Contains(normalizedIdentity) || normalizedIdentity.Contains(normalizedTitle)
                ? 1
                : SimilarityAgainstTitle(normalizedIdentity, normalizedTitle);
        }

        private static void AddKeyword(List<string> result, HashSet<string> seen, string value)
        {
            var keyword = (value ?? string.Empty).Trim();
            if (keyword.Length > 0 && seen.Add(keyword))
            {
                result.Add(keyword);
            }
        }

        private static double SimilarityAgainstTitle(string value, string title)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(title))
            {
                return 0;
            }

            if (title.Contains(value))
            {
                return 1;
            }

            var best = value.Distance(title);
            if (title.Length >= value.Length)
            {
                for (var index = 0; index <= title.Length - value.Length; index++)
                {
                    best = Math.Max(best, value.Distance(title.Substring(index, value.Length)));
                }
            }

            return Clamp(best);
        }

        private static int? ParseSeasonNumber(string value)
        {
            return int.TryParse(value, out var number) && number > 0 ? number : (int?)null;
        }

        private static string NormalizeExplicitSeasonMarkers(string value)
        {
            return ExplicitSeasonMarkerRegex.Replace(value ?? string.Empty, match =>
            {
                var number = ParseSeasonNumber(match.Groups["number"].Value) ??
                             ParseSeasonNumber(match.Groups["short"].Value) ??
                             ParseChineseSeasonNumber(match.Groups["chinese"].Value);
                return number.HasValue
                    ? "season" + number.Value.ToString(CultureInfo.InvariantCulture)
                    : match.Value;
            });
        }

        private static int? ParseChineseSeasonNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var decimalNumber = ParseSeasonNumber(value);
            if (decimalNumber.HasValue)
            {
                return decimalNumber;
            }

            var digits = new Dictionary<char, int>
            {
                ['零'] = 0, ['〇'] = 0, ['一'] = 1, ['二'] = 2, ['两'] = 2,
                ['三'] = 3, ['四'] = 4, ['五'] = 5, ['六'] = 6, ['七'] = 7,
                ['八'] = 8, ['九'] = 9,
            };
            var total = 0;
            var current = 0;
            foreach (var character in value)
            {
                if (digits.TryGetValue(character, out var digit))
                {
                    current = digit;
                }
                else if (character == '十')
                {
                    total += (current == 0 ? 1 : current) * 10;
                    current = 0;
                }
                else if (character == '百')
                {
                    total += (current == 0 ? 1 : current) * 100;
                    current = 0;
                }
                else
                {
                    return null;
                }
            }

            total += current;
            return total > 0 ? total : (int?)null;
        }

        private static double GetYearScore(int? expected, int? actual)
        {
            if (!expected.HasValue || expected.Value <= 0 || !actual.HasValue || actual.Value <= 0)
            {
                return 0.45;
            }

            var difference = Math.Abs(expected.Value - actual.Value);
            if (difference == 0)
            {
                return 1;
            }

            return difference == 1 ? 0.30 : 0;
        }

        private static double GetExactYearScore(int? expected, int? actual)
        {
            return expected.GetValueOrDefault() > 0 && actual.GetValueOrDefault() > 0 &&
                   expected.Value == actual.Value
                ? 1
                : 0;
        }

        private static double GetExactEpisodeScore(int expected, int actual)
        {
            return expected > 0 && actual > 0 && expected == actual ? 1 : 0;
        }

        private static double GetEpisodeScore(int expected, int actual)
        {
            if (expected <= 0 || actual <= 0)
            {
                return 0.45;
            }

            var difference = Math.Abs(expected - actual);
            if (difference == 0)
            {
                return 1;
            }

            if (difference == 1)
            {
                return 0.92;
            }

            return Clamp(1 - (double)difference / Math.Max(expected, actual));
        }

        private static string BuildReason(
            double parent,
            double season,
            double year,
            double episodes)
        {
            var parts = new List<string>();
            if (parent >= 0.95) parts.Add("父剧名出现");
            if (season >= 0.95) parts.Add("季名吻合");
            if (year >= 0.95) parts.Add("年份吻合");
            if (episodes >= 0.95) parts.Add("集数吻合");
            return parts.Count > 0 ? string.Join("、", parts) : "需要人工确认";
        }

        private static double Clamp(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

        private static double Round(double value)
        {
            return Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }
    }
}
