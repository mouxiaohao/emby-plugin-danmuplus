using Emby.Plugin.Danmu.Scraper.Entity;

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

            var value = string.Equals(providerId, "BilibiliID", System.StringComparison.OrdinalIgnoreCase)
                ? media.CommentId
                : media.Id;
            if (string.IsNullOrWhiteSpace(value) && media.Episodes.Count > 0)
            {
                value = media.Episodes[0].CommentId;
            }
            return value ?? string.Empty;
        }
    }
}
