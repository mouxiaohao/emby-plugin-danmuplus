using System;
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

    /// <summary>
    /// Dandanplay episode identifiers are the parent AnimeId followed by a
    /// four-digit episode suffix. The split only locates a candidate parent;
    /// callers must still verify the original full identifier against the API.
    /// </summary>
    public static class DandanEpisodeId
    {
        private const int EpisodeSuffixLength = 4;

        public static bool TryGetAnimeId(string episodeId, out long animeId)
        {
            animeId = 0;
            if (string.IsNullOrWhiteSpace(episodeId) ||
                episodeId.Length <= EpisodeSuffixLength)
            {
                return false;
            }

            foreach (var character in episodeId)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }
            }

            return long.TryParse(
                episodeId.Substring(0, episodeId.Length - EpisodeSuffixLength),
                out animeId) && animeId > 0;
        }

        /// <summary>
        /// Converts an EpisodeId into downloadable metadata only after the
        /// exact same ID was found in the parent Anime detail response.
        /// </summary>
        public static ScraperEpisode CreateVerifiedEpisode(
            string episodeId,
            IEnumerable<DandanEpisode> episodes)
        {
            if (!TryGetAnimeId(episodeId, out var animeId) || episodes == null)
            {
                return null;
            }

            DandanEpisode matchedEpisode = null;
            foreach (var episode in episodes)
            {
                if (episode != null && string.Equals(
                    episode.EpisodeId.ToString(), episodeId, StringComparison.Ordinal))
                {
                    matchedEpisode = episode;
                    break;
                }
            }

            if (matchedEpisode == null)
            {
                return null;
            }

            return new ScraperEpisode
            {
                Id = matchedEpisode.EpisodeId.ToString(),
                CommentId = matchedEpisode.EpisodeId.ToString(),
                ParentMediaId = animeId.ToString(),
                Title = matchedEpisode.EpisodeTitle,
                EpisodeNumber = EpisodeContentClassifier.TryGetPositiveNumber(matchedEpisode.EpisodeNumber) ??
                    EpisodeContentClassifier.TryGetEpisodeNumber(matchedEpisode.EpisodeTitle),
            };
        }
    }
}
