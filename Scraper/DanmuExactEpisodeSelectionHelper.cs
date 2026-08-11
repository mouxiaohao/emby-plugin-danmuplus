using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Converts one freshly revalidated source Episode into the one-item media
    /// shape used by the existing download pipeline. Selection is always by
    /// immutable upstream Episode ID, never by display number or list position.
    /// </summary>
    public static class DanmuExactEpisodeSelectionHelper
    {
        public static bool TryCreateExactMedia(
            ScraperMedia resolvedMedia,
            string sourceEpisodeId,
            out ScraperMedia exactMedia,
            out ScraperEpisode sourceEpisode)
        {
            exactMedia = null;
            sourceEpisode = null;
            if (resolvedMedia == null || string.IsNullOrWhiteSpace(sourceEpisodeId))
            {
                return false;
            }

            sourceEpisode = (resolvedMedia.Episodes ?? new List<ScraperEpisode>())
                .FirstOrDefault(episode => episode != null &&
                    string.Equals(episode.Id, sourceEpisodeId, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(episode.CommentId));
            if (sourceEpisode == null)
            {
                return false;
            }

            exactMedia = new ScraperMedia
            {
                Id = resolvedMedia.Id,
                ProviderId = resolvedMedia.ProviderId,
                Title = resolvedMedia.Title,
                Category = resolvedMedia.Category,
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode
                    {
                        Id = sourceEpisode.Id,
                        CommentId = sourceEpisode.CommentId,
                        ParentMediaId = sourceEpisode.ParentMediaId,
                        EpisodeNumber = 1,
                        Title = sourceEpisode.Title,
                    },
                },
            };
            return true;
        }
    }
}
