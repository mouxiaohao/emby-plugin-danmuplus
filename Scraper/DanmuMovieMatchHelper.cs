using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using BilibiliScraper = Emby.Plugin.Danmu.Scraper.Bilibili.Bilibili;

namespace Emby.Plugin.Danmu.Scraper
{
    public static class DanmuMovieMatchHelper
    {
        public static string ResolveEpisodeLookupId(string providerId, ScraperMedia media)
        {
            if (media == null)
            {
                return string.Empty;
            }

            var isBilibili = string.Equals(
                providerId, BilibiliScraper.ScraperProviderId, System.StringComparison.OrdinalIgnoreCase);
            var value = isBilibili
                ? BilibiliPgcIdPolicy.ResolveMovieEpisodeId(media)
                : media.Id;
            if (!isBilibili && string.IsNullOrWhiteSpace(value) && media.Episodes.Count > 0)
            {
                value = media.Episodes[0].CommentId;
            }
            return value ?? string.Empty;
        }
    }
}
