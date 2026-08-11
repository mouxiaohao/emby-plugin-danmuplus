using System;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Scraper.Bilibili
{
    public static class BilibiliPgcIdPolicy
    {
        public static bool IsPositiveNumericId(string value)
        {
            return long.TryParse(value, out var parsed) && parsed > 0;
        }

        public static bool SupportsExactItem(BaseItem item)
        {
            return item is Movie || item is Season || item is Episode;
        }

        public static bool IsSeasonItem(BaseItem item)
        {
            return item is Season;
        }

        public static string ResolveMovieEpisodeId(ScraperMedia media)
        {
            if (media == null)
            {
                return string.Empty;
            }

            if (IsPositiveNumericId(media.Id) && media.Episodes != null && media.Episodes.Count == 1 &&
                string.Equals(media.Id, media.Episodes[0].Id, StringComparison.Ordinal))
            {
                return media.Id;
            }

            if (media.Episodes != null)
            {
                foreach (var episode in media.Episodes)
                {
                    if (episode != null && IsPositiveNumericId(episode.Id))
                    {
                        return episode.Id;
                    }
                }
            }

            return IsPositiveNumericId(media.CommentId) ? media.CommentId : string.Empty;
        }
    }
}
