using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Performs provider-neutral season searches.  Both the manual matching API and
    /// automatic library-import flow use this class so their candidate sets and
    /// selection decisions cannot drift apart.
    /// </summary>
    public static class DanmuMatchSearchEngine
    {
        public static async Task<DanmuMatchSearchResult> SearchSeasonAsync(
            IEnumerable<AbstractScraper> scraperSource,
            string seriesName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes,
            string keywordOverride,
            ILogger logger)
        {
            var result = new DanmuMatchSearchResult();
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>()).ToList();
            var keywords = DanmuMatchScorer.BuildSearchKeywords(seriesName, seasonName, keywordOverride);
            var sources = new Dictionary<string, DiscoveredSearchInfo>(StringComparer.OrdinalIgnoreCase);
            var failedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Keyword is the outer loop on purpose: first search the parent title on
            // every provider, then run the more specific season-name rounds everywhere.
            foreach (var keyword in keywords)
            {
                foreach (var scraper in scrapers)
                {
                    await SearchProviderAsync(
                        scraper, keyword, seasonName, sources, queried, failedSites, result, logger, false)
                        .ConfigureAwait(false);
                }

                // Do not finalize after an individual keyword round.  r6 requires
                // all enabled sites and all fallback evidence before site-priority
                // selection, otherwise an early response can hide a better local
                // candidate or incorrectly decide a cross-site result.
            }

            // Explicit custom keywords remain isolated. Otherwise, only providers
            // that still lack a 0.90 candidate receive bounded punctuation-clause
            // fallback rounds. Search terms discover candidates; the original full
            // metadata remains authoritative for every score below.
            if (string.IsNullOrWhiteSpace(keywordOverride))
            {
                var standardCandidates = ScoreCandidates(
                    sources, scrapers, seriesName, seasonName, expectedYear, expectedEpisodes);
                var confidentSites = new HashSet<string>(
                    standardCandidates.Where(x => x.Score >= 0.90).Select(x => x.Site),
                    StringComparer.OrdinalIgnoreCase);
                var clauses = DanmuTitleClauseExtractor.Extract(seriesName, keywords);

                foreach (var scraper in scrapers.Where(x => !confidentSites.Contains(x.ProviderId)))
                {
                    foreach (var clause in clauses)
                    {
                        await SearchProviderAsync(
                            scraper, clause, seasonName, sources, queried, failedSites, result, logger, true)
                            .ConfigureAwait(false);

                        var providerConfident = ScoreCandidates(
                                sources, scrapers, seriesName, seasonName, expectedYear, expectedEpisodes)
                            .Any(x => string.Equals(x.Site, scraper.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                                      x.Score >= 0.90);
                        if (providerConfident)
                        {
                            break;
                        }
                    }

                    var providerScores = ScoreCandidates(
                            sources, scrapers, seriesName, seasonName, expectedYear, expectedEpisodes)
                        .Where(x => string.Equals(
                            x.Site, scraper.ProviderId, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (providerScores.Any(x => x.Score >= 0.90))
                    {
                        continue;
                    }

                    // A provider may know the same work under an upstream alias.
                    // Derive at most two second-hop terms from already discovered,
                    // strongly related titles. This is provider-local and contains
                    // no title-specific alias dictionary.
                    var providerAliases = DanmuTitleClauseExtractor.ExtractProviderAliases(
                        providerScores
                            .Where(x => x.ParentTitleScore >= 0.72)
                            .OrderByDescending(x => x.ParentTitleScore)
                            .ThenBy(x => x.Name, StringComparer.Ordinal)
                            .Select(x => x.Name),
                        keywords.Concat(clauses));
                    foreach (var providerAlias in providerAliases)
                    {
                        await SearchProviderAsync(
                            scraper, providerAlias, seasonName, sources, queried, failedSites, result, logger, true)
                            .ConfigureAwait(false);
                        if (ScoreCandidates(
                                sources, scrapers, seriesName, seasonName, expectedYear, expectedEpisodes)
                            .Any(x => string.Equals(
                                          x.Site, scraper.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                                      x.Score >= 0.90))
                        {
                            break;
                        }
                    }
                }
            }

            result.Candidates = ScoreCandidates(
                sources,
                scrapers,
                seriesName,
                seasonName,
                expectedYear,
                expectedEpisodes);

            return result;
        }

        private static async Task SearchProviderAsync(
            AbstractScraper scraper,
            string keyword,
            string seasonName,
            IDictionary<string, DiscoveredSearchInfo> sources,
            ISet<string> queried,
            ISet<string> failedSites,
            DanmuMatchSearchResult result,
            ILogger logger,
            bool aliasRound)
        {
            if (scraper == null || string.IsNullOrWhiteSpace(keyword) ||
                !queried.Add(BuildKey(scraper.ProviderId, DanmuMatchScorer.Normalize(keyword))))
            {
                return;
            }

            try
            {
                var searchResults = await scraper.SearchForApi(keyword).ConfigureAwait(false);
                foreach (var searchInfo in searchResults ?? new List<ScraperSearchInfo>())
                {
                    if (searchInfo == null || string.IsNullOrWhiteSpace(searchInfo.Id) ||
                        string.IsNullOrWhiteSpace(searchInfo.Name))
                    {
                        continue;
                    }

                    AddDiscoveredSource(sources, scraper.ProviderId, searchInfo, aliasRound);
                }
            }
            catch (Exception ex)
            {
                if (failedSites.Add(scraper.ProviderId))
                {
                    result.SearchErrors.Add(scraper.ProviderName + "：搜索失败");
                }

                logger?.LogError(
                    ex,
                    "[{0}] 智能匹配搜索失败: keyword={1}, season={2}",
                    scraper.Name,
                    keyword,
                    seasonName);
            }
        }

        public static async Task<DanmuMatchSearchResult> SearchMovieAsync(
            IEnumerable<AbstractScraper> scraperSource,
            Movie movie,
            string keywordOverride,
            ILogger logger)
        {
            var result = new DanmuMatchSearchResult();
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>()).ToList();
            var movieName = string.IsNullOrWhiteSpace(keywordOverride) ? movie?.Name : keywordOverride.Trim();
            var sources = new Dictionary<string, DiscoveredSearchInfo>(StringComparer.OrdinalIgnoreCase);
            var failedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var scraper in scrapers)
            {
                await SearchMovieProviderAsync(
                    scraper, movieName, movie?.ProductionYear, sources, queried, failedSites, result, logger, false)
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(keywordOverride))
            {
                var standardCandidates = ScoreMovieCandidates(
                    sources, scrapers, movieName, movie?.ProductionYear);
                var confidentSites = new HashSet<string>(
                    standardCandidates.Where(x => x.Score >= 0.90).Select(x => x.Site),
                    StringComparer.OrdinalIgnoreCase);
                var clauses = DanmuTitleClauseExtractor.Extract(movieName, new[] { movieName });

                foreach (var scraper in scrapers.Where(x => !confidentSites.Contains(x.ProviderId)))
                {
                    foreach (var clause in clauses)
                    {
                        await SearchMovieProviderAsync(
                            scraper, clause, movie?.ProductionYear, sources, queried, failedSites, result, logger, true)
                            .ConfigureAwait(false);
                        if (ScoreMovieCandidates(sources, scrapers, movieName, movie?.ProductionYear)
                            .Any(x => string.Equals(x.Site, scraper.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                                      x.Score >= 0.90))
                        {
                            break;
                        }
                    }

                    var providerScores = ScoreMovieCandidates(
                            sources, scrapers, movieName, movie?.ProductionYear)
                        .Where(x => string.Equals(x.Site, scraper.ProviderId, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (providerScores.Any(x => x.Score >= 0.90))
                    {
                        continue;
                    }

                    var aliases = DanmuTitleClauseExtractor.ExtractProviderAliases(
                        providerScores
                            .Where(x => x.ParentTitleScore >= 0.72)
                            .OrderByDescending(x => x.ParentTitleScore)
                            .ThenBy(x => x.Name, StringComparer.Ordinal)
                            .Select(x => x.Name),
                        new[] { movieName }.Concat(clauses));
                    foreach (var alias in aliases)
                    {
                        await SearchMovieProviderAsync(
                            scraper, alias, movie?.ProductionYear, sources, queried, failedSites, result, logger, true)
                            .ConfigureAwait(false);
                        if (ScoreMovieCandidates(sources, scrapers, movieName, movie?.ProductionYear)
                            .Any(x => string.Equals(x.Site, scraper.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                                      x.Score >= 0.90))
                        {
                            break;
                        }
                    }
                }
            }

            result.Candidates = ScoreMovieCandidates(sources, scrapers, movieName, movie?.ProductionYear);
            return result;
        }

        private static async Task SearchMovieProviderAsync(
            AbstractScraper scraper,
            string keyword,
            int? expectedYear,
            IDictionary<string, DiscoveredSearchInfo> sources,
            ISet<string> queried,
            ISet<string> failedSites,
            DanmuMatchSearchResult result,
            ILogger logger,
            bool aliasRound)
        {
            if (scraper == null || string.IsNullOrWhiteSpace(keyword) ||
                !queried.Add(BuildKey(scraper.ProviderId, DanmuMatchScorer.Normalize(keyword))))
            {
                return;
            }

            try
            {
                var searchResults = await scraper.Search(new Movie
                {
                    Name = keyword.Trim(),
                    ProductionYear = expectedYear,
                }).ConfigureAwait(false);
                foreach (var searchInfo in searchResults ?? new List<ScraperSearchInfo>())
                {
                    if (searchInfo == null || string.IsNullOrWhiteSpace(searchInfo.Id) ||
                        string.IsNullOrWhiteSpace(searchInfo.Name) ||
                        DanmuMatchScorer.IsIdentifiableNonMovie(searchInfo.Category))
                    {
                        continue;
                    }

                    AddDiscoveredSource(sources, scraper.ProviderId, searchInfo, aliasRound);
                }
            }
            catch (Exception ex)
            {
                if (failedSites.Add(scraper.ProviderId))
                {
                    result.SearchErrors.Add(scraper.ProviderName + "：搜索失败");
                }
                logger?.LogError(ex, "[{0}] 电影智能匹配搜索失败: movie={1}", scraper.Name, keyword);
            }
        }

        private static List<DanmuMatchCandidate> ScoreMovieCandidates(
            Dictionary<string, DiscoveredSearchInfo> sources,
            IList<AbstractScraper> scrapers,
            string movieName,
            int? expectedYear)
        {
            var candidates = new List<DanmuMatchCandidate>();
            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                var prefix = scraper.ProviderId + "\u001f";
                candidates.AddRange(sources
                    .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(x => DanmuMatchScorer.ScoreMovie(
                        x.Value.Info,
                        scraper.ProviderId,
                        scraper.ProviderName,
                        sourceOrder,
                        movieName,
                        expectedYear,
                        x.Value.AliasOnly)));
            }

            return OrderCandidates(candidates.Where(x => x.Score > 0));
        }

        private static List<DanmuMatchCandidate> ScoreCandidates(
            Dictionary<string, DiscoveredSearchInfo> sources,
            IList<AbstractScraper> scrapers,
            string seriesName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes)
        {
            var candidates = new List<DanmuMatchCandidate>();
            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                var prefix = scraper.ProviderId + "\u001f";
                candidates.AddRange(sources
                    .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(x => DanmuMatchScorer.Score(
                        x.Value.Info,
                        scraper.ProviderId,
                        scraper.ProviderName,
                        sourceOrder,
                        seriesName,
                        seasonName,
                        expectedYear,
                        expectedEpisodes,
                        x.Value.AliasOnly)));
            }

            return OrderCandidates(candidates);
        }

        public static List<DanmuMatchCandidate> OrderCandidates(IEnumerable<DanmuMatchCandidate> candidates)
        {
            var ordered = (candidates ?? Enumerable.Empty<DanmuMatchCandidate>())
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.SourceOrder)
                .ThenByDescending(x => x.TitleScore)
                .ThenByDescending(x => x.ParentTitleScore)
                .ThenByDescending(x => x.KeywordScore)
                .ThenByDescending(x => x.EpisodeScore)
                .ThenByDescending(x => x.YearScore)
                .ThenBy(x => x.SiteName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var selected = DanmuMatchScorer.SelectAutoCandidate(ordered);
            if (selected == null)
            {
                return ordered.Take(60).ToList();
            }

            // The explicit selected fields remain authoritative, but placing the
            // chosen site's rows first makes the configured decision visible to
            // older clients that only render candidates.
            return ordered
                .OrderByDescending(x => x.SourceOrder == selected.SourceOrder)
                .ThenByDescending(x => x.SourceOrder == selected.SourceOrder ? x.Score : 0)
                .ThenByDescending(x => x.Score)
                .ThenBy(x => x.SourceOrder)
                .ThenBy(x => x.SiteName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Take(60)
                .ToList();
        }

        private static string BuildKey(string providerId, string id)
        {
            return (providerId ?? string.Empty) + "\u001f" + (id ?? string.Empty);
        }

        private static void AddDiscoveredSource(
            IDictionary<string, DiscoveredSearchInfo> sources,
            string providerId,
            ScraperSearchInfo searchInfo,
            bool aliasRound)
        {
            var key = BuildKey(providerId, searchInfo.Id);
            if (!sources.TryGetValue(key, out var existing))
            {
                sources[key] = new DiscoveredSearchInfo
                {
                    Info = searchInfo,
                    AliasOnly = aliasRound,
                };
                return;
            }

            if (!aliasRound)
            {
                existing.Info = searchInfo;
                existing.AliasOnly = false;
            }
        }

        private sealed class DiscoveredSearchInfo
        {
            public ScraperSearchInfo Info { get; set; }
            public bool AliasOnly { get; set; }
        }
    }

    public sealed class DanmuMatchSearchResult
    {
        public List<DanmuMatchCandidate> Candidates { get; set; } = new List<DanmuMatchCandidate>();

        public List<string> SearchErrors { get; set; } = new List<string>();
    }
}
