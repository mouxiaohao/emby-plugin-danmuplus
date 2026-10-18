using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Tmdb;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class SeasonNumberFallbackTests
    {
        public static void Run()
        {
            FallsBackWithoutMutatingMetadata().GetAwaiter().GetResult();
            KeepsNamedSelectionAndAmbiguity().GetAwaiter().GetResult();
            FallsBackThroughExistingTmdbAliases().GetAwaiter().GetResult();
            RequiresAnAuthoritativePositiveOrdinal().GetAwaiter().GetResult();
            PreservesExplicitKeywordsAndGenericLabels().GetAwaiter().GetResult();
            KeepsCancellationAndFaultsClosed().GetAwaiter().GetResult();
            PreservesCanonicalCandidatesAndPriority().GetAwaiter().GetResult();
            KeepsWrongSeasonsAndYearsBelowConfidence().GetAwaiter().GetResult();
            BookwormFallbackFeedsLogicalRecursionAndNextPhysicalSeason().GetAwaiter().GetResult();
        }

        private static async Task FallsBackWithoutMutatingMetadata()
        {
            var season = Target(2, "第9季：独特篇章");
            var provider = new Fixture("fixture", _ => new[] { Source("s2", "Parent Season 2") });
            var result = await Search(provider, season, aliases: new[] { "另一个独特篇章" });
            Assert(result.SelectedCandidate?.Id == "s2" && result.SelectedCandidate.MatchScore == 1 && result.UsedSeasonNumberFallback,
                "an unavailable named Season must retry the authoritative Emby S2, not the display-name S9");
            Assert(provider.Terms.SequenceEqual(new[] { "Parent", "Parent 独特篇章", "Parent" }),
                "named discovery must finish before exactly one ordinary standard-Season retry");
            Assert(season.Name == "第9季：独特篇章" && season.IndexNumber == 2 &&
                   result.SelectedCandidate.Name == "Parent Season 2" &&
                   result.CanonicalCandidates.Count == 1 && result.CompletionDiagnostics.Count == 3,
                "fallback must not rename local/source metadata, duplicate identities, or lose diagnostics");
        }

        private static async Task KeepsNamedSelectionAndAmbiguity()
        {
            var season = Target(2);
            var provider = new Fixture("fixture", _ => new[]
            {
                Source("named", "Parent 独特篇章"), Source("generic", "Parent Season 2")
            });
            var matched = await Search(provider, season);
            Assert(matched.SelectedCandidate?.Id == "named" && provider.Terms.Count == 2,
                "a confident named source must beat the generic source without numbered retry");
            var ambiguous = new Fixture("fixture", _ => new[]
            {
                Source("named-a", "Parent 独特篇章"), Source("named-b", "Parent 独特篇章"),
                Source("generic", "Parent Season 2")
            });
            var manual = await Search(ambiguous, season);
            Assert(manual.SelectedCandidate == null && manual.Decision == "manual" && ambiguous.Terms.Count == 2,
                "strong named ambiguity must remain manual, not be bypassed with a generic source");
        }

        private static async Task FallsBackThroughExistingTmdbAliases()
        {
            const string parent = "爱书的下克上";
            const string alias = "小书痴的下克上";
            var instance = typeof(Plugin).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var configuration = typeof(Plugin).BaseType.GetField("_configuration", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalPlugin = instance.GetValue(null);
            var originalSender = TmdbAliasClient.HttpGetResponseAsync;
            var fakePlugin = (Plugin)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
            configuration.SetValue(fakePlugin, new PluginConfiguration
            { Tmdb = new TmdbOption { UseAliasSearch = true, ApiKey = "season-fallback-fixture-key" } });
            try
            {
                instance.SetValue(null, fakePlugin);
                var requests = 0;
                TmdbAliasClient.HttpGetResponseAsync = request =>
                {
                    requests++;
                    var json = request.Url.Contains("alternative_titles")
                        ? "{\"results\":[{\"iso_3166_1\":\"CN\",\"title\":\"" + alias + "\"}]}"
                        : "{}";
                    return Task.FromResult(new HttpResponseInfo
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(json)),
                    });
                };
                var season = Target(1, "士兵之女与神殿巫女");
                season.Genres = new[] { "动画" };
                season.ProviderIds = new ProviderIdDictionary { ["Tmdb"] = "99316701" };
                var provider = new Fixture(Emby.Plugin.Danmu.Scraper.Dandan.Dandan.ScraperProviderId, term => term == alias
                    ? new[] { Source("s1", alias + " 第一季", 2019) }
                    : Array.Empty<ScraperSearchInfo>());
                var result = await Search(provider, season, parent: parent, year: 2019);
                Assert(result.SelectedCandidate?.Id == "s1" && result.SelectedCandidate.MatchScore == 0.80 &&
                       result.UsedTmdbAlias && !result.UsedSeasonNumberFallback && requests == 1 && provider.Terms.Count == 3,
                    "an already successful 80-point TMDB alias must retain precedence and skip numbered retry");

                var ambiguous = new Fixture(Emby.Plugin.Danmu.Scraper.Dandan.Dandan.ScraperProviderId, term => term == alias
                    ? new[] { Source("a", alias + " 第一季", 2019), Source("b", alias + " 第一季", 2019) }
                    : Array.Empty<ScraperSearchInfo>());
                var ambiguousResult = await Search(ambiguous, season, parent: parent, year: 2019);
                Assert(ambiguousResult.SelectedCandidate == null && !ambiguousResult.UsedSeasonNumberFallback &&
                       ambiguous.Terms.Count == 3,
                    "strong ambiguity at the existing 80-point alias threshold must not authorize numbered retry");

                var retryProvider = new Fixture(Emby.Plugin.Danmu.Scraper.Dandan.Dandan.ScraperProviderId, term => term == alias
                    ? new[] { Source("s1", alias + " 第一季", 2020) }
                    : Array.Empty<ScraperSearchInfo>());
                var retry = await Search(retryProvider, season, parent: parent, year: 2019);
                Assert(retry.SelectedCandidate?.Id == "s1" && retry.SelectedCandidate.MatchScore == 0.80 &&
                       retry.UsedTmdbAlias && retry.UsedSeasonNumberFallback && requests == 3,
                    "a genuinely unsuccessful named alias stage must retry the standard label through cached parent aliases");
                Assert(retryProvider.Terms.SequenceEqual(new[] { parent, parent + " " + season.Name, alias, parent, alias }),
                    "both stages must use the existing alias pipeline without reusing named Season identity");

                var mixedSeason = Target(2);
                mixedSeason.Genres = season.Genres;
                mixedSeason.ProviderIds = season.ProviderIds;
                var namedOnly = new Fixture("first", term => term.Contains(mixedSeason.Name)
                    ? new[] { Source("named-only", parent + " " + mixedSeason.Name, 2023) }
                    : Array.Empty<ScraperSearchInfo>());
                var aliasOnly = new Fixture(Emby.Plugin.Danmu.Scraper.Dandan.Dandan.ScraperProviderId, term => term == alias
                    ? new[] { Source("s2", alias + " 第二季", 2023) }
                    : Array.Empty<ScraperSearchInfo>());
                var mixed = await Search(new[] { namedOnly, aliasOnly }, mixedSeason, parent: parent);
                Assert(mixed.SelectedCandidate?.Id == "s2" && mixed.UsedSeasonNumberFallback &&
                       mixed.CanonicalCandidates.Any(candidate => candidate.Id == "named-only") &&
                       DanmuMatchScorer.SelectAutoCandidate(mixed.CanonicalCandidates)?.Id == "s2",
                    "retained named-only rows must not inherit the retry alias threshold or win via old provider priority");

                var noAutomaticMatch = new Fixture(Emby.Plugin.Danmu.Scraper.Dandan.Dandan.ScraperProviderId, term => term == parent
                    ? new[] { Source("generic", parent + " 第一季", 2020) }
                    : Array.Empty<ScraperSearchInfo>());
                var manualRematch = await Search(noAutomaticMatch, season, parent: parent, year: 2019);
                Assert(manualRematch.UsedSeasonNumberFallback && manualRematch.SelectedCandidate == null &&
                       manualRematch.ParentTitleRematchAvailable,
                    "an unsuccessful numbered retry must preserve the existing parent-title rematch action");
            }
            finally
            {
                TmdbAliasClient.HttpGetResponseAsync = originalSender;
                instance.SetValue(null, originalPlugin);
            }
        }

        private static async Task RequiresAnAuthoritativePositiveOrdinal()
        {
            foreach (var ordinal in new int?[] { null, 0, -1 })
            {
                var provider = new Fixture("fixture", _ => new[] { Source("s2", "Parent Season 2") });
                var result = await Search(provider, Target(ordinal));
                Assert(result.SelectedCandidate == null && provider.Terms.Count == 2,
                    "missing, special, and invalid Emby Season numbers must not be guessed");
            }
            var noItem = new Fixture("fixture", _ => new[] { Source("s2", "Parent Season 2") });
            var noItemResult = await Search(noItem, null, seasonName: "Season 2 独特篇章");
            Assert(noItemResult.SelectedCandidate == null && noItem.Terms.Count == 2,
                "a display-name number without an authoritative Season item must not authorize fallback");
        }

        private static async Task PreservesExplicitKeywordsAndGenericLabels()
        {
            foreach (var manual in new[] { false, true })
            {
                var provider = new Fixture("fixture", _ => new[] { Source("s2", "Parent Season 2") });
                var result = await Search(provider, Target(2), keyword: "Parent", manual: manual);
                Assert(result.SelectedCandidate == null && provider.Terms.SequenceEqual(new[] { "Parent" }),
                    "explicit keyword discovery must not silently retry another Season identity");
            }
            var generic = new Fixture("fixture", _ => new[] { Source("s2", "Parent Season 2") });
            var matched = await Search(generic, Target(2, "Season 2"));
            Assert(matched.SelectedCandidate?.Id == "s2" && generic.Terms.Count == 1,
                "standard Season labels must keep the original single-stage search");
        }

        private static async Task KeepsCancellationAndFaultsClosed()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var provider = new Fixture("fixture", _ =>
                {
                    cancellation.Cancel();
                    return new[] { Source("s2", "Parent Season 2") };
                });
                var result = await Search(provider, Target(2), token: cancellation.Token);
                Assert(result.WasCancelled && result.SelectedCandidate == null && provider.Terms.Count == 1,
                    "cancelling named discovery must never start numbered retry");
            }
            using (var cancellation = new CancellationTokenSource())
            {
                Fixture provider = null;
                provider = new Fixture("fixture", _ =>
                {
                    if (provider.Terms.Count == 3) cancellation.Cancel();
                    return new[] { Source("s2", "Parent Season 2") };
                });
                var result = await Search(provider, Target(2), token: cancellation.Token);
                Assert(result.WasCancelled && result.SelectedCandidate == null &&
                       result.CanonicalCandidates.Count == 0 && provider.Terms.Count == 3,
                    "cancelling numbered retry must not resurrect first-stage provisional candidates");
            }
            Fixture failed = null;
            failed = new Fixture("fixture", _ => failed.Terms.Count == 2
                ? throw new InvalidOperationException("named fixture failure")
                : new[] { Source("s2", "Parent Season 2") });
            var incomplete = await Search(failed, Target(2));
            Assert(!incomplete.IsComplete && incomplete.SelectedCandidate == null && failed.Terms.Count == 2,
                "incomplete named discovery must not be treated as an unavailable name");

            Fixture retryFailed = null;
            retryFailed = new Fixture("fixture", _ => retryFailed.Terms.Count == 3
                ? throw new InvalidOperationException("retry fixture failure")
                : new[] { Source("s2", "Parent Season 2") });
            var retryFailure = await Search(retryFailed, Target(2));
            Assert(!retryFailure.IsComplete && retryFailure.SelectedCandidate == null &&
                   retryFailure.SearchErrors.Count > 0 && retryFailure.CompletionDiagnostics.Count == 3,
                "fallback failure must retain diagnostics and cannot confirm the provisional first-stage source");
        }

        private static async Task PreservesCanonicalCandidatesAndPriority()
        {
            Fixture provider = null;
            provider = new Fixture("fixture", _ => provider.Terms.Count == 2
                ? new[] { Source("named-only", "Parent 完全不同") }
                : new[] { Source("s2", "Parent Season 2") });
            var result = await Search(provider, Target(2));
            Assert(result.SelectedCandidate?.Id == "s2" && result.CanonicalCandidates.Count == 2 &&
                   result.CanonicalCandidates.Any(candidate => candidate.Id == "named-only"),
                "composite planning must retain the full named pool as well as retry-stage sources");

            var many = new Fixture("fixture", _ => Enumerable.Range(1, 70)
                .Select(number => Source("extra-" + number, "Parent 完全不同" + number))
                .Concat(new[] { Source("s2", "Parent Season 2") }));
            var untrimmed = await Search(many, Target(2));
            Assert(untrimmed.CanonicalCandidates.Count == 71 && untrimmed.Candidates.Count == 60 &&
                   untrimmed.SelectedCandidate?.Id == "s2",
                "numbered retry must never feed the 60-row UI projection to composite planning");

            var first = new Fixture("first", _ => new[] { Source("s2", "Parent Season 2") });
            var second = new Fixture("second", _ => new[] { Source("s2", "Parent Season 2") });
            var ordered = await Search(new[] { first, second }, Target(2));
            Assert(ordered.SelectedCandidate?.Site == "first" && ordered.CompletedProviderCount == 2 &&
                   ordered.CanonicalCandidates.Count == 2 && ordered.CompletionDiagnostics.Count == 6,
                "fallback must retain configured provider priority and deduplicate completion by provider");
        }

        private static async Task KeepsWrongSeasonsAndYearsBelowConfidence()
        {
            var wrongSeason = new Fixture("fixture", _ => new[] { Source("s3", "Parent Season 3") });
            var wrong = await Search(wrongSeason, Target(2));
            Assert(wrong.SelectedCandidate == null && wrongSeason.Terms.Count == 3,
                "standard fallback must not turn a conflicting source Season into a match");
            var wrongYear = new Fixture("fixture", _ => new[] { Source("s2", "Parent Season 2", 2023) });
            var year = await Search(wrongYear, Target(2));
            Assert(year.SelectedCandidate == null && year.CanonicalCandidates.All(candidate => candidate.MatchScore < 0.90),
                "fallback must retain the normal 90-point confidence rule and exact-year evidence");
        }

        private static async Task BookwormFallbackFeedsLogicalRecursionAndNextPhysicalSeason()
        {
            const string parent = "小书痴的下克上";
            // Synthetic episode counts exercise the real search/recursion seams;
            // these are not a statement about live Dandanplay episode inventory.
            var sources = new[]
            {
                Source("1", parent + " 第一季", 2019, 6), Source("2", parent + " 第二季", 2020, 5),
                Source("3", parent + " 第三季", 2021, 4), Source("4", parent + " 第四季", 2026, 4)
            };
            var provider = new Fixture("dandan", _ => sources);
            var physical = Target(1, "士兵之女与神殿巫女");
            var initial = await Search(provider, physical, parent: parent, year: 2019, episodes: 15);
            Assert(initial.SelectedCandidate?.Id == "1" && initial.CanonicalCandidates.Count == 4,
                "Bookworm must recover source S1 while retaining S2-S4 for composite discovery");

            var ids = Enumerable.Range(1, 15).Select(number => "episode-" + number).ToList();
            var mappings = new List<RemainderProgressGuard.MappingSnapshot>();
            Action<string, int> append = (sourceId, count) =>
            {
                var start = mappings.Count;
                for (var ordinal = 1; ordinal <= count; ordinal++) mappings.Add(new RemainderProgressGuard.MappingSnapshot
                {
                    LocalId = ids[start + ordinal - 1], ProviderId = "dandan", MediaId = sourceId,
                    LookupId = "dandan\u001f" + sourceId, SourceEpisodeId = sourceId + "-" + ordinal,
                    CommentId = sourceId + "-comment-" + ordinal, SourceEpisodeNumber = ordinal,
                });
            };
            append("1", 6);
            Func<RemainderAuthoritativeSnapshot> snapshot = () => new RemainderAuthoritativeSnapshot
            {
                State = physical, UniqueSuffixItemIds = ids.Skip(mappings.Count).ToList(),
                SuffixFirstYear = mappings.Count == 6 ? 2020 : 2021,
                TotalUnmatchedItemCount = ids.Count - mappings.Count, Mappings = mappings.ToList(),
            };
            var active = 1;
            var selected = new List<int> { 1 };
            await new RemainderInteractiveOrchestrator().RunAsync(snapshot(), new RemainderDecisionInput
            {
                ParentTitle = parent, LogicalSeasonNumber = 1, ProviderLock = "dandan",
                LastSelectedTitles = new List<string> { sources[0].Name },
                UsedStableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dandan\u001f1" },
                UsedLookupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dandan\u001f1" },
            }, (_, __) => Task.FromResult(snapshot()), async (suffix, key, token) =>
            {
                var candidates = initial.CanonicalCandidates;
                if (key == "logical")
                {
                    var logical = await DanmuMatchSearchEngine.SearchLogicalSeasonAsync(new[] { provider },
                        new LogicalSeasonSearchContext { ParentTitle = parent, ExpectedLogicalSeasonNumber = active + 1,
                            FirstEpisodeYear = suffix.SuffixFirstYear, SuffixEpisodeCount = suffix.UniqueSuffixItemIds.Count },
                        null, new BoundedSearchPolicy(), token, token, contextItem: physical);
                    Assert(logical.SelectedCandidate?.Id == (active + 1).ToString(),
                        "logical search must score the next logical Season instead of physical S1");
                    candidates = logical.CanonicalCandidates;
                }
                return new RemainderRoundCandidates { Complete = true, PoolKey = key, Candidates = candidates.Select(candidate =>
                    new RemainderCandidate { StableId = "dandan\u001f" + candidate.Id, LookupId = "dandan\u001f" + candidate.Id,
                        ProviderId = "dandan", Titles = new List<string> { candidate.Name }, Year = candidate.Year,
                        VerifiedEpisodeCount = sources.Single(source => source.Id == candidate.Id).EpisodeSize,
                        DetailsComplete = true, LogicalSeasonScore = candidate.MatchScore }).ToList() };
            }, (suffix, decision, _) =>
            {
                Assert(decision.Kind == RemainderDecisionKind.LogicalSeason,
                    "Bookworm numbered sources must advance through logical decisions, not reset via metadata");
                active = decision.NextLogicalSeasonNumber.Value;
                selected.Add(active);
                append(active.ToString(), Math.Min(decision.Candidate.VerifiedEpisodeCount, suffix.UniqueSuffixItemIds.Count));
                return Task.FromResult(new RemainderCommitOutcome
                { Committed = true, GenerationCurrent = true, NextSnapshot = snapshot() });
            }, CancellationToken.None);
            Assert(selected.SequenceEqual(new[] { 1, 2, 3 }) && mappings.Count == ids.Count &&
                   mappings.Take(6).All(mapping => mapping.MediaId == "1") && physical.IndexNumber == 1,
                "fallback must allow S1-S3 recursion without rewriting its confirmed S1 prefix or physical metadata");

            var nextPhysical = Target(2, "领主养女篇");
            var unrelated = new Fixture("other", _ => sources);
            DanmuMatchSearchResult next = null;
            await CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
            {
                new CompositeSeasonTargetRequest
                {
                    SeasonId = physical.Id.ToString(), SeasonNumber = 1,
                    BuildPreviewWithContextAsync = (_, __, ___) => Task.FromResult(new DanmuSeasonMatchResult
                    {
                        SeasonId = physical.Id.ToString(), LogicalContinuationOutcome = new SeasonLogicalTargetOutcome
                        {
                            SeriesId = "bookworm-series", SeasonId = physical.Id.ToString(), LocalSeasonNumber = 1,
                            InitialLogicalSeasonNumber = 1, TerminalLogicalSeasonNumber = active, ProviderId = "dandan",
                            AnimatedWholeSeries = true, ActivatedByLogicalSeasonAdvance = true,
                            IsAuthoritativeComplete = true, GenerationCurrent = true, PlanGeneration = 1,
                            StructureFingerprint = "bookworm-structure", PlanFingerprint = "bookworm-plan",
                            EligibleItemIds = ids, MappedItemIds = mappings.Select(mapping => mapping.LocalId).ToList(),
                        }
                    })
                },
                new CompositeSeasonTargetRequest
                {
                    SeasonId = nextPhysical.Id.ToString(), SeasonNumber = 2,
                    BuildPreviewWithContextAsync = async (context, token, parentToken) =>
                    {
                        Assert(context.IsContinuation && context.ExpectedLogicalSeasonNumber == 4,
                            "complete physical S1 must supply logical S4 to physical S2");
                        next = await Search(new[] { provider, unrelated }, nextPhysical, parent: parent, year: 2026,
                            token: token, logicalContext: context);
                        return new DanmuSeasonMatchResult { SeasonId = nextPhysical.Id.ToString() };
                    }
                }
            }, true, CancellationToken.None);
            Assert(next?.SelectedCandidate?.Id == "4" && next.SelectedCandidate.Site == "dandan" && !next.UsedSeasonNumberFallback &&
                   unrelated.Terms.Count == 0 && !provider.Terms.Any(term => term.Contains("领主养女篇")),
                "proven continuation must keep its provider lock and logical S4, never fall back to physical S2");
        }

        private static Season Target(int? ordinal, string name = "独特篇章") =>
            new Season { Id = Guid.NewGuid(), IndexNumber = ordinal, Name = name };

        private static ScraperSearchInfo Source(string id, string name, int year = 2024, int episodes = 12) =>
            new ScraperSearchInfo { Id = id, Name = name, Category = "动漫", Year = year, EpisodeSize = episodes };

        private static Task<DanmuMatchSearchResult> Search(Fixture provider, Season season,
            string parent = "Parent", int year = 2024, int episodes = 12, IEnumerable<string> aliases = null,
            string keyword = null, bool manual = false, CancellationToken token = default(CancellationToken),
            string seasonName = null) => Search(new[] { provider }, season, parent, year, episodes, aliases,
                keyword, manual, token, seasonName);

        private static Task<DanmuMatchSearchResult> Search(IEnumerable<Fixture> providers, Season season,
            string parent = "Parent", int year = 2024, int episodes = 12, IEnumerable<string> aliases = null,
            string keyword = null, bool manual = false, CancellationToken token = default(CancellationToken),
            string seasonName = null, SeasonLogicalTargetContext logicalContext = null) =>
            DanmuMatchSearchEngine.SearchSeasonAsync(providers, parent, seasonName ?? season?.Name ?? "独特篇章",
                year, episodes, keyword, null, new BoundedSearchPolicy(), token, token,
                localSeasonTitleAliases: aliases, contextItem: season, manualKeywordDiscovery: manual,
                logicalTargetContext: logicalContext);

        private sealed class Fixture : AbstractScraper
        {
            private readonly string _id;
            private readonly Func<string, IEnumerable<ScraperSearchInfo>> _search;
            public List<string> Terms { get; } = new List<string>();
            public Fixture(string id, Func<string, IEnumerable<ScraperSearchInfo>> search) : base(null)
            { _id = id; _search = search; }
            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            { Terms.Add(keyword); return Task.FromResult(_search(keyword).ToList()); }
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) => SearchForApi(item?.Name);
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
