using System.Web;
using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Youku.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper.Youku
{
    public class Youku : AbstractScraper
    {
        public const string ScraperProviderName = "优酷";
        public const string ScraperProviderId = "YoukuID";

        private readonly YoukuApi _api;

        public Youku(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Tencent"))
        {
            _api = new YoukuApi(logManager, httpClient);
        }

        public override int DefaultOrder => 3;

        public override bool DefaultEnable => true;

        public override string Name => "优酷";

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

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
                var videoId = video.ID;
                var title = video.Title;
                var pubYear = video.Year;

                if (isMovieItemType && video.Type != "movie")
                {
                    continue;
                }

                if (!isMovieItemType && video.Type == "movie")
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{videoId}",
                    Name = title,
                    Category = video.Type == "movie" ? "电影" : "电视剧",
                    Year = pubYear,
                    EpisodeSize = video.Total,
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
                var videoId = video.ID;
                var title = video.Title;
                var pubYear = video.Year;

                if (isMovieItemType && video.Type != "movie")
                {
                    continue;
                }

                if (!isMovieItemType && video.Type == "movie")
                {
                    continue;
                }

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.Info("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score:　{2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.Info("[{0}] 发行年份不一致，忽略处理. Youku：{1} emby: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return $"{videoId}";
            }

            return null;
        }


        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            id = HttpUtility.UrlDecode(id);
            var video = await _api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var media = new ScraperMedia();
            media.ProviderId = this.ProviderId; // 设置 ProviderId
            media.Title = video.Title ?? string.Empty;
            media.Year = video.Year;
            media.Category = video.Type ?? string.Empty;
            if (video.Videos != null && video.Videos.Count > 0)
            {
                foreach (var ep in video.Videos)
                {
                    if (EpisodeContentClassifier.IsExplicitNonMain(ep.Title) ||
                        EpisodeContentClassifier.IsExplicitNonMain(ep.RCTitle))
                    {
                        continue;
                    }

                    media.Episodes.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.ID}",
                        CommentId = $"{ep.ID}",
                        Title = ep.Title,
                        EpisodeNumber = EpisodeContentClassifier.TryGetPositiveNumber(ep.Seq) ??
                            EpisodeContentClassifier.TryGetEpisodeNumber(ep.Title),
                    });
                }
            }

            // 优酷的id包括非法的=符号，会导致jellyfin自动删除，这里做下encode
            if (media.Episodes.Count > 0)
            {
                // Exact-ID details reliably establish only this usable episode list.
                media.EpisodeCount = media.Episodes.Count;
            }

            if (isMovieItemType)
            {
                media.Id = HttpUtility.UrlEncode(id);
                media.CommentId = media.Episodes.Count > 0 ? $"{media.Episodes[0].CommentId}" : "";
            }
            else
            {
                media.Id = HttpUtility.UrlEncode(id);
            }

            return media;
        }

        internal static List<ScraperMoviePart> BuildMovieParts(IEnumerable<YoukuEpisode> episodes)
        {
            var parts = new List<ScraperMoviePart>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var episode in episodes ?? Enumerable.Empty<YoukuEpisode>())
            {
                if (episode == null || string.IsNullOrWhiteSpace(episode.ID) ||
                    !seen.Add(episode.ID.Trim()))
                {
                    continue;
                }

                index++;
                var title = !string.IsNullOrWhiteSpace(episode.Title)
                    ? episode.Title.Trim()
                    : (episode.RCTitle ?? string.Empty).Trim();
                parts.Add(new ScraperMoviePart
                {
                    Id = episode.ID.Trim(),
                    Title = title,
                    Index = index,
                    IsDownloadable = true,
                    IsExplicitNonMain = EpisodeContentClassifier.IsExplicitNonMain(episode.Title) ||
                        EpisodeContentClassifier.IsExplicitNonMain(episode.RCTitle),
                });
            }
            return parts;
        }

        public override async Task<List<ScraperMoviePart>> GetMovieParts(
            BaseItem item,
            string parentId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!(item is MediaBrowser.Controller.Entities.Movies.Movie) || string.IsNullOrWhiteSpace(parentId))
            {
                return new List<ScraperMoviePart>();
            }

            var video = await _api.GetVideoAsync(HttpUtility.UrlDecode(parentId), cancellationToken)
                .ConfigureAwait(false);
            return BuildMovieParts(video?.Videos);
        }

        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            id = HttpUtility.UrlDecode(id);
            var sourceEpisode = await _api.GetEpisodeAsync(id, CancellationToken.None).ConfigureAwait(false);
            var expectedId = id.Replace("=", "_");
            if (sourceEpisode == null || string.IsNullOrWhiteSpace(sourceEpisode.ID) ||
                !string.Equals(sourceEpisode.ID, expectedId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            SourceMetadata sourceMetadata = null;
            if (!string.IsNullOrWhiteSpace(sourceEpisode.ShowId))
            {
                var parent = await _api.GetVideoAsync(sourceEpisode.ShowId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (parent != null)
                {
                    sourceMetadata = new SourceMetadata
                    {
                        Title = parent.Title ?? sourceEpisode.ShowTitle ?? string.Empty,
                        Year = parent.Year,
                        Category = parent.Type ?? sourceEpisode.Category ?? string.Empty,
                    };
                }
            }

            if (sourceMetadata == null && !string.IsNullOrWhiteSpace(sourceEpisode.ShowTitle))
            {
                sourceMetadata = new SourceMetadata
                {
                    Title = sourceEpisode.ShowTitle,
                    Category = sourceEpisode.Category ?? string.Empty,
                };
            }

            return new ScraperEpisode()
            {
                Id = sourceEpisode.ID,
                CommentId = sourceEpisode.ID,
                Title = sourceEpisode.Title,
                EpisodeNumber = EpisodeContentClassifier.TryGetPositiveNumber(sourceEpisode.Seq) ??
                    EpisodeContentClassifier.TryGetEpisodeNumber(sourceEpisode.Title),
                SourceMetadata = sourceMetadata,
            };
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            var comments = await _api.GetDanmuContentAsync(commentId, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = 1000;
            danmaku.ChatServer = "acs.youku.com";
            danmaku.ProviderId = ProviderId;
            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText
                {
                    Progress = (int)comment.Playat,
                    Mode = 1,
                    MidHash = $"[youku]{comment.Uid}",
                    Id = comment.ID,
                    Content = comment.Content
                };

                try
                {
                    var property = SingletonManager.JsonSerializer.DeserializeFromString<YoukuCommentProperty>(comment.Propertis);
                    if (property != null)
                    {
                        danmakuText.Color = property.Color;
                    }
                }
                catch (Exception ex)
                {
                    log.Debug("优酷弹幕属性解析失败，保留弹幕正文。commentId={0}, error={1}", comment.ID, ex.Message);
                }

                danmaku.Items.Add(danmakuText);
            }

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
                var videoId = video.ID;
                var title = video.Title;
                var pubYear = video.Year;

                var score = keyword.Distance(title);
                if (score <= 0)
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{videoId}",
                    Name = title,
                    Category = video.Type == "movie" ? "电影" : "电视剧",
                    Year = pubYear,
                    EpisodeSize = video.Total,
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

            if (video.Videos != null && video.Videos.Count > 0)
            {
                foreach (var ep in video.Videos)
                {
                    list.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.ID}",
                        CommentId = $"{ep.ID}",
                        Title = ep.Title,
                        EpisodeNumber = EpisodeContentClassifier.TryGetPositiveNumber(ep.Seq) ??
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
