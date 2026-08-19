using System.Linq;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>One server-side definition for an accepted prefix followed by
    /// ordinary temporary rows. Both Series aggregation and single-Season
    /// rendering consume this instead of inferring a failure from a remainder
    /// stop.</summary>
    public static class CompositeSeasonPartialState
    {
        public static bool HasConfirmedPartialMappings(CompositeSeasonPlan plan)
        {
            return plan != null && (plan.Mappings ?? Enumerable.Empty<CompositeSeasonEpisodeMapping>()).Any() &&
                   (plan.UnmatchedRuns ?? Enumerable.Empty<CompositeSeasonUnmatchedRun>())
                   .Any(run => run != null && (run.Episodes ?? Enumerable.Empty<CompositeSeasonLocalEpisode>()).Any());
        }
    }
}
