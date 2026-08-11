using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Extracts a small, deterministic set of explicit title clauses.  Clauses
    /// are discovery keywords only; candidate scoring always uses the original
    /// local metadata.
    /// </summary>
    public static class DanmuTitleClauseExtractor
    {
        private const int MaximumClauses = 4;
        private static readonly Regex ClauseSeparator = new Regex(
            @"[：:。.!！?？,，;；、|/\\·・—–\-~～〜（）()【】\[\]《》〈〉「」『』]+",
            RegexOptions.Compiled);
        private static readonly Regex SeasonSuffix = new Regex(
            @"\s*(?:(?:第\s*[0-9一二三四五六七八九十百零〇两]+\s*季)|(?:season\s*[0-9ivx]+))\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> GenericClauses = new HashSet<string>(
            new[] { "动画", "动漫", "电影", "剧场版", "电视剧", "正片", "本篇", "全集", "普通话", "国语" },
            StringComparer.OrdinalIgnoreCase);

        public static List<string> Extract(string title, IEnumerable<string> excludedKeywords = null)
        {
            var clauses = new List<string>();
            if (string.IsNullOrWhiteSpace(title))
            {
                return clauses;
            }

            var full = DanmuMatchScorer.Normalize(title);
            var excluded = new HashSet<string>(
                (excludedKeywords ?? Enumerable.Empty<string>())
                    .Select(DanmuMatchScorer.Normalize)
                    .Where(x => x.Length > 0),
                StringComparer.OrdinalIgnoreCase);

            foreach (var raw in ClauseSeparator.Split(title))
            {
                var clause = (raw ?? string.Empty).Trim();
                var normalized = DanmuMatchScorer.Normalize(clause);
                if (normalized.Length < 3 || normalized == full || excluded.Contains(normalized) ||
                    GenericClauses.Contains(normalized) ||
                    clauses.Any(x => string.Equals(
                        DanmuMatchScorer.Normalize(x), normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                clauses.Add(clause);
                if (clauses.Count >= MaximumClauses)
                {
                    break;
                }
            }

            return clauses;
        }

        public static List<string> ExtractProviderAliases(
            IEnumerable<string> providerTitles,
            IEnumerable<string> excludedKeywords)
        {
            var aliases = new List<string>();
            var excluded = (excludedKeywords ?? Enumerable.Empty<string>()).ToList();
            foreach (var providerTitle in providerTitles ?? Enumerable.Empty<string>())
            {
                var stem = SeasonSuffix.Replace(providerTitle ?? string.Empty, string.Empty).Trim();
                foreach (var clause in Extract(stem, excluded.Concat(aliases)))
                {
                    aliases.Add(clause);
                    if (aliases.Count >= 2)
                    {
                        return aliases;
                    }
                }

                var normalizedStem = DanmuMatchScorer.Normalize(stem);
                if (aliases.Count == 0 && normalizedStem.Length >= 3 &&
                    !excluded.Any(x => string.Equals(
                        DanmuMatchScorer.Normalize(x), normalizedStem, StringComparison.OrdinalIgnoreCase)))
                {
                    aliases.Add(stem);
                }
                if (aliases.Count >= 2)
                {
                    break;
                }
            }

            return aliases;
        }
    }
}
