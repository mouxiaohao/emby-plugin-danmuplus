using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Authoritative r5 snapshot for one selected Season. InventoryLocalEpisodes
    /// contains every valid ItemId observed from targetSeason.GetEpisodes(); the
    /// public planning lists contain only exact target-season matches.
    /// </summary>
    public sealed class SeasonPlanningContext
    {
        public string SeriesId { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public int? TargetSeasonNumber { get; set; }
        public List<Episode> Episodes { get; set; } = new List<Episode>();
        public List<CompositeSeasonLocalEpisode> LocalEpisodes { get; set; } =
            new List<CompositeSeasonLocalEpisode>();
        public List<CompositeSeasonLocalEpisode> InventoryLocalEpisodes { get; set; } =
            new List<CompositeSeasonLocalEpisode>();
        public int DisplayedEpisodeCount { get; set; }
        public int ParentZeroOutOfScopeCount { get; set; }
        public int OtherSeasonOutOfScopeCount { get; set; }
        public int UnknownParentOutOfScopeCount { get; set; }
        public int InvalidIdentityCount { get; set; }
        public int DuplicateIdentityCount { get; set; }
        public List<string> Diagnostics { get; set; } = new List<string>();
        public string StructureFingerprint { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public static class SeasonPlanningContextBuilder
    {
        /// <summary>
        /// Compatibility wrapper. Production batch paths use TryBuild through
        /// SeasonTargetPlanningCoordinator so an invalid inventory cannot start
        /// search or execution.
        /// </summary>
        public static SeasonPlanningContext Build(Season season, IEnumerable<Episode> episodes)
        {
            TryBuild(season, episodes, out var context, out _);
            return context ?? new SeasonPlanningContext
            {
                SeasonId = season?.Id.ToString() ?? string.Empty,
                TargetSeasonNumber = season?.IndexNumber,
                Error = "target-season-inventory-unavailable",
            };
        }

        public static bool TryBuild(
            Season season,
            IEnumerable<Episode> episodes,
            out SeasonPlanningContext context,
            out string error)
        {
            context = new SeasonPlanningContext();
            error = string.Empty;
            if (season == null)
            {
                error = "target-season-missing";
                context.Error = error;
                context.Diagnostics.Add(error);
                return false;
            }

            context.SeasonId = season.Id.ToString();
            context.SeriesId = season.GetParent()?.Id.ToString() ?? string.Empty;
            context.TargetSeasonNumber = season.IndexNumber;
            if (!season.IndexNumber.HasValue)
            {
                error = "target-season-number-unknown";
                context.Error = error;
                context.Diagnostics.Add(error);
                return false;
            }

            var source = (episodes ?? Enumerable.Empty<Episode>())
                .Where(item => item != null)
                .ToList();
            var translated = CompositeSeasonMatchService.GetLocalEpisodes(source);
            if (!CompositeSeasonOwnership.TryGetTargetScope(
                    CompositeSeasonTargetContext.ForSeasonNumber(season.IndexNumber),
                    translated, out var scope, out error))
            {
                context.Diagnostics.AddRange(scope?.Diagnostics ?? new List<string>());
                context.Error = error;
                context.StructureFingerprint = CreateStructureFingerprint(context, scope);
                return false;
            }

            context.InventoryLocalEpisodes = scope.ObservedEpisodes;
            context.LocalEpisodes = scope.EligibleEpisodes;
            context.DisplayedEpisodeCount = scope.ObservedEpisodes.Count +
                                            scope.InvalidIdentityCount +
                                            scope.DuplicateIdentityCount;
            context.ParentZeroOutOfScopeCount = scope.ParentZeroCount;
            context.OtherSeasonOutOfScopeCount = scope.OtherSeasonCount;
            context.UnknownParentOutOfScopeCount = scope.UnknownParentCount;
            context.InvalidIdentityCount = scope.InvalidIdentityCount;
            context.DuplicateIdentityCount = scope.DuplicateIdentityCount;
            context.Diagnostics.AddRange(scope.Diagnostics);

            var byId = source
                .Where(item => item.Id != Guid.Empty)
                .GroupBy(item => item.Id.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            context.Episodes = context.LocalEpisodes
                .Where(item => byId.ContainsKey(item.ItemId))
                .Select(item => byId[item.ItemId])
                .ToList();
            context.StructureFingerprint = CreateStructureFingerprint(context, scope);

            if (context.LocalEpisodes.Count == 0 || context.Episodes.Count == 0)
            {
                error = "no-eligible-episodes";
                context.Error = error;
                context.Diagnostics.Add(error);
                return false;
            }
            if (context.Episodes.Count != context.LocalEpisodes.Count)
            {
                error = "target-season-inventory-incomplete";
                context.Error = error;
                context.Diagnostics.Add(error);
                return false;
            }

            context.IsValid = true;
            return true;
        }

        public static SeasonPlanningContext Filter(
            SeasonPlanningContext context, IEnumerable<string> excludedItemIds)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var excluded = new HashSet<string>(excludedItemIds ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            var local = context.LocalEpisodes.Where(item => !excluded.Contains(item.ItemId)).ToList();
            var allowed = new HashSet<string>(local.Select(item => item.ItemId), StringComparer.OrdinalIgnoreCase);
            return new SeasonPlanningContext
            {
                SeriesId = context.SeriesId,
                SeasonId = context.SeasonId,
                TargetSeasonNumber = context.TargetSeasonNumber,
                Episodes = context.Episodes.Where(item => allowed.Contains(item.Id.ToString())).ToList(),
                LocalEpisodes = local,
                InventoryLocalEpisodes = context.InventoryLocalEpisodes.ToList(),
                DisplayedEpisodeCount = context.DisplayedEpisodeCount,
                ParentZeroOutOfScopeCount = context.ParentZeroOutOfScopeCount,
                OtherSeasonOutOfScopeCount = context.OtherSeasonOutOfScopeCount,
                UnknownParentOutOfScopeCount = context.UnknownParentOutOfScopeCount,
                InvalidIdentityCount = context.InvalidIdentityCount,
                DuplicateIdentityCount = context.DuplicateIdentityCount,
                Diagnostics = context.Diagnostics.ToList(),
                StructureFingerprint = context.StructureFingerprint,
                IsValid = context.IsValid,
                Error = context.Error,
            };
        }

        public static string CreateStructureFingerprint(
            SeasonPlanningContext context, CompositeSeasonEpisodeScope scope)
        {
            var targetNumber = context.TargetSeasonNumber.HasValue
                ? context.TargetSeasonNumber.Value.ToString()
                : "?";
            var observations = (scope?.ObservedEpisodes ?? new List<CompositeSeasonLocalEpisode>())
                .Select(item =>
                    (item.ItemId ?? string.Empty) + ":" +
                    (item.ParentSeasonNumber.HasValue ? item.ParentSeasonNumber.Value.ToString() : "?") + ":" +
                    (item.Ownership == CompositeSeasonOwnershipKind.Owning ? "eligible" : "excluded") + ":" +
                    (item.PlacementOrder.HasValue ? item.PlacementOrder.Value.ToString() : "?") + ":" +
                    item.PlacementRelation + ":" +
                    (item.OriginalEpisodeNumber.HasValue ? item.OriginalEpisodeNumber.Value.ToString() : "?"));
            return string.Join("|", new[]
            {
                "series=" + (context.SeriesId ?? string.Empty),
                "season=" + (context.SeasonId ?? string.Empty),
                "target=" + targetNumber,
                "invalid=" + (scope?.InvalidIdentityCount ?? 0),
                "duplicates=" + (scope?.DuplicateIdentityCount ?? 0),
            }.Concat(observations));
        }

        public static string CreatePlanFingerprint(
            SeasonPlanningContext context,
            IEnumerable<DanmuCompositeSeasonSelection> selections,
            CompositeSeasonPlan plan)
        {
            if (context == null || plan == null) return string.Empty;
            // The canonical material is intentionally kept server-side. Length-prefixed
            // fields make the representation unambiguous even when scraper values contain
            // separators; only its fixed-size SHA-256 digest crosses the V22 wire.
            var canonical = new StringBuilder();
            AppendFingerprintField(canonical, "protocol");
            AppendFingerprintField(canonical, DanmuMappingProtocol.CurrentVersion.ToString(CultureInfo.InvariantCulture));
            AppendFingerprintField(canonical, context.StructureFingerprint);
            foreach (var entry in (selections ?? Enumerable.Empty<DanmuCompositeSeasonSelection>())
                         .Select((selection, ordinal) => new { selection, ordinal }))
            {
                AppendFingerprintField(canonical, "selection");
                AppendFingerprintField(canonical, entry.ordinal.ToString(CultureInfo.InvariantCulture));
                var selection = entry.selection;
                AppendFingerprintField(canonical, selection == null ? "null" : "value");
                if (selection == null) continue;
                AppendFingerprintField(canonical, selection.MappingProtocolVersion.ToString(CultureInfo.InvariantCulture));
                AppendFingerprintField(canonical, selection.AlignmentIntent);
                AppendFingerprintField(canonical, selection.ServerResolvedAlignmentMode.HasValue
                    ? selection.ServerResolvedAlignmentMode.Value.ToString() : "?");
                AppendFingerprintField(canonical, selection.Site);
                AppendFingerprintField(canonical, selection.CandidateId);
                AppendFingerprintField(canonical, selection.LocalStartEpisodeItemId);
                AppendFingerprintField(canonical, selection.RequestedEpisodeCount.ToString(CultureInfo.InvariantCulture));
                AppendFingerprintField(canonical, selection.SourceStartEpisodeId);
                AppendFingerprintField(canonical, selection.SourceStartEpisodeNumber.HasValue
                    ? selection.SourceStartEpisodeNumber.Value.ToString(CultureInfo.InvariantCulture) : "?");
                AppendFingerprintField(canonical, selection.MatchOrigin);
                AppendFingerprintField(canonical, selection.SelectionEvidenceToken);
                foreach (var consideredEntry in (selection.ServerConsideredLocalEpisodeItemIds ??
                                 new List<string>()).Select((itemId, consideredOrdinal) =>
                                 new { itemId, consideredOrdinal }))
                {
                    AppendFingerprintField(canonical, "considered-local");
                    AppendFingerprintField(canonical,
                        consideredEntry.consideredOrdinal.ToString(CultureInfo.InvariantCulture));
                    AppendFingerprintField(canonical, consideredEntry.itemId);
                }
                foreach (var sourceEntry in (selection.ServerSourceEpisodes ??
                                 new List<CompositeSeasonSourceEpisode>())
                             .Select((episode, sourceOrdinal) => new { episode, sourceOrdinal }))
                {
                    AppendFingerprintField(canonical, "source-episode");
                    AppendFingerprintField(canonical, sourceEntry.sourceOrdinal.ToString(CultureInfo.InvariantCulture));
                    AppendFingerprintField(canonical, sourceEntry.episode?.EpisodeId);
                    AppendFingerprintField(canonical, sourceEntry.episode?.CommentId);
                    AppendFingerprintField(canonical, sourceEntry.episode?.EpisodeNumber.HasValue == true
                        ? sourceEntry.episode.EpisodeNumber.Value.ToString(CultureInfo.InvariantCulture) : "?");
                    AppendFingerprintField(canonical, sourceEntry.episode == null
                        ? "?" : sourceEntry.episode.SourceOrdinal.ToString(CultureInfo.InvariantCulture));
                }
            }
            foreach (var entry in (plan.Mappings ?? new List<CompositeSeasonEpisodeMapping>())
                         .Select((mapping, ordinal) => new { mapping, ordinal }))
            {
                var mapping = entry.mapping;
                AppendFingerprintField(canonical, "mapping");
                AppendFingerprintField(canonical, entry.ordinal.ToString(CultureInfo.InvariantCulture));
                AppendFingerprintField(canonical, mapping?.LocalEpisodeItemId);
                AppendFingerprintField(canonical, mapping?.Source?.ProviderId);
                AppendFingerprintField(canonical, mapping?.Source?.MediaId);
                AppendFingerprintField(canonical, mapping?.Source?.MediaLookupId);
                AppendFingerprintField(canonical, mapping?.SourceEpisodeId);
                AppendFingerprintField(canonical, mapping?.CommentId);
                AppendFingerprintField(canonical, mapping?.SourceEpisodeNumber.HasValue == true
                    ? mapping.SourceEpisodeNumber.Value.ToString(CultureInfo.InvariantCulture) : "?");
                AppendFingerprintField(canonical, mapping?.Origin);
                AppendFingerprintField(canonical, mapping?.SelectionEvidenceToken);
            }

            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void AppendFingerprintField(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }
    }
}
