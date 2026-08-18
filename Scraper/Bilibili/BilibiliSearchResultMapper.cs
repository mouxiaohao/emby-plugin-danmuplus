using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Scraper.Entity;
using BilibiliMedia = Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media;

namespace Emby.Plugin.Danmu.Scraper.Bilibili
{
    internal enum BilibiliMediaKind
    {
        Unknown,
        Movie,
        Season,
    }

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

            var kind = ResolveKind(media);
            var category = FirstNonEmpty(
                media.SeasonTypeName,
                kind == BilibiliMediaKind.Movie ? "movie" :
                kind == BilibiliMediaKind.Season ? "anime" : string.Empty,
                media.ApiType,
                media.TypeName);
            var year = ResolveYear(media);
            searchInfo = new ScraperSearchInfo
            {
                Id = id.ToString(),
                Name = title,
                Category = category,
                Year = year > 0 ? year : (int?)null,
                EpisodeSize = Math.Max(0, media.EpisodeSize),
                Aliases = string.IsNullOrWhiteSpace(media.OrgTitle)
                    ? new List<string>()
                    : new List<string> { media.OrgTitle.Trim() }
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

        internal static bool IsAllowedTypedMedia(BilibiliMedia media, string expectedType)
        {
            if (media == null ||
                (expectedType != "media_ft" && expectedType != "media_bangumi"))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(media.ApiType) ||
                string.Equals(media.ApiType, expectedType, StringComparison.Ordinal);
        }

        internal static bool IsAllowedAggregateMedia(BilibiliMedia media)
        {
            return media != null &&
                (string.IsNullOrWhiteSpace(media.ApiType) ||
                 string.Equals(media.ApiType, "media_ft", StringComparison.Ordinal) ||
                 string.Equals(media.ApiType, "media_bangumi", StringComparison.Ordinal));
        }

        internal static BilibiliMediaKind ResolveKind(BilibiliMedia media)
        {
            if (media == null) return BilibiliMediaKind.Unknown;

            var category = FirstNonEmpty(media.SeasonTypeName, media.TypeName).ToLowerInvariant();
            if (category.Contains("电影") || category.Contains("movie") || category.Contains("film"))
                return BilibiliMediaKind.Movie;
            if (!string.IsNullOrWhiteSpace(category)) return BilibiliMediaKind.Season;

            // Bilibili season_type=2 is Movie; other positive PGC season types
            // (bangumi, documentary, TV, variety, etc.) are season-like.
            if (media.SeasonType == 2) return BilibiliMediaKind.Movie;
            if (media.SeasonType > 0) return BilibiliMediaKind.Season;

            if (string.Equals(media.ApiType, "media_bangumi", StringComparison.Ordinal))
                return BilibiliMediaKind.Season;
            return string.Equals(media.ApiType, "media_ft", StringComparison.Ordinal)
                ? BilibiliMediaKind.Movie
                : BilibiliMediaKind.Unknown;
        }

        internal static List<BilibiliMedia> MergeByCanonicalIdentity(
            IEnumerable<BilibiliMedia> media,
            int limit)
        {
            var merged = new Dictionary<long, BilibiliMedia>();
            foreach (var item in media ?? Enumerable.Empty<BilibiliMedia>())
            {
                if (!TryMap(item, out _, out _))
                {
                    continue;
                }

                var id = ResolveId(item);
                if (!merged.TryGetValue(id, out var current) || MetadataScore(item) > MetadataScore(current))
                {
                    merged[id] = item;
                }
            }

            return merged.Values.Take(Math.Max(0, limit)).ToList();
        }

        private static int MetadataScore(BilibiliMedia media)
        {
            return (!string.IsNullOrWhiteSpace(media.Title) ? 8 : 0) +
                (!string.IsNullOrWhiteSpace(media.SeasonTypeName) ? 4 : 0) +
                (!string.IsNullOrWhiteSpace(media.ApiType) ? 2 : 0) +
                (media.PubTime > 0 || !string.IsNullOrWhiteSpace(media.PubDate) ? 2 : 0) +
                (media.EpisodeSize > 0 ? 1 : 0);
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
