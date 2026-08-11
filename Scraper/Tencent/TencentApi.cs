using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Scraper.Tencent.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Tencent
{
    public class TencentApi : AbstractApi
    {
        protected Dictionary<string, string> defaultHeaders;
        protected string[] cookies;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TencentApi"/> class.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public TencentApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("TencentApi"), httpClient)
        {

            this.defaultHeaders = new Dictionary<string, string>
            {
                { "Referer", "https://v.qq.com/" },
                { "Origin", "https://v.qq.com/" }
            };

            this.cookies = new[]
            {
                "pgv_pvid=40b67e3b06027f3d; video_platform=2; vversion_name=8.2.95; video_bucketid=4; video_omgid=0a1ff6bc9407c0b1cff86ee5d359614d"
            };
        }


        protected override Dictionary<string, string> GetDefaultHeaders()
        {
            return defaultHeaders;
        }
        
        protected override string[] GetDefaultCookies(string? url=null)
        {
            return cookies;
        }

        public async Task<List<TencentVideo>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<TencentVideo>();
            }

            var cacheKey = $"search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (!SingletonManager.IsDebug && _memoryCache.TryGetValue<List<TencentVideo>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently(cancellationToken).ConfigureAwait(false);

            var originPostData = new TencentSearchRequest() { Query = keyword };
            var url = "https://pbaccess.video.qq.com/trpc.videosearch.mobile_search.MultiTerminalSearch/MbSearch?vplatform=2";

            var result = new List<TencentVideo>();
            var searchResult = await httpClient.GetSelfResultAsyncWithError<TencentSearchResult>(GetDefaultHttpRequestOptions(url, null, cancellationToken), null, "POST", originPostData);
            
            if (searchResult != null && searchResult.Data != null)
            {
                var boxes = new List<TencentSearchBox>();
                if (searchResult.Data.NormalList != null)
                {
                    boxes.Add(searchResult.Data.NormalList);
                }

                if (searchResult.Data.AreaBoxList != null)
                {
                    boxes.AddRange(searchResult.Data.AreaBoxList);
                }

                foreach (var box in boxes)
                {
                    if (box?.ItemList == null)
                    {
                        continue;
                    }

                    foreach (var item in box.ItemList)
                    {
                        if (item?.VideoInfo == null || item.Doc == null)
                        {
                            continue;
                        }

                        var video = item.VideoInfo;
                        if (video.PlaySites != null && video.PlaySites.Count > 0 &&
                            !video.PlaySites.Any(x => x != null && x.EnName == "qq"))
                        {
                            continue;
                        }

                        // videoDoc 是短视频/上传视频，剧集搜索只保留 subjectDoc 结果。
                        if (video.VideoDoc != null || video.Year == null || video.Year == 0 ||
                            string.IsNullOrEmpty(video.Title) || video.Title.Distance(keyword) <= 0)
                        {
                            continue;
                        }

                        video.Id = item.Doc.Id;
                        if (!result.Any(x => x.Id == video.Id))
                        {
                            result.Add(video);
                        }
                    }
                }
            }

            _memoryCache.Set<List<TencentVideo>>(cacheKey, result, expiredOption);
            return result;
        }

        public async Task<TencentVideo?> GetVideoAsync(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var cacheKey = $"media_{id}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<TencentVideo?>(cacheKey, out var video))
            {
                return video;
            }

            var url = $"https://pbaccess.video.qq.com/trpc.universal_backend_service.page_server_rpc.PageServer/GetPageData?video_appid=3000010&vplatform=2";
            var allEpisodes = new List<TencentEpisode>();

            // --- 切换为手动拼接分页参数的逻辑 ---
            var pageSize = 100;
            var beginNum = 1;
            var endNum = pageSize;
            var nextPageContext = string.Empty; // 用于构造请求的分页参数
            var lastId = string.Empty; // 用于防止死循环
            // --- 逻辑切换结束 ---

            try
            {
                do
                {
                    // 首次请求PageContext为空，后续请求使用手动构造的字符串
                    var pageParams = new TencentPageParams() { Cid = id, PageSize = $"{pageSize}", PageContext = nextPageContext };
                    var originPostData = new TencentEpisodeListRequest() { PageParams = pageParams };
                    var result = await httpClient.GetSelfResultAsyncWithError<TencentEpisodeListResult>(GetDefaultHttpRequestOptions(url), null, "POST", originPostData).ConfigureAwait(false);

                    nextPageContext = string.Empty; // 每次循环重置，如果需要下一页再重新赋值

                    // 使用更健壮的方式解析深层嵌套的对象
                    var itemDataLists = result?.Data?.ModuleListDatas?.FirstOrDefault()?.ModuleDatas?.FirstOrDefault()?.ItemDataLists;

                    if (itemDataLists?.ItemDatas != null && itemDataLists.ItemDatas.Any())
                    {
                        var newEpisodes = itemDataLists.ItemDatas
                            .Select(x => x.ItemParams)
                            // 增加更详细的过滤规则，过滤掉预告、彩蛋、直拍等非正片内容
                            .Where(x => x != null && x.IsTrailer != "1" && !x.Title.Contains("直拍") && !x.Title.Contains("彩蛋") && !x.Title.Contains("直播回顾"))
                            .ToList();

                        // 防死循环检查：如果本次获取的最后一集和上次的最后一集相同，则停止
                        if (newEpisodes.Any() && newEpisodes.Last().Vid == lastId)
                        {
                            _logger.Warn($"TencentApi.GetVideoAsync - 检测到重复的分页数据 (lastId: {lastId})，为避免死循环，终止获取。");
                            break;
                        }

                        allEpisodes.AddRange(newEpisodes);
                        _logger.Info($"TencentApi.GetVideoAsync - 成功为ID '{id}' 获取并解析了 {newEpisodes.Count()} 个剧集分片。当前总数: {allEpisodes.Count}。");

                        // 判断是否需要请求下一页
                        if (itemDataLists.ItemDatas.Count == pageSize)
                        {
                            beginNum += pageSize;
                            endNum += pageSize;
                            nextPageContext = $"episode_begin={beginNum}&episode_end={endNum}&episode_step={pageSize}";
                            lastId = allEpisodes.Last().Vid;

                            // 等待一段时间避免 api 请求太快
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        _logger.Warn($"TencentApi.GetVideoAsync - 腾讯API为ID '{id}' 的分页请求未返回有效的剧集列表。终止分页。响应: {result?.ToJson()}");
                        // nextPageContext 此时为空, 循环将自然终止
                    }
                } while (!string.IsNullOrEmpty(nextPageContext) && !cancellationToken.IsCancellationRequested);

                if (allEpisodes.Any())
                {
                    var videoInfo = new TencentVideo
                    {
                        Id = id,
                        // 某些综艺节目可能会返回重复的剧集，这里进行去重
                        EpisodeList = allEpisodes.GroupBy(e => e.Vid).Select(g => g.First()).ToList()
                    };
                    _logger.Info($"TencentApi.GetVideoAsync - ID '{id}' 的所有剧集获取完成，总计 {videoInfo.EpisodeList.Count} 个。");
                    _memoryCache.Set<TencentVideo?>(cacheKey, videoInfo, expiredOption);
                    return videoInfo;
                }

            }
            catch (Exception ex)
            {
                _logger.Error("TencentApi.GetVideoAsync - 处理ID '{0}' 时发生错误", id);
            }

            _memoryCache.Set<TencentVideo?>(cacheKey, null, expiredOption);
            return null;
        }


        public async Task<TencentCommentDownloadResult> GetDanmuContentAsync(
            string vid,
            CancellationToken cancellationToken)
        {
            var downloadResult = new TencentCommentDownloadResult();
            if (string.IsNullOrEmpty(vid))
            {
                return downloadResult;
            }

            var url = $"https://dm.video.qq.com/barrage/base/{vid}";
            var baseRequestOptions = GetDefaultHttpRequestOptions(url, null, cancellationToken);
            baseRequestOptions.TimeoutMs = 15000;
            var result = await httpClient.GetSelfResultAsyncWithError<TencentCommentResult>(
                baseRequestOptions).ConfigureAwait(false);
            if (result != null && result.SegmentIndex != null)
            {
                var start = result.SegmentStart.ToLong();
                var size = result.SegmentSpan.ToLong();
                for (long i = start; result.SegmentIndex.ContainsKey(i) && size > 0; i += size)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var segment = result.SegmentIndex[i];
                    var segmentUrl = $"https://dm.video.qq.com/barrage/segment/{vid}/{segment.SegmentName}";
                    downloadResult.SegmentTotal++;

                    TencentCommentSegmentResult segmentResult = null;
                    Exception lastError = null;
                    const int maxRetries = 3;
                    for (var attempt = 0; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            var requestOptions = GetDefaultHttpRequestOptions(segmentUrl, null, cancellationToken);
                            requestOptions.TimeoutMs = 15000;
                            // 重试时不再复用可能已经被腾讯 CDN 关闭的长连接。
                            if (attempt > 0)
                            {
                                requestOptions.RequestHeaders["Connection"] = "close";
                            }

                            segmentResult = await httpClient
                                .GetSelfResultAsyncWithError<TencentCommentSegmentResult>(requestOptions)
                                .ConfigureAwait(false);
                            if (segmentResult == null)
                            {
                                throw new InvalidOperationException("腾讯弹幕分段返回空响应");
                            }

                            if (attempt > 0)
                            {
                                _logger.Info(
                                    "腾讯弹幕分段重试成功: vid={0}, segment={1}, attempt={2}",
                                    vid,
                                    segment.SegmentName,
                                    attempt + 1);
                            }
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            if (attempt >= maxRetries)
                            {
                                break;
                            }

                            var delayMilliseconds = 500 * (1 << attempt);
                            _logger.Warn(
                                "腾讯弹幕分段下载失败，将重试: vid={0}, segment={1}, attempt={2}/{3}, delayMs={4}, error={5}",
                                vid,
                                segment.SegmentName,
                                attempt + 1,
                                maxRetries + 1,
                                delayMilliseconds,
                                ex.Message);
                            await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (segmentResult == null)
                    {
                        downloadResult.SegmentFailed++;
                        downloadResult.FailedSegmentNames.Add(segment.SegmentName ?? i.ToString());
                        _logger.Error(
                            "腾讯弹幕分段连续重试后仍失败，保留其他分段: vid={0}, segment={1}, error={2}",
                            vid,
                            segment.SegmentName,
                            lastError?.Message ?? "未知错误");
                        continue;
                    }

                    if (segmentResult.BarrageList != null)
                    {
                        // 30秒每segment，为避免弹幕太大，从中间隔抽取最多100条弹幕。
                        downloadResult.Comments.AddRange(segmentResult.BarrageList.ExtractToNumber(100));
                    }

                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                }
            }

            return downloadResult;
        }

        protected override async Task LimitRequestFrequently(CancellationToken cancellationToken = default)
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }
}
