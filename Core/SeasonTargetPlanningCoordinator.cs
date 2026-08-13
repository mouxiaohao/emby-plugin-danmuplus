using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Core
{
    public static class SeasonTargetPlanningCoordinator
    {
        public static CompositeSeasonTargetOwnershipResult ResolveOwnership(
            IEnumerable<CompositeSeasonTargetInventory> source)
        {
            return CompositeSeasonTargetOwnership.Resolve(source);
        }

        public static bool TryBuild(
            Season target,
            IEnumerable<Season> seriesSeasons,
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

            var seasons = (seriesSeasons ?? Enumerable.Empty<Season>())
                .Where(item => item != null).ToList();
            if (!seasons.Any(item => item.Id == target.Id)) seasons.Add(target);
            var contexts = seasons.ToDictionary(item => item.Id.ToString(), item =>
                SeasonPlanningContextBuilder.Build(item,
                    (item.GetEpisodes()?.Items ?? Array.Empty<BaseItem>()).OfType<Episode>()),
                StringComparer.OrdinalIgnoreCase);
            var ownership = ResolveOwnership(seasons.Select(item =>
                new CompositeSeasonTargetInventory
                {
                    TargetId = item.Id.ToString(),
                    TargetSeasonNumber = item.IndexNumber,
                    Episodes = contexts[item.Id.ToString()].LocalEpisodes,
                }));
            if (!ownership.IsValid)
            {
                error = string.Join(",", ownership.Conflicts.Select(conflict =>
                    conflict.Code + ":" + conflict.ItemId));
                return false;
            }

            var targetId = target.Id.ToString();
            var owners = ownership.Assignments.ToDictionary(item => item.ItemId, item => item.TargetId,
                StringComparer.OrdinalIgnoreCase);
            var exclusions = contexts[targetId].LocalEpisodes
                .Where(item => owners.TryGetValue(item.ItemId, out var owner) &&
                    !string.Equals(owner, targetId, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.ItemId);
            context = SeasonPlanningContextBuilder.Filter(contexts[targetId], exclusions);
            return true;
        }
    }
}
