using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;

namespace Emby.Plugin.Danmu.R3SearchQualityRegression
{
    internal static class Program
    {
        private static int Main()
        {
            BuildsOnlyIdentityBearingTerms();
            AppliesProviderNeutralEligibilityBeforeProjection();
            ProjectsProviderGroupsWithFairDeterministicQuotas();
            KeepsCanonicalSelectionIndependentFromProjection();
            ClassifiesIncompleteAndCancelledRounds();
            KeepsThreeSeasonInvocationsEquivalent();
            Console.WriteLine("r3 search-quality regression checks passed.");
            return 0;
        }

        private static void BuildsOnlyIdentityBearingTerms()
        {
            Assert(DanmuMatchScorer.BuildSearchKeywords("间谍过家家", "第 1 季", null)
                    .SequenceEqual(new[] { "间谍过家家" }),
                "a Chinese generic Season label must never become its own call");
            Assert(DanmuMatchScorer.BuildSearchKeywords("SPY x FAMILY", "Season 2", null)
                    .SequenceEqual(new[] { "SPY x FAMILY" }),
                "an English generic Season label must never become its own call");
            Assert(DanmuMatchScorer.BuildSearchKeywords("SPY x FAMILY", "SPY x FAMILY Part 2", null)
                    .SequenceEqual(new[] { "SPY x FAMILY", "SPY x FAMILY Part 2" }),
                "a meaningful Part qualifier must only be sent with its parent identity");
            Assert(DanmuMatchScorer.BuildSearchKeywords(null, "Season 1", null).Count == 0,
                "missing parent identity must not issue a broad generic call");
            Assert(DanmuMatchScorer.BuildSearchKeywords("Ignored", "Season 1", "  手动\uFFFD标题  ")
                    .SequenceEqual(new[] { "手动标题" }),
                "an explicit keyword must be sanitized and sent exactly once");
        }

        private static void AppliesProviderNeutralEligibilityBeforeProjection()
        {
            var relevant = Info("relevant", "SPY×FAMILY Part 2", "动漫", 13, 2022);
            var translated = Info("translated", "间谍家家酒 Part 2", "番剧", 13, 2022);
            var unrelated = Info("unrelated", "宇宙冒险 Season 1", "动漫", 25, 2022);
            var barePart = Info("bare-part", "校园日记 Part 2", "动漫", 13, 2022);
            var movie = Info("movie", "SPY×FAMILY CODE White", "电影", 1, 2023);

            Assert(DanmuMatchScorer.IsEligibleSeasonCandidate(relevant, "SPY x FAMILY",
                       "SPY x FAMILY Part 2", null) &&
                   DanmuMatchScorer.IsEligibleSeasonCandidate(translated, "间谍过家家",
                       "间谍过家家 Part 2", null),
                "relevant normalized and translated Part titles must remain manually selectable");
            Assert(!DanmuMatchScorer.IsEligibleSeasonCandidate(unrelated, "SPY x FAMILY", "Season 1", null),
                "generic labels and year/count coincidences cannot establish identity");
            Assert(!DanmuMatchScorer.IsEligibleSeasonCandidate(barePart, "SPY x FAMILY",
                    "SPY x FAMILY Part 2", null),
                "a matching bare Part number cannot establish parent identity");
            Assert(!DanmuMatchScorer.IsEligibleSeasonCandidate(movie, "SPY x FAMILY", "Season 1", null),
                "an identifiable movie cannot enter Season discovery");

            var first = new ResultScraper("first", new[] { relevant, unrelated, barePart, movie });
            var second = new ResultScraper("second", new[]
            {
                Info("same-evidence", "SPY×FAMILY Part 2", "动漫", 13, 2022),
            });
            var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { first, second }, "SPY x FAMILY", "SPY x FAMILY Part 2",
                    2022, 13, null, null)
                .GetAwaiter().GetResult();
            Assert(result.CanonicalCandidates.Select(candidate => candidate.Id)
                       .OrderBy(id => id).SequenceEqual(new[] { "relevant", "same-evidence" }) &&
                   result.CanonicalCandidates.Select(candidate => candidate.Site).Distinct().Count() == 2,
                "the same eligibility rule must run for every provider before merge and projection");
        }

        private static void ProjectsProviderGroupsWithFairDeterministicQuotas()
        {
            var noisy = Enumerable.Range(0, 100)
                .Select(index => Candidate("a-" + index.ToString("D3"), 0, 1 - index / 1000d));
            var later = Enumerable.Range(0, 4)
                .Select(index => Candidate("b-" + index.ToString("D3"), 1, 0.80 - index / 100d));
            var input = noisy.Concat(later).Reverse().ToList();
            var projection = DanmuMatchSearchEngine.OrderCandidates(input);

            Assert(projection.Count == 60 && projection.Count(candidate => candidate.SourceOrder == 1) == 4,
                "unused round-robin slots must redistribute while preserving every later provider candidate");
            Assert(projection.TakeWhile(candidate => candidate.SourceOrder == 0).Count() == 56 &&
                   projection.Skip(56).All(candidate => candidate.SourceOrder == 1),
                "the visible list must remain grouped in configured provider order");
            Assert(IsNonIncreasing(projection.Where(candidate => candidate.SourceOrder == 0).Select(x => x.Score)) &&
                   IsNonIncreasing(projection.Where(candidate => candidate.SourceOrder == 1).Select(x => x.Score)) &&
                   projection.Select(candidate => candidate.Id)
                       .SequenceEqual(DanmuMatchSearchEngine.OrderCandidates(input).Select(candidate => candidate.Id)),
                "scores and stable ties must be deterministic inside each provider group");
        }

