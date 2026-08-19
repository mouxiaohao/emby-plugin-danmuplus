using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;

namespace Emby.Plugin.Danmu.R207RemainderOrchestration
{
    internal static class Program
    {
        private static int Main()
        {
            PartChainUsesProductionSeam().GetAwaiter().GetResult();
            ProviderLockStaysInsideOneProvider().GetAwaiter().GetResult();
            WrongArcPartCannotCommitProductionSeam().GetAwaiter().GetResult();
            WrongArcDetailBoundaryStillAllowsMetadataContinuation().GetAwaiter().GetResult();
            JojoAndLogicalPolicy();
            PartialAggregationIsAuthoritative();
            FrierenRefreshesSuffixYearAndPool().GetAwaiter().GetResult();
            SafeStopsRetainAuthoritativePrefix().GetAwaiter().GetResult();
            Console.WriteLine("r207 remainder-orchestration regression checks passed.");
            return 0;
        }

        private static async Task PartChainUsesProductionSeam()
        {
            var state = State(("1", 2024), ("2", 2024), ("3", 2024));
            var selected = new List<string>();
            await Run(state, Input(),
                (snapshot, key) => Round(key, C("p2", "Parent part:.2", 2024, 4),
                    C("p3", "Parent Part III", 2024, 4), C("p4", "Parent 第四部分", 2024, 4)),
                (snapshot, decision) =>
                {
                    selected.Add(decision.Candidate.LookupId);
                    return Commit(snapshot, decision, state, consume: true);
                });
            Assert(selected.SequenceEqual(new[] { "p2", "p3", "p4" }),
                "production seam must commit Part 2→3→4 in order");
        }

        private static async Task ProviderLockStaysInsideOneProvider()
        {
            var state = State(("1", 2024), ("2", 2024), ("3", 2024));
            var selected = new List<string>();
            await Run(state, InputWithProvider("provider-a"), (snapshot, key) =>
            {
                if (key == "")
                {
                    return snapshot.UniqueSuffixItemIds.Count == 3
                        ? Round(key, C("a-p2", "Parent Part 2", 2024, 4, providerId: "provider-a"),
                            C("b-p2", "Parent Part 2", 2024, 4, providerId: "provider-b"))
                        : snapshot.UniqueSuffixItemIds.Count == 2
                            ? Round(key, C("a-p3", "Parent Part 3", 2024, 4, providerId: "provider-a"),
                                C("b-combined", "Parent", 2024, 38, providerId: "provider-b"))
                            : Round(key, C("b-only", "Parent Part 4", 2024, 4, providerId: "provider-b"));
                }
                return Round(key);
            }, (snapshot, decision) =>
            {
                selected.Add(decision.Candidate.LookupId);
                return Commit(snapshot, decision, state, consume: true);
            });
            Assert(selected.SequenceEqual(new[] { "a-p2", "a-p3" }),
                "same-provider Part 2→3 must ignore a cross-provider combined row and then silently stop without fallback");

            var logicalState = State(("29", 2026));
            var logicalSelected = new List<string>();
            await Run(logicalState, InputWithProvider("provider-a"), (snapshot, key) => key == ""
                    ? Round(key)
                    : Round("logical:2", C("a-s2", "Parent 第2季", 2026, 4, .90, "provider-a"),
                        C("b-s2", "Parent 第2季", 2026, 4, .99, "provider-b")),
                (snapshot, decision) =>
                {
                    logicalSelected.Add(decision.Candidate.LookupId);
                    return Commit(snapshot, decision, logicalState, consume: true);
                });
            Assert(logicalSelected.SequenceEqual(new[] { "a-s2" }),
                "fresh logical search candidates from another provider must be ignored before score uniqueness");
        }

