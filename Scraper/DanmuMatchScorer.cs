using System;
using System.Collections.Generic;
using System.Linq;
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

        private static readonly Regex SeasonNumberRegex = new Regex(
            @"(?:第\s*[0-9一二三四五六七八九十百零〇两]+\s*季|season\s*\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GenericSeasonLabelRegex = new Regex(
            @"^(?:(?:第\s*)?[0-9一二三四五六七八九十百零〇两]+\s*季|season\s*\d+|s\s*\d+|part\s*\d+|第?\s*[0-9一二三四五六七八九十百零〇两]+\s*(?:部|篇))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GenericSeasonNumberRegex = new Regex(
            @"^(?:(?:第\s*)?[0-9一二三四五六七八九十百零〇两]+\s*季|season\s*\d+|s\s*\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SeparatorRegex = new Regex(
            @"[\s\p{P}\p{S}]+",
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

        public static bool IsEligibleSeasonCandidate(
            ScraperSearchInfo source,
            string seriesName,
            string seasonName,
            string keywordOverride)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Id) ||
                string.IsNullOrWhiteSpace(source.Name) || IsIdentifiableMovie(source.Category))
            {
                return false;
            }

            var title = Normalize(source.Name);
            if (!string.IsNullOrWhiteSpace(keywordOverride))
            {
                return HasTitleEvidence(keywordOverride, title, SeasonCandidateTitleEligibilityFloor);
            }

            var parent = SanitizeSearchTerm(seriesName);
            if (!IsIdentityBearingTitle(parent))
            {
                return false;
            }

            var parentEvidence = GetTitleEvidence(parent, title);
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
            int expectedEpisodes)
        {
            var title = Normalize(source.Name);
            var parent = Normalize(seriesName);
            var seasonKeyword = Normalize(ExtractSeasonKeyword(seriesName, seasonName));
            var combined = Normalize((seriesName ?? string.Empty) + (seasonKeyword ?? string.Empty));

            var parentScore = SimilarityAgainstTitle(parent, title);
            var keywordScore = SimilarityAgainstTitle(seasonKeyword, title);
            var combinedScore = SimilarityAgainstTitle(combined, title);
            double titleScore;

            if (!string.IsNullOrEmpty(seasonKeyword))
            {
                if (title.Contains(seasonKeyword))
                {
                    titleScore = 0.78 + (title.Contains(parent) && !string.IsNullOrEmpty(parent) ? 0.22 : 0.22 * parentScore);
                }
                else
                {
                    titleScore = Math.Max(combinedScore, keywordScore * 0.62 + parentScore * 0.38);
                }
            }
            else
            {
                titleScore = parentScore;
            }

            var yearScore = GetYearScore(expectedYear, source.Year);
            var episodeScore = GetEpisodeScore(expectedEpisodes, source.EpisodeSize);
            var score = !string.IsNullOrEmpty(seasonKeyword)
                ? titleScore * 0.45 + yearScore * 0.15 + episodeScore * 0.40
                : titleScore * 0.55 + yearScore * 0.15 + episodeScore * 0.30;

            if (!string.IsNullOrEmpty(seasonKeyword) && keywordScore < 0.72)
            {
                score *= 0.72;
            }

            if (!string.IsNullOrWhiteSpace(source.Category) && source.Category.Contains("电影"))
            {
                score *= 0.45;
            }

            score = Clamp(score);
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
                KeywordScore = Round(keywordScore),
                YearScore = Round(yearScore),
                EpisodeScore = Round(episodeScore),
                Reason = BuildReason(parentScore, keywordScore, yearScore, episodeScore),
            };
        }

        public static DanmuMatchCandidate ScoreMovie(
            ScraperSearchInfo source,
            string site,
            string siteName,
            int sourceOrder,
            string movieName,
            int? expectedYear)
        {
            var titleScore = SimilarityAgainstTitle(Normalize(movieName), Normalize(source.Name));
            var yearScore = GetYearScore(expectedYear, source.Year);
            var score = titleScore * 0.82 + yearScore * 0.18;
            if (IsIdentifiableNonMovie(source.Category))
            {
                score = 0;
            }

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
            var confident = (candidates ?? new List<DanmuMatchCandidate>())
                .Where(x => x != null && x.Score >= 0.90)
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
            var highestScore = candidates.Max(x => x.Score);
            var winners = candidates.Where(x => x.Score == highestScore).ToList();
            return winners.Count == 1 ? winners[0] : null;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Providers and TMDB occasionally mix these homophones/variants.
            var normalized = SanitizeSearchTerm(value).ToLowerInvariant()
                .Replace('谭', '潭')
                .Replace('臺', '台')
                .Replace('裏', '里');
            return SeparatorRegex.Replace(normalized, string.Empty);
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

        private static string BuildReason(double parent, double keyword, double year, double episodes)
        {
            var parts = new List<string>();
            if (keyword >= 0.95) parts.Add("季名关键词吻合");
            if (parent >= 0.95) parts.Add("父剧名吻合");
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
