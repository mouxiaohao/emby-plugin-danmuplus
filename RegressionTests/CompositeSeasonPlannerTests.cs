using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Controllers;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Dandan;
using Emby.Plugin.Danmu.Scraper.Entity;
using DandanEpisode = Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class CompositeSeasonPlannerTests
    {
        public static void Run()
        {
            PreservesExplicitEvidenceAndBuildsRemainingRuns();
            SupportsSourceStartsAndPartialCoverage();
            MapsFrierenThirtyEightEpisodesAcrossTwoUpstreamSeasons();
            KeepsMarkedFrierenEpisodeEvidenceWhenNoCandidateIsUsable();
            KeepsMarkedPreviewDirectEvidenceAheadOfFreshSearch();
            ContinuesPrimaryAcrossInteriorExactEpisode();
            SelectsSupplementalAfterPrimaryExhaustion();
            ContinuesSupplementalAcrossSpecialAndReentrantDirectEvidence();
            ParsesCompositeSelectionsFromScalarQueryJson();
            SupportsCompositeMappingForAnyLocalSeason();
            DoesNotClassifySingleSourcePartialCoverageAsComposite();
            MapsTwentyFiveEpisodePartSourcesWithBindingSafety();
            CoordinatesSingletonAndSeriesTargetSetsIdentically();
            MapsMultipleSpecialRunsWithoutChangingLocalSeasonMembership();
            SeparatesCanonicalMediaIdentityFromLookupToken();
            RejectsOverlapsAndUnverifiedMappings();
            IdentifiesCompositeSourcesByProviderAndMediaId();
            KeepsDirectEpisodeEvidenceFromFalselyCreatingCompositeSources();
            SortsByStableLocalIdentityWithoutDependingOnDisplayNumbers();
            ValidatesAndAppliesAuthoritativeExclusionsBeforeDirectEvidence();
            SplitsEditableRunsAndRestoresOnlyTheRequestedRange();
            RetainsCompositeSafetyForSubsetAndZeroPersist();
            RebuildsPreviewAndDownloadFromTheSameExclusionAwarePlan();
            RejectsIncompleteAutomaticSeasonAndResidualSearches();
            PreservesDirectMetadataAcrossRemoveReplacementAndRestore();
            RetainsCompositeSafetyWhenReplacementCollapsesToOneSource();
            RejectsForeignAndStaleTemporaryRangesWithoutMutatingThePlan();
            VerifiesControllerParityMetadataAndDialogResetContracts();
            PreservesServerCandidateScoreAcrossOwningPlansAndGroups();
            PreservesExactBindingScoreIntoSelectedCandidate();
        }

        private static void PreservesExplicitEvidenceAndBuildsRemainingRuns()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "frieren-s1", "source-1", "comment-1"),
                Mapping("local-2", "DandanID", "frieren-s1", "source-2", "comment-2"),
                Mapping("local-4", "DandanID", "frieren-s1", "source-4", "comment-4"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, out var plan, out var error), error);
            Assert(plan.Mappings.Count == 3 && plan.Mappings.All(mapping => mapping.Origin == "direct"),
                "explicit/direct mappings must remain the planning baseline");
            Assert(plan.UnmatchedRuns.Count == 2 && RunIds(plan.UnmatchedRuns[0]) == "local-3" &&
                   RunIds(plan.UnmatchedRuns[1]) == "local-5",
                "mapped episodes must split gaps into maximum contiguous runs");

            var request = Segment("local-3", "DandanID", "frieren-ova", "ova-1", new[] { Source("ova-1", "ova-comment", 1) });
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, request, out plan, out var applied, out error), error);
            Assert(applied == 1 && plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-3").Source.MediaId == "frieren-ova",
                "a manual mapping must only fill its selected unmatched run");
            Assert(plan.UnmatchedRuns.Count == 1 && RunIds(plan.UnmatchedRuns[0]) == "local-5",
                "remaining gaps must be recomputed after every mapping");
        }

        private static void SupportsSourceStartsAndPartialCoverage()
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 38), null, out var plan, out var error), error);
            var request = Segment("local-29", "DandanID", "frieren-s2", "source-4", new[]
            {
                Source("source-1", "comment-1", 1), Source("source-2", "comment-2", 2),
                Source("source-3", "comment-3", 3), Source("source-4", "comment-4", 4),
                Source("source-5", "comment-5", 5), Source("source-6", "comment-6", 6),
            }, 10);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, request, out plan, out var applied, out error), error);
            Assert(applied == 3 && plan.Mappings[0].LocalEpisodeItemId == "local-29" &&
                   plan.Mappings[0].SourceEpisodeId == "source-4" && RunIds(plan.UnmatchedRuns.Single()) ==
                   "local-32,local-33,local-34,local-35,local-36,local-37,local-38",
                "a short source must map only its verified prefix from the selected source start");

            var fill = Segment("local-32", "DandanID", "frieren-s2b", "part-1", Enumerable.Range(1, 8)
                .Select(number => Source("part-" + number, "part-comment-" + number, number)));
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, fill, out plan, out applied, out error), error);
            Assert(applied == 7 && plan.UnmatchedRuns.Count == 0,
                "a long source must not overflow the selected local run");
        }

        private static void MapsFrierenThirtyEightEpisodesAcrossTwoUpstreamSeasons()
        {
            var directS1 = Enumerable.Range(1, 28).Select(number => new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = "local-" + number,
                Source = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "frieren-s1" },
                SourceEpisodeId = "s1-" + number,
                CommentId = "s1-comment-" + number,
                SourceEpisodeNumber = number,
                Origin = "episode-provider-id",
            });
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 38), directS1, out var plan, out var error), error);
            Assert(plan.Mappings.Count == 28 && RunIds(plan.UnmatchedRuns.Single()) ==
                   string.Join(",", Enumerable.Range(29, 10).Select(number => "local-" + number)),
                "direct Frieren S1 E1-E28 evidence must leave E29-E38 as one temporary group");

            var secondSeason = Segment("local-29", "DandanID", "frieren-s2", "s2-1",
                Enumerable.Range(1, 10).Select(number => Source("s2-" + number, "s2-comment-" + number, number)));
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, secondSeason, out plan, out var applied, out error), error);
            Assert(applied == 10 && plan.IsComposite && plan.UnmatchedRuns.Count == 0 && plan.Mappings.Count == 38,
                "Frieren 38 = upstream S1(28) + S2(10) must become a complete two-source plan without a temporary group");
            Assert(plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").Source.MediaId == "frieren-s2" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").SourceEpisodeNumber == 1,
                "the second local range must start at the independently verified S2 E1, not continue S1 numbering");
        }

        private static void KeepsMarkedFrierenEpisodeEvidenceWhenNoCandidateIsUsable()
        {
            // This is the restart/ambiguous-search safety case: every local
            // Episode already has an exact Dandan binding, but the Season is
            // marked composite and no automatic candidate may be selected.
            // The planner must therefore retain direct evidence instead of
            // emitting 38 temporary/unmatched episodes.
            var direct = new List<CompositeSeasonEpisodeMapping>();
            foreach (var number in Enumerable.Range(1, 28))
            {
                direct.Add(DirectDandanMapping(number, "17617", "17617" + number.ToString("0000")));
            }
            foreach (var number in Enumerable.Range(1, 10))
            {
                direct.Add(DirectDandanMapping(28 + number, "18886", "18886" + number.ToString("0000")));
            }

            Assert(DandanEpisodeId.TryGetAnimeId("176170001", out var firstParent) && firstParent == 17617 &&
                   DandanEpisodeId.TryGetAnimeId("188860010", out var secondParent) && secondParent == 18886 &&
                   !DandanEpisodeId.TryGetAnimeId("18886x001", out _) &&
                   !DandanEpisodeId.TryGetAnimeId("1888", out _),
                "Dandan Episode ProviderIds must derive only their candidate parent 17617/18886 from a strict numeric four-digit suffix");
            var verified = DandanEpisodeId.CreateVerifiedEpisode("188860010", new[]
            {
                new DandanEpisode { EpisodeId = 188860009, EpisodeTitle = "E09", EpisodeNumber = "9" },
                new DandanEpisode { EpisodeId = 188860010, EpisodeTitle = "E10", EpisodeNumber = "10" },
            });
            Assert(verified?.Id == "188860010" && verified.CommentId == "188860010" &&
                   verified.ParentMediaId == "18886" && verified.EpisodeNumber == 10 &&
                   DandanEpisodeId.CreateVerifiedEpisode("188860010", new[]
                   {
                       new DandanEpisode { EpisodeId = 176170010, EpisodeTitle = "wrong parent", EpisodeNumber = "10" },
                   }) == null,
                "a Dandan direct Episode mapping must be created only from the exact full EpisodeId returned by its parent detail");

            var firstResolver = new DirectEpisodeFakeScraper("DandanID", new ScraperEpisode
            {
                Id = "176170001", CommentId = "176170001", ParentMediaId = "17617", EpisodeNumber = 1,
            });
            var secondResolver = new DirectEpisodeFakeScraper("DandanID", new ScraperEpisode
            {
                Id = "188860001", CommentId = "188860001", ParentMediaId = "18886", EpisodeNumber = 1,
            });
            var firstMedia = DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                firstResolver, new Episode { IndexNumber = 1 }, "176170001", 1).GetAwaiter().GetResult();
            var secondMedia = DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                secondResolver, new Episode { IndexNumber = 29 }, "188860001", 1).GetAwaiter().GetResult();
            var firstMapping = CompositeSeasonMatchService.CreateDirectMapping("local-1", "DandanID", firstMedia, "176170001");
            var secondMapping = CompositeSeasonMatchService.CreateDirectMapping("local-29", "DandanID", secondMedia, "188860001");
            Assert(firstMapping.Source.MediaId == "17617" && firstMapping.Source.MediaLookupId == "176170001" &&
                   secondMapping.Source.MediaId == "18886" && secondMapping.Source.MediaLookupId == "188860001" &&
                   firstResolver.MediaCalls == 0 && secondResolver.MediaCalls == 0 &&
                   firstResolver.MediaEpisodeCalls == 1 && secondResolver.MediaEpisodeCalls == 1,
                "direct Episode resolution must preserve the exact EpisodeId lookup token while exposing the verified parent AnimeId as canonical source identity");
            Assert(CompositeSeasonPlanner.TryCreatePlan(new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "local-1", SortOrder = 1 },
                new CompositeSeasonLocalEpisode { ItemId = "local-29", SortOrder = 2 },
            }, new[] { firstMapping, secondMapping }, out var directPlan, out var directError) && directPlan.IsComposite,
                "two direct Dandan Episodes with parents 17617 and 18886 must produce distinct stable composite sources; " + directError);

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 38), direct, out var plan, out var error), error);
            var stableGroups = plan.Mappings
                .GroupBy(mapping => mapping.Source)
                .OrderBy(group => group.Key.MediaId, StringComparer.Ordinal)
                .ToList();
            Assert(plan.Mappings.Count == 38 && plan.UnmatchedRuns.Count == 0 && plan.IsComposite &&
                   stableGroups.Count == 2 && stableGroups.Single(group => group.Key.MediaId == "17617").Count() == 28 &&
                   stableGroups.Single(group => group.Key.MediaId == "18886").Count() == 10,
                "a marked 38-episode Frieren Season must keep all exact Episode DandanIDs as 17617(28)+18886(10), " +
                "be composite, and never regress to an all-unmatched preview when candidate search is absent or ambiguous");
            Assert(plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").Source.MediaId == "18886" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").SourceEpisodeId == "188860001",
                "the restart preview must preserve the exact S2 parent and episode identity for local E29");
        }

        private static void KeepsMarkedPreviewDirectEvidenceAheadOfFreshSearch()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            Assert(!controller.Contains("compositeMarked") &&
                    !controller.Contains("BuildCompositePlanAsync(latest, null, true") &&
                    controller.Contains("InitializeDecision(result, scrapers, true);"),
                "r4 Season preview must ignore durable markers and local Episode IDs and begin from a fresh explicit plan");
        }

        private static void SupportsCompositeMappingForAnyLocalSeason()
        {
            // The behavior must not be coupled to Season 1: this represents a
            // later local season containing an upstream continuation and an OVA.
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 15), null, out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "BilibiliID", "later-s3", "s3-1",
                    Enumerable.Range(1, 12).Select(number => Source("s3-" + number, "c3-" + number, number))),
                out plan, out var applied, out error), error);
            Assert(applied == 12 && RunIds(plan.UnmatchedRuns.Single()) == "local-13,local-14,local-15",
                "a non-first local season must expose its own remaining temporary group");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-13", "BilibiliID", "later-special", "sp-1",
                    Enumerable.Range(1, 3).Select(number => Source("sp-" + number, "spc-" + number, number))),
                out plan, out applied, out error), error);
            Assert(applied == 3 && plan.IsComposite && plan.UnmatchedRuns.Count == 0,
                "a later local season must be able to complete with an independently selected special");
        }

        private static void ContinuesPrimaryAcrossInteriorExactEpisode()
        {
            var direct = new[] { Mapping("local-13", "DandanID", "direct-episode-provider:DandanID", "s1-13", "c13") };
            direct[0].Origin = "episode-provider-id";
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 38), direct, out var plan, out var error), error);
            var primary = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "s1", MediaLookupId = "s1-lookup" };
            var allPrimaryEpisodes = Enumerable.Range(1, 28).Select(x => Source("s1-" + x, "c" + x, x));
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, primary, allPrimaryEpisodes,
                "automatic-primary", out plan, out var exhausted, out error), error);
            Assert(plan.UnmatchedRuns.Single().Episodes[0].ItemId == "local-29" &&
                   exhausted && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-14").SourceEpisodeId == "s1-14",
                "an interior exact primary episode must continue S1 in order and exhaust it before the residual run");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-29", "DandanID", "s2", "s2-1",
                    Enumerable.Range(1, 10).Select(x => Source("s2-" + x, "s2c" + x, x))),
                out plan, out _, out error), error);
            Assert(plan.IsComposite && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-29").SourceEpisodeId == "s2-1",
                "after the primary is exhausted, the residual must begin at verified S2 E1");
        }

        private static void SelectsSupplementalAfterPrimaryExhaustion()
        {
            var primary = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "s1", MediaLookupId = "s1-lookup" };
            var supplemental = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "s2", MediaLookupId = "s2-lookup" };
            var candidates = new List<DanmuMatchCandidate>
            {
                new DanmuMatchCandidate { Id = "s1-lookup", Site = "DandanID", SourceOrder = 0, Score = 0.99 },
                new DanmuMatchCandidate { Id = "s2-lookup", Site = "DandanID", SourceOrder = 0, Score = 0.95 },
                new DanmuMatchCandidate { Id = "s3-lookup", Site = "DandanID", SourceOrder = 0, Score = 0.93 },
            };
            Assert(CompositeSeasonMatchService.SelectSupplementalCandidate(candidates, Enumerable.Empty<CompositeSeasonSourceIdentity>())?.Id == "s1-lookup" &&
                   CompositeSeasonMatchService.SelectSupplementalCandidate(candidates, new[] { primary })?.Id == "s2-lookup" &&
                   CompositeSeasonMatchService.SelectSupplementalCandidate(candidates, new[] { primary, supplemental })?.Id == "s3-lookup",
                "all exhausted sources must be filtered before unique high-confidence supplemental selection");
        }

        private static void ContinuesSupplementalAcrossSpecialAndReentrantDirectEvidence()
        {
            var primary = Enumerable.Range(1, 28).Select(number => Mapping("local-" + number,
                "DandanID", "s1", "s1-" + number, "s1c-" + number)).ToList();
            primary.Add(Mapping("local-34", "DandanID", "special", "sp-1", "spc-1"));
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 39), primary, out var plan, out var error), error);
            var secondSource = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "s2", MediaLookupId = "s2-lookup",
            };
            var secondEpisodes = Enumerable.Range(1, 10).Select(number => Source("s2-" + number, "s2c-" + number, number));
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, secondSource, secondEpisodes,
                "automatic-residual", out plan, out var exhausted, out error), error);
            Assert(exhausted && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-35").SourceEpisodeId == "s2-6" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-35").SourceEpisodeNumber == 6,
                "a supplemental source must continue across an intervening direct special instead of restarting at E1");

            var reentrantDirect = Enumerable.Range(1, 28).Select(number => Mapping("local-" + number,
                "DandanID", "s1", "s1-" + number, "s1c-" + number)).ToList();
            reentrantDirect.Add(Mapping("local-34", "DandanID", "special", "sp-1", "spc-1"));
            foreach (var number in Enumerable.Range(1, 5))
            {
                var direct = Mapping("local-" + (28 + number), "DandanID",
                    "direct-episode-provider:DandanID", "s2-" + number, "s2c-" + number);
                direct.Origin = "episode-provider-id";
                direct.Source.MediaLookupId = "s2-direct-" + number;
                reentrantDirect.Add(direct);
            }
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 39), reentrantDirect, out plan, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, secondSource, secondEpisodes,
                "automatic-residual", out plan, out exhausted, out error), error);
            Assert(exhausted && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-29").Source.MediaId == "s2" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-29").Source.MediaLookupId == "s2-direct-1" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-35").SourceEpisodeId == "s2-6",
                "re-entry must normalize direct S2 placeholders, preserve their lookup tokens, and resume at S2 E6 after the special");
        }

        private static void ParsesCompositeSelectionsFromScalarQueryJson()
        {
            var property = typeof(DanmuParams).GetProperty("CompositeSelections");
            var member = property?.GetCustomAttribute<DataMemberAttribute>();
            Assert(property?.PropertyType == typeof(string) && property.Name == "CompositeSelections" &&
                   member?.Name == "compositeSelections",
                "the Emby GET-bound compositeSelections CLR/DataMember contract must remain one scalar JSON string");
            Assert(typeof(DanmuParams).GetProperty("ParsedCompositeSelections")?.PropertyType ==
                   typeof(List<DanmuCompositeSeasonSelection>) &&
                   typeof(DanmuParams).GetProperty("ParsedCompositeSelections")
                       .GetCustomAttribute<IgnoreDataMemberAttribute>() != null,
                "the parsed runtime selections must not be exposed to the Emby GET binder");

            const string frontendPayload = "[{\"LocalStartEpisodeItemId\":\"episode-29\",\"RequestedEpisodeCount\":10,\"Site\":\"DandanID\",\"CandidateId\":\"frieren-s2\",\"SourceStartEpisodeId\":\"s2-1\",\"SourceStartEpisodeNumber\":1,\"MatchOrigin\":\"manual\"}]";
            Assert(DanmuCompositeSeasonSelectionJson.TryParse(frontendPayload, out var parsed, out var error) &&
                   parsed.Count == 1 && parsed[0].CandidateId == "frieren-s2" &&
                   parsed[0].RequestedEpisodeCount == 10 && string.IsNullOrEmpty(error),
                "the frontend JSON.stringify payload must deserialize into a compact composite selection");
            Assert(DanmuCompositeSeasonSelectionJson.TryParse("[]", out parsed, out error) && parsed.Count == 0,
                "an empty array must remain compatible with direct-only composite plans");
            Assert(!DanmuCompositeSeasonSelectionJson.TryParse("{not-json", out parsed, out error) &&
                   parsed.Count == 0 && !string.IsNullOrWhiteSpace(error),
                "malformed composite JSON must be safely rejected with a readable error");
        }

        private static void DoesNotClassifySingleSourcePartialCoverageAsComposite()
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 12), null, out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "DandanID", "one-source", "one-1",
                    Enumerable.Range(1, 8).Select(number => Source("one-" + number, "one-comment-" + number, number))),
                out plan, out var applied, out error), error);
            Assert(applied == 8 && !plan.IsComposite && plan.SeasonBindingUnsafe &&
                   !plan.CanPersistCompleteSeasonBinding &&
                   RunIds(plan.UnmatchedRuns.Single()) == "local-9,local-10,local-11,local-12",
                "a partial one-source plan is not composite, but must still block and clear a stale complete Season binding after persistence");
        }

        private static void MapsTwentyFiveEpisodePartSourcesWithBindingSafety()
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 25), null,
                out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "DandanID", "spy-family-part-1", "part1-1",
                    Enumerable.Range(1, 12).Select(number =>
                        Source("part1-" + number, "part1-comment-" + number, number))),
                out plan, out var applied, out error), error);
            Assert(applied == 12 && plan.Mappings.Count == 12 &&
                   RunIds(plan.UnmatchedRuns.Single()) == string.Join(",",
                       Enumerable.Range(13, 13).Select(number => "local-" + number)) &&
                   plan.SeasonBindingUnsafe && !plan.CanPersistCompleteSeasonBinding,
                "Spy x Family Part 1 must map 12/25 and leave one maximal 13-Episode temporary run");

            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-13", "DandanID", "spy-family-part-2", "part2-1",
                    Enumerable.Range(1, 13).Select(number =>
                        Source("part2-" + number, "part2-comment-" + number, number))),
                out plan, out applied, out error), error);
            Assert(applied == 13 && plan.Mappings.Count == 25 && plan.UnmatchedRuns.Count == 0 &&
                   plan.IsComposite && plan.SeasonBindingUnsafe && !plan.CanPersistCompleteSeasonBinding,
                "supplemental Part 2 must reach full Episode coverage while remaining binding-unsafe because it has two sources");
        }

        private static void CoordinatesSingletonAndSeriesTargetSetsIdentically()
        {
            Func<string, CompositeSeasonTargetRequest> target = seasonId =>
                new CompositeSeasonTargetRequest
                {
                    SeasonId = seasonId,
                    BuildPreviewAsync = (ignored, parent) => Task.FromResult(new DanmuSeasonMatchResult
                    {
                        SeasonId = seasonId,
                        Status = "matched",
                    }),
                };
            var singleton = CompositeSeasonTargetSetCoordinator.BuildAsync(
                new[] { target("season-1") }, default).GetAwaiter().GetResult();
            var series = CompositeSeasonTargetSetCoordinator.BuildAsync(
                new[] { target("season-1"), target("season-2") }, default).GetAwaiter().GetResult();
            Assert(singleton.Count == 1 && singleton[0].SeasonId == series[0].SeasonId &&
                   series.Select(result => result.SeasonId).SequenceEqual(new[] { "season-1", "season-2" }),
                "single-Season and whole-Series entry points must use the same stable target-set coordinator contract");
        }

        private static void MapsMultipleSpecialRunsWithoutChangingLocalSeasonMembership()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "main", "main-1", "main-c1"),
                Mapping("local-2", "DandanID", "main", "main-2", "main-c2"),
                Mapping("local-5", "DandanID", "main", "main-5", "main-c5"),
                Mapping("local-6", "DandanID", "main", "main-6", "main-c6"),
                Mapping("local-8", "DandanID", "main", "main-8", "main-c8"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 8), direct, out var plan, out var error), error);
            Assert(RunIds(plan.UnmatchedRuns[0]) == "local-3,local-4" && RunIds(plan.UnmatchedRuns[1]) == "local-7",
                "separate holes must be retained as separate temporary groups in stable local order");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-3", "DandanID", "special-a", "a-1", new[] { Source("a-1", "a-c1", 1), Source("a-2", "a-c2", 2) }),
                out plan, out var applied, out error), error);
            Assert(applied == 2 && RunIds(plan.UnmatchedRuns.Single()) == "local-7",
                "mapping one special group must not move or absorb another unmatched group");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-7", "DandanID", "special-b", "b-1", new[] { Source("b-1", "b-c1", 1) }),
                out plan, out applied, out error), error);
            Assert(applied == 1 && plan.UnmatchedRuns.Count == 0 && plan.IsComposite,
                "multiple independently selected special groups must complete without changing local episode identities");
        }

        private static void SeparatesCanonicalMediaIdentityFromLookupToken()
        {
            var resolved = new ScraperMedia
            {
                // This is the canonical identity returned by the provider detail
                // response, while the token below is what GetMedia accepts.
                Id = "canonical-frieren-s2",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "source-e1", CommentId = "comment-e1", EpisodeNumber = 1 },
                },
            };
            var source = CompositeSeasonMatchService.GetSource("DandanID", resolved, "lookup-token-s2");
            Assert(source.MediaId == "canonical-frieren-s2" && source.MediaLookupId == "lookup-token-s2",
                "canonical media identity and the provider lookup token must remain separate");

            var sameCanonicalDifferentLookup = new CompositeSeasonSourceIdentity
            {
                ProviderId = "dandanid", MediaId = "CANONICAL-FRIEREN-S2", MediaLookupId = "retry-token-s2",
            };
            Assert(source.Equals(sameCanonicalDifferentLookup),
                "composite classification must use provider plus canonical media identity, never a transient lookup token");

            var direct = CompositeSeasonMatchService.CreateDirectMapping(
                "local-1", "DandanID", resolved, "direct-episode-lookup-token");
            Assert(direct != null && direct.Source.MediaId == "canonical-frieren-s2" &&
                   direct.Source.MediaLookupId == "direct-episode-lookup-token" &&
                   direct.SourceEpisodeId == "source-e1" && direct.CommentId == "comment-e1",
                "a direct Episode ProviderId mapping must keep its exact lookup token while retaining canonical media ownership");
        }

        private static void RejectsOverlapsAndUnverifiedMappings()
        {
            var local = LocalEpisodes(1, 2);
            Assert(!CompositeSeasonPlanner.TryCreatePlan(local, new[]
            {
                Mapping("local-1", "DandanID", "s1", "source-1", "comment-1"),
                Mapping("local-1", "DandanID", "s1", "source-2", "comment-2"),
            }, out _, out var error) && error.Contains("only be mapped once"),
                "overlapping local mappings must be rejected");

            Assert(!CompositeSeasonPlanner.TryCreatePlan(local, new[]
            {
                Mapping("local-1", "DandanID", "s1", "source-1", "comment-1"),
                Mapping("local-2", "DandanID", "s1", "source-1", "comment-1"),
            }, out _, out error) && error.Contains("source episode"),
                "one source episode cannot silently serve two local episodes");

            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out error), error);
            Assert(!CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "DandanID", "s1", "source-1", new[] { Source("source-1", string.Empty, 1) }),
                out _, out _, out error) && error.Contains("CommentId"),
                "unverified source episodes must never enter a download plan");
        }

        private static void IdentifiesCompositeSourcesByProviderAndMediaId()
        {
            var mappings = new[]
            {
                Mapping("local-1", "DandanID", "frieren-s1", "source-1", "comment-1"),
                Mapping("local-2", "dandanid", "FRIEREN-S1", "source-2", "comment-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), mappings, out var plan, out var error), error);
            Assert(!plan.IsComposite, "provider/media identity equality must be stable and case-insensitive");

            mappings[1] = Mapping("local-2", "DandanID", "frieren-s2", "source-1", "comment-1");
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), mappings, out plan, out error), error);
            Assert(plan.IsComposite, "different media IDs must classify a season as composite");
        }

        private static void SortsByStableLocalIdentityWithoutDependingOnDisplayNumbers()
        {
            var local = new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "episode-c", SortOrder = 3 },
                new CompositeSeasonLocalEpisode { ItemId = "episode-a", EpisodeNumber = 1, SortOrder = 20 },
                new CompositeSeasonLocalEpisode { ItemId = "episode-b", EpisodeNumber = 1, SortOrder = 10 },
                new CompositeSeasonLocalEpisode { ItemId = "episode-d", SortOrder = 2 },
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            Assert(string.Join(",", plan.OrderedEpisodes.Select(episode => episode.ItemId)) == "episode-d,episode-c,episode-b,episode-a",
                "duplicate and missing display numbers must retain stable ItemId-based episode identity");
        }

        private static void KeepsDirectEpisodeEvidenceFromFalselyCreatingCompositeSources()
        {
            var mappings = new[]
            {
                Mapping("local-1", "DandanID", "direct-episode-provider:DandanID", "direct-1", "comment-1"),
                Mapping("local-2", "DandanID", "direct-episode-provider:DandanID", "direct-2", "comment-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), mappings, out var plan, out var error), error);
            Assert(!plan.IsComposite,
                "multiple exact Episode ProviderIds from one provider must not falsely classify a Season as composite");

            var withSecondSeason = mappings.Concat(new[]
            {
                Mapping("local-3", "DandanID", "frieren-s2", "s2-1", "s2-comment-1"),
            });
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 3), withSecondSeason, out plan, out error), error);
            Assert(plan.IsComposite,
                "direct Episode evidence plus a separately verified upstream Season must classify as composite");
        }

        private static void ValidatesAndAppliesAuthoritativeExclusionsBeforeDirectEvidence()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-4", "DandanID", "s2", "s2-2", "s2c-2"),
                Mapping("local-5", "DandanID", "s2", "s2-3", "s2c-3"),
            };
            Assert(!CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, null,
                    new[] { "local-3", "foreign" }, false, out _, out var error) &&
                   error.Contains("outside the target season"),
                "a single foreign exclusion must reject the whole authoritative draft");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, null,
                new[] { "local-5", "local-3", "local-5", "local-4" }, false, out var plan, out error), error);
            Assert(string.Join(",", plan.EffectiveExcludedLocalEpisodeItemIds) == "local-3,local-4,local-5" &&
                   plan.Mappings.Select(x => x.LocalEpisodeItemId).SequenceEqual(new[] { "local-1", "local-2" }) &&
                   RunIds(plan.UnmatchedRuns.Single()) == "local-3,local-4,local-5",
                "trailing exclusions must deduplicate in authoritative local order and suppress direct evidence before runs are built");

            var replacement = new[]
            {
                Mapping("local-3", "DandanID", "s3", "s3-1", "s3c-1"),
                Mapping("local-4", "DandanID", "s3", "s3-2", "s3c-2"),
                Mapping("local-5", "DandanID", "s3", "s3-3", "s3c-3"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, replacement,
                new[] { "local-3", "local-4", "local-5" }, false, out plan, out error), error);
            Assert(plan.Mappings.Skip(2).All(x => x.Source.MediaId == "s3") && plan.UnmatchedRuns.Count == 0,
                "a verified replacement must occupy the removed trailing range without changing retained mappings");
        }

        private static void SplitsEditableRunsAndRestoresOnlyTheRequestedRange()
        {
            var mappings = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "special", "sp-1", "spc-1"),
                Mapping("local-3", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-4", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-5", "DandanID", "s2", "s2-2", "s2c-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), mappings, out var plan, out var error), error);
            var cards = CompositeSeasonPlanner.GetEditableMappedRuns(plan);
            Assert(cards.Count == 4 &&
                   string.Join(",", cards.Select(card => string.Join("/", card.Mappings.Select(x => x.LocalEpisodeItemId)))) ==
                   "local-1,local-2,local-3,local-4/local-5",
                "S1-special-S1 must render as three independent source cards, with contiguous S2 kept together");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), mappings, null,
                new[] { "local-2" }, false, out plan, out error), error);
            Assert(RunIds(plan.UnmatchedRuns.Single()) == "local-2" &&
                   plan.Mappings.Any(x => x.LocalEpisodeItemId == "local-1") &&
                   plan.Mappings.Any(x => x.LocalEpisodeItemId == "local-3"),
                "removing an interior special must leave both neighboring source cards intact");
            Assert(CompositeSeasonPlanner.TryRestoreExcludedLocalEpisodeItemIds(LocalEpisodes(1, 5),
                plan.EffectiveExcludedLocalEpisodeItemIds, new[] { "local-2" }, out var restored, out error) &&
                   restored.Count == 0,
                "Restore must remove only its own local ids from dialog intent");
            Assert(!CompositeSeasonPlanner.TryRestoreExcludedLocalEpisodeItemIds(LocalEpisodes(1, 5),
                plan.EffectiveExcludedLocalEpisodeItemIds, new[] { "local-3" }, out _, out error) &&
                   error.Contains("currently excluded"),
                "Restore must reject a non-excluded foreign-to-the-draft local range");
        }

        private static void RetainsCompositeSafetyForSubsetAndZeroPersist()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 3), direct, null,
                new[] { "local-3" }, false, out var plan, out var error), error);
            Assert(!plan.IsComposite && plan.CompositeSafetyRequired,
                "a pre-exclusion two-source plan must retain composite safety after a one-source subset draft");
            Assert(!CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, false) &&
                   CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, true),
                "zero persisted files must never create a marker/cleanup transition, while the first persisted file keeps the barrier");
        }

        private static void RebuildsPreviewAndDownloadFromTheSameExclusionAwarePlan()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            Assert(controller.Contains("ExcludedLocalEpisodeItemIds") &&
                   controller.Contains("ParsedExcludedLocalEpisodeItemIds") &&
                   controller.Contains("DanmuExcludedLocalEpisodeItemIdsJson.TryParse"),
                "the scalar GET exclusion contract must be parsed before preview/download dispatch");
            Assert(controller.Contains("BuildCompositePlanAsync(latest, request.ParsedCompositeSelections, false,") &&
                    controller.Contains("BuildCompositePlanAsync(season, request.ParsedCompositeSelections, false,") &&
                    controller.Contains("MergeEpisodeExclusions") &&
                    controller.Contains("TryGetTargetOwnershipExclusions"),
                "composite preview and tracked download must rebuild from the same parsed exclusions");
            Assert(controller.Contains("TryCreatePlan(local, mappings, null,\n                    effectiveExclusions, durableCompositeMarker") &&
                    controller.Contains("TryCreatePlan(local, mappings, replacementMappings,\n                    effectiveExclusions, durableCompositeMarker"),
                "the controller must validate exclusions before direct evidence and then rebuild confirmed replacements");
            Assert(controller.Contains("IsCompositePlan = build.Plan.SeasonBindingUnsafe") &&
                   controller.Contains("CanPersistCompleteSeasonBinding") &&
                    controller.Contains("CommitSeasonDisplayMirrorAfterTerminalAsync"),
                "subset and incomplete downloads must retain the Season-binding barrier, while only complete safe plans may persist a Season binding");
            var automatic = File.ReadAllText(Path.Combine(repositoryRoot, "LibraryManagerEventsHelper.cs"))
                .Replace("\r\n", "\n");
            Assert(automatic.Contains("null, null, false, out var plan") &&
                    automatic.Contains("plan.CanPersistCompleteSeasonBinding") &&
                    automatic.Contains("BeginCompositeSeasonWrite(season, plan.CompositeSafetyRequired)") &&
                    automatic.Contains("SeasonDisplayMirrorPolicy.CanCommit") &&
                    !automatic.Contains("OnCompositeSeasonFilePersistedAsync"),
                "automatic downloads must preserve the lease and defer Season mirrors until terminal success");
        }

        private static void RejectsIncompleteAutomaticSeasonAndResidualSearches()
        {
            var completeness = typeof(Emby.Plugin.Danmu.LibraryManagerEventsHelper).GetMethod(
                "IsCompleteAutomaticSearch",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert(completeness != null,
                "automatic matching must expose one shared completeness predicate for initial and residual searches");

            var unique = new DanmuMatchCandidate
            {
                Site = "DandanID", Id = "unique", Name = "Unique", Score = 0.99, SourceOrder = 0,
            };
            var incomplete = new DanmuMatchSearchResult
            {
                IsComplete = false,
                Candidates = new List<DanmuMatchCandidate> { unique },
                CompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>
                {
                    new DanmuSearchCompletionDiagnostic { Provider = "Bilibili", Status = "timed_out", TimedOut = true },
                    new DanmuSearchCompletionDiagnostic { Provider = "Bilibili", Status = "unstarted", Cancelled = true },
                },
            };
            Assert(!(bool)completeness.Invoke(null, new object[] { incomplete }),
                "a unique partial candidate must remain unusable after any timed-out or unstarted planned call");

            var complete = new DanmuMatchSearchResult
            {
                IsComplete = true,
                Candidates = new List<DanmuMatchCandidate> { unique },
            };
            Assert((bool)completeness.Invoke(null, new object[] { complete }) &&
                   ReferenceEquals(DanmuMatchScorer.SelectAutoCandidate(complete.Candidates), unique),
                "a complete uniquely confident automatic result must preserve the r1 selection behavior");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var automatic = File.ReadAllText(Path.Combine(repositoryRoot, "LibraryManagerEventsHelper.cs"))
                .Replace("\r\n", "\n");
            var initialGuard = automatic.IndexOf(
                "if (!IsCompleteAutomaticSearch(search))\n                            {\n                                LogIncompleteAutomaticSearch(originalSeasonName, \"season\", search);\n                                continue;",
                StringComparison.Ordinal);
            var initialSelection = automatic.IndexOf(
                "selectedCandidate = DanmuMatchScorer.SelectAutoCandidate(search.CanonicalCandidates);",
                StringComparison.Ordinal);
            var residualGuard = automatic.IndexOf(
                "LogIncompleteAutomaticSearch(season.Name, \"residual-range\", search);",
                StringComparison.Ordinal);
            var residualSelection = automatic.IndexOf(
                "var candidate = CompositeSeasonMatchService.SelectSupplementalCandidate(",
                StringComparison.Ordinal);
            var firstDownload = automatic.IndexOf(
                "var outcome = await DownloadEpisodeForProgress(episode, exact, sourceScraper, false, 1)",
                StringComparison.Ordinal);
            var movieGuard = automatic.IndexOf(
                "if (!IsCompleteAutomaticSearch(movieSearch))",
                StringComparison.Ordinal);
            var movieSelection = automatic.IndexOf(
                "selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate(movieSearch.CanonicalCandidates);",
                StringComparison.Ordinal);
            Assert(initialGuard >= 0 && initialGuard < initialSelection,
                "initial automatic Season search must reject incomplete coverage before selecting a candidate");
            Assert(residualGuard >= 0 && residualGuard < residualSelection && residualSelection < firstDownload &&
                   automatic.Substring(residualGuard, residualSelection - residualGuard).Contains("return false;"),
                "an incomplete residual search must abort deterministically before selection, binding, or any file download");
            Assert(movieGuard >= 0 && movieGuard < movieSelection,
                "automatic Movie search must reject incomplete coverage before selecting, binding, or downloading a partial candidate");
        }

        private static void PreservesDirectMetadataAcrossRemoveReplacementAndRestore()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-4", "DandanID", "s2", "s2-2", "s2c-2"),
            };
            foreach (var mapping in direct)
            {
                mapping.Origin = "episode-provider-id";
            }
            var durableSnapshot = direct.Select(MappingSnapshot).ToArray();

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, null,
                    new[] { "local-3", "local-4" }, false, out var removed, out var error), error);
            Assert(RunIds(removed.UnmatchedRuns.Single()) == "local-3,local-4" &&
                   removed.Mappings.Select(mapping => mapping.LocalEpisodeItemId)
                       .SequenceEqual(new[] { "local-1", "local-2" }),
                "removing a direct trailing run must retain every mapping outside that exact local range");
            Assert(direct.Select(MappingSnapshot).SequenceEqual(durableSnapshot),
                "session removal must not mutate durable direct ProviderId/source evidence supplied to the planner");

            var replacement = new[]
            {
                Mapping("local-3", "BilibiliID", "s2-rematch", "r-1", "rc-1"),
                Mapping("local-4", "BilibiliID", "s2-rematch", "r-2", "rc-2"),
            };
            replacement[0].Origin = replacement[1].Origin = "manual";
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, replacement,
                    new[] { "local-3", "local-4" }, false, out var rematched, out error), error);
            Assert(rematched.UnmatchedRuns.Count == 0 &&
                   rematched.Mappings.Where(mapping => mapping.LocalEpisodeItemId == "local-3" ||
                                                       mapping.LocalEpisodeItemId == "local-4")
                       .All(mapping => mapping.Source.MediaId == "s2-rematch" && mapping.Origin == "manual"),
                "a confirmed replacement must fill only the excluded direct run with its exact verified source");
            Assert(direct.Select(MappingSnapshot).SequenceEqual(durableSnapshot),
                "replacement planning must leave the original Episode metadata evidence byte-for-byte unchanged");

            Assert(CompositeSeasonPlanner.TryRestoreExcludedLocalEpisodeItemIds(
                    LocalEpisodes(1, 4),
                    rematched.EffectiveExcludedLocalEpisodeItemIds,
                    new[] { "local-3", "local-4" },
                    out var restoredExclusions,
                    out error) && restoredExclusions.Count == 0,
                error);
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, null,
                    restoredExclusions, false, out var restored, out error), error);
            Assert(restored.Mappings.Select(MappingSnapshot).SequenceEqual(durableSnapshot),
                "Restore plus removal of the run's replacement intent must reconstruct unchanged direct evidence");
        }

        private static void RetainsCompositeSafetyWhenReplacementCollapsesToOneSource()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-4", "DandanID", "s2", "s2-2", "s2c-2"),
            };
            var sameSourceReplacement = new[]
            {
                Mapping("local-3", "DandanID", "s1", "s1-3", "s1c-3"),
                Mapping("local-4", "DandanID", "s1", "s1-4", "s1c-4"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, sameSourceReplacement,
                    new[] { "local-3", "local-4" }, false, out var plan, out var error), error);
            Assert(!plan.IsComposite && plan.CompositeSafetyRequired &&
                   plan.Mappings.Select(mapping => mapping.Source.MediaId).Distinct().Single() == "s1",
                "a same-source replacement may collapse the executable plan to one source but must not downgrade pre-exclusion composite safety");
            Assert(!CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, false) &&
                   CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, true),
                "same-source replacement safety must still be persistence-gated and remain inert for zero files");
        }

        private static void RejectsForeignAndStaleTemporaryRangesWithoutMutatingThePlan()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-5", "DandanID", "s1", "s1-5", "s1c-5"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct,
                    out var plan, out var error), error);
            var beforeMappings = plan.Mappings.Select(MappingSnapshot).ToArray();
            var beforeRuns = plan.UnmatchedRuns.Select(RunIds).ToArray();

            Assert(!DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "foreign-item", 2, out _, out _) &&
                   !DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "local-4", 1, out _, out _) &&
                   !DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "local-3", 1, out _, out _),
                "foreign starts, stale shifted starts, and stale shortened counts must all be rejected");
            Assert(plan.Mappings.Select(MappingSnapshot).SequenceEqual(beforeMappings) &&
                   plan.UnmatchedRuns.Select(RunIds).SequenceEqual(beforeRuns),
                "foreign or stale temporary-range validation must leave the authoritative plan unchanged");
            Assert(DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "local-3", 2, out var run, out error) && RunIds(run) == "local-3,local-4",
                error);
        }

        private static void VerifiesControllerParityMetadataAndDialogResetContracts()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var preview = SliceSource(controller,
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview",
                "private async Task PopulateCompositePreviewIfRequired");
            var download = SliceSource(controller,
                "private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload",
                "private async Task<DanmuDownloadTaskResult> StartTrackedSingleEpisodeDownload");
            var builder = SliceSource(controller,
                "private async Task<CompositePlanBuild> BuildCompositePlanAsync",
                "private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload");

            Assert(preview.Contains("request.ParsedCompositeSelections, false,") &&
                    preview.Contains("effectiveExclusions") &&
                    download.Contains("request.ParsedCompositeSelections, false,\n                request.ParsedExcludedLocalEpisodeItemIds"),
                "preview and download must pass the identical exclusion and selection collections into the authoritative builder");
            Assert(builder.Contains("TryCreatePlan(local, mappings, null,\n                    effectiveExclusions, durableCompositeMarker") &&
                    builder.Contains("TryCreatePlan(local, mappings, replacementMappings,\n                    effectiveExclusions, durableCompositeMarker") &&
                    builder.Contains("TryBuildOwnedPlanningContext"),
                "the shared builder must apply exclusions to direct evidence before replaying verified replacement selections");
            Assert(!preview.Contains("SaveProviderId", StringComparison.Ordinal) &&
                   !preview.Contains("UpdateItem", StringComparison.Ordinal) &&
                   !preview.Contains("SetProviderId", StringComparison.Ordinal) &&
                   !builder.Contains("SaveProviderId", StringComparison.Ordinal) &&
                   !builder.Contains("UpdateItem", StringComparison.Ordinal) &&
                   !builder.Contains("SetProviderId", StringComparison.Ordinal),
                "preview, removal, rematch, and range validation must not write durable ProviderIds or library metadata");

            var frontend = File.ReadAllText(Path.Combine(
                repositoryRoot, "Frontend", "DanmuSmartMatch.CustomCssJS.js")).Replace("\r\n", "\n");
            var dialog = SliceSource(frontend, "function openDialog(title)", "function setBusy(dialog, message, search)");
            Assert(dialog.Contains("compositeDraft: { exclusions: {}, removedRuns: {} }") &&
                   dialog.Contains("dialog.compositeDraft = { exclusions: {}, removedRuns: {} };") &&
                   dialog.IndexOf("dialog.compositeDraft = { exclusions: {}, removedRuns: {} };", StringComparison.Ordinal) <
                   dialog.IndexOf("overlay.remove();", StringComparison.Ordinal),
                "each dialog must own a fresh composite draft and clear it synchronously before close disposal");
            Assert(frontend.Contains("var draft = dialog && dialog.compositeDraft;") &&
                   !frontend.Contains("localStorage", StringComparison.OrdinalIgnoreCase) &&
                   !frontend.Contains("sessionStorage", StringComparison.OrdinalIgnoreCase),
                "composite exclusions must remain dialog-local and must not survive into a later dialog through browser storage");

            var restoreHandler = SliceSource(frontend,
                "restore.addEventListener(\"click\", async function ()",
                "container.appendChild(restore);");
            var filterIndex = restoreHandler.IndexOf("filterCompositeSelectionsByItemIds(", StringComparison.Ordinal);
            var restoreIndex = restoreHandler.IndexOf("restoreCompositeRun(dialog, season, removed.itemIds)", StringComparison.Ordinal);
            var requestIndex = restoreHandler.IndexOf("requestAuthoritativeCompositePlan", StringComparison.Ordinal);
            Assert(filterIndex >= 0 && restoreIndex > filterIndex && requestIndex > restoreIndex &&
                   restoreHandler.Contains("compositeRequestSelections(selections, season), removed.itemIds") &&
                   restoreHandler.Contains("cloneCompositeSelections(removed.selections)") &&
                   restoreHandler.Contains("currentSelections.concat(restoreSelections)"),
                "Restore must filter replacements by the run's real ItemIds, restore only its saved snapshot, and do both before rebuilding direct evidence");
        }

        private static void PreservesExactBindingScoreIntoSelectedCandidate()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var seasonPreview = SliceSource(controller,
                "private async Task<DanmuSeasonMatchResult> GetSeasonMatchPreview(",
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(");
            Assert(!seasonPreview.Contains("TryGetSavedManualBinding") &&
                    !seasonPreview.Contains("GetSeasonScopes(latest)") &&
                    seasonPreview.Contains("DanmuMatchSearchEngine.SearchSeasonAsync("),
                "r4 Season discovery must ignore saved identifiers and register evidence only from fresh search");

            var selectedMapper = SliceSource(controller,
                "private static DanmuSelectedCandidatePreview ToSelectedCandidate(",
                "private static void StampSeasonCandidateEvidence(");
            Assert(selectedMapper.Contains("MatchScore = candidate.MatchScore") &&
                   selectedMapper.Contains("ScoreOrigin = candidate.ScoreOrigin") &&
                   selectedMapper.Contains("SelectionEvidenceToken = candidate.SelectionEvidenceToken"),
                "the selected card must retain the server score, closed provenance, and opaque evidence token");
        }

        private static void PreservesServerCandidateScoreAcrossOwningPlansAndGroups()
        {
            var local = Enumerable.Range(1, 3).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "score-local-" + number,
                EpisodeNumber = number,
                ParentSeasonNumber = number < 3 ? 1 : 0,
                OriginalEpisodeNumber = number,
                SortOrder = number,
                Ownership = number < 3
                    ? CompositeSeasonOwnershipKind.Owning
                    : CompositeSeasonOwnershipKind.Supplemental,
            }).ToList();
            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID",
                MediaId = "score-season",
                MediaLookupId = "score-candidate",
            };
            var episodes = new[]
            {
                Source("score-source-1", "score-comment-1", 1),
                Source("score-source-2", "score-comment-2", 2),
            };
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    plan, source, episodes, "scored", 0.93, DanmuMatchScoreOrigin.SearchConfidence,
                    "opaque-evidence", out plan, out error), error);
            Assert(plan.Mappings.Count == 2 && plan.Mappings.All(mapping =>
                       Math.Abs(mapping.MatchScore - 0.93) < 0.0000001 &&
                       mapping.ScoreOrigin == DanmuMatchScoreOrigin.SearchConfidence &&
                       mapping.SelectionEvidenceToken == "opaque-evidence"),
                "the initial owning candidate must retain its server score and closed evidence on every mapping: " +
                string.Join(";", plan.Mappings.Select(mapping => mapping.LocalEpisodeItemId + ":" +
                    mapping.MatchScore + ":" + mapping.ScoreOrigin + ":" + mapping.SelectionEvidenceToken)));

            var groups = CompositeSeasonMatchService.ToGroups(plan, Enumerable.Empty<Episode>());
            var mapped = groups.Single(group => !group.IsTemporary);
            var unmatched = groups.Single(group => group.IsTemporary);
            Assert(mapped.MatchScore.HasValue && Math.Abs(mapped.MatchScore.Value - 0.93) < 0.0000001 &&
                   mapped.ScoreOrigin == DanmuMatchScoreOrigin.SearchConfidence &&
                   mapped.SelectionEvidenceToken == "opaque-evidence",
                "mapped virtual groups must expose the actual server candidate score and origin");
            var mappedJson = JsonSerializer.Serialize(mapped);
            Assert(mappedJson.Contains("\"MatchScore\":0.93", StringComparison.Ordinal) &&
                   mappedJson.Contains("\"ScoreOrigin\":\"search-confidence\"", StringComparison.Ordinal),
                "the mapped-group wire response must include the real score and its closed origin");
            mapped.MatchScore = 0;
            Assert(JsonSerializer.Serialize(mapped).Contains("\"MatchScore\":0", StringComparison.Ordinal),
                "an explicit server score of zero on a mapped group must remain distinguishable from unmatched");
            Assert(!unmatched.MatchScore.HasValue,
                "unmatched temporary groups must omit a score instead of serializing a fabricated zero");
            Assert(JsonSerializer.Serialize(unmatched).Contains("\"MatchScore\":null", StringComparison.Ordinal) &&
                   !JsonSerializer.Serialize(unmatched).Contains("\"MatchScore\":0", StringComparison.Ordinal),
                "a serializer that emits nulls must still distinguish an unmatched group from score zero");

            Assert(CompositeSeasonPlanner.TryCreatePlan(plan.OrderedEpisodes, plan.Mappings,
                    out var rebuilt, out error), error);
            Assert(rebuilt.Mappings.All(mapping => Math.Abs(mapping.MatchScore - 0.93) < 0.0000001 &&
                       mapping.ScoreOrigin == DanmuMatchScoreOrigin.SearchConfidence &&
                       mapping.SelectionEvidenceToken == "opaque-evidence"),
                "authoritative preview/download reconstruction must preserve candidate evidence");

            var exact = CompositeSeasonMatchService.CreateDirectMapping("exact-local", "DandanID",
                new ScraperMedia
                {
                    Id = "exact-parent",
                    Episodes = new List<ScraperEpisode>
                    {
                        new ScraperEpisode
                        {
                            Id = "exact-episode",
                            CommentId = "exact-comment",
                            EpisodeNumber = 1,
                        },
                    },
                }, "exact-token");
            Assert(exact != null && Math.Abs(exact.MatchScore - 1) < 0.0000001 &&
                   exact.ScoreOrigin == DanmuMatchScoreOrigin.ExactEpisodeId &&
                   exact.Origin == "episode-provider-id",
                "an exact single-Episode identifier must remain closed exact evidence, not a browser score");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            Assert(controller.Contains(
                    "request.MatchScore, request.ScoreOrigin, request.SelectionEvidenceToken,"),
                "the controller must pass resolved server evidence into the initial owning plan");
            var model = File.ReadAllText(Path.Combine(
                repositoryRoot, "Model", "DanmuMatchResult.cs")).Replace("\r\n", "\n");
            var groupModel = SliceSource(model,
                "public class DanmuCompositeSeasonGroup",
                "public class DanmuCompositeEpisode");
            Assert(!groupModel.Contains("JsonIgnore") && !groupModel.Contains("IgnoreDataMember") &&
                   !groupModel.Contains("EmitDefaultValue") &&
                   groupModel.Contains("public double? MatchScore { get; set; }"),
                "Emby/ServiceStack must see a plain nullable score: mapped values serialize, temporary nulls use its default omission policy");
        }

        private static string MappingSnapshot(CompositeSeasonEpisodeMapping mapping)
        {
            return string.Join("|", new[]
            {
                mapping?.LocalEpisodeItemId ?? string.Empty,
                mapping?.Source?.ProviderId ?? string.Empty,
                mapping?.Source?.MediaId ?? string.Empty,
                mapping?.Source?.MediaLookupId ?? string.Empty,
                mapping?.SourceEpisodeId ?? string.Empty,
                mapping?.CommentId ?? string.Empty,
                mapping?.SourceEpisodeNumber?.ToString() ?? string.Empty,
                mapping?.Origin ?? string.Empty,
            });
        }

        private static string SliceSource(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert(start >= 0 && end > start,
                "source-contract markers must remain discoverable: " + startMarker + " -> " + endMarker);
            return source.Substring(start, end - start);
        }

        private static List<CompositeSeasonLocalEpisode> LocalEpisodes(int first, int last) =>
            Enumerable.Range(first, last - first + 1).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "local-" + number, EpisodeNumber = number, SortOrder = number,
            }).ToList();

        private static CompositeSeasonEpisodeMapping Mapping(string local, string provider, string media, string source, string comment) =>
            new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = local,
                Source = new CompositeSeasonSourceIdentity { ProviderId = provider, MediaId = media },
                SourceEpisodeId = source, CommentId = comment, Origin = "direct",
            };

        private static CompositeSeasonEpisodeMapping DirectDandanMapping(int localEpisodeNumber, string parentMediaId,
            string episodeId) =>
            new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = "local-" + localEpisodeNumber,
                Source = new CompositeSeasonSourceIdentity
                {
                    ProviderId = "DandanID",
                    MediaId = parentMediaId,
                    MediaLookupId = episodeId,
                },
                SourceEpisodeId = episodeId,
                CommentId = "chat-" + episodeId,
                SourceEpisodeNumber = localEpisodeNumber <= 28 ? localEpisodeNumber : localEpisodeNumber - 28,
                Origin = "episode-provider-id",
            };

        private static CompositeSeasonSegmentRequest Segment(
            string local, string provider, string media, string sourceStart,
            IEnumerable<CompositeSeasonSourceEpisode> sources, int count = 0) =>
            new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = local, RequestedEpisodeCount = count,
                Source = new CompositeSeasonSourceIdentity { ProviderId = provider, MediaId = media },
                SourceStartEpisodeId = sourceStart, SourceEpisodes = sources.ToList(),
            };

        private static CompositeSeasonSourceEpisode Source(string id, string comment, int number) =>
            new CompositeSeasonSourceEpisode { EpisodeId = id, CommentId = comment, EpisodeNumber = number };

        private static string RunIds(CompositeSeasonUnmatchedRun run) =>
            string.Join(",", run.Episodes.Select(episode => episode.ItemId));

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class DirectEpisodeFakeScraper : AbstractScraper
        {
            private readonly string _providerId;
            private readonly ScraperEpisode _episode;

            public DirectEpisodeFakeScraper(string providerId, ScraperEpisode episode) : base(null)
            {
                _providerId = providerId;
                _episode = episode;
            }

            public int MediaCalls { get; private set; }
            public int MediaEpisodeCalls { get; private set; }
            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id)
            {
                MediaCalls++;
                return Task.FromResult<ScraperMedia>(null);
            }
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
            {
                MediaEpisodeCalls++;
                return Task.FromResult(_episode);
            }
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) =>
                Task.FromResult<ScraperDanmaku>(null);
        }
    }
}
