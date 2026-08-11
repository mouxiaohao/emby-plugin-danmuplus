using System.Collections.Generic;
using Emby.Plugin.Danmu.Scraper.Entity;
using DandanEpisode = Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode;

namespace Emby.Plugin.Danmu.Scraper.Dandan
{
    public static class DandanSeasonEpisodeMapper
    {
        public static List<ScraperEpisode> Map(IEnumerable<DandanEpisode> episodes, bool normalizeSeasonOrdinals)
        {
            var result = new List<ScraperEpisode>();
            if (episodes == null)
            {
                return result;
            }

            foreach (var episode in episodes)
            {
                if (episode == null || EpisodeContentClassifier.IsExplicitNonMain(episode.EpisodeTitle))
                {
                    continue;
                }

                result.Add(new ScraperEpisode
                {
                    Id = episode.EpisodeId.ToString(),
                    CommentId = episode.EpisodeId.ToString(),
                    Title = episode.EpisodeTitle,
                    EpisodeNumber = normalizeSeasonOrdinals
                        ? result.Count + 1
                        : EpisodeContentClassifier.TryGetPositiveNumber(episode.EpisodeNumber) ??
                          EpisodeContentClassifier.TryGetEpisodeNumber(episode.EpisodeTitle),
                });
            }

            return result;
        }
    }
}
