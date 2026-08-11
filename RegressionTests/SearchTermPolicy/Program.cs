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
            Assert(scraper.ApiKeywords.Count == 4,
                "two concurrent default Season plans must issue exactly two prebuilt terms each, with no second hop");
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
            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item)
            {
                var name = item?.Name ?? string.Empty;
                SearchNames.Add(name);
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
