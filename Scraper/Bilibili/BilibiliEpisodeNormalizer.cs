using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;

namespace Emby.Plugin.Danmu.Scraper.Bilibili
{
    public static class BilibiliEpisodeNormalizer
    {
        public static List<VideoEpisode> Normalize(IEnumerable<VideoEpisode> source)
        {
            var indexed = (source ?? Enumerable.Empty<VideoEpisode>())
                .Where(x => x != null)
                .Select((episode, index) => new EpisodeEntry(episode, index))
                .Where(x => !IsExplicitNonMain(x.Episode))
                .ToList();

            var numbered = indexed.Where(x => x.Number.HasValue)
                .GroupBy(x => x.Number.Value)
                .Select(group => group
                    .OrderByDescending(x => HasExplicitMainMetadata(x.Episode))
                    .ThenByDescending(x => x.Episode.Duration)
                    .ThenBy(x => x.SourceIndex)
                    .First())
                .OrderBy(x => x.Number.Value);

            var unnumbered = indexed.Where(x => !x.Number.HasValue).OrderBy(x => x.SourceIndex);
            return numbered.Concat(unnumbered).Select(x => x.Episode).ToList();
        }

        private static bool IsExplicitNonMain(VideoEpisode episode)
        {
            return episode.BadgeType == 1 ||
                   (episode.SectionType.HasValue && episode.SectionType.Value != 0) ||
                   EpisodeContentClassifier.IsExplicitNonMain(episode.Badge) ||
                   EpisodeContentClassifier.IsExplicitNonMain(episode.Title) ||
                   EpisodeContentClassifier.IsExplicitNonMain(episode.LongTitle);
        }

        private static bool HasExplicitMainMetadata(VideoEpisode episode)
        {
            return episode.BadgeType == 0 &&
                   (!episode.SectionType.HasValue || episode.SectionType.Value == 0);
        }

        private sealed class EpisodeEntry
        {
            public EpisodeEntry(VideoEpisode episode, int sourceIndex)
            {
                Episode = episode;
                SourceIndex = sourceIndex;
                Number = EpisodeContentClassifier.TryGetEpisodeNumber(episode.Title);
            }

            public VideoEpisode Episode { get; }
            public int SourceIndex { get; }
            public int? Number { get; }
        }
    }
}
