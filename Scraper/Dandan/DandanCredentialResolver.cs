using System;

namespace Emby.Plugin.Danmu.Scraper.Dandan
{
    public sealed class DandanCredentials
    {
        public DandanCredentials(string apiId, string apiSecret, string source)
        {
            ApiId = apiId;
            ApiSecret = apiSecret;
            Source = source;
        }

        public string ApiId { get; }
        public string ApiSecret { get; }
        public string Source { get; }
    }

    public static class DandanCredentialResolver
    {
        public static DandanCredentials Resolve(
            string configuredId,
            string configuredSecret,
            string environmentId,
            string environmentSecret,
            string legacyId,
            string legacySecret)
        {
            var config = Pair(configuredId, configuredSecret, "插件配置");
            if (config != null) return config;

            var environment = Pair(environmentId, environmentSecret, "环境变量");
            if (environment != null) return environment;

            var legacy = Pair(legacyId, legacySecret, "内置配置");
            if (legacy != null) return legacy;

            throw new InvalidOperationException("弹弹play接口缺少 API ID 和 API Secret，请在插件设置中填写完整凭据");
        }

        private static DandanCredentials Pair(string apiId, string apiSecret, string source)
        {
            var normalizedId = (apiId ?? string.Empty).Trim();
            var normalizedSecret = (apiSecret ?? string.Empty).Trim();
            if (normalizedId.Length == 0 && normalizedSecret.Length == 0)
            {
                return null;
            }

            if (normalizedId.Length == 0 || normalizedSecret.Length == 0)
            {
                throw new InvalidOperationException("弹弹play" + source + "中的 API ID/API Secret 不完整，请同时填写两项");
            }

            return new DandanCredentials(normalizedId, normalizedSecret, source);
        }
    }
}
