using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Pure deterministic planning for a local Emby season that can consist of
    /// several upstream seasons and specials. This deliberately has no Emby,
    /// persistence, scraper, or download dependency.
    /// </summary>
    public static class CompositeSeasonPlanner
    {
        /// <summary>
        /// Continues one primary source only across unmatched runs owned by the
        /// same logical season. Supplemental/unknown runs remain visible and
        /// do not consume a source offset.
        /// </summary>
        public static bool TryApplyRemainingOwningSourceEpisodes(
            CompositeSeasonPlan currentPlan, CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> availableSourceEpisodes, string origin,
            out CompositeSeasonPlan plan, out string error)
        {
            return TryApplyRemainingOwningSourceEpisodes(currentPlan, source, availableSourceEpisodes,
                origin, 0, string.Empty, string.Empty, out plan, out error);
        }

        public static bool TryApplyRemainingOwningSourceEpisodes(
            CompositeSeasonPlan currentPlan, CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> availableSourceEpisodes, string origin,
            double matchScore, string scoreOrigin, string selectionEvidenceToken,
            out CompositeSeasonPlan plan, out string error)
        {
            return TryApplyRemainingOwningSourceEpisodes(currentPlan, source, availableSourceEpisodes,
                origin, matchScore, scoreOrigin, selectionEvidenceToken, null, out plan, out error);
        }

        public static bool TryApplyRemainingOwningSourceEpisodes(
            CompositeSeasonPlan currentPlan, CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> availableSourceEpisodes, string origin,
            double matchScore, string scoreOrigin, string selectionEvidenceToken,
            SourceMetadata sourceMetadata,
            out CompositeSeasonPlan plan, out string error)
        {
            plan = currentPlan;
            error = string.Empty;
            var available = (availableSourceEpisodes ?? Enumerable.Empty<CompositeSeasonSourceEpisode>()).ToList();
            var owningKey = plan?.UnmatchedRuns
                .SelectMany(run => run.Episodes)
                .FirstOrDefault(episode => episode.Ownership == CompositeSeasonOwnershipKind.Owning)
                ?.ParentSeasonNumber;
            if (plan == null || !owningKey.HasValue)
            {
                error = "A valid plan with known owning Episode context is required.";
                return false;
            }

            // Non-owning rows are explicit zero-consumption boundaries. The
            // shared continuation resolver carries one forward-only source
            // frontier across the owning windows and resolves each window's
            // numeric/positional mode independently.
            var nonOwningBoundaryIds = new HashSet<string>(
                plan.OrderedEpisodes
                    .Where(episode => episode.Ownership != CompositeSeasonOwnershipKind.Owning ||
                                      episode.ParentSeasonNumber != owningKey)
                    .Select(episode => episode.ItemId),
                StringComparer.OrdinalIgnoreCase);
            return CompositeSeasonMatchService.TryContinueSourceAcrossSegmentWindows(
                plan, source, available, origin, matchScore, scoreOrigin,
                selectionEvidenceToken, sourceMetadata, nonOwningBoundaryIds,
                out plan, out _, out error);
        }

        public static bool TryApplyRemainingSourceEpisodes(
            CompositeSeasonPlan currentPlan, CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> availableSourceEpisodes, string origin,
            out CompositeSeasonPlan plan, out string error)
        {
            return TryApplyRemainingSourceEpisodes(currentPlan, source, availableSourceEpisodes,
                origin, 0, string.Empty, string.Empty, out plan, out error);
        }

        public static bool TryApplyRemainingSourceEpisodes(
            CompositeSeasonPlan currentPlan, CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> availableSourceEpisodes, string origin,
            double matchScore, string scoreOrigin, string selectionEvidenceToken,
            out CompositeSeasonPlan plan, out string error)
        {
            return TryApplyRemainingSourceEpisodes(currentPlan, source, availableSourceEpisodes,
                origin, matchScore, scoreOrigin, selectionEvidenceToken, null, out plan, out error);
        }

        public static bool TryApplyRemainingSourceEpisodes(
            CompositeSeasonPlan currentPlan, CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> availableSourceEpisodes, string origin,
            double matchScore, string scoreOrigin, string selectionEvidenceToken,
            SourceMetadata sourceMetadata,
            out CompositeSeasonPlan plan, out string error)
        {
            var available = (availableSourceEpisodes ?? Enumerable.Empty<CompositeSeasonSourceEpisode>()).ToList();
            return CompositeSeasonMatchService.TryContinueSourceAcrossSegmentWindows(
                currentPlan, source, available, origin, matchScore, scoreOrigin,
                selectionEvidenceToken, sourceMetadata, out plan, out _, out error);
        }
        public static bool TryCreatePlan(
            IEnumerable<CompositeSeasonLocalEpisode> localEpisodes,
            IEnumerable<CompositeSeasonEpisodeMapping> explicitMappings,
            out CompositeSeasonPlan plan,
            out string error)
        {
            return TryCreatePlan(localEpisodes, explicitMappings, null, null, false, out plan, out error);
        }

        /// <summary>
        /// Rebuilds an editable plan from two authoritative mapping layers.
        /// Direct evidence is removed for accepted dialog exclusions first;
        /// verified replacement mappings are then applied to the remaining
        /// draft.  This order lets a replacement occupy a removed direct
        /// range without allowing it to overwrite an unrelated mapping.
        /// </summary>
        public static bool TryCreatePlan(
            IEnumerable<CompositeSeasonLocalEpisode> localEpisodes,
            IEnumerable<CompositeSeasonEpisodeMapping> directMappings,
            IEnumerable<CompositeSeasonEpisodeMapping> replacementMappings,
            IEnumerable<string> excludedLocalEpisodeItemIds,
            bool durableCompositeMarker,
            out CompositeSeasonPlan plan,
            out string error)
        {
            plan = null;
            if (!TryOrderEpisodes(localEpisodes, out var ordered, out error) ||
                !TryNormalizeExcludedLocalEpisodeItemIds(ordered, excludedLocalEpisodeItemIds,
                    out var exclusions, out error))
            {
                return false;
            }

            var direct = (directMappings ?? Enumerable.Empty<CompositeSeasonEpisodeMapping>()).ToList();
            if (!ValidateMappings(ordered, direct, out error))
            {
                return false;
            }

            var replacements = (replacementMappings ?? Enumerable.Empty<CompositeSeasonEpisodeMapping>())
                .Select(CloneMapping)
                .ToList();
            if (!ValidateMappings(ordered, replacements, out error))
            {
                return false;
            }

            // Safety evidence is the original authoritative direct plan before
            // any dialog exclusions or replacements.  A same-source
            // replacement can legitimately collapse the executable draft to
            // one source, but it must never erase the fact that the durable
            // Episode evidence described a composite Season.
            var preExclusionPlan = BuildPlan(
                ordered,
                direct.Select(CloneMapping).ToList(),
                null,
                false);
            var excludedSet = new HashSet<string>(exclusions, StringComparer.OrdinalIgnoreCase);
            var finalMappings = direct
                .Where(mapping => !excludedSet.Contains(mapping.LocalEpisodeItemId))
                .Select(CloneMapping)
                .ToList();
            finalMappings.AddRange(replacements);
            if (!ValidateMappings(ordered, finalMappings, out error))
            {
                return false;
            }

            var finalPlan = BuildPlan(ordered, finalMappings, exclusions, false);
            finalPlan.CompositeSafetyRequired = durableCompositeMarker ||
                                                preExclusionPlan.IsComposite ||
                                                finalPlan.IsComposite;
            ApplyBindingSafety(finalPlan);
            plan = finalPlan;
            return true;
        }

        /// <summary>
        /// Validates browser exclusion intent against the exact authoritative
        /// local Season inventory.  Duplicates are harmless and collapse to a
        /// stable local-order list; a blank, unknown, or otherwise malformed
        /// value rejects the whole draft.
        /// </summary>
        public static bool TryNormalizeExcludedLocalEpisodeItemIds(
            IEnumerable<CompositeSeasonLocalEpisode> localEpisodes,
            IEnumerable<string> submittedExcludedItemIds,
            out List<string> effectiveExcludedItemIds,
            out string error)
        {
            effectiveExcludedItemIds = new List<string>();
            if (!TryOrderEpisodes(localEpisodes, out var ordered, out error))
            {
                return false;
            }

            var localIds = new HashSet<string>(ordered.Select(x => x.ItemId), StringComparer.OrdinalIgnoreCase);
            var submitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var itemId in submittedExcludedItemIds ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    error = "Every excluded Episode ItemId must be non-empty.";
                    return false;
                }
                if (!localIds.Contains(itemId))
                {
                    error = "An excluded Episode ItemId is outside the target season.";
                    return false;
                }
                submitted.Add(itemId);
            }

            // Echo in authoritative local order so repeated preview/download
            // calls have deterministic wire output independent of input order.
            effectiveExcludedItemIds = ordered.Where(x => submitted.Contains(x.ItemId))
                .Select(x => x.ItemId)
                .ToList();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Pure Restore state transition.  The browser/controller remains
        /// responsible for discarding a replacement selection for the same
        /// local run; this method only removes its stable local ids from the
        /// dialog's exclusion intent.
        /// </summary>
        public static bool TryRestoreExcludedLocalEpisodeItemIds(
            IEnumerable<CompositeSeasonLocalEpisode> localEpisodes,
            IEnumerable<string> effectiveExcludedItemIds,
            IEnumerable<string> localEpisodeItemIdsToRestore,
            out List<string> remainingExcludedItemIds,
            out string error)
        {
            remainingExcludedItemIds = new List<string>();
            if (!TryNormalizeExcludedLocalEpisodeItemIds(localEpisodes, effectiveExcludedItemIds,
                    out var current, out error) ||
                !TryNormalizeExcludedLocalEpisodeItemIds(localEpisodes, localEpisodeItemIdsToRestore,
                    out var restore, out error))
            {
                return false;
            }

            var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            if (restore.Any(itemId => !currentSet.Contains(itemId)))
            {
                error = "Only currently excluded Episode ItemIds may be restored.";
                return false;
            }

            var restoreSet = new HashSet<string>(restore, StringComparer.OrdinalIgnoreCase);
            remainingExcludedItemIds = current.Where(itemId => !restoreSet.Contains(itemId)).ToList();
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// The cleanup barrier exists only after a real file was persisted.
        /// A zero-result composite download must not create a marker or clear
        /// Season ProviderIds.
        /// </summary>
        public static bool ShouldApplyCompositeSafetyAfterPersist(
            CompositeSeasonPlan plan, bool filePersisted)
        {
            return filePersisted && plan != null && plan.CompositeSafetyRequired;
        }

        /// <summary>Returns maximal contiguous mapped runs with one stable source.</summary>
        public static List<CompositeSeasonMappedRun> GetEditableMappedRuns(CompositeSeasonPlan plan)
        {
            if (!ValidatePlan(plan, out _)) return new List<CompositeSeasonMappedRun>();
            var byLocalId = plan.Mappings.ToDictionary(x => x.LocalEpisodeItemId,
                StringComparer.OrdinalIgnoreCase);
            var runs = new List<CompositeSeasonMappedRun>();
            CompositeSeasonMappedRun current = null;
            foreach (var local in plan.OrderedEpisodes)
            {
                if (!byLocalId.TryGetValue(local.ItemId, out var mapping))
                {
                    current = null;
                    continue;
                }
                if (current == null || !current.Source.Equals(mapping.Source) ||
                    !CompositeSeasonOwnership.IsSameLogicalRun(
                        plan.OrderedEpisodes.FirstOrDefault(x => string.Equals(x.ItemId,
                            current.Mappings.Last().LocalEpisodeItemId, StringComparison.OrdinalIgnoreCase)), local) ||
                    current.Mappings.Count == 0 ||
                    Math.Abs(current.Mappings[0].MatchScore - mapping.MatchScore) > 0.0000001 ||
                    !string.Equals(current.Mappings[0].ScoreOrigin, mapping.ScoreOrigin,
                        StringComparison.OrdinalIgnoreCase))
                {
                    current = new CompositeSeasonMappedRun { Source = CloneSource(mapping.Source) };
                    runs.Add(current);
                }
                current.Mappings.Add(CloneMapping(mapping));
            }
            return runs;
        }

        /// <summary>
        /// Existing direct/explicit evidence is always retained. A candidate is
        /// only applied to its selected contiguous unmatched run, so no later
        /// mapping can silently replace or shift an earlier one.
        /// </summary>
        public static bool TryApplySegment(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSegmentRequest request,
            out CompositeSeasonPlan plan,
            out int appliedEpisodeCount,
            out string error)
        {
            var success = TryApplySegmentResolved(currentPlan, request, out plan, out var resolution, out error);
            appliedEpisodeCount = success ? resolution.Mappings.Count : 0;
            return success;
        }

        /// <summary>
        /// Applies one segment and exposes both the authoritative local rows
        /// considered and the exact mappings produced by the shared resolver.
        /// </summary>
        public static bool TryApplySegmentResolved(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSegmentRequest request,
            out CompositeSeasonPlan plan,
            out CompositeSeasonSegmentResolution resolution,
            out string error)
        {
            plan = null;
            resolution = null;
            error = string.Empty;
            if (currentPlan == null || !ValidatePlan(currentPlan, out error))
            {
                error = string.IsNullOrWhiteSpace(error) ? "A valid current plan is required." : error;
                return false;
            }

            if (request == null || request.Source == null || !request.Source.IsValid ||
                string.IsNullOrWhiteSpace(request.LocalStartEpisodeItemId))
            {
                error = "The local start and source identity are required.";
                return false;
            }

            if (request.RequestedEpisodeCount < 0)
            {
                error = "Requested episode count cannot be negative.";
                return false;
            }

            var run = currentPlan.UnmatchedRuns.FirstOrDefault(candidate => candidate.Episodes.Any(episode =>
                string.Equals(episode.ItemId, request.LocalStartEpisodeItemId, StringComparison.OrdinalIgnoreCase)));
            if (run == null)
            {
                error = "The selected local start episode is not currently unmatched.";
                return false;
            }

            if (!TryResolveSegment(run.Episodes, request, out resolution, out error)) return false;

            var mappings = currentPlan.Mappings.Select(CloneMapping).ToList();
            mappings.AddRange(resolution.Mappings.Select(CloneMapping));

            if (!ValidateMappings(currentPlan.OrderedEpisodes, mappings, out error))
            {
                return false;
            }

            plan = BuildPlan(currentPlan.OrderedEpisodes, mappings,
                currentPlan.EffectiveExcludedLocalEpisodeItemIds,
                currentPlan.CompositeSafetyRequired);
            plan.CompositeSafetyRequired = plan.CompositeSafetyRequired || plan.IsComposite;
            ApplyBindingSafety(plan);
            return true;
        }

        /// <summary>
        /// Resolves a segment without mutating a plan. Number reliability is
        /// decided once for the whole selected segment; fallback never mixes
        /// positional and numeric pairs.
        /// </summary>
        public static bool TryResolveSegment(
            IEnumerable<CompositeSeasonLocalEpisode> localEpisodes,
            CompositeSeasonSegmentRequest request,
            out CompositeSeasonSegmentResolution resolution,
            out string error)
        {
            resolution = null;
            error = string.Empty;
            if (request == null || request.Source == null || !request.Source.IsValid ||
                string.IsNullOrWhiteSpace(request.LocalStartEpisodeItemId))
            {
                error = "The local start and source identity are required.";
                return false;
            }
            if (request.RequestedEpisodeCount < 0)
            {
                error = "Requested episode count cannot be negative.";
                return false;
            }
            if (!Enum.IsDefined(typeof(CompositeSeasonAlignmentIntent), request.AlignmentIntent))
            {
                error = "The segment alignment intent is invalid.";
                return false;
            }

            var locals = (localEpisodes ?? Enumerable.Empty<CompositeSeasonLocalEpisode>()).
                Select(CloneLocalEpisode).ToList();
            if (locals.Count == 0 || locals.Any(local => string.IsNullOrWhiteSpace(local.ItemId)) ||
                locals.GroupBy(local => local.ItemId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                error = "The segment local episode list is invalid.";
                return false;
            }
            var localStart = FindIndex(locals, request.LocalStartEpisodeItemId, item => item.ItemId);
            if (localStart < 0)
            {
                error = "The selected local start episode does not exist in the authoritative local segment.";
                return false;
            }

            var sources = request.SourceEpisodes ?? new List<CompositeSeasonSourceEpisode>();
            if (!ValidateSourceEpisodes(sources, out error)) return false;

            var sourceNumbersReliable = HasReliableUniquePositiveNumbers(
                sources.Select(source => source.EpisodeNumber));
            var sourceStart = ResolveSourceStartIndex(sources, request.SourceStartEpisodeId,
                request.SourceStartEpisodeNumber, sourceNumbersReliable, out error);
            if (sourceStart < 0) return false;

            var localCount = locals.Count - localStart;
            if (request.RequestedEpisodeCount > 0) localCount = Math.Min(localCount, request.RequestedEpisodeCount);
            var considered = locals.Skip(localStart).Take(localCount).ToList();
            if (considered.Count == 0)
            {
                error = "The selected local range is empty.";
                return false;
            }

            var localNumbersReliable = HasReliableUniquePositiveNumbers(
                considered.Select(local => local.EpisodeNumber));
            var numberAware = localNumbersReliable && sourceNumbersReliable;
            if (request.RequiredAlignmentMode == CompositeSeasonAlignmentMode.NumberAware && !numberAware)
            {
                error = "A continued number-aware segment no longer has reliable unique positive numbering.";
                return false;
            }
            if (request.RequiredAlignmentMode == CompositeSeasonAlignmentMode.PositionalFallback)
            {
                numberAware = false;
            }
            var resolvedMappings = new List<CompositeSeasonEpisodeMapping>();
            if (numberAware)
            {
                var sourceByNumber = sources.ToDictionary(source => source.EpisodeNumber.Value);
                var localAnchor = request.LocalAnchorEpisodeNumber ?? considered[0].EpisodeNumber.Value;
                if (localAnchor <= 0)
                {
                    error = "The explicit local Episode anchor must be a positive authoritative coordinate.";
                    return false;
                }
                var sourceAnchor = sources[sourceStart].EpisodeNumber.Value;
                foreach (var local in considered)
                {
                    int targetNumber;
                    try
                    {
                        targetNumber = request.AlignmentIntent == CompositeSeasonAlignmentIntent.DefaultZeroOffset
                            ? local.EpisodeNumber.Value
                            : checked((int)((long)sourceAnchor + local.EpisodeNumber.Value - (long)localAnchor));
                    }
                    catch (OverflowException)
                    {
                        error = "The explicit Episode anchor offset overflows the supported Episode number range.";
                        return false;
                    }
                    if (targetNumber <= 0 || !sourceByNumber.TryGetValue(targetNumber, out var source)) continue;
                    resolvedMappings.Add(CreateMapping(local, source, request));
                }
            }
            else
            {
                var positionalSources = OrderSourcesForFallback(sources).ToList();
                var positionalStart = FindIndex(positionalSources, sources[sourceStart].EpisodeId,
                    item => item.EpisodeId);
                var count = Math.Min(considered.Count, positionalSources.Count - positionalStart);
                for (var index = 0; index < count; index++)
                {
                    resolvedMappings.Add(CreateMapping(considered[index], positionalSources[positionalStart + index], request));
                }
            }

            if (resolvedMappings.Count == 0)
            {
                error = "The selected local and source ranges do not overlap.";
                return false;
            }

            resolution = new CompositeSeasonSegmentResolution
            {
                Intent = request.AlignmentIntent,
                Mode = numberAware ? CompositeSeasonAlignmentMode.NumberAware :
                    CompositeSeasonAlignmentMode.PositionalFallback,
                Diagnostic = numberAware ? "number-aware" :
                    "positional-fallback: unreliable local or source numbering",
                ConsideredLocalEpisodes = considered.Select(CloneLocalEpisode).ToList(),
                Mappings = resolvedMappings,
            };
            return true;
        }

        public static bool ValidatePlan(CompositeSeasonPlan plan, out string error)
        {
            error = string.Empty;
            if (plan == null || !TryOrderEpisodes(plan.OrderedEpisodes, out var ordered, out error))
            {
                error = string.IsNullOrWhiteSpace(error) ? "A plan is required." : error;
                return false;
            }

            if (!ValidateMappings(ordered, plan.Mappings, out error))
            {
                return false;
            }

            if (!TryNormalizeExcludedLocalEpisodeItemIds(ordered, plan.EffectiveExcludedLocalEpisodeItemIds,
                    out var exclusions, out error))
            {
                return false;
            }

            var expected = BuildPlan(ordered, plan.Mappings, exclusions,
                plan.CompositeSafetyRequired || plan.IsComposite);
            if (plan.IsComposite != expected.IsComposite ||
                plan.CompositeSafetyRequired != expected.CompositeSafetyRequired ||
                plan.SeasonBindingUnsafe != expected.SeasonBindingUnsafe ||
                plan.CanPersistCompleteSeasonBinding != expected.CanPersistCompleteSeasonBinding ||
                !plan.EffectiveExcludedLocalEpisodeItemIds.SequenceEqual(exclusions, StringComparer.OrdinalIgnoreCase) ||
                !RunsEqual(plan.UnmatchedRuns, expected.UnmatchedRuns))
            {
                error = "The plan derived state does not match its exact mappings.";
                return false;
            }

            return true;
        }

        private static bool TryOrderEpisodes(
            IEnumerable<CompositeSeasonLocalEpisode> episodes,
            out List<CompositeSeasonLocalEpisode> ordered,
            out string error)
        {
            error = string.Empty;
            ordered = (episodes ?? Enumerable.Empty<CompositeSeasonLocalEpisode>())
                .Select((episode, index) => new { Episode = episode, Index = index })
                .OrderBy(entry => entry.Episode?.PlacementOrder ?? entry.Episode?.SortOrder ?? int.MaxValue)
                .ThenBy(entry => entry.Episode?.PlacementRelation ?? 0)
                .ThenBy(entry => IsPositive(entry.Episode?.EpisodeNumber) ? 0 : 1)
                .ThenBy(entry => IsPositive(entry.Episode?.EpisodeNumber) ? entry.Episode.EpisodeNumber.Value : int.MaxValue)
                .ThenBy(entry => entry.Episode?.ItemId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Index)
                .Select(entry => CloneLocalEpisode(entry.Episode))
                .ToList();

            if (ordered.Count == 0)
            {
                error = "At least one local episode is required.";
                return false;
            }

            if (ordered.Any(episode => string.IsNullOrWhiteSpace(episode.ItemId)))
            {
                error = "Every local episode must have an Emby ItemId.";
                return false;
            }

            if (ordered.GroupBy(episode => episode.ItemId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                error = "Local Episode ItemIds must be unique.";
                return false;
            }

            return true;
        }

        private static bool ValidateMappings(
            IEnumerable<CompositeSeasonLocalEpisode> orderedEpisodes,
            IEnumerable<CompositeSeasonEpisodeMapping> mappings,
            out string error)
        {
            error = string.Empty;
            var localIds = new HashSet<string>(orderedEpisodes.Select(episode => episode.ItemId), StringComparer.OrdinalIgnoreCase);
            var usedLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in mappings ?? Enumerable.Empty<CompositeSeasonEpisodeMapping>())
            {
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.LocalEpisodeItemId) ||
                    mapping.Source == null || !mapping.Source.IsValid ||
                    string.IsNullOrWhiteSpace(mapping.SourceEpisodeId) || string.IsNullOrWhiteSpace(mapping.CommentId))
                {
                    error = "Every mapping must contain a local ItemId, verified source identity, source episode ID, and CommentId.";
                    return false;
                }

                if (!localIds.Contains(mapping.LocalEpisodeItemId))
                {
                    error = "A mapping references an episode outside the target season.";
                    return false;
                }

                if (!usedLocal.Add(mapping.LocalEpisodeItemId))
                {
                    error = "A local episode may only be mapped once.";
                    return false;
                }

                if (!usedSource.Add(SourceEpisodeKey(mapping.Source, mapping.SourceEpisodeId)))
                {
                    error = "A verified source episode may only be mapped once within a season plan.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateSourceEpisodes(IList<CompositeSeasonSourceEpisode> sourceEpisodes, out string error)
        {
            error = string.Empty;
            if (sourceEpisodes.Count == 0)
            {
                error = "The verified source episode list is empty.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var episode in sourceEpisodes)
            {
                if (episode == null || string.IsNullOrWhiteSpace(episode.EpisodeId) ||
                    string.IsNullOrWhiteSpace(episode.CommentId))
                {
                    error = "Every selected source episode must have a verified ID and CommentId.";
                    return false;
                }
                if (!ids.Add(episode.EpisodeId))
                {
                    error = "The verified source episode list contains duplicate episode IDs.";
                    return false;
                }
            }
            return true;
        }

        private static bool HasReliableUniquePositiveNumbers(IEnumerable<int?> numbers)
        {
            var seen = new HashSet<int>();
            foreach (var number in numbers)
            {
                if (!number.HasValue || number.Value <= 0 || !seen.Add(number.Value)) return false;
            }
            return seen.Count > 0;
        }

        private static int ResolveSourceStartIndex(
            IList<CompositeSeasonSourceEpisode> sources,
            string sourceStartEpisodeId,
            int? sourceStartEpisodeNumber,
            bool sourceNumbersReliable,
            out string error)
        {
            error = string.Empty;
            if (!string.IsNullOrWhiteSpace(sourceStartEpisodeId))
            {
                var exact = FindIndex(sources, sourceStartEpisodeId, source => source.EpisodeId);
                if (exact >= 0) return exact;
                error = "The selected source start episode does not exist in the verified source episode list.";
                return -1;
            }

            if (!sourceStartEpisodeNumber.HasValue || sourceStartEpisodeNumber.Value <= 0 || !sourceNumbersReliable)
            {
                error = "A number-only source start requires reliable unique positive provider Episode numbering.";
                return -1;
            }

            var matches = sources.Select((source, index) => new { source, index })
                .Where(entry => entry.source.EpisodeNumber == sourceStartEpisodeNumber.Value)
                .Select(entry => entry.index)
                .ToList();
            if (matches.Count != 1)
            {
                error = "The number-only source start does not uniquely identify a verified source episode.";
                return -1;
            }
            return matches[0];
        }

        private static IEnumerable<CompositeSeasonSourceEpisode> OrderSourcesForFallback(
            IList<CompositeSeasonSourceEpisode> sources)
        {
            var ordinalsReliable = sources.All(source => source.SourceOrdinal > 0) &&
                                   sources.Select(source => source.SourceOrdinal).Distinct().Count() == sources.Count;
            return ordinalsReliable
                ? sources.OrderBy(source => source.SourceOrdinal).ToList()
                : sources.ToList();
        }

        private static CompositeSeasonEpisodeMapping CreateMapping(
            CompositeSeasonLocalEpisode local,
            CompositeSeasonSourceEpisode source,
            CompositeSeasonSegmentRequest request)
        {
            return new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = local.ItemId,
                Source = CloneSource(request.Source),
                SourceEpisodeId = source.EpisodeId,
                CommentId = source.CommentId,
                SourceEpisodeNumber = source.EpisodeNumber,
                Origin = request.Origin ?? string.Empty,
                MatchScore = request.MatchScore,
                ScoreOrigin = request.ScoreOrigin ?? string.Empty,
                SelectionEvidenceToken = request.SelectionEvidenceToken ?? string.Empty,
                SourceMetadata = request.SourceMetadata?.Clone(),
            };
        }

        private static CompositeSeasonPlan BuildPlan(
            IEnumerable<CompositeSeasonLocalEpisode> orderedEpisodes,
            IEnumerable<CompositeSeasonEpisodeMapping> mappings,
            IEnumerable<string> effectiveExcludedItemIds = null,
            bool compositeSafetyRequired = false)
        {
            var ordered = orderedEpisodes.Select(CloneLocalEpisode).ToList();
            var mappingList = mappings.Select(CloneMapping).ToList();
            var mapped = new HashSet<string>(mappingList.Select(mapping => mapping.LocalEpisodeItemId), StringComparer.OrdinalIgnoreCase);
            var runs = new List<CompositeSeasonUnmatchedRun>();
            CompositeSeasonUnmatchedRun run = null;
            CompositeSeasonLocalEpisode previous = null;
            foreach (var episode in ordered)
            {
                if (previous != null && !CompositeSeasonOwnership.IsSameLogicalRun(previous, episode))
                {
                    run = null;
                }
                if (mapped.Contains(episode.ItemId))
                {
                    run = null;
                    previous = episode;
                    continue;
                }
                if (run == null)
                {
                    run = new CompositeSeasonUnmatchedRun();
                    runs.Add(run);
                }
                run.Episodes.Add(CloneLocalEpisode(episode));
                previous = episode;
            }

            var plan = new CompositeSeasonPlan
            {
                OrderedEpisodes = ordered,
                Mappings = mappingList,
                UnmatchedRuns = runs,
                IsComposite = mappingList.Select(mapping => mapping.Source).Distinct().Skip(1).Any(),
                EffectiveExcludedLocalEpisodeItemIds = (effectiveExcludedItemIds ?? Enumerable.Empty<string>()).ToList(),
                CompositeSafetyRequired = compositeSafetyRequired,
            };
            ApplyBindingSafety(plan);
            return plan;
        }

        private static void ApplyBindingSafety(CompositeSeasonPlan plan)
        {
            if (plan == null) return;
            var sources = plan.Mappings.Select(mapping => mapping.Source).Distinct().ToList();
            var hasDirectEvidence = plan.Mappings.Any(mapping => string.Equals(
                mapping.Origin, "episode-provider-id", StringComparison.OrdinalIgnoreCase));
            plan.SeasonBindingUnsafe = plan.CompositeSafetyRequired || plan.IsComposite ||
                                       plan.UnmatchedRuns.Count > 0;
            plan.CanPersistCompleteSeasonBinding = !plan.SeasonBindingUnsafe &&
                                                   sources.Count == 1 &&
                                                   !hasDirectEvidence &&
                                                   plan.Mappings.Count == plan.OrderedEpisodes.Count;
        }

        private static bool RunsEqual(IList<CompositeSeasonUnmatchedRun> first, IList<CompositeSeasonUnmatchedRun> second)
        {
            first = first ?? new List<CompositeSeasonUnmatchedRun>();
            second = second ?? new List<CompositeSeasonUnmatchedRun>();
            return first.Count == second.Count && first.Zip(second, (left, right) => left != null && right != null &&
                left.Episodes.Select(episode => episode.ItemId).SequenceEqual(right.Episodes.Select(episode => episode.ItemId), StringComparer.OrdinalIgnoreCase)).All(value => value);
        }

        private static int FindIndex<T>(IList<T> items, string id, Func<T, string> selector)
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (string.Equals(selector(items[index]), id, StringComparison.OrdinalIgnoreCase)) return index;
            }
            return -1;
        }

        private static bool IsPositive(int? value) => value.HasValue && value.Value > 0;

        private static string SourceEpisodeKey(CompositeSeasonSourceIdentity source, string episodeId)
        {
            return (source.ProviderId ?? string.Empty) + "\u001f" + (source.MediaId ?? string.Empty) + "\u001f" + (episodeId ?? string.Empty);
        }

        private static CompositeSeasonLocalEpisode CloneLocalEpisode(CompositeSeasonLocalEpisode episode)
        {
            return new CompositeSeasonLocalEpisode
            {
                ItemId = episode?.ItemId ?? string.Empty,
                EpisodeNumber = episode?.EpisodeNumber,
                ParentSeasonNumber = episode?.ParentSeasonNumber,
                OriginalEpisodeNumber = episode?.OriginalEpisodeNumber,
                PlacementOrder = episode?.PlacementOrder,
                PlacementRelation = episode?.PlacementRelation ?? 0,
                AirsBeforeSeasonNumber = episode?.AirsBeforeSeasonNumber,
                AirsBeforeEpisodeNumber = episode?.AirsBeforeEpisodeNumber,
                AirsAfterSeasonNumber = episode?.AirsAfterSeasonNumber,
                LogicalSeasonLabel = episode?.LogicalSeasonLabel ?? string.Empty,
                Ownership = episode?.Ownership ?? CompositeSeasonOwnershipKind.Unknown,
                SortOrder = episode?.SortOrder,
            };
        }

        private static CompositeSeasonEpisodeMapping CloneMapping(CompositeSeasonEpisodeMapping mapping)
        {
            return new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = mapping.LocalEpisodeItemId, Source = CloneSource(mapping.Source),
                SourceEpisodeId = mapping.SourceEpisodeId, CommentId = mapping.CommentId,
                SourceEpisodeNumber = mapping.SourceEpisodeNumber, Origin = mapping.Origin,
                MatchScore = mapping.MatchScore, ScoreOrigin = mapping.ScoreOrigin,
                SelectionEvidenceToken = mapping.SelectionEvidenceToken,
                SourceMetadata = mapping.SourceMetadata?.Clone(),
            };
        }

        private static CompositeSeasonSourceIdentity CloneSource(CompositeSeasonSourceIdentity source)
        {
            return new CompositeSeasonSourceIdentity
            {
                ProviderId = source?.ProviderId ?? string.Empty,
                MediaId = source?.MediaId ?? string.Empty,
                MediaLookupId = source?.MediaLookupId ?? string.Empty,
            };
        }
    }
}