        private static async Task WrongArcPartCannotCommitProductionSeam()
        {
            var state = State(Enumerable.Range(1, 48).Select(index => (index.ToString(), 2024)).ToArray());
            var counters = new RemainderBoundaryCounters();
            var input = InputWithProvider("provider-a");
            input.LastSelectedTitles = new List<string> { "Parent ArcA" };
            var raw = new[]
            {
                C("arc-b-p2", "Parent ArcB Part 2", 2024, 12, providerId: "provider-a"),
                C("arc-b-p3", "Parent ArcB Part 3", 2024, 12, providerId: "provider-a"),
                C("other-provider", "Parent ArcA Part 2", 2024, 12, providerId: "provider-b"),
            };
            var result = await Run(state, input, (snapshot, key) =>
            {
                if (!string.Equals(key, string.Empty, StringComparison.Ordinal)) return Round(key);
                // Model the controller adapter boundary: Provider B is excluded before
                // detail work, while both locked rows are materially resolved so their
                // authoritative title channels can be judged as a family.
                var lockedResolved = raw.Where(candidate => string.Equals(candidate.ProviderId, "provider-a",
                    StringComparison.OrdinalIgnoreCase)).Select(candidate =>
                    {
                        counters.DetailResolutions++;
                        return candidate;
                    }).ToArray();
                return Round(key, lockedResolved);
            },
                (snapshot, decision) =>
                {
                    return RegisterBuildAndCommit(snapshot, decision, state, counters, consume: true);
                });
            Assert(ReferenceEquals(result, state) && counters.DetailResolutions == 2 &&
                   counters.RemainderEvidenceRegistrations == 0 && counters.AuthoritativeBuilds == 0 && counters.Commits == 0,
                "wrong-arc locked rows may resolve details, but must reach neither evidence, build, nor commit for a false 48/48 plan");
        }

