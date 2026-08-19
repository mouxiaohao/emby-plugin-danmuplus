using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Tmdb
{
    public enum TmdbAliasLanguage
    {
        Chinese,
        English,
        Japanese,
    }

    public sealed class TmdbAliasTitle
    {
        public string Title { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public TmdbAliasLanguage Language { get; set; }
        public int SourceOrder { get; set; }
    }

    public sealed class TmdbAliasSet
    {
        public List<TmdbAliasTitle> Chinese { get; } = new List<TmdbAliasTitle>();
        // Kept for source compatibility with r1 consumers. They are deliberately
        // never populated from alternative_titles; primary details own fallback.
        public List<TmdbAliasTitle> English { get; } = new List<TmdbAliasTitle>();
        public List<TmdbAliasTitle> Japanese { get; } = new List<TmdbAliasTitle>();
        /// <summary>
        /// Builds only the bounded Chinese alternative-title plan. English and Japanese
        /// fallback terms must come from localized TMDB media details, never this list.
        /// </summary>
        public IEnumerable<TmdbAliasTitle> BuildSearchPlan(string libraryTitle = null)
        {
            var normalizedLibraryTitle = DanmuMatchScorer.Normalize(libraryTitle);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return Chinese
                .Where(alias => alias != null && !string.IsNullOrWhiteSpace(alias.Title))
                .Where(alias =>
                {
                    var normalized = DanmuMatchScorer.Normalize(alias.Title);
                    return normalized.Length > 0 &&
                           (normalizedLibraryTitle.Length == 0 ||
                            !normalizedLibraryTitle.Contains(normalized));
                })
                .Where(alias => seen.Add(DanmuMatchScorer.Normalize(alias.Title)))
                .OrderBy(TmdbAliasClient.GetChineseTier)
                .ThenBy(alias => DanmuMatchScorer.Normalize(alias.Title).Length)
                .ThenBy(alias => alias.SourceOrder)
                .Take(3)
                .ToList();
        }
    }

    [DataContract]
    internal sealed class TmdbMediaDetails
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "original_name")]
        public string OriginalName { get; set; }

        [DataMember(Name = "original_title")]
        public string OriginalTitle { get; set; }

        [DataMember(Name = "original_language")]
        public string OriginalLanguage { get; set; }
    }

    [DataContract]
    internal sealed class TmdbAlternativeTitleResponse
    {
        [DataMember(Name = "titles")]
        public List<TmdbAlternativeTitle> Titles { get; set; } = new List<TmdbAlternativeTitle>();

        [DataMember(Name = "results")]
        public List<TmdbAlternativeTitle> Results { get; set; } = new List<TmdbAlternativeTitle>();
    }

    [DataContract]
    internal sealed class TmdbAlternativeTitle
    {
        [DataMember(Name = "iso_3166_1")]
        public string Country { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }
    }

    /// <summary>
    /// Reads only public TMDB application data. It never writes TMDB metadata back to Emby.
    /// </summary>
    public static class TmdbAliasClient
    {
        private const string ApiBaseUrl = "https://api.themoviedb.org/3";
        private const int RequestTimeoutMs = 10000;
        internal const string UserAgent = "DanmuPlus/2.0.6r2";
        private const int MaximumCachedResponses = 128;
        private static readonly TimeSpan SuccessfulCacheLifetime = TimeSpan.FromHours(24);
        private static readonly TimeSpan EmptyCacheLifetime = TimeSpan.FromHours(1);
        private static readonly ConcurrentDictionary<string, Lazy<Task<TmdbCacheValue>>> ResponseCache =
            new ConcurrentDictionary<string, Lazy<Task<TmdbCacheValue>>>(StringComparer.Ordinal);
        internal static Func<HttpRequestOptions, Task<HttpResponseInfo>> HttpGetResponseAsync =
            request => SingletonManager.HttpClient.GetResponse(request);

        private sealed class TmdbCacheValue
        {
            public object Value { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
            public bool Cacheable { get; set; }
        }

        public static bool IsConfigured(TmdbOption option)
        {
            return option != null && option.UseAliasSearch &&
                   (!string.IsNullOrWhiteSpace(option.ReadAccessToken) ||
                    !string.IsNullOrWhiteSpace(option.ApiKey));
        }

        public static bool TryResolveIdentifier(BaseItem item, out string tmdbId, out bool isMovie)
        {
            tmdbId = string.Empty;
            isMovie = item is Movie;
            var current = item;
            for (var depth = 0; current != null && depth < 4; depth++)
            {
                if (current.ProviderIds != null && current.ProviderIds.TryGetValue("Tmdb", out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    tmdbId = value.Trim();
                    isMovie = item is Movie;
                    return true;
                }

                current = current.GetParent();
            }

            return false;
        }

        public static bool IsAnimated(BaseItem item)
        {
            var current = item;
            for (var depth = 0; current != null && depth < 4; depth++)
            {
                if ((current.Genres ?? Array.Empty<string>()).Any(IsAnimationGenre))
                {
                    return true;
                }

                current = current.GetParent();
            }

            return false;
        }

        public static async Task<TmdbAliasSet> GetAliasesAsync(
            BaseItem item,
            TmdbOption option,
            ILogger logger,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!IsConfigured(option) || !IsAnimated(item) ||
                !TryResolveIdentifier(item, out var tmdbId, out var isMovie) ||
                !IsSafeIdentifier(tmdbId))
            {
                return null;
            }

            var path = isMovie ? "movie" : "tv";
            var endpoint = ApiBaseUrl + "/" + path + "/" + Uri.EscapeDataString(tmdbId) + "/alternative_titles";
            return await GetCachedAsync<TmdbAliasSet>(
                "aliases|" + path + "|" + tmdbId,
                async () =>
                {
                    var response = await GetJsonAsync<TmdbAlternativeTitleResponse>(
                        endpoint, string.Empty, option, logger, cancellationToken, "alias").ConfigureAwait(false);
                    if (!response.Cacheable)
                    {
                        return response;
                    }

                    var aliases = Normalize(SelectAlternativeTitles(
                        response.Value as TmdbAlternativeTitleResponse, isMovie));
                    return CreateCacheValue(aliases, aliases.Chinese.Count > 0);
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a localized TMDB primary-title document only after the caller has
        /// exhausted earlier search rounds. Alternative titles are intentionally not
        /// consulted here.
        /// </summary>
        internal static async Task<TmdbMediaDetails> GetDetailsAsync(
            BaseItem item,
            TmdbOption option,
            string language,
            ILogger logger,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!IsConfigured(option) || !IsAnimated(item) ||
                !TryResolveIdentifier(item, out var tmdbId, out var isMovie) ||
                !IsSafeIdentifier(tmdbId) || string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            var path = isMovie ? "movie" : "tv";
            var endpoint = ApiBaseUrl + "/" + path + "/" + Uri.EscapeDataString(tmdbId);
            var query = "language=" + Uri.EscapeDataString(language.Trim());
            return await GetCachedAsync<TmdbMediaDetails>(
                "details|" + path + "|" + tmdbId + "|" + language.Trim().ToLowerInvariant(),
                () => GetJsonAsync<TmdbMediaDetails>(
                    endpoint, query, option, logger, cancellationToken, "details")).ConfigureAwait(false);
        }

        internal static string GetLocalizedPrimaryTitle(TmdbMediaDetails details, bool isMovie)
        {
            return (isMovie ? details?.Title : details?.Name)?.Trim() ?? string.Empty;
        }

        internal static string GetJapaneseOriginalPrimaryTitle(TmdbMediaDetails details, bool isMovie)
        {
            return (isMovie ? details?.OriginalTitle : details?.OriginalName)?.Trim() ?? string.Empty;
        }

        private static async Task<T> GetCachedAsync<T>(
            string cacheKey,
            Func<Task<TmdbCacheValue>> request) where T : class
        {
            if (ResponseCache.TryGetValue(cacheKey, out var existing) &&
                existing.IsValueCreated && existing.Value.IsCompleted)
            {
                var value = await existing.Value.ConfigureAwait(false);
                if (!value.Cacheable || value.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    ResponseCache.TryRemove(cacheKey, out var ignored);
                }
            }

            if (!ResponseCache.ContainsKey(cacheKey) && ResponseCache.Count >= MaximumCachedResponses)
            {
                foreach (var cached in ResponseCache.Where(pair => pair.Value.IsValueCreated &&
                    pair.Value.Value.IsCompleted && pair.Value.Value.Result.ExpiresAt <= DateTimeOffset.UtcNow))
                {
                    ResponseCache.TryRemove(cached.Key, out var ignored);
                }

                // This cache is only an optimization. Clearing a full cache is safer
                // than allowing an unbounded TMDB-id collection in the plugin process.
                if (ResponseCache.Count >= MaximumCachedResponses)
                {
                    ResponseCache.Clear();
                }
            }

            var lazy = ResponseCache.GetOrAdd(cacheKey,
                _ => new Lazy<Task<TmdbCacheValue>>(request, LazyThreadSafetyMode.ExecutionAndPublication));
            var response = await lazy.Value.ConfigureAwait(false);
            if (!response.Cacheable)
            {
                ResponseCache.TryRemove(cacheKey, out var ignored);
            }

            return response.Value as T;
        }

        private static TmdbCacheValue CreateCacheValue(object value, bool hasValue)
        {
            return new TmdbCacheValue
            {
                Value = value,
                Cacheable = true,
                ExpiresAt = DateTimeOffset.UtcNow +
                    (hasValue ? SuccessfulCacheLifetime : EmptyCacheLifetime),
            };
        }

        private static async Task<TmdbCacheValue> GetJsonAsync<T>(
            string endpoint,
            string query,
            TmdbOption option,
            ILogger logger,
            CancellationToken cancellationToken,
            string resource) where T : class
        {
            var token = option.ReadAccessToken?.Trim();
            var apiKey = option.ApiKey?.Trim();
            var attempts = new List<string>();
            if (!string.IsNullOrWhiteSpace(token)) attempts.Add("read_access_token");
            if (!string.IsNullOrWhiteSpace(apiKey)) attempts.Add("api_key");

            foreach (var authentication in attempts)
            {
                var separator = string.IsNullOrWhiteSpace(query) ? "?" : "?";
                var url = string.IsNullOrWhiteSpace(query) ? endpoint : endpoint + separator + query;
                var request = new HttpRequestOptions
                {
                    Url = url,
                    // Emby 4.9 still honors LogUrl when writing request and exception details.
                    // Keep it credential-free even though newer hosts also support Sanitation.
#pragma warning disable CS0618 // Emby 4.9 reads LogUrl; keep it credential-free as a compatibility fallback.
                    LogUrl = url,
#pragma warning restore CS0618
                    UserAgent = UserAgent,
                    AcceptHeader = "application/json",
                    TimeoutMs = RequestTimeoutMs,
                    CancellationToken = cancellationToken,
                };
                request.Sanitation.SanitizeParams.Add("api_key");
                if (!string.IsNullOrWhiteSpace(token)) request.Sanitation.SanitizeStrings.Add(token);
                if (!string.IsNullOrWhiteSpace(apiKey)) request.Sanitation.SanitizeStrings.Add(apiKey);
                if (authentication == "read_access_token")
                {
                    request.RequestHeaders["Authorization"] = "Bearer " + token;
                }
                else
                {
                    request.Url += (request.Url.Contains("?") ? "&" : "?") +
                        "api_key=" + Uri.EscapeDataString(apiKey);
#pragma warning disable CS0618 // See the Emby 4.9 compatibility note above.
                    request.LogUrl += (request.LogUrl.Contains("?") ? "&" : "?") + "api_key=[REDACTED]";
#pragma warning restore CS0618
                }

                try
                {
                    var response = await HttpGetResponseAsync(request).ConfigureAwait(false);
                    if (response != null && response.StatusCode == HttpStatusCode.OK)
                    {
                        var serializer = SingletonManager.JsonSerializer;
                        var parsed = serializer != null
                            ? serializer.DeserializeFromStream<T>(response.Content)
                            : (T)new DataContractJsonSerializer(typeof(T)).ReadObject(response.Content);
                        return CreateCacheValue(parsed, HasPrimaryData(parsed));
                    }

                    logger?.Warn("TMDB {0} request failed with {1}; trying configured fallback if available",
                        resource, authentication);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    logger?.Warn("TMDB {0} request timed out or was cancelled", resource);
                    return new TmdbCacheValue();
                }
                catch (Exception ex)
                {
                    logger?.Warn(
                        "TMDB {0} request failed with {1} ({2}); trying configured fallback if available",
                        resource, authentication, ex.GetType().Name);
                }
            }

            return new TmdbCacheValue();
        }

        private static bool HasPrimaryData<T>(T value) where T : class
        {
            var details = value as TmdbMediaDetails;
            return details != null && (!string.IsNullOrWhiteSpace(details.Name) ||
                   !string.IsNullOrWhiteSpace(details.Title) ||
                   !string.IsNullOrWhiteSpace(details.OriginalName) ||
                   !string.IsNullOrWhiteSpace(details.OriginalTitle));
        }

        internal static IEnumerable<TmdbAlternativeTitle> SelectAlternativeTitles(
            TmdbAlternativeTitleResponse response,
            bool isMovie)
        {
            return isMovie ? response?.Titles : response?.Results;
        }

        internal static TmdbAliasSet Normalize(IEnumerable<TmdbAlternativeTitle> titles)
        {
            var result = new TmdbAliasSet();
            var sourceOrder = 0;
            foreach (var entry in titles ?? Enumerable.Empty<TmdbAlternativeTitle>())
            {
                var title = entry?.Title?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    sourceOrder++;
                    continue;
                }

                var key = DanmuMatchScorer.Normalize(title);
                if (key.Length == 0)
                {
                    sourceOrder++;
                    continue;
                }

                if (IsChinese(entry.Country, title))
                {
                    result.Chinese.Add(new TmdbAliasTitle
                    {
                        Title = title,
                        Country = entry.Country ?? string.Empty,
                        Language = TmdbAliasLanguage.Chinese,
                        SourceOrder = sourceOrder,
                    });
                }

                sourceOrder++;
            }

            return result;
        }

        internal static int GetChineseTier(TmdbAliasTitle alias)
        {
            var traditional = IsTraditionalChinese(alias?.Title, alias?.Country);
            var fullTitle = HasSubtitleBoundary(alias?.Title);
            if (!traditional)
            {
                return fullTitle ? 2 : 1;
            }

            return fullTitle ? 4 : 3;
        }

        private static bool IsSafeIdentifier(string value)
        {
            return value.All(char.IsDigit);
        }

        private static bool IsAnimationGenre(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized.Contains("动画") || normalized.Contains("動漫") ||
                   normalized.Contains("动漫") || normalized.Contains("アニメ") ||
                   normalized.Contains("番剧") || normalized.Contains("番劇") ||
                   normalized.Contains("animation") ||
                   normalized.Contains("anime");
        }

        private static bool IsChinese(string country, string title)
        {
            return string.Equals(country, "CN", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(country, "TW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(country, "HK", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(country, "MO", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(country, "SG", StringComparison.OrdinalIgnoreCase) ||
                   ContainsChinese(title) && !ContainsJapanese(title);
        }

        private static bool IsTraditionalChinese(string title, string country)
        {
            var traditionalEvidence = CountCharacters(title, "書癡剋為擇後臺萬與國體學習歡樂觀點麼開發網頁數據畫龍貓聲優裡這個隻沒過於當從來說話實際應該雲愛貓" );
            var simplifiedEvidence = CountCharacters(title, "书痴克为择后台万与国体学习欢乐观点么开发网页数据画龙猫声优里这个只没过于当从来说话实际应该云爱猫");
            if (traditionalEvidence > 0 && traditionalEvidence >= simplifiedEvidence)
            {
                return true;
            }

            if (simplifiedEvidence > 0)
            {
                return false;
            }

            return string.Equals(country, "TW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(country, "HK", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(country, "MO", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountCharacters(string value, string evidence)
        {
            return (value ?? string.Empty).Count(c => evidence.IndexOf(c) >= 0);
        }

        private static bool HasSubtitleBoundary(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            const string separators = ":：~～〜—–-";
            for (var index = 1; index < value.Length - 1; index++)
            {
                if (separators.IndexOf(value[index]) >= 0)
                {
                    var left = index - 1;
                    var right = index + 1;
                    while (left >= 0 && char.IsWhiteSpace(value[left])) left--;
                    while (right < value.Length && char.IsWhiteSpace(value[right])) right++;
                    if (left >= 0 && right < value.Length &&
                        separators.IndexOf(value[left]) < 0 && separators.IndexOf(value[right]) < 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsChinese(string value)
        {
            return value.Any(c => c >= 0x4e00 && c <= 0x9fff);
        }

        private static bool ContainsJapanese(string value)
        {
            return value.Any(c => (c >= 0x3040 && c <= 0x30ff) || (c >= 0xff66 && c <= 0xff9f));
        }
    }
}
