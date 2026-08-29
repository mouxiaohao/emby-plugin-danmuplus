using System;
using System.Collections.Generic;
using System.Linq;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Server-owned proof that one animated whole-Series target may continue
    /// from the immediately preceding local Season. It is deliberately never
    /// accepted from a request model.
    /// </summary>
    public sealed class SeasonLogicalContinuationProof
    {
        public string SeriesId { get; set; } = string.Empty;
        public string PredecessorSeasonId { get; set; } = string.Empty;
        public string CurrentSeasonId { get; set; } = string.Empty;
        public int PredecessorLocalSeasonNumber { get; set; }
        public int CurrentLocalSeasonNumber { get; set; }
        public long PredecessorPlanGeneration { get; set; }
        public string PredecessorStructureFingerprint { get; set; } = string.Empty;
        public string PredecessorPlanFingerprint { get; set; } = string.Empty;
        public int PredecessorInitialLogicalSeasonNumber { get; set; }
        public int PredecessorTerminalLogicalSeasonNumber { get; set; }
        public int ExpectedLogicalSeasonNumber { get; set; }
        public string RequiredProviderId { get; set; } = string.Empty;
        public bool AnimatedWholeSeries { get; set; }
        // Once a real logical-Season transition seeds the chain, this remains
        // true while complete adjacent targets propagate it. Part count never
        // sets this flag.
        public bool ActivatedByLogicalSeasonAdvance { get; set; }
        public List<string> EligibleItemIds { get; set; } = new List<string>();
        public List<string> MappedItemIds { get; set; } = new List<string>();

        public SeasonLogicalContinuationProof Clone()
        {
            return new SeasonLogicalContinuationProof
            {
                SeriesId = SeriesId ?? string.Empty,
                PredecessorSeasonId = PredecessorSeasonId ?? string.Empty,
                CurrentSeasonId = CurrentSeasonId ?? string.Empty,
                PredecessorLocalSeasonNumber = PredecessorLocalSeasonNumber,
                CurrentLocalSeasonNumber = CurrentLocalSeasonNumber,
                PredecessorPlanGeneration = PredecessorPlanGeneration,
                PredecessorStructureFingerprint = PredecessorStructureFingerprint ?? string.Empty,
                PredecessorPlanFingerprint = PredecessorPlanFingerprint ?? string.Empty,
                PredecessorInitialLogicalSeasonNumber = PredecessorInitialLogicalSeasonNumber,
                PredecessorTerminalLogicalSeasonNumber = PredecessorTerminalLogicalSeasonNumber,
                ExpectedLogicalSeasonNumber = ExpectedLogicalSeasonNumber,
                RequiredProviderId = RequiredProviderId ?? string.Empty,
                AnimatedWholeSeries = AnimatedWholeSeries,
                ActivatedByLogicalSeasonAdvance = ActivatedByLogicalSeasonAdvance,
                EligibleItemIds = (EligibleItemIds ?? new List<string>()).ToList(),
                MappedItemIds = (MappedItemIds ?? new List<string>()).ToList(),
            };
        }

        public bool IsValid()
        {
            return AnimatedWholeSeries && ActivatedByLogicalSeasonAdvance &&
                   !string.IsNullOrWhiteSpace(SeriesId) &&
                   !string.IsNullOrWhiteSpace(PredecessorSeasonId) &&
                   !string.IsNullOrWhiteSpace(CurrentSeasonId) &&
                   !string.Equals(PredecessorSeasonId, CurrentSeasonId,
                       StringComparison.OrdinalIgnoreCase) &&
                   PredecessorLocalSeasonNumber > 0 &&
                   CurrentLocalSeasonNumber == PredecessorLocalSeasonNumber + 1 &&
                   PredecessorPlanGeneration > 0 &&
                   !string.IsNullOrWhiteSpace(PredecessorStructureFingerprint) &&
                   !string.IsNullOrWhiteSpace(PredecessorPlanFingerprint) &&
                   PredecessorInitialLogicalSeasonNumber > 0 &&
                   PredecessorTerminalLogicalSeasonNumber >= PredecessorInitialLogicalSeasonNumber &&
                   ExpectedLogicalSeasonNumber == PredecessorTerminalLogicalSeasonNumber + 1 &&
                   !string.IsNullOrWhiteSpace(RequiredProviderId) &&
                   HasExactCoverage(EligibleItemIds, MappedItemIds);
        }

        public bool HasSameIdentity(SeasonLogicalContinuationProof other)
        {
            if (other == null) return false;
            return string.Equals(SeriesId, other.SeriesId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(PredecessorSeasonId, other.PredecessorSeasonId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(CurrentSeasonId, other.CurrentSeasonId, StringComparison.OrdinalIgnoreCase) &&
                   PredecessorLocalSeasonNumber == other.PredecessorLocalSeasonNumber &&
                   CurrentLocalSeasonNumber == other.CurrentLocalSeasonNumber &&
                   PredecessorPlanGeneration == other.PredecessorPlanGeneration &&
                   string.Equals(PredecessorStructureFingerprint, other.PredecessorStructureFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(PredecessorPlanFingerprint, other.PredecessorPlanFingerprint,
                       StringComparison.Ordinal) &&
                   PredecessorInitialLogicalSeasonNumber == other.PredecessorInitialLogicalSeasonNumber &&
                   PredecessorTerminalLogicalSeasonNumber == other.PredecessorTerminalLogicalSeasonNumber &&
                   ExpectedLogicalSeasonNumber == other.ExpectedLogicalSeasonNumber &&
                   string.Equals(RequiredProviderId, other.RequiredProviderId,
                       StringComparison.OrdinalIgnoreCase) &&
                   AnimatedWholeSeries == other.AnimatedWholeSeries &&
                   ActivatedByLogicalSeasonAdvance == other.ActivatedByLogicalSeasonAdvance &&
                   SetsEqual(EligibleItemIds, other.EligibleItemIds) &&
                   SetsEqual(MappedItemIds, other.MappedItemIds);
        }

        private static bool SetsEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            return new HashSet<string>(left ?? Enumerable.Empty<string>(),
                       StringComparer.OrdinalIgnoreCase)
                .SetEquals(right ?? Enumerable.Empty<string>());
        }

        internal static bool HasExactCoverage(
            IEnumerable<string> eligible,
            IEnumerable<string> mapped)
        {
            var expected = (eligible ?? Enumerable.Empty<string>()).ToList();
            var actual = (mapped ?? Enumerable.Empty<string>()).ToList();
            return expected.Count > 0 && expected.All(item => !string.IsNullOrWhiteSpace(item)) &&
                   actual.All(item => !string.IsNullOrWhiteSpace(item)) &&
                   expected.Distinct(StringComparer.OrdinalIgnoreCase).Count() == expected.Count &&
                   actual.Distinct(StringComparer.OrdinalIgnoreCase).Count() == actual.Count &&
                   expected.Count == actual.Count &&
                   new HashSet<string>(expected, StringComparer.OrdinalIgnoreCase).SetEquals(actual);
        }
    }

    /// <summary>Immutable-by-convention input for one local Season preview.</summary>
    public sealed class SeasonLogicalTargetContext
    {
        public int ExpectedLogicalSeasonNumber { get; private set; }
        public string RequiredProviderId { get; private set; } = string.Empty;
        public SeasonLogicalContinuationProof Proof { get; private set; }

        public bool IsContinuation => Proof != null && Proof.IsValid() &&
            ExpectedLogicalSeasonNumber == Proof.ExpectedLogicalSeasonNumber &&
            string.Equals(RequiredProviderId, Proof.RequiredProviderId,
                StringComparison.OrdinalIgnoreCase);

        public static SeasonLogicalTargetContext Local(int localSeasonNumber)
        {
            return new SeasonLogicalTargetContext
            {
                ExpectedLogicalSeasonNumber = Math.Max(0, localSeasonNumber),
            };
        }

        public static SeasonLogicalTargetContext Continuation(
            int expectedLogicalSeasonNumber,
            string requiredProviderId,
            SeasonLogicalContinuationProof proof)
        {
            return new SeasonLogicalTargetContext
            {
                ExpectedLogicalSeasonNumber = expectedLogicalSeasonNumber,
                RequiredProviderId = requiredProviderId ?? string.Empty,
                Proof = proof?.Clone(),
            };
        }

        public SeasonLogicalTargetContext Clone()
        {
            return IsContinuation
                ? Continuation(ExpectedLogicalSeasonNumber, RequiredProviderId, Proof)
                : Local(ExpectedLogicalSeasonNumber);
        }
    }

    /// <summary>
    /// Server-only authoritative summary emitted after one target preview. It
    /// contains no browser-authored state and is consumed only by the target-set
    /// coordinator.
    /// </summary>
    public sealed class SeasonLogicalTargetOutcome
    {
        public string SeriesId { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public int LocalSeasonNumber { get; set; }
        public int InitialLogicalSeasonNumber { get; set; }
        public int TerminalLogicalSeasonNumber { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public bool AnimatedWholeSeries { get; set; }
        public bool ActivatedByLogicalSeasonAdvance { get; set; }
        public bool IsAuthoritativeComplete { get; set; }
        public bool GenerationCurrent { get; set; }
        public long PlanGeneration { get; set; }
        public string StructureFingerprint { get; set; } = string.Empty;
        public string PlanFingerprint { get; set; } = string.Empty;
        public List<string> EligibleItemIds { get; set; } = new List<string>();
        public List<string> MappedItemIds { get; set; } = new List<string>();
        public string SourceTitle { get; set; } = string.Empty;

        public bool IsValidComplete()
        {
            return AnimatedWholeSeries && IsAuthoritativeComplete && GenerationCurrent &&
                   !string.IsNullOrWhiteSpace(SeriesId) && !string.IsNullOrWhiteSpace(SeasonId) &&
                   LocalSeasonNumber > 0 && InitialLogicalSeasonNumber > 0 &&
                   TerminalLogicalSeasonNumber >= InitialLogicalSeasonNumber &&
                   !string.IsNullOrWhiteSpace(ProviderId) && PlanGeneration > 0 &&
                   !string.IsNullOrWhiteSpace(StructureFingerprint) &&
                   !string.IsNullOrWhiteSpace(PlanFingerprint) &&
                   SeasonLogicalContinuationProof.HasExactCoverage(EligibleItemIds, MappedItemIds) &&
                   ActivatedByLogicalSeasonAdvance ==
                   (TerminalLogicalSeasonNumber > InitialLogicalSeasonNumber);
        }

        public SeasonLogicalTargetOutcome Clone()
        {
            return new SeasonLogicalTargetOutcome
            {
                SeriesId = SeriesId ?? string.Empty,
                SeasonId = SeasonId ?? string.Empty,
                LocalSeasonNumber = LocalSeasonNumber,
                InitialLogicalSeasonNumber = InitialLogicalSeasonNumber,
                TerminalLogicalSeasonNumber = TerminalLogicalSeasonNumber,
                ProviderId = ProviderId ?? string.Empty,
                AnimatedWholeSeries = AnimatedWholeSeries,
                ActivatedByLogicalSeasonAdvance = ActivatedByLogicalSeasonAdvance,
                IsAuthoritativeComplete = IsAuthoritativeComplete,
                GenerationCurrent = GenerationCurrent,
                PlanGeneration = PlanGeneration,
                StructureFingerprint = StructureFingerprint ?? string.Empty,
                PlanFingerprint = PlanFingerprint ?? string.Empty,
                EligibleItemIds = (EligibleItemIds ?? new List<string>()).ToList(),
                MappedItemIds = (MappedItemIds ?? new List<string>()).ToList(),
                SourceTitle = SourceTitle ?? string.Empty,
            };
        }
    }

    public static class SeasonLogicalContinuationPolicy
    {
        public static bool IsEligible(
            bool isSeriesTarget,
            bool isAnimated,
            bool isFullSeriesTarget,
            bool manualKeywordDiscovery)
        {
            return isSeriesTarget && isAnimated && isFullSeriesTarget &&
                   !manualKeywordDiscovery;
        }

        /// <summary>
        /// Part/metadata selections never affect the logical Season. Only a
        /// closed, exactly-next logical-season decision can advance it.
        /// </summary>
        public static int GetTerminalLogicalSeason(
            int initialLogicalSeasonNumber,
            IEnumerable<DanmuRemainderDecisionEvidence> decisions)
        {
            var active = Math.Max(0, initialLogicalSeasonNumber);
            foreach (var decision in decisions ?? Enumerable.Empty<DanmuRemainderDecisionEvidence>())
            {
                if (decision != null &&
                    decision.IsValid() &&
                    string.Equals(decision.DecisionKind,
                        DanmuRemainderDecisionKinds.LogicalSeason, StringComparison.Ordinal) &&
                    string.Equals(decision.Stage,
                        DanmuRemainderDecisionStages.LogicalSeason, StringComparison.Ordinal) &&
                    decision.ActiveLogicalSeasonNumber == active &&
                    decision.LogicalSeasonNumber == active + 1)
                {
                    active = decision.LogicalSeasonNumber.Value;
                }
            }
            return active;
        }
    }
}
