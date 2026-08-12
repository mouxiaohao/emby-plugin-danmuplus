using System;
using System.Collections.Generic;
using System.Linq;
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

            var consumedIds = new HashSet<string>(plan.Mappings
                .Where(mapping => mapping.Source != null && mapping.Source.Equals(source))
                .Select(mapping => mapping.SourceEpisodeId), StringComparer.OrdinalIgnoreCase);
            var available = verifiedEpisodes.Where(episode => !consumedIds.Contains(episode.EpisodeId)).ToList();
            if (available.Count > 0 && plan.UnmatchedRuns.Count > 0 &&
                !CompositeSeasonPlanner.TryApplyRemainingSourceEpisodes(plan, source, available, origin, out plan, out error))
            {
                return false;
            }

            exhausted = AreSourceEpisodesExhausted(plan, source, verifiedIds);
            return true;
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
            return (episodes ?? Enumerable.Empty<Episode>())
                .Where(x => x != null)
                .OrderBy(x => x.IndexNumber ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .Select((episode, index) => new CompositeSeasonLocalEpisode
                {
                    ItemId = episode.Id.ToString(),
                    EpisodeNumber = episode.IndexNumber,
                    SortOrder = index,
                })
                .ToList();
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
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.CommentId))
                .Select((episode, index) => new CompositeSeasonSourceEpisode
                {
                    EpisodeId = episode.Id,
                    CommentId = episode.CommentId,
                    EpisodeNumber = episode.EpisodeNumber ?? index + 1,
                })
                .ToList();
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
            };
        }

        public static List<DanmuCompositeSeasonGroup> ToGroups(
            CompositeSeasonPlan plan,
            IEnumerable<Episode> localEpisodes)
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
                AddMappedGroup(groups, run.Mappings, names, episodeNumbers, ref groupIndex);
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
                Episodes = mappings.Select(mapping => new DanmuCompositeEpisode
                {
                    ItemId = mapping.LocalEpisodeItemId,
                    EpisodeNumber = episodeNumbers.TryGetValue(mapping.LocalEpisodeItemId, out var number) ? number : null,
                    EpisodeName = names.TryGetValue(mapping.LocalEpisodeItemId, out var name) ? name : string.Empty,
                    SourceEpisodeNumber = mapping.SourceEpisodeNumber,
                }).ToList(),
            });
        }
    }
}
