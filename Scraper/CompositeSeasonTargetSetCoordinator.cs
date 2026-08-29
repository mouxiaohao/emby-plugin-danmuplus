using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// One orchestration shape for Series (many targets) and Season (one
    /// target) previews.  The supplied builder remains the sole authority for
    /// detail resolution and Episode-plan construction.
    /// </summary>
    public static class CompositeSeasonTargetSetCoordinator
    {
        public static async Task<List<DanmuSeasonMatchResult>> BuildAsync(
            IEnumerable<CompositeSeasonTargetRequest> targets,
            CancellationToken cancellationToken)
        {
            return await BuildAsync(targets, false, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<List<DanmuSeasonMatchResult>> BuildAsync(
            IEnumerable<CompositeSeasonTargetRequest> targets,
            bool enableLogicalContinuation,
            CancellationToken cancellationToken)
        {
            var ordered = (targets ?? Enumerable.Empty<CompositeSeasonTargetRequest>()).ToList();
            if (ordered.Any(target => target == null || string.IsNullOrWhiteSpace(target.SeasonId) ||
                                      (target.BuildPreviewAsync == null &&
                                       target.BuildPreviewWithContextAsync == null)))
            {
                throw new ArgumentException("Every Season target requires a stable id and preview builder.", nameof(targets));
            }
            if (ordered.GroupBy(target => target.SeasonId, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException("A target set cannot contain the same Season twice.", nameof(targets));
            }

            var results = new List<DanmuSeasonMatchResult>(ordered.Count);
            SeasonLogicalTargetOutcome activeOutcome = null;
            foreach (var target in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var localSeasonNumber = target.SeasonNumber;
                SeasonLogicalTargetContext logicalContext;
                if (enableLogicalContinuation && activeOutcome != null &&
                    localSeasonNumber == activeOutcome.LocalSeasonNumber + 1)
                {
                    var proof = CreateProof(activeOutcome, target.SeasonId, localSeasonNumber);
                    logicalContext = SeasonLogicalTargetContext.Continuation(
                        proof.ExpectedLogicalSeasonNumber, proof.RequiredProviderId, proof);
                }
                else
                {
                    activeOutcome = null;
                    logicalContext = SeasonLogicalTargetContext.Local(localSeasonNumber);
                }

                var result = target.BuildPreviewWithContextAsync != null
                    ? await target.BuildPreviewWithContextAsync(
                        logicalContext.Clone(), cancellationToken, cancellationToken).ConfigureAwait(false)
                    : await target.BuildPreviewAsync(cancellationToken, cancellationToken).ConfigureAwait(false);
                if (result == null || !string.Equals(result.SeasonId, target.SeasonId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A Season target returned a mismatched preview.");
                }
                results.Add(result);

                var outcome = result.LogicalContinuationOutcome;
                var outcomeMatchesTarget = outcome != null && outcome.IsValidComplete() &&
                    string.Equals(outcome.SeasonId, target.SeasonId, StringComparison.OrdinalIgnoreCase) &&
                    outcome.LocalSeasonNumber == localSeasonNumber;
                if (!outcomeMatchesTarget)
                {
                    activeOutcome = null;
                    continue;
                }

                if (logicalContext.IsContinuation)
                {
                    activeOutcome = outcome.InitialLogicalSeasonNumber ==
                                    logicalContext.ExpectedLogicalSeasonNumber &&
                                    string.Equals(outcome.SeriesId,
                                        logicalContext.Proof.SeriesId,
                                        StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(outcome.ProviderId,
                                        logicalContext.RequiredProviderId,
                                        StringComparison.OrdinalIgnoreCase)
                        ? outcome.Clone()
                        : null;
                }
                else
                {
                    activeOutcome = outcome.ActivatedByLogicalSeasonAdvance
                        ? outcome.Clone()
                        : null;
                }
            }
            return results;
        }

        private static SeasonLogicalContinuationProof CreateProof(
            SeasonLogicalTargetOutcome predecessor,
            string currentSeasonId,
            int currentLocalSeasonNumber)
        {
            return new SeasonLogicalContinuationProof
            {
                SeriesId = predecessor.SeriesId,
                PredecessorSeasonId = predecessor.SeasonId,
                CurrentSeasonId = currentSeasonId ?? string.Empty,
                PredecessorLocalSeasonNumber = predecessor.LocalSeasonNumber,
                CurrentLocalSeasonNumber = currentLocalSeasonNumber,
                PredecessorPlanGeneration = predecessor.PlanGeneration,
                PredecessorStructureFingerprint = predecessor.StructureFingerprint,
                PredecessorPlanFingerprint = predecessor.PlanFingerprint,
                PredecessorInitialLogicalSeasonNumber = predecessor.InitialLogicalSeasonNumber,
                PredecessorTerminalLogicalSeasonNumber = predecessor.TerminalLogicalSeasonNumber,
                ExpectedLogicalSeasonNumber = predecessor.TerminalLogicalSeasonNumber + 1,
                RequiredProviderId = predecessor.ProviderId,
                AnimatedWholeSeries = predecessor.AnimatedWholeSeries,
                ActivatedByLogicalSeasonAdvance = true,
                EligibleItemIds = (predecessor.EligibleItemIds ?? new List<string>()).ToList(),
                MappedItemIds = (predecessor.MappedItemIds ?? new List<string>()).ToList(),
            };
        }
    }

    public sealed class CompositeSeasonTargetRequest
    {
        public string SeasonId { get; set; } = string.Empty;
        public int SeasonNumber { get; set; }
        public Func<CancellationToken, CancellationToken, Task<DanmuSeasonMatchResult>> BuildPreviewAsync { get; set; }
        public Func<SeasonLogicalTargetContext, CancellationToken, CancellationToken,
            Task<DanmuSeasonMatchResult>> BuildPreviewWithContextAsync { get; set; }
    }
}
