using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Controllers;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;
using Emby.Plugin.Danmu.Scraper.Dandan;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Iqiyi;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using System.Threading;
using System.Threading.Tasks;
using BilibiliMedia = Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args != null && args.Contains("--manual-keyword-core", StringComparer.Ordinal))
            {
                PreservesManualKeywordEvidenceAndRouting();
                Console.WriteLine("Manual-keyword core regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--movie-part-core", StringComparer.Ordinal))
            {
                MergesServerOwnedCandidateMetadataFieldByField();
                FiltersAndSecuresMoviePartEvidence();
                Console.WriteLine("Movie part core regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--bilibili-search", StringComparer.Ordinal))
            {
                BilibiliSearchTests.Run();
                Console.WriteLine("Bilibili search regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--tmdb-alias", StringComparer.Ordinal))
            {
                TmdbAliasTests.Run();
                Console.WriteLine("TMDB alias regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--seven-day-replay", StringComparer.Ordinal))
            {
                SevenDayReplayTests.Run();
                Console.WriteLine("Seven-day replay regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--composite-season-state", StringComparer.Ordinal))
            {
                CompositeSeasonStateTests.Run();
                Console.WriteLine("Composite season state regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--composite-season-planner", StringComparer.Ordinal))
            {
                CompositeSeasonPlannerTests.Run();
                Console.WriteLine("Composite season planner regression checks passed.");
                return 0;
            }

            if (args != null && args.Contains("--composite-season", StringComparer.Ordinal))
            {
                CompositeSeasonStateTests.Run();
                CompositeSeasonPlannerTests.Run();
                Console.WriteLine("Composite season regression checks passed.");
                return 0;
            }

            MapsAnimeSeason();
            MapsLiveActionSeasonAndCleansTitle();
            UsesIdentifierFallbackOrder();
            OmitsMalformedRecords();
            BilibiliSearchTests.Run();
            TmdbAliasTests.Run();
            SevenDayReplayTests.Run();
            IqiyiSourceMetadataTests.Run();
            OrdersAndSelectsCrossProviderTies();
            PreservesSameSiteHighestScoreTieAmbiguity();
            SelectsCloseHighConfidenceCandidatesBySitePriority();
            ScoresMoviesAndFiltersTelevisionCandidates();
            ScoresStandardCandidatesWithoutDynamicAliasMode();
            UsesOneMovieSearchTermAndPreservesStandardProvenance();
            IsolatesMovieProviderFailures();
            VerifiesLazyCandidateDetailContract();
            PreservesManualKeywordEvidenceAndRouting();
            MergesServerOwnedCandidateMetadataFieldByField();
            FiltersAndSecuresMoviePartEvidence();
            ResolvesProviderIdsBySiteThenHierarchy();
            EnforcesItemLocalProviderScopes();
            DiscoversCandidatesWithBoundedTitleClauses();
            NormalizesDandanStandaloneSeasonOrdinals();
            EnforcesBilibiliPgcIdentifierPolicy();
            ExposesBilibiliAndMgtvExternalIds();
            PreservesSeasonSuccessPersistenceContract();
            PreservesSeriesPreviewProviderIdContract();
            ProjectsProviderIdMetadataWithoutSearchOrScoring();
            PreservesProviderDetailAdapterMetadataContracts();
            RequiresVerifiedDirectEpisodeDetailsAndUsesResolvedUgcCounts();
            ResolvesMovieProviderLookupIdentifiers();
            PreservesSavedManualBindingsUntilForcedSearch();
            AppliesDuplicateAndForceRefreshPolicy();
            PreservesSingleEpisodeIsolationAndLegacyTaskShape();
            PreservesProviderWriteGenerationOrdering();
            R4GenerationPolicyTests.Run();
            CompositeSeasonStateTests.Run();
            ReplacesOnlyRegisteredOrdinaryProviderIds();
            MapsEpisodeSourceNumbersSafely();
            CompositeSeasonPlannerTests.Run();
            DeserializesAndNormalizesBilibiliEpisodes();
            DeserializesBilibiliExactVideoDetails();
            DeserializesBilibiliExactSeasonDetails();
            ClassifiesOnlyExplicitNonMainTitles();
            ResolvesDandanCredentialsByCompletePair();
            RejectsIncompleteDandanCredentialsWithoutLeakingValues();
            PreservesLegacyDandanApiDefaults();
            MigratesOfficialProxyCorsConfigurationWithoutPersistingItsAddress();
            NormalizesAndValidatesDandanProxyPrefixes();
            RoutesExistingDandanEndpointsWithoutLocalProxyAuthentication();
            PreservesDandanTitleBasedMatchingEndpoints();
            EmbedsDandanCredentialSettings();
            VerifiesVersionedConfigurationPageResources();
            VerifiesSingleTargetSmartMatchReliabilityContracts();
            PreservesStrmIndependentSeasonResolutionContract();
            PreservesValidUnicodeWhileRemovingInvalidXmlScalars();
            RemovesInvalidXmlCharacterReferences();
            PreservesCharacterReferenceTextInsideCdata();
            RecoversIqiyiXmlContainingInvalidCharacters();
            ParsesIqiyiQipsMovieTvId();
            RecoversBilibiliXmlContainingInvalidCharacters();
            SanitizesFinalXmlForEveryProviderAndAcceptsSmallValidOutput();
            Console.WriteLine("Danmu plugin regression checks passed.");
            return 0;
        }

        private static void PreservesProviderWriteGenerationOrdering()
        {
            var tracker = new ProviderWriteGenerationTracker();
            const string firstSite = "item\u001fBilibiliID";
            const string secondSite = "item\u001fDandanID";

            tracker.MarkCommitted(firstSite, 2);
            Assert(tracker.IsStale(firstSite, 1, out var committed) && committed == 2,
                "a late older write must not replace a newer successful provider binding");
            Assert(!tracker.IsStale(firstSite, 2, out _),
                "the current generation must remain writable");

            tracker.MarkStarted(firstSite, 4);
            Assert(tracker.IsStale(firstSite, 3, out var latestStarted) && latestStarted == 4,
                "a newer started task must supersede an older task before either one commits");
            Assert(!tracker.IsStale(secondSite, 1, out _),
                "a provider generation must not suppress another provider's binding");

            tracker.MarkCommitted(firstSite, 1);
            Assert(tracker.IsStale(firstSite, 1, out committed) && committed == 4,
                "an older completion must not lower committed or latest-started protection");

            var successful = new DanmuEpisodeDownloadOutcome
            {
                Status = "success",
                FilePersisted = true,
                ProviderId = "BilibiliID",
                ProviderValue = "new-id",
            };
            Assert(DanmuDownloadPersistencePolicy.ShouldPersist(successful),
                "a completed successful download should persist only its selected provider");
            successful.Status = "partial";
            Assert(DanmuDownloadPersistencePolicy.ShouldPersist(successful),
                "a partial result with a persisted valid XML should update its successful target");
            successful.Status = "failed";
            Assert(!DanmuDownloadPersistencePolicy.ShouldPersist(successful),
                "a failed download must preserve existing metadata");
            successful.Status = "skipped";
            Assert(!DanmuDownloadPersistencePolicy.ShouldPersist(successful),
                "a skipped download must preserve existing metadata");
            successful.Status = "success";
            successful.FilePersisted = false;
            Assert(!DanmuDownloadPersistencePolicy.ShouldPersist(successful),
                "a successful status without an actual file must not persist metadata");
        }

        private static void MapsAnimeSeason()
        {
            var source = new BilibiliMedia
            {
                SeasonId = 46089,
                MediaId = 21087073,
                Title = "<em class=\"keyword\">葬送的芙莉莲</em>",
                SeasonTypeName = "番剧",
                PubDate = "2023-09-29 18:00:00",
                EpisodeSize = 28
            };

            Assert(BilibiliSearchResultMapper.TryMap(source, out var result, out _), "anime should map");
            Assert(result.Id == "46089", "anime should use season_id");
            Assert(result.Name == "葬送的芙莉莲", "highlight markup should be removed");
            Assert(result.Year == 2023 && result.EpisodeSize == 28 && result.Category == "番剧", "anime metadata should be preserved");
        }

        private static void MapsLiveActionSeasonAndCleansTitle()
        {
            var source = new BilibiliMedia
            {
                SeasonId = 34793,
                Title = "<em class=\"keyword\">半泽直树</em>",
                SeasonTypeName = "电视剧",
                PubTime = 1373126400,
                EpisodeSize = 10
            };

            Assert(BilibiliSearchResultMapper.TryMap(source, out var result, out _), "live-action season should map");
            Assert(result.Id == "34793" && result.Name == "半泽直树", "live-action identity should be preserved");
            Assert(result.Year == 2013 && result.EpisodeSize == 10 && result.Category == "电视剧", "live-action metadata should be preserved");
        }

        private static void UsesIdentifierFallbackOrder()
        {
            var source = new BilibiliMedia { SeasonId = 7, PgcSeasonId = 8, MediaId = 9, Title = "A" };
            Assert(BilibiliSearchResultMapper.ResolveId(source) == 7, "season_id should win");
            source.SeasonId = 0;
            Assert(BilibiliSearchResultMapper.ResolveId(source) == 8, "pgc_season_id should be the second choice");
            source.PgcSeasonId = 0;
            Assert(BilibiliSearchResultMapper.ResolveId(source) == 9, "media_id should be the final positive fallback");
        }

        private static void OmitsMalformedRecords()
        {
            Assert(!BilibiliSearchResultMapper.TryMap(new BilibiliMedia { Title = "No ID" }, out _, out _), "record without identifier should be omitted");
            Assert(!BilibiliSearchResultMapper.TryMap(new BilibiliMedia { SeasonId = 1, Title = " " }, out _, out _), "record without title should be omitted");
        }

        private static void OrdersAndSelectsCrossProviderTies()
        {
            var candidates = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("later", "LaterSite", 2, 0.90),
                Candidate("priority", "PrioritySite", 0, 0.90),
                Candidate("middle", "MiddleSite", 1, 0.82)
            });

            Assert(candidates[0].Id == "priority", "r6 should surface the selected configured provider first");
            Assert(DanmuMatchScorer.CanAutoSelect(candidates), "a unique candidate on the highest-priority tied provider should auto-bind");
            Assert(DanmuMatchScorer.CanAutoSelect(candidates, false), "r6 selection must not depend on legacy intermediate-search behavior");

            var unequal = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("higher-score", "LaterSite", 5, 0.91),
                Candidate("higher-priority", "PrioritySite", 0, 0.90)
            });
            Assert(unequal[0].Id == "higher-priority", "an earlier 0.90 site must outrank a later 0.91 site");

            var crowded = Enumerable.Range(0, 61)
                .Select(index => Candidate("later-" + index, "LaterSite", 5, 1.0))
                .Append(Candidate("priority-after-cutoff", "PrioritySite", 0, 0.90))
                .ToList();
            var crowdedOrdered = DanmuMatchSearchEngine.OrderCandidates(crowded);
            Assert(crowdedOrdered.Count == 60 && crowdedOrdered[0].Id == "priority-after-cutoff",
                "the confident site-priority decision must run before the 60-candidate display limit");
        }

        private static void PreservesSameSiteHighestScoreTieAmbiguity()
        {
            var candidates = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("a", "PrioritySite", 0, 0.99),
                Candidate("b", "PrioritySite", 0, 0.99),
                Candidate("c", "LaterSite", 1, 0.99)
            });
            Assert(!DanmuMatchScorer.CanAutoSelect(candidates),
                "multiple same-score winners within the highest-priority site must remain ambiguous");

            var oneSite = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("same-site-high", "PrioritySite", 0, 0.90),
                Candidate("same-site-low", "PrioritySite", 0, 0.89),
                Candidate("non-competing-site", "LaterSite", 1, 0.64)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(oneSite)?.Id == "same-site-high" &&
                   DanmuMatchScorer.SelectAutoCandidate(oneSite, false)?.Id == "same-site-high",
                "one-site competitors should select their unique highest score even during intermediate search");
        }

        private static void SelectsCloseHighConfidenceCandidatesBySitePriority()
        {
            var preferredRunnerUp = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("full", "LaterSite", 2, 1.0),
                Candidate("preferred", "PrioritySite", 0, 0.98)
            });
            Assert(preferredRunnerUp[0].Id == "preferred" &&
                   DanmuMatchScorer.SelectAutoCandidate(preferredRunnerUp)?.Id == "preferred",
                "r6 should select and display the earlier confident site");

            var outsideGap = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("full", "FullSite", 1, 1.0),
                Candidate("outside", "PrioritySite", 0, 0.969)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(outsideGap)?.Id == "outside",
                "r6 ignores cross-site score gaps inside the confident pool");

            var belowPoolFloor = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("higher", "LaterSite", 1, 0.949),
                Candidate("lower", "PrioritySite", 0, 0.94)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(belowPoolFloor)?.Id == "lower",
                "the r6 0.90 boundary includes an earlier 0.94 candidate");

            var floorBoundary = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("floor", "LaterSite", 1, 0.95),
                Candidate("below-floor", "PrioritySite", 0, 0.92)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(floorBoundary)?.Id == "below-floor",
                "the 0.90 boundary includes an earlier 0.92 candidate");

            var samePreferredSite = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("preferred-high", "PrioritySite", 0, 0.99),
                Candidate("preferred-low", "PrioritySite", 0, 0.98),
                Candidate("later", "LaterSite", 1, 0.97)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(samePreferredSite)?.Id == "preferred-high",
                "the highest-scoring close candidate within the earliest site should win when unique");

            var preferredThirdScore = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("full", "LateSite", 3, 1.0),
                Candidate("second", "MiddleSite", 2, 0.99),
                Candidate("preferred", "PrioritySite", 0, 0.98)
            });
            Assert(preferredThirdScore.Select(x => x.Id).SequenceEqual(new[] { "preferred", "second", "full" }) &&
                   DanmuMatchScorer.SelectAutoCandidate(preferredThirdScore)?.Id == "preferred",
                "display stays in provider order while the earliest confident site wins over later scores");

            Assert(DanmuMatchScorer.SelectAutoCandidate(preferredRunnerUp, false)?.Id == "preferred" &&
                   DanmuMatchScorer.SelectAutoCandidate(outsideGap, false)?.Id == "outside",
                "legacy selector flags must not reintroduce r5 behavior");

            var exactTie = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("late-tie", "LaterSite", 2, 0.98),
                Candidate("priority-tie", "PrioritySite", 0, 0.98)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(exactTie)?.Id == "priority-tie" &&
                   DanmuMatchScorer.SelectAutoCandidate(exactTie, false)?.Id == "priority-tie",
                "cross-site ties resolve to the earliest configured site");

            Assert(DanmuMatchScorer.SelectAutoCandidate(new List<DanmuMatchCandidate>
                {
                    Candidate("below-floor", "Site", 0, 0.77)
                }) == null,
                "scores below the r6 0.90 boundary must remain unselected");
            Assert(DanmuMatchScorer.SelectAutoCandidate(new List<DanmuMatchCandidate>
                {
                    Candidate("strong", "SiteA", 0, 0.78),
                    Candidate("weak-runner-up", "SiteB", 1, 0.64)
                }) == null,
                "scores below the r6 0.90 boundary must remain unselected");
        }

        private static void ScoresMoviesAndFiltersTelevisionCandidates()
        {
            var exact = DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo { Id = "movie", Name = "流浪地球", Category = "电影", Year = 2019 },
                "MovieSite", "Movie Site", 1, "流浪地球", 2019);
            var weaker = DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo { Id = "weaker", Name = "流浪地球2", Category = "电影", Year = 2023 },
                "MovieSite", "Movie Site", 1, "流浪地球", 2019);
            var television = DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo { Id = "tv", Name = "流浪地球", Category = "电视剧", Year = 2019 },
                "MovieSite", "Movie Site", 1, "流浪地球", 2019);
            var ordered = DanmuMatchSearchEngine.OrderCandidates(new[] { weaker, exact });

            Assert(ordered[0].Id == "movie" && ordered[0].Score >= ordered[1].Score,
                "movie candidates should be ordered by deterministic descending score");
            Assert(DanmuMatchScorer.CanAutoSelect(ordered), "a distinct exact movie match should auto-select");
            Assert(television.Score == 0 && DanmuMatchScorer.IsIdentifiableNonMovie("番剧"),
                "identifiable television candidates must be rejected for movies");
        }

        private static void ScoresStandardCandidatesWithoutDynamicAliasMode()
        {
            var source = new ScraperSearchInfo
            {
                Id = "standard-season",
                Name = "abcdefzzzz",
                Year = 2024,
                EpisodeSize = 12,
            };
            var standard = DanmuMatchScorer.Score(
                source, "AliasID", "Alias", 0, "abcdefghij", "abcdefghij", 2024, 12);
            Assert(Math.Abs(standard.Score - Math.Round(
                       standard.TitleScore * 0.55 + standard.YearScore * 0.15 + standard.EpisodeScore * 0.30,
                       4,
                       MidpointRounding.AwayFromZero)) < 0.0001,
                "fixed-term discovery must retain the standard no-keyword season weights");

            var unrelated = DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = "unrelated",
                    Name = "zzzzzzzzzz",
                    Year = 2024,
                    EpisodeSize = 12,
                },
                "AliasID", "Alias", 0, "abcdefghij", "abcdefghij", 2024, 12);
            Assert(unrelated.TitleScore < 0.72 && unrelated.Score < 0.90,
                "exact year/count must not rescue an unrelated fixed-term candidate");

            var movie = DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo
                {
                    Id = "standard-movie",
                    Name = "abcdefzzzz",
                    Category = "movie",
                    Year = 2024,
                },
                "AliasID", "Alias", 0, "abcdefghij", 2024);
            Assert(movie.Score == Math.Round(
                       movie.TitleScore * 0.82 + movie.YearScore * 0.18,
                       4,
                       MidpointRounding.AwayFromZero),
                "fixed-term movies must use the standard title/year scoring");
        }

        private static void UsesOneMovieSearchTermAndPreservesStandardProvenance()
        {
            const string movieName = "Alpha Adventure：Hidden Alias";
            var provider = new FakeScraper(
                "MovieAliasID",
                null,
                false,
                null,
                null,
                null,
                new Dictionary<string, List<ScraperSearchInfo>>(StringComparer.OrdinalIgnoreCase)
                {
                    [movieName] = new List<ScraperSearchInfo>(),
                    ["Hidden Alias"] = new List<ScraperSearchInfo>
                    {
                        new ScraperSearchInfo
                        {
                            Id = "movie-alias",
                            Name = movieName,
                            Category = "movie",
                            Year = 2024,
                        },
                    },
                });
            var result = DanmuMatchSearchEngine.SearchMovieAsync(
                    new[] { provider },
                    new Movie { Name = movieName, ProductionYear = 2024 },
                    null,
                    null)
                .GetAwaiter().GetResult();

            Assert(provider.SearchNames.SequenceEqual(new[] { movieName }) &&
                   !result.Candidates.Any(x => x.Id == "movie-alias"),
                "Movie search must issue only its standard metadata term and never follow a title-clause alias");

            var duplicateProvider = new FakeScraper(
                "MovieDuplicateID",
                null,
                false,
                null,
                null,
                null,
                new Dictionary<string, List<ScraperSearchInfo>>(StringComparer.OrdinalIgnoreCase)
                {
                    [movieName] = new List<ScraperSearchInfo>
                    {
                        new ScraperSearchInfo
                        {
                            Id = "same",
                            Name = "Alpha Adventure",
                            Category = "movie",
                            Year = 2000,
                        },
                    },
                });
            var duplicate = DanmuMatchSearchEngine.SearchMovieAsync(
                    new[] { duplicateProvider },
                    new Movie { Name = movieName, ProductionYear = 2024 },
                    null,
                    null)
                .GetAwaiter().GetResult();
            Assert(duplicate.Candidates.Count(x => x.Id == "same") == 1 &&
                   duplicate.Candidates.Single(x => x.Id == "same").Year == 2000 &&
                   duplicateProvider.SearchNames.SequenceEqual(new[] { movieName }),
                "Movie default search must retain its sole standard candidate and never add an alias round");
        }

        private static void ReplacesOnlyRegisteredOrdinaryProviderIds()
        {
            var current = new MediaBrowser.Model.Entities.ProviderIdDictionary
            {
                ["BilibiliID"] = "old-bili",
                ["DandanID"] = "old-dandan",
                ["IqiyiID"] = "disabled-iqiyi",
                ["BilibiliIDManual"] = "manual-bili",
                ["DandanIDManual"] = "manual-dandan",
                ["Tmdb"] = "tmdb-1",
                ["Tvdb"] = "tvdb-1",
                ["Imdb"] = "imdb-1",
                ["CustomID"] = "custom-1",
            };
            var updated = DanmuProviderIdWritePolicy.BuildSuccessfulWrite(
                current,
                new[] { "BilibiliID", "DandanID", "IqiyiID", "MgtvID" },
                "DandanID",
                "new-dandan",
                false);

            Assert(updated["DandanID"] == "new-dandan" &&
                   updated["BilibiliID"] == "old-bili" &&
                   updated["IqiyiID"] == "disabled-iqiyi" &&
                   updated["BilibiliIDManual"] == "manual-bili" &&
                   updated["DandanIDManual"] == "manual-dandan" &&
                   updated["Tmdb"] == "tmdb-1" && updated["Tvdb"] == "tvdb-1" &&
                   updated["Imdb"] == "imdb-1" && updated["CustomID"] == "custom-1",
                "successful Season/Episode writes must overwrite only the target key and preserve every other plugin, Manual, and foreign key");
            Assert(current["BilibiliID"] == "old-bili" && current["DandanID"] == "old-dandan",
                "the pure provider-ID policy must not mutate its input dictionary before repository persistence");

            var movieStyle = DanmuProviderIdWritePolicy.BuildSuccessfulWrite(
                current,
                new[] { "BilibiliID", "DandanID", "IqiyiID" },
                "DandanID",
                "movie-upsert",
                false);
            Assert(movieStyle["BilibiliID"] == "old-bili" && movieStyle["IqiyiID"] == "disabled-iqiyi",
                "upsert-only paths such as Movie and pre-download bindings must not apply Season/Episode cleanup");
        }

        private static void IsolatesMovieProviderFailures()
        {
            var search = DanmuMatchSearchEngine.SearchMovieAsync(
                    new AbstractScraper[]
                    {
                        new FakeScraper("WorkingID", new List<ScraperSearchInfo>
                        {
                            new ScraperSearchInfo { Id = "1", Name = "测试电影", Category = "电影", Year = 2024 },
                        }),
                        new FakeScraper("DandanID", null, true),
                    },
                    new Movie { Name = "测试电影", ProductionYear = 2024 },
                    string.Empty,
                    null)
                .GetAwaiter().GetResult();

            Assert(search.Candidates.Count == 1 && search.HasCompletedProviders &&
                   search.CompletedProviderIds.SequenceEqual(new[] { "WorkingID" }) &&
                   search.SelectedCandidate?.Id == "1" && search.Decision == "confident",
                "a successful movie provider should remain normally selectable after a sibling failure");
            Assert(search.SearchErrors.Count == 1 && search.SearchErrors[0].Contains("DandanID") &&
                   search.HasProviderLocalFaults && !search.IsComplete,
                "a failed Dandan proxy provider should remain isolated in diagnostics");
        }

        private static void VerifiesLazyCandidateDetailContract()
        {
            var scraper = new FakeScraper("LazyID", null, false, new Dictionary<string, ScraperMedia>
            {
                ["candidate-a"] = new ScraperMedia
                {
                    Id = "candidate-a",
                    Episodes = new List<ScraperEpisode>
                    {
                        new ScraperEpisode { Id = "source-2", CommentId = "comment-2", EpisodeNumber = 2, Title = "Second" },
                        new ScraperEpisode { Id = "source-1", CommentId = "comment-1", EpisodeNumber = 1, Title = "First" },
                    },
                },
            }, null, new Dictionary<string, List<ScraperSearchInfo>>
            {
                ["Example"] = new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo { Id = "candidate-a", Name = "Example", EpisodeSize = 2 },
                },
            });
            var discovery = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { scraper }, "Example", "Example", 2024, 2, "Example", null)
                .GetAwaiter().GetResult();
            Assert(discovery.Candidates.Count == 1 && scraper.MediaCalls == 0,
                "candidate discovery must not resolve source media before an explicit detail request");

            var registry = new DanmuCandidateEvidenceRegistry();
            var token = registry.Register("target-episode", "LazyID", "candidate-a", 0.9, "search-confidence");
            Assert(registry.TryResolve(token, "target-episode", "LazyID", "candidate-a", out var evidence) &&
                   !registry.TryResolve(token, "target-episode", "LazyID", "candidate-b", out _) &&
                   !registry.TryResolve(token, "other-target", "LazyID", "candidate-a", out _),
                "candidate evidence must bind target, provider, and candidate before provider access");
            evidence.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
            Assert(!registry.TryResolve(token, "target-episode", "LazyID", "candidate-a", out _),
                "expired candidate evidence must be rejected without resolving media");

            var resolver = typeof(DanmuController).GetMethod(
                "ResolveMatchCandidateDetailsMediaAsync", BindingFlags.Static | BindingFlags.NonPublic);
            var safeCandidate = typeof(DanmuController).GetMethod(
                "IsSafeMatchCandidateId", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(resolver != null && safeCandidate != null,
                "lazy candidate-detail resolver and input validator must remain testable");
            Assert(!(bool)safeCandidate.Invoke(null, new object[] { "https://host/media" }) &&
                   !(bool)safeCandidate.Invoke(null, new object[] { "../media" }) &&
                   !(bool)safeCandidate.Invoke(null, new object[] { "a\\media" }) &&
                   !(bool)safeCandidate.Invoke(null, new object[] { "bad\u0001id" }) &&
                   !(bool)safeCandidate.Invoke(null, new object[] { new string('a', 513) }),
                "detail requests must reject URI, path, control-character, and oversized candidate ids");
            var media = ((Task<ScraperMedia>)resolver.Invoke(null, new object[]
                { new Episode { Name = "Local", IndexNumber = 2 }, new Season { Name = "Season" }, scraper, "candidate-a" }))
                .GetAwaiter().GetResult();
            Assert(media?.Episodes.Count == 2 && scraper.MediaCalls == 1,
                "one explicit inspection must resolve only the named candidate exactly once");
            var rows = media.Episodes.OrderBy(item => item.EpisodeNumber ?? int.MaxValue)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList();
            Assert(rows.Select(item => item.Title).SequenceEqual(new[] { "First", "Second" }),
                "candidate detail source rows must be deterministically ordered by number then identity");

            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var detailStart = controller.IndexOf("private async Task<DanmuMatchCandidateDetailResult> GetMatchCandidateDetails", StringComparison.Ordinal);
            var detailEnd = controller.IndexOf("private static Task<ScraperMedia> ResolveMatchCandidateDetailsMediaAsync", detailStart, StringComparison.Ordinal);
            var detailBody = detailStart >= 0 && detailEnd > detailStart
                ? controller.Substring(detailStart, detailEnd - detailStart) : string.Empty;
            Assert(controller.Contains("MatchCandidateDetails") &&
                   controller.Contains("CandidateEvidence.TryResolve(evidenceToken") &&
                   controller.Contains("candidate-detail-evidence-stale") &&
                   controller.Contains("StampCandidateEvidence(episode, result.Target.Candidates)") &&
                   !detailBody.Contains("BindMatch(") && !detailBody.Contains("StartTrackedDownload(") &&
                   !detailBody.Contains("UpdateToRepository") && !detailBody.Contains("CompositePlan") &&
                   !controller.Contains("DanmuSeasonCollection") && !controller.Contains("DanmuSeasonSegment"),
                "detail inspection must remain evidence-gated, target-bound, and independent of collection/segment protocols");
        }

        private static void MergesServerOwnedCandidateMetadataFieldByField()
        {
            var snapshot = new SourceMetadata
            {
                Title = "Search title",
                Year = 2014,
                Category = "Search category",
            };
            var registry = new DanmuCandidateEvidenceRegistry();
            var token = registry.Register(
                "item-1", "DandanID", "candidate-1", 0.9, "search-confidence", snapshot);
            snapshot.Year = 1999;
            Assert(registry.TryResolve(token, "item-1", "DandanID", "candidate-1", out var evidence) &&
                   evidence.SourceMetadata?.Year == 2014,
                "candidate metadata evidence must be a server-owned clone");

            var merged = SourceMetadata.MergeDetailWithSnapshot(
                new SourceMetadata { Title = "Exact title", Category = "Exact category" },
                evidence.SourceMetadata);
            Assert(merged?.Title == "Exact title" && merged.Year == 2014 &&
                   merged.Category == "Exact category",
                "non-empty exact detail must win field-by-field while snapshot fills only missing year");
            var trustworthyExactYear = SourceMetadata.MergeDetailWithSnapshot(
                new SourceMetadata { Title = "Exact title", Year = 2020 },
                new SourceMetadata { Title = "Search title", Year = 2014 });
            Assert(trustworthyExactYear?.Year == 2020,
                "a trustworthy exact production year must win over a conflicting candidate snapshot");
            var noBrowserPath = typeof(DanmuCompositeSeasonSelection).GetProperty("SourceMetadata") == null;
            Assert(noBrowserPath,
                "browser composite selections must not accept SourceMetadata as authoritative input");
            var automaticSelectionJson = JsonSerializer.Serialize(new DanmuCompositeSeasonSelection
            {
                ServerSourceMetadata = new SourceMetadata { Title = "server-only", Year = 2014 },
            });
            Assert(!automaticSelectionJson.Contains("server-only", StringComparison.Ordinal) &&
                   !automaticSelectionJson.Contains("ServerSourceMetadata", StringComparison.Ordinal),
                "automatic planning metadata snapshots must remain server-only");
        }

        private static void FiltersAndSecuresMoviePartEvidence()
        {
            var parts = MoviePartPolicy.GetUsableParts(new[]
            {
                new ScraperMoviePart { Id = "main-a", Title = "国语", Index = 1, IsDownloadable = true },
                new ScraperMoviePart { Id = "trailer", Title = "Official Trailer", Index = 2, IsDownloadable = true },
                new ScraperMoviePart { Id = "bonus", Title = "制作花絮", Index = 3, IsDownloadable = true },
                new ScraperMoviePart { Id = "main-b", Title = "版本 B", Index = 4, IsDownloadable = true },
                new ScraperMoviePart { Id = "main-b", Title = "duplicate", Index = 5, IsDownloadable = true },
                new ScraperMoviePart { Id = "unstable", Title = "unstable", Index = 6, IsDownloadable = false },
            });
            Assert(parts.Select(part => part.Id).SequenceEqual(new[] { "main-a", "main-b" }),
                "explicit extras must be filtered before stable first selection while indistinguishable usable parts remain");
            var indistinguishable = MoviePartPolicy.GetUsableParts(new[]
            {
                new ScraperMoviePart { Id = "opaque-1", Title = "版本", IsDownloadable = true },
                new ScraperMoviePart { Id = "opaque-2", Title = "版本", IsDownloadable = true },
            });
            Assert(indistinguishable.Count == 2 && indistinguishable[0].Id == "opaque-1",
                "all-indistinguishable usable parts must retain stable order and must not block the default first part");
            var single = MoviePartPolicy.GetUsableParts(new[]
            {
                new ScraperMoviePart { Id = "only-main", Title = "正片", IsDownloadable = true },
                new ScraperMoviePart { Id = "only-extra", Title = "Interview", IsDownloadable = true },
            });
            Assert(single.Count == 1 && single[0].Id == "only-main",
                "one usable part after filtering must remain the implicit default without a fabricated alternative");
            var oversized = MoviePartPolicy.GetUsableParts(Enumerable.Range(0, 10000)
                .Select(index => new ScraperMoviePart
                {
                    Id = "part-" + index,
                    Title = "版本 " + index,
                    Index = index,
                    IsDownloadable = true,
                }));
            Assert(oversized.Count == MoviePartPolicy.MaximumUsableParts &&
                   oversized.Select(part => part.Id).SequenceEqual(
                       Enumerable.Range(0, MoviePartPolicy.MaximumUsableParts).Select(index => "part-" + index)),
                "usable Movie choices must be stably capped before evidence registration");

            var registry = new DanmuCandidateEvidenceRegistry();
            var parentToken = registry.Register("movie-1", "BilibiliID", "parent-1", 0.95,
                "search-confidence", new SourceMetadata { Title = "Parent Movie", Year = 2024 });
            var firstToken = registry.RegisterMoviePart(
                parentToken, "movie-1", "BilibiliID", "parent-1", parts[0]);
            var otherParentToken = registry.Register("movie-1", "BilibiliID", "parent-1", 0.95,
                "search-confidence");
            var resolvesSelected = registry.TryResolveMoviePart(
                firstToken, parentToken, "movie-1", "BilibiliID", "parent-1", out var selected);
            Assert(!string.IsNullOrWhiteSpace(firstToken) && resolvesSelected &&
                   selected.PartId == "main-a" &&
                   !registry.TryResolveMoviePart(firstToken, parentToken, "movie-2", "BilibiliID", "parent-1", out _) &&
                   !registry.TryResolveMoviePart(firstToken, parentToken, "movie-1", "OtherID", "parent-1", out _) &&
                   !registry.TryResolveMoviePart(firstToken, parentToken, "movie-1", "BilibiliID", "parent-2", out _) &&
                   !registry.TryResolveMoviePart(firstToken, otherParentToken, "movie-1", "BilibiliID", "parent-1", out _) &&
                   !registry.TryResolveMoviePart("tampered-unregistered", parentToken, "movie-1", "BilibiliID", "parent-1", out _),
                "Movie part evidence must be scoped to parent token, item, provider, and candidate");
            selected.ExpiresUtc = DateTime.UtcNow.AddSeconds(-1);
            Assert(!registry.TryResolveMoviePart(firstToken, parentToken, "movie-1", "BilibiliID", "parent-1", out _),
                "expired Movie part evidence must fail closed without timing-dependent sleeps");
            Assert(string.IsNullOrWhiteSpace(registry.RegisterMoviePart(
                    parentToken, "movie-1", "BilibiliID", "parent-1",
                    new ScraperMoviePart
                    {
                        Id = "excluded", Title = "预告", IsDownloadable = true, IsExplicitNonMain = true,
                    })),
                "an explicitly excluded Movie part must never receive selectable evidence");

            var publicJson = JsonSerializer.Serialize(new DanmuMoviePartChoice
            {
                Token = firstToken,
                PartTitle = "国语",
                Index = 1,
                Selected = true,
            });
            Assert(!publicJson.Contains("main-a", StringComparison.Ordinal),
                "public Movie part payload must never serialize the raw provider leaf id");
            var taskJson = JsonSerializer.Serialize(new DanmuDownloadTaskResult
            {
                SelectedMoviePartId = "main-a",
                PartTitle = "国语",
            });
            Assert(!taskJson.Contains("main-a", StringComparison.Ordinal),
                "download task payload must hide the server-owned selected Movie leaf id");
            var selectedIdProperty = typeof(ScraperMedia).GetProperty(nameof(ScraperMedia.SelectedMoviePartId));
            var rawPartIdProperty = typeof(ScraperMoviePart).GetProperty(nameof(ScraperMoviePart.Id));
            Assert(selectedIdProperty.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null &&
                   selectedIdProperty.GetCustomAttribute<System.Runtime.Serialization.IgnoreDataMemberAttribute>() != null &&
                   rawPartIdProperty.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() != null &&
                   rawPartIdProperty.GetCustomAttribute<System.Runtime.Serialization.IgnoreDataMemberAttribute>() != null &&
                   !JsonSerializer.Serialize(new ScraperMedia { SelectedMoviePartId = "raw-selected-leaf" })
                       .Contains("raw-selected-leaf", StringComparison.Ordinal) &&
                   !JsonSerializer.Serialize(new ScraperMoviePart { Id = "raw-provider-leaf" })
                       .Contains("raw-provider-leaf", StringComparison.Ordinal),
                "raw Movie leaf identities must be hidden from System.Text.Json and IgnoreDataMember serializers");

            var explicitMedia = new ScraperMedia
            {
                Id = "parent", CommentId = "legacy-default", SelectedMoviePartId = "chosen-leaf",
            };
            var chosenEpisode = DanmuMovieMatchHelper.ResolveEpisodeForDownload(
                explicitMedia, new ScraperEpisode { Id = "chosen-leaf", CommentId = "chosen-comment" }, "chosen-leaf");
            Assert(chosenEpisode.CommentId == "chosen-comment",
                "a verified non-default Movie leaf must use its resolved CommentId");
            foreach (var invalidLookup in new[]
            {
                new { Episode = (ScraperEpisode)null, Failure = (Exception)null },
                new { Episode = new ScraperEpisode { Id = "chosen-leaf", CommentId = "" }, Failure = (Exception)null },
                new { Episode = (ScraperEpisode)null, Failure = (Exception)new InvalidOperationException("leaf disappeared") },
            })
            {
                var failedClosed = false;
                try
                {
                    DanmuMovieMatchHelper.ResolveEpisodeForDownload(
                        explicitMedia, invalidLookup.Episode, "chosen-leaf", invalidLookup.Failure);
                }
                catch (DanmuDownloadErrorException)
                {
                    failedClosed = true;
                }
                Assert(failedClosed,
                    "an explicit Movie leaf that is null, empty, or throws must fail closed without legacy fallback");
            }
            var legacyFallback = DanmuMovieMatchHelper.ResolveEpisodeForDownload(
                new ScraperMedia { Id = "parent", CommentId = "legacy-default" }, null, "parent",
                new InvalidOperationException("legacy detail unavailable"));
            Assert(legacyFallback.CommentId == "legacy-default",
                "the legacy default fallback must remain available only when no explicit Movie leaf is selected");

            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var movieDownload = controller.IndexOf(
                "private async Task<DanmuDownloadTaskResult> StartTrackedMovieDownload", StringComparison.Ordinal);
            var evidenceCheck = controller.IndexOf("TryResolveMoviePart(", movieDownload, StringComparison.Ordinal);
            var explicitFailure = controller.IndexOf(
                "电影正片版本选择已失效或不属于当前候选", evidenceCheck, StringComparison.Ordinal);
            var explicitReturn = controller.IndexOf("return failed;", explicitFailure, StringComparison.Ordinal);
            var providerAccess = controller.IndexOf("scraper.GetMedia(movie, request.CandidateId)", movieDownload,
                StringComparison.Ordinal);
            Assert(movieDownload >= 0 && evidenceCheck > movieDownload &&
                   explicitFailure > evidenceCheck && explicitReturn > explicitFailure &&
                   providerAccess > explicitReturn,
                "an explicit Movie part choice must be rejected before any provider access and must never silently fall back");
        }

        private static void ResolvesProviderIdsBySiteThenHierarchy()
        {
            var early = new FakeScraper("EarlyID", null, false, new Dictionary<string, ScraperMedia>
            {
                ["season-id"] = new ScraperMedia
                {
                    Id = "season-id", Title = "Upstream Movie", Year = 2024, Category = "Movie",
                },
                ["current-id"] = new ScraperMedia { Id = "current-id" },
            });
            var late = new FakeScraper("LateID", null, false, new Dictionary<string, ScraperMedia>
            {
                ["episode-id"] = new ScraperMedia { Id = "episode-id" },
            });
            var current = new Movie { Name = "current" };
            var season = new Movie { Name = "season" };
            current.ProviderIds["LateID"] = "episode-id";
            season.ProviderIds["EarlyID"] = "season-id";

            var crossSite = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { early, late },
                    new BaseItem[] { current, season },
                    null)
                .GetAwaiter().GetResult();
            Assert(crossSite.Candidate?.Id == "season-id" && crossSite.MatchOrigin == "provider-id" &&
                   crossSite.Candidate.SourceMetadata?.Title == "Upstream Movie" &&
                   crossSite.Candidate.SourceMetadata.Year == 2024 &&
                   crossSite.Candidate.SourceMetadata.Category == "Movie" &&
                   early.SearchCalls == 0 && early.ApiKeywords.Count == 0,
                "the earlier enabled site must beat a later current-item ProviderId");

            current.ProviderIds["EarlyID"] = "current-id";
            var sameSite = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { early, late },
                    new BaseItem[] { current, season },
                    null)
                .GetAwaiter().GetResult();
            Assert(sameSite.Candidate?.Id == "current-id",
                "within an enabled site, current-item ProviderId must beat the parent value");

            var episodeScraper = new FakeScraper(
                "EpisodeID",
                null,
                false,
                null,
                new Dictionary<string, ScraperEpisode>
                {
                    ["episode-provider-id"] = new ScraperEpisode
                    {
                        Id = "episode-provider-id",
                        CommentId = "episode-comment-id",
                        EpisodeNumber = 3,
                        Title = "Episode Three",
                        SourceMetadata = new SourceMetadata
                        {
                            Title = "Upstream Parent Season", Year = 2023, Category = "Anime",
                        },
                    },
                    ["episode-without-number"] = new ScraperEpisode
                    {
                        Id = "episode-without-number",
                        CommentId = "episode-comment-without-number",
                    },
                    ["episode-without-comment"] = new ScraperEpisode
                    {
                        Id = "episode-without-comment",
                    },
                });
            var episodeItem = new Episode { Name = "episode", IndexNumber = 3 };
            episodeItem.ProviderIds["EpisodeID"] = "episode-provider-id";
            var episodeDecision = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { episodeScraper },
                    new BaseItem[] { episodeItem },
                    null)
                .GetAwaiter().GetResult();
            Assert(episodeDecision.Candidate?.Id == "episode-provider-id" &&
                   episodeDecision.Media?.Episodes.Single().CommentId == "episode-comment-id" &&
                   episodeDecision.Candidate.SourceMetadata?.Title == "Upstream Parent Season" &&
                   episodeDecision.Candidate.SourceMetadata.Year == 2023 &&
                   episodeDecision.Candidate.SourceMetadata.Category == "Anime" &&
                   episodeDecision.Candidate.SourceMetadata.Title != "Episode Three" &&
                   episodeScraper.SearchCalls == 0 && episodeScraper.ApiKeywords.Count == 0,
                "Episode ProviderIds must resolve through GetMediaEpisode rather than the season-media API");
            var directMedia = DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                    episodeScraper, episodeItem, "episode-provider-id", 3)
                .GetAwaiter().GetResult();
            Assert(directMedia?.Episodes.Single().CommentId == "episode-comment-id" &&
                   directMedia.Episodes.Single().EpisodeNumber == 3 &&
                   episodeScraper.MediaCalls == 0 && episodeScraper.MediaEpisodeCalls >= 2,
                "direct Episode ProviderIds must retain the exact GetMediaEpisode result for download without calling GetMedia");

            var noNumberEpisode = new Episode { Name = "episode without local number" };
            noNumberEpisode.ProviderIds["EpisodeID"] = "episode-without-number";
            var noNumberDecision = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { episodeScraper },
                    new BaseItem[] { noNumberEpisode },
                    null)
                .GetAwaiter().GetResult();
            Assert(noNumberDecision.Candidate?.Id == "episode-without-number" &&
                   noNumberDecision.Media?.Episodes.Single().EpisodeNumber == 1 &&
                   noNumberDecision.ResolvedScopeType == "Episode" &&
                   (noNumberDecision.Candidate?.SourceMetadata == null ||
                    !noNumberDecision.Candidate.SourceMetadata.HasValue),
                "an exact Episode ProviderId must remain usable without a local IndexNumber");

            var noCommentEpisode = new Episode { Name = "episode without comment", IndexNumber = 1 };
            noCommentEpisode.ProviderIds["EpisodeID"] = "episode-without-comment";
            var noCommentDecision = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { episodeScraper },
                    new BaseItem[] { noCommentEpisode },
                    null)
                .GetAwaiter().GetResult();
            Assert(noCommentDecision.Candidate == null &&
                   noCommentDecision.Diagnostics.Any(x => x.StartsWith("provider-id-unresolved")),
                "an Episode ProviderId without a downloadable comment id must fall through as stale");

            var parentSeries = new Series { Name = "parent series" };
            parentSeries.ProviderIds["EarlyID"] = "season-id";
            var callsBeforeSeriesScope = early.MediaCalls;
            var seriesScopeDecision = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { early },
                    new BaseItem[] { new Season { Name = "child season" }, parentSeries },
                    null)
                .GetAwaiter().GetResult();
            Assert(seriesScopeDecision.Candidate == null && early.MediaCalls == callsBeforeSeriesScope,
                "Series ProviderIds must remain untouched and unread even when accidentally supplied to the resolver");

            current.ProviderIds["EarlyID"] = "stale-current";
            season.ProviderIds["EarlyID"] = "stale-season";
            var stale = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { early },
                    new BaseItem[] { current, season },
                    null)
                .GetAwaiter().GetResult();
            var bindings = new Dictionary<string, string> { ["EarlyIDManual"] = "manual-id" };
            Assert(stale.Candidate == null && stale.Diagnostics.Any(x => x.StartsWith("provider-id-unresolved")) &&
                   DanmuMatchBindingHelper.TryGetSavedManualBinding(
                       false, new[] { early }, bindings, out _, out var manualId) && manualId == "manual-id" &&
                   !DanmuMatchBindingHelper.TryGetSavedManualBinding(
                       true, new[] { early }, bindings, out _, out _),
                "stale identifiers must continue to binding while rematch bypasses it without deleting it");
        }

        private static void ResolvesMovieProviderLookupIdentifiers()
        {
            var media = new ScraperMedia
            {
                Id = "season-or-album-id",
                CommentId = "470296",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "470296", CommentId = "fallback-episode-id" },
                },
            };
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("BilibiliID", media) == "470296",
                "Bilibili movies should resolve through their ep id");
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("IqiyiID", media) == "season-or-album-id",
                "non-Bilibili movies should resolve through their provider media id");
            media.CommentId = string.Empty;
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("BilibiliID", media) == "470296",
                "Bilibili movie lookup should fall back only to a durable numeric episode ep_id");
        }

        private static void EnforcesItemLocalProviderScopes()
        {
            var movie = new Movie { Name = "movie" };
            var season = new Season { Name = "season" };
            var episode = new Episode { Name = "episode" };
            var series = new Series { Name = "series" };

            Assert(DanmuProviderIdResolver.GetMovieScopes(movie).SequenceEqual(new BaseItem[] { movie }),
                "Movie exact matching must inspect only the Movie");
            Assert(DanmuProviderIdResolver.GetSeasonScopes(season).SequenceEqual(new BaseItem[] { season }),
                "Season exact matching must inspect only the Season");
            Assert(DanmuProviderIdResolver.GetSingleEpisodeDirectScopes(episode)
                    .SequenceEqual(new BaseItem[] { episode }),
                "single Episode direct matching must inspect only the Episode");

            season.ProviderIds["SeasonOnlyID"] = "season-only";
            var seasonOnlyScraper = new FakeScraper("SeasonOnlyID", null, false,
                new Dictionary<string, ScraperMedia>
                {
                    ["season-only"] = new ScraperMedia { Id = "season-only" },
                });
            var seasonOnlyDecision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { seasonOnlyScraper },
                    DanmuProviderIdResolver.GetSingleEpisodeDirectScopes(episode), null)
                .GetAwaiter().GetResult();
            Assert(seasonOnlyDecision.Candidate == null && seasonOnlyScraper.MediaCalls == 0,
                "a Season-only ProviderId must not be reported as Episode-local direct evidence");

            series.ProviderIds["ScopedID"] = "series-value";
            var scraper = new FakeScraper("ScopedID", null, false,
                new Dictionary<string, ScraperMedia>
                {
                    ["series-value"] = new ScraperMedia { Id = "series-value" },
                });
            var decision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { scraper }, new BaseItem[] { season, series }, null)
                .GetAwaiter().GetResult();
            Assert(decision.Candidate == null && scraper.MediaCalls == 0,
                "the resolver must reject Series scopes even when a caller accidentally supplies one");

            season.ProviderIds["ScopedID"] = "series-value";
            season.ProviderIds["ScopedIDManual"] = "series-manual";
            season.ProviderIds["Other"] = "keep";
            series.ProviderIds["ScopedIDManual"] = "series-manual";
            var equalLocalPreserved = DanmuProviderIdResolver.GetItemLocalProviderIds(
                season, series, new[] { scraper });
            Assert(equalLocalPreserved["ScopedID"] == "series-value" &&
                   equalLocalPreserved["ScopedIDManual"] == "series-manual" &&
                   equalLocalPreserved["Other"] == "keep",
                "a Season ID must remain eligible even when its value equals an ignored Series ID");

            var later = new FakeScraper("LaterID", null, false,
                new Dictionary<string, ScraperMedia>
                {
                    ["later-value"] = new ScraperMedia { Id = "later-value" },
                });
            season.ProviderIds["LaterID"] = "later-value";
            var equalValueDecision = DanmuProviderIdResolver.ResolveAsync(
                    new AbstractScraper[] { scraper, later },
                    DanmuProviderIdResolver.GetSeasonScopes(season), null, series)
                .GetAwaiter().GetResult();
            Assert(equalValueDecision.Scraper == scraper &&
                   equalValueDecision.Candidate?.Id == "series-value" &&
                   scraper.MediaCalls == 1 && later.MediaCalls == 0,
                "configured provider order must retain an item-local Season ID that equals the Series value");
            season.ProviderIds["ScopedID"] = "season-value";
            season.ProviderIds["ScopedIDManual"] = "season-manual";
            var localPreserved = DanmuProviderIdResolver.GetItemLocalProviderIds(
                season, series, new[] { scraper });
            Assert(localPreserved["ScopedID"] == "season-value" &&
                   localPreserved["ScopedIDManual"] == "season-manual",
                "a distinct Season ordinary or manual ID must remain eligible");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs"));
            var library = File.ReadAllText(Path.Combine(repositoryRoot, "LibraryManagerEventsHelper.cs"));
            Assert(!controller.Contains("GetSeasonScopes(latest)") &&
                    !controller.Contains("TryGetSavedManualBinding(\n                    true,\n                    scrapers") &&
                    Regex.IsMatch(controller,
                        @"GetSingleEpisodeDirectScopes\(latest\)[\s\S]{0,300}authoritativeSeries,[\s\S]{0,100}cancellationToken\)\.ConfigureAwait\(false\)") &&
                    controller.Contains("_libraryManagerEventsHelper.SaveProviderId(item, providerId, providerValue, true)") &&
                    controller.Contains("CommitSeasonDisplayMirrorAfterTerminalAsync") &&
                    library.Contains("UpsertSeasonDisplayMirrorAsync") &&
                    !library.Contains("SaveAutomaticSeasonProviderId"),
                "r4 must retain item-local Movie/Episode paths while Season matching is fresh-plan only and mirrors only at terminal completion");
        }

        private static void DiscoversCandidatesWithBoundedTitleClauses()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var engine = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "DanmuMatchSearchEngine.cs"))
                .Replace("\r\n", "\n");
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var resolver = File.ReadAllText(Path.Combine(
                repositoryRoot, "Scraper", "DanmuProviderIdResolver.cs")).Replace("\r\n", "\n");

            Assert(engine.Contains(".Take(string.IsNullOrWhiteSpace(keywordOverride) ? 2 : 1)") &&
                   !engine.Contains("DanmuTitleClauseExtractor") &&
                   !engine.Contains("ExtractProviderAliases"),
                "Season search must use only bounded standard metadata terms and must not derive punctuation or provider-result aliases");
            Assert(engine.Contains("// Movies intentionally have one standard metadata term.") &&
                   !engine.Contains("var clauses = DanmuTitleClauseExtractor.Extract(movieName"),
                "Movie search must issue exactly one standard or explicit metadata term without a second hop");
            var libraryEvents = File.ReadAllText(Path.Combine(
                repositoryRoot, "LibraryManagerEventsHelper.cs")).Replace("\r\n", "\n");
            Assert(libraryEvents.Contains("residualSeasonName, season.ProductionYear") &&
                   libraryEvents.Contains("IsDistinctSeasonIdentity(") &&
                   !libraryEvents.Contains("seriesTitle, season.ProductionYear, run.Episodes.Count, seriesTitle"),
                "residual automatic search must keep the Series keyword override while passing only a distinct real Season identity for rank-2 fidelity");
            Assert(resolver.Contains("GetSingleEpisodeDirectScopes(Episode episode)") &&
                   resolver.Contains("new BaseItem[] { episode }") &&
                   controller.Contains("DanmuProviderIdResolver.GetSingleEpisodeDirectScopes(latest)"),
                "the single Episode direct path must never include a Season scope");
            Assert(controller.Contains("InitializeDecision(result, episodeScrapers, forceSearch);") &&
                   controller.Contains("if (!forceSearch)\n            {\n                var providerDecision = await DanmuProviderIdResolver.ResolveAsync(\n                    episodeScrapers,") &&
                   controller.Contains("var rematch = IsRematch(request);"),
                "force/rematch must bypass Episode direct lookup before search fallback");
            Assert(CountOccurrences(controller, "result.ResolvedScopeType = decision.ResolvedScopeType;") == 2 &&
                   CountOccurrences(controller, "result.ResolvedScopeItemId = decision.ResolvedScopeItemId;") == 2 &&
                   CountOccurrences(controller, "target.ResolvedScopeType = source.ResolvedScopeType;") == 3 &&
                   CountOccurrences(controller, "target.ResolvedScopeItemId = source.ResolvedScopeItemId;") == 3,
                "every ApplyProviderDecision and CopyDecision path must preserve resolved scope provenance");
        }

        private static void NormalizesDandanStandaloneSeasonOrdinals()
        {
            var cumulative = Enumerable.Range(15, 12)
                .Select(number => new Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode
                {
                    EpisodeId = 152930000 + number,
                    EpisodeNumber = number.ToString(),
                    EpisodeTitle = "第" + number + "话",
                })
                .ToList();
            cumulative.Insert(3, new Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode
            {
                EpisodeId = 999,
                EpisodeNumber = "PV",
                EpisodeTitle = "正式预告",
            });

            var mapped = DandanSeasonEpisodeMapper.Map(cumulative, true);
            Assert(mapped.Count == 12 && mapped.Select(x => x.EpisodeNumber).SequenceEqual(
                       Enumerable.Range(1, 12).Select(x => (int?)x)),
                "Dandan standalone seasons must filter non-main entries before local 1..N normalization");
            Assert(mapped[0].Id == "152930015" && mapped[11].Id == "152930026" &&
                   mapped.All(x => x.Id == x.CommentId),
                "normalization must retain every real Dandan EpisodeId");
        }

        private static void EnforcesBilibiliPgcIdentifierPolicy()
        {
            Assert(BilibiliPgcIdPolicy.SupportsExactItem(new Season()) &&
                   BilibiliPgcIdPolicy.SupportsExactItem(new Episode()) &&
                   BilibiliPgcIdPolicy.SupportsExactItem(new Movie()) &&
                   !BilibiliPgcIdPolicy.SupportsExactItem(new Series()),
                "Bilibili exact matching must support Season/Movie/Episode but never Series");
            Assert(BilibiliPgcIdPolicy.IsPositiveNumericId("46089") &&
                   BilibiliPgcIdPolicy.IsPositiveNumericId("779775") &&
                   !BilibiliPgcIdPolicy.IsPositiveNumericId("BV1test") &&
                   !BilibiliPgcIdPolicy.IsPositiveNumericId("123,456"),
                "durable Bilibili identifiers must be positive PGC numeric IDs");
            var movieMedia = new ScraperMedia
            {
                Id = "779775",
                CommentId = "779775",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "779775", CommentId = "100,200" },
                },
            };
            Assert(BilibiliPgcIdPolicy.ResolveMovieEpisodeId(movieMedia) == "779775",
                "a PGC Movie must persist ep_id rather than its transient aid,cid tuple");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var bilibili = File.ReadAllText(Path.Combine(
                repositoryRoot, "Scraper", "Bilibili", "Bilibili.cs"));
            Assert(bilibili.Contains("if (isEpisodeItemType)") &&
                   bilibili.Contains("var seasonInfo = await _api.GetSeasonAsync(numericId") &&
                   bilibili.Contains("if (isMovieItemType &&") &&
                   bilibili.Contains("var exactEpisode = await _api.GetEpisodeAsync(numericId"),
                "Movie parent IDs must resolve as season identity while legacy exact ep_id remains a verified fallback");
        }

        private static void ExposesBilibiliAndMgtvExternalIds()
        {
            var biliSeason = new Emby.Plugin.Danmu.Scraper.Bilibili.ExternalId.SeasonExternalId();
            var biliMovie = new Emby.Plugin.Danmu.Scraper.Bilibili.ExternalId.MovieExternalId();
            var biliEpisode = new Emby.Plugin.Danmu.Scraper.Bilibili.ExternalId.EpisodeExternalId();
            var mgtvSeason = new Emby.Plugin.Danmu.Scrapers.Mgtv.ExternalId.SeasonExternalId();
            Assert(biliSeason.Supports(new Season()) && biliSeason.Supports(new Series()) &&
                   biliMovie.Supports(new Movie()) && biliEpisode.Supports(new Episode()) &&
                   mgtvSeason.Supports(new Season()) && mgtvSeason.Supports(new Series()),
                "Bilibili and Mgtv external IDs must be visible on their item editors, including display-only Series fields");
            Assert(biliSeason.UrlFormatString == "#" && biliMovie.UrlFormatString == "#" &&
                   biliEpisode.UrlFormatString == "#",
                "Bilibili polymorphic PGC IDs must not be formatted as one misleading public URL");
        }

        private static void PreservesSeasonSuccessPersistenceContract()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(repositoryRoot, "Core", "Controllers", "DanmuController.cs"));
            var library = File.ReadAllText(Path.Combine(repositoryRoot, "LibraryManagerEventsHelper.cs"));
            Assert(controller.Contains("PersistProviderIdAfterAcceptedOutcome(episode, outcome)") &&
                    !controller.Contains("PersistSeasonProviderIdAfterAcceptedOutcome(") &&
                    controller.Contains("CommitSeasonDisplayMirrorAfterTerminalAsync") &&
                    controller.Contains("CanPersistCompleteSeasonBinding") &&
                   controller.Contains("ErrorCode = \"mapping_required\"") &&
                   controller.Contains("ErrorCode = \"partial_confirmation_required\""),
                "mapped Season downloads must write each successful Episode, persist a Season binding only for a complete single-source plan, and reject positional or unconfirmed partial downloads");
            Assert(library.Contains("persisted = true") &&
                    !library.Contains("SaveAutomaticSeasonProviderId(") &&
                    library.Contains("SeasonDisplayMirrorPolicy.CanCommit") &&
                    library.Contains("acceptedCount") && library.Contains("anyFailed") &&
                    library.Contains("MarkStarted(GetProviderWriteKey(item, providerId), generation)") &&
                    library.Contains("UpsertSeasonDisplayMirrorAsync"),
                "automatic import must write a Season display mirror only after complete accepted terminal success");
        }

        private static void PreservesSeriesPreviewProviderIdContract()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                    repositoryRoot, "Core", "Controllers", "DanmuController.cs"))
                .Replace("\r\n", "\n");
            var seriesStart = controller.IndexOf("else if (item is Series series)", StringComparison.Ordinal);
            var seriesEnd = controller.IndexOf("if (request.SeasonNumber.HasValue", seriesStart, StringComparison.Ordinal);
            var seriesPreview = controller.Substring(seriesStart, seriesEnd - seriesStart);
            Assert(seriesPreview.Contains("_libraryManager.GetItemList(new InternalItemsQuery") &&
                   seriesPreview.Contains("ParentIds = new[] { series.InternalId }") &&
                   seriesPreview.Contains("IncludeItemTypes = new[] { \"Season\" }") &&
                   seriesPreview.Contains("Recursive = false") &&
                   !seriesPreview.Contains("DtoOptions"),
                "Series match preview must enumerate direct, non-projected library Seasons with ProviderIds intact");
            Assert(controller.Contains("item is Series,") &&
                   controller.Contains("item as Series,") &&
                   controller.Contains("bool preserveProvidedSeason = false") &&
                   controller.Contains("Series explicitParentSeries = null") &&
                   controller.Contains("_libraryManager.GetItemById(parentSeries.InternalId)") &&
                   controller.Contains("var latest = preserveProvidedSeason") &&
                   controller.Contains(": _libraryManager.GetItemById(season.Id) as Season ?? season"),
                "Series preview must pass its entry Series explicitly while direct Season/Episode paths refresh the parent by InternalId");

            var scraper = new FakeScraper("SeriesSeasonID", null, false, new Dictionary<string, ScraperMedia>
            {
                ["series-season-id"] = new ScraperMedia
                {
                    Id = "series-season-id",
                    Episodes = new List<ScraperEpisode>
                    {
                        new ScraperEpisode { Id = "episode", CommentId = "episode" },
                    },
                },
            });
            var providerSeason = new Season { Name = "Series preview season" };
            providerSeason.ProviderIds["SeriesSeasonID"] = "series-season-id";
            var decision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { scraper }, new BaseItem[] { providerSeason }, null)
                .GetAwaiter().GetResult();
            Assert(decision.Candidate?.Id == "series-season-id" &&
                   decision.MatchOrigin == "provider-id" &&
                   decision.ResolvedScopeType == "Season",
                "a Series-entry Season retaining ProviderIds must take provider-ID precedence before scoring");
        }

        private static void ProjectsProviderIdMetadataWithoutSearchOrScoring()
        {
            var resolved = new ScraperMedia
            {
                Id = "exact-upstream-id",
                Title = "Upstream-only title",
                Year = 2024,
                Category = "Upstream category",
                EpisodeCount = 26,
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "1", CommentId = "1", Title = "Episode 1" },
                    new ScraperEpisode { Id = "2", CommentId = "2", Title = "Episode 2" },
                },
            };
            var scraper = new FakeScraper("ExactID", new List<ScraperSearchInfo>(), false,
                new Dictionary<string, ScraperMedia>
                {
                    ["movie"] = resolved,
                    ["series"] = resolved,
                    ["season"] = resolved,
                    ["missing"] = null,
                },
                new Dictionary<string, ScraperEpisode>
                {
                    ["episode"] = new ScraperEpisode
                    {
                        Id = "episode",
                        CommentId = "episode-comment",
                        Title = "Upstream episode title",
                    },
                });

            foreach (var scope in new BaseItem[]
            {
                new Movie { Name = "local movie" },
                new Season { Name = "local season" },
            })
            {
                scope.ProviderIds["ExactID"] = scope is Movie ? "movie" :
                    "season";
                var decision = DanmuProviderIdResolver.ResolveAsync(
                        new[] { scraper }, new[] { scope }, null)
                    .GetAwaiter().GetResult();
                Assert(decision.Candidate?.Id == "exact-upstream-id" &&
                       decision.Candidate.Name == "Upstream-only title" &&
                       decision.Candidate.Year == 2024 &&
                       decision.Candidate.Category == "Upstream category" &&
                       decision.Candidate.EpisodeSize == 26,
                    "exact ProviderId candidates must project only declared upstream metadata");
            }

            var ignoredSeries = new Series { Name = "local series" };
            ignoredSeries.ProviderIds["ExactID"] = "series";
            var ignoredSeriesDecision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { scraper }, new BaseItem[] { ignoredSeries }, null)
                .GetAwaiter().GetResult();
            Assert(ignoredSeriesDecision.Candidate == null,
                "Series metadata may be displayed but must never become an exact-match candidate");

            var episode = new Episode { Name = "local episode" };
            episode.ProviderIds["ExactID"] = "episode";
            var directEpisode = DanmuProviderIdResolver.ResolveAsync(
                    new[] { scraper }, new BaseItem[] { episode }, null)
                .GetAwaiter().GetResult();
            Assert(directEpisode.Candidate != null &&
                   directEpisode.Candidate.SourceMetadata == null &&
                   directEpisode.Candidate.EpisodeSize == 1 &&
                   directEpisode.Media?.Episodes.Count == 1 &&
                   directEpisode.Media.Episodes[0].Title == "Upstream episode title",
                "direct Episode identifiers must keep the one-item download result without presenting an episode title as the parent SourceTitle");

            var fallback = new FakeScraper("FallbackID", null, false,
                new Dictionary<string, ScraperMedia>
                {
                    ["fallback"] = new ScraperMedia
                    {
                        Id = "fallback",
                        Episodes = new List<ScraperEpisode>
                        {
                            new ScraperEpisode { Id = "1", CommentId = "1" },
                            new ScraperEpisode { Id = "2", CommentId = "2" },
                            new ScraperEpisode { Id = "not-downloadable" },
                        },
                    },
                });
            var fallbackScope = new Season { Name = "local title must not leak" };
            fallbackScope.ProviderIds["FallbackID"] = "fallback";
            var fallbackDecision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { fallback }, new BaseItem[] { fallbackScope }, null)
                .GetAwaiter().GetResult();
            Assert(fallbackDecision.Candidate?.Name == "标题未知" &&
                   fallbackDecision.Candidate.Year == null &&
                   fallbackDecision.Candidate.Category == string.Empty &&
                   fallbackDecision.Candidate.EpisodeSize == 2,
                "missing detail fields must remain unknown and count only usable resolved episodes");

            var unresolvedScope = new Movie { Name = "unresolved local movie" };
            unresolvedScope.ProviderIds["ExactID"] = "missing";
            var unresolved = DanmuProviderIdResolver.ResolveAsync(
                    new[] { scraper }, new BaseItem[] { unresolvedScope }, null)
                .GetAwaiter().GetResult();
            Assert(unresolved.Candidate == null && unresolved.Diagnostics.Any(x => x == "provider-id-unresolved:ExactID") &&
                   scraper.SearchCalls == 0,
                "resolved and unresolved ProviderId detail paths must not invoke search or scoring");
        }

        private static void PreservesProviderDetailAdapterMetadataContracts()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var dandan = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Dandan", "Dandan.cs"));
            var bilibili = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Bilibili", "Bilibili.cs"));
            var iqiyi = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Iqiyi", "Iqiyi.cs"));
            var mgtv = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Mgtv", "Mgtv.cs"));
            var tencent = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Tencent", "Tencent.cs"));
            var youku = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Youku", "Youku.cs"));

            Assert(dandan.Contains("media.Title = anime.AnimeTitle") &&
                   dandan.Contains("media.Year = anime.Year") &&
                   dandan.Contains("media.Category = anime.TypeDescription") &&
                   dandan.Contains("DandanSeasonEpisodeMapper.Map(") &&
                   dandan.Contains("? media.Episodes.Count") &&
                   dandan.Contains(": anime.EpisodeCount"),
                "Dandan exact Anime details must retain their explicit upstream metadata");
            Assert(bilibili.Contains("Title = videoInfo.Title") &&
                   !bilibili.Contains("Year = videoInfo.Pubdate") &&
                   !bilibili.Contains("Year = tupleVideo.Pubdate") &&
                   !bilibili.Contains("TryGetYearFromSeasonDetail") &&
                   bilibili.Contains("EpisodeCount = videoInfo.VideosCount") &&
                   bilibili.Contains("GetSeasonDetailAsync(numericId") &&
                   bilibili.Contains("Title = !string.IsNullOrWhiteSpace(seasonDetail?.Title)") &&
                   bilibili.Contains("seasonDetail?.Total > 0") &&
                   !bilibili.Contains("Title = ep.Title ?? item.Name") &&
                   !bilibili.Contains("Title = item?.Name"),
                "Bilibili exact details must retain known values without treating publication/upload timestamps as production year");
            Assert(iqiyi.Contains("media.Title = video.VideoName") &&
                   iqiyi.Contains("media.Category = video.channelName") &&
                   iqiyi.Contains("media.EpisodeCount = video.VideoCount") &&
                   !iqiyi.Contains("media.Year ="),
                "iQiyi exact details must retain supported fields while leaving year unknown");
            foreach (var source in new[] { mgtv, tencent, youku })
            {
                Assert(source.Contains("media.EpisodeCount = media.Episodes.Count") &&
                       source.Contains("media.Title = video.Title") &&
                       source.Contains("media.Year = video.Year") &&
                       (source.Contains("media.Category = video.TypeName") ||
                        source.Contains("media.Category = video.Type")),
                    "providers whose exact collection detail exposes title/year/category must return those available fields");
            }
        }

        private static void RequiresVerifiedDirectEpisodeDetailsAndUsesResolvedUgcCounts()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var dandan = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Dandan", "Dandan.cs"));
            var bilibili = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Bilibili", "Bilibili.cs"));
            var bilibiliApi = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Bilibili", "BilibiliApi.cs"));
            var iqiyi = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Iqiyi", "Iqiyi.cs"));
            var mgtv = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Mgtv", "Mgtv.cs"));
            var tencent = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Tencent", "Tencent.cs"));
            var youku = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Youku", "Youku.cs"));

            Assert(dandan.Contains("DandanEpisodeId.TryGetAnimeId(id, out var animeId)") &&
                   dandan.Contains("includeNonMainEpisodes: true") &&
                   dandan.Contains("DandanEpisodeId.CreateVerifiedEpisode(id, anime?.Episodes)") &&
                   !dandan.Contains("Dandan's existing detail endpoint accepts Anime IDs"),
                "Dandan Episode ProviderIds must derive only a candidate Anime parent, then verify the complete EpisodeId from detail before becoming exact evidence");
            Assert(mgtv.Contains("GetVideoAsync(seasonId") &&
                   mgtv.Contains("string.Equals(x.VideoId, id") &&
                   tencent.Contains("GetVideoAsync(seasonId") &&
                   tencent.Contains("string.Equals(x.Vid, id"),
                "MGTV and Tencent Episode ProviderIds must be found in a remotely resolved same-provider collection");
            Assert(youku.Contains("GetEpisodeAsync(id") &&
                   youku.Contains("expectedId = id.Replace") &&
                   iqiyi.Contains("GetVideoBaseAsync(id") && iqiyi.Contains("if (video == null)"),
                "Youku and iQiyi Episode ProviderIds must use exact remote detail calls");
            Assert(bilibili.Contains("GetVideoByAidAsync(tupleAid") &&
                   bilibili.Contains("tupleVideo?.Aid != tupleAid") &&
                   bilibili.Contains("FirstOrDefault(x => x.Cid == tupleCid)") &&
                   bilibili.Contains("matchingUgcEpisode") &&
                   bilibiliApi.Contains("x/web-interface/view?aid={aid}") &&
                   bilibili.Contains("GetEpisodeAsync(numericId") &&
                   bilibili.Contains("上游未确认该 ep_id；返回 null") &&
                   bilibili.Contains("scraperMedia.EpisodeCount = scraperMedia.Episodes.Count"),
                "Bilibili must verify saved aid,cid tuples by official AID detail and count UGC matches from the resolved collection");

            var noDetailScraper = new FakeScraper("NoDetailID", null, false, null,
                new Dictionary<string, ScraperEpisode> { ["unverified"] = null });
            var localEpisode = new Episode { Name = "local-only episode" };
            localEpisode.ProviderIds["NoDetailID"] = "unverified";
            var decision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { noDetailScraper }, new BaseItem[] { localEpisode }, null)
                .GetAwaiter().GetResult();
            Assert(decision.Candidate == null &&
                   decision.Diagnostics.Any(x => x == "provider-id-unresolved:NoDetailID") &&
                   noDetailScraper.MediaCalls == 0 && noDetailScraper.SearchCalls == 0,
                "an unverified direct Episode ID must not become an exact candidate or trigger search");

            var tupleScraper = new FakeScraper("BilibiliID", null, false, null,
                new Dictionary<string, ScraperEpisode>
                {
                    ["123,456"] = new ScraperEpisode
                    {
                        Id = "123,456",
                        CommentId = "123,456",
                        Title = "Verified upstream part",
                        EpisodeNumber = 2,
                    },
                });
            var tupleEpisode = new Episode { Name = "local tuple episode", IndexNumber = 2 };
            tupleEpisode.ProviderIds["BilibiliID"] = "123,456";
            var tupleDecision = DanmuProviderIdResolver.ResolveAsync(
                    new[] { tupleScraper }, new BaseItem[] { tupleEpisode }, null)
                .GetAwaiter().GetResult();
            Assert(tupleDecision.Candidate?.Id == "123,456" &&
                   tupleDecision.Candidate.SourceMetadata == null &&
                   tupleDecision.Media?.Episodes.Single().Id == "123,456" &&
                   tupleDecision.Media.Episodes.Single().CommentId == "123,456" &&
                   tupleDecision.Media.Episodes.Single().Title == "Verified upstream part" &&
                   tupleScraper.MediaCalls == 0 && tupleScraper.SearchCalls == 0,
                "a verified Bilibili aid,cid episode must round-trip without presenting its episode title as parent SourceTitle or triggering search");
        }

        private static void MapsEpisodeSourceNumbersSafely()
        {
            Assert(DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(3, 12) == 3,
                "the local episode number should be the default source suggestion when available");
            Assert(!DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(13, 12).HasValue,
                "a source suggestion outside the candidate episode list should be omitted");
            Assert(!DanmuEpisodeMatchHelper.IsValidSourceEpisodeNumber(0, 12) &&
                   DanmuEpisodeMatchHelper.IsValidSourceEpisodeNumber(12, 12),
                "source episode validation should accept only positive existing numbers");

            var sourceEpisodes = new List<ScraperEpisode>
            {
                new ScraperEpisode { CommentId = "episode-1", EpisodeNumber = 1 },
                new ScraperEpisode { CommentId = "episode-3", EpisodeNumber = 3 },
            };
            Assert(DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(1, sourceEpisodes) == 1 &&
                   DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(2, sourceEpisodes) == null &&
                   DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(3, sourceEpisodes) == 3 &&
                   DanmuEpisodeMatchHelper.TryGetSourceEpisode(sourceEpisodes, 3, out var third) &&
                   third.CommentId == "episode-3",
                "explicit provider numbering should preserve a real episode gap instead of mapping episode 2 by list position");

            var legacyEpisodes = new List<ScraperEpisode>
            {
                new ScraperEpisode { CommentId = "legacy-1" },
                new ScraperEpisode { CommentId = "legacy-2" },
            };
            Assert(DanmuEpisodeMatchHelper.TryGetSourceEpisode(legacyEpisodes, 2, out var legacySecond) &&
                   legacySecond.CommentId == "legacy-2",
                "episode collections with no reliable numbering should retain the legacy positional fallback");
        }

        private static void DeserializesBilibiliExactVideoDetails()
        {
            const string officialDetailJson =
                "{\"bvid\":\"BV1test\",\"aid\":123,\"videos\":2,\"title\":\"Exact video\",\"pages\":[{\"cid\":456,\"page\":2,\"part\":\"Exact part\"}],\"ugc_season\":{\"id\":9,\"title\":\"UGC season\",\"sections\":[{\"id\":10,\"episodes\":[{\"id\":11,\"aid\":123,\"cid\":789,\"title\":\"UGC episode\"}]}]}}";
            var video = JsonSerializer.Deserialize<Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Video>(officialDetailJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert(video?.VideosCount == 2 && video.Pages?.Single().Cid == 456 &&
                   video.Pages.Single().PartName == "Exact part" &&
                   video.UgcSeason?.Sections?.Single().Episodes?.Single().AId == 123 &&
                   video.UgcSeason.Sections.Single().Episodes.Single().CId == 789 &&
                   video.UgcSeason.Sections.Single().Episodes.Single().Title == "UGC episode",
                "official Bilibili AID/BVID detail JSON must retain videos, pages.part, and ugc_season episode fields");
        }

        private static void DeserializesBilibiliExactSeasonDetails()
        {
            const string officialDetailJson =
                "{\"code\":0,\"result\":{\"season_id\":46089,\"title\":\"葬送的芙莉莲\",\"season_title\":\"葬送的芙莉莲 第一季\",\"total\":28,\"publish\":{\"pub_time\":\"2023-09-29 23:00:00\"}}}";
            var detail = JsonSerializer.Deserialize<ApiResult<VideoSeasonDetail>>(officialDetailJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert(detail?.Code == 0 && detail.Result?.SeasonId == 46089 &&
                   detail.Result.Title == "葬送的芙莉莲" &&
                   detail.Result.SeasonTitle == "葬送的芙莉莲 第一季" &&
                   detail.Result.Total == 28 &&
                   detail.Result.Publish?.PubTime == "2023-09-29 23:00:00",
                "official Bilibili PGC season detail JSON must retain title, season_title, publish.pub_time, and declared total");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var biliSource = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Bilibili", "Bilibili.cs"));
            var apiSource = File.ReadAllText(Path.Combine(repositoryRoot, "Scraper", "Bilibili", "BilibiliApi.cs"));
            Assert(biliSource.Contains("GetSeasonDetailAsync(numericId") &&
                   biliSource.Contains("seasonDetail?.Total > 0") &&
                   !biliSource.Contains("TryGetYearFromSeasonDetail") &&
                   !biliSource.Contains("Year = videoInfo.Pubdate") &&
                   !biliSource.Contains("Year = tupleVideo.Pubdate") &&
                   !apiSource.Contains("Year = result.Result.PubTime"),
                "exact Bilibili resolution must use same-ID detail metadata without mapping publication/upload timestamps to work Year");
            Assert(biliSource.Contains("VideoSeasonDetail seasonDetail = null") &&
                   biliSource.Contains("exact metadata detail unavailable; retaining ep/list media") &&
                   biliSource.Contains("must not discard ep/list's exact downloadable media"),
                "failed Bilibili Season display-detail enrichment must preserve exact ep/list media");
            Assert(apiSource.Contains("pgc/view/web/season?season_id={seasonId}") &&
                   !apiSource.Contains("SearchAsync(seasonId"),
                "Bilibili Season metadata enrichment must use only the exact season detail endpoint");
        }

        private static void DeserializesAndNormalizesBilibiliEpisodes()
        {
            const string json = "{\"season_id\":46089,\"episodes\":[" +
                                "{\"id\":101,\"title\":\"1\",\"long_title\":\"第1集预告\",\"badge\":\"预告\",\"badge_type\":1,\"section_type\":1,\"duration\":35000}," +
                                "{\"id\":201,\"title\":\"1\",\"long_title\":\"冒险开始\",\"badge_type\":0,\"section_type\":0,\"duration\":1500000}," +
                                "{\"id\":102,\"title\":\"2\",\"badge\":\"预告\",\"badge_type\":1,\"section_type\":1,\"duration\":40000}," +
                                "{\"id\":202,\"title\":\"2\",\"badge_type\":0,\"section_type\":0,\"duration\":1490000}]}";
            var season = JsonSerializer.Deserialize<VideoSeason>(json);
            Assert(season.SeasonId == 46089 && season.Episodes[0].SectionType == 1, "underscored Bilibili fields should deserialize explicitly");

            var normalized = BilibiliEpisodeNormalizer.Normalize(season.Episodes);
            Assert(normalized.Count == 2, "interleaved previews should be removed");
            Assert(normalized[0].Id == 201 && normalized[1].Id == 202, "full episodes should retain canonical numeric order");
        }

        private static void ClassifiesOnlyExplicitNonMainTitles()
        {
            Assert(EpisodeContentClassifier.IsExplicitNonMain("【预告】第8集"), "explicit preview marker should be excluded");
            Assert(EpisodeContentClassifier.IsExplicitNonMain("第8集预告"), "a Chinese preview suffix should be excluded");
            Assert(EpisodeContentClassifier.IsExplicitNonMain("PV 01"), "explicit PV marker should be excluded");
            Assert(!EpisodeContentClassifier.IsExplicitNonMain("第1集"), "ordinary episode title should remain");
            Assert(!EpisodeContentClassifier.IsExplicitNonMain("PVZ大战"), "PV letters inside a word should not be treated as a marker");
        }

        private static void ResolvesDandanCredentialsByCompletePair()
        {
            var configured = DandanCredentialResolver.Resolve(
                " configured-id ", " configured-secret ",
                "environment-id", "environment-secret",
                "legacy-id", "legacy-secret");
            Assert(configured.ApiId == "configured-id" && configured.ApiSecret == "configured-secret",
                "configured Dandan credentials should win and be trimmed");
            Assert(configured.Source == "插件配置", "credential source should identify plugin configuration");

            var environment = DandanCredentialResolver.Resolve(
                "", "", " environment-id ", " environment-secret ", "legacy-id", "legacy-secret");
            Assert(environment.ApiId == "environment-id" && environment.Source == "环境变量",
                "environment pair should be used when configuration is empty");

            var legacy = DandanCredentialResolver.Resolve("", "", "", "", " legacy-id ", " legacy-secret ");
            Assert(legacy.ApiId == "legacy-id" && legacy.Source == "内置配置",
                "legacy pair should remain a final compatibility fallback");
        }

        private static void RejectsIncompleteDandanCredentialsWithoutLeakingValues()
        {
            var message = CaptureCredentialError("LEAK_ID", "", "environment-id", "environment-secret");
            Assert(message.Contains("不完整"), "partial configured credentials should report an incomplete pair");
            Assert(!message.Contains("LEAK_ID"), "credential errors must not include the API ID");

            message = CaptureCredentialError("", "LEAK_SECRET", "environment-id", "environment-secret");
            Assert(!message.Contains("LEAK_SECRET"), "credential errors must not include the API Secret");

            message = CaptureCredentialError("", "", "", "");
            Assert(message.Contains("缺少"), "an empty credential chain should report missing credentials");
        }

        private static string CaptureCredentialError(
            string configuredId,
            string configuredSecret,
            string environmentId,
            string environmentSecret)
        {
            try
            {
                DandanCredentialResolver.Resolve(
                    configuredId, configuredSecret, environmentId, environmentSecret, "", "");
                throw new InvalidOperationException("expected credential resolution to fail");
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        private static void PreservesLegacyDandanApiDefaults()
        {
            var serializer = new XmlSerializer(typeof(DandanOption));
            DandanOption option;
            using (var reader = new StringReader("<DandanOption />"))
            {
                option = (DandanOption)serializer.Deserialize(reader);
            }

            Assert(!option.UseProxyApi,
                "legacy Dandan configuration without an API mode should remain in custom API mode");
            Assert(option.ProxyCorsUrl == string.Empty,
                "legacy Dandan configuration should default to an empty proxy CORS prefix");
            Assert(option.WithRelatedDanmu && option.ChConvert == 0,
                "adding proxy settings must not change existing Dandan option defaults");
        }

        private static void PreservesSavedManualBindingsUntilForcedSearch()
        {
            var first = new FakeScraper("FirstID", null);
            var saved = new FakeScraper("SavedID", null);
            var providerIds = new Dictionary<string, string>
            {
                ["FirstID"] = "automatic-id",
                ["SavedIDManual"] = "saved-manual-id",
            };

            Assert(DanmuMatchBindingHelper.TryGetSavedManualBinding(
                       false, new[] { first, saved }, providerIds, out var scraper, out var manualId) &&
                   scraper == saved && manualId == "saved-manual-id",
                "Movie and Season previews should prefer an existing explicit manual binding");
            Assert(!DanmuMatchBindingHelper.TryGetSavedManualBinding(
                       true, new[] { first, saved }, providerIds, out _, out _) &&
                   providerIds["SavedIDManual"] == "saved-manual-id",
                "forced search should bypass but not delete the saved manual binding");
        }

        private static void AppliesDuplicateAndForceRefreshPolicy()
        {
            var now = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Local);
            Assert(DanmuDownloadPolicy.ShouldSkipExistingDanmu(
                       false, true, now.AddDays(-2), now),
                "a recent existing danmu file should produce a duplicate skip");
            Assert(!DanmuDownloadPolicy.ShouldSkipExistingDanmu(
                       true, true, now.AddMinutes(-1), now),
                "force refresh should bypass the duplicate skip policy");
            Assert(!DanmuDownloadPolicy.ShouldSkipExistingDanmu(
                       false, true, now.AddDays(-8), now) &&
                   !DanmuDownloadPolicy.ShouldSkipExistingDanmu(
                       false, false, DateTime.MinValue, now),
                "expired or missing danmu files should remain downloadable");

            var invalidMovie = new ScraperMedia
            {
                Id = string.Empty,
                CommentId = string.Empty,
                Episodes = new List<ScraperEpisode>(),
            };
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("IqiyiID", invalidMovie) == string.Empty,
                "movie candidate preparation should reject media without a provider playback/comment identifier");
        }

        private static void PreservesSingleEpisodeIsolationAndLegacyTaskShape()
        {
            var seasonBindings = new Dictionary<string, string>
            {
                ["DandanIDManual"] = "season-binding",
            };
            var sources = new List<ScraperEpisode>
            {
                new ScraperEpisode { CommentId = "source-1" },
                new ScraperEpisode { CommentId = "source-2" },
                new ScraperEpisode { CommentId = "source-3" },
            };
            Assert(DanmuEpisodeMatchHelper.TryGetSourceEpisode(sources, 2, out var selected) &&
                   selected.CommentId == "source-2" && sources.Count == 3 &&
                   seasonBindings["DandanIDManual"] == "season-binding",
                "single-Episode selection should isolate the requested source and leave sibling/source collections and Season binding intact");

            var legacySeasonTask = new DanmuDownloadTaskResult
            {
                TaskId = "season-task",
                SeriesId = "series-id",
                SeasonId = "season-id",
                SeasonName = "Season 1",
                Status = "running",
                Episodes = new List<DanmuEpisodeDownloadResult>
                {
                    new DanmuEpisodeDownloadResult { ItemId = "episode-1", EpisodeNumber = 1 },
                    new DanmuEpisodeDownloadResult { ItemId = "episode-2", EpisodeNumber = 2 },
                },
            };
            var json = JsonSerializer.Serialize(legacySeasonTask);
            var roundTrip = JsonSerializer.Deserialize<DanmuDownloadTaskResult>(json);
            Assert(roundTrip.SeasonId == "season-id" && roundTrip.SeriesId == "series-id" &&
                   roundTrip.Episodes.Count == 2 && roundTrip.Status == "running",
                "additive single-target fields must preserve the existing Series/Season task shape");
        }

        private static void MigratesOfficialProxyCorsConfigurationWithoutPersistingItsAddress()
        {
            var serializer = new XmlSerializer(typeof(DandanOption));
            DandanOption Deserialize(string xml)
            {
                using (var reader = new StringReader(xml))
                {
                    return (DandanOption)serializer.Deserialize(reader);
                }
            }

            var legacyEmpty = Deserialize("<DandanOption />");
            var legacyCustom = Deserialize("<DandanOption><ProxyCorsUrl>https://worker.example/cors/</ProxyCorsUrl></DandanOption>");
            var explicitOfficial = Deserialize("<DandanOption><UseOfficialProxyCors>true</UseOfficialProxyCors></DandanOption>");
            var explicitCustom = Deserialize("<DandanOption><UseOfficialProxyCors>false</UseOfficialProxyCors></DandanOption>");

            Assert(DandanApi.ResolveUseOfficialProxyCors(legacyEmpty),
                "a legacy configuration without a custom prefix should migrate to official CORS");
            Assert(!DandanApi.ResolveUseOfficialProxyCors(legacyCustom),
                "a legacy configuration with a custom prefix should preserve custom CORS routing");
            Assert(DandanApi.ResolveUseOfficialProxyCors(explicitOfficial) &&
                   !DandanApi.ResolveUseOfficialProxyCors(explicitCustom),
                "explicit official CORS choices must take precedence over the custom-prefix value");

            explicitCustom.ProxyCorsUrl = string.Empty;
            Assert(!DandanApi.ResolveUseOfficialProxyCors(explicitCustom),
                "an explicit false choice must not reset when the custom prefix is empty");
            explicitOfficial.ProxyCorsUrl = "https://worker.example/cors/";
            Assert(DandanApi.ResolveUseOfficialProxyCors(explicitOfficial),
                "an explicit true choice must remain selected when a custom prefix is retained");

            string serialized;
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, explicitOfficial);
                serialized = writer.ToString();
            }
            Assert(serialized.Contains("UseOfficialProxyCors") &&
                   !serialized.Contains("workers.dev", StringComparison.OrdinalIgnoreCase),
                "serializing the official CORS selection must persist only the boolean choice");
        }

        private static void NormalizesAndValidatesDandanProxyPrefixes()
        {
            Assert(
                DandanApi.NormalizeProxyCorsUrl("  https://worker.example/cors  ") ==
                "https://worker.example/cors/",
                "proxy CORS prefixes should be trimmed and receive one trailing slash");
            Assert(
                DandanApi.NormalizeProxyCorsUrl("https://worker.example/cors////") ==
                "https://worker.example/cors/",
                "repeated trailing slashes should normalize to exactly one slash");

            var invalidPrefixes = new[]
            {
                string.Empty,
                "relative/cors/",
                "ftp://worker.example/cors/",
                "https://worker.example/cors/?token=LEAK_QUERY",
                "https://worker.example/cors/#LEAK_FRAGMENT"
            };
            foreach (var invalidPrefix in invalidPrefixes)
            {
                var message = CaptureProxyPrefixError(invalidPrefix);
                Assert(message.Contains("missing or invalid"),
                    "invalid proxy prefixes should produce a deterministic configuration error");
                Assert((invalidPrefix.Length == 0 || !message.Contains(invalidPrefix)) &&
                       !message.Contains("LEAK_QUERY") &&
                       !message.Contains("LEAK_FRAGMENT"),
                    "proxy configuration errors must not echo the configured value");
            }
        }

        private static string CaptureProxyPrefixError(string proxyCorsUrl)
        {
            try
            {
                DandanApi.NormalizeProxyCorsUrl(proxyCorsUrl);
                throw new InvalidOperationException("expected proxy prefix validation to fail");
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        private static void RoutesExistingDandanEndpointsWithoutLocalProxyAuthentication()
        {
            const string proxyPrefix = "https://worker.example/cors/";
            var officialUrls = new[]
            {
                "https://api.dandanplay.net/api/v2/search/anime?keyword=Frieren%20S2",
                "https://api.dandanplay.net/api/v2/bangumi/12345",
                "https://api.dandanplay.net/api/v2/comment/67890?withRelated=true&chConvert=2"
            };

            foreach (var officialUrl in officialUrls)
            {
                Assert(DandanApi.RouteOfficialUrl(officialUrl, false, false, string.Empty) == officialUrl,
                    "custom API mode should preserve the exact official URL");
                Assert(DandanApi.RouteOfficialUrl(officialUrl, true, false, proxyPrefix) ==
                       proxyPrefix + officialUrl,
                    "custom proxy mode should preserve the complete endpoint and query string");
            }

            var officialRoutes = officialUrls
                .Select(url => DandanApi.RouteOfficialUrl(url, true, true, string.Empty))
                .ToList();
            var officialPrefixLength = officialRoutes[0].Length - officialUrls[0].Length;
            var officialPrefix = officialRoutes[0].Substring(0, officialPrefixLength);
            Assert(!string.IsNullOrWhiteSpace(officialPrefix) && officialRoutes.Select((route, index) =>
                       route == officialPrefix + officialUrls[index]).All(value => value),
                "official CORS mode should route every endpoint through one exact backend prefix");

            Assert(DandanApi.ShouldAddLocalAuthentication(false),
                "custom API mode should retain local Dandanplay signing");
            Assert(!DandanApi.ShouldAddLocalAuthentication(true),
                "proxy API mode should not add local Dandanplay authentication");
            Assert(DandanApi.RouteOfficialUrl(officialUrls[0], true, false, proxyPrefix).Contains("search/anime"),
                "proxy routing should succeed independently of any local credential resolver");
            Assert(!DandanApi.ResolveUseOfficialProxyCors(new DandanOption
            {
                UseOfficialProxyCors = false,
                ProxyCorsUrl = proxyPrefix,
            }), "custom CORS mode must not fall back to official routing");
        }

        private static void PreservesDandanTitleBasedMatchingEndpoints()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Scraper", "Dandan", "DandanApi.cs"));
            var source = File.ReadAllText(sourcePath);

            Assert(source.Contains("search/anime") && source.Contains("bangumi/") && source.Contains("comment/"),
                "Dandan should retain its search, bangumi, and comment endpoint pipeline");
            Assert(source.IndexOf("/match", StringComparison.OrdinalIgnoreCase) < 0 &&
                   source.IndexOf("fileHash", StringComparison.OrdinalIgnoreCase) < 0,
                "Dandan must not introduce dd-danmaku hash matching");
            Assert(!Regex.IsMatch(source,
                    "(?:_logger|logger)\\.[A-Za-z]+\\([^;]*(?:ApiId|ApiSecret|X-Signature|signature)",
                    RegexOptions.IgnoreCase),
                "Dandan request logging must not include credential, signature, or authentication-header values");
        }

        private static void EmbedsDandanCredentialSettings()
        {
            var assembly = typeof(DandanCredentialResolver).Assembly;
            var names = assembly.GetManifestResourceNames();
            var html = ReadResource(assembly, names.Single(x => x.EndsWith("configPage.html", StringComparison.OrdinalIgnoreCase)));
            var script = ReadResource(assembly, names.Single(x => x.EndsWith("config.js", StringComparison.OrdinalIgnoreCase)));

            Assert(html.Contains("id=\"DandanApiId\""), "settings page should contain the Dandan API ID input");
            Assert(html.Contains("id=\"DandanApiSecret\"") && html.Contains("type=\"password\""),
                "settings page should mask the Dandan API Secret");
            Assert(html.Contains("id=\"UseProxyApi\"") && html.Contains("id=\"UseCustomApi\"") &&
                   CountOccurrences(html, "name=\"DandanApiMode\"") == 2,
                "settings page should contain two mutually exclusive Dandan API mode radios");
            Assert(html.Contains("id=\"ProxyCorsUrl\"") && html.Contains("id=\"DandanProxyApiSettings\"") &&
                   html.Contains("id=\"DandanCustomApiSettings\""),
                "settings page should contain the proxy CORS input and both conditional sections");
            Assert(html.Contains("id=\"UseOfficialProxyCors\"") &&
                   !html.Contains("workers.dev", StringComparison.OrdinalIgnoreCase),
                "settings page should expose only the official-CORS choice, never its backend address");
            Assert(!html.Contains("id=\"ProxyCorsUrl\" value="),
                "settings page must not prefill or embed a proxy CORS URL");
            Assert(script.Contains("config.Dandan.ApiId") && script.Contains("config.Dandan.ApiSecret"),
                "settings script should load both Dandan credential values");
            Assert(script.Contains("config.Dandan.UseProxyApi === true") &&
                   script.Contains("config.Dandan.ProxyCorsUrl || ''"),
                "settings script should load the API mode and proxy CORS prefix with legacy-safe defaults");
            Assert(script.Contains("dandan.ApiId") && script.Contains("dandan.ApiSecret") &&
                   script.Contains("dandan.WithRelatedDanmu") && script.Contains("dandan.ChConvert"),
                "settings script should save credentials without dropping existing Dandan options");
            Assert(script.Contains("dandan.UseProxyApi") && script.Contains("dandan.ProxyCorsUrl"),
                "settings script should save the selected API mode and proxy CORS prefix");
            Assert(script.Contains("resolveUseOfficialProxyCors") && script.Contains("dandan.UseOfficialProxyCors") &&
                   script.Contains("ProxyCorsUrl').disabled = !useProxyApi || useOfficialProxyCors"),
                "settings script should migrate legacy choices, persist an explicit boolean, and disable only the active official route input");
            Assert(script.Contains("classList.toggle('hide', !useProxyApi)") &&
                   script.Contains("classList.toggle('hide', useProxyApi)"),
                "settings script should switch proxy and custom field visibility");
            Assert(!script.Contains("DandanApiId').value = ''") &&
                   !script.Contains("DandanApiSecret').value = ''") &&
                   !script.Contains("ProxyCorsUrl').value = ''"),
                "switching API modes must not clear inactive values");
        }

        private static void VerifiesVersionedConfigurationPageResources()
        {
            var assembly = typeof(DandanCredentialResolver).Assembly;
            var generatedType = assembly.GetType("Emby.Plugin.Danmu.Configuration.GeneratedConfigurationPageResources");
            Assert(generatedType != null, "the build should compile generated configuration-page resource names");
            var token = (string)generatedType.GetField("CacheToken", BindingFlags.Static | BindingFlags.NonPublic)
                .GetRawConstantValue();
            var pageName = (string)generatedType.GetField("PageName", BindingFlags.Static | BindingFlags.NonPublic)
                .GetRawConstantValue();
            var controllerName = (string)generatedType.GetField("ControllerName", BindingFlags.Static | BindingFlags.NonPublic)
                .GetRawConstantValue();
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            Assert(assembly.GetName().Name == "Emby.Plugin.Danmu",
                "the configuration heading must not change the plugin assembly identity");
            Assert(token == NormalizeCacheToken(informationalVersion) &&
                   Regex.IsMatch(token, "^[A-Za-z0-9_-]+$"),
                "the generated cache token should normalize informational-version metadata to a URL-safe identifier");
            Assert(pageName == "danmu-" + token && controllerName == "danmuJs-" + token,
                "configuration page and controller identifiers must share the generated token");
            Assert(NormalizeCacheToken("2.0.1+r5") == "2-0-1-r5",
                "cache-token normalization should replace unsafe build-metadata punctuation");

            var names = assembly.GetManifestResourceNames();
            var html = ReadResource(assembly, names.Single(x => x.EndsWith("configPage.html", StringComparison.OrdinalIgnoreCase)));
            Assert(html.Contains("data-controller=\"__plugin/" + controllerName + "\"") &&
                   !html.Contains("__DANMU_CONFIG_CACHE_TOKEN__", StringComparison.Ordinal),
                "the embedded configuration page should contain the matching generated controller without a placeholder");

            var sourceRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var sourceHtml = File.ReadAllText(Path.Combine(sourceRoot, "Configuration", "configPage.html"));
            Assert(ContainsConfigurationHeading(sourceHtml, "DanmuPlus 配置") &&
                   !ContainsConfigurationHeading(sourceHtml, "Danmu 配置"),
                "the configuration-page source template should use only the DanmuPlus section heading");
            Assert(ContainsConfigurationHeading(html, "DanmuPlus 配置") &&
                   !ContainsConfigurationHeading(html, "Danmu 配置"),
                "the generated embedded configuration page should use only the DanmuPlus section heading");

            const string sourceUrl = "https://github.com/mouxiaohao/emby-plugin-danmuplus/tree/main";
            const string legacySourceUrl = "https://github.com/cxfksword/jellyfin-plugin-danmu";
            Assert(ContainsConfigurationSourceLink(sourceHtml, sourceUrl) &&
                   !ContainsConfigurationSourceLink(sourceHtml, legacySourceUrl),
                "the configuration-page source template should link only its source action to the DanmuPlus main branch");
            Assert(ContainsConfigurationSourceLink(html, sourceUrl) &&
                   !ContainsConfigurationSourceLink(html, legacySourceUrl),
                "the generated embedded configuration page should retain the external DanmuPlus main-branch source action");
            Assert(CountOccurrences(sourceHtml, "https://worker.example/cors/") == 2 &&
                   CountOccurrences(html, "https://worker.example/cors/") == 2,
                "the source-link change must retain the public custom-proxy example in source and generated resources");

            var readmeSource = File.ReadAllText(Path.Combine(sourceRoot, "README.md"));
            Assert(CountOccurrences(readmeSource,
                       "https://github.com/mouxiaohao/emby-plugin-danmuplus/releases/latest") == 2 &&
                   readmeSource.Contains("[完整更新日志（UPDATE.md）](UPDATE.md)") &&
                   readmeSource.Contains("https://github.com/Shurelol/Emby.CustomCssJS"),
                "the source-link change must retain the release, update-log, and CustomCssJS documentation destinations");

            var pluginSource = File.ReadAllText(Path.GetFullPath(Path.Combine(
                sourceRoot, "Plugin.cs")));
            Assert(pluginSource.Contains("GeneratedConfigurationPageResources.PageName") &&
                   pluginSource.Contains("GeneratedConfigurationPageResources.ControllerName"),
                "PluginPageInfo registration should use generated matched identifiers");
            Assert(pluginSource.Contains("new Guid(\"cdbc5624-3ea9-4f9d-94cc-3be20585f926\")") &&
                   pluginSource.Contains("public sealed override string Name => \"Danmu\";") &&
                   pluginSource.Contains("DisplayName = \"弹幕配置\"") &&
                   pluginSource.Contains(".Configuration.configPage.html") &&
                   pluginSource.Contains(".Configuration.config.js"),
                "the heading must not change the plugin ID, plugin-list name, navigation name, or resource routes");

            var projectSource = File.ReadAllText(Path.Combine(sourceRoot, "Emby.Plugin.Danmu.csproj"));
            Assert(projectSource.Contains("LogicalName=\"$(RootNamespace).Configuration.configPage.html\"") &&
                   projectSource.Contains("<EmbeddedResource Include=\"Configuration/config.js\" />") &&
                   projectSource.Contains("PageName = &quot;danmu-$(DanmuConfigCacheToken)&quot;") &&
                   projectSource.Contains("ControllerName = &quot;danmuJs-$(DanmuConfigCacheToken)&quot;"),
                "the project should retain the assembly-derived configuration resource keys and routes");

            var configurationScript = File.ReadAllText(Path.Combine(sourceRoot, "Configuration", "config.js"));
            Assert(configurationScript.Contains("pluginUniqueId: 'cdbc5624-3ea9-4f9d-94cc-3be20585f926'") &&
                   configurationScript.Contains("ApiClient.getPluginConfiguration(TemplateConfig.pluginUniqueId)") &&
                   configurationScript.Contains("ApiClient.updatePluginConfiguration(TemplateConfig.pluginUniqueId, config)"),
                "the configuration controller should retain the existing plugin ID and configuration route");
            Assert(configurationScript.Contains("config.ToAss =") &&
                   configurationScript.Contains("config.Scrapers = scrapers") &&
                   configurationScript.Contains("config.Dandan = dandan") &&
                   configurationScript.Contains("config.Tmdb = tmdb"),
                "the configuration controller should retain the established saved-setting groups");
        }

        private static void PreservesManualKeywordEvidenceAndRouting()
        {
            var registry = new DanmuCandidateEvidenceRegistry();
            var ordinaryToken = registry.Register(
                "target", "ProviderID", "candidate", .91, "search-confidence",
                new SourceMetadata { Title = "Snapshot" });
            Assert(ordinaryToken.Length > 0 &&
                   registry.TryResolve(ordinaryToken, "target", "providerid", "CANDIDATE",
                       out var evidence) &&
                   evidence.MatchScore == .91 && evidence.ScoreOrigin == "search-confidence" &&
                   evidence.SourceMetadata?.Title == "Snapshot" &&
                   !registry.TryResolve(ordinaryToken, "other-target", "ProviderID", "candidate", out _) &&
                   !registry.TryResolve(ordinaryToken, "target", "OtherProvider", "candidate", out _) &&
                   !registry.TryResolve(ordinaryToken, "target", "ProviderID", "other-candidate", out _),
                "manual keyword results must reuse ordinary target/site/candidate-bound evidence");

            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var selectedPreviewStart = controller.IndexOf(
                "private async Task<DanmuSelectedCandidateDetailPreview> GetSelectedCandidatePreview",
                StringComparison.Ordinal);
            var selectedPreviewEnd = controller.IndexOf(
                "private async Task<DanmuSelectedCandidateDetailPreview> GetSelectedMoviePartPreview",
                selectedPreviewStart, StringComparison.Ordinal);
            var selectedPreview = controller.Substring(selectedPreviewStart,
                selectedPreviewEnd - selectedPreviewStart);
            Assert(selectedPreview.IndexOf("CandidateEvidence.TryResolve(", StringComparison.Ordinal) >= 0 &&
                   selectedPreview.IndexOf("CandidateEvidence.TryResolve(", StringComparison.Ordinal) <
                   selectedPreview.IndexOf("ResolveSelectedCandidateDetailAsync(", StringComparison.Ordinal),
                "Episode candidate detail must validate evidence before its first provider call");

            Assert(controller.Contains(
                       "string.Equals(request?.Mode, DanmuMatchIntent.ManualKeyword, StringComparison.Ordinal)",
                       StringComparison.Ordinal) &&
                   controller.Contains("GetMovieMatchPreview", StringComparison.Ordinal) &&
                   controller.Contains("GetEpisodeMatchPreview", StringComparison.Ordinal) &&
                   controller.Contains("GetSeasonMatchPreview", StringComparison.Ordinal) &&
                   controller.Contains("manualKeywordDiscovery: true", StringComparison.Ordinal) &&
                   controller.Contains("IsTemporaryRangeSearch(request)", StringComparison.Ordinal),
                "manual-keyword must use exact Mode routing across Movie, Episode-via-Season, Season/Series, and temporary ranges");
            var isManualKeyword = typeof(DanmuController).GetMethod(
                "IsManualKeyword", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(isManualKeyword != null &&
                   (bool)isManualKeyword.Invoke(null, new object[]
                   {
                       new DanmuParams { Mode = DanmuMatchIntent.ManualKeyword },
                   }) &&
                   !(bool)isManualKeyword.Invoke(null, new object[]
                   {
                       new DanmuParams { Mode = "MANUAL-KEYWORD" },
                   }) &&
                   !(bool)isManualKeyword.Invoke(null, new object[]
                   {
                       new DanmuParams { Mode = string.Concat("manual", "-raw") },
                   }),
                "only the exact case-sensitive manual-keyword Mode discriminator may activate l10 behavior, and the unpublished old value must not remain an alias");

            var shouldUseComposite = typeof(DanmuController).GetMethod(
                "ShouldUseCompositeSeasonPlanPreview", BindingFlags.Static | BindingFlags.NonPublic);
            var mixedManualRequest = new DanmuParams
            {
                Mode = DanmuMatchIntent.ManualKeyword,
                Keyword = "keyword search",
                CompositePlan = true,
                Site = "Dandan",
                CandidateId = "browser-authored-candidate",
            };
            var temporaryManualRequest = new DanmuParams
            {
                Mode = DanmuMatchIntent.ManualKeyword,
                Keyword = "range keyword",
                CompositePlan = true,
                SearchScope = "temporary-range",
            };
            Assert(shouldUseComposite != null &&
                   !(bool)shouldUseComposite.Invoke(null, new object[] { mixedManualRequest }) &&
                   (bool)shouldUseComposite.Invoke(null, new object[] { temporaryManualRequest }) &&
                   (bool)shouldUseComposite.Invoke(null, new object[]
                   {
                       new DanmuParams { CompositePlan = true },
                   }),
                "manual-keyword must ignore a mixed CompositePlan/detail intent while retaining authoritative temporary-range validation and ordinary composite behavior");

            var dispatchStart = controller.IndexOf("var targetRequests = seasons.Select", StringComparison.Ordinal);
            var dispatchEnd = controller.IndexOf("result.Seasons.AddRange", dispatchStart, StringComparison.Ordinal);
            var dispatch = controller.Substring(dispatchStart, dispatchEnd - dispatchStart);
            var manualBranchStart = dispatch.IndexOf("manualKeyword", StringComparison.Ordinal);
            var manualBranchEnd = dispatch.IndexOf(": !string.IsNullOrWhiteSpace(request.Site)",
                manualBranchStart, StringComparison.Ordinal);
            var manualBranch = dispatch.Substring(manualBranchStart, manualBranchEnd - manualBranchStart);
            Assert(manualBranch.Contains("ShouldUseCompositeSeasonPlanPreview(request)", StringComparison.Ordinal) &&
                   manualBranch.Contains("GetCompositeSeasonPlanPreview", StringComparison.Ordinal) &&
                   manualBranch.Contains("GetSeasonMatchPreview", StringComparison.Ordinal) &&
                   manualBranch.Contains("manualKeywordDiscovery: true", StringComparison.Ordinal) &&
                   !manualBranch.Contains("request.CompositePlan", StringComparison.Ordinal) &&
                   !manualBranch.Contains("GetSelectedSeasonCandidatePlanPreview", StringComparison.Ordinal),
                "a mixed manual-keyword request must continue through provider keyword discovery and must not enter browser-authored composite or selected-candidate detail");

            var applySingleSummary = typeof(DanmuController).GetMethod(
                "TryApplySingleManualKeywordSeasonSummary", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var itemType in new[] { "Season", "Series" })
            {
                foreach (var status in new[] { "incomplete", "cancelled", "invalid_request", "ambiguous" })
                {
                    var child = new DanmuSeasonMatchResult
                    {
                        MatchIntent = DanmuMatchIntent.ManualKeyword,
                        Status = status,
                        Message = itemType + "-" + status,
                        DecisionReason = "child-" + status,
                    };
                    var preview = new DanmuMatchPreviewResult
                    {
                        ItemType = itemType,
                        MatchIntent = DanmuMatchIntent.ManualKeyword,
                        Status = "top-level-placeholder",
                        Message = "top-level-placeholder",
                        CanStart = true,
                    };
                    preview.Seasons.Add(child);
                    Assert(applySingleSummary != null &&
                           (bool)applySingleSummary.Invoke(null, new object[] { preview, true }) &&
                           preview.Status == child.Status && preview.Message == child.Message &&
                           preview.DecisionReason == child.DecisionReason && !preview.CanStart,
                        "single-target manual-keyword " + itemType +
                        " must preserve the child " + status + " status at the top level");
                }
            }
            var ordinarySeries = new DanmuMatchPreviewResult
            {
                ItemType = "Series",
                Status = "ordinary-series-placeholder",
                Message = "ordinary-series-placeholder",
                CanStart = true,
            };
            ordinarySeries.Seasons.Add(new DanmuSeasonMatchResult
            {
                Status = "incomplete",
                Message = "ordinary-child",
            });
            Assert(!(bool)applySingleSummary.Invoke(null, new object[] { ordinarySeries, false }) &&
                   ordinarySeries.Status == "ordinary-series-placeholder" && ordinarySeries.CanStart,
                "ordinary whole-Series aggregation must not use the single-target manual-keyword status shortcut");

            Assert(controller.Contains("StampCandidateEvidence(evidenceTarget, result.Candidates)") &&
                   controller.Contains("TryResolveMoviePart(", StringComparison.Ordinal) &&
                   controller.Contains("BuildCompositePlanAsync(", StringComparison.Ordinal) &&
                   !controller.Contains("TryRegisterManualKeywordBatch", StringComparison.Ordinal) &&
                   !controller.Contains("StartsWith(\"mk_\"", StringComparison.Ordinal),
                "manual keyword candidates must reuse ordinary evidence before existing detail, Movie-part, and authoritative Season mapping gates");

            var candidateDetailStart = controller.IndexOf(
                "private async Task<DanmuMatchCandidateDetailResult> GetMatchCandidateDetails",
                StringComparison.Ordinal);
            var candidateDetailEnd = controller.IndexOf(
                "private static Task<ScraperMedia> ResolveMatchCandidateDetailsMediaAsync",
                candidateDetailStart, StringComparison.Ordinal);
            var candidateDetail = controller.Substring(candidateDetailStart,
                candidateDetailEnd - candidateDetailStart);
            Assert(candidateDetail.Contains("IsSafeMatchCandidateId(request?.CandidateId)") &&
                   candidateDetail.IndexOf("CandidateEvidence.TryResolve(", StringComparison.Ordinal) >= 0 &&
                   candidateDetail.IndexOf("CandidateEvidence.TryResolve(", StringComparison.Ordinal) <
                   candidateDetail.IndexOf("var media = await ResolveMatchCandidateDetailsMediaAsync(", StringComparison.Ordinal),
                "manual candidate detail must retain the ordinary ID filter and validate evidence before provider access");

            var registrySource = File.ReadAllText(Path.Combine(root, "Core", "DanmuCandidateEvidenceRegistry.cs"));
            var modelSource = File.ReadAllText(Path.Combine(root, "Model", "DanmuMatchResult.cs"));
            Assert(!registrySource.Contains("ManualKeyword", StringComparison.Ordinal) &&
                   !registrySource.Contains("\"mk_\"", StringComparison.Ordinal) &&
                   !modelSource.Contains("public List<string> Aliases", StringComparison.Ordinal),
                "manual keyword discovery must not create a separate evidence namespace or alias projection");
        }

        private static bool ContainsConfigurationHeading(string html, string heading)
        {
            return Regex.IsMatch(
                html,
                "<h2\\b[^>]*\\bclass\\s*=\\s*[\"'][^\"']*\\bsectionTitle\\b[^\"']*[\"'][^>]*>\\s*" +
                Regex.Escape(heading) +
                "\\s*</h2>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool ContainsConfigurationSourceLink(string html, string href)
        {
            return Regex.IsMatch(
                html,
                "<a\\s+id=\"test\"\\s+is=\"emby-linkbutton\"\\s+" +
                "class=\"raised button-alt headerHelpButton emby-button\"\\s+target=\"_blank\"\\s+" +
                "href=\"" + Regex.Escape(href) + "\">\\s*源码\\s*</a>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string NormalizeCacheToken(string informationalVersion)
        {
            var token = Regex.Replace(informationalVersion ?? string.Empty, "[^A-Za-z0-9_-]+", "-");
            token = Regex.Replace(token, "(^-+|-+$)", string.Empty);
            return string.IsNullOrEmpty(token) ? "build" : token;
        }

        private static void VerifiesSingleTargetSmartMatchReliabilityContracts()
        {
            var controllerSource = File.ReadAllText(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Core", "Controllers", "DanmuController.cs")));
            Assert(controllerSource.Contains("task.MatchOrigin, \"provider-id\"") &&
                   controllerSource.Contains("ResolveDirectEpisodeMediaAsync("),
                "direct Episode ProviderId retries must stay on GetMediaEpisode instead of treating the id as a Season id");

            Assert(DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(null, 12) == null &&
                   DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(1, 0) == null &&
                   !DanmuEpisodeMatchHelper.IsValidSourceEpisodeNumber(-1, 12),
                "Episode suggestions should reject missing, special, and unavailable source numbers without affecting siblings");

            var lateProvider = new TaskCompletionSource<DanmuEpisodeDownloadOutcome>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var timedOut = false;
            var timeoutOutcome = SingleTargetDownloadArbiter.AwaitAsync(
                    lateProvider.Task,
                    TimeSpan.FromMilliseconds(20),
                    CancellationToken.None,
                    onTimeout: () => timedOut = true)
                .GetAwaiter().GetResult();
            Assert(timeoutOutcome.Status == "skipped" && timedOut,
                "a single-target provider that exceeds its deadline should become skipped");
            Assert(timeoutOutcome.Message.Contains("20") && !timeoutOutcome.Message.Contains("180"),
                "timeout diagnostics should describe the configured deadline instead of a hard-coded 180 seconds");
            lateProvider.SetResult(new DanmuEpisodeDownloadOutcome { Status = "success", Message = "late" });
            Assert(timeoutOutcome.Status == "skipped",
                "a late provider completion must not overwrite the already selected timeout result");

            var providerSkipped = false;
            var duplicateSkipOutcome = SingleTargetDownloadArbiter.AwaitAsync(
                    Task.FromResult(new DanmuEpisodeDownloadOutcome { Status = "skipped", Message = "duplicate" }),
                    TimeSpan.FromSeconds(1),
                    CancellationToken.None,
                    onTimeout: () => providerSkipped = true)
                .GetAwaiter().GetResult();
            Assert(duplicateSkipOutcome.Status == "skipped" && !providerSkipped,
                "a provider-reported duplicate skip must not be reported as a timeout");

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var cancelledProvider = new TaskCompletionSource<DanmuEpisodeDownloadOutcome>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var cancelled = false;
                try
                {
                    SingleTargetDownloadArbiter.AwaitAsync(
                            cancelledProvider.Task,
                            TimeSpan.FromSeconds(1),
                            cancellation.Token)
                        .GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                Assert(cancelled,
                    "a cancelled single-target task must complete as cancelled without waiting for its provider");
            }

            using (var simultaneousCancellation = new CancellationTokenSource())
            {
                simultaneousCancellation.Cancel();
                var cancelled = false;
                try
                {
                    SingleTargetDownloadArbiter.AwaitAsync(
                            Task.FromResult(new DanmuEpisodeDownloadOutcome
                            {
                                Status = "success",
                                Message = "provider completed",
                            }),
                            TimeSpan.FromSeconds(1),
                            simultaneousCancellation.Token)
                        .GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert(cancelled,
                    "cancellation must win when the cancellation signal and provider result are both already complete");
            }
        }

        private static void PreservesStrmIndependentSeasonResolutionContract()
        {
            var controllerSource = File.ReadAllText(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Core", "Controllers", "DanmuController.cs")));
            var start = controllerSource.IndexOf(
                "private Season ResolveSeason(DanmuParams request)", StringComparison.Ordinal);
            var end = controllerSource.IndexOf(
                "private static Season SelectSeasonByContext", start, StringComparison.Ordinal);
            Assert(start >= 0 && end > start,
                "the Season context resolver must remain available to direct and fallback matching entry points");
            var resolver = controllerSource.Substring(start, end - start);
            Assert(resolver.Contains("_libraryManager.GetItemById(request.Id) as Season") &&
                   resolver.Contains("_libraryManager.GetItemById(request.SeriesId) as Series") &&
                   resolver.Contains("SelectSeasonByContext(seasons, request)") &&
                   !resolver.Contains(".Path") &&
                   !resolver.Contains("ContainingFolderPath") &&
                   !resolver.Contains("FileNameWithoutExtension"),
                "Season resolution must use Emby ItemId/Series context and remain independent of STRM or physical media paths");
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static void PreservesValidUnicodeWhileRemovingInvalidXmlScalars()
        {
            var source = "中文\t换行\n回车\remoji😀尾部" + '\u0001' + '\uFFFE' + '\uFFFF' +
                         "\uD800孤立高代理\uDC00孤立低代理";
            var sanitized = Xml10Sanitizer.SanitizeText(source);

            Assert(sanitized.Contains("中文\t换行\n回车\remoji😀尾部"),
                "valid Chinese, whitespace, and supplementary Unicode should be preserved");
            Assert(!sanitized.Contains('\u0001') && !sanitized.Contains('\uFFFE') &&
                   !sanitized.Contains('\uFFFF') && !sanitized.Contains("\uD800") &&
                   !sanitized.Contains("\uDC00"),
                "invalid XML scalars and isolated surrogates should be removed");
        }

        private static void RemovesInvalidXmlCharacterReferences()
        {
            const string source = "<root>保留&#10;&#x1F600;移除&#0;&#xB;&#65535;&#x110000;</root>";
            var sanitized = Xml10Sanitizer.SanitizeDocument(source);
            var document = new XmlDocument();
            document.LoadXml(sanitized);

            Assert(sanitized.Contains("&#10;") && sanitized.Contains("&#x1F600;"),
                "legal numeric XML character references should be preserved");
            Assert(!sanitized.Contains("&#0;") && !sanitized.Contains("&#xB;") &&
                   !sanitized.Contains("&#65535;") && !sanitized.Contains("&#x110000;"),
                "illegal numeric XML character references should be removed");
        }

        private static void PreservesCharacterReferenceTextInsideCdata()
        {
            const string source = "<root><![CDATA[字面文本 &#xFFFF; 和 &#0;]]><value>&#xFFFF;</value></root>";
            var sanitized = Xml10Sanitizer.SanitizeDocument(source);
            var document = new XmlDocument();
            document.LoadXml(sanitized);

            Assert(document.DocumentElement.FirstChild.Value == "字面文本 &#xFFFF; 和 &#0;",
                "numeric-reference-like text inside CDATA should remain literal and unchanged");
            Assert(document.DocumentElement.SelectSingleNode("value").InnerText == string.Empty,
                "an illegal numeric character reference outside CDATA should still be removed");
        }

        private static void RecoversIqiyiXmlContainingInvalidCharacters()
        {
            var xml = "<danmu><sum>1</sum><validSum>1</validSum><duration>1</duration>" +
                      "<data><entry><int>1</int><list><bulletInfo>" +
                      "<contentId>1</contentId><content>中文\n😀\uFFFF尾部&#0;</content>" +
                      "<font>1</font><color>FFFFFF</color><showTime>1</showTime>" +
                      "</bulletInfo></list></entry></data></danmu>";
            var cleaned = IqiyiApi.RemoveInvalidXmlChars(xml);
            var serializer = new XmlSerializer(typeof(IqiyiCommentDocument));
            IqiyiCommentDocument result;
            using (var reader = new StringReader(cleaned))
            {
                result = (IqiyiCommentDocument)serializer.Deserialize(reader);
            }

            Assert(result.Data[0].List[0].Content == "中文\n😀尾部",
                "Iqiyi fallback should remove invalid XML data without damaging valid comment text");
        }

        private static void RecoversBilibiliXmlContainingInvalidCharacters()
        {
            var xml = "<i><d p=\"1,1,25,16777215,0,0,user,1,1\">中文\n😀\uFFFF尾部&#0;</d></i>";
            var result = Emby.Plugin.Danmu.Scraper.Bilibili.Bilibili.ParseXml(xml);

            Assert(result.Items.Count == 1 && result.Items[0].Content == "中文\n😀尾部",
                "Bilibili XML fallback should recover a comment containing invalid XML data");
        }

        private static void SanitizesFinalXmlForEveryProviderAndAcceptsSmallValidOutput()
        {
            var providers = new[] { "BilibiliID", "IqiyiID", "TencentID", "YoukuID", "MgtvID", "DandanID" };
            foreach (var provider in providers)
            {
                var danmaku = new ScraperDanmaku
                {
                    ProviderId = provider,
                    Items = new List<ScraperDanmakuText>
                    {
                        new ScraperDanmakuText
                        {
                            Id = 1,
                            Progress = 1000,
                            MidHash = "用户\uFFFF😀",
                            Content = "中文\n😀\u0001\uFFFE\uFFFF尾部"
                        }
                    }
                };

                Assert(DanmuDownloadContent.HasUsableItems(danmaku),
                    "a single valid comment should be usable regardless of provider");
                var bytes = DanmuDownloadContent.Serialize(danmaku);
                Assert(bytes.Length < 1024,
                    "the regression fixture should remain below the removed one-kilobyte threshold");

                var document = new XmlDocument();
                document.LoadXml(Encoding.UTF8.GetString(bytes));
                var finalContent = document.DocumentElement.SelectSingleNode("d").InnerText;
                Assert(finalContent.Replace("\r\n", "\n") == "中文\n😀尾部",
                    "final XML should preserve valid text and remove invalid characters for " + provider +
                    "; actual=" + finalContent);
            }

            Assert(!DanmuDownloadContent.HasUsableItems(new ScraperDanmaku()),
                "an empty danmu result should not be treated as usable");
        }

        private static string ReadResource(System.Reflection.Assembly assembly, string name)
        {
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static DanmuMatchCandidate Candidate(string id, string site, int sourceOrder, double score)
        {
            return new DanmuMatchCandidate
            {
                Id = id,
                Name = id,
                Site = site,
                SiteName = site,
                SourceOrder = sourceOrder,
                Score = score
            };
        }

        private static void ParsesIqiyiQipsMovieTvId()
        {
            var method = typeof(IqiyiApi).GetMethod(
                "ExtractTvId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var parsed = (long)method.Invoke(null, new object[]
            {
                "qips://tvid=243967400;vid=eb299ecdb0803913ed5139ee05062de9;"
            });
            Assert(parsed == 243967400, "iQIYI qips movie URLs should expose their TvId");
        }

        private sealed class FakeScraper : AbstractScraper
        {
            private readonly string _providerId;
            private readonly List<ScraperSearchInfo> _results;
            private readonly bool _throws;
            private readonly Dictionary<string, ScraperMedia> _mediaById;
            private readonly Dictionary<string, ScraperEpisode> _episodesById;
            private readonly Dictionary<string, List<ScraperSearchInfo>> _apiResultsByKeyword;
            private readonly Dictionary<string, List<ScraperSearchInfo>> _movieResultsByKeyword;
            public int MediaCalls { get; private set; }
            public int MediaEpisodeCalls { get; private set; }
            public int SearchCalls { get; private set; }
            public List<string> ApiKeywords { get; } = new List<string>();
            public List<string> SearchNames { get; } = new List<string>();

            public FakeScraper(
                string providerId,
                List<ScraperSearchInfo> results,
                bool throws = false,
                Dictionary<string, ScraperMedia> mediaById = null,
                Dictionary<string, ScraperEpisode> episodesById = null,
                Dictionary<string, List<ScraperSearchInfo>> apiResultsByKeyword = null,
                Dictionary<string, List<ScraperSearchInfo>> movieResultsByKeyword = null)
                : base(null)
            {
                _providerId = providerId;
                _results = results;
                _throws = throws;
                _mediaById = mediaById ?? new Dictionary<string, ScraperMedia>(StringComparer.OrdinalIgnoreCase);
                _episodesById = episodesById ?? new Dictionary<string, ScraperEpisode>(StringComparer.OrdinalIgnoreCase);
                _apiResultsByKeyword = apiResultsByKeyword ??
                    new Dictionary<string, List<ScraperSearchInfo>>(StringComparer.OrdinalIgnoreCase);
                _movieResultsByKeyword = movieResultsByKeyword;
            }

            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;

            public override Task<List<ScraperSearchInfo>> Search(BaseItem item)
            {
                SearchCalls++;
                SearchNames.Add(item?.Name ?? string.Empty);
                if (_throws) throw new InvalidOperationException("provider failed");
                if (_movieResultsByKeyword != null)
                {
                    _movieResultsByKeyword.TryGetValue(item?.Name ?? string.Empty, out var keywordResults);
                    return Task.FromResult(keywordResults ?? new List<ScraperSearchInfo>());
                }
                return Task.FromResult(_results ?? new List<ScraperSearchInfo>());
            }

            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                ApiKeywords.Add(keyword);
                if (_throws) throw new InvalidOperationException("provider failed");
                _apiResultsByKeyword.TryGetValue(keyword ?? string.Empty, out var results);
                return Task.FromResult(results ?? new List<ScraperSearchInfo>());
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id)
            {
                MediaCalls++;
                _mediaById.TryGetValue(id ?? string.Empty, out var media);
                return Task.FromResult(media);
            }
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
            {
                MediaEpisodeCalls++;
                _episodesById.TryGetValue(id ?? string.Empty, out var episode);
                return Task.FromResult(episode);
            }
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
