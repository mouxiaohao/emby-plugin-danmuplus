using System;
using System.Collections.Generic;
using System.Linq;

namespace Emby.Plugin.Danmu.Model
{
    /// <summary>
    /// Explicit target ownership for composite planning.  A physical Emby
    /// folder may display specials from another logical season; planning must
    /// use the Episode parent season, never its placement in that folder.
    /// </summary>
    public sealed class CompositeSeasonTargetContext
    {
        public int? TargetSeasonNumber { get; set; }
        public string TargetLogicalSeasonKey { get; set; } = string.Empty;
        public bool IsKnown => TargetSeasonNumber.HasValue;

        public static CompositeSeasonTargetContext ForSeasonNumber(int? seasonNumber) =>
            new CompositeSeasonTargetContext
            {
                TargetSeasonNumber = seasonNumber,
                TargetLogicalSeasonKey = seasonNumber.HasValue ? "season-" + seasonNumber.Value : string.Empty,
            };
    }

    /// <summary>Pure fail-closed ownership and stable ordering policy.</summary>
    public static class CompositeSeasonOwnership
    {
        public static bool IsOwnedBy(CompositeSeasonTargetContext context, CompositeSeasonLocalEpisode episode)
        {
            return context != null && context.IsKnown && episode != null &&
                   episode.ParentSeasonNumber.HasValue &&
                   episode.ParentSeasonNumber.Value == context.TargetSeasonNumber.Value;
        }

        /// <summary>
        /// Returns the complete display inventory. Episodes placed under the
        /// target folder but logically belonging to another known season remain
        /// visible as Supplemental; callers may never silently drop them.
        /// Missing parent-season metadata is Unknown and cannot be used as
        /// primary mapping evidence.
        /// </summary>
        public static bool TryGetDisplayEpisodes(
            CompositeSeasonTargetContext context,
            IEnumerable<CompositeSeasonLocalEpisode> episodes,
            out List<CompositeSeasonLocalEpisode> displayEpisodes)
        {
            displayEpisodes = new List<CompositeSeasonLocalEpisode>();
            if (context == null || !context.IsKnown)
            {
                return false;
            }

            displayEpisodes = (episodes ?? Enumerable.Empty<CompositeSeasonLocalEpisode>())
                .Where(episode => episode != null)
                .OrderBy(episode => episode.PlacementOrder ?? episode.SortOrder ?? int.MaxValue)
                .ThenBy(episode => episode.PlacementRelation)
                .ThenBy(episode => episode.OriginalEpisodeNumber ?? episode.EpisodeNumber ?? int.MaxValue)
                .ThenBy(episode => episode.ItemId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(episode => Mark(context, episode))
                .ToList();
            return true;
        }

        /// <summary>Returns only the fail-closed primary-mapping ownership set.</summary>
        public static bool TryGetOwnedEpisodes(
            CompositeSeasonTargetContext context,
            IEnumerable<CompositeSeasonLocalEpisode> episodes,
            out List<CompositeSeasonLocalEpisode> ownedEpisodes)
        {
            ownedEpisodes = new List<CompositeSeasonLocalEpisode>();
            if (!TryGetDisplayEpisodes(context, episodes, out var displayEpisodes)) return false;
            ownedEpisodes = displayEpisodes.Where(episode => episode.Ownership == CompositeSeasonOwnershipKind.Owning)
                .ToList();
            return true;
        }

        public static bool IsSameLogicalRun(CompositeSeasonLocalEpisode left, CompositeSeasonLocalEpisode right)
        {
            if (left == null || right == null) return false;
            // Legacy callers have no target context and therefore no parent
            // metadata at all. Preserve their stable single-run behaviour;
            // context-aware callers still treat unknown ownership as ineligible
            // primary evidence through TryGetOwnedEpisodes.
            if (!left.ParentSeasonNumber.HasValue && !right.ParentSeasonNumber.HasValue)
            {
                return true;
            }
            return left.ParentSeasonNumber.HasValue && right.ParentSeasonNumber.HasValue &&
                   left.ParentSeasonNumber.Value == right.ParentSeasonNumber.Value;
        }

        private static CompositeSeasonLocalEpisode Clone(CompositeSeasonLocalEpisode episode)
        {
            return new CompositeSeasonLocalEpisode
            {
                ItemId = episode.ItemId ?? string.Empty,
                EpisodeNumber = episode.EpisodeNumber,
                ParentSeasonNumber = episode.ParentSeasonNumber,
                OriginalEpisodeNumber = episode.OriginalEpisodeNumber,
                PlacementOrder = episode.PlacementOrder,
                PlacementRelation = episode.PlacementRelation,
                AirsBeforeSeasonNumber = episode.AirsBeforeSeasonNumber,
                AirsBeforeEpisodeNumber = episode.AirsBeforeEpisodeNumber,
                AirsAfterSeasonNumber = episode.AirsAfterSeasonNumber,
                LogicalSeasonLabel = episode.LogicalSeasonLabel ?? string.Empty,
                Ownership = episode.Ownership,
                SortOrder = episode.SortOrder,
            };
        }

        private static CompositeSeasonLocalEpisode Mark(
            CompositeSeasonTargetContext context, CompositeSeasonLocalEpisode episode)
        {
            var marked = Clone(episode);
            marked.Ownership = !episode.ParentSeasonNumber.HasValue
                ? CompositeSeasonOwnershipKind.Unknown
                : IsOwnedBy(context, episode)
                    ? CompositeSeasonOwnershipKind.Owning
                    : CompositeSeasonOwnershipKind.Supplemental;
            return marked;
        }
    }

