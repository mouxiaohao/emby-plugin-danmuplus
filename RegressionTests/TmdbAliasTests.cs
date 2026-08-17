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
            ScoresLiveOnePunchManThirdSeasonAliasFallback();
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
            Assert(TmdbAliasClient.UserAgent == "DanmuPlus/2.0.5r1",
                "TMDB requests must identify the 2.0.5r1 build");
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

            Assert(aboveThreshold.Score == 0.70 && belowThreshold.Score == 0.60 &&
                   aboveThreshold.Score == OrdinaryNoKeywordScore(aboveThreshold) &&
                   belowThreshold.Score == OrdinaryNoKeywordScore(belowThreshold),
                "conflicting Season/year evidence must receive only parent and exact positive metadata evidence: above=" +
                aboveThreshold.Score + ", below=" + belowThreshold.Score);
            Assert(aboveThreshold.YearScore == 0 && belowThreshold.YearScore == 0 &&
                   aboveThreshold.Name.Contains("Season 1") && aboveThreshold.Year == 2015 &&
                   aboveThreshold.Reason.Contains("父剧名出现") &&
                   aboveThreshold.Reason.Contains("集数吻合"),
                "the retained candidate fields and positive reason evidence must still explain a conflicting result");
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
                candidate.YearScore * 0.10 + candidate.EpisodeScore * 0.10,
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
            Assert(correct.Score == 1 && bare.Score == 0.70 && second.Score == 0.70 &&
                   youkuSecond.Score == 0.60,
                "Season weights must be parent 60, whole-remainder Season 20, exact year 10, and exact episode count 10");
            Assert(extraThirdKeyword.KeywordScore > 0 && extraThirdKeyword.KeywordScore < 1 &&
                   extraThirdKeyword.Score < 1,
                "an equal numeric Season marker must not erase additional remainder keywords or receive the full 20-point Season score");

            var splitChannels = ScoreOnePunchMan("split", "一拳超人", 2025, 12, "第三季");
            Assert(splitChannels.Score == 0.80 && splitChannels.ParentTitleScore == 1 &&
                   splitChannels.KeywordScore == 0,
                "parent evidence from Name and Season evidence from SourceMetadata.Title must not be combined across source-title channels");

            var yearOffByOne = ScoreOnePunchMan("year", "一拳超人 第三季", 2024, 12, null);
            var episodesOffByOne = ScoreOnePunchMan("episodes", "一拳超人 第三季", 2025, 13, null);
            var missingMetadata = ScoreOnePunchMan("missing", "一拳超人 第三季", 0, 0, null);
            Assert(yearOffByOne.YearScore == 0 && episodesOffByOne.EpisodeScore == 0 &&
                   missingMetadata.YearScore == 0 && missingMetadata.EpisodeScore == 0 &&
                   yearOffByOne.Score == 0.90 && episodesOffByOne.Score == 0.90 &&
                   missingMetadata.Score == 0.80,
                "nearby or missing year/episode metadata must contribute zero instead of partial credit");

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
            Assert(bothParents.ParentTitleScore == 1 && bothParents.KeywordScore < 1 &&
                   bothParents.Score < 1,
                "matching both original and current alias parents must take the best single-parent combination without removing or stacking both parents");

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
            Assert(firstSeason.Score == 0.70 && secondSeason.Score == 0.70,
                "bare and conflicting localized Seasons must stay below the alias threshold with parent plus episode evidence only: first=" +
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
