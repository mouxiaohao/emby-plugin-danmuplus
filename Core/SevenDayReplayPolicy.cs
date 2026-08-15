using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Defines the deliberately narrow replay contract for files skipped only
    /// because the existing XML was written within seven days.
    /// </summary>
    public static class SevenDayReplayPolicy
    {
        public const string RecentFileSkipReason = "seven_day_recent_file";
        public const string ReplayKind = "seven_day_skipped";

        public static bool IsTerminal(string status)
        {
            return string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "completed_with_warnings", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "completed_with_errors", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRecentFileSkip(DanmuEpisodeDownloadResult episode)
        {
            return episode != null &&
                   string.Equals(episode.Status, "skipped", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(episode.SkipReason, RecentFileSkipReason, StringComparison.Ordinal);
        }

        public static List<DanmuEpisodeDownloadResult> FreezeEligibleEpisodes(
            IEnumerable<DanmuEpisodeDownloadResult> originEpisodes,
            ISet<string> acceptedItemIds)
        {
            var accepted = acceptedItemIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return (originEpisodes ?? Enumerable.Empty<DanmuEpisodeDownloadResult>())
                .Where(episode => IsRecentFileSkip(episode) &&
                                  !accepted.Contains(episode.ItemId ?? string.Empty))
                .Select(CloneEpisode)
                .ToList();
        }

        public static DanmuEpisodeDownloadResult CloneEpisode(DanmuEpisodeDownloadResult episode)
        {
            if (episode == null) return null;
            return new DanmuEpisodeDownloadResult
            {
                ItemId = episode.ItemId ?? string.Empty,
                EpisodeNumber = episode.EpisodeNumber,
                SourceEpisodeNumber = episode.SourceEpisodeNumber,
                EpisodeName = episode.EpisodeName ?? string.Empty,
                SourceSite = episode.SourceSite ?? string.Empty,
                SourceCandidateId = episode.SourceCandidateId ?? string.Empty,
                SourceEpisodeId = episode.SourceEpisodeId ?? string.Empty,
                SourceScopeType = episode.SourceScopeType ?? string.Empty,
                MatchOrigin = episode.MatchOrigin ?? string.Empty,
                Status = episode.Status ?? string.Empty,
                Message = episode.Message ?? string.Empty,
                SkipReason = episode.SkipReason ?? string.Empty,
            };
        }
    }
}
