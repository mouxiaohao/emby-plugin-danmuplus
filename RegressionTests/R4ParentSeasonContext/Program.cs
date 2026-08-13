using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.R4ParentSeasonContextRegression
{
    internal static class Program
    {
        private static int Main()
        {
            KeepsPlacedSpecialsVisibleButOutsideTheOwningRun();
            AdapterPreservesEnumerationAndMarksOnePunchOwnership();
            AdapterFeedsPlannerWithoutMergingDuplicateSeasonOrdinals();
            FailsClosedForUnknownTargetOrEpisodeOwnership();
            PreventsACompleteSourceFromSwallowingSupplementalSpecials();
            ContinuesOwningSourceAcrossAnInteriorSpecialWithoutConsumingIt();
            ResolvesSeriesItemOwnershipWithoutDuplicateClaims();
            ResolvesRawOnePunchForeignDisplayOwnershipWithoutPlacementFields();
            Console.WriteLine("R4 parent-season context regression checks passed.");
            return 0;
        }

        private static void AdapterFeedsPlannerWithoutMergingDuplicateSeasonOrdinals()
        {
            var input = new List<Episode>
            {
                new Episode { ParentIndexNumber = 1, IndexNumber = 1 },
                new Episode { ParentIndexNumber = 1, IndexNumber = 2 },
                new Episode { ParentIndexNumber = 0, IndexNumber = 1 },
                new Episode { ParentIndexNumber = 0, IndexNumber = 2 },
            };
            var adapted = CompositeSeasonMatchService.GetLocalEpisodes(
                input, CompositeSeasonTargetContext.ForSeasonNumber(1));
            // Detached Episode fixtures do not receive Emby's persisted BaseItem Id.
            // Supply only that persistence boundary so the adapter's ordering and
            // parent-season output can flow unchanged through the planner.
            for (var index = 0; index < adapted.Count; index++)
            {
                adapted[index].ItemId = "fixture-" + index;
            }
            Assert(CompositeSeasonPlanner.TryCreatePlan(adapted, null, out var plan, out var error), error);
            Assert(plan.OrderedEpisodes.Select(x => x.ParentSeasonNumber + ":" + x.EpisodeNumber)
                       .SequenceEqual(new[] { "1:1", "1:2", "0:1", "0:2" }) &&
                   plan.UnmatchedRuns.Count == 2,
                "adapter enumeration and planner placement ordering must preserve S01 then S00 as two logical runs");
        }

        private static void AdapterPreservesEnumerationAndMarksOnePunchOwnership()
        {
            // Deliberately interleave duplicate display numbers. The adapter
            // must preserve input placement rather than sorting by IndexNumber.
            var input = new List<Episode>
            {
                new Episode { ParentIndexNumber = 1, IndexNumber = 1 },
                new Episode { ParentIndexNumber = 0, IndexNumber = 1 },
                new Episode { ParentIndexNumber = 1, IndexNumber = 2 },
            };
            input.AddRange(Enumerable.Range(3, 10).Select(number =>
                new Episode { ParentIndexNumber = 1, IndexNumber = number }));
            input.AddRange(Enumerable.Range(2, 6).Select(number =>
                new Episode { ParentIndexNumber = 0, IndexNumber = number }));

            var adapted = CompositeSeasonMatchService.GetLocalEpisodes(
                input, CompositeSeasonTargetContext.ForSeasonNumber(1));
            Assert(adapted.Count == 19 && adapted[0].ParentSeasonNumber == 1 && adapted[0].EpisodeNumber == 1 &&
                   adapted[1].ParentSeasonNumber == 0 && adapted[1].EpisodeNumber == 1 &&
                   adapted[2].ParentSeasonNumber == 1 && adapted[2].EpisodeNumber == 2,
                "adapter fallback must retain the original Emby enumeration ordinal when runtime placement fields are absent");
            Assert(adapted.Count(x => x.Ownership == CompositeSeasonOwnershipKind.Owning) == 12 &&
                   adapted.Count(x => x.Ownership == CompositeSeasonOwnershipKind.Supplemental) == 7,
                "adapter must classify placed S00 episodes without callers pre-filling PlacementOrder");
        }

        private static void KeepsPlacedSpecialsVisibleButOutsideTheOwningRun()
        {
            var display = OnePunchDisplayEpisodes();
            Assert(CompositeSeasonOwnership.TryGetDisplayEpisodes(
                    CompositeSeasonTargetContext.ForSeasonNumber(1), display, out var marked),
                "known target context must produce its complete display inventory");
            Assert(marked.Count == 19 && marked.Count(x => x.Ownership == CompositeSeasonOwnershipKind.Owning) == 12 &&
                   marked.Count(x => x.Ownership == CompositeSeasonOwnershipKind.Supplemental) == 7,
                "placed S00 episodes must remain visible as seven supplemental records, not become S01 ownership");
            Assert(CompositeSeasonPlanner.TryCreatePlan(marked, null, out var plan, out var error), error);
            Assert(plan.UnmatchedRuns.Count == 2 && RunIds(plan.UnmatchedRuns[0]) == "s1-1,s1-2,s1-3,s1-4,s1-5,s1-6,s1-7,s1-8,s1-9,s1-10,s1-11,s1-12" &&
                   RunIds(plan.UnmatchedRuns[1]) == "s0-1,s0-2,s0-3,s0-4,s0-5,s0-6,s0-7",
                "logical ParentSeasonNumber changes must form temporary-run boundaries regardless of placement order");
        }

        private static void FailsClosedForUnknownTargetOrEpisodeOwnership()
        {
            var display = OnePunchDisplayEpisodes();
            display.Add(new CompositeSeasonLocalEpisode { ItemId = "unknown", EpisodeNumber = 1, PlacementOrder = 20 });
            Assert(!CompositeSeasonOwnership.TryGetDisplayEpisodes(new CompositeSeasonTargetContext(), display, out _),
                "unknown target ownership must fail closed");
            Assert(CompositeSeasonOwnership.TryGetDisplayEpisodes(
                    CompositeSeasonTargetContext.ForSeasonNumber(1), display, out var marked) &&
                   marked.Single(x => x.ItemId == "unknown").Ownership == CompositeSeasonOwnershipKind.Unknown,
                "unknown episode parent data must never be promoted into primary ownership");
            Assert(CompositeSeasonOwnership.TryGetOwnedEpisodes(
                    CompositeSeasonTargetContext.ForSeasonNumber(1), marked, out var owned) &&
                   owned.All(x => x.ParentSeasonNumber == 1) && owned.Count == 12,
                "cross-target ownership API must only expose exact ParentSeasonNumber matches for primary mapping");
        }

        private static void PreventsACompleteSourceFromSwallowingSupplementalSpecials()
        {
            var display = OnePunchDisplayEpisodes();
            CompositeSeasonOwnership.TryGetDisplayEpisodes(CompositeSeasonTargetContext.ForSeasonNumber(1), display, out var marked);
            Assert(CompositeSeasonPlanner.TryCreatePlan(marked, null, out var plan, out var error), error);
            var source = Enumerable.Range(1, 19).Select(number => new CompositeSeasonSourceEpisode
            {
                EpisodeId = "source-" + number,
                CommentId = "comment-" + number,
                EpisodeNumber = number,
            }).ToList();
            var request = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = "s1-1",
                RequestedEpisodeCount = 0,
                Source = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "one-punch-s1" },
                SourceEpisodes = source,
                SourceStartEpisodeId = "source-1",
            };
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, request, out plan, out var applied, out error), error);
            Assert(applied == 12 && plan.Mappings.Count == 12 && plan.UnmatchedRuns.Count == 1 &&
                   RunIds(plan.UnmatchedRuns[0]) == "s0-1,s0-2,s0-3,s0-4,s0-5,s0-6,s0-7",
                "a 19-episode source selected for the owning S01 run may map only S01E01-E12 and must leave placed S00 specials unmatched");
        }

        private static void ContinuesOwningSourceAcrossAnInteriorSpecialWithoutConsumingIt()
        {
            var display = new List<CompositeSeasonLocalEpisode>
            {
                Local("s1-1", 1, 1, 1), Local("s1-2", 2, 1, 2),
                Local("special", 1, 0, 3),
                Local("s1-3", 3, 1, 4), Local("s1-4", 4, 1, 5),
            };
            CompositeSeasonOwnership.TryGetDisplayEpisodes(
                CompositeSeasonTargetContext.ForSeasonNumber(1), display, out var marked);
            Assert(CompositeSeasonPlanner.TryCreatePlan(marked, null, out var plan, out var error), error);
            var source = Enumerable.Range(1, 4).Select(number => new CompositeSeasonSourceEpisode
            {
                EpisodeId = "source-" + number, CommentId = "comment-" + number, EpisodeNumber = number,
            });
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    plan, new CompositeSeasonSourceIdentity { ProviderId = "test", MediaId = "s1" },
                    source, "primary", out plan, out error), error);
            Assert(plan.Mappings.Count == 4 && plan.UnmatchedRuns.Count == 1 &&
                   RunIds(plan.UnmatchedRuns[0]) == "special" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "s1-3").SourceEpisodeId == "source-3",
                "primary source offset must continue across owning runs while an interior supplemental run stays unmatched");
            Assert(CompositeSeasonPlanner.GetEditableMappedRuns(plan).Count == 2,
                "visible mapped groups must remain split at the supplemental logical-season boundary");
        }

        private static void ResolvesSeriesItemOwnershipWithoutDuplicateClaims()
        {
            var placedSpecial = Local("shared-special", 1, 0, 2);
            placedSpecial.PlacementRelation = -1;
            var parentCopyWithSamePlacementMetadata = CloneLocal(placedSpecial);
            var result = CompositeSeasonTargetOwnership.Resolve(new[]
            {
                new CompositeSeasonTargetInventory
                {
                    TargetId = "season-1", TargetSeasonNumber = 1,
                    Episodes = new List<CompositeSeasonLocalEpisode> { Local("s1", 1, 1, 1), placedSpecial },
                },
                new CompositeSeasonTargetInventory
                {
                    TargetId = "season-0", TargetSeasonNumber = 0,
                    Episodes = new List<CompositeSeasonLocalEpisode>
                    {
                        parentCopyWithSamePlacementMetadata, Local("s0-only", 2, 0, 2),
                    },
                },
            });
            Assert(result.IsValid && result.Assignments.Count == 3 &&
                   result.Assignments.Single(x => x.ItemId == "shared-special").TargetId == "season-1" &&
                   result.Assignments.Select(x => x.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3,
                "explicit placement must win over ParentSeasonNumber while every ItemId receives at most one target");

            var conflict = CompositeSeasonTargetOwnership.Resolve(new[]
            {
                new CompositeSeasonTargetInventory { TargetId = "a", TargetSeasonNumber = 1,
                    Episodes = new List<CompositeSeasonLocalEpisode> { Local("ambiguous", 1, 1, 1) } },
                new CompositeSeasonTargetInventory { TargetId = "b", TargetSeasonNumber = 1,
                    Episodes = new List<CompositeSeasonLocalEpisode> { Local("ambiguous", 1, 1, 2) } },
            });
            Assert(!conflict.IsValid && conflict.Assignments.Count == 0 &&
                   conflict.Conflicts.Single().Code == "item-ownership-ambiguous",
                "multiple parent-season matches must fail closed as a structured conflict");
        }

        private static void ResolvesRawOnePunchForeignDisplayOwnershipWithoutPlacementFields()
        {
            var s1Main = Enumerable.Range(1, 12)
                .Select(number => Local("s1-" + number, number, 1, number)).ToList();
            var s0Specials = Enumerable.Range(1, 7)
                .Select(number => Local("s0-" + number, number, 0, 12 + number)).ToList();
            foreach (var episode in s0Specials)
            {
                episode.PlacementRelation = 0;
            }

            var result = CompositeSeasonTargetOwnership.Resolve(new[]
            {
                new CompositeSeasonTargetInventory
                {
                    TargetId = "season-0", TargetSeasonNumber = 0,
                    Episodes = s0Specials.Select(CloneLocal).ToList(),
                },
                new CompositeSeasonTargetInventory
                {
                    TargetId = "season-1", TargetSeasonNumber = 1,
                    Episodes = s1Main.Concat(s0Specials.Select(CloneLocal)).ToList(),
                },
            });
            Assert(result.IsValid && result.Assignments.Count == 19 &&
                   result.Assignments.Select(item => item.ItemId)
                       .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 19 &&
                   s0Specials.All(special => result.Assignments.Single(item => item.ItemId == special.ItemId)
                       .TargetId == "season-1"),
                "a sole foreign display inventory must own duplicated placed specials when Emby exposes no placement fields");

            Assert(CompositeSeasonOwnership.TryGetDisplayEpisodes(
                    CompositeSeasonTargetContext.ForSeasonNumber(1),
                    s1Main.Concat(s0Specials), out var s1Display),
                "the S1 target must retain its complete display inventory");
            Assert(s1Display.Count(item => item.Ownership == CompositeSeasonOwnershipKind.Owning) == 12 &&
                   s1Display.Count(item => item.Ownership == CompositeSeasonOwnershipKind.Supplemental) == 7,
                "the S1 display must mark twelve owning and seven supplemental Episodes");
            Assert(CompositeSeasonPlanner.TryCreatePlan(s1Display, null, out var plan, out var error), error);
            Assert(plan.OrderedEpisodes.Count == 19 && plan.UnmatchedRuns.Count == 2 &&
                   plan.UnmatchedRuns[0].Episodes.Count == 12 && plan.UnmatchedRuns[1].Episodes.Count == 7,
                "the selected S1 owner plan must retain twelve owning Episodes plus one seven-Episode supplemental run");

            var ambiguous = CompositeSeasonTargetOwnership.Resolve(new[]
            {
                new CompositeSeasonTargetInventory { TargetId = "season-0", TargetSeasonNumber = 0,
                    Episodes = new List<CompositeSeasonLocalEpisode> { Local("shared", 1, 0, 1) } },
                new CompositeSeasonTargetInventory { TargetId = "season-1", TargetSeasonNumber = 1,
                    Episodes = new List<CompositeSeasonLocalEpisode> { Local("shared", 1, 0, 1) } },
                new CompositeSeasonTargetInventory { TargetId = "season-2", TargetSeasonNumber = 2,
                    Episodes = new List<CompositeSeasonLocalEpisode> { Local("shared", 1, 0, 1) } },
            });
            Assert(!ambiguous.IsValid && ambiguous.Assignments.Count == 0 &&
                   ambiguous.Conflicts.Single().Code == "item-ownership-ambiguous",
                "multiple foreign display inventories must remain fail-closed");
        }

        private static CompositeSeasonLocalEpisode CloneLocal(CompositeSeasonLocalEpisode episode) =>
            new CompositeSeasonLocalEpisode
            {
                ItemId = episode.ItemId,
                EpisodeNumber = episode.EpisodeNumber,
                OriginalEpisodeNumber = episode.OriginalEpisodeNumber,
                ParentSeasonNumber = episode.ParentSeasonNumber,
                PlacementOrder = episode.PlacementOrder,
                PlacementRelation = episode.PlacementRelation,
                SortOrder = episode.SortOrder,
            };

        private static CompositeSeasonLocalEpisode Local(string id, int episode, int parent, int placement) =>
            new CompositeSeasonLocalEpisode
            {
                ItemId = id, EpisodeNumber = episode, OriginalEpisodeNumber = episode,
                ParentSeasonNumber = parent, PlacementOrder = placement, SortOrder = placement,
            };

        private static List<CompositeSeasonLocalEpisode> OnePunchDisplayEpisodes()
        {
            var items = Enumerable.Range(1, 12).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "s1-" + number, EpisodeNumber = number, OriginalEpisodeNumber = number,
                ParentSeasonNumber = 1, PlacementOrder = number, SortOrder = number,
            }).ToList();
            items.AddRange(Enumerable.Range(1, 7).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "s0-" + number, EpisodeNumber = number, OriginalEpisodeNumber = number,
                ParentSeasonNumber = 0, PlacementOrder = 12 + number, SortOrder = 12 + number,
            }));
            return items;
        }

        private static string RunIds(CompositeSeasonUnmatchedRun run) => string.Join(",", run.Episodes.Select(x => x.ItemId));
        private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
