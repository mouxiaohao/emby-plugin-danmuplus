using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using DandanEpisode = Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode;

namespace Emby.Plugin.Danmu.Scraper.Dandan
{
    public class Dandan : AbstractScraper
    {
        public const string ScraperProviderName = "弹弹play";
        public const string ScraperProviderId = "DandanID";

        private readonly DandanApi _api;

        public Dandan(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Dandan"))
        {
            _api = new DandanApi(logManager, jsonSerializer, httpClient);
        }

        public override int DefaultOrder => 2;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

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
            var animes = await this._api.SearchAsync(searchName, cancellationToken).ConfigureAwait(false);
            foreach (var anime in animes)
            {
                var animeId = anime.AnimeId;
                var title = anime.AnimeTitle;
                var pubYear = anime.Year;

                if (isMovieItemType && anime.Type != "movie")
                {
                    continue;
                }

                if (!isMovieItemType && anime.Type == "movie")
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{animeId}",
                    Name = title,
                    Category = anime.TypeDescription,
                    Year = pubYear,
                    EpisodeSize = anime.EpisodeCount ?? 0,
                });
            }

            return list;
        }

        public override async Task<string?> SearchMediaId(BaseItem item)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var animes = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var anime in animes)
            {
                var animeId = anime.AnimeId;
                var title = anime.AnimeTitle;
                var pubYear = anime.Year;

                if (isMovieItemType && anime.Type != "movie")
                {
                    continue;
                }

                if (!isMovieItemType && anime.Type == "movie")
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
                    log.Info("[{0}] 发行年份不一致，忽略处理. dandan：{1} jellyfin: {2}", title, pubYear, itemPubYear);
                    continue;
                }
                
                return $"{animeId}";
            }

            return null;
        }


        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            var animeId = id.ToLong();
            if (animeId <= 0)
            {
                return null;
            }


            var anime = await _api.GetAnimeAsync(animeId, CancellationToken.None).ConfigureAwait(false);
            if (anime == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, animeId);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var media = new ScraperMedia();

            media.Id = id;
            media.ProviderId = this.ProviderId; // 设置 ProviderId
            media.Title = anime.AnimeTitle ?? string.Empty;
            media.Year = anime.Year;
            media.Category = anime.TypeDescription ?? string.Empty;
            var normalizeSeasonOrdinals = item is Season;
            media.Episodes.AddRange(DandanSeasonEpisodeMapper.Map(
                anime.Episodes, normalizeSeasonOrdinals));
            media.EpisodeCount = normalizeSeasonOrdinals
                ? media.Episodes.Count
                : anime.EpisodeCount;

            if (isMovieItemType && media.Episodes.Count > 0)
            {
                media.CommentId = media.Episodes[0].CommentId;
            }


            return media;
        }

        internal static List<ScraperMoviePart> BuildMovieParts(
            long parentId,
            IEnumerable<DandanEpisode> episodes)
        {
            var parts = new List<ScraperMoviePart>();
            var seen = new HashSet<long>();
            var index = 0;
            foreach (var episode in episodes ?? Enumerable.Empty<DandanEpisode>())
            {
                var episodeId = episode?.EpisodeId ?? 0;
                if (episodeId <= 0 || !seen.Add(episodeId) ||
                    !DandanEpisodeId.TryGetAnimeId(episodeId.ToString(), out var verifiedParentId) ||
                    verifiedParentId != parentId)
                {
                    continue;
                }

                index++;
                var title = (episode.EpisodeTitle ?? string.Empty).Trim();
                parts.Add(new ScraperMoviePart
                {
                    Id = episodeId.ToString(),
                    Title = title,
                    Index = index,
                    IsDownloadable = true,
                    IsExplicitNonMain = EpisodeContentClassifier.IsExplicitNonMain(title),
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
            if (!(item is MediaBrowser.Controller.Entities.Movies.Movie) ||
                !long.TryParse(parentId, out var animeId) || animeId <= 0)
            {
                return new List<ScraperMoviePart>();
            }

            var anime = await _api.GetAnimeAsync(animeId, cancellationToken, includeNonMainEpisodes: true)
                .ConfigureAwait(false);
            return BuildMovieParts(animeId, anime?.Episodes);
        }

        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            if (isMovieItemType)
            {
                if (DandanEpisodeId.TryGetAnimeId(id, out var selectedAnimeId))
                {
                    var selectedAnime = await _api.GetAnimeAsync(
                        selectedAnimeId,
                        CancellationToken.None,
                        includeNonMainEpisodes: true).ConfigureAwait(false);
                    return DandanEpisodeId.CreateVerifiedEpisode(id, selectedAnime?.Episodes);
                }

                // id是animeId
                var anime = await _api.GetAnimeAsync(id.ToLong(), CancellationToken.None).ConfigureAwait(false);
                if (anime == null || anime.Episodes == null || anime.Episodes.Count <= 0)
                {
                    return null;
                }

                var firstEpisode = anime.Episodes[0];
                return new ScraperEpisode()
                {
                    Id = id,
                    CommentId = $"{firstEpisode.EpisodeId}",
                    Title = firstEpisode.EpisodeTitle,
                    EpisodeNumber = EpisodeContentClassifier.TryGetPositiveNumber(firstEpisode.EpisodeNumber) ??
                        EpisodeContentClassifier.TryGetEpisodeNumber(firstEpisode.EpisodeTitle),
                };
            }
            else
            {
                // The saved Episode ProviderId identifies one Dandan episode.
                // Its prefix only locates a candidate Anime; the full ID is
                // verified against the detail payload before this becomes exact
                // evidence, so malformed/stale IDs still fail closed.
                if (!DandanEpisodeId.TryGetAnimeId(id, out var animeId))
                {
                    return null;
                }

                var anime = await _api.GetAnimeAsync(
                    animeId,
                    CancellationToken.None,
                    includeNonMainEpisodes: true).ConfigureAwait(false);
                var verified = DandanEpisodeId.CreateVerifiedEpisode(id, anime?.Episodes);
                if (verified != null && anime != null)
                {
                    verified.SourceMetadata = new SourceMetadata
                    {
                        Title = anime.AnimeTitle ?? string.Empty,
                        Year = anime.Year,
                        Category = anime.TypeDescription ?? string.Empty,
                    };
                }
                return verified;
            }
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            var cid = commentId.ToLong();
            if (cid <= 0)
            {
                return null;
            }

            var comments = await _api.GetCommentsAsync(cid, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = cid;
            danmaku.ChatServer = "api.dandanplay.net";
            danmaku.ProviderId = ScraperProviderId;
            foreach (var comment in comments)
            {
                var danmakuText = new ScraperDanmakuText();
                var arr = comment.P.Split(',');
                danmakuText.Progress = (int)(Convert.ToDouble(arr[0]) * 1000);
                danmakuText.Mode = Convert.ToInt32(arr[1]);
                danmakuText.Color = Convert.ToUInt32(arr[2]);
                danmakuText.MidHash = arr[3];
                danmakuText.Id = comment.Cid;
                danmakuText.Content = comment.Text;

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
            log.Info("SearchForApi={0}", keyword);
            var animes = await this._api.SearchAsync(keyword, cancellationToken).ConfigureAwait(false);
            foreach (var anime in animes)
            {
                
                log.Info("查询结果 anime={0}", anime.ToJson());
                var animeId = anime.AnimeId;
                var title = anime.AnimeTitle;
                var pubYear = anime.Year;

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{animeId}",
                    Name = title,
                    Category = anime.TypeDescription,
                    Year = pubYear,
                    EpisodeSize = anime.EpisodeCount ?? 0,
                });
            }

            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var animeId = id.ToLong();
            if (animeId <= 0)
            {
                return list;
            }

            var anime = await this._api.GetAnimeAsync(animeId, CancellationToken.None).ConfigureAwait(false);
            if (anime == null)
            {
                return list;
            }

            if (anime.Episodes != null && anime.Episodes.Count > 0)
            {
                foreach (var ep in anime.Episodes)
                {
                    list.Add(new ScraperEpisode()
                    {
                        Id = $"{ep.EpisodeId}",
                        CommentId = $"{ep.EpisodeId}",
                        Title = ep.EpisodeTitle,
                        EpisodeNumber = EpisodeContentClassifier.TryGetPositiveNumber(ep.EpisodeNumber) ??
                            EpisodeContentClassifier.TryGetEpisodeNumber(ep.EpisodeTitle),
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
