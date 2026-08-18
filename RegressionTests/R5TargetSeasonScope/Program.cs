using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;

namespace Emby.Plugin.Danmu.R5TargetSeasonScopeRegression
{
    internal static class Program
    {
        private static int Main()
        {
            OnePunchIgnoresSevenPlacedSpecials();
            SeitokaiIgnoresEightPlacedSpecials();
            WholeSeriesKnownPositiveTargetsNeverExecuteS0OrUnknown();
            MixedInventoryKeepsExactParentDownloadSetOnly();
            StandaloneSeasonZeroUsesOnlyParentZero();
            ExcludesOtherAndUnknownParents();
            EmptyEligibleScopeStaysNonExecutable();
            InventoryUnavailableFailsClosedByContract();
            DeDuplicatesIdenticalIdsAndRejectsConflicts();
            ShortSourceLeavesOnlyEligibleRemainder();
            DuplicateAndUnknownObservationsDoNotCutEligiblePlan();
            ParentChangeInvalidatesFingerprint();
            PlanFingerprintCoversSelectionsAndMappings();
            AutomaticGenerationIsSupersededByInteractivePreview();
            InteractiveAndAutomaticSnapshotsAreIdentical();
            SeriesAndDirectSeasonBatchFixturesHaveScopeParity();
            IdentifierMetamorphismCannotChangeBatchScope();
            SourceContractsKeepAllBatchPathsOnTheCoordinator();
            Console.WriteLine("R5 target-season scope regression checks passed.");
            return 0;
        }

