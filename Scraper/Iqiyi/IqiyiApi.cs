using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Serialization;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Scraper.Iqiyi.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Iqiyi
{
    public class IqiyiApi : AbstractApi
    {
        private const string MOBILE_USER_AGENT =
            "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Mobile Safari/537.36 Edg/136.0.0.0";

        private new const string HTTP_USER_AGENT = MOBILE_USER_AGENT;
        private static readonly Regex regVideoInfo = new Regex(@"""videoInfo"":(\{.+?\}),""", RegexOptions.Compiled);
        private static readonly Regex regAlbumInfo = new Regex(@"""albumInfo"":(\{.+?\}),""", RegexOptions.Compiled);
        
        public IqiyiApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger(typeof(IqiyiApi).ToString()), httpClient)
        {
            
        }
        
    public async Task<List<IqiyiSearchAlbumInfo>> SearchAsync(string keyword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return new List<IqiyiSearchAlbumInfo>();
        }

        var cacheKey = $"search_{keyword}";
        var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
        if (!SingletonManager.IsDebug && 
            _memoryCache.TryGetValue<List<IqiyiSearchAlbumInfo>>(cacheKey, out var cacheValue))
        {
            return cacheValue;
        }

        await this.LimitRequestFrequently();

        keyword = HttpUtility.UrlEncode(keyword);
        var url = $"https://search.video.iqiyi.com/o?if=html5&key={keyword}&pageNum=1&pageSize=20";

        var result = new List<IqiyiSearchAlbumInfo>();
        var searchResult = await httpClient.GetSelfResultAsync<IqiyiSearchResult>(GetDefaultHttpRequestOptions(url), null).ConfigureAwait(false);
        if (searchResult != null && searchResult.Data != null)
        {
            result = searchResult.Data.DocInfos
                .Where(x => x.Score > 0.7)
                .Select(x => x.AlbumDocInfo)
                .Where(x => !string.IsNullOrEmpty(x.Link) && x.Link.Contains("iqiyi.com") && x.SiteId == "iqiyi" && x.VideoDocType == 1 && !x.Channel.Contains("原创") && !x.Channel.Contains("教育"))
                .ToList();
        }

        _memoryCache.Set<List<IqiyiSearchAlbumInfo>>(cacheKey, result, expiredOption);
        return result;
    }

    public async Task<IqiyiHtmlVideoInfo?> GetVideoAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var cacheKey = $"video_{id}";
        var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
        if (!SingletonManager.IsDebug && _memoryCache.TryGetValue<IqiyiHtmlVideoInfo?>(cacheKey, out var video))
        {
            return video;
        }

        // 获取电视剧信息(aid)：https://pcw-api.iqiyi.com/album/album/baseinfo/5328486914190101
        // 获取电视剧剧集信息(综艺不适用)(aid)：https://pcw-api.iqiyi.com/albums/album/avlistinfo?aid=5328486914190101&page=1&size=10
        // 获取电视剧剧集信息(综艺不适用)(aid)：https://pub.m.iqiyi.com/h5/main/videoList/album/?albumId=5328486914190101&size=39&page=1&needPrevue=true&needVipPrevue=false
        var videoInfo = await GetVideoBaseAsync(id, cancellationToken).ConfigureAwait(false);
        if (videoInfo != null)
        {
            // 新版 base_info 接口已经直接返回完整分集，不再重复走旧接口。
            if (videoInfo.Epsodelist != null && videoInfo.Epsodelist.Count > 0)
            {
                _memoryCache.Set<IqiyiHtmlVideoInfo?>(cacheKey, videoInfo, expiredOption);
                return videoInfo;
            }

            if (videoInfo.channelName == "综艺")
            { // 综艺需要特殊处理
                videoInfo.Epsodelist = await this.GetZongyiEpisodesAsync($"{videoInfo.AlbumId}", cancellationToken).ConfigureAwait(false);
            }
            else if (videoInfo.channelName == "电影")
            { // 电影
                var duration = new TimeSpan(0, 0, videoInfo.Duration);
                videoInfo.Epsodelist = new List<IqiyiEpisode>() {
                    new IqiyiEpisode() {TvId = videoInfo.TvId, Order = 1, Name = videoInfo.VideoName, Duration = duration.ToString(@"hh\:mm\:ss"), PlayUrl = videoInfo.VideoUrl}
                };
            }
            else
            { // 电视剧需要再获取剧集信息
                videoInfo.Epsodelist = await this.GetEpisodesAsync($"{videoInfo.AlbumId}", videoInfo.VideoCount, cancellationToken).ConfigureAwait(false);
            }

            _memoryCache.Set<IqiyiHtmlVideoInfo?>(cacheKey, videoInfo, expiredOption);
            return videoInfo;
        }

        _memoryCache.Set<IqiyiHtmlVideoInfo?>(cacheKey, null, expiredOption);
        return null;
    }

    public async Task<IqiyiHtmlVideoInfo?> GetVideoBaseAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var cacheKey = $"video_base_{id}";
        var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
        if (!SingletonManager.IsDebug && _memoryCache.TryGetValue<IqiyiHtmlVideoInfo?>(cacheKey, out var video))
        {
            return video;
        }

        await this.LimitRequestFrequently();

        var url = $"https://m.iqiyi.com/v_{id}.html";
        var defaultHttpRequestOptions = GetDefaultHttpRequestOptions(url, null, cancellationToken);
        defaultHttpRequestOptions.UserAgent = HTTP_USER_AGENT;
        var videoInfo = await httpClient.GetSelfResultAsync<IqiyiHtmlVideoInfo>(defaultHttpRequestOptions, response =>
        {
            
            // 确保响应状态码为成功
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                // 读取响应流
                using (var responseStream = response.Content)
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var htmlResult = reader.ReadToEnd();
                    var albumJson = regAlbumInfo.FirstMatchGroup(htmlResult);
                    var albumInfo = albumJson.FromJson<IqiyiHtmlAlbumInfo>();
                    var videoJson = regVideoInfo.FirstMatchGroup(htmlResult);
                    var videoInfo = videoJson.FromJson<IqiyiHtmlVideoInfo>();
                    // 新版移动页面会输出 `"videoInfo":{}`。空对象反序列化后并非 null，
                    // 但 AlbumId/TvId/VideoUrl 全部无效；若把它当作成功结果，后续会请求
                    // avlistinfo?aid=0&size=0，最终得到空分集列表。
                    if (IsLegacyVideoInfoUsable(videoInfo))
                    {
                        if (albumInfo != null)
                        {
                            videoInfo.VideoCount = albumInfo.VideoCount;
                        }

                        return videoInfo.ToJson();
                    }
                }

                return null;
            }
            else
            {
                // 处理不成功的响应
                throw new InvalidOperationException($"请求失败，HTTP 状态码：{response.StatusCode}");
            }
            
        });
        
        if (videoInfo != null)
        {
            this._memoryCache.Set(cacheKey, videoInfo, expiredOption);
            return videoInfo;
        }

        // 爱奇艺新版移动页可能只返回空的 videoInfo/albumInfo。使用带签名的
        // base_info 接口按页面 link id 获取基础信息及完整分集列表。
        var baseInfoVideo = await GetVideoFromBaseInfoAsync(id, cancellationToken).ConfigureAwait(false);
        if (baseInfoVideo != null)
        {
            this._memoryCache.Set(cacheKey, baseInfoVideo, expiredOption);
        }

        return baseInfoVideo;
    }

    private static bool IsLegacyVideoInfoUsable(IqiyiHtmlVideoInfo videoInfo)
    {
        return videoInfo != null &&
               videoInfo.AlbumId > 0 &&
               videoInfo.TvId > 0 &&
               !string.IsNullOrEmpty(videoInfo.LinkId);
    }

    private async Task<IqiyiHtmlVideoInfo?> GetVideoFromBaseInfoAsync(string id, CancellationToken cancellationToken)
    {
        if (!TryConvertLinkIdToEntityId(id, out var entityId))
        {
            _logger.Warn("爱奇艺 link id 无法转换为 entity id: {0}", id);
            return null;
        }

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["entity_id"] = entityId.ToString(),
            ["device_id"] = "qd5fwuaj4hunxxdgzwkcqmefeb3ww5hx",
            ["auth_cookie"] = string.Empty,
            ["user_id"] = "0",
            ["vip_type"] = "-1",
            ["vip_status"] = "0",
            ["conduit_id"] = string.Empty,
            ["pcv"] = "13.082.22866",
            ["app_version"] = "13.082.22866",
            ["ext"] = string.Empty,
            ["app_mode"] = "standard",
            ["scale"] = "100",
            ["timestamp"] = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(),
            ["src"] = "pca_tvg",
            ["os"] = string.Empty,
            ["ad_ext"] = "{\"r\":\"2.2.0-ares6-pure\"}"
        };

        parameters["sign"] = CreateBaseInfoSign(parameters);
        var query = string.Join("&", parameters.Select(x => $"{HttpUtility.UrlEncode(x.Key)}={HttpUtility.UrlEncode(x.Value)}"));
        var url = $"https://www.iqiyi.com/prelw/tvg/v2/lw/base_info?{query}";
        var requestOptions = GetDefaultHttpRequestOptions(url, null, cancellationToken);
        requestOptions.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        requestOptions.AcceptHeader = "*/*";
        requestOptions.RequestHeaders["Origin"] = "https://www.iqiyi.com";
        requestOptions.RequestHeaders["Referer"] = "https://www.iqiyi.com/";

        var response = await httpClient.GetSelfResponse(requestOptions).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode())
        {
            _logger.Warn("爱奇艺 base_info 请求失败: id={0}, status={1}", id, response.StatusCode);
            return null;
        }

        string body;
        using (var responseStream = response.Content)
        using (var reader = new StreamReader(responseStream, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        try
        {
            using (var document = JsonDocument.Parse(body))
            {
                var root = document.RootElement;
                if (!TryGetInt64(root, "status_code", out var statusCode) || statusCode != 0 ||
                    !root.TryGetProperty("data", out var data))
                {
                    _logger.Warn("爱奇艺 base_info 返回异常: id={0}", id);
                    return null;
                }

                var result = new IqiyiHtmlVideoInfo
                {
                    Epsodelist = new List<IqiyiEpisode>()
                };

                if (data.TryGetProperty("base_data", out var baseData))
                {
                    if (!TryGetInt64(baseData, "qipu_id", out var albumId))
                    {
                        TryGetInt64(baseData, "_id", out albumId);
                    }

                    result.AlbumId = albumId;
                    result.VideoName = GetString(baseData, "title") ?? GetString(baseData, "current_video_title") ?? string.Empty;
                    result.VideoUrl = GetString(baseData, "share_url") ?? $"https://www.iqiyi.com/v_{id}.html";
                    result.VideoCount = GetInt32(baseData, "total_episode");
                    var channelId = GetInt32(baseData, "channel_id");
                    result.channelName = GetChannelName(channelId);
                    result.TvId = ExtractTvId(GetString(baseData, "play_url"));
                }

                if (data.TryGetProperty("template", out var template) &&
                    template.TryGetProperty("tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tab in tabs.EnumerateArray())
                    {
                        if (!tab.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var block in blocks.EnumerateArray())
                        {
                            var blockType = GetString(block, "bk_type");
                            if (blockType == "album_episodes" ||
                                (blockType == "video_list" && HasTag(block, "episodes")))
                            {
                                CollectEpisodes(block, result.Epsodelist);
                            }
                        }
                    }
                }

                result.Epsodelist = result.Epsodelist
                    .Where(x => x.TvId > 0 && !string.IsNullOrEmpty(x.LinkId))
                    .GroupBy(x => x.TvId)
                    .Select(x => x.First())
                    .OrderBy(x => x.Order)
                    .ToList();

                // 电影或接口仅返回当前视频时仍构造单集，保证可继续下载弹幕。
                if (result.Epsodelist.Count == 0 && result.TvId > 0)
                {
                    result.Epsodelist.Add(new IqiyiEpisode
                    {
                        TvId = result.TvId,
                        Name = result.VideoName,
                        Order = 1,
                        Duration = string.Empty,
                        PlayUrl = result.VideoUrl
                    });
                }

                if (result.VideoCount <= 0)
                {
                    result.VideoCount = result.Epsodelist.Count;
                }

                _logger.Info("爱奇艺 base_info 获取成功: id={0}, entityId={1}, episodes={2}", id, entityId, result.Epsodelist.Count);
                return result.TvId > 0 || result.Epsodelist.Count > 0 ? result : null;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "解析爱奇艺 base_info 失败: id={0}", id);
            return null;
        }
    }

    private static bool TryConvertLinkIdToEntityId(string linkId, out ulong entityId)
    {
        const ulong xorKey = 0x75706971676cUL;
        entityId = 0;
        if (string.IsNullOrEmpty(linkId))
        {
            return false;
        }

        ulong value = 0;
        foreach (var ch in linkId.ToLowerInvariant())
        {
            int digit;
            if (ch >= '0' && ch <= '9')
            {
                digit = ch - '0';
            }
            else if (ch >= 'a' && ch <= 'z')
            {
                digit = ch - 'a' + 10;
            }
            else
            {
                return false;
            }

            if (value > (ulong.MaxValue - (ulong)digit) / 36UL)
            {
                return false;
            }

            value = (value * 36UL) + (ulong)digit;
        }

        entityId = value ^ xorKey;
        if (entityId < 900000UL)
        {
            entityId = 100UL * (entityId + 900000UL);
        }

        return entityId > 0;
    }

    private static string CreateBaseInfoSign(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var source = string.Join("&", parameters.Where(x => x.Key != "sign").Select(x => $"{x.Key}={x.Value}")) +
                     "&secret_key=howcuteitis";
        using (var md5 = MD5.Create())
        {
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(source));
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
    }

    private static void CollectEpisodes(JsonElement element, List<IqiyiEpisode> episodes)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (GetInt32(element, "content_type") == 1)
            {
                var pageUrl = GetString(element, "page_url");
                var tvId = 0L;
                if (!TryGetInt64(element, "qipu_id", out tvId))
                {
                    tvId = ExtractTvId(GetString(element, "play_url"));
                }

                var order = GetInt32(element, "album_order");
                if (tvId > 0 && order > 0 && !string.IsNullOrEmpty(pageUrl))
                {
                    episodes.Add(new IqiyiEpisode
                    {
                        TvId = tvId,
                        Name = GetString(element, "short_display_name") ?? GetString(element, "title") ?? $"第{order}集",
                        Order = order,
                        Duration = string.Empty,
                        PlayUrl = pageUrl
                    });
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectEpisodes(property.Value, episodes);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                CollectEpisodes(child, episodes);
            }
        }
    }

    private static bool HasTag(JsonElement block, string tag)
    {
        if (!block.TryGetProperty("tag", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return tags.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == tag);
    }

    private static string GetChannelName(int channelId)
    {
        switch (channelId)
        {
            case 1: return "电影";
            case 2: return "电视剧";
            case 3: return "纪录片";
            case 4: return "动漫";
            case 6: return "综艺";
            case 15: return "儿童";
            default: return string.Empty;
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        return TryGetInt64(element, propertyName, out var value) && value <= int.MaxValue ? (int)value : 0;
    }

    private static bool TryGetInt64(JsonElement element, string propertyName, out long value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt64(out value);
        }

        return property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out value);
    }

    private static long ExtractTvId(string? playUrl)
    {
        if (string.IsNullOrEmpty(playUrl))
        {
            return 0;
        }

        var match = Regex.Match(playUrl, @"(?:^|[;?&])tvid=(\d+)", RegexOptions.IgnoreCase);
        return match.Success && long.TryParse(match.Groups[1].Value, out var tvId) ? tvId : 0;
    }

    /// <summary>
    /// 获取电视剧剧集列表(综艺不适用)
    /// </summary>
    public async Task<List<IqiyiEpisode>> GetEpisodesAsync(string albumId, int size, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(albumId))
        {
            return new List<IqiyiEpisode>();
        }

        var url = $"https://pcw-api.iqiyi.com/albums/album/avlistinfo?aid={albumId}&page=1&size={size}";
        // var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        // response.EnsureSuccessStatusCode();

        var albumResult = await httpClient.GetSelfResultAsyncWithError<IqiyiVideoResult>(GetDefaultHttpRequestOptions(url), null).ConfigureAwait(false);
        if (albumResult != null && albumResult.Data != null && albumResult.Data.Epsodelist != null)
        {
            return albumResult.Data.Epsodelist;
        }

        return new List<IqiyiEpisode>();
    }

    /// <summary>
    /// 获取综艺剧集列表
    /// </summary>
    public async Task<List<IqiyiEpisode>> GetZongyiEpisodesAsync(string albumId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(albumId))
        {
            return new List<IqiyiEpisode>();
        }

        var url = $"https://pcw-api.iqiyi.com/album/album/baseinfo/{albumId}";
        var albumResult = await httpClient.GetSelfResultAsyncWithError<IqiyiAlbumResult>(GetDefaultHttpRequestOptions(url), null).ConfigureAwait(false);
        if (albumResult != null && albumResult.Data != null && albumResult.Data.FirstVideo != null && albumResult.Data.LatestVideo != null)
        {
            var startDate = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(albumResult.Data.FirstVideo.publishTime).ToLocalTime();
            var endDate = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddMilliseconds(albumResult.Data.LatestVideo.publishTime).ToLocalTime();
            // 超过一年的太大直接不处理
            var totalDays = (endDate - startDate).TotalDays;
            if (totalDays > 365)
            {
                return new List<IqiyiEpisode>();
            }

            var list = new List<IqiyiVideoListInfo>();
            for (var begin = startDate; begin.Month <= endDate.Month; begin = begin.AddMonths(1))
            {
                var year = begin.Year;
                var month = begin.ToString("MM");
                url = $"https://pub.m.iqiyi.com/h5/main/videoList/source/month/?sourceId={albumId}&year={year}&month={month}";

                var videoListResult = await httpClient.GetSelfResultAsyncWithError<IqiyiVideoListResult>(GetDefaultHttpRequestOptions(url), null).ConfigureAwait(false);
                if (videoListResult != null && videoListResult.Data != null && videoListResult.Data.Videos != null && videoListResult.Data.Videos.Count > 0)
                {
                    list.AddRange(videoListResult.Data.Videos.Where(x => !x.ShortTitle.Contains("精编版") && !x.ShortTitle.Contains("会员版")));
                }
                else
                {
                    break;
                }

                Thread.Sleep(200);
            }

            var result = new List<IqiyiEpisode>();
            list = list.OrderBy(x => x.PublishTime).ToList();
            for (int i = 0; i < list.Count; i++)
            {
                result.Add(new IqiyiEpisode() { TvId = list[i].Id, Name = list[i].ShortTitle, Order = (i + 1), Duration = list[i].Duration, PlayUrl = list[i].PlayUrl });
            }
            return result;
        }

        return new List<IqiyiEpisode>();
    }


    public async Task<List<IqiyiComment>> GetDanmuContentAsync(string tvId, CancellationToken cancellationToken)
    {
        var danmuList = new List<IqiyiComment>();
        if (string.IsNullOrEmpty(tvId))
        {
            return danmuList;
        }

        int mat = 1;
        do
        {
            try
            {
                var comments = await this.GetDanmuContentByMatAsync(tvId, mat, cancellationToken);
                // 每段有300秒弹幕，为避免弹幕太大，从中间隔抽取最大60秒200条弹幕
                danmuList.AddRange(comments.ExtractToNumber(1000));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "获取爱奇艺弹幕({0})解析失败", tvId);
                break;
            }
            catch (Exception ex)
            {
                break;
            }

            mat++;

            // 等待一段时间避免api请求太快
            Thread.Sleep(100);
        } while (mat < 1000);

        return danmuList;
    }

    // mat从0开始，视频分钟数
    public async Task<List<IqiyiComment>> GetDanmuContentByMatAsync(string tvId, int mat, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(tvId) || tvId.Length < 4)
        {
            return new List<IqiyiComment>();
        }

        var s1 = tvId.Substring(tvId.Length - 4, 2);
        var s2 = tvId.Substring(tvId.Length - 2);
        // 一次拿300秒的弹幕
        var url = $"http://cmts.iqiyi.com/bullet/{s1}/{s2}/{tvId}_300_{mat}.z";
        HttpRequestOptions defaaultHttpRequestOptions = GetDefaultHttpRequestOptions(url, null, cancellationToken);
        var response = await httpClient.GetSelfResponse(defaaultHttpRequestOptions);
        if (!(response.StatusCode >= HttpStatusCode.OK && response.StatusCode <= (HttpStatusCode)299))
        {
            _logger.Info("请求http异常, httpRequestOptions={0}, status={1}", defaaultHttpRequestOptions.ToString(), response.StatusCode);
            throw new HttpRequestException("请求异常 code=" + response.StatusCode);
        }
        
        using (var zipStream = response.Content)
        {
           byte[] decompressedData = new byte[4096];
            int decompressedLength = 0;
            using (var memoryStream = new MemoryStream())
            {
                using (InflaterInputStream inflater = new InflaterInputStream(zipStream))
                {
                    do
                    {
                        decompressedLength = inflater.Read(decompressedData, 0, decompressedData.Length);
                        memoryStream.Write(decompressedData, 0, decompressedLength);
                    } while (decompressedLength > 0);
                }

                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream, Encoding.UTF8, true, 1024, true))
                {
                    var serializer = new XmlSerializer(typeof(IqiyiCommentDocument));

                    try
                    {
                        var result = serializer.Deserialize(reader) as IqiyiCommentDocument;
                        if (result != null && result.Data != null)
                        {
                            var comments = new List<IqiyiComment>();
                            foreach (var entry in result.Data)
                            {
                                comments.AddRange(entry.List);
                            }
                            return comments;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        memoryStream.Position = 0;
                        using (var cleanReader = new StreamReader(memoryStream, Encoding.UTF8, true, 1024, true))
                        {
                            var cleanXml = RemoveInvalidXmlChars(cleanReader.ReadToEnd());
                            using (var stringReader = new StringReader(cleanXml))
                            {
                                var result = serializer.Deserialize(stringReader) as IqiyiCommentDocument;
                                if (result != null && result.Data != null)
                                {
                                    var comments = new List<IqiyiComment>();
                                    foreach (var entry in result.Data)
                                    {
                                        comments.AddRange(entry.List);
                                    }
                                    return comments;
                                }
                            }
                        }
                    }
                }
            }

        }

        return new List<IqiyiComment>();
    }

    public static string RemoveInvalidXmlChars(string xml)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return xml;
        }

        const string pattern = @"[\u0000-\u0008\u000B\u000C\u000E-\u001F\u200B-\u200D\uFEFF]|&#0;";
        return Regex.Replace(xml, pattern, string.Empty);
    }

    protected Task LimitRequestFrequently()
    {
        Thread.Sleep(1000);
        return Task.CompletedTask;
        // Task.CompletedTask;
        // await this._timeConstraint;
    }
    }
}
