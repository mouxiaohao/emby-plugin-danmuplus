using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Core
{
    public sealed class SeasonPlanningContext
    {
        public List<Episode> Episodes { get; set; } = new List<Episode>();
        public List<CompositeSeasonLocalEpisode> LocalEpisodes { get; set; } =
            new List<CompositeSeasonLocalEpisode>();
        public string StructureFingerprint { get; set; } = string.Empty;
    }

    public static class SeasonPlanningContextBuilder
    {
        public static SeasonPlanningContext Build(Season season, IEnumerable<Episode> episodes)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            var source = (episodes ?? Enumerable.Empty<Episode>()).Where(item => item != null).ToList();
            var local = CompositeSeasonMatchService.GetLocalEpisodes(
                source, CompositeSeasonTargetContext.ForSeasonNumber(season.IndexNumber));
            var byId = source.GroupBy(item => item.Id.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var ordered = local.Where(item => byId.ContainsKey(item.ItemId)).Select(item => byId[item.ItemId]).ToList();
            return Create(ordered, local);
        }

        public static SeasonPlanningContext Filter(
            SeasonPlanningContext context, IEnumerable<string> excludedItemIds)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var excluded = new HashSet<string>(excludedItemIds ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            var local = context.LocalEpisodes.Where(item => !excluded.Contains(item.ItemId)).ToList();
            var allowed = new HashSet<string>(local.Select(item => item.ItemId), StringComparer.OrdinalIgnoreCase);
            var episodes = context.Episodes.Where(item => allowed.Contains(item.Id.ToString())).ToList();
            return Create(episodes, local);
        }

        private static SeasonPlanningContext Create(
            List<Episode> episodes, List<CompositeSeasonLocalEpisode> local)
        {
            return new SeasonPlanningContext
            {
                Episodes = episodes,
                LocalEpisodes = local,
                StructureFingerprint = string.Join("|", local.Select(item =>
                    (item.ItemId ?? string.Empty) + ":" +
                    (item.ParentSeasonNumber.HasValue ? item.ParentSeasonNumber.Value.ToString() : "?") + ":" +
                    (item.OriginalEpisodeNumber.HasValue ? item.OriginalEpisodeNumber.Value.ToString() : "?") + ":" +
                    (item.PlacementOrder.HasValue ? item.PlacementOrder.Value.ToString() : "?") + ":" +
                    item.PlacementRelation + ":" +
                    (item.AirsBeforeSeasonNumber.HasValue ? item.AirsBeforeSeasonNumber.Value.ToString() : "?") + ":" +
                    (item.AirsBeforeEpisodeNumber.HasValue ? item.AirsBeforeEpisodeNumber.Value.ToString() : "?") + ":" +
                    (item.AirsAfterSeasonNumber.HasValue ? item.AirsAfterSeasonNumber.Value.ToString() : "?"))),
            };
        }
    }
}
