using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Core.Controllers;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Tmdb;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class TmdbAliasTests
    {
        private const string AscendanceOfABookworm =
            "\u5c0f\u4e66\u75f4\u7684\u4e0b\u514b\u4e0a";

        public static void Run()
        {
            ParsesTvResultsIntoChineseAliasSearchPlan();
            ParsesMovieTitlesIntoChineseAliasSearchPlan();
            RecognizesChineseAnimationGenre();
            RecognizesAdditionalAnimationGenres();
            DoesNotTreatNonLatinTitleAsEnglish();
            OrdersChineseAliasesAfterContainmentFiltering();
            KeepsFewerThanThreeEligibleAliasesWithoutPadding();
            UsesTextEvidenceBeforeTmdbRegion();
            KeepsPrimaryTitleFallbacksOutOfAlternativeTitles();
            ExercisesTmdbClientFallbackCachingAndCancellation();
            PreservesOrdinaryScoresForContradictoryEvidence();
            ScoresSeasonEvidenceWithExactIndependentChannels();
            ScoresContinuousSeasonAndParentTitleEvidence();
            ScoresLiveOnePunchManThirdSeasonAliasFallback();
            ScoresBookwormFourthSeasonDespiteAuthoritativeCountDifference();
            ScoresOnlyValidatedShortParentAliasExtensions();
            OrdersJojoSplitSeasonTitlesWithoutNumericLeakage();
            ReplacesCanonicalCandidatesForAutomaticSelection();
            PreservesLazyRoundOrderingAndShortCircuitContract();
            TracksCompletedAliasProviderCalls();
            KeepsExhaustedSeasonAliasesServerLocal();
            SearchesParentTitleOnceWithOrdinarySeasonScoring();
            FreezesParentTitleRematchRequestBoundary();
        }

        private static void ParsesTvResultsIntoChineseAliasSearchPlan()
        {
            var response = Deserialize(
                "{\"id\":91768,\"results\":[{\"iso_3166_1\":\"CN\",\"title\":\"" +
                AscendanceOfABookworm + "\"}]}");
            var aliases = TmdbAliasClient.Normalize(
                TmdbAliasClient.SelectAlternativeTitles(response, false));

            Assert(aliases.BuildSearchPlan().Any(alias =>
                    alias.Language == TmdbAliasLanguage.Chinese &&
                    alias.Title == AscendanceOfABookworm),
                "TMDB TV alternative_titles must read top-level results and schedule the Chinese alias for search");
        }

        private static void ParsesMovieTitlesIntoChineseAliasSearchPlan()
        {
            var response = Deserialize(
                "{\"id\":123,\"titles\":[{\"iso_3166_1\":\"CN\",\"title\":\"" +
                AscendanceOfABookworm + "\"}]}");
            var aliases = TmdbAliasClient.Normalize(
                TmdbAliasClient.SelectAlternativeTitles(response, true));

            Assert(aliases.BuildSearchPlan().Any(alias =>
                    alias.Language == TmdbAliasLanguage.Chinese &&
                    alias.Title == AscendanceOfABookworm),
                "TMDB Movie alternative_titles must retain top-level titles in the Chinese alias search plan");
        }

        private static void RecognizesChineseAnimationGenre()
        {
            var series = new Series { Genres = new[] { "\u52a8\u753b" } };

            Assert(TmdbAliasClient.IsAnimated(series),
                "a Series tagged with the Chinese genre \u52a8\u753b must enable TMDB alias lookup");
        }

        private static void RecognizesAdditionalAnimationGenres()
        {
            Assert(TmdbAliasClient.IsAnimated(new Series { Genres = new[] { "动漫" } }) &&
                   TmdbAliasClient.IsAnimated(new Series { Genres = new[] { "アニメ" } }),
                "common Chinese and Japanese animation genre labels must enable TMDB alias lookup");
        }

        private static void DoesNotTreatNonLatinTitleAsEnglish()
        {
            var response = Deserialize(
                "{\"id\":91768,\"results\":[{\"iso_3166_1\":\"RU\",\"title\":\"Власть книжного червя\"}]}");
            var aliases = TmdbAliasClient.Normalize(
                TmdbAliasClient.SelectAlternativeTitles(response, false));

            Assert(aliases.English.Count == 0,
                "a non-Latin title must not consume the single English alias fallback slot");
        }

        private static void OrdersChineseAliasesAfterContainmentFiltering()
        {
            var libraryTitle = "爱书的下克上：为了成为图书管理员不择手段！";
            var aliases = TmdbAliasClient.Normalize(new[]
            {
                new TmdbAlternativeTitle { Country = "CN", Title = "爱书的下克上" },
                new TmdbAlternativeTitle { Country = "TW", Title = "小書痴：為了成為圖書管理員" },
                new TmdbAlternativeTitle { Country = "CN", Title = "小书痴的下克上：为了成为图书管理员不择手段" },
                new TmdbAlternativeTitle { Country = "CN", Title = AscendanceOfABookworm },
                new TmdbAlternativeTitle { Country = "CN", Title = "小書痴" },
                new TmdbAlternativeTitle { Country = "CN", Title = AscendanceOfABookworm },
                new TmdbAlternativeTitle { Country = "TW", Title = "小書痴～圖書管理員" },
            });

            var plan = aliases.BuildSearchPlan(libraryTitle).Select(x => x.Title).ToList();
            Assert(plan.SequenceEqual(new[] {
                AscendanceOfABookworm,
                "小书痴的下克上：为了成为图书管理员不择手段",
                "小書痴" }),
                "containment exclusion, de-duplication, four-tier ordering, separator handling, and the three-term cap must be deterministic");
            Assert(!plan.Contains("爱书的下克上") && plan.Count == 3,
                "a contained alias must not consume a Chinese attempt before the bounded cap");
            Assert(TmdbAliasClient.GetChineseTier(aliases.Chinese.Single(x => x.Title == "小書痴")) == 3 &&
                   TmdbAliasClient.GetChineseTier(aliases.Chinese.Single(x => x.Title == "小書痴：為了成為圖書管理員")) == 4,
                "Traditional short and subtitle variants must retain tiers three and four");
        }

        private static void UsesTextEvidenceBeforeTmdbRegion()
        {
            var aliases = TmdbAliasClient.Normalize(new[]
            {
                new TmdbAlternativeTitle { Country = "CN", Title = "小書痴" },
                new TmdbAlternativeTitle { Country = "RU", Title = "中性标题" },
            });

            Assert(TmdbAliasClient.GetChineseTier(aliases.Chinese[0]) == 3 &&
                   TmdbAliasClient.GetChineseTier(aliases.Chinese[1]) == 1,
                "Traditional-only text must override a CN region, while mixed or neutral Chinese text falls back to Simplified deterministically");
        }

        private static void KeepsFewerThanThreeEligibleAliasesWithoutPadding()
        {
            var plan = TmdbAliasClient.Normalize(new[]
            {
                new TmdbAlternativeTitle { Country = "CN", Title = "短标题" },
                new TmdbAlternativeTitle { Country = "TW", Title = "短標題" },
            }).BuildSearchPlan("完全不同的库标题").Select(alias => alias.Title).ToList();
            Assert(plan.Count == 2 && plan.SequenceEqual(new[] { "短标题", "短標題" }),
                "a two-alias response must run only those two Chinese rounds before primary-title fallback");
        }

        private static void KeepsPrimaryTitleFallbacksOutOfAlternativeTitles()
        {
            var aliases = TmdbAliasClient.Normalize(new[]
            {
                new TmdbAlternativeTitle { Country = "US", Title = "Ascendance of a Bookworm" },
                new TmdbAlternativeTitle { Country = "JP", Title = "本好きの下剋上" },
            });
            var englishDetails = new TmdbMediaDetails
            {
                Name = "Ascendance of a Bookworm",
                OriginalName = "本好きの下剋上",
                OriginalLanguage = "ja",
            };
            var japaneseDetails = new TmdbMediaDetails { Name = "本好きの下剋上" };

            Assert(!aliases.BuildSearchPlan().Any() && aliases.English.Count == 0 && aliases.Japanese.Count == 0,
                "English and Japanese alternative_titles must never enter the fallback search plan");
            Assert(TmdbAliasClient.GetLocalizedPrimaryTitle(englishDetails, false) == "Ascendance of a Bookworm" &&
                   TmdbAliasClient.GetJapaneseOriginalPrimaryTitle(englishDetails, false) == "本好きの下剋上" &&
                   TmdbAliasClient.GetLocalizedPrimaryTitle(japaneseDetails, false) == "本好きの下剋上",
                "fallback rounds must use en-US primary first, then Japanese original or ja-JP primary data");
            Assert(TmdbAliasClient.UserAgent == "DanmuPlus/2.0.7",
                "TMDB requests must identify the 2.0.7 build");
        }

        private static void ExercisesTmdbClientFallbackCachingAndCancellation()
        {
            var requests = new List<HttpRequestOptions>();
            var originalSender = TmdbAliasClient.HttpGetResponseAsync;
            try
            {
                TmdbAliasClient.HttpGetResponseAsync = request =>
                {
                    requests.Add(request);
                    if (request.CancellationToken.IsCancellationRequested)
                    {
                        return Task.FromCanceled<HttpResponseInfo>(request.CancellationToken);
                    }

                    if (request.RequestHeaders != null && request.RequestHeaders.ContainsKey("Authorization"))
                    {
                        return Task.FromResult(new HttpResponseInfo { StatusCode = HttpStatusCode.Unauthorized });
                    }

                    if (request.Url.Contains("998879") && request.Url.Contains("alternative_titles"))
                    {
                        return Task.FromResult(new HttpResponseInfo { StatusCode = HttpStatusCode.BadGateway });
                    }

                    var json = request.Url.Contains("alternative_titles")
                        ? "{\"results\":[{\"iso_3166_1\":\"CN\",\"title\":\"小书痴的下克上\"}]}"
                        : "{\"name\":\"Ascendance of a Bookworm\",\"original_name\":\"本好きの下剋上\",\"original_language\":\"ja\"}";
                    return Task.FromResult(new HttpResponseInfo
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(json)),
                    });
                };

                var option = new TmdbOption
                {
                    UseAliasSearch = true,
                    ReadAccessToken = "bearer-secret",
                    ApiKey = "api-key-secret",
                };
                var series = CreateTmdbSeries("998877");
                var aliases = TmdbAliasClient.GetAliasesAsync(series, option, null).GetAwaiter().GetResult();
                var details = TmdbAliasClient.GetDetailsAsync(series, option, "en-US", null).GetAwaiter().GetResult();
                var cached = TmdbAliasClient.GetDetailsAsync(series, option, "en-US", null).GetAwaiter().GetResult();

                Assert(aliases.Chinese.Count == 1 && details.Name == "Ascendance of a Bookworm" &&
                       ReferenceEquals(details, cached) && requests.Count == 4,
                    "each endpoint must fall back from Bearer to API key once, while successful localized details are cached");
#pragma warning disable CS0618 // The regression intentionally verifies the Emby 4.9 LogUrl fallback.
                Assert(requests.All(request => !request.LogUrl.Contains("bearer-secret") &&
                                               !request.LogUrl.Contains("api-key-secret")) &&
                       requests.Any(request => request.Url.Contains("api_key=")),
                    "TMDB request logs must sanitize credentials while retaining API-key fallback");
#pragma warning restore CS0618

                var failedAliasItem = CreateTmdbSeries("998879");
                var failedAliases = TmdbAliasClient.GetAliasesAsync(failedAliasItem, option, null)
                    .GetAwaiter().GetResult();
                var fallbackDetails = TmdbAliasClient.GetDetailsAsync(
                    failedAliasItem, option, "en-US", null).GetAwaiter().GetResult();
                var failedRequestCount = requests.Count(request => request.Url.Contains("998879") &&
                    request.Url.Contains("alternative_titles"));
                TmdbAliasClient.GetAliasesAsync(failedAliasItem, option, null).GetAwaiter().GetResult();
                Assert(failedAliases == null && fallbackDetails?.Name == "Ascendance of a Bookworm" &&
                       failedRequestCount == 2 && requests.Count(request => request.Url.Contains("998879") &&
                           request.Url.Contains("alternative_titles")) == 4,
                    "an alternative-title endpoint failure must not block primary details and must not be long-cached");

                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    var cancelled = TmdbAliasClient.GetDetailsAsync(
                        CreateTmdbSeries("998878"), option, "en-US", null, cancellation.Token)
                        .GetAwaiter().GetResult();
                    Assert(cancelled == null,
                        "a cancelled detail endpoint must fail in isolation without returning cached or partial data");
                }
            }
            finally
            {
                TmdbAliasClient.HttpGetResponseAsync = originalSender;
            }
        }

        private static Series CreateTmdbSeries(string id)
        {
            return new Series
            {
                Genres = new[] { "动画" },
                ProviderIds = new ProviderIdDictionary { ["Tmdb"] = id },
            };
        }

        private static void PreservesOrdinaryScoresForContradictoryEvidence()
        {
            Assert(DanmuMatchScorer.ParseExplicitSeasonNumber("OVA S00 / Season 1") == null,
                "mixed Season 0 and positive markers must remain ambiguous instead of creating contradiction evidence");
            var aboveThreshold = ScoreSeason(
                "One Punch Man Season 1", 2015, 12, "Season 3", 2025, true);
            var belowThreshold = ScoreSeason(
                "One Punch Man Season 1", 2015, 6, "Season 3", 2025, true);
            var manualAbove = ScoreSeason(
                "One Punch Man Season 1", 2015, 12, "Season 3", 2025, false);
            var manualBelow = ScoreSeason(
                "One Punch Man Season 1", 2015, 6, "Season 3", 2025, false);

            Assert(aboveThreshold.Score == 0.60 && belowThreshold.Score == 0.60 &&
                   aboveThreshold.Score == OrdinaryNoKeywordScore(aboveThreshold) &&
                   belowThreshold.Score == OrdinaryNoKeywordScore(belowThreshold),
                "conflicting Season/year evidence must receive only the 60-point parent evidence: above=" +
                aboveThreshold.Score + ", below=" + belowThreshold.Score);
            Assert(aboveThreshold.YearScore == 0 && belowThreshold.YearScore == 0 &&
                   aboveThreshold.EpisodeScore == 0 && belowThreshold.EpisodeScore == 0 &&
                   aboveThreshold.Name.Contains("Season 1") && aboveThreshold.Year == 2015 &&
                   aboveThreshold.Reason.Contains("父剧名出现") &&
                   !aboveThreshold.Reason.Contains("集数吻合"),
                "Episode metadata must remain visible but neutral in both score fields and positive reasons");
            Assert(manualAbove.Score == aboveThreshold.Score && manualBelow.Score == belowThreshold.Score &&
                   manualAbove.Reason == aboveThreshold.Reason && manualBelow.Reason == belowThreshold.Reason,
                "the legacy manual flag must remain score- and reason-neutral after removing the automatic cap");
            Assert(DanmuMatchScorer.AutomaticConfidenceThreshold == 0.90 &&
                   DanmuMatchScorer.SelectAutoCandidate(new[] { aboveThreshold }) == null,
                "removing the contradiction cap must not lower the standard automatic threshold");
            Assert(DanmuMatchScorer.ParseExplicitSeasonNumber("Episode 3 Part 2 Cour 1") == null &&
                   DanmuMatchScorer.ParseExplicitSeasonNumber("Season 0") == null &&
                   DanmuMatchScorer.ParseExplicitSeasonNumber(new[] { "Season 2", "第3季" }) == null,
                "bare, Episode, Part/Cour, Season 0, and ambiguous markers must not become contradictory Season evidence");
        }

        private static DanmuMatchCandidate ScoreSeason(
            string title,
            int year,
            int episodes,
            string targetSeason,
            int targetYear,
            bool applyCap)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = title,
                Name = title,
                Category = "动漫",
                Year = year,
                EpisodeSize = episodes,
            }, "dandan", "DandanPlay", 0, "One Punch Man", targetSeason, targetYear, 12,
                null, null, applyCap, DanmuMatchScorer.ParseExplicitSeasonNumber(targetSeason));
        }

        private static double OrdinaryNoKeywordScore(DanmuMatchCandidate candidate)
        {
            return Math.Round(
                candidate.ParentTitleScore * 0.60 + candidate.KeywordScore * 0.20 +
                candidate.YearScore * 0.20,
                4,
                MidpointRounding.AwayFromZero);
        }

        private static void ScoresSeasonEvidenceWithExactIndependentChannels()
        {
            var correct = ScoreOnePunchMan("correct", "一拳超人 第三季", 2025, 12, null);
            var bare = ScoreOnePunchMan("bare", "一拳超人", 2015, 12, null);
            var second = ScoreOnePunchMan("second", "一拳超人 第二季", 2019, 12, null);
            var youkuSecond = ScoreOnePunchMan("youku", "一拳超人 第二季", 2019, 13, null);
            var extraThirdKeyword = ScoreOnePunchMan(
                "extra-third", "一拳超人 胆小鬼 第三季", 2025, 12, null);
            Assert(correct.Score == 1 && bare.Score == 0.60 && second.Score == 0.60 &&
                   youkuSecond.Score == 0.60,
                "Season weights must be parent 60, whole-remainder Season 20, exact year 20, and episode count 0");
            Assert(extraThirdKeyword.KeywordScore == 0 && extraThirdKeyword.Score == 0.80,
                "a pure generic target must compare as empty against additional descriptive remainder text");

            var splitChannels = ScoreOnePunchMan("split", "一拳超人", 2025, 12, "第三季");
            Assert(splitChannels.Score == 0.80 && splitChannels.ParentTitleScore == 1 &&
                   splitChannels.KeywordScore == 0,
                "parent evidence from Name and Season evidence from SourceMetadata.Title must not be combined across source-title channels");

            var yearOffByOne = ScoreOnePunchMan("year", "一拳超人 第三季", 2024, 12, null);
            var episodesOffByOne = ScoreOnePunchMan("episodes", "一拳超人 第三季", 2025, 13, null);
            var missingMetadata = ScoreOnePunchMan("missing", "一拳超人 第三季", 0, 0, null);
            Assert(correct.YearScore == 1 && correct.EpisodeScore == 0 &&
                   yearOffByOne.YearScore == 0 && episodesOffByOne.EpisodeScore == 0 &&
                   missingMetadata.YearScore == 0 && missingMetadata.EpisodeScore == 0 &&
                   yearOffByOne.Score == 0.80 && episodesOffByOne.Score == 1 &&
                   missingMetadata.Score == 0.80 && episodesOffByOne.EpisodeSize == 13 &&
                   DanmuMatchScorer.SelectAutoCandidate(new[] { episodesOffByOne })?.Id == "episodes" &&
                   !episodesOffByOne.Reason.Contains("集数吻合"),
                "exact year must contribute 20 points while Episode metadata remains visible and cannot reduce an otherwise automatic Season match");

            var movie = DanmuMatchScorer.ScoreMovie(new ScraperSearchInfo
            {
                Id = "movie-unchanged",
                Name = "一拳超人",
                Category = "电影",
                Year = 2025,
            }, "dandan", "DandanPlay", 0, "一拳超人", 2025);
            Assert(movie.Score == 1 && movie.TitleScore == 1 && movie.YearScore == 1,
                "the Season-only 60/20/20/0 refinement must not change Movie scoring");

            var firstSeasonBare = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "first",
                Name = "一拳超人",
                Category = "动漫",
                Year = 2015,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, "一拳超人", "第一季", 2015, 12,
                null, null, true, 1);
            Assert(firstSeasonBare.Score == 1 && firstSeasonBare.KeywordScore == 1,
                "only Season 1 may match an empty source remainder to an empty Season variant");

            var ordinaryLocalParentAlias = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "ordinary-local-parent",
                Name = "One Punch Man Season 3",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, "一拳超人", "第三季", 2025, 12,
                new[] { "One Punch Man" }, null, true, 3);
            Assert(ordinaryLocalParentAlias.ParentTitleScore == 0,
                "ordinary Season scoring must not award parent points from Series OriginalTitle or local parent aliases");

            var wrongNumberWithSimilarArc = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "wrong-number-similar-arc",
                Name = "一拳超人 第二季 胆小鬼篇",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, "一拳超人", "一拳超人 胆小鬼篇", 2025, 12,
                null, null, true, 3);
            Assert(wrongNumberWithSimilarArc.ParentTitleScore == 1 &&
                   wrongNumberWithSimilarArc.KeywordScore == 0 &&
                   wrongNumberWithSimilarArc.Score == 0.80,
                "an explicit source Season number conflicting with expected S3 must force zero Season score even when a numberless arc title is similar");

            var targetVariantBaseline = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "target-variant-baseline",
                Name = "一拳超人 胆小鬼篇",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, "一拳超人", "一拳超人 完全不同篇", 2025, 12,
                null, null, true, 3);
            var wrongTargetVariant = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "wrong-target-variant",
                Name = "一拳超人 胆小鬼篇",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, "一拳超人", "一拳超人 完全不同篇", 2025, 12,
                null, new[] { "一拳超人 第二季 胆小鬼篇" }, true, 3);
            Assert(wrongTargetVariant.KeywordScore == targetVariantBaseline.KeywordScore &&
                   wrongTargetVariant.Score == targetVariantBaseline.Score,
                "a target Season-title variant with an explicit number conflicting with expected S3 must be excluded from similarity scoring");
        }

        private static void ScoresContinuousSeasonAndParentTitleEvidence()
        {
            const string tangParent = "唐朝诡事录";
            var prefixInsertion = ScoreNamedSeason(
                "prefix-insertion", tangParent + "之西行", tangParent, "第2季：西行", 2);
            var exact = ScoreNamedSeason(
                "exact", tangParent + "西行", tangParent, "第2季：西行", 2);
            var substitution = ScoreNamedSeason(
                "substitution", tangParent + "东行", tangParent, "第2季：西行", 2);
            var reordered = ScoreNamedSeason(
                "reordered", tangParent + "行西", tangParent, "第2季：西行", 2);
            var disjoint = ScoreNamedSeason(
                "disjoint", tangParent + "北斗", tangParent, "第2季：西行", 2);
            var pureMarker = ScoreNamedSeason(
                "pure-marker", tangParent + "第2季", tangParent, "第2季：西行", 2);
            var wrongMarker = ScoreNamedSeason(
                "wrong-marker", tangParent + "第3季：西行", tangParent, "第2季：西行", 2);

            Assert(prefixInsertion.ParentTitleScore == 1 &&
                   prefixInsertion.KeywordScore == 0.6667 &&
                   prefixInsertion.MatchScore == 0.9333,
                "西行 / 之西行 must receive proportional 2/3 Season evidence without deleting 之");
            Assert(exact.KeywordScore == 1 && exact.MatchScore == 1 &&
                   substitution.KeywordScore == 0.5 && substitution.MatchScore == 0.9 &&
                   DanmuMatchScorer.SelectAutoCandidate(new[] { substitution })?.Id == "substitution",
                "exact named Seasons must retain 20 points and a unique 90-point substitution must remain auto-selectable");
            Assert(reordered.KeywordScore == 0 && disjoint.KeywordScore == 0 &&
                   reordered.MatchScore == 0.8 && disjoint.MatchScore == 0.8,
                "reordered and disjoint two-character Season names must receive zero edit similarity");
            Assert(pureMarker.KeywordScore == 0,
                "a pure-marker/named pair must receive zero Season evidence, actual=" + pureMarker.KeywordScore);
            Assert(wrongMarker.KeywordScore == 0,
                "a conflicting explicit Season marker must receive zero Season evidence, actual=" + wrongMarker.KeywordScore);

            var correctMarkersDifferentNames = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "correct-markers-different-names",
                Name = tangParent + " Season 2 东行",
                Category = "动漫",
                Year = 2024,
            }, "dandan", "DandanPlay", 0, tangParent, "第2季：西行", 2024, 0,
                null, null, false, 2);
            Assert(correctMarkersDifferentNames.KeywordScore == 0.5 &&
                   correctMarkersDifferentNames.MatchScore == 0.9,
                "shared correct marker text must be removed before comparing 西行 / 东行");

            var firstSeasonBare = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "first-season-empty",
                Name = tangParent,
                Category = "动漫",
                Year = 2024,
            }, "dandan", "DandanPlay", 0, tangParent, "第一季", 2024, 0,
                null, null, false, 1);
            Assert(firstSeasonBare.KeywordScore == 1 && firstSeasonBare.MatchScore == 1,
                "the established Season 1 empty/empty residual exception must remain full credit");

            const string localBookworm = "爱书的下克上：为了成为图书管理员不择手段！";
            const string sourceBookworm = "小书痴的下克上 〜为了成为图书管理员而不择手段〜";
            var bookworm = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "bookworm-continuous-parent",
                Name = sourceBookworm,
                Category = "动漫",
            }, "dandan", "DandanPlay", 0, localBookworm, "完全不同", 0, 0);
            var emptyParent = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "empty-parent",
                Name = sourceBookworm,
                Category = "动漫",
            }, "dandan", "DandanPlay", 0, string.Empty, "完全不同", 0, 0);
            var emptyParentExactSeason = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "empty-parent-exact-season",
                Name = "西行",
                Category = "动漫",
                Year = 2024,
            }, "dandan", "DandanPlay", 0, string.Empty, "西行", 2024, 0);
            var disjointParent = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "disjoint-parent",
                Name = "北斗神拳",
                Category = "动漫",
            }, "dandan", "DandanPlay", 0, "唐朝诡事录", "完全不同", 0, 0);
            Assert(DanmuMatchScorer.Normalize(localBookworm).Length == 19 &&
                   DanmuMatchScorer.Normalize(sourceBookworm).Length == 21 &&
                   bookworm.ParentTitleScore == 0.8571,
                "the normalized Bookworm parent pair must have distance 3/max length 21 and score 0.8571");
            Assert(emptyParent.ParentTitleScore == 0 && disjointParent.ParentTitleScore == 0,
                "empty and disjoint parent-title endpoints must remain zero");
            Assert(emptyParentExactSeason.ParentTitleScore == 0 &&
                   emptyParentExactSeason.KeywordScore == 1 &&
                   emptyParentExactSeason.MatchScore == 0.4,
                "an empty parent set must not discard the source channel's existing Season evidence");

            var movie = DanmuMatchScorer.ScoreMovie(new ScraperSearchInfo
            {
                Id = "movie-metric-isolation",
                Name = "行西",
                Category = "电影",
                Year = 2024,
            }, "dandan", "DandanPlay", 0, "西行", 2024);
            Assert(movie.TitleScore == 0 && movie.MatchScore == 0.18,
                "Movie scoring must retain the existing short-string Jaro-Winkler result and 82/18 weights");
        }

        private static DanmuMatchCandidate ScoreNamedSeason(
            string id,
            string sourceTitle,
            string parentTitle,
            string seasonTitle,
            int expectedSeasonNumber)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = id,
                Name = sourceTitle,
                Category = "动漫",
                Year = 2024,
            }, "dandan", "DandanPlay", 0, parentTitle, seasonTitle, 2024, 0,
                null, null, false, expectedSeasonNumber);
        }

        private static DanmuMatchCandidate ScoreOnePunchMan(
            string id,
            string name,
            int year,
            int episodes,
            string metadataTitle)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = id,
                Name = name,
                Category = "动漫",
                Year = year,
                EpisodeSize = episodes,
                SourceMetadata = metadataTitle == null
                    ? null
                    : new SourceMetadata { Title = metadataTitle },
            }, "dandan", "DandanPlay", 0, "一拳超人", "第三季", 2025, 12,
                null, null, true, 3);
        }

        private static void ScoresLiveOnePunchManThirdSeasonAliasFallback()
        {
            const string season = "第 3 季";
            const string englishTerm = "One-Punch Man";
            const string japaneseTerm = "ワンパンマン";
            Assert(DanmuMatchScorer.Normalize("一拳超人 第3季") ==
                   DanmuMatchScorer.Normalize("一拳超人 第三季"),
                "Arabic and Chinese explicit Season numerals must normalize to the same title form");

            var thirdSeason = ScoreLiveOnePunchManCandidate(
                "17576", "一拳超人 第三季", 2025, englishTerm, season);
            var firstSeason = ScoreLiveOnePunchManCandidate(
                "11123", "一拳超人", 2015, englishTerm, season);
            var secondSeason = ScoreLiveOnePunchManCandidate(
                "12430", "一拳超人 第二季", 2019, englishTerm, season);
            var japaneseThirdSeason = ScoreLiveOnePunchManCandidate(
                "17576", "一拳超人 第三季", 2025, japaneseTerm, season);
            var chineseOnlyThirdSeason = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "chinese-only",
                Name = "一拳超人 第三季",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
                SearchAlias = englishTerm,
            }, "dandan", "DandanPlay", 0, englishTerm, season, 2025, 12,
                new[] { "一拳超人" }, new[] { season }, true, 3, true);
            var englishOnlyThirdSeason = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "english-only",
                Name = "One-Punch Man Season 3",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
                SearchAlias = englishTerm,
            }, "dandan", "DandanPlay", 0, englishTerm, season, 2025, 12,
                new[] { "一拳超人" }, new[] { season }, true, 3, true);
            var bothParents = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "both",
                Name = "One-Punch Man 一拳超人 第三季",
                Category = "动漫",
                Year = 2025,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, englishTerm, season, 2025, 12,
                new[] { "一拳超人" }, new[] { season }, true, 3, true);

            Assert(thirdSeason.Score == 1 && japaneseThirdSeason.Score == 1 &&
                   chineseOnlyThirdSeason.Score == 1 && englishOnlyThirdSeason.Score == 1,
                "TMDB Season rounds must score either the original localized parent or the current English/Japanese term as independent 60-point parent evidence");
            Assert(bothParents.ParentTitleScore == 1 && bothParents.KeywordScore == 1 &&
                   bothParents.Score == 1,
                "a second known parent in the same source-title channel may be validated as the trailing Season marker's parent-like prefix, without stacking parent points");

            var integratedCalls = new List<string>();
            var integratedCandidates = new List<DanmuMatchCandidate>();
            var integratedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var integratedReached = InvokeAliasTermWithContext(
                new DanmuMatchSearchResult(),
                new TermFixtureScraper(integratedCalls, term => new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "integrated-local-alias",
                        Name = "一拳旧名 第三季",
                        Category = "动漫",
                        Year = 2025,
                        EpisodeSize = 12,
                    },
                }),
                englishTerm,
                integratedCandidates,
                integratedTerms,
                "一拳超人",
                season,
                3,
                2025,
                12,
                new[] { "一拳旧名" },
                new[] { "一拳旧名 第三季" },
                CancellationToken.None);
            Assert(integratedReached && integratedCalls.SequenceEqual(new[] { englishTerm }) &&
                   integratedCandidates.Count == 1 &&
                   integratedCandidates[0].ParentTitleScore == 1 &&
                   integratedCandidates[0].Score == 1,
                "the real SearchTmdbTermAsync Season path must explicitly carry original/local parent aliases into alias-only parent scoring");
            Assert(firstSeason.Score == 0.60 && secondSeason.Score == 0.60 &&
                   firstSeason.EpisodeScore == 0 && secondSeason.EpisodeScore == 0,
                "bare and conflicting localized Seasons must stay below the alias threshold with parent evidence only: first=" +
                firstSeason.Score + ", second=" + secondSeason.Score);

            var aliasResult = new DanmuMatchSearchResult
            {
                CanonicalCandidates = new List<DanmuMatchCandidate>
                {
                    new DanmuMatchCandidate { Id = "baseline", Site = "dandan", Score = 0.45 },
                },
            };
            DanmuMatchSearchEngine.ApplyTmdbAliasCandidates(aliasResult,
                new[] { firstSeason, secondSeason, thirdSeason });
            Assert(aliasResult.UsedTmdbAlias &&
                   DanmuMatchScorer.SelectAutoCandidate(aliasResult.CanonicalCandidates)?.Id == "17576" &&
                   aliasResult.CanonicalCandidates[0].Id == "17576",
                "alias replacement must keep the highest current-term exact Season candidate and select it before conflicting Seasons");
        }

        private static DanmuMatchCandidate ScoreLiveOnePunchManCandidate(
            string id,
            string title,
            int year,
            string searchTerm,
            string season)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = id,
                Name = title,
                Category = "动漫",
                Year = year,
                EpisodeSize = 12,
                SearchAlias = searchTerm,
                Aliases = new List<string> { searchTerm },
            }, "dandan", "DandanPlay", 0, searchTerm, season, 2025, 12,
                new[] { "一拳超人" }, new[] { season }, true, 3, true);
        }

        private static void ScoresBookwormFourthSeasonDespiteAuthoritativeCountDifference()
        {
            const string originalSeries = "爱书的下克上";
            const string originalSeason = "爱书的下克上 S4";
            const string aliasTerm = "小书痴";
            var calls = new List<string>();
            var candidates = new List<DanmuMatchCandidate>();
            var reachedAliasThreshold = InvokeAliasTermWithContext(
                new DanmuMatchSearchResult(),
                new TermFixtureScraper(calls, term => new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "bookworm-s4",
                        Name = "小书痴第四季",
                        Category = "动漫",
                        Year = 2026,
                        EpisodeSize = 24,
                    },
                }),
                aliasTerm,
                candidates,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                originalSeries,
                originalSeason,
                4,
                2026,
                16,
                null,
                null,
                CancellationToken.None);

            var candidate = candidates.Single();
            Assert(reachedAliasThreshold && calls.SequenceEqual(new[] { aliasTerm }) &&
                   candidate.Score == 1 && candidate.ParentTitleScore == 1 &&
                   candidate.KeywordScore == 1 && candidate.YearScore == 1 &&
                   candidate.EpisodeScore == 0 && candidate.EpisodeSize == 24 &&
                   DanmuMatchScorer.SelectAutoCandidate(candidates)?.Id == "bookworm-s4",
                "爱书的下克上 S4 / 小书痴第四季 / 2026 must score 100 and auto-select when local 16 differs from source 24");
        }

        private static void ScoresOnlyValidatedShortParentAliasExtensions()
        {
            const string aliasTerm = "小书痴的下克上";
            const string originalParent = "爱书的下克上：为了成为图书管理员不择手段！";
            const string extendedAliasParent = "小书痴的下克上 〜为了成为图书管理员而不择手段〜";
            const string originalSecondSeason = originalParent + " 第二季";
            var calls = new List<string>();
            var candidates = new List<DanmuMatchCandidate>();
            var reachedAliasThreshold = InvokeAliasTermWithContext(
                new DanmuMatchSearchResult(),
                new TermFixtureScraper(calls, term => new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "bookworm-live-s2",
                        Name = extendedAliasParent + " 第二季",
                        Category = "动漫",
                        Year = 2020,
                        EpisodeSize = 12,
                    },
                    new ScraperSearchInfo
                    {
                        Id = "bookworm-live-ova",
                        Name = extendedAliasParent + " OVA",
                        Category = "动漫",
                        Year = 2020,
                        EpisodeSize = 12,
                    },
                }),
                aliasTerm,
                candidates,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                originalParent,
                originalSecondSeason,
                2,
                2020,
                12,
                null,
                null,
                CancellationToken.None);

            var correct = candidates.Single(candidate => candidate.Id == "bookworm-live-s2");
            var ova = candidates.Single(candidate => candidate.Id == "bookworm-live-ova");
            Assert(reachedAliasThreshold && calls.SequenceEqual(new[] { aliasTerm }) &&
                   correct.ParentTitleScore == 1 && correct.KeywordScore == 1 &&
                   correct.YearScore == 1 && correct.Score == 1 &&
                   ova.ParentTitleScore == 1 && ova.KeywordScore == 0 &&
                   ova.YearScore == 1 && ova.Score == 0.80 &&
                   DanmuMatchScorer.SelectAutoCandidate(candidates)?.Id == "bookworm-live-s2",
                "the live Bookworm alias round must score the extended-parent S2 as 60/20/20=100, keep OVA at 80, and uniquely auto-select S2");

            foreach (var seasonNumber in new[] { 2, 3, 4 })
            {
                var marker = seasonNumber == 2 ? "第二季" : seasonNumber == 3 ? "第三季" : "第四季";
                var scored = ScoreBookwormExtendedAlias(
                    "bookworm-s" + seasonNumber,
                    extendedAliasParent + marker,
                    originalParent + marker,
                    aliasTerm,
                    originalParent,
                    seasonNumber);
                Assert(scored.ParentTitleScore == 1 && scored.KeywordScore == 1 &&
                       scored.YearScore == 1 && scored.Score == 1,
                    "validated Bookworm extended-parent S" + seasonNumber +
                    " must retain exact 60/20/20 evidence");
            }

            var syntheticOnly = ScoreBookwormExtendedAlias(
                "synthetic-only", extendedAliasParent + "第二季", aliasTerm,
                aliasTerm, originalParent, 2);
            var trailingText = ScoreBookwormExtendedAlias(
                "trailing-text", extendedAliasParent + "第二季新章", originalSecondSeason,
                aliasTerm, originalParent, 2);
            var conflictingMarkers = ScoreBookwormExtendedAlias(
                "conflicting", extendedAliasParent + "第二季第三季", originalSecondSeason,
                aliasTerm, originalParent, 2);
            var numericPrefix = ScoreBookwormExtendedAlias(
                "numeric-prefix", aliasTerm + "1234第二季", originalSecondSeason,
                aliasTerm, originalParent, 2);
            var shortPrefix = ScoreBookwormExtendedAlias(
                "short-prefix", aliasTerm + "为了成第二季", originalSecondSeason,
                aliasTerm, originalParent, 2);
            var validatedFourLetterPrefix = ScoreBookwormExtendedAlias(
                "valid-prefix", aliasTerm + "为了成为第二季", originalSecondSeason,
                aliasTerm, originalParent, 2);
            Assert(syntheticOnly.KeywordScore < 1 && trailingText.KeywordScore < 1 &&
                   conflictingMarkers.KeywordScore == 0 && numericPrefix.KeywordScore < 1 &&
                   shortPrefix.KeywordScore < 1 && validatedFourLetterPrefix.KeywordScore == 1,
                "short-parent recovery must require real target evidence, one correct trailing marker, and a 4-letter parent-similar prefix");

            var ordinarySearch = ScoreBookwormExtendedAlias(
                "ordinary-search", extendedAliasParent + "第二季", originalSecondSeason,
                aliasTerm, originalParent, 2, false);
            Assert(ordinarySearch.ParentTitleScore == 1 && ordinarySearch.KeywordScore < 1 &&
                   ordinarySearch.Score < 1,
                "ordinary Season scoring must retain whole-remainder similarity even when alias-extension conditions otherwise match");

            var longerThanKnownParent = ScoreBookwormExtendedAlias(
                "overlong-prefix", aliasTerm + "无关字" + originalParent + "第二季",
                originalSecondSeason, aliasTerm, originalParent, 2);
            Assert(longerThanKnownParent.KeywordScore < 1,
                "an unrelated prefix followed by a complete known parent must be rejected when the extension is longer than that parent");

            var namedTargetWithGenericAlias = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "named-target-with-generic-alias",
                Name = extendedAliasParent + "第二季",
                Category = "动漫",
                Year = 2020,
                EpisodeSize = 12,
                SearchAlias = aliasTerm,
            }, "dandan", "DandanPlay", 0, aliasTerm, "星尘斗士", 2020, 12,
                new[] { originalParent }, new[] { "Season 2" }, true, 2, true);
            Assert(namedTargetWithGenericAlias.ParentTitleScore == 1 &&
                   namedTargetWithGenericAlias.KeywordScore < 1 &&
                   namedTargetWithGenericAlias.Score < 1,
                "a named target remainder must fail closed even when another local Season alias is the expected generic Season 2 label");
        }

        private static DanmuMatchCandidate ScoreBookwormExtendedAlias(
            string id,
            string sourceTitle,
            string originalSeason,
            string aliasTerm,
            string originalParent,
            int expectedSeasonNumber,
            bool includeLocalSeriesAliasesForParentScoring = true)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = id,
                Name = sourceTitle,
                Category = "动漫",
                Year = 2020,
                EpisodeSize = 12,
                SearchAlias = aliasTerm,
            }, "dandan", "DandanPlay", 0, aliasTerm, aliasTerm, 2020, 12,
                new[] { originalParent }, new[] { originalSeason }, true,
                expectedSeasonNumber, includeLocalSeriesAliasesForParentScoring);
        }

        private static void OrdersJojoSplitSeasonTitlesWithoutNumericLeakage()
        {
            var stardust = ScoreJojoCandidate("star", "JOJO的奇妙冒险 星尘斗士", 24);
            var egypt = ScoreJojoCandidate("egypt", "JOJO的奇妙冒险 星尘斗士 埃及篇", 24);
            var wrongNumber = ScoreJojoCandidate("wrong", "JOJO的奇妙冒险 第三季", 24);
            Assert(stardust.Score > egypt.Score && egypt.Score > wrongNumber.Score &&
                   wrongNumber.KeywordScore == 0,
                "JOJO Stardust and Egypt titles must remain ordered by whole-remainder similarity while an explicit wrong Season number contributes no Season evidence");
        }

        private static DanmuMatchCandidate ScoreJojoCandidate(string id, string title, int episodes)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = id,
                Name = title,
                Category = "动漫",
                Year = 2014,
                EpisodeSize = episodes,
            }, "dandan", "DandanPlay", 0, "JOJO的奇妙冒险",
                "JOJO的奇妙冒险 星尘斗士篇", 2014, 48, null, null, true, 2);
        }

        private static void ReplacesCanonicalCandidatesForAutomaticSelection()
        {
            var baseline = new DanmuMatchCandidate { Id = "baseline", Score = 0.60 };
            var aliasWinner = new DanmuMatchCandidate
            {
                Id = "alias-winner",
                Site = "dandan",
                Score = 0.90,
                MatchOrigin = "tmdb-alias",
                DecisionReason = "tmdb-alias:" + AscendanceOfABookworm,
            };
            var result = new DanmuMatchSearchResult
            {
                CanonicalCandidates = new[] { baseline }.ToList(),
                Candidates = new[] { baseline }.ToList(),
            };

            DanmuMatchSearchEngine.ApplyTmdbAliasCandidates(result, new[] { aliasWinner });

            Assert(result.UsedTmdbAlias &&
                   result.CanonicalCandidates.Count == 1 &&
                   result.CanonicalCandidates[0].Id == "alias-winner" &&
                   DanmuMatchScorer.SelectAutoCandidate(result.CanonicalCandidates)?.Id == "alias-winner",
                "TMDB alias results must replace canonical candidates used by automatic selection");
        }

        private static void PreservesLazyRoundOrderingAndShortCircuitContract()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Scraper", "DanmuMatchSearchEngine.cs"));
            var source = File.ReadAllText(sourcePath);
            var chineseIndex = source.IndexOf("aliases?.BuildSearchPlan", StringComparison.Ordinal);
            var englishIndex = source.IndexOf("option, \"en-US\"", StringComparison.Ordinal);
            var japaneseIndex = source.IndexOf("option, \"ja-JP\"", StringComparison.Ordinal);
            Assert(chineseIndex >= 0 && englishIndex > chineseIndex && japaneseIndex > englishIndex &&
                   source.Contains("if (reachedThreshold)") && source.Contains("break;"),
                "Chinese rounds must precede lazy English/Japanese primary details and stop after the first 0.80 round");
        }

        private static void TracksCompletedAliasProviderCalls()
        {
            var successful = new DanmuMatchSearchResult { IsComplete = false };
            InvokeAliasTerm(successful, new AliasFixtureScraper("DandanID", false), CancellationToken.None);
            Assert(successful.HasCompletedProviders && successful.CompletedProviderCount == 1 &&
                   successful.CompletedProviderIds.SequenceEqual(new[] { "DandanID" }),
                "a successful TMDB alias provider call, including an empty response, must count as completed-provider coverage");
            InvokeAliasTerm(successful, new AliasFixtureScraper("DandanID", false), CancellationToken.None);
            Assert(successful.CompletedProviderCount == 1,
                "repeated alias calls from the same provider must not duplicate completed-provider coverage");

            successful.CanonicalCandidates.Add(new DanmuMatchCandidate
            {
                Id = "alias-high-confidence",
                Site = "dandan",
                SourceOrder = 0,
                Score = 0.90,
                MatchOrigin = "tmdb-alias",
            });
            Classify(successful);
            Assert(successful.Decision == "confident" &&
                   successful.SelectedCandidate?.Id == "alias-high-confidence",
                "an alias-completed provider must restore ordinary confident classification after the initial round failed");

            var allAliasFailed = new DanmuMatchSearchResult { IsComplete = false };
            InvokeAliasTerm(allAliasFailed, new AliasFixtureScraper("DandanID", true), CancellationToken.None);
            Classify(allAliasFailed);
            Assert(!allAliasFailed.HasCompletedProviders && allAliasFailed.Decision == "retryable-incomplete",
                "all failed alias calls must not fabricate completed-provider coverage or escape the retryable path");

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var parentCancelled = new DanmuMatchSearchResult { IsComplete = false, WasCancelled = true };
                InvokeAliasTerm(parentCancelled, new AliasFixtureScraper("DandanID", false), cancellation.Token);
                Classify(parentCancelled);
                Assert(!parentCancelled.HasCompletedProviders && parentCancelled.Decision == "cancelled" &&
                       parentCancelled.SelectedCandidate == null,
                    "parent cancellation during an alias call must remain terminal even if a provider would otherwise be available");
            }
        }

        private static void KeepsExhaustedSeasonAliasesServerLocal()
        {
            var baseline = new DanmuMatchCandidate
            {
                Id = "ordinary-low",
                Site = "other",
                Score = 0.42,
            };
            var result = new DanmuMatchSearchResult
            {
                CanonicalCandidates = new List<DanmuMatchCandidate> { baseline },
                Candidates = new List<DanmuMatchCandidate> { baseline },
            };
            var aliases = new List<DanmuMatchCandidate>
            {
                new DanmuMatchCandidate { Id = "jojo-repeat", Site = "dandan", Score = 0.61 },
                new DanmuMatchCandidate { Id = "jojo-repeat", Site = "dandan", Score = 0.64 },
            };

            var exhausted = DanmuMatchSearchEngine.CompleteTmdbAliasPlan(
                result, aliases, 2, false, false);
            Assert(exhausted && !result.UsedTmdbAlias &&
                   result.CanonicalCandidates.Count == 1 &&
                   result.CanonicalCandidates[0].Id == "ordinary-low" &&
                   result.Candidates.Count == 1,
                "low-confidence repeated Season aliases must stay server-local and expose only exhaustion state");

            var calls = new List<string>();
            var faultThenSuccess = new TermFixtureScraper(calls, term =>
            {
                if (term == "fault")
                {
                    throw new InvalidOperationException("fixture fault");
                }
                return new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "jojo-low",
                        Name = "unrelated",
                        Category = "动画",
                        Year = 1990,
                        EpisodeSize = 1,
                    },
                };
            });
            var accumulated = new List<DanmuMatchCandidate>();
            var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Assert(!InvokeAliasTerm(result, faultThenSuccess, "fault", accumulated, attempted) &&
                   !InvokeAliasTerm(result, faultThenSuccess, "later", accumulated, attempted) &&
                   calls.SequenceEqual(new[] { "fault", "later" }) &&
                   result.HasCompletedProviders,
                "one alias fault must not prevent the next unique alias from completing");

            var thresholdResult = new DanmuMatchSearchResult();
            var thresholdCalls = new List<string>();
            var thresholdScraper = new TermFixtureScraper(thresholdCalls, term =>
                new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "jojo-winner",
                        Name = term,
                        Category = "动画",
                        Year = 2012,
                        EpisodeSize = 26,
                    },
                });
            var thresholdAliases = new List<DanmuMatchCandidate>();
            var thresholdTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var reached = InvokeAliasTerm(
                thresholdResult, thresholdScraper, "JOJO的奇妙冒险", thresholdAliases, thresholdTerms);
            Assert(reached && thresholdCalls.Count == 1 &&
                   !DanmuMatchSearchEngine.CompleteTmdbAliasPlan(
                       thresholdResult, thresholdAliases, thresholdTerms.Count, true, false) &&
                   thresholdResult.UsedTmdbAlias && thresholdResult.CanonicalCandidates.Count > 0,
                "the first threshold-reaching Season alias must be immediately eligible to short-circuit and apply");

            var movieResult = new DanmuMatchSearchResult();
            Assert(!DanmuMatchSearchEngine.CompleteTmdbAliasPlan(
                       movieResult, aliases, 2, false, true) && movieResult.UsedTmdbAlias,
                "Movie alias replacement must remain unchanged even when its candidates are below the Season alias threshold");
        }

        private static void SearchesParentTitleOnceWithOrdinarySeasonScoring()
        {
            const string authoritativeParentTitle = "JOJO的奇妙冒险";
            var providerTerms = new List<string>();
            var scraper = new TermFixtureScraper(providerTerms, term =>
                new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "jojo-parent",
                        Name = authoritativeParentTitle,
                        Category = "动画",
                        Year = 2012,
                        EpisodeSize = 26,
                    },
                });
            var tmdbCalls = 0;
            var originalSender = TmdbAliasClient.HttpGetResponseAsync;
            try
            {
                TmdbAliasClient.HttpGetResponseAsync = request =>
                {
                    tmdbCalls++;
                    throw new InvalidOperationException("parent-title rematch must not call TMDB");
                };
                var search = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new[] { scraper }, authoritativeParentTitle, "Season 1", 2012, 26,
                    authoritativeParentTitle, null, Core.BoundedSearchPolicy.Shared,
                    CancellationToken.None, CancellationToken.None, null, null,
                    new Season { IndexNumber = 1 }).GetAwaiter().GetResult();

                Assert(providerTerms.SequenceEqual(new[] { authoritativeParentTitle }) &&
                       tmdbCalls == 0 && search.SelectedCandidate?.Id == "jojo-parent" &&
                       !search.UsedTmdbAlias && !search.ParentTitleRematchAvailable,
                    "parent-title rematch must make one provider round with the authoritative title, ordinary auto scoring, and zero TMDB expansion");
            }
            finally
            {
                TmdbAliasClient.HttpGetResponseAsync = originalSender;
            }
        }

        private static void FreezesParentTitleRematchRequestBoundary()
        {
            Assert(typeof(DanmuParams).GetProperty("ParentTitleRematch")?
                       .GetCustomAttribute<DataMemberAttribute>()?.Name == "parentTitleRematch" &&
                   typeof(DanmuSeasonMatchResult).GetProperty("ParentTitleRematchAvailable") != null &&
                   typeof(DanmuItemMatchResult).GetProperty("ParentTitleRematchAvailable") == null,
                "l6 request/response fields must remain additive and Season-owned");

            var mixedIntent = typeof(DanmuController).GetMethod(
                "HasMixedParentTitleRematchIntent", BindingFlags.Static | BindingFlags.NonPublic);
            Assert(!(bool)mixedIntent.Invoke(null, new object[]
                   {
                       new DanmuParams { ParentTitleRematch = true },
                   }) &&
                   (bool)mixedIntent.Invoke(null, new object[]
                   {
                       new DanmuParams { ParentTitleRematch = true, Keyword = "forged parent" },
                   }) &&
                   (bool)mixedIntent.Invoke(null, new object[]
                   {
                       new DanmuParams { ParentTitleRematch = true, Mode = DanmuMatchIntent.Rematch },
                   }) &&
                   (bool)mixedIntent.Invoke(null, new object[]
                   {
                       new DanmuParams { ParentTitleRematch = true, Site = "dandan" },
                   }) &&
                   (bool)mixedIntent.Invoke(null, new object[]
                   {
                       new DanmuParams { ParentTitleRematch = true, CompositePlan = true },
                   }) &&
                   (bool)mixedIntent.Invoke(null, new object[]
                   {
                       new DanmuParams { ParentTitleRematch = true, SearchScope = "temporary-range" },
                   }),
                "mixed keyword, mode, selection, composite, and temporary parent-rematch intents must be rejected before search");

            var selector = typeof(DanmuController).GetMethod(
                "SelectUniqueParentTitleRematchSeason", BindingFlags.Static | BindingFlags.NonPublic);
            var seasons = new[]
            {
                new Season { Name = "Season 1", IndexNumber = 1 },
                new Season { Name = "Season 2", IndexNumber = 2 },
            };
            Assert(selector.Invoke(null, new object[]
                   {
                       seasons, new DanmuParams { SeasonNumber = 1, SeasonName = "Season 1" },
                   }) == seasons[0] &&
                   selector.Invoke(null, new object[] { seasons, new DanmuParams() }) == null &&
                   selector.Invoke(null, new object[]
                   {
                       seasons, new DanmuParams { SeasonName = "client-forged-title" },
                   }) == null,
                "parent-title rematch must resolve exactly one authoritative Season and reject missing, ambiguous, or forged locators");

            var tmdbDiagnostic = typeof(DanmuController).GetMethod(
                "IsTmdbAliasDiagnostic", BindingFlags.Static | BindingFlags.NonPublic);
            var tmdbError = typeof(DanmuController).GetMethod(
                "IsTmdbAliasError", BindingFlags.Static | BindingFlags.NonPublic);
            Assert((bool)tmdbDiagnostic.Invoke(null, new object[]
                   {
                       new DanmuSearchCompletionDiagnostic { Provider = "TMDB Alias" },
                   }) &&
                   (bool)tmdbDiagnostic.Invoke(null, new object[]
                   {
                       new DanmuSearchCompletionDiagnostic { Provider = "tmdb-fallback" },
                   }) &&
                   !(bool)tmdbDiagnostic.Invoke(null, new object[]
                   {
                       new DanmuSearchCompletionDiagnostic { Provider = "Youku" },
                   }) &&
                   (bool)tmdbError.Invoke(null, new object[] { "[tmdb-alias]: failed" }) &&
                   !(bool)tmdbError.Invoke(null, new object[] { "Youku: failed" }),
                "alias exhaustion must suppress normalized TMDB-only diagnostics while retaining ordinary provider failures");

            var controllerSource = File.ReadAllText(Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Core", "Controllers", "DanmuController.cs")));
            var parentBranchStart = controllerSource.IndexOf(
                "if (request.ParentTitleRematch)", StringComparison.Ordinal);
            var mixedGateIndex = controllerSource.IndexOf(
                "HasMixedParentTitleRematchIntent(request)", parentBranchStart, StringComparison.Ordinal);
            var resolutionGateIndex = controllerSource.IndexOf(
                "TryResolveParentTitleRematchTarget(item, request", parentBranchStart, StringComparison.Ordinal);
            var providerSearchIndex = controllerSource.IndexOf(
                "var parentTitleSeason = await GetSeasonMatchPreview(", parentBranchStart, StringComparison.Ordinal);
            Assert(controllerSource.Contains("var seriesName = authoritativeParentSeries?.Name ?? string.Empty;") &&
                   controllerSource.Contains("SeriesId = authoritativeParentSeries?.Id.ToString() ?? string.Empty") &&
                   controllerSource.Contains("new[] { authoritativeParentSeries?.OriginalTitle }") &&
                   controllerSource.Contains("authoritativeParentSeries.Name,") &&
                   controllerSource.Contains("parent-title-unavailable") &&
                   parentBranchStart >= 0 && mixedGateIndex > parentBranchStart &&
                   resolutionGateIndex > mixedGateIndex && providerSearchIndex > resolutionGateIndex,
                "Season preview must use authoritative Series identity, and mixed/missing/ambiguous requests must return before any provider search");
        }

        private static void InvokeAliasTerm(
            DanmuMatchSearchResult result,
            AbstractScraper scraper,
            CancellationToken cancellationToken)
        {
            InvokeAliasTerm(result, scraper, "alias", new List<DanmuMatchCandidate>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), cancellationToken);
        }

        private static bool InvokeAliasTerm(
            DanmuMatchSearchResult result,
            AbstractScraper scraper,
            string term,
            List<DanmuMatchCandidate> candidates,
            ISet<string> attemptedTerms,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return InvokeAliasTermWithContext(
                result, scraper, term, candidates, attemptedTerms,
                "Series", "Season 1", 1, 2012, 26, null, null, cancellationToken);
        }

        private static bool InvokeAliasTermWithContext(
            DanmuMatchSearchResult result,
            AbstractScraper scraper,
            string term,
            List<DanmuMatchCandidate> candidates,
            ISet<string> attemptedTerms,
            string originalSeriesName,
            string originalSeasonName,
            int targetSeasonNumber,
            int expectedYear,
            int expectedEpisodes,
            IEnumerable<string> localSeriesTitleAliases,
            IEnumerable<string> localSeasonTitleAliases,
            CancellationToken cancellationToken)
        {
            var method = typeof(DanmuMatchSearchEngine).GetMethod(
                "SearchTmdbTermAsync", BindingFlags.Static | BindingFlags.NonPublic);
            var task = (Task<bool>)method.Invoke(null, new object[]
            {
                result, scraper, term, candidates, attemptedTerms, 0,
                originalSeriesName, originalSeasonName, targetSeasonNumber,
                expectedYear, expectedEpisodes, false, null, cancellationToken,
                localSeriesTitleAliases, localSeasonTitleAliases,
            });
            return task.GetAwaiter().GetResult();
        }

        private static void Classify(DanmuMatchSearchResult result)
        {
            var method = typeof(DanmuMatchSearchEngine).GetMethod(
                "ClassifyResult", BindingFlags.Static | BindingFlags.NonPublic);
            method.Invoke(null, new object[] { result });
        }

        private sealed class AliasFixtureScraper : AbstractScraper
        {
            private readonly string _providerId;
            private readonly bool _throws;

            public AliasFixtureScraper(string providerId, bool throws) : base(null)
            {
                _providerId = providerId;
                _throws = throws;
            }

            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) => _throws
                ? Task.FromException<List<ScraperSearchInfo>>(new InvalidOperationException("alias fixture failure"))
                : Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private sealed class TermFixtureScraper : AbstractScraper
        {
            private readonly IList<string> _terms;
            private readonly Func<string, List<ScraperSearchInfo>> _search;

            public TermFixtureScraper(
                IList<string> terms,
                Func<string, List<ScraperSearchInfo>> search) : base(null)
            {
                _terms = terms;
                _search = search;
            }

            public override string Name => "Dandan";
            public override string ProviderName => "Dandan";
            public override string ProviderId => "dandan";
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                SearchForApi(item?.Name);
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                _terms.Add(keyword);
                return Task.FromResult(_search(keyword));
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private static TmdbAlternativeTitleResponse Deserialize(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(TmdbAlternativeTitleResponse));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (TmdbAlternativeTitleResponse)serializer.ReadObject(stream);
            }
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
