using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Tmdb;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Provider-neutral, bounded candidate discovery.  All planned terms are
    /// known before execution. Terms remain serial per provider while provider
    /// work shares one global bounded gate across interactive and import paths.
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
            ILogger logger,
            IEnumerable<string> localSeriesTitleAliases = null,
            IEnumerable<string> localSeasonTitleAliases = null,
            BaseItem contextItem = null)
        {
            using (var automaticDeadline = new CancellationTokenSource(
                BoundedSearchPolicy.Shared.Options.AutomaticOperationTimeout))
            {
                return await SearchSeasonAsync(
                    scraperSource,
                    seriesName,
                    seasonName,
                    expectedYear,
                    expectedEpisodes,
                    keywordOverride,
                    logger,
                    BoundedSearchPolicy.Shared,
                    automaticDeadline.Token,
                    automaticDeadline.Token,
                    localSeriesTitleAliases,
                    localSeasonTitleAliases,
                    contextItem).ConfigureAwait(false);
            }
        }

        public static async Task<DanmuMatchSearchResult> SearchSeasonAsync(
            IEnumerable<AbstractScraper> scraperSource,
            string seriesName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes,
            string keywordOverride,
            ILogger logger,
            BoundedSearchPolicy policy,
            CancellationToken cancellationToken)
        {
            return await SearchSeasonAsync(
                scraperSource,
                seriesName,
                seasonName,
                expectedYear,
                expectedEpisodes,
                keywordOverride,
                logger,
                policy,
                cancellationToken,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Separates the bounded child execution token from the parent/user
        /// cancellation token.  Child budget exhaustion remains an actionable
        /// partial result; parent cancellation invalidates the whole result.
        /// </summary>
        public static async Task<DanmuMatchSearchResult> SearchSeasonAsync(
            IEnumerable<AbstractScraper> scraperSource,
            string seriesName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes,
            string keywordOverride,
            ILogger logger,
            BoundedSearchPolicy policy,
            CancellationToken executionCancellationToken,
            CancellationToken parentCancellationToken,
            IEnumerable<string> localSeriesTitleAliases = null,
            IEnumerable<string> localSeasonTitleAliases = null,
            BaseItem contextItem = null)
        {
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>()).ToList();
            // Prefer the authoritative Emby Season ordinal. Display names are
            // only a compatibility fallback for callers without an item.
            var targetSeasonNumber = contextItem?.IndexNumber ??
                                     DanmuMatchScorer.ParseExplicitSeasonNumber(seasonName);
            var keywords = DanmuMatchScorer.BuildSearchKeywords(seriesName, seasonName, keywordOverride)
                .Take(string.IsNullOrWhiteSpace(keywordOverride) ? 2 : 1)
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var outcomes = await ExecutePlannedCallsAsync(
                scrapers,
                keywords,
                (scraper, keyword, cancellationToken) =>
                    scraper.SearchForApiWithDiagnostics(keyword, cancellationToken),
                searchInfo => DanmuMatchScorer.IsEligibleSeasonCandidate(
                    searchInfo, seriesName, seasonName, keywordOverride,
                    localSeriesTitleAliases, localSeasonTitleAliases),
                policy,
                executionCancellationToken,
                logger,
                "season").ConfigureAwait(false);
            var result = ToResult(outcomes, parentCancellationToken);
            if (keywords.Count == 0)
            {
                result.IsComplete = false;
                result.CompletionDiagnostics.Add(new DanmuSearchCompletionDiagnostic
                {
                    Status = "invalid_metadata",
                    Message = "An identity-bearing Series title is required for Season search.",
                });
                result.SearchErrors.Add("An identity-bearing Series title is required for Season search.");
            }

            result.CanonicalCandidates = ScoreCandidates(
                MergeSources(outcomes),
                scrapers,
                seriesName,
                seasonName,
                expectedYear,
                expectedEpisodes,
                localSeriesTitleAliases,
                localSeasonTitleAliases,
                string.IsNullOrWhiteSpace(keywordOverride),
                targetSeasonNumber);
            result.Candidates = OrderCandidates(result.CanonicalCandidates);
            if (string.IsNullOrWhiteSpace(keywordOverride))
            {
                await TryApplyTmdbAliasesAsync(result, scrapers, contextItem, seriesName, seasonName,
                    expectedYear, expectedEpisodes, false, targetSeasonNumber, logger,
                    executionCancellationToken, parentCancellationToken).ConfigureAwait(false);
            }
            ClassifyResult(result);
            return result;
        }

        public static async Task<DanmuMatchSearchResult> SearchMovieAsync(
            IEnumerable<AbstractScraper> scraperSource,
            Movie movie,
            string keywordOverride,
            ILogger logger)
        {
            using (var automaticDeadline = new CancellationTokenSource(
                BoundedSearchPolicy.Shared.Options.AutomaticOperationTimeout))
            {
                return await SearchMovieAsync(
                    scraperSource,
                    movie,
                    keywordOverride,
                    logger,
                    BoundedSearchPolicy.Shared,
                    automaticDeadline.Token).ConfigureAwait(false);
            }
        }

        public static async Task<DanmuMatchSearchResult> SearchMovieAsync(
            IEnumerable<AbstractScraper> scraperSource,
            Movie movie,
            string keywordOverride,
            ILogger logger,
            BoundedSearchPolicy policy,
            CancellationToken cancellationToken,
            BaseItem contextItem = null)
        {
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>()).ToList();
            var movieName = string.IsNullOrWhiteSpace(keywordOverride) ? movie?.Name : keywordOverride.Trim();
            var expectedYear = movie?.ProductionYear;
            // Movies intentionally have one standard metadata term.
            var keywords = string.IsNullOrWhiteSpace(movieName)
                ? new List<string>()
                : new List<string> { movieName };
            var outcomes = await ExecutePlannedCallsAsync(
                scrapers,
                keywords,
                (scraper, keyword, cancellationToken) => scraper.SearchWithDiagnostics(new Movie
                    {
                        Name = keyword.Trim(),
                        ProductionYear = expectedYear,
                    }, cancellationToken),
                searchInfo => searchInfo != null &&
                              !string.IsNullOrWhiteSpace(searchInfo.Id) &&
                              !string.IsNullOrWhiteSpace(searchInfo.Name) &&
                              !DanmuMatchScorer.IsIdentifiableNonMovie(searchInfo.Category),
                policy,
                cancellationToken,
                logger,
                "movie").ConfigureAwait(false);
            var result = ToResult(outcomes, cancellationToken);
            var localAliases = string.IsNullOrWhiteSpace(keywordOverride) ||
                               string.Equals(keywordOverride?.Trim(), movie?.Name, StringComparison.OrdinalIgnoreCase)
                ? new[] { movie?.OriginalTitle }
                : Enumerable.Empty<string>();
            result.CanonicalCandidates = ScoreMovieCandidates(
                MergeSources(outcomes), scrapers, movieName, expectedYear, localAliases);
            result.Candidates = OrderCandidates(result.CanonicalCandidates);
            if (string.IsNullOrWhiteSpace(keywordOverride))
            {
                await TryApplyTmdbAliasesAsync(result, scrapers, contextItem ?? movie, movieName, string.Empty,
                    expectedYear, 0, true, null, logger,
                    cancellationToken, cancellationToken).ConfigureAwait(false);
            }
            ClassifyResult(result);
            return result;
        }

        private static bool IsAnimationContext(BaseItem item)
        {
            return TmdbAliasClient.IsAnimated(item);
        }

        private static async Task TryApplyTmdbAliasesAsync(
            DanmuMatchSearchResult result,
            IList<AbstractScraper> scrapers,
            BaseItem contextItem,
            string seriesOrMovieName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes,
            bool isMovie,
            int? targetSeasonNumber,
            ILogger logger,
            CancellationToken executionCancellationToken,
            CancellationToken parentCancellationToken)
        {
            var dandan = scrapers.FirstOrDefault(x =>
                string.Equals(x.ProviderId, Dandan.Dandan.ScraperProviderId, StringComparison.OrdinalIgnoreCase));
            if (dandan == null || contextItem == null)
            {
                return;
            }

            var baseline = result.Candidates
                .Where(x => string.Equals(x.Site, dandan.ProviderId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (baseline.Any(x => x.Score >= 0.80))
            {
                return;
            }

            var option = Plugin.Instance?.Configuration?.Tmdb;
            var aliases = await TmdbAliasClient.GetAliasesAsync(
                contextItem, option, logger, executionCancellationToken).ConfigureAwait(false);
            if (executionCancellationToken.IsCancellationRequested)
            {
                MarkAliasSearchCancellation(result, parentCancellationToken);
                return;
            }

            var aliasCandidates = new List<DanmuMatchCandidate>();
            var sourceOrder = scrapers.IndexOf(dandan);
            var attemptedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reachedThreshold = false;
            foreach (var alias in aliases?.BuildSearchPlan(seriesOrMovieName) ?? Enumerable.Empty<TmdbAliasTitle>())
            {
                reachedThreshold = await SearchTmdbTermAsync(
                    dandan, alias?.Title, aliasCandidates, attemptedTerms, sourceOrder,
                    seriesOrMovieName, seasonName, targetSeasonNumber, expectedYear,
                    expectedEpisodes, isMovie, logger, executionCancellationToken).ConfigureAwait(false);
                if (executionCancellationToken.IsCancellationRequested)
                {
                    MarkAliasSearchCancellation(result, parentCancellationToken);
                    return;
                }

                if (reachedThreshold)
                {
                    break;
                }
            }

            // Detail documents are deliberately lazy. A successful Chinese term makes
            // neither primary-title request, while a failed alias endpoint still leaves
            // the baseline and these independent fallback rounds available.
            var englishDetails = default(TmdbMediaDetails);
            if (!reachedThreshold)
            {
                englishDetails = await TmdbAliasClient.GetDetailsAsync(
                    contextItem, option, "en-US", logger, executionCancellationToken).ConfigureAwait(false);
                if (executionCancellationToken.IsCancellationRequested)
                {
                    MarkAliasSearchCancellation(result, parentCancellationToken);
                    return;
                }

                reachedThreshold = await SearchTmdbTermAsync(
                    dandan,
                    TmdbAliasClient.GetLocalizedPrimaryTitle(englishDetails, isMovie),
                    aliasCandidates, attemptedTerms, sourceOrder, seriesOrMovieName, seasonName,
                    targetSeasonNumber, expectedYear, expectedEpisodes, isMovie, logger,
                    executionCancellationToken).ConfigureAwait(false);
            }

            if (!reachedThreshold && !executionCancellationToken.IsCancellationRequested)
            {
                var japaneseTitle = string.Equals(englishDetails?.OriginalLanguage, "ja",
                    StringComparison.OrdinalIgnoreCase)
                    ? TmdbAliasClient.GetJapaneseOriginalPrimaryTitle(englishDetails, isMovie)
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(japaneseTitle))
                {
                    var japaneseDetails = await TmdbAliasClient.GetDetailsAsync(
                        contextItem, option, "ja-JP", logger, executionCancellationToken).ConfigureAwait(false);
                    if (executionCancellationToken.IsCancellationRequested)
                    {
                        MarkAliasSearchCancellation(result, parentCancellationToken);
                        return;
                    }

                    japaneseTitle = TmdbAliasClient.GetLocalizedPrimaryTitle(japaneseDetails, isMovie);
                }

                await SearchTmdbTermAsync(
                    dandan, japaneseTitle, aliasCandidates, attemptedTerms, sourceOrder,
                    seriesOrMovieName, seasonName, targetSeasonNumber, expectedYear,
                    expectedEpisodes, isMovie, logger, executionCancellationToken).ConfigureAwait(false);
            }

            ApplyTmdbAliasCandidates(result, aliasCandidates);
        }

        private static async Task<bool> SearchTmdbTermAsync(
            AbstractScraper dandan,
            string term,
            List<DanmuMatchCandidate> aliasCandidates,
            ISet<string> attemptedTerms,
            int sourceOrder,
            string seriesOrMovieName,
            string seasonName,
            int? targetSeasonNumber,
            int? expectedYear,
            int expectedEpisodes,
            bool isMovie,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var normalized = DanmuMatchScorer.Normalize(term);
            if (normalized.Length == 0 || !attemptedTerms.Add(normalized))
            {
                return aliasCandidates.Any(x => x.Score >= 0.80);
            }

            try
            {
                List<ScraperSearchInfo> searchResults;
                if (isMovie)
                {
                    searchResults = await dandan.Search(new Movie
                    {
                        Name = term,
                        ProductionYear = expectedYear,
                    }, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    searchResults = await dandan.SearchForApi(term, cancellationToken).ConfigureAwait(false);
                }

                var sources = (searchResults ?? new List<ScraperSearchInfo>())
                    .Where(source => source != null && !string.IsNullOrWhiteSpace(source.Id) &&
                                     !string.IsNullOrWhiteSpace(source.Name) &&
                                     (!isMovie || !DanmuMatchScorer.IsIdentifiableNonMovie(source.Category)))
                    .ToList();
                foreach (var source in sources)
                {
                    source.SearchAlias = term;
                }

                aliasCandidates.AddRange(isMovie
                    ? sources.Select(source => DanmuMatchScorer.ScoreMovie(
                        source, dandan.ProviderId, dandan.ProviderName, sourceOrder, term, expectedYear))
                    : sources.Select(source => DanmuMatchScorer.Score(
                        source, dandan.ProviderId, dandan.ProviderName, sourceOrder, term,
                        BuildAliasSeasonName(term, seriesOrMovieName, seasonName), expectedYear,
                        expectedEpisodes, null, null, true, targetSeasonNumber)));
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[弹弹play] TMDB primary-title search failed for one term");
            }

            return aliasCandidates.Any(x => x.Score >= 0.80);
        }

        internal static void ApplyTmdbAliasCandidates(
            DanmuMatchSearchResult result,
            IEnumerable<DanmuMatchCandidate> aliasCandidates)
        {
            var canonicalCandidates = OrderCanonicalCandidates(
                (aliasCandidates ?? Enumerable.Empty<DanmuMatchCandidate>()).Where(x => x.Score > 0));
            if (canonicalCandidates.Count == 0)
            {
                return;
            }

            result.CanonicalCandidates = canonicalCandidates;
            result.Candidates = OrderCandidates(canonicalCandidates);
            result.UsedTmdbAlias = true;
        }

        private static void MarkAliasSearchCancellation(
            DanmuMatchSearchResult result,
            CancellationToken parentCancellationToken)
        {
            var parentCancelled = parentCancellationToken.IsCancellationRequested;
            result.IsComplete = false;
            result.WasCancelled = parentCancelled;
            result.CompletionDiagnostics.Add(new DanmuSearchCompletionDiagnostic
            {
                Provider = Dandan.Dandan.ScraperProviderId,
                Status = parentCancelled ? "cancelled" : "timeout",
                Message = parentCancelled
                    ? "TMDB alias search was cancelled."
                    : "TMDB alias search exceeded the bounded operation deadline.",
                TimedOut = !parentCancelled,
                Cancelled = parentCancelled,
            });
        }

        private static string BuildAliasSeasonName(string alias, string originalSeriesName, string originalSeasonName)
        {
            var keyword = DanmuMatchScorer.ExtractSeasonKeyword(originalSeriesName, originalSeasonName);
            return string.IsNullOrWhiteSpace(keyword) ? alias : alias + " " + keyword;
        }

        private static async Task<List<ProviderSearchOutcome>> ExecutePlannedCallsAsync(
            IList<AbstractScraper> scrapers,
            IList<string> keywords,
            Func<AbstractScraper, string, CancellationToken, Task<ScraperSearchResult>> search,
            Func<ScraperSearchInfo, bool> include,
            BoundedSearchPolicy policy,
            CancellationToken cancellationToken,
            ILogger logger,
            string searchKind)
        {
            var boundedPolicy = policy ?? BoundedSearchPolicy.Shared;
            var tasks = new List<Task<ProviderSearchOutcome>>();
            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                tasks.Add(ExecuteProviderCallsAsync(
                    scraper,
                    sourceOrder,
                    keywords,
                    search,
                    include,
                    boundedPolicy,
                    cancellationToken,
                    logger,
                    searchKind));
            }

            return (await Task.WhenAll(tasks).ConfigureAwait(false))
                .OrderBy(outcome => outcome.SourceOrder)
                .ToList();
        }

        private static async Task<ProviderSearchOutcome> ExecuteProviderCallsAsync(
            AbstractScraper scraper,
            int sourceOrder,
            IEnumerable<string> keywords,
            Func<AbstractScraper, string, CancellationToken, Task<ScraperSearchResult>> search,
            Func<ScraperSearchInfo, bool> include,
            BoundedSearchPolicy policy,
            CancellationToken cancellationToken,
            ILogger logger,
            string searchKind)
        {
            var outcome = new ProviderSearchOutcome(sourceOrder, scraper);
            var plannedKeywords = (keywords ?? Enumerable.Empty<string>()).ToList();
            if (scraper == null)
            {
                foreach (var keyword in plannedKeywords)
                {
                    outcome.AddDiagnostic("unstarted", keyword, 0, "Provider is unavailable.", false, true);
                }

                return outcome;
            }

            // One provider's terms intentionally execute in order. A timed-out
            // non-cooperative term retains that provider's gate until it ends,
            // so later planned terms are cancelled by the enclosing deadline
            // rather than running concurrently against the same site.
            foreach (var keyword in plannedKeywords)
            {
                var stopwatch = Stopwatch.StartNew();
                var execution = await policy.ExecuteAsync(
                    scraper.ProviderId,
                    providerCancellationToken => search(scraper, keyword, providerCancellationToken),
                    cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                switch (execution.Status)
                {
                    case BoundedSearchExecutionStatus.Completed:
                        var providerResult = execution.Result ?? new ScraperSearchResult();
                        foreach (var searchInfo in providerResult.Candidates)
                        {
                            if (include(searchInfo))
                            {
                                AddDiscoveredSource(outcome.Sources, scraper.ProviderId, searchInfo);
                            }
                        }

                        foreach (var diagnostic in providerResult.Diagnostics)
                        {
                            outcome.AddDiagnostic(
                                diagnostic.Status,
                                keyword,
                                stopwatch.ElapsedMilliseconds,
                                diagnostic.Message,
                                diagnostic.TimedOut,
                                diagnostic.Cancelled);
                        }

                        outcome.AddDiagnostic("completed", keyword, stopwatch.ElapsedMilliseconds, string.Empty, false, false);
                        break;

                    case BoundedSearchExecutionStatus.ProviderTimedOut:
                        outcome.AddDiagnostic("timed_out", keyword, stopwatch.ElapsedMilliseconds,
                            "Provider call exceeded the bounded deadline.", true, false);
                        break;

                    case BoundedSearchExecutionStatus.Cancelled:
                        outcome.AddDiagnostic(cancellationToken.IsCancellationRequested ? "unstarted" : "cancelled",
                            keyword,
                            stopwatch.ElapsedMilliseconds,
                            cancellationToken.IsCancellationRequested
                                ? "Search operation deadline or cancellation occurred before this call completed."
                                : "Provider call was cancelled.",
                            false,
                            true);
                        break;

                    default:
                        var error = execution.Error?.Message ?? "Provider search failed.";
                        outcome.AddDiagnostic("failed", keyword, stopwatch.ElapsedMilliseconds, error, false, false);
                        logger?.LogError(execution.Error,
                            "[{0}] bounded {1} search failed: keyword={2}",
                            scraper.Name,
                            searchKind,
                            keyword);
                        break;
                }
            }

            return outcome;
        }

        private static DanmuMatchSearchResult ToResult(
            IEnumerable<ProviderSearchOutcome> outcomes,
            CancellationToken cancellationToken)
        {
            var result = new DanmuMatchSearchResult
            {
                WasCancelled = cancellationToken.IsCancellationRequested,
            };
            foreach (var outcome in outcomes ?? Enumerable.Empty<ProviderSearchOutcome>())
            {
                result.CompletionDiagnostics.AddRange(outcome.Diagnostics);
                if (outcome.Diagnostics.Any(diagnostic =>
                    !string.Equals(diagnostic.Status, "completed", StringComparison.OrdinalIgnoreCase)))
                {
                    result.IsComplete = false;
                }

                foreach (var diagnostic in outcome.Diagnostics.Where(diagnostic =>
                    !string.Equals(diagnostic.Status, "completed", StringComparison.OrdinalIgnoreCase)))
                {
                    var providerName = outcome.Scraper?.ProviderName ?? "Unknown provider";
                    result.SearchErrors.Add(providerName + ": " + diagnostic.Status);
                }
            }

            // An empty provider list is a completed no-candidate search. It is
            // intentionally not an execution failure, preserving legacy UI.
            return result;
        }

        private static Dictionary<string, DiscoveredSearchInfo> MergeSources(
            IEnumerable<ProviderSearchOutcome> outcomes)
        {
            var sources = new Dictionary<string, DiscoveredSearchInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var outcome in outcomes ?? Enumerable.Empty<ProviderSearchOutcome>())
            {
                foreach (var source in outcome.Sources.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sources[source.Key] = source.Value;
                }
            }

            return sources;
        }

        private static List<DanmuMatchCandidate> ScoreMovieCandidates(
            Dictionary<string, DiscoveredSearchInfo> sources,
            IList<AbstractScraper> scrapers,
            string movieName,
            int? expectedYear,
            IEnumerable<string> localTitleAliases)
        {
            var candidates = new List<DanmuMatchCandidate>();
            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                if (scraper == null)
                {
                    continue;
                }

                var prefix = scraper.ProviderId + "\u001f";
                candidates.AddRange(sources
                    .Where(source => source.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(source => DanmuMatchScorer.ScoreMovie(
                        source.Value.Info,
                        scraper.ProviderId,
                        scraper.ProviderName,
                        sourceOrder,
                        movieName,
                        expectedYear,
                        localTitleAliases)));
            }

            return OrderCanonicalCandidates(candidates.Where(candidate => candidate.Score > 0));
        }

        private static List<DanmuMatchCandidate> ScoreCandidates(
            Dictionary<string, DiscoveredSearchInfo> sources,
            IList<AbstractScraper> scrapers,
            string seriesName,
            string seasonName,
            int? expectedYear,
            int expectedEpisodes,
            IEnumerable<string> localSeriesTitleAliases,
            IEnumerable<string> localSeasonTitleAliases,
            bool applyContradictionCap,
            int? expectedSeasonNumber)
        {
            var candidates = new List<DanmuMatchCandidate>();
            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                if (scraper == null)
                {
                    continue;
                }

                var prefix = scraper.ProviderId + "\u001f";
                candidates.AddRange(sources
                    .Where(source => source.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(source => DanmuMatchScorer.Score(
                        source.Value.Info,
                        scraper.ProviderId,
                        scraper.ProviderName,
                        sourceOrder,
                        seriesName,
                        seasonName,
                        expectedYear,
                        expectedEpisodes,
                        localSeriesTitleAliases,
                        localSeasonTitleAliases,
                        applyContradictionCap,
                        expectedSeasonNumber)));
            }

            return OrderCanonicalCandidates(candidates);
        }

        public static List<DanmuMatchCandidate> OrderCandidates(IEnumerable<DanmuMatchCandidate> candidates)
        {
            const int visibleLimit = 60;
            var groups = OrderCanonicalCandidates(candidates)
                .GroupBy(candidate => candidate.SourceOrder)
                .OrderBy(group => group.Key)
                .Select(group => group.ToList())
                .ToList();
            var allocated = new int[groups.Count];
            var count = 0;
            for (var groupIndex = 0; groupIndex < groups.Count && count < visibleLimit; groupIndex++)
            {
                allocated[groupIndex] = 1;
                count++;
            }

            while (count < visibleLimit)
            {
                var added = false;
                for (var groupIndex = 0; groupIndex < groups.Count && count < visibleLimit; groupIndex++)
                {
                    if (allocated[groupIndex] >= groups[groupIndex].Count)
                    {
                        continue;
                    }

                    allocated[groupIndex]++;
                    count++;
                    added = true;
                }

                if (!added)
                {
                    break;
                }
            }

            var projected = new List<DanmuMatchCandidate>(count);
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                projected.AddRange(groups[groupIndex].Take(allocated[groupIndex]));
            }

            return projected;
        }

        public static List<DanmuMatchCandidate> OrderCanonicalCandidates(
            IEnumerable<DanmuMatchCandidate> candidates)
        {
            return (candidates ?? Enumerable.Empty<DanmuMatchCandidate>())
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.SourceOrder)
                .ThenByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.TitleScore)
                .ThenByDescending(candidate => candidate.ParentTitleScore)
                .ThenByDescending(candidate => candidate.KeywordScore)
                .ThenByDescending(candidate => candidate.EpisodeScore)
                .ThenByDescending(candidate => candidate.YearScore)
                .ThenBy(candidate => candidate.SiteName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ClassifyResult(DanmuMatchSearchResult result)
        {
            if (result.WasCancelled)
            {
                result.Decision = "cancelled";
                result.SelectedCandidate = null;
                return;
            }

            var selected = DanmuMatchScorer.SelectAutoCandidate(result.CanonicalCandidates);
            if (result.IsComplete)
            {
                result.Decision = selected != null
                    ? "confident"
                    : result.CanonicalCandidates.Count > 0 ? "manual" : "no_match";
                result.SelectedCandidate = selected;
                return;
            }

            if (result.CanonicalCandidates.Count == 0)
            {
                result.Decision = "retryable-incomplete";
                result.SelectedCandidate = null;
                return;
            }

            result.Decision = selected != null ? "partial-confident" : "partial-manual";
            result.SelectedCandidate = selected;
        }

        private static string BuildKey(string providerId, string id)
        {
            return (providerId ?? string.Empty) + "\u001f" + (id ?? string.Empty);
        }

        private static void AddDiscoveredSource(
            IDictionary<string, DiscoveredSearchInfo> sources,
            string providerId,
            ScraperSearchInfo searchInfo)
        {
            var key = BuildKey(providerId, searchInfo.Id);
            sources[key] = new DiscoveredSearchInfo
            {
                Info = searchInfo,
            };
        }

        private sealed class ProviderSearchOutcome
        {
            public ProviderSearchOutcome(int sourceOrder, AbstractScraper scraper)
            {
                SourceOrder = sourceOrder;
                Scraper = scraper;
            }

            public int SourceOrder { get; }
            public AbstractScraper Scraper { get; }
            public Dictionary<string, DiscoveredSearchInfo> Sources { get; } =
                new Dictionary<string, DiscoveredSearchInfo>(StringComparer.OrdinalIgnoreCase);
            public List<DanmuSearchCompletionDiagnostic> Diagnostics { get; } =
                new List<DanmuSearchCompletionDiagnostic>();

            public void AddDiagnostic(
                string status,
                string keyword,
                long elapsedMilliseconds,
                string message,
                bool timedOut,
                bool cancelled)
            {
                Diagnostics.Add(new DanmuSearchCompletionDiagnostic
                {
                    Provider = Scraper?.ProviderId ?? string.Empty,
                    Status = status,
                    Message = string.IsNullOrWhiteSpace(keyword) ? message : keyword + ": " + message,
                    ElapsedMilliseconds = elapsedMilliseconds,
                    TimedOut = timedOut,
                    Cancelled = cancelled,
                });
            }
        }

        private sealed class DiscoveredSearchInfo
        {
            public ScraperSearchInfo Info { get; set; }
        }
    }

    public sealed class DanmuMatchSearchResult
    {
        public List<DanmuMatchCandidate> CanonicalCandidates { get; set; } = new List<DanmuMatchCandidate>();
        public List<DanmuMatchCandidate> Candidates { get; set; } = new List<DanmuMatchCandidate>();
        public DanmuMatchCandidate SelectedCandidate { get; set; }
        public string Decision { get; set; } = string.Empty;
        public List<string> SearchErrors { get; set; } = new List<string>();
        public List<DanmuSearchCompletionDiagnostic> CompletionDiagnostics { get; set; } =
            new List<DanmuSearchCompletionDiagnostic>();
        public bool IsComplete { get; set; } = true;
        public bool WasCancelled { get; set; }
        public bool UsedTmdbAlias { get; set; }
    }
}
