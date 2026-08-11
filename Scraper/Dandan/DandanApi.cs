using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Scraper.Dandan.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Dandan
{
    public class DandanApi : AbstractApi
    {
        private const string OfficialApiBaseUrl = "https://api.dandanplay.net/api/v2/";
        private const string OfficialProxyCorsBaseUrl = "https://danmuplus-dandan-proxy.mouxiaohao.workers.dev/cors/";
        private static readonly object _lock = new object();
        private DateTime lastRequestTime = DateTime.Now.AddDays(-1);
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        const string API_ID = "";
        const string API_SECRET = "";


        public DandanOption Config
        {
            get { return Plugin.Instance?.Configuration.Dandan ?? new DandanOption(); }
        }

        protected DandanCredentials ResolveCredentials()
        {
            return DandanCredentialResolver.Resolve(
                Config.ApiId,
                Config.ApiSecret,
                Environment.GetEnvironmentVariable("DANDAN_API_ID"),
                Environment.GetEnvironmentVariable("DANDAN_API_SECRET"),
                API_ID,
                API_SECRET);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DandanApi"/> class.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public DandanApi(ILogManager logManager, IJsonSerializer jsonSerializer, IHttpClient httpClient)
            : base(logManager.GetLogger("DandanApi"), httpClient)
        {
            _logger = logManager.getDefaultLogger(GetType().ToString());
            this._jsonSerializer = jsonSerializer;
            // IHttpClient client = ServiceRegistrator.GetByType<IHttpClient>();
            // httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }


        public async Task<List<Anime>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<Anime>();
            }

            var cacheKey = $"search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<Anime>>(cacheKey, out var searchResult))
            {
                return searchResult;
            }

            this.LimitRequestFrequently();

            var encodedKeyword = HttpUtility.UrlEncode(keyword);
            var officialUrl = $"{OfficialApiBaseUrl}search/anime?keyword={encodedKeyword}";
            var config = Config;
            var useProxyApi = config.UseProxyApi;
            var url = RouteOfficialUrl(officialUrl, useProxyApi, ResolveUseOfficialProxyCors(config), config.ProxyCorsUrl);
            var httpRequestOptions = new HttpRequestOptions
            {
                //Url = $"http://sub.xmp.sandai.net:8000/subxl/{cid}.json",
                Url = url,
                UserAgent = $"{HTTP_USER_AGENT}",
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
            };
            AddAuthenticationIfRequired(httpRequestOptions, officialUrl, useProxyApi);
            var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);

            // _logger.Info("res = {0}", response.ToString());
            // _logger.Info("{0} Search | Response -> {1}", url, _jsonSerializer.SerializeToString(response));
            
            if (response.StatusCode != HttpStatusCode.OK)
            { 
                return new List<Anime>();
            }

            // var result = await response.Content.ReadFromJsonAsync<SearchResult>();
            SearchResult result = _jsonSerializer.DeserializeFromStream<SearchResult>(response.Content);
            if (result != null && result.Success)
            {
                _memoryCache.Set<List<Anime>>(cacheKey, result.Animes, expiredOption);
                return result.Animes;
            }

            _memoryCache.Set<List<Anime>>(cacheKey, new List<Anime>(), expiredOption);
            return new List<Anime>();
        }

        public async Task<Anime?> GetAnimeAsync(
            long animeId,
            CancellationToken cancellationToken,
            bool includeNonMainEpisodes = false)
        {
            if (animeId <= 0)
            {
                return null;
            }

            var cacheKey = $"anime_{animeId}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<Anime?>(cacheKey, out var anime))
            {
                return FilterEpisodes(anime, includeNonMainEpisodes);
            }

            var officialUrl = $"{OfficialApiBaseUrl}bangumi/{animeId}";
            var config = Config;
            var useProxyApi = config.UseProxyApi;
            var url = RouteOfficialUrl(officialUrl, useProxyApi, ResolveUseOfficialProxyCors(config), config.ProxyCorsUrl);
            HttpRequestOptions httpRequestOptions = new HttpRequestOptions
            {
                //Url = $"http://sub.xmp.sandai.net:8000/subxl/{cid}.json",
                Url = url,
                UserAgent = $"{HTTP_USER_AGENT}",
                TimeoutMs = 30000,
                AcceptHeader = "application/json",
            };
            AddAuthenticationIfRequired(httpRequestOptions, officialUrl, useProxyApi);
            var response = await httpClient.GetResponse(httpRequestOptions).ConfigureAwait(false);
            // var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            // response.EnsureSuccessStatusCode();
                        
            if (response.StatusCode != HttpStatusCode.OK)
            { 
                return null;
            }
            
            AnimeResult result = _jsonSerializer.DeserializeFromStream<AnimeResult>(response.Content);
            // var result = await response.Content.ReadFromJsonAsync<AnimeResult>(cancellationToken)
            //     .ConfigureAwait(false);
            if (result != null && result.Success && result.Bangumi != null)
            {
                // 过滤掉特典剧集，episodeNumber为S1/S2.。。
                anime = result.Bangumi;

                // Cache the complete payload. Exact Episode ProviderId
                // verification must still be able to inspect specials after an
                // ordinary Season lookup has populated this cache entry.
                _memoryCache.Set<Anime?>(cacheKey, anime, expiredOption);
                return FilterEpisodes(anime, includeNonMainEpisodes);
            }

            _memoryCache.Set<Anime?>(cacheKey, null, expiredOption);
            return null;
        }

        private static Anime FilterEpisodes(Anime anime, bool includeNonMainEpisodes)
        {
            if (anime == null || includeNonMainEpisodes || anime.Episodes == null)
            {
                return anime;
            }

            return new Anime
            {
                AnimeId = anime.AnimeId,
                AnimeTitle = anime.AnimeTitle,
                Type = anime.Type,
                TypeDescription = anime.TypeDescription,
                ImageUrl = anime.ImageUrl,
                StartDate = anime.StartDate,
                EpisodeCount = anime.EpisodeCount,
                Episodes = anime.Episodes.Where(x =>
                {
                    var success = int.TryParse(x?.EpisodeNumber, out var parsedNumber);
                    return success && parsedNumber > 0;
                }).ToList(),
            };
        }

        public async Task<List<Comment>> GetCommentsAsync(long epId, CancellationToken cancellationToken)
        {
            if (epId <= 0)
            {
                throw new ArgumentNullException(nameof(epId));
            }

            var withRelated = this.Config.WithRelatedDanmu ? "true" : "false";
            var chConvert = this.Config.ChConvert;
            var officialUrl = $"{OfficialApiBaseUrl}comment/{epId}?withRelated={withRelated}&chConvert={chConvert}";
            var config = Config;
            var useProxyApi = config.UseProxyApi;
            var url = RouteOfficialUrl(officialUrl, useProxyApi, ResolveUseOfficialProxyCors(config), config.ProxyCorsUrl);
            HttpRequestOptions httpRequestOptions = GetDefaultHttpRequestOptions(url);
            AddAuthenticationIfRequired(httpRequestOptions, officialUrl, useProxyApi);
            var result = await httpClient.GetSelfResultAsync<CommentResult>(httpRequestOptions).ConfigureAwait(false);
            
            if (result != null)
            {
                return result.Comments;
            }
            throw new Exception($"Request fail. epId={epId}");
        }

        protected void LimitRequestFrequently(double intervalMilliseconds = 1000)
        {
            var diff = 0;
            lock (_lock)
            {
                var ts = DateTime.Now - lastRequestTime;
                diff = (int)(intervalMilliseconds - ts.TotalMilliseconds);
                lastRequestTime = DateTime.Now;
            }

            if (diff > 0)
            {
                this._logger.Debug("请求太频繁，等待{0}毫秒后继续执行...", diff);
                Thread.Sleep(diff);
            }
        }

        internal static string NormalizeProxyCorsUrl(string proxyCorsUrl)
        {
            var normalized = (proxyCorsUrl ?? string.Empty).Trim();
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException(
                    "Dandanplay proxy API is enabled, but its CORS prefix is missing or invalid. Configure an absolute HTTP or HTTPS URL.");
            }

            return normalized.TrimEnd('/') + "/";
        }

        internal static bool ResolveUseOfficialProxyCors(DandanOption config)
        {
            if (config == null)
            {
                return true;
            }

            return config.UseOfficialProxyCors ?? string.IsNullOrWhiteSpace(config.ProxyCorsUrl);
        }

        internal static string RouteOfficialUrl(
            string officialUrl,
            bool useProxyApi,
            bool useOfficialProxyCors,
            string proxyCorsUrl)
        {
            if (!useProxyApi)
            {
                return officialUrl;
            }

            if (useOfficialProxyCors)
            {
                return OfficialProxyCorsBaseUrl + officialUrl;
            }

            return NormalizeProxyCorsUrl(proxyCorsUrl) + officialUrl;
        }

        internal static string RouteOfficialUrl(string officialUrl, bool useProxyApi, string proxyCorsUrl)
        {
            return RouteOfficialUrl(
                officialUrl,
                useProxyApi,
                useProxyApi && string.IsNullOrWhiteSpace(proxyCorsUrl),
                proxyCorsUrl);
        }

        internal static bool ShouldAddLocalAuthentication(bool useProxyApi)
        {
            return !useProxyApi;
        }

        private void AddAuthenticationIfRequired(
            HttpRequestOptions httpRequestOptions,
            string officialUrl,
            bool useProxyApi)
        {
            if (!ShouldAddLocalAuthentication(useProxyApi))
            {
                return;
            }

            InjectAppId(httpRequestOptions, officialUrl);
        }

        private void InjectAppId(HttpRequestOptions httpRequestOptions, string url)
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var credentials = ResolveCredentials();
            var signature = GenerateSignature(url, timestamp, credentials);
            httpRequestOptions.RequestHeaders.Add("X-AppId", credentials.ApiId);
            httpRequestOptions.RequestHeaders.Add("X-Signature", signature);
            httpRequestOptions.RequestHeaders.Add("X-Timestamp", timestamp.ToString());
        }

        protected string GenerateSignature(string url, long timestamp)
        {
            return GenerateSignature(url, timestamp, ResolveCredentials());
        }

        private static string GenerateSignature(string url, long timestamp, DandanCredentials credentials)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var str = $"{credentials.ApiId}{timestamp}{path}{credentials.ApiSecret}";
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(str));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
