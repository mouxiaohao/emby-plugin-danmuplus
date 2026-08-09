using System;
using Emby.Plugin.Danmu.Scraper.Entity;
using BilibiliMedia = Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media;

namespace Emby.Plugin.Danmu.Scraper.Bilibili
{
    internal static class BilibiliSearchResultMapper
    {
        internal static bool TryMap(BilibiliMedia media, out ScraperSearchInfo searchInfo, out string skipReason)
        {
            searchInfo = null;
            skipReason = string.Empty;

            if (media == null)
            {
                skipReason = "null media record";
                return false;
            }

            var id = ResolveId(media);
            if (id <= 0)
            {
                skipReason = "no positive season or media identifier";
                return false;
            }

            var title = media.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                skipReason = "empty title";
                return false;
            }

            var category = FirstNonEmpty(media.SeasonTypeName, media.ApiType, media.TypeName);
            var year = ResolveYear(media);
            searchInfo = new ScraperSearchInfo
            {
                Id = id.ToString(),
                Name = title,
                Category = category,
                Year = year > 0 ? year : (int?)null,
                EpisodeSize = Math.Max(0, media.EpisodeSize)
            };

            return true;
        }

        internal static long ResolveId(BilibiliMedia media)
        {
            if (media == null)
            {
                return 0;
            }

            if (media.SeasonId > 0)
            {
                return media.SeasonId;
            }

            if (media.PgcSeasonId > 0)
            {
                return media.PgcSeasonId;
            }

            return media.MediaId > 0 ? media.MediaId : 0;
        }

        private static int ResolveYear(BilibiliMedia media)
        {
            if (!string.IsNullOrWhiteSpace(media.PubDate) &&
                DateTime.TryParse(media.PubDate, out var parsedDate))
            {
                return parsedDate.Year;
            }

            if (media.PubTime <= 0)
            {
                return 0;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(media.PubTime).Year;
            }
            catch (ArgumentOutOfRangeException)
            {
                return 0;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }
}