        private static async Task WrongArcDetailBoundaryStillAllowsMetadataContinuation()
        {
            var state = State(("1", 2024));
            var counters = new RemainderBoundaryCounters(); var selected = new List<string>();
            var input = InputWithProvider("provider-a");
            input.LastSelectedTitles = new List<string> { "Parent ArcA" };
            var result = await Run(state, input, (snapshot, key) =>
            {
                if (!string.Equals(key, string.Empty, StringComparison.Ordinal)) return Round(key);
                var lockedResolved = new[]
                {
                    C("arc-b-p2", "Parent ArcB Part 2", 2024, 12, providerId: "provider-a"),
                    C("arc-a-continuation", "Parent ArcA Continuation", 2024, 12, providerId: "provider-a"),
                    C("other-provider", "Parent ArcA Continuation", 2024, 12, providerId: "provider-b"),
                }.Where(candidate => string.Equals(candidate.ProviderId, "provider-a", StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => { counters.DetailResolutions++; return candidate; }).ToArray();
                return Round(key, lockedResolved);
            }, (snapshot, decision) =>
            {
                selected.Add(decision.Candidate.LookupId);
                return RegisterBuildAndCommit(snapshot, decision, state, counters, consume: true);
            });
            Assert(!ReferenceEquals(result, state) && selected.SequenceEqual(new[] { "arc-a-continuation" }) &&
                   counters.DetailResolutions == 2 && counters.RemainderEvidenceRegistrations == 1 &&
                   counters.AuthoritativeBuilds == 1 && counters.Commits == 1,
                "after same-provider detail resolution, wrong-arc Parts must be removed before the ArcA metadata continuation is authored");
        }

        private static void JojoAndLogicalPolicy()
        {
            Assert(RemainderAutoMatchCoordinator.Decide(Input(C("tiny", "Parent", 2024, 3))).State ==
                RemainderDecisionState.NotApplicable, "<=3 source never participates");
            Assert(RemainderAutoMatchCoordinator.Decide(Input(C("a", "Parent", 2024, 4),
                C("b", "Parent", 2024, 4))).State == RemainderDecisionState.Rejected,
                "JOJO same year/count ambiguity rejects");
            var warning = RemainderAutoMatchCoordinator.Decide(Input(C("warn", "Parent", 2024, 5)));
            Assert(warning.Kind == RemainderDecisionKind.MetadataCountWarning,
                "sole same-year count mismatch is an advisory binding");
            Assert(RemainderAutoMatchCoordinator.DecideLogicalSeason(Input(), new[]
                { C("s2", "Parent 第2季", 2024, 4, .89) }).State == RemainderDecisionState.NotApplicable,
                ".89 logical candidate must not bind");
            Assert(RemainderAutoMatchCoordinator.DecideLogicalSeason(Input(), new[]
                { C("s2", "Parent 第2季", 2024, 4, .90) }).State == RemainderDecisionState.Selected,
                ".90 logical candidate binds");
            var usedStable = Input(C("used", "Parent Part 2", 2024, 4));
            usedStable.UsedStableIds.Add("used");
            Assert(RemainderAutoMatchCoordinator.Decide(usedStable).State == RemainderDecisionState.NotApplicable,
                "used stable source cannot recur");
            var usedLookup = Input(C("alias", "Parent Part 2", 2024, 4));
            usedLookup.UsedLookupIds.Add("alias");
            Assert(RemainderAutoMatchCoordinator.Decide(usedLookup).State == RemainderDecisionState.NotApplicable,
                "used lookup alias cannot recur");
        }

        private static void PartialAggregationIsAuthoritative()
        {
            var partial = new CompositeSeasonPlan
            {
                Mappings = new List<CompositeSeasonEpisodeMapping> { new CompositeSeasonEpisodeMapping { LocalEpisodeItemId = "1" } },
                UnmatchedRuns = new List<CompositeSeasonUnmatchedRun> { new CompositeSeasonUnmatchedRun
                    { Episodes = new List<CompositeSeasonLocalEpisode> { new CompositeSeasonLocalEpisode { ItemId = "2" } } } },
            };
            Assert(CompositeSeasonPartialState.HasConfirmedPartialMappings(partial),
                "Series and Season aggregation share confirmed-prefix partial definition");
            partial.Mappings.Clear();
            Assert(!CompositeSeasonPartialState.HasConfirmedPartialMappings(partial),
                "unmatched-only plan is not a retained confirmed prefix");
        }

        private static async Task FrierenRefreshesSuffixYearAndPool()
        {
            var state = State(("29", 2026), ("30", 2026), ("31", 2027));
            var logicalYears = new List<int?>(); var selected = new List<string>();
            await Run(state, Input(), (snapshot, key) =>
            {
                if (key == "") return Round(key); // Part/metadata failed, request fresh logical.
                if (key == "logical")
                {
                    logicalYears.Add(snapshot.SuffixFirstYear);
                    return logicalYears.Count == 1
                        ? Round("logical:2", C("s2", "Parent ArcA 第2季", 2026, 4, .90))
                        : Round("logical:3", C("s3", "Parent ArcA 第3季", 2027, 4, .90));
                }
                return Round(key, C("s2p2", "Parent ArcA 第2季 part2", 2026, 4));
            }, (snapshot, decision) =>
            {
                selected.Add(decision.Candidate.LookupId);
                return Commit(snapshot, decision, state, consume: true);
            });
            Assert(selected.SequenceEqual(new[] { "s2", "s2p2", "s3" }),
                "logical S2, S2 Part2, fresh S3 must each use accepted rounds");
            Assert(logicalYears.SequenceEqual(new int?[] { 2026, 2027 }),
                "fresh logical search uses each current suffix first-year");
        }

        private static async Task SafeStopsRetainAuthoritativePrefix()
        {
            var original = State(("1", 2024), ("2", 2024));
            var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var cancelledResult = await Run(original, Input(), (s, k) => Round(k, C("p2", "Parent Part2", 2024, 4)),
                (s, d) => Commit(s, d, original, true), cancelled.Token);
            Assert(ReferenceEquals(cancelledResult, original), "cancel keeps last committed prefix state");

            var detailFailure = await Run(original, Input(), (s, k) => new RemainderRoundCandidates { Complete = false },
                (s, d) => throw new InvalidOperationException("commit must not be called"));
            Assert(ReferenceEquals(detailFailure, original), "incomplete detail coverage keeps prefix");

            var attempts = 0;
            var noProgress = await Run(original, Input(), (s, k) => Round(k, C("p2", "Parent Part2", 2024, 4)),
                (s, d) => { attempts++; return Commit(s, d, original, consume: false); });
            Assert(ReferenceEquals(noProgress, original) && attempts == 1,
                "no-progress has zero accepted commits and no later recursion");

            var drift = await Run(original, Input(), (s, k) => Round(k, C("p2", "Parent Part2", 2024, 4)),
                (s, d) => Commit(s, d, original, true, driftPrefix: true));
            Assert(ReferenceEquals(drift, original), "mapping-number/inventory drift has zero accepted commit");
            var stale = await Run(original, Input(), (s, k) => Round(k, C("p2", "Parent Part2", 2024, 4)),
                (s, d) => { var outcome = Commit(s, d, original, true); outcome.GenerationCurrent = false; return outcome; });
            Assert(ReferenceEquals(stale, original), "stale generation has zero accepted commit/write");
            var sparseAfterCommit = await Run(original, Input(), (s, k) => Round(k, C("p2", "Parent Part2", 2024, 4)),
                (s, d) => Commit(s, d, original, true, noContinuation: true));
            Assert(!ReferenceEquals(sparseAfterCommit, original) && sparseAfterCommit.NoContinuation,
                "a valid progress commit survives when its next plan has an internal gap and no recursive suffix");
            var gap = new List<string>();
            Assert(!RemainderProgressGuard.TryGetUniqueMaximalSuffix(new[] { "1", "2", "3" },
                new[] { new[] { "3" }, new[] { "1" } }, out gap), "sparse internal run is never a suffix");
        }

        private static async Task<TestState> Run(TestState state, RemainderDecisionInput input,
            Func<RemainderAuthoritativeSnapshot, string, RemainderRoundCandidates> pool,
            Func<RemainderAuthoritativeSnapshot, RemainderDecision, RemainderCommitOutcome> commit,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var initial = Snapshot(state);
            return await new RemainderInteractiveOrchestrator().RunAsync(initial, input,
                (value, token) => Task.FromResult(Snapshot((TestState)value)),
                (snapshot, key, token) => Task.FromResult(pool(snapshot, key)),
                (snapshot, decision, token) => Task.FromResult(commit(snapshot, decision)), cancellationToken) as TestState;
        }

        private static RemainderRoundCandidates Round(string key, params RemainderCandidate[] candidates) =>
            new RemainderRoundCandidates { Complete = true, PoolKey = key, Candidates = candidates.ToList() };

        private static RemainderCommitOutcome Commit(RemainderAuthoritativeSnapshot snapshot, RemainderDecision decision,
            TestState state, bool consume, bool driftPrefix = false, bool noContinuation = false)
        {
            var next = (snapshot.State as TestState ?? state).Clone();
            if (consume && next.Rows.Count > 0)
            {
                var local = next.Rows[0].Id; next.Rows.RemoveAt(0);
                next.Mappings.Add(new RemainderProgressGuard.MappingSnapshot { LocalId = local, ProviderId = "p",
                    MediaId = decision.Candidate.StableId, LookupId = decision.Candidate.LookupId,
                    SourceEpisodeId = "ep-" + local, CommentId = "cid-" + local, SourceEpisodeNumber = 1 });
            }
            if (driftPrefix) next.Mappings[0].SourceEpisodeNumber = 999;
            if (noContinuation) next.NoContinuation = true;
            return new RemainderCommitOutcome { Committed = true, GenerationCurrent = true, NextSnapshot = Snapshot(next) };
        }

        private static RemainderCommitOutcome RegisterBuildAndCommit(RemainderAuthoritativeSnapshot snapshot,
            RemainderDecision decision, TestState state, RemainderBoundaryCounters counters, bool consume)
        {
            counters.RemainderEvidenceRegistrations++;
            counters.AuthoritativeBuilds++;
            counters.Commits++;
            return Commit(snapshot, decision, state, consume);
        }

        private static RemainderAuthoritativeSnapshot Snapshot(TestState state) => new RemainderAuthoritativeSnapshot
        {
            State = state, UniqueSuffixItemIds = state.NoContinuation ? new List<string>() : state.Rows.Select(row => row.Id).ToList(),
            SuffixFirstYear = state.NoContinuation || state.Rows.Count == 0 ? (int?)null : state.Rows[0].Year,
            TotalUnmatchedItemCount = state.Rows.Count,
            Mappings = state.Mappings.Select(CloneMapping).ToList(),
        };

        private static RemainderDecisionInput Input(params RemainderCandidate[] candidates) => InputWithProvider(string.Empty, candidates);

        private static RemainderDecisionInput InputWithProvider(string providerLock, params RemainderCandidate[] candidates) => new RemainderDecisionInput
        {
            ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent" }, LogicalSeasonNumber = 1,
            RemainderFirstYear = 2024, RemainderEpisodeCount = 4, CanonicalCandidates = candidates.ToList(),
            UsedStableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UsedLookupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase), CandidateCoverageComplete = true, ProviderLock = providerLock,
        };

