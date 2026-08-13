using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.R4IdentifierMetamorphicRegression
{
    internal static class Program
    {
        private static readonly IdentifierVariant[] Variants =
        {
            new IdentifierVariant("empty", _ => { }),
            new IdentifierVariant("partial", fixture =>
                fixture.Episodes[0].ProviderIds["DandanID"] = "partial-e1"),
            new IdentifierVariant("complete", fixture =>
            {
                fixture.Series.ProviderIds["DandanID"] = "complete-series";
                fixture.Season.ProviderIds["DandanID"] = "complete-season";
                for (var index = 0; index < fixture.Episodes.Count; index++)
                {
                    fixture.Episodes[index].ProviderIds["DandanID"] = "complete-e" + (index + 1);
                }
            }),
            new IdentifierVariant("stale", fixture =>
            {
                fixture.Series.ProviderIds["DandanID"] = "deleted-media";
                fixture.Season.ProviderIds["YoukuID"] = "stale-season";
                fixture.Episodes[0].ProviderIds["BilibiliID"] = "stale-episode";
            }),
            new IdentifierVariant("cross-provider", fixture =>
            {
                fixture.Series.ProviderIds["TencentID"] = "cross-series";
                fixture.Season.ProviderIds["IqiyiID"] = "cross-season";
                fixture.Episodes[0].ProviderIds["MgtvID"] = "cross-e1";
                fixture.Episodes[1].ProviderIds["YoukuID"] = "cross-e2";
                fixture.Episodes[2].ProviderIds["DandanID"] = "cross-e3";
            }),
            new IdentifierVariant("foreign", fixture =>
            {
                fixture.Series.ProviderIds["Tmdb"] = "foreign-series";
                fixture.Season.ProviderIds["ForeignPluginID"] = "foreign-season";
                fixture.Episodes[0].ProviderIds["AnotherPlugin"] = "foreign-episode";
            }),
            new IdentifierVariant("saved-manual-series", fixture =>
                fixture.Series.ProviderIds["DandanIDManual"] = "manual-series"),
            new IdentifierVariant("saved-manual-season", fixture =>
                fixture.Season.ProviderIds["YoukuIDManual"] = "manual-season"),
            new IdentifierVariant("saved-manual-episode", fixture =>
                fixture.Episodes[0].ProviderIds["BilibiliIDManual"] = "manual-episode"),
        };

        private static int Main()
        {
            var snapshots = Variants.Select(BuildSnapshot).ToList();
            var baseline = snapshots[0];
            foreach (var snapshot in snapshots.Skip(1))
            {
                Assert(snapshot.ProviderShape != baseline.ProviderShape,
                    snapshot.Name + " must actually mutate the local identifier fixture");
                Assert(snapshot.SearchCalls == baseline.SearchCalls,
                    snapshot.Name + " changed provider search calls");
                Assert(snapshot.Scores == baseline.Scores,
                    snapshot.Name + " changed candidate scores or selection");
                Assert(snapshot.OrderedEpisodes == baseline.OrderedEpisodes,
                    snapshot.Name + " changed the authoritative local order");
                Assert(snapshot.Mappings == baseline.Mappings,
                    snapshot.Name + " changed the explicit current-selection plan");
                Assert(snapshot.TemporaryRuns == baseline.TemporaryRuns,
                    snapshot.Name + " changed maximal temporary runs");
            }

            Assert(baseline.SearchCalls == "FixtureID:Identifier Fixture" &&
                   baseline.Mappings.Contains("local-1>FixtureID:selected-media:source-1") &&
                   baseline.Mappings.Split('|').Length == 3 &&
                   baseline.TemporaryRuns == "local-4|local-5,local-6",
                "the baseline must exercise one real search, three explicit mappings, and two logical temporary runs");
            VerifyBatchEntrySourceGate();
            Console.WriteLine("R4 identifier-free metamorphic regression checks passed for " +
                              snapshots.Count + " identifier sets.");
            return 0;
        }

        private static Snapshot BuildSnapshot(IdentifierVariant variant)
        {
            var fixture = Fixture.Create();
            variant.Apply(fixture);

            var scraper = new RecordingScraper();
            var local = CompositeSeasonMatchService.GetLocalEpisodes(
                fixture.Episodes, CompositeSeasonTargetContext.ForSeasonNumber(1));
            for (var index = 0; index < local.Count; index++) local[index].ItemId = "local-" + (index + 1);
            Assert(CompositeSeasonOwnership.TryGetOwnedEpisodes(
                    CompositeSeasonTargetContext.ForSeasonNumber(1), local, out var owning),
                "known fixture ownership must be available");

            var search = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { scraper }, fixture.Series.Name, fixture.Season.Name,
                    fixture.Season.ProductionYear, owning.Count, null, null)
                .GetAwaiter().GetResult();
            var selected = search.CanonicalCandidates.Single(candidate => candidate.Id == "selected-media");

            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            var currentSelection = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = "local-1",
                RequestedEpisodeCount = 3,
                Source = new CompositeSeasonSourceIdentity
                {
                    ProviderId = selected.Site,
                    MediaId = selected.Id,
                    MediaLookupId = selected.Id,
                },
                SourceEpisodes = Enumerable.Range(1, 3).Select(number => new CompositeSeasonSourceEpisode
                {
                    EpisodeId = "source-" + number,
                    CommentId = "comment-" + number,
                    EpisodeNumber = number,
                }).ToList(),
                SourceStartEpisodeId = "source-1",
                Origin = "manual",
                MatchScore = selected.MatchScore,
                ScoreOrigin = selected.ScoreOrigin,
                SelectionEvidenceToken = "current-selection",
            };
            Assert(CompositeSeasonPlanner.TryApplySegment(
                    plan, currentSelection, out plan, out var applied, out error) && applied == 3, error);

            return new Snapshot
            {
                Name = variant.Name,
                ProviderShape = ProviderShape(fixture),
                SearchCalls = string.Join("|", scraper.Keywords.Select(keyword => scraper.ProviderId + ":" + keyword)),
                Scores = string.Join("|", search.CanonicalCandidates.Select(candidate =>
                    candidate.Site + ":" + candidate.Id + ":" + candidate.Score.ToString("R") + ":" +
                    candidate.MatchScore.ToString("R") + ":" + candidate.ScoreOrigin)) +
                    ";selected=" + (search.SelectedCandidate?.Id ?? string.Empty),
                OrderedEpisodes = string.Join("|", plan.OrderedEpisodes.Select(episode =>
                    episode.ItemId + ":S" + episode.ParentSeasonNumber + "E" + episode.EpisodeNumber)),
                Mappings = string.Join("|", plan.Mappings.Select(mapping =>
                    mapping.LocalEpisodeItemId + ">" + mapping.Source.ProviderId + ":" +
                    mapping.Source.MediaId + ":" + mapping.SourceEpisodeId + ":" +
                    mapping.MatchScore.ToString("R") + ":" + mapping.ScoreOrigin)),
                TemporaryRuns = string.Join("|", plan.UnmatchedRuns.Select(run =>
                    string.Join(",", run.Episodes.Select(episode => episode.ItemId)))),
            };
        }

        private static string ProviderShape(Fixture fixture)
        {
            string Shape(BaseItem item) => string.Join(",", item.ProviderIds
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + "=" + pair.Value));
            return "series{" + Shape(fixture.Series) + "};season{" + Shape(fixture.Season) + "};episodes{" +
                   string.Join("|", fixture.Episodes.Select(Shape)) + "}";
        }

        private static void VerifyBatchEntrySourceGate()
        {
            var root = FindRepositoryRoot();
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var interactive = Slice(controller, "private async Task<DanmuSeasonMatchResult> GetSeasonMatchPreview(",
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(");
            var composite = Slice(controller,
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(",
                "private async Task PopulateCompositePreviewIfRequired(");
            var library = File.ReadAllText(Path.Combine(root, "LibraryManagerEventsHelper.cs"));
            var automatic = Slice(library, "if (selectedScraper == null)",
                "if (selectedScraper == null || string.IsNullOrWhiteSpace(selectedMediaId))");

            foreach (var entry in new[] { interactive, composite, automatic })
            {
                Assert(!entry.Contains("DanmuProviderIdResolver.", StringComparison.Ordinal) &&
                       !entry.Contains("TryGetSavedManualBinding(", StringComparison.Ordinal) &&
                       entry.Contains("DanmuMatchSearchEngine.SearchSeasonAsync", StringComparison.Ordinal),
                    "a Series/Season batch entry must search descriptively without resolving local identifiers");
            }
            Assert(interactive.Contains("TryBuildOwnedPlanningContext", StringComparison.Ordinal) &&
                   composite.Contains("BuildCompositePlanAsync", StringComparison.Ordinal) &&
                   automatic.Contains("TryBuildAutomaticPlanningContext", StringComparison.Ordinal),
                "interactive, composite, and automatic paths must retain the shared target coordinator boundaries");
        }

        private static string Slice(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert(start >= 0 && end > start, "source gate markers must remain discoverable");
            return source.Substring(start, end - start);
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Emby.Plugin.Danmu.csproj"))) return current.FullName;
                current = current.Parent;
            }
            throw new InvalidOperationException("repository root not found");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class IdentifierVariant
        {
            public IdentifierVariant(string name, Action<Fixture> apply) { Name = name; Apply = apply; }
            public string Name { get; }
            public Action<Fixture> Apply { get; }
        }

        private sealed class Fixture
        {
            public Series Series { get; private set; }
            public Season Season { get; private set; }
            public List<Episode> Episodes { get; private set; }

            public static Fixture Create()
            {
                return new Fixture
                {
                    Series = new Series { Name = "Identifier Fixture", ProductionYear = 2024 },
                    Season = new Season { Name = "Season 1", IndexNumber = 1, ProductionYear = 2024 },
                    Episodes = new List<Episode>
                    {
                        new Episode { ParentIndexNumber = 1, IndexNumber = 1 },
                        new Episode { ParentIndexNumber = 1, IndexNumber = 2 },
                        new Episode { ParentIndexNumber = 1, IndexNumber = 3 },
                        new Episode { ParentIndexNumber = 1, IndexNumber = 4 },
                        new Episode { ParentIndexNumber = 0, IndexNumber = 1 },
                        new Episode { ParentIndexNumber = 0, IndexNumber = 2 },
                    },
                };
            }
        }

        private sealed class Snapshot
        {
            public string Name { get; set; }
            public string ProviderShape { get; set; }
            public string SearchCalls { get; set; }
            public string Scores { get; set; }
            public string OrderedEpisodes { get; set; }
            public string Mappings { get; set; }
            public string TemporaryRuns { get; set; }
        }

        private sealed class RecordingScraper : AbstractScraper
        {
            public RecordingScraper() : base(null) { }
            public List<string> Keywords { get; } = new List<string>();
            public override string Name => "Fixture";
            public override string ProviderName => "Fixture";
            public override string ProviderId => "FixtureID";
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                Keywords.Add(keyword);
                return Task.FromResult(new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = "selected-media", Name = "Identifier Fixture",
                        Category = "\u52A8\u753B", Year = 2024, EpisodeSize = 3,
                    },
                });
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) =>
                Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) =>
                Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) =>
                Task.FromResult<ScraperDanmaku>(null);
        }
    }
}
