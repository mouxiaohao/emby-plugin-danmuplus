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

namespace Emby.Plugin.Danmu.Scraper.Iqiyi
{
    public class Iqiyi : AbstractScraper
    {
        public const string ScraperProviderName = "爱奇艺";
        public const string ScraperProviderId = "IqiyiID";

        private readonly IqiyiApi _api;

        public Iqiyi(IHttpClient httpClient, ILogManager logManager) : base(logManager.getDefaultLogger("Iqiyi"))
        {
            _api = new IqiyiApi(logManager, httpClient);
        }

        public override int DefaultOrder => 4;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                if (isMovieItemType && video.ChannelName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.ChannelName == "电影")
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{video.LinkId}",
                    Name = video.Name,
                    Category = video.ChannelName,
                    Year = video.Year,
                    EpisodeSize = video.ItemTotalNumber,
                });
            }


            return list;
        }

        public override async Task<string?> SearchMediaId(BaseItem item)
        {
            if (item is Season season)
            {
                var preview = await GetMatchPreview(season, null, false).ConfigureAwait(false);
                if (preview.AutoSelected && !string.IsNullOrEmpty(preview.SelectedId))
                {
                    log.LogInformation(
                        "[IQIYI] 智能匹配成功: {0} -> {1}, id={2}, score={3}",
                        season.Name,
                        preview.Candidates.FirstOrDefault(x => x.Id == preview.SelectedId)?.Name,
                        preview.SelectedId,
                        preview.Candidates.FirstOrDefault(x => x.Id == preview.SelectedId)?.Score);
                    return preview.SelectedId;
                }

                log.LogInformation(
                    "[IQIYI] 智能匹配未达到自动选择条件: season={0}, status={1}, message={2}",
                    season.Name,
                    preview.Status,
                    preview.Message);
                return null;
            }

            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var title = video.Name;
                var pubYear = video.Year;

                if (isMovieItemType && video.ChannelName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.ChannelName == "电影")
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
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. Iqiyi：{1} jellyfin: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return video.LinkId;
            }

            return null;
        }

        public async Task<DanmuSeasonMatchResult> GetMatchPreview(
            Season season,
            string keywordOverride,
            bool forceSearch)
        {
            var parent = season.GetParent();
            var seriesName = parent?.Name ?? string.Empty;
            var seasonName = season.Name ?? seriesName;
            var expectedEpisodes = 0;
            var episodeResult = season.GetEpisodes();
            if (episodeResult != null)
            {
                expectedEpisodes = episodeResult.Items.Count(x =>
                    x.ParentIndexNumber.HasValue && x.ParentIndexNumber.Value > 0);
            }

            var result = new DanmuSeasonMatchResult
            {
                SeasonId = season.Id.ToString(),
                SeasonName = seasonName,
                SeriesName = seriesName,
                SeasonNumber = season.IndexNumber,
                Year = season.ProductionYear,
                EpisodeCount = expectedEpisodes,
                Keyword = DanmuMatchScorer.ExtractSeasonKeyword(seriesName, seasonName),
            };

            var keywords = DanmuMatchScorer.BuildSearchKeywords(seriesName, seasonName, keywordOverride);
            var sources = new Dictionary<string, ScraperSearchInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyword in keywords)
            {
                var videos = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
                foreach (var video in videos)
                {
                    var id = video.LinkId;
                    if (string.IsNullOrWhiteSpace(id) || video.ChannelName == "电影")
                    {
                        continue;
                    }

                    sources[id] = new ScraperSearchInfo
                    {
                        Id = id,
                        Name = video.Name,
                        Category = video.ChannelName,
                        Year = video.Year,
                        EpisodeSize = video.ItemTotalNumber,
                    };
                }
            }

            result.Candidates = sources.Values
                .Select(source => DanmuMatchScorer.Score(
                    source,
                    ProviderId,
                    ProviderName,
                    DefaultOrder,
                    seriesName,
                    seasonName,
                    season.ProductionYear,
                    expectedEpisodes))
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.EpisodeScore)
                .ThenByDescending(candidate => candidate.YearScore)
                .Take(50)
                .ToList();

            string manualId = null;
            if (season.ProviderIds != null)
            {
                season.ProviderIds.TryGetValue(ScraperProviderId + "Manual", out manualId);
            }

            if (!forceSearch && !string.IsNullOrWhiteSpace(manualId))
            {
                var bound = result.Candidates.FirstOrDefault(x =>
                    string.Equals(x.Id, manualId, StringComparison.OrdinalIgnoreCase));
                if (bound == null)
                {
                    bound = new DanmuMatchCandidate
                    {
                        Id = manualId,
                        Name = "已手动绑定的爱奇艺项目",
                        Score = 1,
                        ManualBound = true,
                        Reason = "使用已保存的手动绑定",
                    };
                    result.Candidates.Insert(0, bound);
                }
                else
                {
                    bound.ManualBound = true;
                    bound.Score = 1;
                    bound.Reason = "使用已保存的手动绑定";
                    result.Candidates = result.Candidates
                        .OrderByDescending(x => x.ManualBound)
                        .ThenByDescending(x => x.Score)
                        .ToList();
                }

                result.Status = "bound";
                result.Message = "使用已经保存的手动匹配";
                result.AutoSelected = true;
                result.SelectedId = manualId;
                return result;
            }

            if (result.Candidates.Count == 0)
            {
                result.Status = "no_match";
                result.Message = "没有搜索到爱奇艺候选项目";
                return result;
            }

            if (DanmuMatchScorer.CanAutoSelect(result.Candidates))
            {
                result.Status = "matched";
                result.Message = "已根据季名、父剧名、年份和集数选出高置信度结果";
                result.AutoSelected = true;
                result.SelectedId = result.Candidates[0].Id;
                return result;
            }

            result.Status = result.Candidates[0].Score >= 0.62 ? "ambiguous" : "no_match";
            result.Message = result.Status == "ambiguous"
                ? "存在多个接近的结果，需要手动选择"
                : "自动评分不足，需要手动选择或换关键词搜索";
            return result;
        }


        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // id是编码后的
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var video = await _api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }


            var media = new ScraperMedia();
            media.Id = id; // 使用url编码后的id (LinkId)
            media.ProviderId = this.ProviderId;
            if (isMovieItemType && video.Epsodelist != null && video.Epsodelist.Count > 0)
            {
                var tvId = video.Epsodelist[0].TvId;
                if (tvId > 0) // 确保 TvId 有效
                {
                    media.CommentId = $"{tvId}";
                    log.Info($"[IQIYI] GetMedia: 电影 '{item.Name}' (LinkId: {id}) 设置 CommentId 为 TvId: {tvId}");
                }
                else
                {
                    log.Warn($"[IQIYI] GetMedia: 电影 '{item.Name}' (LinkId: {id}) 获取到的 TvId 无效 ({tvId})，media.CommentId 将为空。");
                }
            }

            if (video.Epsodelist != null && video.Epsodelist.Count > 0)
            {
                foreach (var ep in video.Epsodelist)
                {
                    if (EpisodeContentClassifier.IsExplicitNonMain(ep.Name))
                    {
                        continue;
                    }

                    var episodeTvId = ep.TvId;
                    var episodeCommentId = (episodeTvId > 0) ? $"{episodeTvId}" : string.Empty;
                    if (episodeTvId <= 0) {
                        log.Warn($"[IQIYI] GetMedia: 剧集 '{ep.Name}' (LinkId: {ep.LinkId}) 的 TvId 无效 ({episodeTvId})，ScraperEpisode.CommentId 将为空。");
                    }
                    media.Episodes.Add(new ScraperEpisode() { Id = $"{ep.LinkId}", CommentId = episodeCommentId, Title = ep.Name });
                }
            }

            return media;
        }

        /// <inheritdoc />
        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // id是编码后的
            var video = await _api.GetVideoBaseAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                return null;
            }
            var tvId = video.TvId;
            if (tvId <= 0)
            {
                log.Warn($"[IQIYI] GetMediaEpisode: 对于 LinkId '{id}', 从 GetVideoBaseAsync 获取的 TvId 无效: {tvId}. 返回的 ScraperEpisode 的 CommentId 将为空。");
            }
            var commentIdForEpisode = (tvId > 0) ? $"{tvId}" : string.Empty;
            return new ScraperEpisode() { Id = id, CommentId = commentIdForEpisode, Title = video.VideoName };
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            if (!long.TryParse(commentId, out var tvIdNumeric) || tvIdNumeric <= 0)
            {
                log.Warn($"[IQIYI] GetDanmuContent: 接收到无效的 TvId ('{commentId}') 用于获取弹幕。项目: '{item?.Name ?? "未知项目"}'.");
                return null; // TvId 无效，无法获取弹幕
            }

            var comments = await _api.GetDanmuContentAsync(commentId, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = commentId.ToLong();
            danmaku.ChatServer = "cmts.iqiyi.com";
            danmaku.ProviderId = ProviderId;
            foreach (var comment in comments)
            {
                try
                {
                    var danmakuText = new ScraperDanmakuText();
                    danmakuText.Progress = (int)comment.ShowTime * 1000;
                    danmakuText.Mode = 1;
                    danmakuText.MidHash = $"[iqiyi]{comment.UserInfo.Uid}";
                    danmakuText.Id = comment.ContentId.ToLong();
                    danmakuText.Content = comment.Content;
                    if (uint.TryParse(comment.Color, System.Globalization.NumberStyles.HexNumber, null, out var color))
                    {
                        danmakuText.Color = color;
                    }

                    danmaku.Items.Add(danmakuText);
                }
                catch (Exception ex)
                {
                }
            }
            
            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }


        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            var videos = await this._api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{video.LinkId}",
                    Name = video.Name,
                    Category = video.ChannelName,
                    Year = video.Year,
                    EpisodeSize = video.ItemTotalNumber,
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

            if (video.Epsodelist != null && video.Epsodelist.Count > 0)
            {
                foreach (var ep in video.Epsodelist)
                {
                    list.Add(new ScraperEpisode() { Id = $"{ep.LinkId}", CommentId = $"{ep.TvId}", Title = ep.Name });
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