        private static RemainderCandidate C(string id, string title, int year, int count, double score = 0, string providerId = "") =>
            new RemainderCandidate { StableId = id, LookupId = id, ProviderId = providerId, Titles = new List<string> { title }, Year = year,
                VerifiedEpisodeCount = count, DetailsComplete = true, LogicalSeasonScore = score };

        private static TestState State(params (string Id, int Year)[] rows) => new TestState
        {
            Rows = rows.Select(row => new Row { Id = row.Id, Year = row.Year }).ToList(),
            Mappings = new List<RemainderProgressGuard.MappingSnapshot> { new RemainderProgressGuard.MappingSnapshot
                { LocalId = "prefix", ProviderId = "p", MediaId = "first", LookupId = "first", SourceEpisodeId = "p1", CommentId = "c1", SourceEpisodeNumber = 1 } },
        };

        private sealed class TestState
        {
            public List<Row> Rows { get; set; } = new List<Row>();
            public bool NoContinuation { get; set; }
            public List<RemainderProgressGuard.MappingSnapshot> Mappings { get; set; } = new List<RemainderProgressGuard.MappingSnapshot>();
            public TestState Clone() => new TestState { Rows = Rows.Select(row => new Row { Id = row.Id, Year = row.Year }).ToList(),
                NoContinuation = NoContinuation, Mappings = Mappings.Select(CloneMapping).ToList() };
        }
        private sealed class RemainderBoundaryCounters
        {
            public int DetailResolutions { get; set; }
            public int RemainderEvidenceRegistrations { get; set; }
            public int AuthoritativeBuilds { get; set; }
            public int Commits { get; set; }
        }
        private sealed class Row { public string Id { get; set; } = string.Empty; public int Year { get; set; } }
        private static RemainderProgressGuard.MappingSnapshot CloneMapping(RemainderProgressGuard.MappingSnapshot item) =>
            new RemainderProgressGuard.MappingSnapshot { LocalId = item.LocalId, ProviderId = item.ProviderId, MediaId = item.MediaId,
                LookupId = item.LookupId, SourceEpisodeId = item.SourceEpisodeId, CommentId = item.CommentId,
                SourceEpisodeNumber = item.SourceEpisodeNumber, Origin = item.Origin, Token = item.Token };
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
