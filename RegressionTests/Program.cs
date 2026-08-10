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
using MediaBrowser.Model.Logging;
using System.Threading;
using System.Threading.Tasks;
using BilibiliMedia = Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class Program
    {
        private static int Main()
        {
            MapsAnimeSeason();
            MapsLiveActionSeasonAndCleansTitle();
            UsesIdentifierFallbackOrder();
            OmitsMalformedRecords();
            OrdersAndSelectsCrossProviderTies();
            PreservesSameSiteHighestScoreTieAmbiguity();
            SelectsCloseHighConfidenceCandidatesBySitePriority();
            ScoresMoviesAndFiltersTelevisionCandidates();
            IsolatesMovieProviderFailures();
            ResolvesMovieProviderLookupIdentifiers();
            PreservesSavedManualBindingsUntilForcedSearch();
            AppliesDuplicateAndForceRefreshPolicy();
            PreservesSingleEpisodeIsolationAndLegacyTaskShape();
            MapsEpisodeSourceNumbersSafely();
            DeserializesAndNormalizesBilibiliEpisodes();
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

            Assert(candidates[0].Id == "priority", "configured provider priority should order exact-score ties");
            Assert(DanmuMatchScorer.CanAutoSelect(candidates), "a unique candidate on the highest-priority tied provider should auto-bind");
            Assert(!DanmuMatchScorer.CanAutoSelect(candidates, false), "a priority tie should not stop parent-title search before fallback rounds");

            var unequal = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("higher-score", "LaterSite", 5, 0.91),
                Candidate("higher-priority", "PrioritySite", 0, 0.90)
            });
            Assert(unequal[0].Id == "higher-score", "provider priority must not outrank a higher score");
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
            Assert(preferredRunnerUp[0].Id == "full" &&
                   DanmuMatchScorer.SelectAutoCandidate(preferredRunnerUp)?.Id == "preferred",
                "display order must stay score-descending while the earlier site's 0.98 candidate is selected");

            var outsideGap = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("full", "FullSite", 1, 1.0),
                Candidate("outside", "PrioritySite", 0, 0.969)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(outsideGap)?.Id == "full",
                "a candidate more than 0.03 below the top score must stay outside the close pool");

            var belowPoolFloor = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("higher", "LaterSite", 1, 0.949),
                Candidate("lower", "PrioritySite", 0, 0.94)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(belowPoolFloor) == null,
                "candidates below 0.95 must remain ambiguous even when their scores are close");

            var floorBoundary = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("floor", "LaterSite", 1, 0.95),
                Candidate("below-floor", "PrioritySite", 0, 0.92)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(floorBoundary)?.Id == "floor",
                "the 0.95 pool floor must exclude a 0.92 candidate even at the 0.03 gap boundary");

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
            Assert(preferredThirdScore.Select(x => x.Id).SequenceEqual(new[] { "full", "second", "preferred" }) &&
                   DanmuMatchScorer.SelectAutoCandidate(preferredThirdScore)?.Id == "preferred",
                "the sole pooled candidate from the earliest site may win without changing score order");

            Assert(DanmuMatchScorer.SelectAutoCandidate(preferredRunnerUp, false) == null,
                "intermediate search must not resolve a multi-candidate close pool by site priority");
            Assert(DanmuMatchScorer.SelectAutoCandidate(outsideGap, false)?.Id == "full",
                "intermediate search may stop when only one candidate is in the close pool");

            var exactTie = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("late-tie", "LaterSite", 2, 0.98),
                Candidate("priority-tie", "PrioritySite", 0, 0.98)
            });
            Assert(DanmuMatchScorer.SelectAutoCandidate(exactTie)?.Id == "priority-tie" &&
                   DanmuMatchScorer.SelectAutoCandidate(exactTie, false) == null,
                "exact ties must use the same final-only site-priority rule");

            Assert(DanmuMatchScorer.SelectAutoCandidate(new List<DanmuMatchCandidate>
                {
                    Candidate("below-floor", "Site", 0, 0.77)
                }) == null,
                "the existing top-score floor must remain unchanged");
            Assert(DanmuMatchScorer.SelectAutoCandidate(new List<DanmuMatchCandidate>
                {
                    Candidate("strong", "SiteA", 0, 0.78),
                    Candidate("weak-runner-up", "SiteB", 1, 0.64)
                })?.Id == "strong",
                "the existing runner-up floor must remain unchanged");
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

            Assert(search.Candidates.Count == 1, "successful movie providers should still contribute candidates");
            Assert(search.SearchErrors.Count == 1 && search.SearchErrors[0].Contains("DandanID"),
                "a failed Dandan proxy provider should be isolated in diagnostics");
        }

        private static void ResolvesMovieProviderLookupIdentifiers()
        {
            var media = new ScraperMedia
            {
                Id = "season-or-album-id",
                CommentId = "bilibili-ep-id",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { CommentId = "fallback-episode-id" },
                },
            };
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("BilibiliID", media) == "bilibili-ep-id",
                "Bilibili movies should resolve through their ep id");
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("IqiyiID", media) == "season-or-album-id",
                "non-Bilibili movies should resolve through their provider media id");
            media.CommentId = string.Empty;
            Assert(DanmuMovieMatchHelper.ResolveEpisodeLookupId("BilibiliID", media) == "fallback-episode-id",
                "Bilibili movie lookup should fall back to the first episode comment id");
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

            var pluginSource = File.ReadAllText(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Plugin.cs")));
            Assert(pluginSource.Contains("GeneratedConfigurationPageResources.PageName") &&
                   pluginSource.Contains("GeneratedConfigurationPageResources.ControllerName"),
                "PluginPageInfo registration should use generated matched identifiers");
        }

        private static string NormalizeCacheToken(string informationalVersion)
        {
            var token = Regex.Replace(informationalVersion ?? string.Empty, "[^A-Za-z0-9_-]+", "-");
            token = Regex.Replace(token, "(^-+|-+$)", string.Empty);
            return string.IsNullOrEmpty(token) ? "build" : token;
        }

        private static void VerifiesSingleTargetSmartMatchReliabilityContracts()
        {
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

            public FakeScraper(string providerId, List<ScraperSearchInfo> results, bool throws = false)
                : base(null)
            {
                _providerId = providerId;
                _results = results;
                _throws = throws;
            }

            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;

            public override Task<List<ScraperSearchInfo>> Search(BaseItem item)
            {
                if (_throws) throw new InvalidOperationException("provider failed");
                return Task.FromResult(_results ?? new List<ScraperSearchInfo>());
            }

            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
