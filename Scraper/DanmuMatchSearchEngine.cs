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
            var sources = new Dictionary<string, ScraperSearchInfo>(StringComparer.OrdinalIgnoreCase);
            var failedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Keyword is the outer loop on purpose: first search the parent title on
            // every provider, then run the more specific season-name rounds everywhere.
            foreach (var keyword in keywords)
            {
                foreach (var scraper in scrapers)
                {
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

                            sources[BuildKey(scraper.ProviderId, searchInfo.Id)] = searchInfo;
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

                // Only stop after the current keyword has been searched on every
                // provider.  This keeps the decision global while avoiding several
                // extra full-site rounds when the parent-title round has already
                // produced an unambiguous winner.
                var currentCandidates = ScoreCandidates(
                    sources,
                    scrapers,
                    seriesName,
                    seasonName,
                    expectedYear,
                    expectedEpisodes);
                // A provider-priority tie may be auto-bound only after all fallback
                // keywords have had a chance to add stronger season evidence.
                if (DanmuMatchScorer.CanAutoSelect(currentCandidates, false))
                {
                    result.Candidates = currentCandidates;
                    return result;
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

        public static async Task<DanmuMatchSearchResult> SearchMovieAsync(
            IEnumerable<AbstractScraper> scraperSource,
            Movie movie,
            string keywordOverride,
            ILogger logger)
        {
            var result = new DanmuMatchSearchResult();
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>()).ToList();
            var movieName = string.IsNullOrWhiteSpace(keywordOverride) ? movie?.Name : keywordOverride.Trim();
            var sources = new Dictionary<string, ScraperSearchInfo>(StringComparer.OrdinalIgnoreCase);

            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                try
                {
                    // Search(BaseItem) lets every provider apply its existing Movie-specific filtering.
                    var searchItem = new Movie
                    {
                        Name = movieName ?? string.Empty,
                        ProductionYear = movie?.ProductionYear,
                    };
                    var searchResults = await scraper.Search(searchItem).ConfigureAwait(false);
                    foreach (var searchInfo in searchResults ?? new List<ScraperSearchInfo>())
                    {
                        if (searchInfo == null || string.IsNullOrWhiteSpace(searchInfo.Id) ||
                            string.IsNullOrWhiteSpace(searchInfo.Name) ||
                            DanmuMatchScorer.IsIdentifiableNonMovie(searchInfo.Category))
                        {
                            continue;
                        }

                        sources[BuildKey(scraper.ProviderId, searchInfo.Id)] = searchInfo;
                    }
                }
                catch (Exception ex)
                {
                    result.SearchErrors.Add(scraper.ProviderName + "：搜索失败");
                    logger?.LogError(ex, "[{0}] 电影智能匹配搜索失败: movie={1}", scraper.Name, movieName);
                }
            }

            var candidates = new List<DanmuMatchCandidate>();
            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                var prefix = scraper.ProviderId + "\u001f";
                candidates.AddRange(sources
                    .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(x => DanmuMatchScorer.ScoreMovie(
                        x.Value,
                        scraper.ProviderId,
                        scraper.ProviderName,
                        sourceOrder,
                        movieName,
                        movie?.ProductionYear)));
            }

            result.Candidates = OrderCandidates(candidates.Where(x => x.Score > 0));
            return result;
        }

        private static List<DanmuMatchCandidate> ScoreCandidates(
            Dictionary<string, ScraperSearchInfo> sources,
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
                        x.Value,
                        scraper.ProviderId,
                        scraper.ProviderName,
                        sourceOrder,
                        seriesName,
                        seasonName,
                        expectedYear,
                        expectedEpisodes)));
            }

            return OrderCandidates(candidates);
        }

        public static List<DanmuMatchCandidate> OrderCandidates(IEnumerable<DanmuMatchCandidate> candidates)
        {
            return (candidates ?? Enumerable.Empty<DanmuMatchCandidate>())
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
                .Take(60)
                .ToList();
        }

        private static string BuildKey(string providerId, string id)
        {
            return (providerId ?? string.Empty) + "\u001f" + (id ?? string.Empty);
        }
    }

    public sealed class DanmuMatchSearchResult
    {
        public List<DanmuMatchCandidate> Candidates { get; set; } = new List<DanmuMatchCandidate>();

        public List<string> SearchErrors { get; set; } = new List<string>();
    }
}
