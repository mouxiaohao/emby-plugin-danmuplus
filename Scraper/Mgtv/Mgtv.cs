using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Common.Net;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using System.Collections.Generic;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Mgtv
{
    public class Mgtv : AbstractScraper
   {
        public const string ScraperProviderName = "芒果TV";
        public const string ScraperProviderId = "MgtvID";

        private readonly MgtvApi _api;

        public Mgtv(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Mgtv"))
        {
            _api = new MgtvApi(logManager, httpClient);
        }

        public override int DefaultOrder => 6;

        public override bool DefaultEnable => true;

        public override string Name => "芒果TV";

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;


        private static readonly Regex regTvEpisodeTitle = new Regex(@"^第.+?集$", RegexOptions.Compiled);

        public override Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            return Search(item, CancellationToken.None);
        }

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var list = new List<ScraperSearchInfo>();
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await this._api.SearchAsync(searchName, cancellationToken).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var videoId = video.Id;
                var title = video.Title;
                var pubYear = video.Year;

                if (isMovieItemType && video.TypeName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.TypeName == "电影")
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{videoId}",
                    Name = title,
                    Category = video.TypeName,
                    Year = pubYear,
                    EpisodeSize = video.VideoCount,
                });
            }


            return list;
        }

        public override async Task<string?> SearchMediaId(BaseItem item)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var videoId = video.Id;
                var title = video.Title;
                var pubYear = video.Year;

                if (isMovieItemType && video.TypeName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.TypeName == "电影")
                {
                    continue;
                }

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.LogDebug("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score:　{2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. year: {1} jellyfin: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return video.Id;
            }

            return null;
        }


        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var video = await _api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }


            var media = new ScraperMedia();
            media.Id = id;
            if (isMovieItemType && video.EpisodeList != null && video.EpisodeList.Count > 0)
            {
                media.CommentId = $"{id},{video.EpisodeList[0].VideoId}";
            }
            if (video.EpisodeList != null && video.EpisodeList.Count > 0)
            {
                foreach (var ep in video.EpisodeList)
                {
                    if (EpisodeContentClassifier.IsExplicitNonMain(ep.Title) ||
                        EpisodeContentClassifier.IsExplicitNonMain(ep.Title2))
                    {
                        continue;
                    }

                    media.Episodes.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.VideoId}",
                        CommentId = $"{id},{ep.VideoId}",
                        Title = ep.Title,
                        EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(ep.Title2) ??
                            EpisodeContentClassifier.TryGetEpisodeNumber(ep.Title),
                    });
                }
            }

            if (media.Episodes.Count > 0)
            {
                // Exact-ID details reliably establish only this usable episode list.
                media.EpisodeCount = media.Episodes.Count;
            }

            return media;
        }

        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            if (isMovieItemType)
            {
                var video = await _api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
                if (video == null || video.EpisodeList == null || video.EpisodeList.Count <= 0)
                {
                    return null;
                }

                var firstEpisode = video.EpisodeList[0];
                return new ScraperEpisode()
                {
                    Id = id,
                    CommentId = $"{id},{firstEpisode.VideoId}",
                    Title = firstEpisode.Title,
                    EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(firstEpisode.Title2) ??
                        EpisodeContentClassifier.TryGetEpisodeNumber(firstEpisode.Title),
                };
            }


            // MGTV has no single-episode detail endpoint in the current API.
            // Verify the exact VideoId against its own saved collection detail
            // instead of accepting the local Episode ProviderId as evidence.
            var episodeItem = item as MediaBrowser.Controller.Entities.TV.Episode;
            var season = episodeItem?.Season;
            if (season?.ProviderIds == null ||
                !season.ProviderIds.TryGetValue(ScraperProviderId, out var seasonId) ||
                string.IsNullOrWhiteSpace(seasonId))
            {
                return null;
            }

            var seasonVideo = await _api.GetVideoAsync(seasonId, CancellationToken.None).ConfigureAwait(false);
            var sourceEpisode = seasonVideo?.EpisodeList?.FirstOrDefault(x =>
                string.Equals(x.VideoId, id, StringComparison.OrdinalIgnoreCase));
            if (sourceEpisode == null)
            {
                return null;
            }

            return new ScraperEpisode()
            {
                Id = sourceEpisode.VideoId,
                CommentId = $"{seasonId},{sourceEpisode.VideoId}",
                Title = sourceEpisode.Title,
                EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(sourceEpisode.Title2) ??
                    EpisodeContentClassifier.TryGetEpisodeNumber(sourceEpisode.Title),
            };
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            var arr = commentId.Split(',');
            if (arr.Length < 2)
            {
                return null;
            }

            var cid = arr[0];
            var vid = arr[1];
            if (string.IsNullOrEmpty(cid) || string.IsNullOrEmpty(vid))
            {
                return null;
            }
            var comments = await _api.GetDanmuContentAsync(cid, vid, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = vid.ToLong();
            danmaku.ChatServer = "galaxy.bz.mgtv.com";
            danmaku.ProviderId = ScraperProviderId;
            foreach (var comment in comments)
            {

                var danmakuText = new ScraperDanmakuText();
                danmakuText.Progress = comment.Time;
                // 映射弹幕模式 (0:滚动 1:顶部 2:底部)
                danmakuText.Mode = comment.Type switch
                {
                    1 => 5, // 顶部
                    2 => 4, // 底部
                    _ => 1  // 默认滚动
                };
                danmakuText.MidHash = $"[mgtv]{comment.Uuid}";
                danmakuText.Id = comment.Id;
                danmakuText.Content = comment.Content;
                if (comment.Color != null && comment.Color.ColorLeft != null)
                {
                    danmakuText.Color = comment.Color.ColorLeft.HexNumber;
                }

                danmaku.Items.Add(danmakuText);
            }

            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }


        public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            return SearchForApi(keyword, CancellationToken.None);
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(
            string keyword,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var list = new List<ScraperSearchInfo>();
            var videos = await this._api.SearchAsync(keyword, cancellationToken).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var videoId = video.Id;
                var title = video.Title;
                var pubYear = video.Year;
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{videoId}",
                    Name = title,
                    Category = video.TypeName,
                    Year = pubYear,
                    EpisodeSize = video.VideoCount,
                });
            }
            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var video = await this._api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                return list;
            }

            if (video.EpisodeList != null && video.EpisodeList.Count > 0)
            {
                foreach (var ep in video.EpisodeList)
                {
                    var title = regTvEpisodeTitle.IsMatch(ep.Title2) ? ep.Title2 : ep.Title;
                    list.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.VideoId}",
                        CommentId = $"{id},{ep.VideoId}",
                        Title = title,
                        EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(ep.Title2) ??
                            EpisodeContentClassifier.TryGetEpisodeNumber(ep.Title),
                    });
                }
            }

            return list;
        }

        public override async Task<ScraperDanmaku?> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }
    }
}
