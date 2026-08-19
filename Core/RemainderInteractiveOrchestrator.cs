using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Scraper;

namespace Emby.Plugin.Danmu.Core
{
    // Provider-neutral control flow. Opaque state deliberately keeps Controller
    // media/build types out of this seam and keeps CompositeSeasonPlanner pure.
    public sealed class RemainderAuthoritativeSnapshot
    {
        public object State { get; set; }
        // This is the only continuation the orchestrator may target. The
        // callback must return a single proved maximal suffix, not a UI range.
        public IList<string> UniqueSuffixItemIds { get; set; } = new List<string>();
        public int? SuffixFirstYear { get; set; }
        // Includes every unmatched run. It remains authoritative even when an
        // internal gap means no safe recursive continuation exists.
        public int TotalUnmatchedItemCount { get; set; }
        // Complete plan mapping inventory, including the confirmed prefix.
        public IList<RemainderProgressGuard.MappingSnapshot> Mappings { get; set; } =
            new List<RemainderProgressGuard.MappingSnapshot>();
    }

    public sealed class RemainderRoundCandidates
    {
        public IList<RemainderCandidate> Candidates { get; set; } = new List<RemainderCandidate>();
        public string PoolKey { get; set; } = string.Empty;
        public bool Complete { get; set; }
    }

    public sealed class RemainderCommitOutcome
    {
        public bool Committed { get; set; }
        public bool GenerationCurrent { get; set; }
        // A successful authoritative rebuild supplies the next complete
        // snapshot. Retaining this as one object prevents controller state from
        // being advanced ahead of the accepted plan.
        public RemainderAuthoritativeSnapshot NextSnapshot { get; set; }
    }

    public sealed class RemainderInteractiveOrchestrator
    {
        public async Task<object> RunAsync(
            RemainderAuthoritativeSnapshot initialSnapshot, RemainderDecisionInput initialInput,
            Func<object, CancellationToken, Task<RemainderAuthoritativeSnapshot>> refreshSnapshot,
            Func<RemainderAuthoritativeSnapshot, string, CancellationToken, Task<RemainderRoundCandidates>> getCandidates,
            Func<RemainderAuthoritativeSnapshot, RemainderDecision, CancellationToken, Task<RemainderCommitOutcome>> commit,
            CancellationToken cancellationToken)
        {
            var snapshot = initialSnapshot;
            if (snapshot == null || refreshSnapshot == null || getCandidates == null || commit == null)
                return snapshot?.State;

            var input = initialInput ?? new RemainderDecisionInput();
            // The controller establishes this from the first server-resolved
            // source. Keep a private copy so later round state cannot relax it.
            var providerLock = input.ProviderLock ?? string.Empty;
            var usedStable = input.UsedStableIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedLookup = input.UsedLookupIds ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Termination is measured from the first authoritative suffix, not
            // from all local rows or an arbitrary network depth.
            var maximumRounds = Math.Max(0, snapshot.UniqueSuffixItemIds?.Count ?? 0);
            var activePoolKey = string.Empty;
            for (var round = 0; round < maximumRounds; round++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    var refreshed = await refreshSnapshot(snapshot.State, cancellationToken).ConfigureAwait(false);
                    if (refreshed == null || refreshed.UniqueSuffixItemIds == null ||
                        refreshed.UniqueSuffixItemIds.Count == 0) break;
                    snapshot = refreshed;
                    input.RemainderEpisodeCount = snapshot.UniqueSuffixItemIds.Count;
                    input.RemainderFirstYear = snapshot.SuffixFirstYear;
                    input.ProviderLock = providerLock;
                    input.UsedStableIds = usedStable;
                    input.UsedLookupIds = usedLookup;

                    var pool = await getCandidates(snapshot, activePoolKey, cancellationToken).ConfigureAwait(false);
                    if (pool == null || !pool.Complete) break;
                    input.CandidateCoverageComplete = pool.Complete;
                    input.CanonicalCandidates = pool.Candidates ?? new List<RemainderCandidate>();
                    var decision = RemainderAutoMatchCoordinator.Decide(input);
                    var nextPoolKey = activePoolKey;
                    if (decision.State == RemainderDecisionState.NotApplicable)
                    {
                        // A logical search is deliberately fresh only after the
                        // two local tiers are inapplicable; Unknown/Rejected can
                        // never request or use it as a fallback.
                        var logical = await getCandidates(snapshot, "logical", cancellationToken).ConfigureAwait(false);
                        if (logical == null || !logical.Complete) break;
                        decision = RemainderAutoMatchCoordinator.DecideLogicalSeason(input,
                            logical.Candidates ?? new List<RemainderCandidate>());
                        if (decision.State == RemainderDecisionState.Selected)
                            nextPoolKey = logical.PoolKey ?? string.Empty;
                    }
                    if (decision.State != RemainderDecisionState.Selected || decision.Candidate == null) break;

                    var priorSuffix = new HashSet<string>(snapshot.UniqueSuffixItemIds,
                        StringComparer.OrdinalIgnoreCase);
                    var outcome = await commit(snapshot, decision, cancellationToken).ConfigureAwait(false);
                    var next = outcome?.NextSnapshot;
                    if (outcome == null || !outcome.Committed || !outcome.GenerationCurrent || next == null ||
                        !RemainderProgressGuard.CanCommit(true, snapshot.TotalUnmatchedItemCount,
                            next.TotalUnmatchedItemCount,
                            CountNewMappings(snapshot.Mappings, next.Mappings), snapshot.Mappings,
                            next.Mappings, priorSuffix)) break;

                    // These are operation facts, hence advance strictly after
                    // the complete rebuild/progress fence has accepted a round.
                    snapshot = next;
                    activePoolKey = nextPoolKey;
                    usedStable.Add(decision.Candidate.StableId ?? string.Empty);
                    usedLookup.Add(decision.Candidate.LookupId ?? string.Empty);
                    if (decision.Kind == RemainderDecisionKind.Part) input.LastPartNumber = decision.PartNumber;
                    if (decision.Kind == RemainderDecisionKind.LogicalSeason)
                    {
                        input.LogicalSeasonNumber = decision.NextLogicalSeasonNumber.GetValueOrDefault(input.LogicalSeasonNumber);
                        input.LastPartNumber = null;
                    }
                    input.LastSelectedTitles = (decision.Candidate.Titles ?? new List<string>()).ToList();
                }
                catch (OperationCanceledException)
                {
                    // A post-prefix cancellation is a silent safe stop.
                    break;
                }
                catch
                {
                    // Provider/detail/rebuild callback faults are Unknown. The
                    // caller retains the last committed snapshot and no lower
                    // tier or later round is attempted.
                    break;
                }
            }
            return snapshot.State;
        }

        private static int CountNewMappings(IEnumerable<RemainderProgressGuard.MappingSnapshot> before,
            IEnumerable<RemainderProgressGuard.MappingSnapshot> after)
        {
            var prior = new HashSet<string>((before ?? Enumerable.Empty<RemainderProgressGuard.MappingSnapshot>())
                .Where(x => x != null).Select(x => x.LocalId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            return (after ?? Enumerable.Empty<RemainderProgressGuard.MappingSnapshot>()).Count(x => x != null &&
                !prior.Contains(x.LocalId ?? string.Empty));
        }
    }
}