    public sealed class CompositeSeasonTargetInventory
    {
        public string TargetId { get; set; } = string.Empty;
        public int? TargetSeasonNumber { get; set; }
        public List<CompositeSeasonLocalEpisode> Episodes { get; set; } =
            new List<CompositeSeasonLocalEpisode>();
    }

    public sealed class CompositeSeasonItemOwnership
    {
        public string ItemId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int? ParentSeasonNumber { get; set; }
        public string Resolution { get; set; } = string.Empty;
    }

    public sealed class CompositeSeasonOwnershipConflict
    {
        public string ItemId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public List<string> CandidateTargetIds { get; set; } = new List<string>();
    }

    public sealed class CompositeSeasonTargetOwnershipResult
    {
        public List<CompositeSeasonItemOwnership> Assignments { get; set; } =
            new List<CompositeSeasonItemOwnership>();
        public List<CompositeSeasonOwnershipConflict> Conflicts { get; set; } =
            new List<CompositeSeasonOwnershipConflict>();
        public bool IsValid => Conflicts.Count == 0;
    }

    /// <summary>
    /// Assigns every ItemId to at most one Series target. A target's explicit
    /// placement inventory wins; otherwise an exact ParentSeasonNumber target
    /// may claim it. Ambiguity is returned as structured conflict and never
    /// resolved by target enumeration order.
    /// </summary>
    public static class CompositeSeasonTargetOwnership
    {
        public static CompositeSeasonTargetOwnershipResult Resolve(
            IEnumerable<CompositeSeasonTargetInventory> targets)
        {
            var result = new CompositeSeasonTargetOwnershipResult();
            var inventories = (targets ?? Enumerable.Empty<CompositeSeasonTargetInventory>())
                .Where(target => target != null && !string.IsNullOrWhiteSpace(target.TargetId))
                .ToList();
            var entries = inventories.SelectMany((target, targetOrder) =>
                (target.Episodes ?? new List<CompositeSeasonLocalEpisode>())
                    .Where(episode => episode != null && !string.IsNullOrWhiteSpace(episode.ItemId))
                    .Select(episode => new { Target = target, Episode = episode, TargetOrder = targetOrder }))
                .GroupBy(entry => entry.Episode.ItemId, StringComparer.OrdinalIgnoreCase);

            foreach (var itemEntries in entries)
            {
                var candidates = itemEntries.ToList();
                var placed = candidates.Where(entry => entry.Episode.PlacementOrder.HasValue &&
                        entry.Episode.PlacementRelation != 0 &&
                        entry.Episode.ParentSeasonNumber.HasValue &&
                        entry.Target.TargetSeasonNumber.HasValue &&
                        entry.Target.TargetSeasonNumber.Value != entry.Episode.ParentSeasonNumber.Value).ToList();
                var placedTargets = placed.Select(entry => entry.Target.TargetId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var selected = placedTargets.Count == 1
                    ? placed.First(entry => string.Equals(entry.Target.TargetId, placedTargets[0],
                        StringComparison.OrdinalIgnoreCase))
                    : null;
                var resolution = "placement";
                var placementConflict = placedTargets.Count > 1;

                // Emby 4.9 may return a placed Episode from both its physical
                // parent Season inventory and the Season whose display contains
                // it, without exposing AirsBefore/AirsAfter on the plugin API.
                // In that raw shape the sole foreign display inventory is the
                // placement evidence; the physical parent inventory must not
                // reclaim the same ItemId.
                if (selected == null && !placementConflict)
                {
                    var foreignDisplays = candidates.Where(entry =>
                            entry.Episode.ParentSeasonNumber.HasValue &&
                            entry.Target.TargetSeasonNumber.HasValue &&
                            entry.Target.TargetSeasonNumber.Value != entry.Episode.ParentSeasonNumber.Value)
                        .ToList();
                    var foreignTargets = foreignDisplays.Select(entry => entry.Target.TargetId)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    selected = foreignTargets.Count == 1
                        ? foreignDisplays.First(entry => string.Equals(entry.Target.TargetId, foreignTargets[0],
                            StringComparison.OrdinalIgnoreCase))
                        : null;
                    placementConflict = foreignTargets.Count > 1;
                }

                if (selected == null && !placementConflict)
                {
                    var parentMatches = candidates.Where(entry => entry.Episode.ParentSeasonNumber.HasValue &&
                        entry.Target.TargetSeasonNumber == entry.Episode.ParentSeasonNumber).ToList();
                    var parentTargets = parentMatches.Select(entry => entry.Target.TargetId)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    selected = parentTargets.Count == 1
                        ? parentMatches.First(entry => string.Equals(entry.Target.TargetId, parentTargets[0],
                            StringComparison.OrdinalIgnoreCase))
                        : null;
                    resolution = "parent-season";
                }

                if (selected == null)
                {
                    result.Conflicts.Add(new CompositeSeasonOwnershipConflict
                    {
                        ItemId = itemEntries.Key,
                        Code = "item-ownership-ambiguous",
                        CandidateTargetIds = candidates.Select(entry => entry.Target.TargetId)
                            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList(),
                    });
                    continue;
                }

                result.Assignments.Add(new CompositeSeasonItemOwnership
                {
                    ItemId = itemEntries.Key,
                    TargetId = selected.Target.TargetId,
                    ParentSeasonNumber = selected.Episode.ParentSeasonNumber,
                    Resolution = resolution,
                });
            }
            return result;
        }

    }
}
