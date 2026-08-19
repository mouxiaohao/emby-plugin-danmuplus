using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;

namespace Emby.Plugin.Danmu.R207RemainderCoreRegression
{
    internal static class Program
    {
        private static int Main()
        {
            ParsesOnlyStrictPartGrammar();
            AppliesS1AndSeasonConflictRules();
            KeepsTierOutcomesFailClosed();
            AppliesMetadataAndLogicalSeasonRules();
            EnforcesProgressCommitRule();
            FreezesFamilySuffixAndDriftGuards();
            FreezesFamilyChannelsAndLogicalGates();
            ProtectsAuthoritativeFamilyIdentityFromPartMarkers();
            EnforcesProviderLockDecisionBoundary();
            RequiresClosedLockedProviderSearch();
            Console.WriteLine("r207 remainder-core regression checks passed.");
            return 0;
        }

        private static void ParsesOnlyStrictPartGrammar()
        {
            foreach (var fixture in new[] { ("Part 2", 2), ("part:.2", 2), ("Part II", 2), ("第二部分", 2), ("ＰＡＲＴ　２", 2) })
            {
                Assert(PartTitleParser.TryParse(fixture.Item1, out var result) && result == fixture.Item2, "strict Part forms must normalize equivalently");
            }
            foreach (var rejected in new[] { "Part 0", "Part -2", "Part IIX", "Part Z", "Part", "2", "第二季", "第二部", "第二篇", "cour 2", "Episode 2", "counterpart", "counterpart2", "partition" })
                Assert(!PartTitleParser.TryParse(rejected, out _), "non-Part grammar must not be promoted: " + rejected);
            foreach (var malformed in new[] { "Part -2", "Part Z", "Part" })
                Assert(PartTitleParser.Analyze(malformed, out _) == PartTitleParseStatus.Malformed, "explicit malformed Part must not downgrade: " + malformed);
        }

        private static void AppliesS1AndSeasonConflictRules()
        {
            var selected = Decide(1, null, 2024, 12, Candidate("p2", "Parent 第一季 Part 2", 2024, 12));
            Assert(selected.State == RemainderDecisionState.Selected && selected.Kind == RemainderDecisionKind.Part, "parent-only S1 source must accept S1 Part 2");
            var conflict = Decide(2, null, 2024, 12, Candidate("s3", "Parent 第3季 Part 2", 2024, 12));
            Assert(conflict.State == RemainderDecisionState.NotApplicable, "a different explicit Season must be excluded before Part tier");
        }

        private static void KeepsTierOutcomesFailClosed()
        {
            var gap = Decide(1, 2, 2024, 12, Candidate("p4", "Parent Part 4", 2024, 12));
            Assert(gap.State == RemainderDecisionState.Rejected, "a Part gap must not downgrade to metadata");
            var ambiguous = Decide(1, null, 2024, 12, Candidate("a", "Parent Part 2", 2024, 12), Candidate("b", "Parent Part 2", 2024, 12));
            Assert(ambiguous.State == RemainderDecisionState.Rejected, "two stable next Parts must reject");
            var unknown = Decide(1, null, 2024, 12, Candidate("bad", "Parent Part 2", 2024, 12, complete: false));
            Assert(unknown.State == RemainderDecisionState.Unknown, "incomplete detail must not downgrade");
        }

        private static void AppliesMetadataAndLogicalSeasonRules()
        {
            var exact = Decide(1, null, 2024, 12, Candidate("exact", "Parent", 2024, 12));
            Assert(exact.Kind == RemainderDecisionKind.Metadata, "unique same year/count must select");
            var warning = Decide(1, null, 2024, 12, Candidate("warning", "Parent", 2024, 13));
            Assert(warning.Kind == RemainderDecisionKind.MetadataCountWarning, "sole same-year mismatch must select with warning");
            var logical = RemainderAutoMatchCoordinator.DecideLogicalSeason(new RemainderDecisionInput { ParentTitle = "Parent", LogicalSeasonNumber = 1, RemainderFirstYear = 2026 }, new[] { Candidate("s2", "Parent 第2季", 2025, 10, score: .90) });
            Assert(logical.Kind == RemainderDecisionKind.LogicalSeason && logical.NextLogicalSeasonNumber == 2, "logical S2 must accept 60/20/20 score at .90");
            var low = RemainderAutoMatchCoordinator.DecideLogicalSeason(new RemainderDecisionInput { ParentTitle = "Parent", LogicalSeasonNumber = 2, RemainderFirstYear = 2027 }, new[] { Candidate("s3", "Parent 第3季", 2026, 10, score: .89) });
            Assert(low.State == RemainderDecisionState.NotApplicable, "logical score below .90 must not select");
        }

