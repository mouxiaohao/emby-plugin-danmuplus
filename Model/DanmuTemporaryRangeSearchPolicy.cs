using System;
using System.Linq;

namespace Emby.Plugin.Danmu.Model
{
    /// <summary>
    /// Validates the browser's temporary-season search intent against the
    /// already rebuilt, exclusion-aware composite plan.  A search may only
    /// describe one complete unmatched run; it can never narrow, extend, or
    /// start in the middle of a run that will later be applied as a mapping.
    /// </summary>
    public static class DanmuTemporaryRangeSearchPolicy
    {
        public static bool TryResolveUnmatchedRun(
            CompositeSeasonPlan plan,
            string localStartEpisodeItemId,
            int requestedEpisodeCount,
            out CompositeSeasonUnmatchedRun run,
            out string error)
        {
            run = null;
            error = string.Empty;
            if (plan == null)
            {
                error = "The authoritative composite plan is unavailable.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(localStartEpisodeItemId) || requestedEpisodeCount <= 0)
            {
                error = "A temporary range requires a local start episode and a positive episode count.";
                return false;
            }

            var matches = (plan.UnmatchedRuns ?? Enumerable.Empty<CompositeSeasonUnmatchedRun>())
                .Where(candidate => candidate != null && candidate.Episodes != null && candidate.Episodes.Count > 0)
                .Where(candidate => string.Equals(
                    candidate.Episodes[0].ItemId,
                    localStartEpisodeItemId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                error = "The temporary range must start at exactly one current unmatched run.";
                return false;
            }

            run = matches[0];
            if (run.Episodes.Count != requestedEpisodeCount)
            {
                run = null;
                error = "The temporary range must cover the complete current unmatched run.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// A dialog-supplied value is an explicit user choice.  Otherwise the
        /// owning Series title is the default and the Season title is only a
        /// fallback.  Returning false prevents an empty-title provider call.
        /// </summary>
        public static bool TryResolveSearchKeyword(
            string requestedKeyword,
            string seriesName,
            string seasonName,
            out string keyword)
        {
            keyword = (requestedKeyword ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            keyword = (seriesName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            keyword = (seasonName ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(keyword);
        }
    }
}