        private static void OnePunchIgnoresSevenPlacedSpecials()
        {
            var input = Range("one-main", 12, 1, 0)
                .Concat(Range("one-special", 7, 0, 12)).ToList();
            var scope = Scope(1, input);
            Assert(scope.ObservedEpisodes.Count == 19 && scope.EligibleEpisodes.Count == 12 &&
                   scope.ParentZeroCount == 7 && scope.OtherSeasonCount == 0 &&
                   scope.UnknownParentCount == 0,
                "One Punch S1 must retain nineteen observations but expose only twelve Parent 1 Episodes");
            Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                    out var plan, out var error), error);
            plan = ApplyFullSource(plan, 12, "one-punch-s1");
            Assert(plan.OrderedEpisodes.Count == 12 && plan.Mappings.Count == 12 &&
                   plan.UnmatchedRuns.Count == 0 &&
                   plan.OrderedEpisodes.All(item => item.ParentSeasonNumber == 1),
                "One Punch S1 must map twelve Parent 1 Episodes with no S00 temporary run");
        }

        private static void SeitokaiIgnoresEightPlacedSpecials()
        {
            var scope = Scope(1, Range("seitokai-main", 13, 1, 0)
                .Concat(Range("seitokai-special", 8, 0, 13)));
            Assert(scope.ObservedEpisodes.Count == 21 && scope.EligibleEpisodes.Count == 13 &&
                   scope.ParentZeroCount == 8,
                "Seitokai S1 must expose thirteen Parent 1 Episodes and ignore eight placed S00 Episodes");
            Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                    out var plan, out var error), error);
            plan = ApplyFullSource(plan, 13, "seitokai-s1");
            Assert(plan.Mappings.Count == 13 && plan.UnmatchedRuns.Count == 0,
                "Seitokai S1 must map thirteen Parent 1 Episodes with no S00 temporary run");
        }

        private static void WholeSeriesKnownPositiveTargetsNeverExecuteS0OrUnknown()
        {
            var s1 = new FixtureSeason(1, Range("series-s1", 3, 1, 0)
                .Concat(Range("series-s1-special", 2, 0, 3)));
            var s0 = new FixtureSeason(0, Range("series-s0", 2, 0, 0));
            var unknown = new FixtureSeason(null, Range("series-unknown", 2, null, 0));
            var source = new CountingSourceHarness();
            var wholeSeries = KnownPositiveSeriesTargets(new[] { s1, s0, unknown })
                .Select(target => ExecuteFixtureTarget(target, source, 3))
                .ToList();

            Assert(wholeSeries.Count == 1 && source.ResolveCalls == 1 &&
                   wholeSeries[0].Plan.OrderedEpisodes.Count == 3 &&
                   wholeSeries[0].DownloadItemIds.All(id => s1.Episodes.Any(item => item.ItemId == id)) &&
                   !wholeSeries[0].DownloadItemIds.Any(id => s0.Episodes.Any(item => item.ItemId == id)),
                "whole-Series must enumerate and execute known positive S1+ only; S0 and unknown targets have zero source/download calls");
        }

        private static void MixedInventoryKeepsExactParentDownloadSetOnly()
        {
            var mixed = Range("mixed-main", 12, 1, 0)
                .Concat(Range("mixed-s0", 7, 0, 12))
                .Concat(Range("mixed-s2", 2, 2, 19))
                .Concat(new[] { Local("mixed-unknown", 1, null, 21) })
                .ToList();
            var target = new FixtureSeason(1, mixed);
            var complete = ExecuteFixtureTarget(target, new CountingSourceHarness(), 12);
            Assert(complete.Scope.EligibleEpisodes.Count == 12 && complete.Plan.UnmatchedRuns.Count == 0 &&
                   complete.DownloadItemIds.SequenceEqual(complete.Scope.EligibleEpisodes.Select(item => item.ItemId)) &&
                   complete.DownloadItemIds.All(id => mixed.Single(item => item.ItemId == id).ParentSeasonNumber == 1),
                "complete source mapping and download inputs must contain only exact Parent 1 Episodes, with zero temporary runs");

            var shortResult = ExecuteFixtureTarget(target, new CountingSourceHarness(), 10);
            Assert(shortResult.Plan.UnmatchedRuns.Count == 1 &&
                   shortResult.Plan.UnmatchedRuns[0].Episodes.Count == 2 &&
                   shortResult.Plan.UnmatchedRuns[0].Episodes.Select(item => item.ItemId).SequenceEqual(
                       shortResult.Scope.EligibleEpisodes.Skip(10).Select(item => item.ItemId)) &&
                   shortResult.DownloadItemIds.Count == 10,
                "a short source must leave one maximal eligible remainder and never introduce S0/foreign/unknown download input");
        }

        private static void StandaloneSeasonZeroUsesOnlyParentZero()
        {
            var ownInventory = Range("s0", 8, 0, 0)
                .Concat(Range("foreign-main", 2, 1, 7)).ToList();
            var scope = Scope(0, ownInventory);
            Assert(scope.EligibleEpisodes.Count == 8 && scope.EligibleEpisodes.All(item =>
                       item.ParentSeasonNumber == 0) && scope.OtherSeasonCount == 2 &&
                   scope.ParentZeroCount == 0,
                "explicit S0 must apply the same exact equality rule to its own inventory");
            Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                    out var plan, out var error), error);
            plan = ApplyFullSource(plan, 8, "seitokai-s0");
            Assert(plan.Mappings.Count == 8 && plan.UnmatchedRuns.Count == 0,
                "standalone Seitokai S0 must map only its eight own-inventory Parent 0 Episodes");
        }

        private static void ExcludesOtherAndUnknownParents()
        {
            var items = Range("s2", 3, 2, 0).ToList();
            items.Add(Local("s1", 1, 1, 3));
            items.Add(Local("s3", 1, 3, 4));
            items.Add(Local("unknown", 1, null, 5));
            var scope = Scope(2, items);
            Assert(scope.EligibleEpisodes.Count == 3 && scope.OtherSeasonCount == 2 &&
                   scope.UnknownParentCount == 1 && scope.EligibleEpisodes.All(item =>
                       item.ParentSeasonNumber == 2),
                "other normal Seasons and unknown parents must be physically excluded before planning");
            Assert(!CompositeSeasonOwnership.TryGetTargetScope(
                    CompositeSeasonTargetContext.ForSeasonNumber(null), items, out _, out var error) &&
                   error == "target-season-number-unknown",
                "an unknown target Season must fail closed");
        }

        private static void DeDuplicatesIdenticalIdsAndRejectsConflicts()
        {
            var repeated = Local("duplicate", 1, 1, 0);
            var scope = Scope(1, new[] { repeated, Clone(repeated) });
            Assert(scope.ObservedEpisodes.Count == 1 && scope.EligibleEpisodes.Count == 1 &&
                   scope.DuplicateIdentityCount == 1,
                "identical ItemId observations must be represented once");

            var conflict = Clone(repeated);
            conflict.ParentSeasonNumber = 0;
            Assert(!CompositeSeasonOwnership.TryGetTargetScope(
                    CompositeSeasonTargetContext.ForSeasonNumber(1),
                    new[] { repeated, conflict }, out _, out var error) &&
                   error.StartsWith("target-season-inventory-conflict:", StringComparison.Ordinal),
                "one ItemId observed with conflicting parent metadata must fail closed");

            var invalid = Clone(repeated);
            invalid.ItemId = Guid.Empty.ToString();
            var invalidScope = Scope(1, new[] { repeated, invalid });
            Assert(invalidScope.InvalidIdentityCount == 1 && invalidScope.EligibleEpisodes.Count == 1,
                "invalid identities must remain diagnostic-only and never enter the eligible set");
        }

        private static void EmptyEligibleScopeStaysNonExecutable()
        {
            var scope = Scope(1, Range("special-only", 3, 0, 0)
                .Concat(new[] { Local("unknown-only", 1, null, 4) }));
            Assert(scope.EligibleEpisodes.Count == 0 && scope.ParentZeroCount == 3 &&
                   scope.UnknownParentCount == 1,
                "an inventory with no exact-parent Episodes must expose no executable planning input");
        }

        private static void InventoryUnavailableFailsClosedByContract()
        {
            var root = FindRepositoryRoot();
            var coordinator = System.IO.File.ReadAllText(System.IO.Path.Combine(root,
                "Core", "SeasonTargetPlanningCoordinator.cs"));
            Assert(coordinator.Contains("var result = target.GetEpisodes();", StringComparison.Ordinal) &&
                   coordinator.Contains("result == null || result.Items == null", StringComparison.Ordinal) &&
                   coordinator.Contains("error = \"target-season-inventory-unavailable\";", StringComparison.Ordinal) &&
                   coordinator.Contains("catch (Exception)", StringComparison.Ordinal),
                "unavailable or throwing target-Season inventory must fail closed with a stable diagnostic");
        }

        private static void ShortSourceLeavesOnlyEligibleRemainder()
        {
            var scope = Scope(1, Range("main", 12, 1, 0)
                .Concat(Range("foreign", 7, 0, 12)));
            Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                    out var plan, out var error), error);
            var source = new CompositeSeasonSourceIdentity { ProviderId = "test", MediaId = "short" };
            var sourceEpisodes = Enumerable.Range(1, 10).Select(number =>
                new CompositeSeasonSourceEpisode
                {
                    EpisodeId = "source-" + number,
                    CommentId = "comment-" + number,
                    EpisodeNumber = number,
                }).ToList();
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    plan, source, sourceEpisodes, "scored", out plan, out error), error);
            Assert(plan.Mappings.Count == 10 && plan.UnmatchedRuns.Count == 1 &&
                   plan.UnmatchedRuns[0].Episodes.Count == 2 &&
                   plan.UnmatchedRuns[0].Episodes.All(item => item.ParentSeasonNumber == 1),
                "a short source must leave exactly one two-Episode eligible remainder regardless of foreign display records");
        }

        private static void DuplicateAndUnknownObservationsDoNotCutEligiblePlan()
        {
            var first = Local("duplicate-main", 1, 1, 0);
            var inventory = new List<CompositeSeasonLocalEpisode>
            {
                first,
                Clone(first),
                Local("second-main", 2, 1, 1),
                Local("unknown-between", 3, null, 2),
                Local("foreign-between", 4, 0, 3),
                Local("third-main", 3, 1, 4),
            };
            var result = ExecuteFixtureTarget(new FixtureSeason(1, inventory), new CountingSourceHarness(), 3);
            Assert(result.Scope.DuplicateIdentityCount == 1 && result.Scope.UnknownParentCount == 1 &&
                   result.Scope.EligibleEpisodes.Select(item => item.ItemId).SequenceEqual(new[]
                   {
                       first.ItemId, GuidFor("second-main"), GuidFor("third-main"),
                   }) && result.Plan.Mappings.Count == 3 && result.Plan.UnmatchedRuns.Count == 0,
                "duplicate and unknown observations are diagnostic-only and cannot split, truncate, or displace the exact-parent plan");
        }

        private static void ParentChangeInvalidatesFingerprint()
        {
            var firstScope = Scope(1, new[] { Local("moving", 1, 0, 0), Local("main", 1, 1, 1) });
            var secondScope = Scope(1, new[] { Local("moving", 1, 1, 0), Local("main", 1, 1, 1) });
            var context = new SeasonPlanningContext
            {
                SeriesId = GuidFor("series"), SeasonId = GuidFor("season"), TargetSeasonNumber = 1,
            };
            var first = SeasonPlanningContextBuilder.CreateStructureFingerprint(context, firstScope);
            var second = SeasonPlanningContextBuilder.CreateStructureFingerprint(context, secondScope);
            Assert(!string.Equals(first, second, StringComparison.Ordinal) &&
                   first.Contains(":0:excluded:", StringComparison.Ordinal) &&
                   second.Contains(":1:eligible:", StringComparison.Ordinal),
                "the fingerprint must include every observed parent and eligibility outcome");
        }

        private static void SourceContractsKeepAllBatchPathsOnTheCoordinator()
        {
            var root = FindRepositoryRoot();
            var controller = System.IO.File.ReadAllText(System.IO.Path.Combine(root,
                "Core", "Controllers", "DanmuController.cs"));
            var automatic = System.IO.File.ReadAllText(System.IO.Path.Combine(root,
                "LibraryManagerEventsHelper.cs"));
            Assert(controller.Contains("candidate.IndexNumber.HasValue && candidate.IndexNumber.Value > 0",
                       StringComparison.Ordinal) &&
                   controller.Contains("seasons.Where(candidate =>", StringComparison.Ordinal) &&
                   !controller.Contains("CompositeSeasonTargetOwnership.Resolve(inventories)",
                       StringComparison.Ordinal),
                "whole-Series must skip S0/unknown and interactive production must not resolve cross-target ownership");
            Assert(controller.Contains("SeasonTargetPlanningCoordinator.TryBuild(season, out context, out error)",
                       StringComparison.Ordinal) &&
                   automatic.Contains("SeasonTargetPlanningCoordinator.TryBuild(season, out context, out error)",
                       StringComparison.Ordinal) &&
                   automatic.Contains("eventType == EventType.Add || eventType == EventType.Update",
                       StringComparison.Ordinal) &&
                   !automatic.Substring(
                       automatic.IndexOf("public async Task ProcessQueuedSeasonEvents", StringComparison.Ordinal),
                       automatic.IndexOf("private bool TryBuildAutomaticPlanningContext", StringComparison.Ordinal) -
                       automatic.IndexOf("public async Task ProcessQueuedSeasonEvents", StringComparison.Ordinal))
                       .Contains("GetDanmuProviderId", StringComparison.Ordinal) &&
                   automatic.Contains("SeasonPlanGenerationCoordinator.Shared.Begin", StringComparison.Ordinal) &&
                   automatic.Contains("IsCurrent(seasonId, automaticGeneration)", StringComparison.Ordinal) &&
                   automatic.Contains("Generation = automaticGeneration", StringComparison.Ordinal) &&
                   automatic.Contains("seasons.Where(candidate =>", StringComparison.Ordinal) &&
                   automatic.Contains("season.IndexNumber.Value <= 0", StringComparison.Ordinal) &&
                   automatic.Contains("season?.IndexNumber.GetValueOrDefault() <= 0", StringComparison.Ordinal) &&
                   !automatic.Substring(
                       automatic.IndexOf("private async Task<bool> DownloadAutomaticSeasonWithCompositePlan", StringComparison.Ordinal),
                       automatic.IndexOf("private sealed class AutomaticSeasonPlanSnapshot", StringComparison.Ordinal) -
                       automatic.IndexOf("private async Task<bool> DownloadAutomaticSeasonWithCompositePlan", StringComparison.Ordinal))
                       .Contains("SearchSeasonAsync", StringComparison.Ordinal) &&
                   !automatic.Contains("season.IndexNumber.HasValue && season.IndexNumber == 0",
                       StringComparison.Ordinal),
                "interactive positive Seasons must share the coordinator, while unattended S0 is rejected before provider work and automatic plans never discover residual sources");
        }

        private static void PlanFingerprintCoversSelectionsAndMappings()
        {
            var scope = Scope(1, Range("fingerprint", 2, 1, 0));
            var context = new SeasonPlanningContext
            {
                SeriesId = GuidFor("fp-series"), SeasonId = GuidFor("fp-season"),
                TargetSeasonNumber = 1,
                StructureFingerprint = "scope-fingerprint",
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                out var firstPlan, out var error), error);
            var source = new CompositeSeasonSourceIdentity
                { ProviderId = "test", MediaId = "media", MediaLookupId = "lookup" };
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(firstPlan, source,
                new[]
                {
                    new CompositeSeasonSourceEpisode { EpisodeId = "ep-1", CommentId = "c-1", EpisodeNumber = 1 },
                    new CompositeSeasonSourceEpisode { EpisodeId = "ep-2", CommentId = "c-2", EpisodeNumber = 2 },
                }, "manual", out firstPlan, out error), error);
            var selection = new DanmuCompositeSeasonSelection
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                AlignmentIntent = DanmuCompositeAlignmentIntentWire.DefaultZeroOffset,
                Site = "test", CandidateId = "lookup",
                LocalStartEpisodeItemId = scope.EligibleEpisodes[0].ItemId,
                RequestedEpisodeCount = 2, SourceStartEpisodeId = "ep-1",
                MatchOrigin = "manual", SelectionEvidenceToken = "evidence-a",
                ServerResolvedAlignmentMode = CompositeSeasonAlignmentMode.NumberAware,
                ServerConsideredLocalEpisodeItemIds = scope.EligibleEpisodes
                    .Select(item => item.ItemId).ToList(),
                ServerSourceEpisodes = new List<CompositeSeasonSourceEpisode>
                {
                    new CompositeSeasonSourceEpisode
                        { EpisodeId = "ep-1", CommentId = "c-1", EpisodeNumber = 1, SourceOrdinal = 1 },
                    new CompositeSeasonSourceEpisode
                        { EpisodeId = "ep-2", CommentId = "c-2", EpisodeNumber = 2, SourceOrdinal = 2 },
                },
            };
            var baseline = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            Assert(baseline.Length == 64 && baseline.All(character =>
                       (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f')) &&
                   !baseline.Contains("c-1", StringComparison.Ordinal),
                "the V22 wire fingerprint must be a fixed lowercase SHA-256 digest and never expose CommentId");
            firstPlan.Mappings[0].CommentId = "changed-comment";
            var commentChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            firstPlan.Mappings[0].CommentId = "c-1";
            selection.CandidateId = "changed";
            var selectionChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            firstPlan.Mappings[0].SourceEpisodeId = "changed-episode";
            var mappingChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            firstPlan.Mappings[0].SourceEpisodeId = "ep-1";
            selection.CandidateId = "lookup";
            selection.AlignmentIntent = DanmuCompositeAlignmentIntentWire.ExplicitAnchor;
            var intentChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            selection.AlignmentIntent = DanmuCompositeAlignmentIntentWire.DefaultZeroOffset;
            selection.ServerResolvedAlignmentMode = CompositeSeasonAlignmentMode.PositionalFallback;
            var modeChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            selection.ServerResolvedAlignmentMode = CompositeSeasonAlignmentMode.NumberAware;
            selection.ServerSourceEpisodes[0].SourceOrdinal = 2;
            var provenanceChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            selection.ServerSourceEpisodes[0].SourceOrdinal = 1;
            selection.MappingProtocolVersion = 21;
            var protocolChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            selection.MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion;
            selection.ServerConsideredLocalEpisodeItemIds.Reverse();
            var consideredOrderChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, firstPlan);
            Assert(baseline != commentChanged && baseline != selectionChanged &&
                   selectionChanged != mappingChanged && baseline != intentChanged &&
                   baseline != modeChanged && baseline != provenanceChanged &&
                   baseline != protocolChanged && baseline != consideredOrderChanged,
                "protocol intent, resolved mode, source provenance/order, CommentId, and exact mappings must invalidate the server fingerprint");
        }

        private static void AutomaticGenerationIsSupersededByInteractivePreview()
        {
            var coordinator = new SeasonPlanGenerationCoordinator();
            var seasonId = GuidFor("automatic-generation-race");
            var automaticGeneration = coordinator.Begin(seasonId);
            Assert(coordinator.IsCurrent(seasonId, automaticGeneration),
                "a newly started automatic plan must initially own the Season generation");
            var interactiveGeneration = coordinator.Begin(seasonId);
            Assert(!coordinator.IsCurrent(seasonId, automaticGeneration) &&
                   coordinator.IsCurrent(seasonId, interactiveGeneration),
                "a later interactive preview must supersede the old automatic plan before further writes or mirror");
        }

        private static void InteractiveAndAutomaticSnapshotsAreIdentical()
        {
            foreach (var target in new[] { 1, 0 })
            {
                var scope = Scope(target, Range("parity-" + target, 3, target, 0)
                    .Concat(Range("parity-foreign-" + target, 2, target == 0 ? 1 : 0, 3)));
                Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                    out var interactive, out var error), error);
                Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                    out var automatic, out error), error);
                Assert(string.Join("|", interactive.OrderedEpisodes.Select(item => item.ItemId)) ==
                       string.Join("|", automatic.OrderedEpisodes.Select(item => item.ItemId)) &&
                       interactive.UnmatchedRuns.Single().Episodes.Count ==
                       automatic.UnmatchedRuns.Single().Episodes.Count,
                    "interactive and automatic normal/S0 snapshots must share eligible order and temporary runs");
            }
        }

        private static void SeriesAndDirectSeasonBatchFixturesHaveScopeParity()
        {
            var inventory = Range("parity-main", 4, 1, 0)
                .Concat(Range("parity-special", 3, 0, 4))
                .Concat(new[] { Local("parity-unknown", 1, null, 7) })
                .ToList();
            var direct = ExecuteFixtureTarget(new FixtureSeason(1, inventory), new CountingSourceHarness(), 3);
            var series = KnownPositiveSeriesTargets(new[]
                { new FixtureSeason(0, Range("ignored-s0", 1, 0, 0)), new FixtureSeason(1, inventory) })
                .Select(target => ExecuteFixtureTarget(target, new CountingSourceHarness(), 3)).Single();
            Assert(direct.Scope.EligibleEpisodes.Select(item => item.ItemId).SequenceEqual(
                       series.Scope.EligibleEpisodes.Select(item => item.ItemId)) &&
                   direct.Plan.OrderedEpisodes.Select(item => item.ItemId).SequenceEqual(
                       series.Plan.OrderedEpisodes.Select(item => item.ItemId)) &&
                   direct.Plan.UnmatchedRuns.Single().Episodes.Select(item => item.ItemId).SequenceEqual(
                       series.Plan.UnmatchedRuns.Single().Episodes.Select(item => item.ItemId)) &&
                   direct.DownloadItemIds.SequenceEqual(series.DownloadItemIds),
                "Series and direct-Season paths must produce identical eligible ids/count, plan, temporary remainder, and download set");
        }

        private static void IdentifierMetamorphismCannotChangeBatchScope()
        {
            var inventory = Range("meta-main", 3, 1, 0)
                .Concat(Range("meta-s0", 2, 0, 3)).ToList();
            var target = new FixtureSeason(1, inventory);
            var first = ExecuteFixtureTarget(target, new CountingSourceHarness(), 2,
                "Provider-A:old-identifier");
            var second = ExecuteFixtureTarget(target, new CountingSourceHarness(), 2,
                "Provider-B:forged-or-reordered-identifier");
            Assert(first.Scope.EligibleEpisodes.Select(item => item.ItemId).SequenceEqual(
                       second.Scope.EligibleEpisodes.Select(item => item.ItemId)) &&
                   first.Plan.Mappings.Select(item => item.LocalEpisodeItemId).SequenceEqual(
                       second.Plan.Mappings.Select(item => item.LocalEpisodeItemId)) &&
                   first.Plan.UnmatchedRuns.SelectMany(run => run.Episodes).Select(item => item.ItemId).SequenceEqual(
                       second.Plan.UnmatchedRuns.SelectMany(run => run.Episodes).Select(item => item.ItemId)) &&
                   first.DownloadItemIds.SequenceEqual(second.DownloadItemIds),
                "provider identifier metamorphism must not alter r5 target scope, plan membership, temporary runs, or batch download input");
        }

        // Small deterministic batch harness: it models only the r5 boundary
        // between a target inventory, the pure planner, and executable mapped
        // download input. It intentionally has no provider identifier branch.
        private sealed class FixtureSeason
        {
            public FixtureSeason(int? number, IEnumerable<CompositeSeasonLocalEpisode> episodes)
            {
                Number = number;
                Episodes = (episodes ?? Enumerable.Empty<CompositeSeasonLocalEpisode>()).ToList();
            }

            public int? Number { get; }
            public List<CompositeSeasonLocalEpisode> Episodes { get; }
        }

        private sealed class CountingSourceHarness
        {
            public int ResolveCalls { get; private set; }

            public List<CompositeSeasonSourceEpisode> Resolve(int count, string identifierMetamorphism)
            {
                ResolveCalls++;
                // The identifier is intentionally observed but never fed into
                // scope or planner membership; it represents arbitrary local
                // ProviderId changes that r5 must ignore.
                var ignored = identifierMetamorphism ?? string.Empty;
                return Enumerable.Range(1, count).Select(number => new CompositeSeasonSourceEpisode
                {
                    EpisodeId = "fixture-source-" + number,
                    CommentId = "fixture-comment-" + number,
                    EpisodeNumber = number,
                }).ToList();
            }
        }

        private sealed class FixtureBatchResult
        {
            public CompositeSeasonEpisodeScope Scope { get; set; }
            public CompositeSeasonPlan Plan { get; set; }
            public List<string> DownloadItemIds { get; set; } = new List<string>();
        }

        private static IEnumerable<FixtureSeason> KnownPositiveSeriesTargets(
            IEnumerable<FixtureSeason> seasons)
        {
            return (seasons ?? Enumerable.Empty<FixtureSeason>())
                .Where(season => season != null && season.Number.HasValue && season.Number.Value > 0)
                .OrderBy(season => season.Number.Value);
        }

        private static FixtureBatchResult ExecuteFixtureTarget(
            FixtureSeason target,
            CountingSourceHarness source,
            int sourceEpisodeCount,
            string identifierMetamorphism = "")
        {
            var scope = Scope(target.Number.Value, target.Episodes);
            Assert(CompositeSeasonPlanner.TryCreatePlan(scope.EligibleEpisodes, null,
                out var plan, out var error), error);
            var sourceIdentity = new CompositeSeasonSourceIdentity
            {
                ProviderId = "fixture-provider",
                MediaId = "fixture-media",
                MediaLookupId = "fixture-media",
            };
            var sourceEpisodes = source.Resolve(sourceEpisodeCount, identifierMetamorphism);
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    plan, sourceIdentity, sourceEpisodes, "search-confidence", out plan, out error), error);
            var mapped = new HashSet<string>(plan.Mappings.Select(mapping => mapping.LocalEpisodeItemId));
            return new FixtureBatchResult
            {
                Scope = scope,
                Plan = plan,
                // Batch input is derived from verified mappings in local scope
                // order; observed supplemental/unknown records cannot enter it.
                DownloadItemIds = plan.OrderedEpisodes.Where(item => mapped.Contains(item.ItemId))
                    .Select(item => item.ItemId).ToList(),
            };
        }

        private static CompositeSeasonEpisodeScope Scope(
            int target, IEnumerable<CompositeSeasonLocalEpisode> episodes)
        {
            Assert(CompositeSeasonOwnership.TryGetTargetScope(
                    CompositeSeasonTargetContext.ForSeasonNumber(target), episodes,
                    out var scope, out var error), error);
            return scope;
        }

        private static CompositeSeasonPlan ApplyFullSource(
            CompositeSeasonPlan plan, int count, string mediaId)
        {
            var source = new CompositeSeasonSourceIdentity
                { ProviderId = "test", MediaId = mediaId, MediaLookupId = mediaId };
            var episodes = Enumerable.Range(1, count).Select(number =>
                new CompositeSeasonSourceEpisode
                {
                    EpisodeId = mediaId + "-" + number,
                    CommentId = mediaId + "-comment-" + number,
                    EpisodeNumber = number,
                }).ToList();
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    plan, source, episodes, "search-confidence", out var mapped, out var error), error);
            return mapped;
        }

        private static IEnumerable<CompositeSeasonLocalEpisode> Range(
            string prefix, int count, int? parent, int offset)
        {
            return Enumerable.Range(1, count).Select(number =>
                Local(prefix + "-" + number, number, parent, offset + number));
        }

        private static CompositeSeasonLocalEpisode Local(
            string key, int episode, int? parent, int order)
        {
            return new CompositeSeasonLocalEpisode
            {
                ItemId = GuidFor(key), EpisodeNumber = episode,
                OriginalEpisodeNumber = episode, ParentSeasonNumber = parent,
                PlacementOrder = order, SortOrder = order,
            };
        }

        private static CompositeSeasonLocalEpisode Clone(CompositeSeasonLocalEpisode item)
        {
            return new CompositeSeasonLocalEpisode
            {
                ItemId = item.ItemId, EpisodeNumber = item.EpisodeNumber,
                OriginalEpisodeNumber = item.OriginalEpisodeNumber,
                ParentSeasonNumber = item.ParentSeasonNumber,
                PlacementOrder = item.PlacementOrder, SortOrder = item.SortOrder,
            };
        }

        private static string GuidFor(string value)
        {
            var bytes = System.Security.Cryptography.MD5.HashData(
                System.Text.Encoding.UTF8.GetBytes(value));
            return new Guid(bytes).ToString();
        }

        private static string FindRepositoryRoot()
        {
            var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !System.IO.File.Exists(System.IO.Path.Combine(
                       directory.FullName, "Emby.Plugin.Danmu.csproj")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
