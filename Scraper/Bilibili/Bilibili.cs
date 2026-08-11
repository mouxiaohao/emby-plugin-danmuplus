using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Common.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper.Bilibili
{
    public class Bilibili : AbstractScraper
    {
        public const string ScraperProviderName = "哔哩哔哩";
        public const string ScraperProviderId = "BilibiliID";

        private readonly BilibiliApi _api;

        public Bilibili(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("Bilibili"))
        {
            _api = new BilibiliApi(logManager, httpClient);
        }

        private static int UnixTimeStampToYear(long unixTimeStamp)
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp);
            return dateTimeOffset.Year;
        }

        private static int? TryGetYearFromSeasonDetail(VideoSeasonDetail seasonDetail, VideoSeason seasonInfo)
        {
            var publishTime = seasonDetail?.Publish?.PubTime;
            if (!string.IsNullOrWhiteSpace(publishTime) &&
                DateTime.TryParse(publishTime, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var published))
            {
                return published.Year;
            }

            var episodePubTime = seasonInfo?.Episodes?
                .FirstOrDefault(episode => episode.PubTime > 0)?.PubTime;
            return episodePubTime > 0 ? UnixTimeStampToYear(episodePubTime.Value) : (int?)null;
        }

        private async Task<string> ResolveMoviePgcEpisodeIdAsync(long seasonId)
        {
            if (seasonId <= 0)
            {
                return string.Empty;
            }

            var season = await _api.GetSeasonAsync(seasonId, CancellationToken.None).ConfigureAwait(false);
            var episode = season?.Episodes?.FirstOrDefault(x =>
                x != null && x.Id > 0 && x.CId > 0 &&
                !EpisodeContentClassifier.IsExplicitNonMain(
                    !string.IsNullOrWhiteSpace(x.LongTitle) ? x.LongTitle : x.Title));
            return episode?.Id > 0 ? episode.Id.ToString() : string.Empty;
        }

        public override int DefaultOrder => 1;
        public override bool DefaultEnable => true;
        public override string Name => ScraperProviderName;
        public override string ProviderName => ScraperProviderName;
        public override string ProviderId => ScraperProviderId;


        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            log.Info($"Bilibili.Search - 开始为 Emby 项目搜索: '{item.Name}', 类型: {item.GetType().Name}, 路径: '{item.Path}'");
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            log.Info($"Bilibili.Search - Emby 项目 '{item.Name}' 的标准化搜索名称: '{searchName}'");
            var searchResult = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (searchResult == null || searchResult.Result == null)
                {
                    log.Info($"Bilibili.Search - API 为 '{searchName}' 返回了 null 或空结果列表。");
                    return list;
                }

                foreach (var mediaItem in searchResult.Result) // 将 'video' 重命名为 'mediaItem' 以避免与 Entity.Video 混淆
                {
                    // Jellyfin 按 media.Type == "media_ft" || media.Type == "media_bangumi" 进行过滤
                    // 您当前的 Media 实体具有来自 Bilibili API 的 SeasonTypeName。
                    log.Info($"Bilibili.Search - 正在处理 API 结果项: 标题='{mediaItem.Title}', Api类型='{mediaItem.ApiType}', 季度类型名称='{mediaItem.SeasonTypeName}', MediaId='{mediaItem.MediaId}', SeasonId='{mediaItem.SeasonId}', PgcSeasonId='{mediaItem.PgcSeasonId}', 发布时间戳='{mediaItem.PubTime}'");
                    // 假设有效项目的 TypeName 是 "media_ft" (影视) 或 "media_bangumi" (番剧)。
                    // if (mediaItem.TypeName != "media_ft" && mediaItem.TypeName != "media_bangumi")
                    // {
                    //    continue;
                    // }

                    var id = mediaItem.MediaId.ToString(); // Default to MediaId
                    var title = mediaItem.Title;
                    var pubYear = 0; // 默认为 MediaId
                    if (!string.IsNullOrEmpty(mediaItem.PubDate)) // 假设 PubDate 是 "YYYY-MM-DD" 或类似格式
                    {
                        if (DateTime.TryParse(mediaItem.PubDate, out var parsedDate))
                        {
                            pubYear = parsedDate.Year;
                        }
                    } // 回退到 PubTime (Unix 时间戳)
                    else if (mediaItem.PubTime > 0)
                    {
                        pubYear = UnixTimeStampToYear(mediaItem.PubTime);
                    }

                    var episodeSize = mediaItem.EpisodeSize;
                    // In Bilibili API, PGC content (番剧/国创) uses season_id.
                    // UGC content (like 电影 or other user uploads) might use media_id or bvid.
                    // 在 Bilibili API 中，PGC 内容（番剧/国创）使用 season_id。
                    // UGC 内容（如电影或其他用户上传）可能使用 media_id 或 bvid。
                    if (mediaItem.SeasonId > 0) // 如果是基于季度的项目（如番剧）
                    {
                        id = mediaItem.SeasonId.ToString();
                        log.Info($"Bilibili.Search - 为B站项目 '{mediaItem.Title}' 使用 SeasonId '{id}'");
                    }

                    // 使用 B站 API 返回的 SeasonTypeName进行比较
                    if (isMovieItemType && mediaItem.SeasonTypeName != "电影") 
                    {
                        log.Info($"Bilibili.Search - 跳过B站项目 '{mediaItem.Title}' (季度类型: '{mediaItem.SeasonTypeName}') 因为 Emby 项目是电影但B站项目不是 '电影'. Emby 名称: '{item.Name}'");
                        continue;
                    }

                    if (!isMovieItemType && mediaItem.SeasonTypeName == "电影")
                    {
                        log.Info($"Bilibili.Search - 跳过B站项目 '{mediaItem.Title}' (季度类型: '{mediaItem.SeasonTypeName}') 因为 Emby 项目不是电影但B站项目是 '电影'. Emby 名称: '{item.Name}'");
                        continue;
                    }

                    if (isMovieItemType)
                    {
                        var seasonId = mediaItem.SeasonId > 0
                            ? mediaItem.SeasonId
                            : mediaItem.PgcSeasonId;
                        id = await ResolveMoviePgcEpisodeIdAsync(seasonId).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            log.Info("Bilibili.Search - 电影候选没有可验证的 PGC 正片 ep_id，跳过: {0}", title);
                            continue;
                        }
                    }

                    log.Info($"Bilibili.Search - Found candidate for backend scoring: '{item.Name}'. Bili Title: '{title}', ID to use: '{id}', Bili Year: {pubYear}, Bili Type: '{mediaItem.SeasonTypeName ?? mediaItem.ApiType}'");
                    list.Add(new ScraperSearchInfo()
                    {
                        Id = id, // Season uses season_id; Movie uses playable ep_id.
                        Name = title,
                        Category = mediaItem.SeasonTypeName ?? mediaItem.TypeName, // 如果 SeasonTypeName 可用，则优先使用
                        Year = pubYear,
                        EpisodeSize = episodeSize,
                    });
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Bilibili.Search - 处理 Emby 项目 '{0}' (标准化名称: '{1}') 时发生错误", item.Name, searchName);
            }

            log.Info($"Bilibili.Search - Emby 项目 '{item.Name}' 的搜索已完成。找到 {list.Count} 个潜在的 ScraperSearchInfo 匹配项。");
            return list;
        }

        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var candidates = new List<ScraperSearchInfo>();
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return candidates;
            }

            keyword = keyword.Trim();
            log.Info("Bilibili.SearchForApi - searching keyword '{0}'.", keyword);
            var searchResult = await _api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            var mediaItems = searchResult?.Result ??
                new List<Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media>();
            var skipped = 0;

            foreach (var mediaItem in mediaItems)
            {
                if (!BilibiliSearchResultMapper.TryMap(mediaItem, out var candidate, out var skipReason))
                {
                    skipped++;
                    log.Debug(
                        "Bilibili.SearchForApi - skipped result for keyword '{0}': {1}.",
                        keyword,
                        skipReason);
                    continue;
                }

                candidates.Add(candidate);
                log.Debug(
                    "Bilibili.SearchForApi - mapped candidate: id={0}, title='{1}', year={2}, episodes={3}, category='{4}'.",
                    candidate.Id,
                    candidate.Name,
                    candidate.Year,
                    candidate.EpisodeSize,
                    candidate.Category);
            }

            log.Info(
                "Bilibili.SearchForApi - keyword '{0}' returned {1} records; mapped {2}, skipped {3}.",
                keyword,
                mediaItems.Count,
                candidates.Count,
                skipped);
            return candidates;
        }

        public override async Task<string?> SearchMediaId(BaseItem item)
        {
            log.Info($"Bilibili.SearchMediaId - 开始为 Emby 项目搜索媒体ID: '{item.Name}', 路径: '{item.Path}'");
            string? matchedId = null;
            try
            {
                var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
                var searchName = this.NormalizeSearchName(item.Name);
                log.Info($"Bilibili.SearchMediaId - Emby 项目 '{item.Name}' 的标准化搜索名称: '{searchName}'");
                var searchResult = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
                if (searchResult == null || searchResult.Result == null)
                {
                    log.Info($"Bilibili.SearchMediaId - API 为 '{searchName}' 返回了 null 或空结果列表。");
                    return null;
                }

                log.Info($"Bilibili.SearchMediaId - API 为 '{searchName}' 返回了 {searchResult.Result.Count} 个结果。Emby 项目: '{item.Name}'");
                foreach (var mediaItem in searchResult.Result)
                {
                    log.Info($"Bilibili.SearchMediaId - Processing API result item: Title='{mediaItem.Title}', ApiType='{mediaItem.ApiType}', SeasonTypeName='{mediaItem.SeasonTypeName}', MediaId='{mediaItem.MediaId}', SeasonId='{mediaItem.SeasonId}', PgcSeasonId='{mediaItem.PgcSeasonId}', PubTime='{mediaItem.PubTime}'");
                    string id = mediaItem.MediaId > 0 ? mediaItem.MediaId.ToString() : "0"; // Default to MediaId, ensure it's not 0 before using
                    var title = mediaItem.Title;
                    var pubYear = 0;
                    if (!string.IsNullOrEmpty(mediaItem.PubDate))
                    {
                         if (DateTime.TryParse(mediaItem.PubDate, out var parsedDate))
                        {
                            pubYear = parsedDate.Year;
                        }
                    }
                    else if (mediaItem.PubTime > 0)
                    {
                        pubYear = UnixTimeStampToYear(mediaItem.PubTime);
                    }

                    if (mediaItem.SeasonId > 0) // 如果 SeasonId 可用，则优先使用
                    {
                        id = mediaItem.SeasonId.ToString(); // Prefer SeasonId if available
                        log.Info($"Bilibili.SearchMediaId - 为B站项目 '{mediaItem.Title}' 使用 SeasonId '{id}'。Emby 名称: '{item.Name}'");

                    }

                    // 使用 B站 API 返回的 SeasonTypeName进行比较
                    if (isMovieItemType && mediaItem.SeasonTypeName != "电影")
                    {
                        log.Info($"Bilibili.SearchMediaId - 跳过B站项目 '{mediaItem.Title}' (季度类型: '{mediaItem.SeasonTypeName}') - Emby 项目是电影，B站项目不是 '电影'. Emby 名称: '{item.Name}'");
                        continue;
                    }

                    if (!isMovieItemType && mediaItem.SeasonTypeName == "电影")
                    {
                        log.Info($"Bilibili.SearchMediaId - 跳过B站项目 '{mediaItem.Title}' (季度类型: '{mediaItem.SeasonTypeName}') - Emby 项目不是电影，B站项目是 '电影'. Emby 名称: '{item.Name}'");
                        continue;
                    }

                    var score = searchName.Distance(title);
                    if (score < 0.7)
                    {
                        log.Info($"Bilibili.SearchMediaId - 因标题相似度低 ({score}，阈值: 0.7，对比: '{searchName}') 而跳过B站项目 '{title}'. Emby 名称: '{item.Name}'");
                        continue;
                    }

                    var itemPubYear = item.ProductionYear ?? 0;
                    if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                    {
                        log.Info($"Bilibili.SearchMediaId - 因年份不匹配而跳过B站项目 '{title}'。B站年份: {pubYear}, Emby 年份: {itemPubYear}. Emby 名称: '{item.Name}'");
                        continue;
                    }
                    
                    matchedId = id;
                    log.Info($"Bilibili.SearchMediaId - 为 Emby 项目 '{item.Name}' 找到匹配! B站标题: '{title}', 选定ID: '{matchedId}', 相似度: {score}, B站年份: {pubYear}, Emby 年份: {itemPubYear}"); // 找到一个匹配项
                    break;
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Bilibili.SearchMediaId - 在处理 Emby 项目 '{0}' 期间发生错误", item.Name);
            }

            if (matchedId == null) {
                log.Info($"Bilibili.SearchMediaId - 未为 Emby 项目 '{item.Name}' 找到匹配。");
            }
            log.Info($"Bilibili.SearchMediaId - Emby 项目 '{item.Name}' 的媒体ID搜索已完成。返回的B站ID: '{matchedId ?? "无"}'");
            return matchedId;
        }

        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            log.Info($"Bilibili.GetMedia - 开始为 Emby 项目获取媒体信息: '{item.Name}', 上一步获取的B站ID: '{id}'");
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (!BilibiliPgcIdPolicy.SupportsExactItem(item) ||
                !BilibiliPgcIdPolicy.IsPositiveNumericId(id))
            {
                log.Warn("Bilibili.GetMedia - 仅接受 Movie/Season/Episode 的 PGC 数字标识符: item={0}, id={1}",
                    item?.Name, id);
                return null;
            }

            ScraperMedia? scraperMedia = null;
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var isEpisodeItemType = item is MediaBrowser.Controller.Entities.TV.Episode;

            try
            {
                // id 有多种格式: bvid, season_id (用于 PGC 如番剧), ep_id (用于 PGC 剧集)
                // Jellyfin 也处理 'av' 前缀的 avid。
                if (id.StartsWith("BV", StringComparison.OrdinalIgnoreCase))
                {
                    log.Info($"Bilibili.GetMedia - ID '{id}' 是一个 BVID。正在为 Emby 项目 '{item.Name}' 调用 _api.GetVideoByBvidAsync。");
                    var videoInfo = await _api.GetVideoByBvidAsync(id, CancellationToken.None).ConfigureAwait(false);
                    if (videoInfo != null)
                    {
                        scraperMedia = new ScraperMedia()
                        {
                            Id = id,
                            ProviderId = this.ProviderId,
                            Title = videoInfo.Title ?? string.Empty,
                            Year = videoInfo.Pubdate > 0 ? UnixTimeStampToYear(videoInfo.Pubdate) : (int?)null,
                            EpisodeCount = videoInfo.VideosCount > 0 ? videoInfo.VideosCount : (int?)null,
                        };
                        var aid = videoInfo.Aid ?? 0;
                        if (videoInfo.UgcSeason?.Sections != null && videoInfo.UgcSeason.Sections.Any() &&
                            videoInfo.UgcSeason.Sections[0].Episodes != null)
                        {
                            foreach (var episode in videoInfo.UgcSeason.Sections[0].Episodes)
                            {
                                if (EpisodeContentClassifier.IsExplicitNonMain(episode.Title))
                                {
                                    continue;
                                }

                                var episodeAid = episode.AId ?? aid;
                                var episodeId = episodeAid > 0 ? $"{episodeAid},{episode.CId}" : $"{episode.CId}";
                                scraperMedia.Episodes.Add(new ScraperEpisode
                                {
                                    Id = episodeId,
                                    Title = episode.Title,
                                    CommentId = $"{episode.CId}",
                                    EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(episode.Title),
                                });
                            }

                            // The UGC season response is the resolved
                            // collection. Its filtered usable entries, not the
                            // BVID's generic video count, describe this match.
                            scraperMedia.EpisodeCount = scraperMedia.Episodes.Count;
                        }
                        else if (videoInfo.Pages != null && videoInfo.Pages.Any())
                        {
                            foreach (var page in videoInfo.Pages)
                            {
                                if (EpisodeContentClassifier.IsExplicitNonMain(page.PartName))
                                {
                                    continue;
                                }

                                log.Debug($"GetMedia (BVID: {id}): 添加分P。CID: {page.Cid}, 标题: {page.PartName}");
                                // 对于 BVID，CommentId 通常是分P的 CID。
                                // ScraperEpisode 的 Id 可以是 BVID + 分P号，或者如果 CID 唯一，则仅为 CID。
                                // Jellyfin uses CommentId = page.Cid
                                var episodeId = aid > 0 ? $"{aid},{page.Cid}" : $"{page.Cid}";
                                scraperMedia.Episodes.Add(new ScraperEpisode()
                                {
                                    Id = episodeId,
                                    Title = page.PartName,
                                    CommentId = $"{page.Cid}",
                                    EpisodeNumber = page.Page > 0
                                        ? page.Page
                                        : EpisodeContentClassifier.TryGetEpisodeNumber(page.PartName),
                                });
                            }
                        }
                        else { log.Info($"Bilibili.GetMedia (BVID: {id}): 找到了 '{item.Name}' 的视频信息，但没有列出分P。"); }

                        if (isMovieItemType && scraperMedia.Episodes.Any())
                        {
                            var firstEpisodeCommentId = scraperMedia.Episodes.First().CommentId;
                            scraperMedia.CommentId = firstEpisodeCommentId;
                            log.Info($"Bilibili.GetMedia (BVID: {id}): Emby 项目 '{item.Name}' 是电影。将 ScraperMedia.CommentId 设置为第一个分P的 CID: '{firstEpisodeCommentId}'");
                        }
                    }
                    else { log.Info($"Bilibili.GetMedia (BVID: {id}): _api.GetVideoByBvidAsync 为 '{item.Name}' 返回了 null。"); }
                }
                // else if (id.StartsWith("av", StringComparison.OrdinalIgnoreCase)) { /* TODO: 如果需要，处理 AVID */ }
                else if (long.TryParse(id, out var numericId)) // 可能是 season_id 或 ep_id
                {
                    if (isMovieItemType || isEpisodeItemType)
                    {
                        var exactEpisode = await _api.GetEpisodeAsync(numericId, CancellationToken.None)
                            .ConfigureAwait(false);
                        if (exactEpisode == null || exactEpisode.Id != numericId || exactEpisode.CId <= 0 ||
                            !exactEpisode.AId.HasValue || exactEpisode.AId.Value <= 0)
                        {
                            log.Warn("Bilibili.GetMedia - Movie/Episode ep_id 未通过精确 PGC 验证: {0}", id);
                            return null;
                        }

                        var exactTitle = !string.IsNullOrWhiteSpace(exactEpisode.LongTitle)
                            ? exactEpisode.LongTitle
                            : exactEpisode.Title ?? string.Empty;
                        scraperMedia = new ScraperMedia
                        {
                            Id = id,
                            ProviderId = ProviderId,
                            Title = exactTitle,
                            Year = exactEpisode.PubTime > 0
                                ? UnixTimeStampToYear(exactEpisode.PubTime)
                                : (int?)null,
                            EpisodeCount = 1,
                            CommentId = id,
                            Episodes = new List<ScraperEpisode>
                            {
                                new ScraperEpisode
                                {
                                    Id = id,
                                    CommentId = id,
                                    Title = exactTitle,
                                    EpisodeNumber = 1,
                                },
                            },
                        };
                        return scraperMedia;
                    }

                    log.Info($"Bilibili.GetMedia (数字ID: {numericId}): 假定为 season_id。正在为 Emby 项目 '{item.Name}' 调用 _api.GetSeasonAsync。");
                    // 首先尝试作为季度获取 (用于剧集/番剧)
                    var seasonInfo = await _api.GetSeasonAsync(numericId, CancellationToken.None).ConfigureAwait(false);
                    if (seasonInfo != null && seasonInfo.Episodes != null && seasonInfo.Episodes.Any())
                    {
                        VideoSeasonDetail seasonDetail = null;
                        try
                        {
                            seasonDetail = await _api.GetSeasonDetailAsync(numericId, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Detail is presentation enrichment only: a failure
                            // must not discard ep/list's exact downloadable media.
                            log.Warn($"Bilibili.GetMedia (Season ID: {id}): exact metadata detail unavailable; retaining ep/list media. {ex.Message}");
                        }

                        scraperMedia = new ScraperMedia()
                        {
                            Id = id,
                            ProviderId = this.ProviderId,
                            Title = !string.IsNullOrWhiteSpace(seasonDetail?.Title)
                                ? seasonDetail.Title
                                : seasonDetail?.SeasonTitle ?? string.Empty,
                            Year = TryGetYearFromSeasonDetail(seasonDetail, seasonInfo),
                            EpisodeCount = seasonDetail?.Total > 0
                                ? seasonDetail.Total
                                : seasonInfo.TotalCount > 0 ? seasonInfo.TotalCount : (int?)null,
                        }; // 使用原始 ID (season_id)
                        
                        var episodesToProcess = seasonInfo.Episodes;

                        foreach (var ep in episodesToProcess)
                        {
                            log.Info($"Bilibili.GetMedia (Season ID: {id}): 从季度中添加剧集。ep_id: {ep.Id}, 标题: '{ep.Title}', CID: {ep.CId}, AID: {ep.AId}");
                            string episodeIdentifier = ep.Id.ToString();
                            log.Info($"Bilibili.GetMedia (Season ID: {id}): 创建 ScraperEpisode。使用 ep_id '{episodeIdentifier}' 作为 Id 和 CommentId。原始剧集标题: '{ep.Title}'");
                            scraperMedia.Episodes.Add(new ScraperEpisode() 
                            { 
                                Id = episodeIdentifier, // Ensure this is the ep_id
                                Title = ep.Title ?? string.Empty,
                                CommentId = episodeIdentifier, // Ensure this is also the ep_id
                                EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(ep.Title),
                            });
                        }
                        log.Info($"Bilibili.GetMedia (Season ID: {id}): 在 Emby 项目 '{item.Name}' 的季度中找到 {scraperMedia.Episodes.Count} 个剧集。");
                        if (isMovieItemType && scraperMedia.Episodes.Any())
                        {
                             // 对于电影，ScraperMedia 的 CommentId 也应该是电影本身的 ep_id。
                             scraperMedia.CommentId = scraperMedia.Episodes.First().CommentId; // 这应该是 ep_id (例如 470296)
                             log.Info($"Bilibili.GetMedia (Season ID: {id}): Emby 项目 '{item.Name}' 是电影。将 ScraperMedia.CommentId 设置为第一个剧集的 CommentId: '{scraperMedia.CommentId}'");
                        }
                        else if (!isMovieItemType && scraperMedia.Episodes.Count > 1)
                        {
                            log.Info($"Bilibili.GetMedia (Season ID: {id}): Emby 项目 '{item.Name}' 是剧集。找到 {scraperMedia.Episodes.Count} 个分集。");
                        }
                        else if (scraperMedia.Episodes.Count == 0)
                        {
                            log.Info($"Bilibili.GetMedia (Season ID: {id}): '{item.Name}' 的季度信息未包含任何剧集。");
                        }
                    }
                    else if (isMovieItemType) // 如果 GetSeasonAsync 对于电影失败
                    {
                        log.Info($"Bilibili.GetMedia (Season ID: {id}): _api.GetSeasonAsync 为电影 '{item.Name}' 返回了 null 或没有剧集。ScraperMedia 将为 null。");
                    }
                    else { log.Info($"Bilibili.GetMedia (数字ID: {id}): _api.GetSeasonAsync 为 '{item.Name}' 返回了 null 或没有剧集。"); }
                }
                else { log.Info($"Bilibili.GetMedia - Emby 项目 '{item.Name}' 的 ID '{id}' 不是 BVID 也不是数字ID。无法处理。"); }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Bilibili.GetMedia - 处理 Emby 项目 '{0}' (B站ID: '{1}') 时发生错误", item.Name, id);
            }

            if (scraperMedia == null)
            {
                log.Info($"Bilibili.GetMedia - 无法获取 Emby 项目 '{item.Name}' (B站ID: '{id}') 的媒体信息。返回 null。");
            }
            else
            {
                log.Info($"Bilibili.GetMedia - Emby 项目 '{item.Name}' (B站ID: '{id}') 的媒体信息获取完成。ScraperMedia.CommentId: '{scraperMedia.CommentId}', 剧集数量: {scraperMedia.Episodes.Count}");
            }
            log.Info($"Bilibili.GetMedia - 为 '{item.Name}' 返回 ScraperMedia: {(scraperMedia != null ? "对象，CommentId 为 " + scraperMedia.CommentId : "null")}");
            return scraperMedia;
        }

        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {
            log.Info($"Bilibili.GetMediaEpisode - 开始为 Emby 项目获取媒体剧集信息: '{item?.Name ?? "未知项目"}', 从 GetMedia 获取的ID (应为 ep_id 或 CID): '{id}'");
            if (item == null) {
                log.Warn("Bilibili.GetMediaEpisode - 传入的 BaseItem 为 null。");
            }
            if (string.IsNullOrEmpty(id)) {
                log.Warn($"Bilibili.GetMediaEpisode - 传入的 ID 为 null 或空。Emby 项目: '{item?.Name ?? "未知项目"}'");
                return null;
            }
            if ((!(item is MediaBrowser.Controller.Entities.Movies.Movie) &&
                 !(item is MediaBrowser.Controller.Entities.TV.Episode)) ||
                !BilibiliPgcIdPolicy.IsPositiveNumericId(id))
            {
                log.Warn("Bilibili.GetMediaEpisode - 仅接受 Movie/Episode 的 PGC ep_id: item={0}, id={1}",
                    item?.Name, id);
                return null;
            }
            // 此方法期望 'id' 是可以识别单个剧集弹幕的内容。
            // 对于 PGC 内容 (番剧/动漫), 'id' 通常是 ep_id。
            // 对于 UGC 内容 (来自 BVID 的分P视频), 'id' 应该是该分P的 CID。
            try
            {
                if (id.Contains(","))
                {
                    var parts = id.Split(',');
                    if (parts.Length != 2 ||
                        !long.TryParse(parts[0], out var tupleAid) || tupleAid <= 0 ||
                        !long.TryParse(parts[1], out var tupleCid) || tupleCid <= 0)
                    {
                        return null;
                    }

                    // AID detail is an official exact lookup. Accept a tuple
                    // only when its returned AID and an upstream part/UGC
                    // episode CId both prove the stored pair belongs together.
                    var tupleVideo = await _api.GetVideoByAidAsync(tupleAid, CancellationToken.None).ConfigureAwait(false);
                    if (tupleVideo?.Aid != tupleAid)
                    {
                        return null;
                    }

                    var matchingPage = tupleVideo.Pages?.FirstOrDefault(x => x.Cid == tupleCid);
                    if (matchingPage != null)
                    {
                        return new ScraperEpisode()
                        {
                            Id = $"{tupleAid},{tupleCid}",
                            CommentId = $"{tupleAid},{tupleCid}",
                            Title = matchingPage.PartName,
                            EpisodeNumber = matchingPage.Page > 0
                                ? matchingPage.Page
                                : EpisodeContentClassifier.TryGetEpisodeNumber(matchingPage.PartName),
                        };
                    }

                    var matchingUgcEpisode = tupleVideo.UgcSeason?.Sections?
                        .Where(x => x.Episodes != null)
                        .SelectMany(x => x.Episodes)
                        .FirstOrDefault(x => x.CId == tupleCid);
                    if (matchingUgcEpisode == null)
                    {
                        return null;
                    }

                    return new ScraperEpisode()
                    {
                        Id = $"{tupleAid},{tupleCid}",
                        CommentId = $"{tupleAid},{tupleCid}",
                        Title = matchingUgcEpisode.Title,
                        EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(matchingUgcEpisode.Title),
                    };
                }

                if (id.StartsWith("BV", StringComparison.OrdinalIgnoreCase)) // 理论上 GetMediaEpisode 不应该直接收到 BVID，而是应该收到处理过的 CID 或 ep_id
                {
                    log.Info($"Bilibili.GetMediaEpisode - 为 Emby 项目 '{item?.Name ?? "未知项目"}' 收到了 BVID '{id}'。此方法需要 ep_id 或 CID。尝试获取 BVID 的第一个分P作为备选。");
                    // 这种情况比较棘手。如果 'id' 只是 BVID，那么是哪个分P的剧集信息？
                    // 假设这里的 'id' 实际上可能是 GetMedia 在处理 BVID 时传递过来的 CID。
                    // 或者，调用者需要为某个分P提供更具体的 ID。
                    // 目前，我们假设如果是 BVID，则意味着第一个分P的 CID。
                    var videoInfo = await _api.GetVideoByBvidAsync(id, CancellationToken.None).ConfigureAwait(false);
                    if (videoInfo?.Cid.HasValue == true && videoInfo.Cid.Value > 0)
                    {
                        return new ScraperEpisode() { Id = id, CommentId = $"{videoInfo.Cid}", Title = videoInfo.Title };
                    }

                    if (videoInfo?.Pages != null && videoInfo.Pages.Any())
                    {
                        var firstPart = videoInfo.Pages.First();
                        log.Info($"Bilibili.GetMediaEpisode (BVID: {id}): 对于 Emby 项目 '{item?.Name ?? "未知项目"}', 使用第一个分P。CID: {firstPart.Cid}, 标题: '{firstPart.PartName}'。返回 CID 作为 CommentId。");
                        return new ScraperEpisode()
                        {
                            Id = $"{firstPart.Cid}",
                            CommentId = $"{firstPart.Cid}",
                            Title = firstPart.PartName,
                            EpisodeNumber = firstPart.Page > 0
                                ? firstPart.Page
                                : EpisodeContentClassifier.TryGetEpisodeNumber(firstPart.PartName),
                        };
                    } else {
                        log.Warn($"Bilibili.GetMediaEpisode (BVID: {id}): 无法获取 BVID '{id}' (Emby 项目 '{item?.Name ?? "未知项目"}') 的视频信息或分P。"); // 明确返回 null
                        return null;
                    }
                }
                else if (long.TryParse(id, out var numericId))
                {
                    if (numericId <= 0) {
                        log.Warn($"Bilibili.GetMediaEpisode - 解析出的数字ID '{numericId}' 无效 (来自原始ID '{id}')。Emby 项目: '{item?.Name ?? "未知项目"}'");
                        return null;
                    }
                    log.Info($"Bilibili.GetMediaEpisode (数字ID: {numericId}): 假定为 Emby 项目 '{item?.Name ?? "未知项目"}' 的 ep_id。正在调用 _api.GetEpisodeAsync。");
                    // 这里的 'id' 期望是 ep_id (从 GetMedia 的 ScraperEpisode.CommentId 传递过来)
                    var episodeInfo = await _api.GetEpisodeAsync(numericId, CancellationToken.None).ConfigureAwait(false);
                    if (episodeInfo != null)
                    {
                        string finalCommentId = $"{episodeInfo.Id}"; // Default to ep_id
                        if (episodeInfo.AId.HasValue && episodeInfo.CId > 0)
                        {
                            finalCommentId = $"{episodeInfo.AId.Value},{episodeInfo.CId}";
                            log.Info($"Bilibili.GetMediaEpisode (ep_id: {numericId}): 为 Emby 项目 '{item?.Name ?? "未知项目"}' 构建的 Proto 弹幕 CommentId: '{finalCommentId}' (AID: {episodeInfo.AId.Value}, CID: {episodeInfo.CId})");
                        }
                        else
                        {
                            log.Warn($"Bilibili.GetMediaEpisode - ep_id {numericId} (Emby 项目 '{item?.Name ?? "未知项目"}') 的剧集信息缺少有效的 AID ({episodeInfo.AId}) 或 CID ({episodeInfo.CId})。将使用 ep_id 作为 CommentId。");
                            // 如果实现了 XML 回退，finalCommentId 可以是 episodeInfo.CId.ToString()
                        }
                        log.Info($"Bilibili.GetMediaEpisode (ep_id: {numericId}): 为 Emby 项目 '{item?.Name ?? "未知项目"}' 返回 ScraperEpisode。ID: {episodeInfo.Id}, CommentId: '{finalCommentId}', 标题: '{episodeInfo.Title}'");
                        return new ScraperEpisode()
                        {
                            Id = $"{episodeInfo.Id}",
                            CommentId = finalCommentId,
                            Title = episodeInfo.Title,
                            EpisodeNumber = EpisodeContentClassifier.TryGetEpisodeNumber(episodeInfo.Title),
                        };
                    }
                    else
                    {
                        // 如果 GetEpisodeAsync 对于 ep_id 失败，说明有问题。
                        // 如果 'id' 本意是直接的 CID (UGC 内容)，可能会走到这个路径。
                        // 但是，对于 PGC 电影，我们期望 ep_id 能够工作。
                        log.Warn($"Bilibili.GetMediaEpisode (数字ID: {numericId}): _api.GetEpisodeAsync 为 Emby 项目 '{item?.Name ?? "未知项目"}' 返回了 null。如果这是一个 ep_id，则表示未找到该剧集。如果这是一个 CID，则直接将其用作 CommentId。");
                        // 如果 GetEpisodeAsync 返回 null，意味着无法通过此 numericId (作为 ep_id) 获取到 aid/cid。
                        // 此时，如果 numericId 本身就是一个有效的 CID (例如来自 UGC 视频)，那么直接用它作为 CommentId 是合理的。
                        // 后续 GetDanmuContent 会尝试将其作为 CID 处理（尽管目前 XML 备选方案未实现）。
                        // 但如果 numericId 是一个无效的 ep_id，那么这里返回的 ScraperEpisode 可能也无法成功获取弹幕。
                        // 为了保持一致性，如果 GetEpisodeAsync 返回 null，我们应该认为无法为这个 ID 构造出有效的 "aid,cid"。
                        log.Info($"Bilibili.GetMediaEpisode (数字ID: {numericId}): 上游未确认该 ep_id；返回 null 以继续 ProviderId fallback。");
                        return null;
                    }
                }
                else { log.Warn($"Bilibili.GetMediaEpisode - Emby 项目 '{item?.Name ?? "未知项目"}' 的 ID '{id}' 不是 BVID 也不是有效的数字ID。无法处理。"); }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Bilibili.GetMediaEpisode - 处理 Emby 项目 '{0}' (ID: '{1}') 时发生错误", item?.Name ?? "未知项目", id);
            }
            log.Warn($"Bilibili.GetMediaEpisode - 无法为 Emby 项目 '{item?.Name ?? "未知项目"}' (ID: '{id}') 确定有效的 ScraperEpisode。返回 null。"); // 回退或 ID 格式未处理
            return null;
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            log.Info($"Bilibili.GetDanmuContent - 开始为 Emby 项目获取弹幕内容: '{item?.Name ?? "未知项目"}', 从 GetMediaEpisode 获取的 CommentID: '{commentId}'");
            if (string.IsNullOrEmpty(commentId))
            {
                log.Info("Bilibili.GetDanmuContent - CommentID 为 null 或空。返回 null。");
                return null;
            }

            try
            {
                // Jellyfin 处理 "aid,cid" 格式的 proto。
                // 您当前的逻辑假设 commentId 对于 PGC 是 epId，对于 UGC 是 CID。

                if (commentId.Contains(",")) // 处理 "aid,cid" 格式的 proto，类似 Jellyfin
                {
                    var parts = commentId.Split(',');
                    if (parts.Length == 2 && long.TryParse(parts[0], out var aid) && long.TryParse(parts[1], out var cid))
                    {
                        if (aid > 0 && cid > 0)
                        {
                            // XML 接口通常比 proto 历史分段返回更多弹幕，优先使用。
                            try
                            {
                                var bytes = await _api.GetDanmuContentByCidAsync(cid, CancellationToken.None).ConfigureAwait(false);
                                var xmlDanmaku = ParseXml(System.Text.Encoding.UTF8.GetString(bytes));
                                if (xmlDanmaku.Items.Count > 0)
                                {
                                    xmlDanmaku.ChatId = cid;
                                    xmlDanmaku.ProviderId = ScraperProviderId;
                                    xmlDanmaku.DataSize = xmlDanmaku.Items.Count;
                                    return xmlDanmaku;
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Debug("B站 XML 弹幕解析失败，回退 proto。cid={0}, error={1}", cid, ex.Message);
                            }

                            log.Info($"Bilibili.GetDanmuContent - Emby 项目 '{item?.Name ?? "未知项目"}' 的 CommentID '{commentId}' 是 'aid,cid' 格式。尝试使用 AID: {aid}, CID: {cid} 获取 Proto 弹幕。");
                            var danmaku = await _api.GetDanmuContentByProtoAsync(aid, cid, CancellationToken.None).ConfigureAwait(false);
                            if (danmaku != null)
                            {
                                danmaku.ProviderId = ScraperProviderId;
                            }

                            return danmaku;
                        }
                        else { log.Info($"Bilibili.GetDanmuContent - Emby 项目 '{item?.Name ?? "未知项目"}' 的 'aid,cid' 格式 CommentID '{commentId}' 中 AID ({aid}) 或 CID ({cid}) 无效。"); }
                    }
                    else { log.Info($"Bilibili.GetDanmuContent - Emby 项目 '{item?.Name ?? "未知项目"}' 的 CommentID '{commentId}' 不是有效的 'aid,cid' 格式。"); }
                    return null;
                }

                // 如果不是 "aid,cid"，则假设 commentId 是 ep_id (用于 PGC) 或直接的 CID。
                if (long.TryParse(commentId, out var numericId))
                {
                    log.Info($"Bilibili.GetDanmuContent - Emby 项目 '{item?.Name ?? "未知项目"}' 的 CommentID '{commentId}' 是数字。假定为 ep_id。正在获取剧集详情以获取 AID/CID 用于 Proto 弹幕。");
                    // 首先尝试作为 PGC 剧集获取，以找到其 AID 和 CID
                    var episode = await _api.GetEpisodeAsync(numericId, CancellationToken.None).ConfigureAwait(false);
                    if (episode != null && episode.AId.HasValue && episode.CId > 0)
                    {
                        log.Info($"Bilibili.GetDanmuContent (ep_id: {commentId}): 为 Emby 项目 '{item?.Name ?? "未知项目"}' 获取到剧集详情。AID: {episode.AId.Value}, CID: {episode.CId}。正在获取 Proto 弹幕。");
                        var danmaku = await _api.GetDanmuContentByProtoAsync(episode.AId.Value, episode.CId, CancellationToken.None).ConfigureAwait(false);
                        if (danmaku != null)
                        {
                            danmaku.ProviderId = ScraperProviderId;
                        }

                        return danmaku;
                    }
                    else
                    {
                        // 如果不是 PGC 剧集 (或没有 AID)，则假设 numericId 是直接的 CID。
                        // 对于直接的 CID，我们需要 AID 来获取 proto。如果没有 AID，则需要 XML。
                        // Jellyfin 的示例在只知道 CID 时会回退到 XML。
                        log.Info($"Bilibili.GetDanmuContent (ep_id/cid: {commentId}): 为 Emby 项目 '{item?.Name ?? "未知项目"}' 无法通过 AID/CID 获取剧集详情 (AID: {episode?.AId}, CID: {episode?.CId})。无法获取 Proto 弹幕。XML 备选方案未实现。");
                        var bytes = await _api.GetDanmuContentByCidAsync(numericId, CancellationToken.None).ConfigureAwait(false);
                        var xmlDanmaku = ParseXml(System.Text.Encoding.UTF8.GetString(bytes));
                        xmlDanmaku.ChatId = numericId;
                        xmlDanmaku.ProviderId = ScraperProviderId;
                        xmlDanmaku.DataSize = xmlDanmaku.Items.Count;
                        return xmlDanmaku;
                    }
                }
                log.Info($"Bilibili.GetDanmuContent - Emby 项目 '{item?.Name ?? "未知项目"}' 的 CommentID '{commentId}' 不是 'aid,cid' 格式也不是有效的数字ID。无法继续。");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[{0}] 获取 CommentID '{1}' 的弹幕内容时发生错误", this.Name, commentId);
            }
            log.Info($"Bilibili.GetDanmuContent - Emby 项目 '{item?.Name ?? "未知项目"}' (CommentID: '{commentId}') 的弹幕内容获取已完成。未返回弹幕内容。");
            return null;
        }

        internal static ScraperDanmaku ParseXml(string xml)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(xml);
            }
            catch (XmlException)
            {
                doc.LoadXml(Xml10Sanitizer.SanitizeDocument(xml));
            }

            var result = new ScraperDanmaku();
            var nodes = doc.GetElementsByTagName("d");
            foreach (XmlNode node in nodes)
            {
                var p = node.Attributes?["p"];
                if (p == null)
                {
                    continue;
                }

                var values = p.Value.Split(',');
                if (values.Length < 8)
                {
                    continue;
                }

                try
                {
                    result.Items.Add(new ScraperDanmakuText
                    {
                        Progress = (int)(Convert.ToDouble(values[0], System.Globalization.CultureInfo.InvariantCulture) * 1000),
                        Mode = Convert.ToInt32(values[1]),
                        Fontsize = Convert.ToInt32(values[2]),
                        Color = Convert.ToUInt32(values[3]),
                        Ctime = Convert.ToInt64(values[4]),
                        Pool = Convert.ToInt32(values[5]),
                        MidHash = values[6],
                        Id = Convert.ToInt64(values[7]),
                        Weight = values.Length > 8 ? Convert.ToInt32(values[8]) : 0,
                        Content = node.InnerText
                    });
                }
                catch (FormatException)
                {
                    // 单条格式异常不影响其余弹幕。
                }
            }

            result.Items.Sort((x, y) => x.Progress.CompareTo(y.Progress));
            result.DataSize = result.Items.Count;
            return result;
        }
    }
}