        private static void KeepsCanonicalSelectionIndependentFromProjection()
        {
            var candidates = Enumerable.Range(0, 80)
                .Select(index => Candidate("noise-" + index.ToString("D3"), 0, 0.50))
                .Concat(new[] { Candidate("winner", 1, 0.99) })
                .ToList();
            var canonical = DanmuMatchSearchEngine.OrderCanonicalCandidates(candidates);
            var projection = DanmuMatchSearchEngine.OrderCandidates(canonical);
            Assert(DanmuMatchScorer.SelectAutoCandidate(canonical)?.Id == "winner" &&
                   projection.Any(candidate => candidate.Id == "winner"),
                "selection must use the untruncated canonical set and quota must retain later-provider participation");

            var tied = new[] { Candidate("tie-b", 0, 0.99), Candidate("tie-a", 0, 0.99) };
            Assert(DanmuMatchScorer.SelectAutoCandidate(tied) == null,
                "same-site highest-score ties must remain ambiguous regardless of display ordering");
        }

        private static void ClassifiesIncompleteAndCancelledRounds()
        {
            var options = new BoundedSearchPolicyOptions(
                TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
            var policy = new BoundedSearchPolicy(options);
            var completed = new ResultScraper("completed", new[]
            {
                Info("winner", "SPY x FAMILY", "动漫", 25, 2022),
            });
            var failed = new ThrowingScraper("failed");
            var partial = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { completed, failed }, "SPY x FAMILY", "Season 1", 2022, 25,
                    null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(!partial.IsComplete && partial.Decision == "partial-confident" &&
                   partial.SelectedCandidate?.Id == "winner" && partial.Candidates.Count == 1 &&
                   partial.Candidates[0].MatchScore == partial.Candidates[0].Score &&
                   partial.Candidates[0].ScoreOrigin == "search-confidence" &&
                   partial.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "failed"),
                "provider-local failure must retain a confident completed-provider candidate and diagnostics");

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new AbstractScraper[] { completed }, "SPY x FAMILY", "Season 1", 2022, 25,
                        null, null, policy, cancelled.Token)
                    .GetAwaiter().GetResult();
                Assert(result.WasCancelled && result.Decision == "cancelled" && result.SelectedCandidate == null,
                    "whole-operation cancellation must never expose a confirmable provisional candidate");
            }

            using (var childBudget = new CancellationTokenSource())
            {
                childBudget.Cancel();
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new AbstractScraper[] { completed }, "SPY x FAMILY", "Season 1", 2022, 25,
                        null, null, policy, childBudget.Token, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert(!result.WasCancelled && result.Decision == "retryable-incomplete",
                    "child budget exhaustion must remain retryable instead of impersonating parent cancellation");
            }

            var missingParent = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { completed }, null, "Season 1", 2022, 25,
                    null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(missingParent.Decision == "retryable-incomplete" && completed.Keywords.Count == 1 &&
                   missingParent.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "invalid_metadata"),
                "missing parent metadata must be retryable and issue no additional provider call");
        }

        private static void KeepsThreeSeasonInvocationsEquivalent()
        {
            foreach (var season in new[] { "Season 1", "Season 2", "Season 3" })
            {
                var scraper = new ResultScraper(season, Array.Empty<ScraperSearchInfo>());
                DanmuMatchSearchEngine.SearchSeasonAsync(new[] { scraper }, "SPY x FAMILY", season,
                        2022, 12, null, null)
                    .GetAwaiter().GetResult();
                Assert(scraper.Keywords.SequenceEqual(new[] { "SPY x FAMILY" }),
                    "each Series target Season must receive the same identity-bearing plan as a singleton Season");
            }
        }

        private static ScraperSearchInfo Info(string id, string name, string category, int episodes, int year) =>
            new ScraperSearchInfo { Id = id, Name = name, Category = category, EpisodeSize = episodes, Year = year };

        private static DanmuMatchCandidate Candidate(string id, int sourceOrder, double score) =>
            new DanmuMatchCandidate
            {
                Id = id,
                Name = id,
                Site = "site-" + sourceOrder,
                SiteName = "site-" + sourceOrder,
                SourceOrder = sourceOrder,
                Score = score,
                TitleScore = score,
            };

        private static bool IsNonIncreasing(IEnumerable<double> values)
        {
            var materialized = values.ToList();
            return materialized.Zip(materialized.Skip(1), (left, right) => left >= right).All(value => value);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private class ResultScraper : AbstractScraper
        {
            private readonly string _id;
            private readonly List<ScraperSearchInfo> _results;
            public ResultScraper(string id, IEnumerable<ScraperSearchInfo> results) : base(null)
            {
                _id = id;
                _results = results.ToList();
            }
            public List<string> Keywords { get; } = new List<string>();
            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) => Task.FromResult(_results);
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                Keywords.Add(keyword);
                return Task.FromResult(_results);
            }
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private sealed class ThrowingScraper : ResultScraper
        {
            public ThrowingScraper(string id) : base(id, Array.Empty<ScraperSearchInfo>()) { }
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) =>
                throw new InvalidOperationException("fixture failure");
        }
    }
}
