using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class LogicalLibraryInventoryTests
    {
        public static void Run()
        {
            var previous = BaseItem.LibraryManager;
            var library = DispatchProxy.Create<ILibraryManager, LibraryFixture>();
            var fixture = (LibraryFixture)(object)library;
            BaseItem.LibraryManager = library;
            try
            {
                var series = new Series { InternalId = 1, Id = Guid.NewGuid(), Name = "Series", PresentationUniqueKey = "series" };
                fixture.Series = series;
                fixture.Seasons = new[] { Season(series, 0, 10), Season(series, 1, 11),
                    Season(series, 1, 12), Season(series, 2, 13), Season(series, 2, 14), Season(series, 3, 15) };
                fixture.Episodes = Versions(1, 12).Concat(Versions(0, 2)).ToArray();

                // Exercise the production SDK overload and coordinator, not a parallel dedup implementation.
                var s1 = fixture.Seasons[1];
                Assert(SeasonTargetPlanningCoordinator.TryBuild(s1, out var context, out var error), error);
                Assert(context.LocalEpisodes.Count == 12 && context.Episodes.Count == 12,
                    "24 physical S1 files must yield 12 logical matching/download items");
                Assert(context.DisplayedEpisodeCount == 14 && context.ParentZeroOutOfScopeCount == 2,
                    "placed specials must be counted logically and remain out of the normal-season plan");
                Assert(context.Episodes.All(item => item.Name == "primary") &&
                       context.LocalEpisodes.Select(item => item.EpisodeNumber).SequenceEqual(
                           Enumerable.Range(1, 12).Select(number => (int?)number)),
                    "retain the representative and ordering returned by Emby, not a physical version sort");
                VerifyMappings(context);

                var groupedSeasons = series.GetSeasons(null, new DtoOptions(false)).OfType<Season>().ToList();
                Assert(groupedSeasons.Count == 4 && groupedSeasons.Count(item => item.IndexNumber > 0) == 3,
                    "background and fallback Series enumeration must return 3 logical regular seasons plus S00");

                Assert(SeasonTargetPlanningCoordinator.TryBuild(s1, fixture.Seasons, out var compatibility, out error) &&
                       context.StructureFingerprint == compatibility.StructureFingerprint,
                    "compatibility overload must use the identical grouped own-season snapshot");
                fixture.Episodes = Versions(0, 2).ToArray();
                Assert(SeasonTargetPlanningCoordinator.TryBuild(fixture.Seasons[0], out var specials, out error) &&
                       specials.LocalEpisodes.Count == 2 && specials.LocalEpisodes.All(item => item.ParentSeasonNumber == 0),
                    "explicit S00 must still map its own logical episodes");

                fixture.Episodes = Versions(1, 12).ToArray();
                Assert(SeasonTargetPlanningCoordinator.TryBuild(s1, out var before, out error), error);
                var alternate = fixture.Episodes[1];
                fixture.Episodes[1] = fixture.Episodes[0];
                fixture.Episodes[0] = alternate;
                Assert(SeasonTargetPlanningCoordinator.TryBuild(s1, out var after, out error) &&
                       before.StructureFingerprint != after.StructureFingerprint,
                    "changed canonical representative must invalidate a captured plan");
                Assert(CompositeSeasonPlanner.TryCreatePlan(before.LocalEpisodes, null, out var beforePlan, out error), error);
                Assert(CompositeSeasonPlanner.TryCreatePlan(after.LocalEpisodes, null, out var afterPlan, out error), error);
                Assert(SeasonPlanningContextBuilder.CreatePlanFingerprint(before, null, beforePlan) !=
                       SeasonPlanningContextBuilder.CreatePlanFingerprint(after, null, afterPlan),
                    "download/retry plan fingerprint must cover the changed representative");

                var extra = Episode(1, 1, 900, "different-logical-item");
                fixture.Episodes = new[] { fixture.Episodes[0], extra };
                Assert(SeasonTargetPlanningCoordinator.TryBuild(s1, out var distinct, out error) &&
                       distinct.LocalEpisodes.Count == 2,
                    "equal season/episode numbers must not erase different presentation identities");

                VerifyQueryContracts();
                Console.WriteLine("Logical library inventory regression checks passed.");
            }
            finally { BaseItem.LibraryManager = previous; }
        }

        private static void VerifyMappings(SeasonPlanningContext context)
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(context.LocalEpisodes, null, out var plan, out var error), error);
            var source = new CompositeSeasonSourceIdentity { ProviderId = "test", MediaId = "season", MediaLookupId = "season" };
            var episodes = Enumerable.Range(1, 12).Select(number => new CompositeSeasonSourceEpisode
            {
                EpisodeId = "source-" + number, CommentId = "comment-" + number, EpisodeNumber = number,
            }).ToList();
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(plan, source, episodes,
                "search-confidence", out var complete, out error), error);
            Assert(complete.Mappings.Count == 12 && complete.UnmatchedRuns.Count == 0 &&
                   complete.Mappings.Select(item => item.LocalEpisodeItemId).Distinct().Count() == 12 &&
                   complete.Mappings.All(mapping => mapping.SourceEpisodeNumber ==
                       context.LocalEpisodes.Single(item => item.ItemId == mapping.LocalEpisodeItemId).EpisodeNumber),
                "full source must map E01-E12 one-to-one instead of using its 12 slots for E01-E06 twice");
            var executable = context.Episodes.Where(item => complete.Mappings.Any(mapping =>
                mapping.LocalEpisodeItemId == item.Id.ToString())).ToList();
            Assert(executable.Count == 12 && executable.All(item => item.Name == "primary"),
                "execution inputs must contain exactly one Emby representative per logical episode");

            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(plan, source, episodes.Take(10),
                "manual-selection", out var partial, out error), error);
            Assert(partial.Mappings.Count == 10 && partial.UnmatchedRuns.Count == 1 &&
                   partial.UnmatchedRuns[0].Episodes.Select(item => item.EpisodeNumber).SequenceEqual(new int?[] { 11, 12 }),
                "a short manual source must leave only logical E11-E12 unmatched");
            var filtered = SeasonPlanningContextBuilder.Filter(context, new[] { context.LocalEpisodes[0].ItemId });
            Assert(filtered.LocalEpisodes.Count == 11 && filtered.Episodes.Count == 11 &&
                   filtered.LocalEpisodes.All(item => item.EpisodeNumber != 1),
                "excluding a logical episode must not reveal its alternate physical version");
        }

        private static Season Season(Series series, int number, long id) => new Season
        {
            InternalId = id, Id = Guid.NewGuid(), Name = "Season " + number,
            IndexNumber = number, ParentId = series.InternalId, SeriesId = series.InternalId,
            SeriesPresentationUniqueKey = series.PresentationUniqueKey,
            PresentationUniqueKey = "series-season-" + number,
        };

        private static Episode Episode(int season, int number, long id, string key) => new Episode
        {
            InternalId = id, Id = Guid.NewGuid(), Name = "primary", ParentIndexNumber = season,
            IndexNumber = number, SeriesId = 1, SeriesPresentationUniqueKey = "series",
            PresentationUniqueKey = key,
        };

        private static IEnumerable<Episode> Versions(int season, int count)
        {
            for (var number = 1; number <= count; number++)
            {
                var key = "series-" + season + "-" + number;
                yield return Episode(season, number, season * 100 + number * 2, key);
                var alternate = Episode(season, number, season * 100 + number * 2 + 1, key);
                alternate.Name = "alternate";
                yield return alternate;
            }
        }

        private static void VerifyQueryContracts()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var queries = Regex.Matches(controller, @"new InternalItemsQuery\s*\{[^}]*?(?:new\[\]\s*\{[^}]*\}[^}]*?)*\}");
            var seasonQueries = queries.Cast<Match>().Select(match => match.Value)
                .Where(text => text.Contains("\"Season\"", StringComparison.Ordinal)).ToList();
            Assert(seasonQueries.Count == 3 && seasonQueries.All(text =>
                    text.Contains("GroupByPresentationUniqueKey = true", StringComparison.Ordinal)),
                "direct, empty-result fallback, and parent-title rematch Season queries must all group presentations");
        }

        private static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        // This fixture models the SDK query contract only. Live Emby checks independently
        // verify the real database's grouping and canonical representative selection.
        public class LibraryFixture : DispatchProxy
        {
            public Series Series;
            public Season[] Seasons;
            public Episode[] Episodes;

            protected override object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "GetItemById") return Series;
                if (method.Name == "GetItemList" || method.Name == "GetItemsResult")
                {
                    var query = args.OfType<InternalItemsQuery>().Single();
                    IEnumerable<BaseItem> items = query.IncludeItemTypes.Contains("Episode")
                        ? Episodes.Cast<BaseItem>() : Seasons;
                    if (query.GroupByPresentationUniqueKey == true)
                        items = items.GroupBy(item => item.PresentationUniqueKey).Select(group => group.First());
                    var result = items.ToArray();
                    return method.Name == "GetItemList" ? (object)result :
                        new QueryResult<BaseItem> { Items = result, TotalRecordCount = result.Length };
                }
                throw new NotSupportedException("Unexpected library call: " + method.Name);
            }
        }
    }
}