        private static void EnforcesProgressCommitRule()
        {
            Assert(RemainderProgressGuard.CanCommit(true, 10, 9, 1), "a current plan with strict progress may commit");
            Assert(!RemainderProgressGuard.CanCommit(false, 10, 9, 1) && !RemainderProgressGuard.CanCommit(true, 10, 10, 1) && !RemainderProgressGuard.CanCommit(true, 10, 9, 0), "stale, no-progress, or zero-map attempts must stop");
        }

        private static void FreezesFamilySuffixAndDriftGuards()
        {
            var unrelated = Decide(1, null, 2024, 12, Candidate("bad", "Parent TotallyDifferent Part2", 2024, 12));
            Assert(unrelated.State == RemainderDecisionState.NotApplicable, "parent-only channel must reject unrelated residual Part titles");
            var suffix = new List<string>();
            Assert(RemainderProgressGuard.TryGetUniqueMaximalSuffix(new[] { "1", "2", "3" }, new[] { new[] { "2", "3" } }, out suffix) && suffix.Count == 2,
                "only a continuous trailing unmatched run may recurse");
            Assert(!RemainderProgressGuard.TryGetUniqueMaximalSuffix(new[] { "1", "2", "3" }, new[] { new[] { "1" }, new[] { "3" } }, out suffix),
                "internal gaps must silently stop recursion");
            var before = new[] { new RemainderProgressGuard.MappingSnapshot { LocalId = "1", ProviderId = "p", MediaId = "m", LookupId = "l", SourceEpisodeId = "e", CommentId = "c", Origin = "o", Token = "t" } };
            var preserved = new List<RemainderProgressGuard.MappingSnapshot> { new RemainderProgressGuard.MappingSnapshot { LocalId = "1", ProviderId = "p", MediaId = "m", LookupId = "l", SourceEpisodeId = "e", CommentId = "c", Origin = "o", Token = "t" }, new RemainderProgressGuard.MappingSnapshot { LocalId = "2", ProviderId = "p", MediaId = "m2" } };
            Assert(RemainderProgressGuard.PreservesExistingMappings(before, preserved, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "2" }), "new mapping must originate from prior suffix while old mapping stays exact");
            preserved[0].CommentId = "changed";
            Assert(!RemainderProgressGuard.PreservesExistingMappings(before, preserved, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "2" }), "mapping provenance drift must reject commit");
        }

        private static void FreezesFamilyChannelsAndLogicalGates()
        {
            Assert(DecideWithLast(new[] { "Parent", null }, Candidate("x", "Unrelated Part2", 2024, 12)).State == RemainderDecisionState.NotApplicable,
                "a null alias must not revive an unrelated title");
            Assert(DecideWithLast(new[] { "Parent" }, Candidate("x", "Part2", 2024, 12)).State == RemainderDecisionState.NotApplicable &&
                   DecideWithLast(new[] { "Parent" }, Candidate("s", "第一季", 2024, 12)).State == RemainderDecisionState.NotApplicable,
                "bare Part/Season channels without parent must not become the parent family");
            Assert(DecideWithLast(new[] { "Parent" }, Candidate("p2", "Parent part:.2", 2024, 12)).Kind == RemainderDecisionKind.Part,
                "parser-qualified punctuation Part must be removed in the family channel");
            Assert(DecideWithLast(new[] { "Parent" }, Candidate("attached", "ParentPart2", 2024, 12)).Kind == RemainderDecisionKind.Part,
                "known parent context permits an attached Part token without generic word-suffix parsing");
            Assert(DecideWithLast(new[] { "Parent Foo" }, Candidate("tight", "Parent FooPart2", 2024, 12)).Kind == RemainderDecisionKind.Part,
                "shared title residual immediately followed by Part remains eligible");
            Assert(DecideWithLast(new[] { "Parent" }, Candidate("s1tight", "Parent 第一季Part2", 2024, 12)).Kind == RemainderDecisionKind.Part,
                "S1 compatible season phrase immediately followed by Part remains eligible");
            Assert(DecideWithLast(new[] { "Shared Translation" }, Candidate("alias", "Shared Translation Part2", 2024, 12)).Kind == RemainderDecisionKind.Part,
                "shared identity-bearing aliases remain a valid family channel");
            Assert(DecideWithLast(new[] { "Parent" }, Candidate("s1", "Parent 第一季 Part2", 2024, 12)).Kind == RemainderDecisionKind.Part,
                "S1 parent title may continue through explicit first-season Part2");
            var s00 = new RemainderDecisionInput { ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent" }, LogicalSeasonNumber = 0,
                RemainderFirstYear = 2024, RemainderEpisodeCount = 12, CanonicalCandidates = new List<RemainderCandidate> { Candidate("s00", "Parent Season 0 Part2", 2024, 12) } };
            Assert(RemainderAutoMatchCoordinator.Decide(s00).State == RemainderDecisionState.NotApplicable,
                "a parent-only fallback is S1-only and must not promote an S00 Part title");
            var malformed = DecideWithLast(new[] { "Parent" }, Candidate("bad", "Parent Part2 Part3", 2024, 12));
            Assert(PartTitleParser.Analyze("Parent Part2 Part3", out _) == PartTitleParseStatus.Malformed, "multiple differing Part expressions must be malformed at the parser boundary");
            Assert(malformed.State == RemainderDecisionState.Rejected, "malformed/multiple Part grammar must not downgrade to metadata");
            foreach (var invalid in new[] { "Parent Part Z", "Parent Part", "Parent Part -2" })
                Assert(DecideWithLast(new[] { "Parent" }, Candidate("invalid-" + invalid, invalid, 2024, 12)).State == RemainderDecisionState.Rejected,
                    "parent-qualified malformed Part must reject before fallback: " + invalid);
            var logicalInput = new RemainderDecisionInput { ParentTitle = "Parent", LogicalSeasonNumber = 1, RemainderFirstYear = 2026,
                UsedStableIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "same" }, UsedLookupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "lookup" } };
            var used = Candidate("same", "Parent 第2季", 2026, 12, score: .90); used.LookupId = "other";
            Assert(RemainderAutoMatchCoordinator.DecideLogicalSeason(logicalInput, new[] { used }).State == RemainderDecisionState.NotApplicable,
                "logical search excludes used stable identities");
            used.StableId = "fresh"; used.LookupId = "lookup";
            Assert(RemainderAutoMatchCoordinator.DecideLogicalSeason(logicalInput, new[] { used }).State == RemainderDecisionState.NotApplicable,
                "logical search excludes used lookup identities");
            used.LookupId = "fresh"; used.DetailsComplete = false;
            Assert(RemainderAutoMatchCoordinator.DecideLogicalSeason(logicalInput, new[] { used }).State == RemainderDecisionState.Unknown,
                "incomplete logical detail is terminal Unknown");
            var sameStableA = Candidate("stable", "Parent Part2", 2024, 12); sameStableA.LookupId = "a";
            var sameStableB = Candidate("stable", "Parent Part2", 2024, 12); sameStableB.LookupId = "b";
            var representative = DecideWithLast(new[] { "Parent" }, sameStableA, sameStableB);
            Assert(representative.State == RemainderDecisionState.Selected && representative.Candidate.LookupId == "a",
                "stable aliases choose their deterministic first lookup representative");
        }

        private static void EnforcesProviderLockDecisionBoundary()
        {
            var metadata = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent" },
                LogicalSeasonNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                ProviderLock = "provider-a",
                CanonicalCandidates = new List<RemainderCandidate>
                {
                    Candidate("a-only", "Parent", 2024, 12, providerId: "provider-a"),
                    Candidate("b-same-tuple", "Parent", 2024, 12, complete: false, providerId: "provider-b"),
                },
            };
            var selected = RemainderAutoMatchCoordinator.Decide(metadata);
            Assert(selected.State == RemainderDecisionState.Selected && selected.Candidate.ProviderId == "provider-a",
                "an out-of-lock same tuple, including incomplete detail, must not affect same-provider uniqueness");

            metadata.CanonicalCandidates.Add(Candidate("a-duplicate", "Parent", 2024, 12, providerId: "provider-a"));
            Assert(RemainderAutoMatchCoordinator.Decide(metadata).State == RemainderDecisionState.Rejected,
                "two distinct locked-provider tuple candidates must remain ambiguous");

            var part = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent" }, LogicalSeasonNumber = 1,
                LastPartNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12, ProviderLock = "provider-a",
                CanonicalCandidates = new List<RemainderCandidate>
                {
                    Candidate("a-p2", "Parent Part 2", 2024, 12, providerId: "provider-a"),
                    Candidate("b-p2", "Parent Part 2", 2024, 12, providerId: "provider-b"),
                },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(part).Candidate.ProviderId == "provider-a",
                "Part applicability must exclude another provider before exact-next uniqueness");

            var logical = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LogicalSeasonNumber = 1, RemainderFirstYear = 2026, ProviderLock = "provider-a",
            };
            var logicalDecision = RemainderAutoMatchCoordinator.DecideLogicalSeason(logical, new[]
            {
                Candidate("a-s2", "Parent 第2季", 2026, 12, score: .90, providerId: "provider-a"),
                Candidate("b-s2", "Parent 第2季", 2026, 12, score: .99, providerId: "provider-b"),
            });
            Assert(logicalDecision.State == RemainderDecisionState.Selected && logicalDecision.Candidate.ProviderId == "provider-a",
                "fresh logical-season decisions must use only the immutable first-provider lock");

            var noFallback = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent" }, LogicalSeasonNumber = 1,
                RemainderFirstYear = 2024, RemainderEpisodeCount = 12, ProviderLock = "provider-a",
                CanonicalCandidates = new List<RemainderCandidate>
                {
                    Candidate("b-only", "Parent", 2024, 12, providerId: "provider-b"),
                },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(noFallback).State == RemainderDecisionState.NotApplicable,
                "a locked provider with no continuation must silently stop instead of crossing providers");
        }

        private static void ProtectsAuthoritativeFamilyIdentityFromPartMarkers()
        {
            var wrongArcOnly = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent ArcA" },
                LogicalSeasonNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                CanonicalCandidates = new List<RemainderCandidate>
                {
                    Candidate("arc-b-p2", "Parent ArcB Part 2", 2024, 12),
                    Candidate("arc-b-p3", "Parent ArcB Part 3", 2024, 12),
                },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(wrongArcOnly).State == RemainderDecisionState.NotApplicable,
                "a valid Part marker must not make ArcB share ArcA's title family");

            wrongArcOnly.CanonicalCandidates.Add(Candidate("arc-a-continuation", "Parent ArcA Continuation", 2024, 12));
            var metadata = RemainderAutoMatchCoordinator.Decide(wrongArcOnly);
            Assert(metadata.State == RemainderDecisionState.Selected && metadata.Kind == RemainderDecisionKind.Metadata &&
                   metadata.Candidate.StableId == "arc-a-continuation",
                "wrong-arc Parts must be excluded before a same-arc partless metadata continuation is evaluated");

            var sameArcPart = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent ArcA" },
                LogicalSeasonNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                CanonicalCandidates = new List<RemainderCandidate> { Candidate("arc-a-p2", "Parent ArcA Part 2", 2024, 12) },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(sameArcPart).Kind == RemainderDecisionKind.Part,
                "a genuine ArcA to ArcA Part 2 continuation must remain eligible");
            Assert(DecideWithLast(new[] { "Parent Foo" }, Candidate("tight", "Parent FooPart2", 2024, 12)).Kind ==
                   RemainderDecisionKind.Part,
                "a contextual Parent FooPart2 continuation must retain its real shared Foo core");

            var allParentOnly = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent", "Parent Season 1" },
                LogicalSeasonNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                CanonicalCandidates = new List<RemainderCandidate> { Candidate("s1-p2", "Parent 第一季 Part 2", 2024, 12) },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(allParentOnly).Kind == RemainderDecisionKind.Part,
                "the S1 parent-only fallback remains available only when every authoritative last-title channel is parent-only");

            var parentOnlySeasonTwo = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent", "Parent Season 2" },
                LogicalSeasonNumber = 2, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                CanonicalCandidates = new List<RemainderCandidate> { Candidate("s2-parent-only", "Parent Season 2 Part 2", 2024, 12) },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(parentOnlySeasonTwo).State == RemainderDecisionState.NotApplicable,
                "the parent-only fallback must not promote a logical S2 Part title");

            var mixedAliases = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent ArcA", "Parent" },
                LogicalSeasonNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                CanonicalCandidates = new List<RemainderCandidate>
                {
                    new RemainderCandidate
                    {
                        StableId = "arc-b-mixed", LookupId = "arc-b-mixed", Year = 2024, VerifiedEpisodeCount = 12,
                        Titles = new List<string> { "Parent ArcB Part 2", "Parent" },
                    },
                },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(mixedAliases).State == RemainderDecisionState.NotApplicable,
                "a parent-only alias must not launder a different non-parent arc through pairwise fallback");

            var seasonTwo = new RemainderDecisionInput
            {
                ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent ArcA 第2季" },
                LogicalSeasonNumber = 2, RemainderFirstYear = 2024, RemainderEpisodeCount = 12,
                CanonicalCandidates = new List<RemainderCandidate> { Candidate("s3", "Parent ArcA 第3季 Part 2", 2024, 12) },
            };
            Assert(RemainderAutoMatchCoordinator.Decide(seasonTwo).State == RemainderDecisionState.NotApplicable,
                "an explicit S3 candidate remains incompatible while the active logical Season is S2");
            seasonTwo.CanonicalCandidates = new List<RemainderCandidate> { Candidate("s2", "Parent ArcA 第2季 Part 2", 2024, 12) };
            Assert(RemainderAutoMatchCoordinator.Decide(seasonTwo).Kind == RemainderDecisionKind.Part,
                "a real shared non-parent core keeps an explicit active S2 Part candidate family-compatible");
        }

        private static void RequiresClosedLockedProviderSearch()
        {
            var aCompleteBFaulted = new[]
            {
                new DanmuSearchCompletionDiagnostic { Provider = "provider-a", Status = "completed" },
                new DanmuSearchCompletionDiagnostic { Provider = "provider-b", Status = "failed" },
            };
            Assert(RemainderProviderCompletion.IsClosed(aCompleteBFaulted, "provider-a"),
                "a closed locked provider must remain eligible when an unrelated provider fails");
            Assert(!RemainderProviderCompletion.IsClosed(aCompleteBFaulted, "provider-b"),
                "the failed provider must not be treated as closed");
            Assert(!RemainderProviderCompletion.IsClosed(new[]
            {
                new DanmuSearchCompletionDiagnostic { Provider = "provider-a", Status = "failed" },
            }, "provider-a") && !RemainderProviderCompletion.IsClosed(null, "provider-a"),
                "failed or unknown locked-provider evidence must fail closed rather than infer completion from an empty pool");
        }

        private static RemainderDecision DecideWithLast(IEnumerable<string> lastTitles, params RemainderCandidate[] candidates) =>
            RemainderAutoMatchCoordinator.Decide(new RemainderDecisionInput { ParentTitle = "Parent", LastSelectedTitles = lastTitles.ToList(), LogicalSeasonNumber = 1, RemainderFirstYear = 2024, RemainderEpisodeCount = 12, CanonicalCandidates = candidates });

        private static RemainderDecision Decide(int logicalSeason, int? lastPart, int year, int count, params RemainderCandidate[] candidates) =>
            RemainderAutoMatchCoordinator.Decide(new RemainderDecisionInput { ParentTitle = "Parent", LastSelectedTitles = new List<string> { "Parent" }, LogicalSeasonNumber = logicalSeason, LastPartNumber = lastPart, RemainderFirstYear = year, RemainderEpisodeCount = count, CanonicalCandidates = candidates });

        private static RemainderCandidate Candidate(string id, string title, int year, int episodes, bool complete = true, double score = 0, string providerId = "") =>
            new RemainderCandidate { StableId = id, ProviderId = providerId, Titles = new List<string> { title }, Year = year, VerifiedEpisodeCount = episodes, DetailsComplete = complete, LogicalSeasonScore = score };

        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
