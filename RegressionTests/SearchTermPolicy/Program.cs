using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.SearchTermPolicyRegression
{
    internal static class Program
    {
        private static int Main()
        {
            DefaultSeasonUsesAtMostTwoPrebuiltTermsPerProvider();
            ExplicitSeasonKeywordUsesExactlyOneTerm();
            MovieAlwaysUsesExactlyOneTerm();
            EpisodeAndTemporaryRangeUseOnlyThePrebuiltSeasonTerms();
            InteractiveAndAutomaticOperationsKeepOneCallPerProvider();
            ManualKeywordPreservesExistingScoringAndDisplayWindow();
            ManualKeywordRejectsWhitespaceAndNeverSelects();
            ManualKeywordIsolatesFailuresAndCancellation();
            VerifiesControllerIntentRoutingContract();
            Console.WriteLine("Search-term policy regression checks passed.");
            return 0;
        }

        private static void DefaultSeasonUsesAtMostTwoPrebuiltTermsPerProvider()
        {
            const string series = "Alpha Main: Punctuation Clause";
            const string season = "Season 2";
            var first = new RecordingScraper("First", new Dictionary<string, List<ScraperSearchInfo>>
            {
                [series] = LowConfidenceResult("provider-returned-alias"),
            });
            var second = new RecordingScraper("Second", new Dictionary<string, List<ScraperSearchInfo>>
            {
                [series] = LowConfidenceResult("provider-returned-alias-2"),
            });
            DanmuMatchSearchEngine.SearchSeasonAsync(new AbstractScraper[] { first, second }, series, season,
                2024, 12, null, null).GetAwaiter().GetResult();

            var expected = DanmuMatchScorer.BuildSearchKeywords(series, season, null).Take(2).ToArray();
            Assert(first.ApiKeywords.SequenceEqual(expected) && second.ApiKeywords.SequenceEqual(expected),
                "a low-confidence Season search must issue only the two prebuilt standard terms to every provider");
            Assert(!first.ApiKeywords.Contains("Punctuation Clause") && !first.ApiKeywords.Contains("provider-returned-alias"),
                "punctuation clauses and provider-returned aliases must never create a second search hop");
        }

        private static void ExplicitSeasonKeywordUsesExactlyOneTerm()
        {
            var scraper = new RecordingScraper("ExplicitSeason", new Dictionary<string, List<ScraperSearchInfo>>
            {
                ["only-this"] = LowConfidenceResult("provider-returned-alias"),
            });
            DanmuMatchSearchEngine.SearchSeasonAsync(new[] { scraper }, "Alpha: Clause", "Season 2", 2024, 12,
                "only-this", null).GetAwaiter().GetResult();
            Assert(scraper.ApiKeywords.SequenceEqual(new[] { "only-this" }),
                "an explicit Season keyword must be sent exactly once and must not expand after a low-confidence response");
        }

        private static void MovieAlwaysUsesExactlyOneTerm()
        {
            var movie = new Movie { Name = "Movie Main: Punctuation Clause", ProductionYear = 2024 };
            var standard = new RecordingScraper("Movie", null, new Dictionary<string, List<ScraperSearchInfo>>
            {
                [movie.Name] = LowConfidenceMovieResult("movie-provider-alias"),
            });
            DanmuMatchSearchEngine.SearchMovieAsync(new[] { standard }, movie, null, null)
                .GetAwaiter().GetResult();
            Assert(standard.SearchNames.SequenceEqual(new[] { movie.Name }),
                "a low-confidence Movie search must make one standard metadata request without punctuation/alias expansion");

            var explicitScraper = new RecordingScraper("MovieExplicit", null,
                new Dictionary<string, List<ScraperSearchInfo>>
                {
                    ["only-movie"] = LowConfidenceMovieResult("movie-provider-alias"),
                });
            DanmuMatchSearchEngine.SearchMovieAsync(new[] { explicitScraper }, movie, "only-movie", null)
                .GetAwaiter().GetResult();
            Assert(explicitScraper.SearchNames.SequenceEqual(new[] { "only-movie" }),
                "an explicit Movie keyword must be the one and only provider request");
        }

        private static void EpisodeAndTemporaryRangeUseOnlyThePrebuiltSeasonTerms()
        {
            var episode = new RecordingScraper("EpisodeForced", new Dictionary<string, List<ScraperSearchInfo>>());
            DanmuMatchSearchEngine.SearchSeasonAsync(new[] { episode }, "Episode Series: Alias", "Season 3",
                2024, 12, null, null).GetAwaiter().GetResult();
            var episodeTerms = DanmuMatchScorer.BuildSearchKeywords("Episode Series: Alias", "Season 3", null)
                .Take(2).ToArray();
            Assert(episode.ApiKeywords.SequenceEqual(episodeTerms),
                "a forced Episode search must use the same bounded Season discovery plan and no detail lookup");

            var range = new RecordingScraper("TemporaryRange", new Dictionary<string, List<ScraperSearchInfo>>());
            DanmuMatchSearchEngine.SearchSeasonAsync(new[] { range }, "Range Series: Alias", "Season 2",
                2024, 5, null, null).GetAwaiter().GetResult();
            var rangeTerms = DanmuMatchScorer.BuildSearchKeywords("Range Series: Alias", "Season 2", null)
                .Take(2).ToArray();
            Assert(range.ApiKeywords.Count <= 2 &&
                   range.ApiKeywords.All(term => rangeTerms.Contains(term, StringComparer.OrdinalIgnoreCase)) &&
                   !range.ApiKeywords.Any(term => term.IndexOf("Unrelated", StringComparison.OrdinalIgnoreCase) >= 0),
                "temporary-range discovery must use only its prebuilt title terms, never a title clause fallback");
        }

        private static void InteractiveAndAutomaticOperationsKeepOneCallPerProvider()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromSeconds(2),
                interactiveOperationTimeout: TimeSpan.FromSeconds(3),
                automaticOperationTimeout: TimeSpan.FromSeconds(3));
            var policy = new BoundedSearchPolicy(options);
            var scraper = new ConcurrentRecordingScraper("shared-provider");
            using (var registry = new SearchOperationRegistry(options))
            {
                Assert(registry.TryBegin("interactive-7-4", SearchOperationScope.Interactive, out var interactive, out _),
                    "interactive operation must register");
                Assert(registry.TryBegin("automatic-7-4", SearchOperationScope.Automatic, out var automatic, out _),
                    "automatic operation must register independently");
                using (interactive)
                using (automatic)
                {
                    var interactiveSearch = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new[] { scraper }, "Shared Series", "Season 1", 2024, 12, null, null,
                        policy, interactive.CancellationToken);
                    var automaticSearch = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new[] { scraper }, "Shared Series", "Season 1", 2024, 12, null, null,
                        policy, automatic.CancellationToken);
                    Task.WhenAll(interactiveSearch, automaticSearch).GetAwaiter().GetResult();
                }
            }

            Assert(scraper.MaximumConcurrentCalls == 1,
                "default, interactive, and automatic searches must share the one-call-per-provider gate");
            Assert(scraper.ApiKeywords.Count == 2,
                "two concurrent generic-Season plans must issue only the parent identity once each");
        }

        private static void VerifiesControllerIntentRoutingContract()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var controller = System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var episodeStart = controller.IndexOf("private async Task<DanmuItemMatchResult> GetEpisodeMatchPreview", StringComparison.Ordinal);
            var episodeEnd = controller.IndexOf("private async Task<DanmuSeasonMatchResult> GetSeasonMatchPreview", episodeStart,
                StringComparison.Ordinal);
            var episode = controller.Substring(episodeStart, episodeEnd - episodeStart);
            Assert(episode.Contains("GetSeasonMatchPreview(", StringComparison.Ordinal) &&
                   !episode.Contains("GetMedia(season, candidate.Id)", StringComparison.Ordinal),
                "forced Episode search must delegate to bounded Season discovery and perform no candidate detail fan-out");
            Assert(controller.Contains("IsTemporaryRangeSearch(request)", StringComparison.Ordinal) &&
                   controller.Contains("DanmuMatchSearchEngine.SearchSeasonAsync", StringComparison.Ordinal),
                "temporary-range intent must enter the same bounded Season search engine");
        }

        private static void ManualKeywordPreservesExistingScoringAndDisplayWindow()
        {
            const string keyword = "  JOJO + 星尘!?  内 部  ";
            const string seriesName = "JOJO";
            const string seasonName = "Season 2";
            var seasonProviderKeyword = DanmuMatchScorer.BuildSearchKeywords(
                seriesName, seasonName, keyword).Single();
            var firstRows = Enumerable.Range(0, 65).Select(index => new ScraperSearchInfo
            {
                Id = index < 2 ? "duplicate-id" : "first-" + index,
                Name = index == 1 ? "JOJO Season 2" : "First " + index,
                Category = "anime",
                Year = index == 1 ? 2024 : 1980 + index,
                EpisodeSize = index == 1 ? 12 : index,
                Aliases = new List<string> { "alias-" + index },
                SourceMetadata = new Emby.Plugin.Danmu.Model.SourceMetadata
                {
                    Title = "source-" + index,
                    Year = 2000 + index,
                    Category = "source-category-" + index,
                },
            }).ToList();
            firstRows.Insert(3, null);
            firstRows.Insert(4, new ScraperSearchInfo
            {
                Id = "filtered-movie",
                Name = "Provider-identifiable movie",
                Category = "movie",
            });
            firstRows.Insert(5, new ScraperSearchInfo { Id = "filtered-nameless" });
            var first = new RecordingScraper("FirstKeyword", new Dictionary<string, List<ScraperSearchInfo>>
            {
                [seasonProviderKeyword] = firstRows,
            });
            var second = new RecordingScraper("SecondKeyword", new Dictionary<string, List<ScraperSearchInfo>>
            {
                [seasonProviderKeyword] = new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "duplicate-id", Name = "JOJO Season 2", Category = "documentary",
                        Year = 2024, EpisodeSize = 12,
                    },
                    new ScraperSearchInfo { Id = "second-1", Name = "Last" },
                },
            });

            var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { first, second }, seriesName, seasonName, 2024, 12,
                    keyword, null, BoundedSearchPolicy.Shared,
                    CancellationToken.None, CancellationToken.None,
                    null, null, new Season { IndexNumber = 2 },
                    manualKeywordDiscovery: true)
                .GetAwaiter().GetResult();
            Assert(seasonProviderKeyword == "JOJO + 星尘!?  内 部" &&
                   first.ApiKeywords.SequenceEqual(new[] { seasonProviderKeyword }) &&
                   second.ApiKeywords.SequenceEqual(new[] { seasonProviderKeyword }),
                "manual keyword Season search must retain existing explicit-keyword cleanup while preserving punctuation, literal plus, internal whitespace, and non-ASCII text");
            var firstCandidates = result.Candidates.TakeWhile(row => row.Site == "FirstKeyword").ToList();
            Assert(result.Candidates.Count == 60 && firstCandidates.Count == 58 &&
                   result.Candidates.Skip(firstCandidates.Count).All(row => row.Site == "SecondKeyword") &&
                   firstCandidates.Zip(firstCandidates.Skip(1),
                       (left, right) => left.Score >= right.Score).All(value => value),
                "manual keyword search must reuse the ordinary provider-fair 60-row window, site priority, and descending per-site score order");
            Assert(result.Candidates.Count(row =>
                       row.Site == "FirstKeyword" && row.Id == "duplicate-id") == 1 &&
                   result.Candidates.Count(row => row.Id == "duplicate-id") == 2 &&
                   firstCandidates[0].Id == "duplicate-id" &&
                   firstCandidates[0].Name == "JOJO Season 2" &&
                   firstCandidates[0].SourceMetadata?.Title == "source-1",
                "manual keyword search must reuse ordinary same-site ID merge while retaining cross-site identity and server metadata");
            Assert(!result.Candidates.Any(row =>
                       row.Id == "filtered-movie" || row.Id == "filtered-nameless"),
                "manual keyword Season search must keep the ordinary structural and identifiable-movie eligibility gate");
            Assert(result.Candidates.All(row => row.MatchScore == row.Score &&
                       row.ScoreOrigin == "search-confidence") &&
                   result.SelectedCandidate == null && string.IsNullOrEmpty(result.Decision) &&
                   !result.UsedTmdbAlias,
                "manual keyword rows must carry ordinary server scoring without TMDB aliases or automatic selection");

            var movieContext = new Movie { Name = "Local Movie", ProductionYear = 2024 };
            var movieProviderKeyword = keyword.Trim();
            var movieScraper = new RecordingScraper("MovieKeyword", movieResults:
                new Dictionary<string, List<ScraperSearchInfo>>
                {
                    [movieProviderKeyword] = new List<ScraperSearchInfo>
                    {
                        new ScraperSearchInfo
                        {
                            Id = "typed-movie-row", Name = movieProviderKeyword,
                            Category = "movie", Year = 2024,
                        },
                        new ScraperSearchInfo
                        {
                            Id = "zero-score-row", Name = "ZZZZZZZZZZ",
                            Category = "movie", Year = 1900,
                        },
                        new ScraperSearchInfo
                        {
                            Id = "filtered-series-row",
                            Name = "Typed series",
                            Category = "anime",
                        },
                        new ScraperSearchInfo { Id = "filtered-movie-nameless", Category = "movie" },
                    },
                });
            var movieResult = DanmuMatchSearchEngine.SearchMovieAsync(
                    new[] { movieScraper }, movieContext, keyword, null,
                    BoundedSearchPolicy.Shared, CancellationToken.None, movieContext,
                    retainZeroScoreCandidates: true, manualKeywordDiscovery: true)
                .GetAwaiter().GetResult();
            Assert(movieScraper.SearchNames.SequenceEqual(new[] { movieProviderKeyword }) &&
                   movieScraper.SearchYears.SequenceEqual(new int?[] { movieContext.ProductionYear }) &&
                   movieScraper.ApiKeywords.Count == 0 &&
                   movieResult.Candidates.Count == 2 &&
                   movieResult.Candidates[0].Id == "typed-movie-row" &&
                   movieResult.Candidates.Any(row => row.Id == "zero-score-row" && row.Score == 0) &&
                   movieResult.Candidates.All(row => row.ScoreOrigin == "search-confidence") &&
                   !movieResult.Candidates.Any(row =>
                       row.Id.StartsWith("filtered-", StringComparison.Ordinal)) &&
                   movieResult.SelectedCandidate == null && string.IsNullOrEmpty(movieResult.Decision) &&
                   !movieResult.UsedTmdbAlias,
                "manual keyword Movie search must retain typed query/year/eligibility, ordinary score ordering, and zero-score rows without threshold filtering or automatic selection");

            var engineSource = System.IO.File.ReadAllText(System.IO.Path.Combine(
                FindRepositoryRoot(AppContext.BaseDirectory), "Scraper", "DanmuMatchSearchEngine.cs"));
            Assert(engineSource.Contains("bool manualKeywordDiscovery = false") &&
                   engineSource.Contains("MergeSources(outcomes)") &&
                   engineSource.Contains("ScoreCandidates(") &&
                   engineSource.Contains("ScoreMovieCandidates(") &&
                   engineSource.Contains("result.Candidates = OrderCandidates(") &&
                   engineSource.Contains("if (!manualKeywordDiscovery && string.IsNullOrWhiteSpace(keywordOverride))") &&
                   engineSource.Contains("if (!manualKeywordDiscovery)") &&
                   !engineSource.Contains("SearchSeasonManualKeywordAsync") &&
                   !engineSource.Contains("SearchMovieManualKeywordAsync") &&
                   !engineSource.Contains("ManualKeywordProviderOutcome") &&
                   !engineSource.Contains("DanmuManualKeywordSearchResult") &&
                   !engineSource.Contains("SearchForManualKeywordWithDiagnostics"),
                "manual keyword discovery must be an explicit policy on the ordinary merge/scoring/window engine, not a parallel engine or keyword heuristic");
        }

        private static void ManualKeywordRejectsWhitespaceAndNeverSelects()
        {
            var scraper = new RecordingScraper("WhitespaceKeyword");
            foreach (var keyword in new[] { string.Empty, "   ", "\t\r\n" })
            {
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new[] { scraper }, "Series", "Season 1", 2024, 12, keyword, null,
                        BoundedSearchPolicy.Shared, CancellationToken.None, CancellationToken.None,
                        manualKeywordDiscovery: true)
                    .GetAwaiter().GetResult();
                Assert(!result.IsComplete && result.Candidates.Count == 0 &&
                       result.CompletionDiagnostics.Any(diagnostic =>
                           diagnostic.Status == "invalid_request"),
                    "empty and whitespace-only manual keyword input must be invalid");
            }
            Assert(scraper.ApiKeywords.Count == 0,
                "invalid manual keyword input must issue zero provider calls");

            var exact = new RecordingScraper("ExactLooking", new Dictionary<string, List<ScraperSearchInfo>>
            {
                ["Exact"] = new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "exact", Name = "Exact", Year = 2024, EpisodeSize = 12,
                    },
                },
            });
            var exactResult = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new[] { exact }, "Exact", "Season 1", 2024, 12, "Exact", null,
                    BoundedSearchPolicy.Shared, CancellationToken.None, CancellationToken.None,
                    null, null, new Season { IndexNumber = 1 },
                    manualKeywordDiscovery: true)
                .GetAwaiter().GetResult();
            Assert(exactResult.Candidates.Single().Score > 0 &&
                   exactResult.Candidates.Single().ScoreOrigin == "search-confidence" &&
                   exactResult.SelectedCandidate == null &&
                   string.IsNullOrEmpty(exactResult.Decision),
                "an exact-looking manual keyword row may be scored but must never be auto-selected");
        }

        private static void ManualKeywordIsolatesFailuresAndCancellation()
        {
            var working = new DiagnosticScraper("WorkingKeyword", false, false,
                new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo { Id = "dup", Name = "first" },
                    new ScraperSearchInfo { Id = "dup", Name = "second" },
                });
            var failing = new DiagnosticScraper("FailingKeyword", true, false, null);
            var partial = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { failing, working }, "Series", "Season 1", null, 0,
                    "raw", null, BoundedSearchPolicy.Shared,
                    CancellationToken.None, CancellationToken.None,
                    manualKeywordDiscovery: true)
                .GetAwaiter().GetResult();
            Assert(partial.Candidates.Select(row => row.Name).SequenceEqual(new[] { "second" }) &&
                   partial.SearchErrors.Any(error => error.Contains("FailingKeyword")) &&
                   partial.HasCompletedProviders && !partial.IsComplete &&
                   partial.SelectedCandidate == null,
                "one provider failure must preserve the successful site's ordinary same-ID merge and diagnostics without selection");

            var allFailed = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { failing }, "Series", "Season 1", null, 0,
                    "raw", null, BoundedSearchPolicy.Shared,
                    CancellationToken.None, CancellationToken.None,
                    manualKeywordDiscovery: true)
                .GetAwaiter().GetResult();
            Assert(allFailed.Candidates.Count == 0 &&
                   !allFailed.HasCompletedProviders && !allFailed.IsComplete,
                "all-provider failure must remain a retryable no-row outcome");

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var cancelled = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new AbstractScraper[]
                        {
                            new DiagnosticScraper("CancelledKeyword", false, true, null),
                        },
                        "Series", "Season 1", null, 0, "raw", null,
                        BoundedSearchPolicy.Shared, cancellation.Token, cancellation.Token,
                        manualKeywordDiscovery: true)
                    .GetAwaiter().GetResult();
                Assert(cancelled.WasCancelled && cancelled.Candidates.Count == 0 &&
                       cancelled.CanonicalCandidates.Count == 0 &&
                       cancelled.SelectedCandidate == null,
                    "explicit parent cancellation must clear all provisional manual keyword rows and selection");
            }
        }

        private static List<ScraperSearchInfo> LowConfidenceResult(string id) => new List<ScraperSearchInfo>
        {
            new ScraperSearchInfo { Id = id, Name = "Unrelated: returned; alias!", Year = 1980, EpisodeSize = 1 },
        };

        private static List<ScraperSearchInfo> LowConfidenceMovieResult(string id) => new List<ScraperSearchInfo>
        {
            new ScraperSearchInfo { Id = id, Name = "Unrelated: returned; alias!", Category = "movie", Year = 1980 },
        };

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var current = new System.IO.DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(current.FullName, "Emby.Plugin.Danmu.csproj")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new System.IO.DirectoryNotFoundException("Unable to locate plugin repository root.");
        }

        private sealed class RecordingScraper : AbstractScraper
        {
            private readonly string _id;
            private readonly Dictionary<string, List<ScraperSearchInfo>> _apiResults;
            private readonly Dictionary<string, List<ScraperSearchInfo>> _movieResults;

            public RecordingScraper(string id, Dictionary<string, List<ScraperSearchInfo>> apiResults = null,
                Dictionary<string, List<ScraperSearchInfo>> movieResults = null) : base(null)
            {
                _id = id;
                _apiResults = apiResults ?? new Dictionary<string, List<ScraperSearchInfo>>();
                _movieResults = movieResults ?? new Dictionary<string, List<ScraperSearchInfo>>();
            }

            public List<string> ApiKeywords { get; } = new List<string>();
            public List<string> SearchNames { get; } = new List<string>();
            public List<int?> SearchYears { get; } = new List<int?>();
            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item)
            {
                var name = item?.Name ?? string.Empty;
                SearchNames.Add(name);
                SearchYears.Add(item?.ProductionYear);
                _movieResults.TryGetValue(name, out var result);
                return Task.FromResult(result ?? new List<ScraperSearchInfo>());
            }
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                ApiKeywords.Add(keyword);
                _apiResults.TryGetValue(keyword, out var result);
                return Task.FromResult(result ?? new List<ScraperSearchInfo>());
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private sealed class DiagnosticScraper : AbstractScraper
        {
            private readonly string _id;
            private readonly bool _fail;
            private readonly bool _cancel;
            private readonly List<ScraperSearchInfo> _rows;

            public DiagnosticScraper(string id, bool fail, bool cancel, List<ScraperSearchInfo> rows) : base(null)
            {
                _id = id;
                _fail = fail;
                _cancel = cancel;
                _rows = rows;
            }

            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(_rows ?? new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) =>
                Task.FromResult(_rows ?? new List<ScraperSearchInfo>());
            public override Task<ScraperSearchResult> SearchForApiWithDiagnostics(
                string keyword, CancellationToken cancellationToken)
            {
                if (_cancel)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException(cancellationToken);
                }
                if (_fail)
                {
                    throw new InvalidOperationException("provider-local-failure");
                }
                return Task.FromResult(new ScraperSearchResult
                {
                    Candidates = _rows ?? new List<ScraperSearchInfo>(),
                });
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private sealed class ConcurrentRecordingScraper : AbstractScraper
        {
            private readonly string _id;
            private int _activeCalls;
            private int _maximumConcurrentCalls;

            public ConcurrentRecordingScraper(string id) : base(null)
            {
                _id = id;
            }

            public List<string> ApiKeywords { get; } = new List<string>();
            public int MaximumConcurrentCalls => _maximumConcurrentCalls;
            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                ApiKeywords.Add(keyword);
                var active = Interlocked.Increment(ref _activeCalls);
                var observed = Volatile.Read(ref _maximumConcurrentCalls);
                while (active > observed)
                {
                    var prior = Interlocked.CompareExchange(ref _maximumConcurrentCalls, active, observed);
                    if (prior == observed)
                    {
                        break;
                    }

                    observed = prior;
                }

                try
                {
                    await Task.Delay(25).ConfigureAwait(false);
                    return new List<ScraperSearchInfo>();
                }
                finally
                {
                    Interlocked.Decrement(ref _activeCalls);
                }
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }
    }
}
