using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;

namespace Emby.Plugin.Danmu.TemporaryRangePolicyRegression
{
    internal static class Program
    {
        private static int Main()
        {
            AcceptsOnlyTheEntireCurrentUnmatchedRun();
            UsesExplicitThenSeriesThenSeasonKeywordsWithoutEmptyCalls();
            VerifiesControllerRangeAndForcedSearchContracts();
            Console.WriteLine("Temporary-range policy regression checks passed.");
            return 0;
        }

        private static void AcceptsOnlyTheEntireCurrentUnmatchedRun()
        {
            var local = Enumerable.Range(1, 6)
                .Select(number => new CompositeSeasonLocalEpisode
                {
                    ItemId = "local-" + number,
                    EpisodeNumber = number,
                    SortOrder = number,
                })
                .ToList();
            var direct = new List<CompositeSeasonEpisodeMapping>
            {
                Mapping("local-1", "source-1"),
                Mapping("local-2", "source-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(
                    local,
                    direct,
                    null,
                    new[] { "local-1", "local-2" },
                    false,
                    out var plan,
                    out var error), error);
            var originalRun = plan.UnmatchedRuns.Single().Episodes.Select(episode => episode.ItemId).ToArray();
            var originalExclusions = plan.EffectiveExcludedLocalEpisodeItemIds.ToArray();

            Assert(DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                    plan, "local-1", 6, out var range, out error) &&
                   range.Episodes.Select(episode => episode.ItemId).SequenceEqual(originalRun),
                "a temporary search must accept precisely the one complete exclusion-aware unmatched run");
            Assert(!DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                    plan, "local-2", 5, out _, out _) &&
                   !DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                    plan, "local-1", 2, out _, out _),
                "a middle start or shortened count must be rejected rather than silently narrowing the draft");
            Assert(plan.UnmatchedRuns.Single().Episodes.Select(episode => episode.ItemId).SequenceEqual(originalRun) &&
                   plan.EffectiveExcludedLocalEpisodeItemIds.SequenceEqual(originalExclusions),
                "rejected temporary intent must not mutate the authoritative draft plan");
        }

        private static CompositeSeasonEpisodeMapping Mapping(string localItemId, string sourceEpisodeId)
        {
            return new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = localItemId,
                Source = new CompositeSeasonSourceIdentity { ProviderId = "test", MediaId = "source" },
                SourceEpisodeId = sourceEpisodeId,
                CommentId = "comment-" + sourceEpisodeId,
                SourceEpisodeNumber = int.Parse(sourceEpisodeId.Substring("source-".Length)),
                Origin = "direct",
            };
        }

        private static void UsesExplicitThenSeriesThenSeasonKeywordsWithoutEmptyCalls()
        {
            Assert(DanmuTemporaryRangeSearchPolicy.TryResolveSearchKeyword(
                       " edited input ", "Series", "Season", out var keyword) && keyword == "edited input",
                "an edited temporary keyword must remain explicit");
            Assert(DanmuTemporaryRangeSearchPolicy.TryResolveSearchKeyword(
                       "", "Series", "Season", out keyword) && keyword == "Series",
                "a default temporary query must use the Series title first");
            Assert(DanmuTemporaryRangeSearchPolicy.TryResolveSearchKeyword(
                       "", "", "Season", out keyword) && keyword == "Season",
                "a missing Series title must fall back to the Season title");
            Assert(!DanmuTemporaryRangeSearchPolicy.TryResolveSearchKeyword(
                       "", "", "", out keyword) && string.IsNullOrEmpty(keyword),
                "two empty titles must be retryable input rather than an empty provider query");
        }

        private static void VerifiesControllerRangeAndForcedSearchContracts()
        {
            var controller = File.ReadAllText(Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory),
                "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var temporaryMethodStart = controller.IndexOf("private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview", StringComparison.Ordinal);
            var temporaryMethodEnd = controller.IndexOf("private async Task PopulateCompositePreviewIfRequired", temporaryMethodStart, StringComparison.Ordinal);
            var temporaryMethod = controller.Substring(temporaryMethodStart, temporaryMethodEnd - temporaryMethodStart);
            Assert(temporaryMethod.Contains("DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun", StringComparison.Ordinal) &&
                   temporaryMethod.Contains("range.Episodes.Count", StringComparison.Ordinal) &&
                   temporaryMethod.Contains("temporary-range-keyword-required", StringComparison.Ordinal) &&
                   temporaryMethod.Contains("search-incomplete", StringComparison.Ordinal) &&
                   !temporaryMethod.Contains("DanmuProviderIdResolver", StringComparison.Ordinal) &&
                   !temporaryMethod.Contains("TryGetSavedManualBinding", StringComparison.Ordinal),
                "temporary-range searches must validate the full run, use its length, return retry diagnostics, and bypass persistent Season hints");
            Assert(controller.Contains("GetMovieMatchPreview(\n                    movie,\n                    request.Keyword,\n                    rematch", StringComparison.Ordinal) &&
                   controller.Contains("GetEpisodeMatchPreview(\n                    episode,\n                    request.Keyword,\n                    rematch", StringComparison.Ordinal) &&
                   controller.Contains("if (!forceSearch)", StringComparison.Ordinal) &&
                   controller.Contains("TryGetSavedManualBinding(\n                    forceSearch", StringComparison.Ordinal) &&
                   controller.Contains("SeasonTargetPlanningCoordinator.TryBuild", StringComparison.Ordinal) &&
                   !controller.Contains("TryGetSavedManualBinding(\n                    effectiveForceSearch", StringComparison.Ordinal),
                "forced Movie and Episode previews must preserve their exact-identifier bypass while r5 Season planning stays identifier-free behind the shared target coordinator");
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Emby.Plugin.Danmu.csproj")))
                {
                    return current.FullName;
                }
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Unable to locate the plugin repository root.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
