using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.R207R2SeasonContinuityRegression
{
    internal static class Program
    {
        private static int Main()
        {
            LimitsEligibilityToAnimatedFullSeries();
            PropagatesGenericAdjacentLogicalSeasons().GetAwaiter().GetResult();
            ActivatesLaterAndResetsAtGaps().GetAwaiter().GetResult();
            RejectsIncompleteAndStaleAuthority().GetAwaiter().GetResult();
            KeepsArbitraryPartCountsOutOfSeasonArithmetic().GetAwaiter().GetResult();
            FiltersContinuationSearchBeforeProviderCalls().GetAwaiter().GetResult();
            PreservesGlobalProviderSearchBeforeActivation().GetAwaiter().GetResult();
            ModelsBookwormS1ThroughS4OnDandan().GetAwaiter().GetResult();
            BindsContinuationToServerEvidence();
            FingerprintsLogicalContinuationIdentity();
            KeepsContinuationProofOffTheWire();
            Console.WriteLine("r207r2 season-continuity regression checks passed.");
            return 0;
        }

        private static void LimitsEligibilityToAnimatedFullSeries()
        {
            Assert(SeasonLogicalContinuationPolicy.IsEligible(true, true, true, false),
                "an animated full-Series interactive request is eligible");
            Assert(!SeasonLogicalContinuationPolicy.IsEligible(false, true, true, false) &&
                   !SeasonLogicalContinuationPolicy.IsEligible(true, false, true, false) &&
                   !SeasonLogicalContinuationPolicy.IsEligible(true, true, false, false) &&
                   !SeasonLogicalContinuationPolicy.IsEligible(true, true, true, true),
                "explicit Season, non-animation, narrowed/background, and manual-keyword requests are ineligible");
        }

        private static async Task PropagatesGenericAdjacentLogicalSeasons()
        {
            var contexts = new Dictionary<int, SeasonLogicalTargetContext>();
            var targets = new[]
            {
                Target(1, contexts, Outcome(1, 1, 3, "dandan", logicalAdvance: true)),
                Target(2, contexts, Outcome(2, 4, 4, "dandan", logicalAdvance: false)),
                Target(3, contexts, Outcome(3, 5, 5, "dandan", logicalAdvance: false)),
            };
            var results = await CompositeSeasonTargetSetCoordinator.BuildAsync(
                targets, true, CancellationToken.None).ConfigureAwait(false);

            Assert(results.Count == 3 && !contexts[1].IsContinuation,
                "the chain-activating target keeps ordinary local behavior");
            Assert(contexts[2].IsContinuation && contexts[2].ExpectedLogicalSeasonNumber == 4 &&
                   contexts[2].RequiredProviderId == "dandan",
                "local S2 must continue at source S4 on the chain Provider");
            Assert(contexts[3].IsContinuation && contexts[3].ExpectedLogicalSeasonNumber == 5,
                "a complete single-logical continuation must propagate to every later adjacent Season");
        }

        private static async Task ActivatesLaterAndResetsAtGaps()
        {
            var later = new Dictionary<int, SeasonLogicalTargetContext>();
            await CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
            {
                Target(1, later, Outcome(1, 1, 1, "provider-a", false)),
                Target(2, later, Outcome(2, 2, 4, "provider-b", true)),
                Target(3, later, Outcome(3, 5, 5, "provider-b", false)),
            }, true, CancellationToken.None).ConfigureAwait(false);
            Assert(!later[2].IsContinuation && later[3].ExpectedLogicalSeasonNumber == 5 &&
                   later[3].RequiredProviderId == "provider-b",
                "any later physical Season may activate the general N-1 to N rule");

            var gap = new Dictionary<int, SeasonLogicalTargetContext>();
            await CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
            {
                Target(1, gap, Outcome(1, 1, 3, "dandan", true)),
                Target(3, gap, Outcome(3, 3, 3, "other", false)),
            }, true, CancellationToken.None).ConfigureAwait(false);
            Assert(!gap[3].IsContinuation && gap[3].ExpectedLogicalSeasonNumber == 3 &&
                   string.IsNullOrWhiteSpace(gap[3].RequiredProviderId),
                "a missing physical Season resets the chain without bridging");
        }

        private static async Task RejectsIncompleteAndStaleAuthority()
        {
            foreach (var unsafeOutcome in new[]
            {
                Outcome(1, 1, 3, "dandan", true, complete: false),
                Outcome(1, 1, 3, "dandan", true, complete: true, current: false),
            })
            {
                var contexts = new Dictionary<int, SeasonLogicalTargetContext>();
                await CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
                {
                    Target(1, contexts, unsafeOutcome),
                    Target(2, contexts, Outcome(2, 2, 2, "other", false)),
                }, true, CancellationToken.None).ConfigureAwait(false);
                Assert(!contexts[2].IsContinuation && contexts[2].ExpectedLogicalSeasonNumber == 2,
                    "partial or stale predecessors cannot author an offset");
            }
        }

        private static async Task KeepsArbitraryPartCountsOutOfSeasonArithmetic()
        {
            foreach (var count in new[] { 1, 2, 3, 8, 21 })
            {
                var parts = Enumerable.Range(1, count).Select(number => new DanmuRemainderDecisionEvidence
                {
                    DecisionKind = DanmuRemainderDecisionKinds.Part,
                    Stage = DanmuRemainderDecisionStages.Part,
                    PartNumber = number,
                    ActiveLogicalSeasonNumber = 4,
                }).ToList();
                Assert(SeasonLogicalContinuationPolicy.GetTerminalLogicalSeason(4, parts) == 4,
                    "N Parts must retain logical Season K for N=" + count);
            }

            var contexts = new Dictionary<int, SeasonLogicalTargetContext>();
            await CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
            {
                Target(1, contexts, Outcome(1, 1, 1, "dandan", false)),
                Target(2, contexts, Outcome(2, 2, 2, "dandan", false)),
            }, true, CancellationToken.None).ConfigureAwait(false);
            Assert(!contexts[2].IsContinuation && contexts[2].ExpectedLogicalSeasonNumber == 2,
                "S1 Part1+Part2 must leave local S2 on Season 2, never Part3 or Season3");

            var logical = ValidLogicalDecision(4, 5);
            Assert(SeasonLogicalContinuationPolicy.GetTerminalLogicalSeason(4,
                       new[] { logical }.Concat(Enumerable.Range(1, 12).Select(number =>
                           new DanmuRemainderDecisionEvidence
                           {
                               DecisionKind = DanmuRemainderDecisionKinds.Part,
                               Stage = DanmuRemainderDecisionStages.Part,
                               PartNumber = number,
                               ActiveLogicalSeasonNumber = 5,
                           }))) == 5,
                "an active chain advances only once for logical S5 regardless of later Part count");
        }

        private static async Task FiltersContinuationSearchBeforeProviderCalls()
        {
            var locked = new RecordingScraper("provider-a", "Parent 第4季", "a-s4");
            var forbidden = new RecordingScraper("provider-b", "Parent 第4季", "b-s4");
            var season = new Season { Name = "Season 2", IndexNumber = 2, ProductionYear = 2026 };
            var search = await DanmuMatchSearchEngine.SearchSeasonAsync(
                new AbstractScraper[] { locked, forbidden }, "Parent", season.Name, 2026, 10, null, null,
                BoundedSearchPolicy.Shared, CancellationToken.None, CancellationToken.None,
                null, null, season, false,
                SeasonLogicalTargetContext.Continuation(4, "provider-a", Proof(1, 2, 3, "provider-a")))
                .ConfigureAwait(false);

            Assert(locked.CallCount > 0 && forbidden.CallCount == 0,
                "continuation must filter Providers before any search call");
            Assert(search.CanonicalCandidates.Any(candidate => candidate.Site == "provider-a" &&
                       candidate.Id == "a-s4") &&
                   search.CanonicalCandidates.All(candidate => candidate.Site == "provider-a"),
                "only same-Provider logical S4 candidates may survive");
        }

        private static async Task ModelsBookwormS1ThroughS4OnDandan()
        {
            var contexts = new Dictionary<int, SeasonLogicalTargetContext>();
            var providerCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            await CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
            {
                new CompositeSeasonTargetRequest
                {
                    SeasonId = "bookworm-s1", SeasonNumber = 1,
                    BuildPreviewWithContextAsync = (context, _, __) =>
                    {
                        contexts[1] = context;
                        providerCalls["dandan-s1-s3"] = 1;
                        return Task.FromResult(Result("bookworm-s1",
                            Outcome(1, 1, 3, "Dandan", true, sourceTitle: "小书痴的下克上 第一季/第二季/第三季")));
                    },
                },
                new CompositeSeasonTargetRequest
                {
                    SeasonId = "bookworm-s2", SeasonNumber = 2,
                    BuildPreviewWithContextAsync = (context, _, __) =>
                    {
                        contexts[2] = context;
                        providerCalls[context.RequiredProviderId + "-s4"] = 1;
                        return Task.FromResult(Result("bookworm-s2",
                            Outcome(2, 4, 4, "Dandan", false, sourceTitle: "小书痴的下克上 第四季")));
                    },
                },
            }, true, CancellationToken.None).ConfigureAwait(false);

            Assert(contexts[2].ExpectedLogicalSeasonNumber == 4 &&
                   string.Equals(contexts[2].RequiredProviderId, "Dandan", StringComparison.OrdinalIgnoreCase) &&
                   providerCalls.ContainsKey("Dandan-s4") && providerCalls.Count == 2,
                "Bookworm local S1 source S1-S3 must make local S2 use Dandan source S4 only");
        }

        private static async Task PreservesGlobalProviderSearchBeforeActivation()
        {
            var first = new RecordingScraper("provider-a", "Parent 第4季", "a-s4");
            var second = new RecordingScraper("provider-b", "Parent 第4季", "b-s4");
            await DanmuMatchSearchEngine.SearchSeasonAsync(
                new AbstractScraper[] { first, second }, "Parent", "Season 4", 2026, 10,
                null, null, BoundedSearchPolicy.Shared, CancellationToken.None,
                CancellationToken.None, null, null,
                new Season { Name = "Season 4", IndexNumber = 4, ProductionYear = 2026 },
                false, SeasonLogicalTargetContext.Local(4)).ConfigureAwait(false);
            Assert(first.CallCount > 0 && second.CallCount > 0,
                "the chain-activating target must retain ordinary all-provider priority");
        }

        private static void BindsContinuationToServerEvidence()
        {
            var registry = new DanmuCandidateEvidenceRegistry();
            var proof = Proof(1, 2, 3, "dandan");
            var token = registry.Register("season-2", "dandan", "media-s4", .95,
                DanmuMatchScoreOrigin.SearchConfidence, null, null, 7, 4,
                "dandan", proof);
            proof.RequiredProviderId = "mutated-after-registration";

            Assert(!string.IsNullOrWhiteSpace(token) &&
                   registry.TryResolve(token, "season-2", "dandan", "media-s4", out var evidence) &&
                   evidence.PlanGeneration == 7 && evidence.InitialLogicalSeasonNumber == 4 &&
                   evidence.ContinuationProof != null &&
                   string.Equals(evidence.ContinuationProof.RequiredProviderId, "dandan",
                       StringComparison.OrdinalIgnoreCase),
                "candidate evidence must clone and retain generation/logical/provider proof");
            Assert(!registry.TryResolve(token, "season-2", "other", "media-s4", out _) &&
                   string.IsNullOrWhiteSpace(registry.Register("season-2", "other", "media-s4",
                       .95, DanmuMatchScoreOrigin.SearchConfidence, null, null, 7, 4,
                       "dandan", Proof(1, 2, 3, "dandan"))),
                "target/provider substitution must fail before candidate detail resolution");
        }

        private static void FingerprintsLogicalContinuationIdentity()
        {
            var context = new SeasonPlanningContext
            {
                SeriesId = "series", SeasonId = "season-2", TargetSeasonNumber = 2,
                StructureFingerprint = "structure-2", IsValid = true,
            };
            var plan = new CompositeSeasonPlan();
            var proof = Proof(1, 2, 3, "dandan");
            var logical = SeasonLogicalTargetContext.Continuation(4, "dandan", proof);
            var baseline = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, Array.Empty<DanmuCompositeSeasonSelection>(), plan,
                logical, 4, false);
            var terminalChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, Array.Empty<DanmuCompositeSeasonSelection>(), plan,
                logical, 5, true);
            var otherProof = Proof(1, 2, 3, "other");
            var providerChanged = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, Array.Empty<DanmuCompositeSeasonSelection>(), plan,
                SeasonLogicalTargetContext.Continuation(4, "other", otherProof), 4, false);

            Assert(!string.Equals(baseline, terminalChanged, StringComparison.Ordinal) &&
                   !string.Equals(baseline, providerChanged, StringComparison.Ordinal),
                "terminal logical Season, activation, Provider, and predecessor identity must affect the plan fingerprint");
        }

        private static void KeepsContinuationProofOffTheWire()
        {
            var result = Result("season-1", Outcome(1, 1, 3, "dandan", true));
            var json = JsonSerializer.Serialize(result);
            Assert(!json.Contains("LogicalContinuationOutcome", StringComparison.OrdinalIgnoreCase) &&
                   !json.Contains("ContinuationProof", StringComparison.OrdinalIgnoreCase) &&
                   !json.Contains("RequiredProviderId", StringComparison.OrdinalIgnoreCase),
                "server-only continuation state must not alter the V22 response shape");
        }

        private static CompositeSeasonTargetRequest Target(int number,
            IDictionary<int, SeasonLogicalTargetContext> contexts, SeasonLogicalTargetOutcome outcome)
        {
            return new CompositeSeasonTargetRequest
            {
                SeasonId = "season-" + number,
                SeasonNumber = number,
                BuildPreviewWithContextAsync = (context, _, __) =>
                {
                    contexts[number] = context;
                    return Task.FromResult(Result("season-" + number, outcome));
                },
            };
        }

        private static DanmuSeasonMatchResult Result(string seasonId, SeasonLogicalTargetOutcome outcome)
        {
            var stampedOutcome = outcome.Clone();
            stampedOutcome.SeasonId = seasonId;
            return new DanmuSeasonMatchResult
            {
                SeasonId = seasonId,
                SeasonNumber = stampedOutcome.LocalSeasonNumber,
                Status = stampedOutcome.IsAuthoritativeComplete ? "matched" : "partial",
                AutoSelected = stampedOutcome.IsAuthoritativeComplete,
                LogicalContinuationOutcome = stampedOutcome,
            };
        }

        private static SeasonLogicalTargetOutcome Outcome(int local, int initial, int terminal,
            string provider, bool logicalAdvance, bool complete = true, bool current = true,
            string sourceTitle = "source")
        {
            var ids = new[] { "episode-" + local + "-1", "episode-" + local + "-2" };
            return new SeasonLogicalTargetOutcome
            {
                SeriesId = "series", SeasonId = "season-" + local, LocalSeasonNumber = local,
                InitialLogicalSeasonNumber = initial, TerminalLogicalSeasonNumber = terminal,
                ProviderId = provider, ActivatedByLogicalSeasonAdvance = logicalAdvance,
                IsAuthoritativeComplete = complete, GenerationCurrent = current,
                PlanGeneration = local, StructureFingerprint = "structure-" + local,
                PlanFingerprint = "plan-" + local, EligibleItemIds = ids.ToList(),
                MappedItemIds = complete ? ids.ToList() : ids.Take(1).ToList(), SourceTitle = sourceTitle,
                AnimatedWholeSeries = true,
            };
        }

        private static SeasonLogicalContinuationProof Proof(int previous, int current, int terminal,
            string provider)
        {
            var ids = new[] { "episode-" + previous + "-1", "episode-" + previous + "-2" };
            return new SeasonLogicalContinuationProof
            {
                SeriesId = "series", PredecessorSeasonId = "season-" + previous,
                CurrentSeasonId = "season-" + current,
                PredecessorLocalSeasonNumber = previous, CurrentLocalSeasonNumber = current,
                PredecessorPlanGeneration = previous,
                PredecessorStructureFingerprint = "structure-" + previous,
                PredecessorPlanFingerprint = "plan-" + previous,
                PredecessorInitialLogicalSeasonNumber = 1,
                PredecessorTerminalLogicalSeasonNumber = terminal,
                ExpectedLogicalSeasonNumber = terminal + 1,
                RequiredProviderId = provider, AnimatedWholeSeries = true,
                ActivatedByLogicalSeasonAdvance = true,
                EligibleItemIds = ids.ToList(), MappedItemIds = ids.ToList(),
            };
        }

        private static DanmuRemainderDecisionEvidence ValidLogicalDecision(int active, int next)
        {
            var runIds = Enumerable.Range(1, 4).Select(number => "run-" + number).ToList();
            return new DanmuRemainderDecisionEvidence
            {
                DecisionKind = DanmuRemainderDecisionKinds.LogicalSeason,
                Stage = DanmuRemainderDecisionStages.LogicalSeason,
                ComparisonYear = 2026,
                SourceYear = 2026,
                LocalEpisodeCount = runIds.Count,
                VerifiedSourceEpisodeCount = runIds.Count,
                LogicalSeasonNumber = next,
                ActiveLogicalSeasonNumber = active,
                FinalScore = 1,
                SimilarCandidateCount = 1,
                MatchingTupleCount = 0,
                AuthoritativeParentTitle = "parent",
                ParentTitleScore = 1,
                SeasonNumberScore = 1,
                YearScore = 1,
                ProviderLock = "dandan",
                StableProviderId = "dandan",
                StableMediaId = "logical-" + next,
                RunStartItemId = runIds[0],
                RunItemIds = runIds,
                PlanGeneration = 1,
                VerifiedSourceEpisodes = Enumerable.Range(1, 4).Select(number =>
                    new CompositeSeasonSourceEpisode
                    {
                        EpisodeId = "source-" + number,
                        CommentId = "comment-" + number,
                        EpisodeNumber = number,
                        SourceOrdinal = number,
                    }).ToList(),
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class RecordingScraper : AbstractScraper
        {
            private readonly string _provider;
            private readonly string _title;
            private readonly string _id;

            public RecordingScraper(string provider, string title, string id) : base(null)
            {
                _provider = provider;
                _title = title;
                _id = id;
            }

            public int CallCount { get; private set; }
            public override string Name => _provider;
            public override string ProviderName => _provider;
            public override string ProviderId => _provider;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                CallCount++;
                return Task.FromResult(new List<ScraperSearchInfo>
                {
                    new ScraperSearchInfo
                    {
                        Id = _id, Name = _title, Year = 2026, EpisodeSize = 10, Category = "动画",
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
