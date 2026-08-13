using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// The single r5 boundary for every batch Season operation. It reads only
    /// target.GetEpisodes(); sibling Season inventories cannot claim, lend, or
    /// supplement an Episode for the selected target.
    /// </summary>
    public static class SeasonTargetPlanningCoordinator
    {
        [Obsolete("r5 batch planning no longer resolves ownership across Season targets.")]
        public static CompositeSeasonTargetOwnershipResult ResolveOwnership(
            IEnumerable<CompositeSeasonTargetInventory> source)
        {
            return CompositeSeasonTargetOwnership.Resolve(source);
        }

        public static bool TryBuild(
            Season target,
            out SeasonPlanningContext context,
            out string error)
        {
            context = null;
            error = string.Empty;
            if (target == null)
            {
                error = "target-season-missing";
                return false;
            }
            if (!target.IndexNumber.HasValue)
            {
                error = "target-season-number-unknown";
                return false;
            }

            try
            {
                var result = target.GetEpisodes();
                if (result == null || result.Items == null)
                {
                    error = "target-season-inventory-unavailable";
                    return false;
                }

                return SeasonPlanningContextBuilder.TryBuild(target,
                    result.Items.OfType<Episode>(), out context, out error);
            }
            catch (Exception)
            {
                error = "target-season-inventory-unavailable";
                context = null;
                return false;
            }
        }

        /// <summary>
        /// Compatibility signature retained for r4 callers while intentionally
        /// ignoring sibling inventories. Every request is scoped solely from
        /// the selected target Season.
        /// </summary>
        public static bool TryBuild(
            Season target,
            IEnumerable<Season> ignoredSeriesSeasons,
            out SeasonPlanningContext context,
            out string error)
        {
            return TryBuild(target, out context, out error);
        }
    }
}
