using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class R4GenerationPolicyTests
    {
        public static void Run()
        {
            RejectsLegacyBatchOrigins();
            GuardsPlanGenerations();
            FencesQueuedCompositeWritesWhenPreviewIsSuperseded();
            CommitsOnlyCompleteCurrentSingleSource();
            EnforcesR4CoordinatorPaths();
        }

        private static void RejectsLegacyBatchOrigins()
        {
            Assert(DanmuMappingProtocol.IsCurrent(22) && !DanmuMappingProtocol.IsCurrent(21) &&
                   !DanmuMappingProtocol.IsCurrent(20),
                "V22 must reject cached V21 and older mapping drafts");
            Assert(DanmuCompositeAlignmentIntentWire.TryParse(
                       DanmuCompositeAlignmentIntentWire.DefaultZeroOffset, out var defaultIntent) &&
                   defaultIntent == CompositeSeasonAlignmentIntent.DefaultZeroOffset &&
                   DanmuCompositeAlignmentIntentWire.TryParse(
                       DanmuCompositeAlignmentIntentWire.ExplicitAnchor, out var explicitIntent) &&
                   explicitIntent == CompositeSeasonAlignmentIntent.ExplicitAnchor &&
                   !DanmuCompositeAlignmentIntentWire.TryParse(string.Empty, out _) &&
                   !DanmuCompositeAlignmentIntentWire.TryParse("defaultzerooffset", out _) &&
                   !DanmuCompositeAlignmentIntentWire.TryParse("0", out _),
                "V22 alignment intent must be present and closed to the two exact wire strings");
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

        private static void FencesQueuedCompositeWritesWhenPreviewIsSuperseded()
        {
            var gate = typeof(Emby.Plugin.Danmu.Core.Controllers.DanmuController).GetMethod(
                "IsCompositePlanExecutionCurrent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(gate != null, "composite execution must expose one generation/fingerprint gate");

            const string seasonId = "season-write-fence";
            const string fingerprint = "fingerprint-v22";

            var beforeFirstWriteCoordinator = new SeasonPlanGenerationCoordinator();
            var beforeFirstWriteGeneration = beforeFirstWriteCoordinator.Begin(seasonId);
            var beforeFirstWriteFingerprints = new ConcurrentDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            beforeFirstWriteFingerprints[seasonId + "\u001f" + beforeFirstWriteGeneration] = fingerprint;
            Assert(!InvokeCompositeWriteGate(gate, beforeFirstWriteCoordinator,
                    beforeFirstWriteFingerprints, seasonId, beforeFirstWriteGeneration,
                    "changed-fingerprint"),
                "a changed fingerprint must fail closed even while the operation generation is current");
            beforeFirstWriteCoordinator.Begin(seasonId);
            var writesBeforeFirst = 0;
            if (InvokeCompositeWriteGate(gate, beforeFirstWriteCoordinator,
                    beforeFirstWriteFingerprints, seasonId, beforeFirstWriteGeneration, fingerprint))
            {
                writesBeforeFirst++;
            }
            Assert(writesBeforeFirst == 0,
                "a new preview before the first write must leave the superseded task at zero writes");

            var betweenEpisodesCoordinator = new SeasonPlanGenerationCoordinator();
            var betweenEpisodesGeneration = betweenEpisodesCoordinator.Begin(seasonId);
            var betweenEpisodesFingerprints = new ConcurrentDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            betweenEpisodesFingerprints[seasonId + "\u001f" + betweenEpisodesGeneration] = fingerprint;
            var completedWrites = 0;
            if (InvokeCompositeWriteGate(gate, betweenEpisodesCoordinator,
                    betweenEpisodesFingerprints, seasonId, betweenEpisodesGeneration, fingerprint))
            {
                completedWrites++;
            }
            betweenEpisodesCoordinator.Begin(seasonId);
            if (InvokeCompositeWriteGate(gate, betweenEpisodesCoordinator,
                    betweenEpisodesFingerprints, seasonId, betweenEpisodesGeneration, fingerprint))
            {
                completedWrites++;
            }
            Assert(completedWrites == 1,
                "superseding between Episodes must preserve the already completed write but fence every later write");
        }

        private static bool InvokeCompositeWriteGate(
            MethodInfo gate,
            SeasonPlanGenerationCoordinator coordinator,
            ConcurrentDictionary<string, string> fingerprints,
            string seasonId,
            long generation,
            string fingerprint)
        {
            return (bool)gate.Invoke(null, new object[]
            {
                coordinator, fingerprints, seasonId, generation, fingerprint,
            });
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
            Assert(controller.Contains("CompositeSeasonPlanner.TryApplySegmentResolved(") &&
                   controller.Contains("AlignmentIntent = alignmentIntent") &&
                   controller.Contains("SourceStartEpisodeNumber = selection.SourceStartEpisodeNumber") &&
                   controller.Contains("ApplyGroupAlignmentIntents(response.CompositeGroups, build.Selections)") &&
                   controller.Contains("group.AlignmentIntent = selection?.AlignmentIntent") &&
                   !controller.Contains("ElementAtOrDefault(requestedSourceNumber - 1)"),
                "initial and supplemental selections must share the resolver, echo authoritative intent, and never reinterpret a source number as an ordinal");
            Assert(controller.Contains("SeasonTargetPlanningCoordinator.TryBuild") &&
                   controller.Contains("CommitSeasonDisplayMirrorAfterTerminalAsync(season, task)"),
                "download/retry must share ownership and terminal mirror fences");
            var compositeStart = controller.IndexOf(
                "private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload",
                StringComparison.Ordinal);
            var compositeEnd = controller.IndexOf(
                "private async Task<DanmuDownloadTaskResult> StartTrackedMovieDownload",
                compositeStart, StringComparison.Ordinal);
            var compositeBody = compositeStart >= 0 && compositeEnd > compositeStart
                ? controller.Substring(compositeStart, compositeEnd - compositeStart)
                : string.Empty;
            Assert(Count(compositeBody, "EnsureCompositePlanExecutionCurrent(task, season.Id.ToString())") >= 8 &&
                   compositeBody.Contains("catch (StaleCompositePlanException)") &&
                   (compositeBody.IndexOf("EnsureCompositePlanExecutionCurrent(task, season.Id.ToString());\r\n                            var outcome", StringComparison.Ordinal) >= 0 ||
                    compositeBody.IndexOf("EnsureCompositePlanExecutionCurrent(task, season.Id.ToString());\n                            var outcome", StringComparison.Ordinal) >= 0),
                "composite execution must recheck generation/fingerprint around source resolution, each XML download, ProviderId persistence, and terminal mirror work");
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

        private static int Count(string value, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
