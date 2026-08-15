using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Tmdb;
using MediaBrowser.Common.Net;
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
            AppliesConservativeSeasonConflictCap();
            ScoresLiveOnePunchManThirdSeasonAliasFallback();
            ReplacesCanonicalCandidatesForAutomaticSelection();
            PreservesLazyRoundOrderingAndShortCircuitContract();
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
            Assert(TmdbAliasClient.UserAgent == "DanmuPlus/2.0.4r2",
                "TMDB requests must identify the r2 build without retaining the r1 user-agent");
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

        private static void AppliesConservativeSeasonConflictCap()
        {
            Assert(DanmuMatchScorer.ParseExplicitSeasonNumber("OVA S00 / Season 1") == null,
                "mixed Season 0 and positive markers must remain ambiguous instead of creating contradiction evidence");
            var wrongSeason = ScoreSeason("One Punch Man Season 1", 2015, "Season 3", 2025, true);
            var wrongYear = ScoreSeason("One Punch Man", 2015, "Season 3", 2025, true);
            var manual = ScoreSeason("One Punch Man Season 1", 2015, "Season 3", 2025, false);
            var special = ScoreSeason("One Punch Man Season 1", 2015, "Season 0", 2025, true);
            var compatible = ScoreSeason("One Punch Man Season 3", 2024, "Season 3", 2025, true);

            Assert(wrongSeason.Score == 0.79 && wrongYear.Score == 0.79,
                "explicit wrong Seasons and known years differing by at least two must remain below the alias threshold");
            Assert(manual.Score > 0.80 && special.Score > 0.80 && compatible.Score > 0.80,
                "manual searches, Season 0, and compatible or adjacent-year evidence must retain existing scores");
            Assert(DanmuMatchScorer.ParseExplicitSeasonNumber("Episode 3 Part 2 Cour 1") == null &&
                   DanmuMatchScorer.ParseExplicitSeasonNumber("Season 0") == null &&
                   DanmuMatchScorer.ParseExplicitSeasonNumber(new[] { "Season 2", "第3季" }) == null,
                "bare, Episode, Part/Cour, Season 0, and ambiguous markers must not become contradictory Season evidence");
        }

        private static DanmuMatchCandidate ScoreSeason(
            string title, int year, string targetSeason, int targetYear, bool applyCap)
        {
            return DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = title,
                Name = title,
                Category = "动漫",
                Year = year,
                EpisodeSize = 12,
            }, "dandan", "DandanPlay", 0, "One Punch Man", targetSeason, targetYear, 12,
                null, null, applyCap, DanmuMatchScorer.ParseExplicitSeasonNumber(targetSeason));
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

            Assert(thirdSeason.Score >= 0.80 && japaneseThirdSeason.Score >= 0.80 &&
                   thirdSeason.Reason.Contains("本次搜索词结果季号吻合"),
                "the live-equivalent English/Japanese TMDB fallback must bridge a localized exact third-Season result without reverting to the library title");
            Assert(firstSeason.Score < 0.80 && secondSeason.Score < 0.80,
                "the live-equivalent first and second Season results must remain below the alias threshold when year or explicit Season evidence conflicts: first=" +
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
            }, "dandan", "DandanPlay", 0, searchTerm, season, 2025, 12,
                null, null, true, 3);
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
