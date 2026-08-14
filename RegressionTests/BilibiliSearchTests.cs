using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using BilibiliMedia = Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media;
using TencentEpisode = Emby.Plugin.Danmu.Scraper.Tencent.Entity.TencentEpisode;
using YoukuEpisode = Emby.Plugin.Danmu.Scraper.Youku.Entity.YoukuEpisode;
using DandanEpisode = Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode;
using IqiyiEpisode = Emby.Plugin.Danmu.Scraper.Iqiyi.Entity.IqiyiEpisode;
using MgtvEpisode = Emby.Plugin.Danmu.Scrapers.Mgtv.Entity.MgtvEpisode;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class BilibiliSearchTests
    {
        internal static void Run()
        {
            MergesAggregateAndTypedBourneFixtures();
            RejectsOrdinaryVideoFromTypedMediaPath();
            UsesTypedKindAndProviderAliasAcrossConsumers();
            ExercisesInjectableTypedPaginationAndPartialFailure();
            StopsTypedRetrievalAfterSessionFailure();
            PropagatesPartialDiagnosticsToMovieAndSeasonSearch();
            PreservesMovieParentIdentityAndBuildsStableProviderParts();
            RejectsPublicationTimestampsAsExactWorkYears();
        }

        private static void RejectsPublicationTimestampsAsExactWorkYears()
        {
            var exact = Emby.Plugin.Danmu.Scraper.Bilibili.Bilibili.CreateExactMovieEpisodeMedia(
                "12345",
                new VideoEpisode
                {
                    Id = 12345,
                    AId = 100,
                    CId = 200,
                    LongTitle = "正片",
                    PubTime = 1672531200, // 2023 provider publication; not production year.
                });
            Assert(exact != null && exact.Year == null,
                "Bilibili exact PGC publication time must not be exposed as work Year");

            var bourneSnapshot = new SourceMetadata
            {
                Title = "谍影重重",
                Year = 2002,
                Category = "电影",
            };
            var merged = SourceMetadata.MergeDetailWithSnapshot(
                new SourceMetadata { Title = "谍影重重", Category = "电影" },
                bourneSnapshot);
            Assert(merged?.Year == 2002,
                "Bourne 2002 candidate work year must survive exact detail that has no trustworthy production year");
        }

        private static void PreservesMovieParentIdentityAndBuildsStableProviderParts()
        {
            var parent = new BilibiliMedia
            {
                SeasonId = 501,
                Title = "The Bourne Identity",
                ApiType = "media_ft",
                SeasonType = 2,
            };
            Assert(BilibiliSearchResultMapper.TryMap(parent, out var candidate, out _) &&
                   candidate.Id == "501",
                "a Bilibili Movie candidate must retain its parent season identity");

            var biliParts = Emby.Plugin.Danmu.Scraper.Bilibili.Bilibili.BuildMovieParts(new[]
            {
                new VideoEpisode { Id = 1001, AId = 11, CId = 21, LongTitle = "普通话", Title = "正片" },
                new VideoEpisode { Id = 1002, AId = 12, CId = 22, LongTitle = "预告片", Badge = "预告" },
                new VideoEpisode { Id = 1003, AId = 13, CId = 23, LongTitle = "粤语" },
                new VideoEpisode { Id = 1001, AId = 11, CId = 21, LongTitle = "重复" },
                new VideoEpisode { Id = 1004, AId = 14, CId = 0, LongTitle = "无下载身份" },
            });
            var eligibleBiliParts = MoviePartPolicy.GetUsableParts(biliParts);
            Assert(biliParts.Count == 3 && eligibleBiliParts.Select(part => part.Id)
                       .SequenceEqual(new[] { "1001", "1003" }),
                "Bilibili parts must deduplicate ep_id, reject unverified leaves, flag extras, and retain provider order");

            var tencentParts = Emby.Plugin.Danmu.Scraper.Tencent.Tencent.BuildMovieParts("cid-parent", new[]
            {
                new TencentEpisode { Vid = "vid-cn", Title = "普通话" },
                new TencentEpisode { Vid = "vid-preview", Title = "终极预告", IsTrailer = "1" },
                new TencentEpisode { Vid = "vid-yue", Title = "粤语" },
                new TencentEpisode { Vid = "vid-cn", Title = "重复" },
                new TencentEpisode { Vid = "", Title = "无独立vid" },
            });
            Assert(tencentParts.Count == 3 &&
                   MoviePartPolicy.GetUsableParts(tencentParts).Select(part => part.Id)
                       .SequenceEqual(new[] { "cid-parent|vid-cn", "cid-parent|vid-yue" }),
                "Tencent parts must require independent vid, deduplicate, flag trailers, and retain stable order");

            var youkuParts = Emby.Plugin.Danmu.Scraper.Youku.Youku.BuildMovieParts(new[]
            {
                new YoukuEpisode { ID = "X-main", Title = "普通话" },
                new YoukuEpisode { ID = "X-extra", Title = "幕后花絮" },
                new YoukuEpisode { ID = "X-yue", RCTitle = "粤语" },
                new YoukuEpisode { ID = "X-main", Title = "重复" },
                new YoukuEpisode { ID = "", Title = "无exact identity" },
            });
            Assert(youkuParts.Count == 3 && youkuParts[2].Title == "粤语" &&
                   MoviePartPolicy.GetUsableParts(youkuParts).Select(part => part.Id)
                       .SequenceEqual(new[] { "X-main", "X-yue" }),
                "Youku parts must require exact episode identity, use conservative title fallback, and flag extras");

            var dandanParts = Emby.Plugin.Danmu.Scraper.Dandan.Dandan.BuildMovieParts(42, new[]
            {
                new DandanEpisode { EpisodeId = 420001, EpisodeTitle = "正片" },
                new DandanEpisode { EpisodeId = 420002, EpisodeTitle = "制作花絮" },
                new DandanEpisode { EpisodeId = 430001, EpisodeTitle = "错误父条目" },
            });
            Assert(dandanParts.Count == 2 && dandanParts[1].IsExplicitNonMain,
                "Dandan parts must verify the parent encoded by EpisodeId and flag explicit extras");

            var iqiyiParts = Emby.Plugin.Danmu.Scraper.Iqiyi.Iqiyi.BuildMovieParts(new[]
            {
                new IqiyiEpisode { TvId = 1, Name = "普通话", PlayUrl = "https://www.iqiyi.com/v_main.html" },
                new IqiyiEpisode { TvId = 2, Name = "采访", PlayUrl = "https://www.iqiyi.com/v_interview.html" },
                new IqiyiEpisode { TvId = 0, Name = "无下载身份", PlayUrl = "https://www.iqiyi.com/v_invalid.html" },
            });
            Assert(iqiyiParts.Count == 2 && iqiyiParts[0].Id == "main" &&
                   MoviePartPolicy.GetUsableParts(iqiyiParts).Select(part => part.Id)
                       .SequenceEqual(new[] { "main" }),
                "Iqiyi parts must require both stable LinkId and downloadable TvId");

            var mgtvParts = Emby.Plugin.Danmu.Scrapers.Mgtv.Mgtv.BuildMovieParts("collection", new[]
            {
                new MgtvEpisode { VideoId = "v1", Title2 = "普通话" },
                new MgtvEpisode { VideoId = "v2", Title2 = "Bonus" },
                new MgtvEpisode { VideoId = "v1", Title2 = "重复" },
            });
            Assert(mgtvParts.Count == 2 && mgtvParts[0].Id == "collection|v1" &&
                   MoviePartPolicy.GetUsableParts(mgtvParts).Select(part => part.Id)
                       .SequenceEqual(new[] { "collection|v1" }),
                "MGTV parts must deduplicate stable VideoId and flag explicit bonus material");
        }

        private static void MergesAggregateAndTypedBourneFixtures()
        {
            const string aggregateJson = "{\"code\":0,\"data\":{\"result\":[" +
                "{\"result_type\":\"media_ft\",\"data\":[" +
                "{\"season_id\":503,\"title\":\"谍影重重3\",\"season_type_name\":\"电影\"}," +
                "{\"season_id\":504,\"title\":\"谍影重重4\",\"season_type_name\":\"电影\"}," +
                "{\"season_id\":505,\"title\":\"谍影重重5\",\"season_type_name\":\"电影\"}]}]}}";
            const string typedJson = "{\"code\":0,\"data\":{\"page\":1,\"pagesize\":20,\"numResults\":5,\"numPages\":1,\"result\":[" +
                "{\"season_id\":501,\"title\":\"谍影重重\",\"season_type_name\":\"电影\"}," +
                "{\"season_id\":502,\"title\":\"谍影重重2\",\"season_type_name\":\"电影\"}," +
                "{\"season_id\":503,\"title\":\"谍影重重3\",\"season_type_name\":\"电影\"}]}}";

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var aggregate = JsonSerializer.Deserialize<ApiResult<BiliSearchAllV2Data>>(
                aggregateJson,
                jsonOptions);
            var typed = JsonSerializer.Deserialize<ApiResult<BiliSearchTypeData>>(typedJson, jsonOptions);
            var records = aggregate.Data.Result.SelectMany(group => group.Data)
                .Concat(typed.Data.Result)
                .ToList();
            var merged = BilibiliSearchResultMapper.MergeByCanonicalIdentity(records, 100);
            var mapped = merged.Select(item =>
            {
                Assert(BilibiliSearchResultMapper.TryMap(item, out var candidate, out _),
                    "every Bourne media fixture should map");
                return candidate;
            }).ToList();

            Assert(mapped.Count == 5 && mapped.Select(item => item.Id).Distinct().Count() == 5,
                "aggregate plus typed media results should deduplicate by canonical usable identity");
            var expectedTitles = new[]
            {
                "谍影重重", "谍影重重2", "谍影重重3", "谍影重重4", "谍影重重5"
            };
            foreach (var expectedTitle in expectedTitles)
            {
                Assert(mapped.Any(item => item.Name == expectedTitle),
                    "the merged Bilibili fixture should retain " + expectedTitle);
            }

            Assert(BilibiliApi.TypedSearchPageLimit == 2 &&
                   BilibiliApi.TypedSearchRecordLimitPerType == 40 &&
                   BilibiliApi.SearchRecordLimit == 100,
                "typed Bilibili retrieval must retain fixed page and record budgets");
        }

        private static void RejectsOrdinaryVideoFromTypedMediaPath()
        {
            var ordinaryVideo = new BilibiliMedia
            {
                SeasonId = 999,
                Title = "谍影重重解说",
                ApiType = "video",
            };
            Assert(!BilibiliSearchResultMapper.IsAllowedTypedMedia(ordinaryVideo, "media_ft") &&
                   !BilibiliSearchResultMapper.IsAllowedTypedMedia(ordinaryVideo, "media_bangumi"),
                "ordinary video records must never enter typed Movie or Bangumi discovery");
            Assert(!BilibiliSearchResultMapper.IsAllowedTypedMedia(ordinaryVideo, "video"),
                "the typed-media gate must reject an unsupported video search type itself");
            Assert(!BilibiliSearchResultMapper.IsAllowedAggregateMedia(ordinaryVideo),
                "an ordinary video record must also be rejected after aggregate group extraction");
        }

        private static void UsesTypedKindAndProviderAliasAcrossConsumers()
        {
            var movie = new BilibiliMedia
            {
                SeasonId = 701,
                Title = "Localized title",
                OrgTitle = "Original*",
                ApiType = "media_ft",
            };
            var season = new BilibiliMedia
            {
                SeasonId = 702,
                Title = "Season title",
                ApiType = "media_bangumi",
            };
            var typedTelevision = new BilibiliMedia
            {
                SeasonId = 703,
                Title = "Television title",
                ApiType = "media_ft",
                SeasonTypeName = "电视剧",
            };

            Assert(BilibiliSearchResultMapper.ResolveKind(movie) == BilibiliMediaKind.Movie &&
                   BilibiliSearchResultMapper.ResolveKind(season) == BilibiliMediaKind.Season,
                "the typed endpoint must provide a controlled fallback when explicit season classification is absent");
            Assert(BilibiliSearchResultMapper.ResolveKind(typedTelevision) == BilibiliMediaKind.Season,
                "a media_ft record explicitly classified as television must remain available to Season search");
            Assert(BilibiliSearchResultMapper.TryMap(movie, out var candidate, out _) &&
                   candidate.Category == "movie" && candidate.Aliases.SequenceEqual(new[] { "Original*" }),
                "typed media_ft without a localized label must map as Movie and retain org_title as a real source alias");
        }

        private static void ExercisesInjectableTypedPaginationAndPartialFailure()
        {
            var calls = new List<string>();
            var aggregate = new SearchResult
            {
                Result = new List<BilibiliMedia>
                {
                    new BilibiliMedia { SeasonId = 803, Title = "Film 3", ApiType = "media_ft" },
                }
            };
            var merged = BilibiliApi.MergeTypedAsync(
                aggregate,
                (type, page, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    calls.Add(type + ":" + page);
                    Assert(type == "media_ft" || type == "media_bangumi",
                        "typed retrieval must never request the ordinary video endpoint");
                    if (type == "media_ft" && page == 2)
                        throw new InvalidOperationException("fixture page two failure");
                    return Task.FromResult(new BiliSearchTypeData
                    {
                        Page = page,
                        NumPages = type == "media_ft" ? 2 : 1,
                        Result = type == "media_ft"
                            ? new List<BilibiliMedia>
                            {
                                new BilibiliMedia { SeasonId = 801, Title = "Film 1" },
                                new BilibiliMedia { SeasonId = 999, Title = "Uploader clip", ApiType = "video" },
                            }
                            : new List<BilibiliMedia>()
                    });
                },
                CancellationToken.None).GetAwaiter().GetResult();

            Assert(calls.SequenceEqual(new[] { "media_ft:1", "media_ft:2", "media_bangumi:1" }),
                "the injectable typed fetcher should stop at the fixed page budget/final page and continue the other media kind after failure");
            Assert(merged.Result.Select(item => item.SeasonId).OrderBy(id => id)
                       .SequenceEqual(new long[] { 801, 803 }) &&
                   merged.Diagnostics.Count == 1 && merged.Diagnostics[0].Page == 2,
                "page two failure must preserve aggregate and page one candidates, reject video, and surface one diagnostic");
        }

        private static void StopsTypedRetrievalAfterSessionFailure()
        {
            Assert(!BilibiliApi.ShouldAttemptTypedSearch(new SearchResult { SessionAvailable = false }) &&
                   BilibiliApi.ShouldAttemptTypedSearch(new SearchResult { SessionAvailable = true }),
                "a provider-wide session failure must stop typed requests, while an established session may continue");
        }

        private static void PropagatesPartialDiagnosticsToMovieAndSeasonSearch()
        {
            var bilibili = new PartialBilibiliScraper();
            var other = new SuccessfulScraper();
            var scrapers = new AbstractScraper[] { bilibili, other };

            var movie = DanmuMatchSearchEngine.SearchMovieAsync(
                scrapers,
                new Movie { Name = "谍影重重", ProductionYear = 2002 },
                null,
                null).GetAwaiter().GetResult();
            Assert(movie.CanonicalCandidates.Any(candidate => candidate.Site == "BilibiliID") &&
                   movie.CanonicalCandidates.Any(candidate => candidate.Site == "OtherID"),
                "a typed-page failure must retain Bilibili and cross-provider Movie candidates");
            Assert(movie.CompletionDiagnostics.Any(diagnostic =>
                    diagnostic.Provider == "BilibiliID" && diagnostic.Status == "partial_failure"),
                "the Movie consumer must receive the Bilibili typed-page diagnostic");

            var season = DanmuMatchSearchEngine.SearchSeasonAsync(
                scrapers,
                "测试动画",
                "测试动画 第一季",
                2024,
                12,
                "测试动画",
                null).GetAwaiter().GetResult();
            Assert(season.CanonicalCandidates.Any(candidate => candidate.Site == "BilibiliID") &&
                   season.CanonicalCandidates.Any(candidate => candidate.Site == "OtherID"),
                "a typed-page failure must retain Bilibili and cross-provider Season candidates");
            Assert(season.CompletionDiagnostics.Any(diagnostic =>
                    diagnostic.Provider == "BilibiliID" && diagnostic.Status == "partial_failure"),
                "the Season consumer must receive the same Bilibili typed-page diagnostic channel");
        }

        private static ScraperSearchResult Result(ScraperSearchInfo candidate, bool diagnostic)
        {
            var result = new ScraperSearchResult
            {
                Candidates = new List<ScraperSearchInfo> { candidate }
            };
            if (diagnostic)
            {
                result.Diagnostics.Add(new ScraperSearchDiagnostic
                {
                    Status = "partial_failure",
                    Message = "Bilibili media_ft page 2: fixture failure",
                });
            }

            return result;
        }

        private sealed class PartialBilibiliScraper : FixtureScraper
        {
            public PartialBilibiliScraper() : base("BilibiliID", true)
            {
            }
        }

        private sealed class SuccessfulScraper : FixtureScraper
        {
            public SuccessfulScraper() : base("OtherID", false)
            {
            }
        }

        private abstract class FixtureScraper : AbstractScraper
        {
            private readonly string _providerId;
            private readonly bool _diagnostic;

            protected FixtureScraper(string providerId, bool diagnostic) : base(null)
            {
                _providerId = providerId;
                _diagnostic = diagnostic;
            }

            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;

            public override Task<ScraperSearchResult> SearchWithDiagnostics(
                BaseItem item,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Result(new ScraperSearchInfo
                {
                    Id = _providerId + "-movie",
                    Name = "谍影重重",
                    Category = "电影",
                    Year = 2002,
                    EpisodeSize = 1,
                }, _diagnostic));
            }

            public override Task<ScraperSearchResult> SearchForApiWithDiagnostics(
                string keyword,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Result(new ScraperSearchInfo
                {
                    Id = _providerId + "-season",
                    Name = "测试动画 第一季",
                    Category = "动画",
                    Year = 2024,
                    EpisodeSize = 12,
                }, _diagnostic));
            }

            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) =>
                Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) =>
                Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) =>
                Task.FromResult<ScraperDanmaku>(null);
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
