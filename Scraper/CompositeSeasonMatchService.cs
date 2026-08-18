using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Thin Emby-facing adapter around <see cref="CompositeSeasonPlanner"/>.
    /// It only translates stable library/source objects; all planning remains
    /// pure and deterministic in the planner.
    /// </summary>
    public static class CompositeSeasonMatchService
    {
        public const int MaximumSourceEpisodeNameLength = 256;
        private static readonly object PlacementPropertyLock = new object();
        private static readonly Dictionary<string, PropertyInfo> PlacementProperties =
            new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);

        private sealed class SegmentWindowExactMapping
        {
            public CompositeSeasonLocalEpisode Local { get; set; }
            public CompositeSeasonEpisodeMapping Mapping { get; set; }
        }
        public static bool AreSourceEpisodesExhausted(CompositeSeasonPlan plan,
            CompositeSeasonSourceIdentity source, IEnumerable<string> sourceEpisodeIds)
        {
            var expected = new HashSet<string>(sourceEpisodeIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (source == null || expected.Count == 0) return false;
            var consumed = new HashSet<string>((plan?.Mappings ?? new List<CompositeSeasonEpisodeMapping>())
                .Where(x => x.Source != null && x.Source.Equals(source))
                .Select(x => x.SourceEpisodeId), StringComparer.OrdinalIgnoreCase);
            return expected.All(consumed.Contains);
        }

        /// <summary>
        /// Rebuilds the derived plan state after folding direct Episode evidence
        /// into a verified Season/special source, then consumes that source in
        /// source order over every remaining local run.  The direct mapping's
        /// lookup token is deliberately retained: it is needed later for the
        /// exact direct-Episode resolver, while MediaId becomes the canonical
        /// identity used for composite detection and source consumption.
        /// </summary>
        public static bool TryNormalizeAndContinueSource(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> sourceEpisodes,
            string origin,
            out CompositeSeasonPlan plan,
            out bool exhausted,
            out string error)
        {
            return TryNormalizeAndContinueSource(currentPlan, source, sourceEpisodes, origin,
                0, string.Empty, string.Empty, out plan, out exhausted, out error);
        }

        public static bool TryNormalizeAndContinueSource(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> sourceEpisodes,
            string origin,
            double matchScore,
            string scoreOrigin,
            string selectionEvidenceToken,
            out CompositeSeasonPlan plan,
            out bool exhausted,
            out string error)
        {
            return TryNormalizeAndContinueSource(currentPlan, source, sourceEpisodes, origin,
                matchScore, scoreOrigin, selectionEvidenceToken, null, out plan, out exhausted, out error);
        }

        public static bool TryNormalizeAndContinueSource(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSourceIdentity source,
            IEnumerable<CompositeSeasonSourceEpisode> sourceEpisodes,
            string origin,
            double matchScore,
            string scoreOrigin,
            string selectionEvidenceToken,
            SourceMetadata sourceMetadata,
            out CompositeSeasonPlan plan,
            out bool exhausted,
            out string error)
        {
            plan = currentPlan;
            exhausted = false;
            error = string.Empty;
            var verifiedEpisodes = (sourceEpisodes ?? Enumerable.Empty<CompositeSeasonSourceEpisode>()).ToList();
            var verifiedIds = new HashSet<string>(verifiedEpisodes.Select(x => x?.EpisodeId)
                .Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            if (currentPlan == null || source == null || !source.IsValid || verifiedIds.Count == 0)
            {
                error = "A valid plan, source, and verified source episodes are required.";
                return false;
            }
            if (verifiedEpisodes.Any(episode => episode == null ||
                    string.IsNullOrWhiteSpace(episode.EpisodeId) ||
                    string.IsNullOrWhiteSpace(episode.CommentId)) ||
                verifiedEpisodes.Select(episode => episode.EpisodeId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != verifiedEpisodes.Count)
            {
                error = "Verified source episodes require unique non-empty IDs and non-empty CommentIds.";
                return false;
            }

            var normalizedMappings = currentPlan.Mappings.Select(mapping => new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = mapping.LocalEpisodeItemId,
                Source = new CompositeSeasonSourceIdentity
                {
                    ProviderId = mapping.Source?.ProviderId ?? string.Empty,
                    MediaId = mapping.Source?.MediaId ?? string.Empty,
                    MediaLookupId = mapping.Source?.MediaLookupId ?? string.Empty,
                },
                SourceEpisodeId = mapping.SourceEpisodeId,
                CommentId = mapping.CommentId,
                SourceEpisodeNumber = mapping.SourceEpisodeNumber,
                Origin = mapping.Origin,
                MatchScore = mapping.MatchScore,
                ScoreOrigin = mapping.ScoreOrigin,
                SelectionEvidenceToken = mapping.SelectionEvidenceToken,
                SourceMetadata = mapping.SourceMetadata?.Clone(),
            }).ToList();
            foreach (var mapping in normalizedMappings.Where(mapping =>
                         string.Equals(mapping.Origin, "episode-provider-id", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(mapping.Source.ProviderId, source.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                         verifiedIds.Contains(mapping.SourceEpisodeId)))
            {
                mapping.Source.MediaId = source.MediaId;
            }

            if (!CompositeSeasonPlanner.TryCreatePlan(currentPlan.OrderedEpisodes, normalizedMappings, out plan, out error))
            {
                return false;
            }
            plan.EffectiveExcludedLocalEpisodeItemIds = currentPlan.EffectiveExcludedLocalEpisodeItemIds
                .ToList();
            plan.CompositeSafetyRequired = currentPlan.CompositeSafetyRequired || plan.IsComposite;

            if (!TryContinueSourceAcrossSegmentWindows(plan, source, verifiedEpisodes, origin,
                    matchScore, scoreOrigin, selectionEvidenceToken, sourceMetadata,
                    out plan, out var sourceFrontier, out error)) return false;

            exhausted = sourceFrontier >= verifiedEpisodes.Count;
            return true;
        }

        /// <summary>
        /// Continues one verified source through local segment windows. A row
        /// mapped to another source closes the current window but consumes no
        /// source coordinate. Each new window starts at the forward-only
        /// source frontier and may therefore have a different affine offset.
        /// </summary>
        internal static bool TryContinueSourceAcrossSegmentWindows(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSourceIdentity source,
            IList<CompositeSeasonSourceEpisode> verifiedEpisodes,
            string origin,
            double matchScore,
            string scoreOrigin,
            string selectionEvidenceToken,
            SourceMetadata sourceMetadata,
            out CompositeSeasonPlan plan,
            out int sourceFrontier,
            out string error)
        {
            return TryContinueSourceAcrossSegmentWindows(
                currentPlan, source, verifiedEpisodes, origin, matchScore, scoreOrigin,
                selectionEvidenceToken, sourceMetadata, null,
                out plan, out sourceFrontier, out error);
        }

        internal static bool TryContinueSourceAcrossSegmentWindows(
            CompositeSeasonPlan currentPlan,
            CompositeSeasonSourceIdentity source,
            IList<CompositeSeasonSourceEpisode> verifiedEpisodes,
            string origin,
            double matchScore,
            string scoreOrigin,
            string selectionEvidenceToken,
            SourceMetadata sourceMetadata,
            ISet<string> zeroConsumptionBoundaryLocalItemIds,
            out CompositeSeasonPlan plan,
            out int sourceFrontier,
            out string error)
        {
            var workingPlan = currentPlan;
            var failure = string.Empty;
            plan = currentPlan;
            sourceFrontier = 0;
            error = string.Empty;
            var frontierValue = 0;
            if (currentPlan == null || !CompositeSeasonPlanner.ValidatePlan(currentPlan, out error) ||
                source == null || !source.IsValid || verifiedEpisodes == null || verifiedEpisodes.Count == 0 ||
                verifiedEpisodes.Any(episode => episode == null ||
                    string.IsNullOrWhiteSpace(episode.EpisodeId) || string.IsNullOrWhiteSpace(episode.CommentId)) ||
                verifiedEpisodes.Select(episode => episode.EpisodeId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != verifiedEpisodes.Count)
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "A valid plan, source, and structurally valid verified source scope are required."
                    : error;
                return false;
            }
            var ordinalReliable = verifiedEpisodes.All(episode => episode.SourceOrdinal > 0) &&
                                  verifiedEpisodes.Select(episode => episode.SourceOrdinal)
                                      .Distinct().Count() == verifiedEpisodes.Count;
            var sources = ordinalReliable
                ? verifiedEpisodes.OrderBy(episode => episode.SourceOrdinal).ToList()
                : verifiedEpisodes.ToList();
            var sourceIndexById = sources.Select((episode, index) => new { episode.EpisodeId, index })
                .ToDictionary(entry => entry.EpisodeId, entry => entry.index, StringComparer.OrdinalIgnoreCase);
            var sourceNumbersReliable = sources.All(episode => episode.EpisodeNumber.HasValue &&
                                                               episode.EpisodeNumber.Value > 0) &&
                                        sources.Select(episode => episode.EpisodeNumber.Value)
                                            .Distinct().Count() == sources.Count;
            var sourceByNumber = sourceNumbersReliable
                ? sources.ToDictionary(episode => episode.EpisodeNumber.Value)
                : new Dictionary<int, CompositeSeasonSourceEpisode>();
            var initialMappings = workingPlan.Mappings.ToDictionary(mapping => mapping.LocalEpisodeItemId,
                StringComparer.OrdinalIgnoreCase);
            var ordered = workingPlan.OrderedEpisodes;

            var cursor = 0;
            while (cursor < ordered.Count)
            {
                if (IsForeignBoundary(ordered[cursor].ItemId))
                {
                    cursor++;
                    continue;
                }
                var windowStart = cursor;
                while (cursor < ordered.Count && !IsForeignBoundary(ordered[cursor].ItemId)) cursor++;
                var window = ordered.Skip(windowStart).Take(cursor - windowStart).ToList();
                if (window.Count == 0) continue;

                var sameSourceMappings = window
                    .Where(local => initialMappings.TryGetValue(local.ItemId, out var mapping) &&
                                    mapping.Source != null && mapping.Source.Equals(source))
                    .Select(local => new SegmentWindowExactMapping
                    {
                        Local = local,
                        Mapping = initialMappings[local.ItemId],
                    })
                    .ToList();
                var localNumbersReliable = window.All(local => local.EpisodeNumber.HasValue &&
                                                               local.EpisodeNumber.Value > 0) &&
                                           window.Select(local => local.EpisodeNumber.Value)
                                               .Distinct().Count() == window.Count;
                if (sourceNumbersReliable && localNumbersReliable)
                {
                    if (!TryApplyNumericWindow(window, sameSourceMappings, ref frontierValue))
                    {
                        plan = workingPlan;
                        sourceFrontier = frontierValue;
                        error = failure;
                        return false;
                    }
                }
                else
                {
                    if (!TryApplyPositionalWindow(window, sameSourceMappings, ref frontierValue))
                    {
                        plan = workingPlan;
                        sourceFrontier = frontierValue;
                        error = failure;
                        return false;
                    }
                }
            }
            plan = workingPlan;
            sourceFrontier = frontierValue;
            error = failure;
            return true;

            bool IsForeignBoundary(string localItemId)
            {
                return (zeroConsumptionBoundaryLocalItemIds?.Contains(localItemId) ?? false) ||
                       initialMappings.TryGetValue(localItemId, out var mapping) &&
                       (mapping.Source == null || !mapping.Source.Equals(source));
            }

            bool TryApplyNumericWindow(
                IList<CompositeSeasonLocalEpisode> window,
                IList<SegmentWindowExactMapping> sameMappings,
                ref int frontier)
            {
                if (frontier >= sources.Count && sameMappings.Count == 0) return true;
                CompositeSeasonLocalEpisode anchorLocal;
                CompositeSeasonSourceEpisode anchorSource;
                if (sameMappings.Count > 0)
                {
                    anchorLocal = sameMappings[0].Local;
                    if (!sourceIndexById.TryGetValue(sameMappings[0].Mapping.SourceEpisodeId,
                            out var anchorIndex) || anchorIndex < frontier)
                    {
                        failure = "A same-source exact mapping falls behind the current source frontier.";
                        return false;
                    }
                    anchorSource = sources[anchorIndex];
                }
                else
                {
                    anchorLocal = window[0];
                    anchorSource = sources[frontier];
                }

                var offset = (long)anchorSource.EpisodeNumber.Value - anchorLocal.EpisodeNumber.Value;
                foreach (var exact in sameMappings)
                {
                    if (!sourceIndexById.TryGetValue(exact.Mapping.SourceEpisodeId, out var exactIndex))
                    {
                        failure = "A same-source exact mapping is absent from the verified source scope.";
                        return false;
                    }
                    if (exactIndex < frontier)
                    {
                        failure = "A same-source exact mapping falls behind the current source frontier.";
                        return false;
                    }
                    var expected = (long)exact.Local.EpisodeNumber.Value + offset;
                    if (sources[exactIndex].EpisodeNumber.Value != expected)
                    {
                        failure = "Same-source exact mappings conflict inside one segment window.";
                        return false;
                    }
                }

                long maximumTarget = long.MinValue;
                foreach (var local in window)
                {
                    long target;
                    try { target = checked((long)local.EpisodeNumber.Value + offset); }
                    catch (OverflowException)
                    {
                        failure = "The segment-window numeric frontier overflowed.";
                        return false;
                    }
                    maximumTarget = Math.Max(maximumTarget, target);
                    if (initialMappings.ContainsKey(local.ItemId) || target <= 0 || target > int.MaxValue ||
                        !sourceByNumber.ContainsKey((int)target)) continue;
                    var request = CreateWindowRequest(local, anchorLocal.EpisodeNumber.Value,
                        anchorSource.EpisodeId, CompositeSeasonAlignmentMode.NumberAware);
                    if (!CompositeSeasonPlanner.TryApplySegmentResolved(
                            workingPlan, request, out workingPlan, out _, out failure)) return false;
                }

                var next = Math.Max(frontier, sourceIndexById[anchorSource.EpisodeId] + 1);
                for (var index = frontier; index < sources.Count; index++)
                {
                    if (sources[index].EpisodeNumber.Value <= maximumTarget) next = Math.Max(next, index + 1);
                }
                frontier = Math.Max(frontier, next);
                return true;
            }

            bool TryApplyPositionalWindow(
                IList<CompositeSeasonLocalEpisode> window,
                IList<SegmentWindowExactMapping> sameMappings,
                ref int frontier)
            {
                var baseIndex = frontier;
                if (sameMappings.Count > 0)
                {
                    var firstRow = window.IndexOf(sameMappings[0].Local);
                    if (!sourceIndexById.TryGetValue(sameMappings[0].Mapping.SourceEpisodeId,
                            out var firstSourceIndex))
                    {
                        failure = "A same-source exact mapping is absent from the verified source scope.";
                        return false;
                    }
                    baseIndex = firstSourceIndex - firstRow;
                    if (baseIndex < frontier)
                    {
                        failure = "A positional same-source mapping falls behind the current source frontier.";
                        return false;
                    }
                }
                foreach (var exact in sameMappings)
                {
                    var row = window.IndexOf(exact.Local);
                    var expectedIndex = baseIndex + row;
                    if (expectedIndex < 0 || expectedIndex >= sources.Count ||
                        !string.Equals(sources[expectedIndex].EpisodeId,
                            exact.Mapping.SourceEpisodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        failure = "Same-source positional mappings conflict inside one segment window.";
                        return false;
                    }
                }
                for (var row = 0; row < window.Count; row++)
                {
                    var local = window[row];
                    var sourceIndex = baseIndex + row;
                    if (initialMappings.ContainsKey(local.ItemId) || sourceIndex < 0 ||
                        sourceIndex >= sources.Count) continue;
                    var request = CreateWindowRequest(local, null, sources[sourceIndex].EpisodeId,
                        CompositeSeasonAlignmentMode.PositionalFallback);
                    if (!CompositeSeasonPlanner.TryApplySegmentResolved(
                            workingPlan, request, out workingPlan, out _, out failure)) return false;
                }
                frontier = Math.Max(frontier, Math.Min(sources.Count, baseIndex + window.Count));
                return true;
            }

            CompositeSeasonSegmentRequest CreateWindowRequest(
                CompositeSeasonLocalEpisode local,
                int? localAnchorNumber,
                string sourceAnchorEpisodeId,
                CompositeSeasonAlignmentMode mode)
            {
                return new CompositeSeasonSegmentRequest
                {
                    LocalStartEpisodeItemId = local.ItemId,
                    RequestedEpisodeCount = 1,
                    Source = source,
                    SourceEpisodes = sources,
                    SourceStartEpisodeId = sourceAnchorEpisodeId,
                    LocalAnchorEpisodeNumber = localAnchorNumber,
                    AlignmentIntent = CompositeSeasonAlignmentIntent.ExplicitAnchor,
                    RequiredAlignmentMode = mode,
                    Origin = origin ?? string.Empty,
                    MatchScore = matchScore,
                    ScoreOrigin = scoreOrigin ?? string.Empty,
                    SelectionEvidenceToken = selectionEvidenceToken ?? string.Empty,
                    SourceMetadata = sourceMetadata?.Clone(),
                };
            }
        }

        public static DanmuMatchCandidate SelectSupplementalCandidate(
            IEnumerable<DanmuMatchCandidate> candidates, IEnumerable<CompositeSeasonSourceIdentity> exhaustedSources)
        {
            var eligible = (candidates ?? Enumerable.Empty<DanmuMatchCandidate>()).Where(x => x != null);
            var exhaustedKeys = new HashSet<string>((exhaustedSources ?? Enumerable.Empty<CompositeSeasonSourceIdentity>())
                .Where(source => source != null && !string.IsNullOrWhiteSpace(source.ProviderId) &&
                                 !string.IsNullOrWhiteSpace(source.MediaLookupId))
                .Select(source => source.ProviderId + "\u001f" + source.MediaLookupId), StringComparer.OrdinalIgnoreCase);
            if (exhaustedKeys.Count > 0)
            {
                eligible = eligible.Where(candidate => !exhaustedKeys.Contains(
                    (candidate.Site ?? string.Empty) + "\u001f" + (candidate.Id ?? string.Empty)));
            }
            return DanmuMatchScorer.SelectAutoCandidate(eligible.ToList());
        }

        /// <summary>
        /// r5 residual selection operates only over the already filtered
        /// target-season eligible set. The legacy supplemental name remains as
        /// a compatibility wrapper, but no production path uses it.
        /// </summary>
        public static DanmuMatchCandidate SelectResidualCandidate(
            IEnumerable<DanmuMatchCandidate> candidates,
            IEnumerable<CompositeSeasonSourceIdentity> exhaustedSources)
        {
            return SelectSupplementalCandidate(candidates, exhaustedSources);
        }

        // Compatibility wrapper for the first composite import rollout. New
        // callers should pass the complete exhausted-source set above.
        public static DanmuMatchCandidate SelectSupplementalCandidate(
            IEnumerable<DanmuMatchCandidate> candidates, CompositeSeasonSourceIdentity exhaustedSource,
            string exhaustedLookupId, bool primaryExhausted)
        {
            if (exhaustedSource != null && !string.IsNullOrWhiteSpace(exhaustedLookupId))
            {
                exhaustedSource = new CompositeSeasonSourceIdentity
                {
                    ProviderId = exhaustedSource.ProviderId,
                    MediaId = exhaustedSource.MediaId,
                    MediaLookupId = exhaustedLookupId,
                };
            }
            return SelectSupplementalCandidate(candidates,
                primaryExhausted && exhaustedSource != null ? new[] { exhaustedSource } : Enumerable.Empty<CompositeSeasonSourceIdentity>());
        }
        public static List<CompositeSeasonLocalEpisode> GetLocalEpisodes(IEnumerable<Episode> episodes)
        {
            return GetLocalEpisodes(episodes, null);
        }

        /// <summary>
        /// Creates local planning records from Episode metadata.  With a target
        /// context it returns the complete placed/display inventory while
        /// marking owning, supplemental, and unknown logical ownership.  Only
        /// callers asking for the ownership set may use it as primary evidence.
        /// </summary>
        public static List<CompositeSeasonLocalEpisode> GetLocalEpisodes(
            IEnumerable<Episode> episodes, CompositeSeasonTargetContext targetContext)
        {
            // Preserve Emby's enumeration ordinal first. IndexNumber alone is
            // not a placement key: a displayed S00E01 may coexist with S01E01.
            var translated = (episodes ?? Enumerable.Empty<Episode>())
                .Where(x => x != null)
                .Select((episode, index) => new CompositeSeasonLocalEpisode
                {
                    ItemId = episode.Id.ToString(),
                    EpisodeNumber = episode.IndexNumber,
                    OriginalEpisodeNumber = episode.IndexNumber,
                    ParentSeasonNumber = episode.ParentIndexNumber,
                    PlacementOrder = index,
                    PlacementRelation = 0,
                    // These placement properties exist on newer Emby runtimes
                    // but not on the 4.8.5 compile reference used by the plugin.
                    // Read them compatibly and fail back to enumeration order.
                    AirsBeforeSeasonNumber = GetNullableIntProperty(episode, "AirsBeforeSeasonNumber"),
                    AirsBeforeEpisodeNumber = GetNullableIntProperty(episode, "AirsBeforeEpisodeNumber"),
                    AirsAfterSeasonNumber = GetNullableIntProperty(episode, "AirsAfterSeasonNumber"),
                    LogicalSeasonLabel = episode.ParentIndexNumber.HasValue
                        ? "S" + episode.ParentIndexNumber.Value.ToString("00") : string.Empty,
                    SortOrder = index,
                })
                .ToList();
            ApplyPlacementMetadata(translated, targetContext);
            if (targetContext == null) return translated;
            return CompositeSeasonOwnership.TryGetDisplayEpisodes(targetContext, translated, out var display)
                ? display
                : new List<CompositeSeasonLocalEpisode>();
        }

        private static void ApplyPlacementMetadata(
            IList<CompositeSeasonLocalEpisode> episodes, CompositeSeasonTargetContext context)
        {
            if (episodes == null || context == null || !context.IsKnown) return;
            var owning = episodes.Where(episode => episode.ParentSeasonNumber == context.TargetSeasonNumber)
                .ToList();
            var lastOwningOrder = owning.Select(episode => episode.SortOrder ?? 0).DefaultIfEmpty(0).Max();
            foreach (var episode in episodes)
            {
                if (episode.AirsBeforeSeasonNumber == context.TargetSeasonNumber &&
                    episode.AirsBeforeEpisodeNumber.GetValueOrDefault() > 0)
                {
                    var anchor = owning.FirstOrDefault(candidate =>
                        candidate.OriginalEpisodeNumber == episode.AirsBeforeEpisodeNumber);
                    episode.PlacementOrder = anchor?.SortOrder ?? lastOwningOrder + 1;
                    episode.PlacementRelation = -1;
                }
                else if (episode.AirsAfterSeasonNumber == context.TargetSeasonNumber)
                {
                    episode.PlacementOrder = lastOwningOrder + 1;
                    episode.PlacementRelation = 1;
                }
            }
        }

        private static int? GetNullableIntProperty(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName)) return null;
            var cacheKey = instance.GetType().AssemblyQualifiedName + "\u001f" + propertyName;
            PropertyInfo property;
            lock (PlacementPropertyLock)
            {
                if (!PlacementProperties.TryGetValue(cacheKey, out property))
                {
                    property = instance.GetType().GetProperty(propertyName);
                    // Cache missing properties too by retaining a null value.
                    PlacementProperties[cacheKey] = property;
                }
            }
            if (property == null || !property.CanRead) return null;
            try
            {
                var value = property.GetValue(instance, null);
                if (value == null) return null;
                return value is int number ? number : Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        public static CompositeSeasonSourceIdentity GetSource(string providerId, ScraperMedia media, string fallbackMediaId)
        {
            return new CompositeSeasonSourceIdentity
            {
                ProviderId = providerId ?? string.Empty,
                MediaId = !string.IsNullOrWhiteSpace(media?.Id) ? media.Id : fallbackMediaId ?? string.Empty,
                MediaLookupId = fallbackMediaId ?? string.Empty,
            };
        }

        public static List<CompositeSeasonSourceEpisode> GetSourceEpisodes(ScraperMedia media)
        {
            return (media?.Episodes ?? new List<ScraperEpisode>())
                .Select((episode, index) => new CompositeSeasonSourceEpisode
                {
                    EpisodeId = episode?.Id ?? string.Empty,
                    CommentId = episode?.CommentId ?? string.Empty,
                    EpisodeNumber = episode?.EpisodeNumber,
                    SourceOrdinal = index + 1,
                })
                .ToList();
        }

        /// <summary>
        /// Returns an ephemeral, bounded title lookup for a single provider
        /// response. Callers keep it only for preview projection; mappings,
        /// fingerprints, evidence, persistence, and download execution never
        /// receive these values.
        /// </summary>
        public static Dictionary<string, string> GetSourceEpisodeNames(
            ScraperMedia media, CompositeSeasonSourceIdentity source)
        {
            return (media?.Episodes ?? new List<ScraperEpisode>())
                .Where(episode => episode != null && !string.IsNullOrWhiteSpace(episode.Id) &&
                    !string.IsNullOrWhiteSpace(episode.CommentId))
                .GroupBy(episode => GetSourceEpisodeNameKey(source, episode.Id), StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => BoundSourceEpisodeName(group.First().Title), StringComparer.Ordinal);
        }

        public static string GetSourceEpisodeNameKey(
            CompositeSeasonSourceIdentity source, string episodeId)
        {
            return (source?.ProviderId ?? string.Empty) + "\u001f" +
                (source?.MediaId ?? string.Empty) + "\u001f" + (episodeId ?? string.Empty);
        }

        private static string BoundSourceEpisodeName(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            var normalized = title.Trim();
            return normalized.Length <= MaximumSourceEpisodeNameLength
                ? normalized : normalized.Substring(0, MaximumSourceEpisodeNameLength);
        }

        public static CompositeSeasonEpisodeMapping CreateDirectMapping(
            string localEpisodeItemId,
            string providerId,
            ScraperMedia media,
            string fallbackMediaId)
        {
            var sourceEpisode = GetSourceEpisodes(media).FirstOrDefault();
            if (sourceEpisode == null)
            {
                return null;
            }

            return new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = localEpisodeItemId ?? string.Empty,
                // A direct Episode id is a lookup token, not a parent media id.
                // Group exact evidence from one provider under a conservative
                // placeholder identity; any later verified Season/special entry
                // remains distinct and therefore makes the final plan composite.
                Source = new CompositeSeasonSourceIdentity
                {
                    ProviderId = providerId ?? string.Empty,
                    MediaId = !string.IsNullOrWhiteSpace(media?.Id) &&
                              !string.Equals(media.Id, fallbackMediaId, StringComparison.OrdinalIgnoreCase)
                        ? media.Id
                        : "direct-episode-provider:" + (providerId ?? string.Empty),
                    MediaLookupId = fallbackMediaId ?? string.Empty,
                },
                SourceEpisodeId = sourceEpisode.EpisodeId,
                CommentId = sourceEpisode.CommentId,
                SourceEpisodeNumber = sourceEpisode.EpisodeNumber,
                Origin = "episode-provider-id",
                MatchScore = 1,
                ScoreOrigin = "exact-episode-id",
                SourceMetadata = GetSourceMetadata(media),
            };
        }

        public static SourceMetadata GetSourceMetadata(ScraperMedia media)
        {
            if (media == null) return null;
            if (media.SourceMetadata?.HasValue == true) return media.SourceMetadata.Clone();
            var metadata = new SourceMetadata
            {
                Title = media.Title ?? string.Empty,
                Year = media.Year,
                Category = media.Category ?? string.Empty,
            };
            return metadata.HasValue ? metadata : null;
        }

        public static List<DanmuCompositeSeasonGroup> ToGroups(
            CompositeSeasonPlan plan,
            IEnumerable<Episode> localEpisodes,
            IReadOnlyDictionary<string, string> sourceEpisodeNames = null)
        {
            var names = (localEpisodes ?? Enumerable.Empty<Episode>())
                .Where(x => x != null)
                .ToDictionary(x => x.Id.ToString(), x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            var episodeNumbers = (plan?.OrderedEpisodes ?? new List<CompositeSeasonLocalEpisode>())
                .ToDictionary(x => x.ItemId, x => x.EpisodeNumber, StringComparer.OrdinalIgnoreCase);
            var groups = new List<DanmuCompositeSeasonGroup>();
            var groupIndex = 0;
            foreach (var run in CompositeSeasonPlanner.GetEditableMappedRuns(plan))
            {
                AddMappedGroup(groups, run.Mappings, names, episodeNumbers, sourceEpisodeNames, ref groupIndex);
            }

            foreach (var run in plan?.UnmatchedRuns ?? new List<CompositeSeasonUnmatchedRun>())
            {
                groups.Add(new DanmuCompositeSeasonGroup
                {
                    GroupId = "temporary-" + (++groupIndex),
                    IsTemporary = true,
                    Episodes = run.Episodes.Select(episode => new DanmuCompositeEpisode
                    {
                        ItemId = episode.ItemId,
                        EpisodeNumber = episode.EpisodeNumber,
                        EpisodeName = names.TryGetValue(episode.ItemId, out var name) ? name : string.Empty,
                    }).ToList(),
                });
            }

            return groups;
        }

        private static void AddMappedGroup(
            ICollection<DanmuCompositeSeasonGroup> groups,
            IList<CompositeSeasonEpisodeMapping> mappings,
            IReadOnlyDictionary<string, string> names,
            IReadOnlyDictionary<string, int?> episodeNumbers,
            IReadOnlyDictionary<string, string> sourceEpisodeNames,
            ref int groupIndex)
        {
            if (mappings == null || mappings.Count == 0) return;
            var first = mappings[0];
            var origins = mappings.Select(x => x.Origin ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            groups.Add(new DanmuCompositeSeasonGroup
            {
                GroupId = "mapped-" + (++groupIndex),
                IsTemporary = false,
                Site = first.Source.ProviderId ?? string.Empty,
                CandidateId = !string.IsNullOrWhiteSpace(first.Source.MediaLookupId)
                    ? first.Source.MediaLookupId
                    : first.Source.MediaId ?? string.Empty,
                SourceStartEpisodeId = first.SourceEpisodeId ?? string.Empty,
                SourceStartEpisodeNumber = first.SourceEpisodeNumber,
                MatchOrigin = origins.Count == 1 ? origins[0] : "mixed",
                MatchScore = first.MatchScore,
                ScoreOrigin = first.ScoreOrigin ?? string.Empty,
                SelectionEvidenceToken = first.SelectionEvidenceToken ?? string.Empty,
                SourceMetadata = first.SourceMetadata?.Clone(),
                Episodes = mappings.Select(mapping => new DanmuCompositeEpisode
                {
                    ItemId = mapping.LocalEpisodeItemId,
                    EpisodeNumber = episodeNumbers.TryGetValue(mapping.LocalEpisodeItemId, out var number) ? number : null,
                    EpisodeName = names.TryGetValue(mapping.LocalEpisodeItemId, out var name) ? name : string.Empty,
                    SourceEpisodeNumber = mapping.SourceEpisodeNumber,
                    SourceEpisodeName = sourceEpisodeNames != null && sourceEpisodeNames.TryGetValue(
                        GetSourceEpisodeNameKey(mapping.Source, mapping.SourceEpisodeId), out var sourceName)
                        ? sourceName ?? string.Empty : string.Empty,
                }).ToList(),
            });
        }
    }
}
