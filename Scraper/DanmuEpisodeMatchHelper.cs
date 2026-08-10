using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Scraper
{
    public static class DanmuEpisodeMatchHelper
    {
        public static int? SuggestSourceEpisodeNumber(int? localEpisodeNumber, int availableEpisodes)
        {
            var number = localEpisodeNumber ?? 0;
            return IsValidSourceEpisodeNumber(number, availableEpisodes) ? number : (int?)null;
        }

        public static bool IsValidSourceEpisodeNumber(int sourceEpisodeNumber, int availableEpisodes)
        {
            return sourceEpisodeNumber > 0 && sourceEpisodeNumber <= availableEpisodes;
        }

        public static int? SuggestSourceEpisodeNumber(
            int? localEpisodeNumber,
            IEnumerable<ScraperEpisode> sourceEpisodes)
        {
            var number = localEpisodeNumber ?? 0;
            return TryGetSourceEpisode(sourceEpisodes, number, out _)
                ? number
                : (int?)null;
        }

        public static bool TryGetSourceEpisode(
            IEnumerable<ScraperEpisode> sourceEpisodes,
            int sourceEpisodeNumber,
            out ScraperEpisode sourceEpisode)
        {
            sourceEpisode = null;
            if (sourceEpisodeNumber <= 0 || sourceEpisodes == null)
            {
                return false;
            }

            var episodes = sourceEpisodes.ToList();
            if (episodes.Any(episode => episode?.EpisodeNumber.HasValue == true))
            {
                sourceEpisode = episodes.FirstOrDefault(episode =>
                    episode?.EpisodeNumber == sourceEpisodeNumber);
                return sourceEpisode != null;
            }

            // Compatibility fallback for providers or previously constructed
            // media that expose no reliable source numbering at all.
            sourceEpisode = episodes.ElementAtOrDefault(sourceEpisodeNumber - 1);
            return sourceEpisode != null;
        }
    }
}
