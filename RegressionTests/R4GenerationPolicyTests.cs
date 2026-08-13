using System;
using System.IO;
using Emby.Plugin.Danmu.Core;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class R4GenerationPolicyTests
    {
        public static void Run()
        {
            RejectsLegacyBatchOrigins();
            GuardsPlanGenerations();
            CommitsOnlyCompleteCurrentSingleSource();
            EnforcesR4CoordinatorPaths();
        }

        private static void RejectsLegacyBatchOrigins()
        {
            Assert(DanmuMappingProtocol.IsCurrent(20) && !DanmuMappingProtocol.IsCurrent(19),
                "r4 protocol must reject cached V19/r3 drafts");
            foreach (var origin in new[] { "episode-provider-id", "exact-binding", "binding", "direct" })
                Assert(!DanmuMappingProtocol.IsAllowedBatchOrigin(origin), "legacy local-id origin must be rejected: " + origin);
            Assert(DanmuMappingProtocol.IsAllowedBatchOrigin("manual") &&
                   DanmuMappingProtocol.IsAllowedBatchOrigin("scored"),
                "current explicit selections must remain accepted");
        }

        private static void GuardsPlanGenerations()
        {
            var coordinator = new SeasonPlanGenerationCoordinator();
            var first = coordinator.Begin("season-1");
            Assert(coordinator.IsCurrent("season-1", first), "new plan generation must be current");
            var second = coordinator.Begin("season-1");
            Assert(!coordinator.IsCurrent("season-1", first) && coordinator.IsCurrent("season-1", second),
                "new preview/rematch must supersede the old generation");
        }

        private static void CommitsOnlyCompleteCurrentSingleSource()
        {
            var complete = new SeasonDisplayMirrorCommit
            {
                Generation = 7, ProviderId = "DandanID", CanonicalMediaId = "7532",
                EligibleEpisodeCount = 13, MappedEpisodeCount = 13, TerminalEpisodeCount = 13,
                AcceptedEpisodeCount = 13, StableSourceCount = 1, HasCanonicalSeasonIdentity = true,
            };
            Assert(SeasonDisplayMirrorPolicy.CanCommit(complete, out _),
                "complete current single-source task must be mirror eligible");
            complete.HasUnmatchedEpisodes = true;
            Assert(!SeasonDisplayMirrorPolicy.CanCommit(complete, out _), "partial task must be no-op");
            complete.HasUnmatchedEpisodes = false; complete.StableSourceCount = 2;
            Assert(!SeasonDisplayMirrorPolicy.CanCommit(complete, out _), "multi-source task must be no-op");
            complete.StableSourceCount = 1; complete.Failed = true;
            Assert(!SeasonDisplayMirrorPolicy.CanCommit(complete, out _), "failed task must be no-op");
            complete.Failed = false; complete.Cancelled = true;
            Assert(!SeasonDisplayMirrorPolicy.CanCommit(complete, out _), "cancelled task must be no-op");
            complete.Cancelled = false; complete.StaleStructure = true;
            Assert(!SeasonDisplayMirrorPolicy.CanCommit(complete, out _), "stale task must be no-op");
        }

        private static void EnforcesR4CoordinatorPaths()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var library = File.ReadAllText(Path.Combine(root, "LibraryManagerEventsHelper.cs"));
            Assert(controller.Contains("TryApplyRemainingOwningSourceEpisodes") &&
                   controller.Contains("else if (!CompositeSeasonPlanner.TryApplySegment"),
                "initial owning and temporary supplemental selections must use distinct planner paths");
            Assert(controller.Contains("SeasonTargetPlanningCoordinator.TryBuild") &&
                   controller.Contains("CommitSeasonDisplayMirrorAfterTerminalAsync(season, task)"),
                "download/retry must share ownership and terminal mirror fences");
            Assert(library.Contains("if (!(item is Season) && item.HasAnyDanmuProviderIds())") &&
                   library.Contains("TryBuildAutomaticPlanningContext"),
                "automatic Season handling must ignore identifiers and use ownership-filtered planning");
            Assert(!controller.Contains("IndexNumber.Value != 0"),
                "Season fallback resolution must retain Season 0 targets");
        }

        private static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
