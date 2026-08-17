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
            RetainsManualCustomSearchCandidatesWithoutTitleEvidence();
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

        private static void RetainsManualCustomSearchCandidatesWithoutTitleEvidence()
        {
            var thirdSeason = Info("dandan-s3", "\u4e00\u62f3\u8d85\u4eba \u7b2c\u4e09\u5b63", "anime", 12, 2025);
            var emptyId = Info(string.Empty, "usable title", "anime", 12, 2025);
            var emptyTitle = Info("empty-title", string.Empty, "anime", 12, 2025);
            var movie = Info("movie", "\u4e00\u62f3\u8d85\u4eba \u5267\u573a\u7248", "movie", 1, 2025);

            foreach (var keyword in new[]
            {
                "one punch",
                "one punch man",
                "one punch+man",
                "\u4e00\u62f3 \u8d85\u4eba",
                "\u4e00\u62f3+\u8d85\u4eba",
            })
            {
                Assert(DanmuMatchScorer.IsEligibleSeasonCandidate(
                        thirdSeason, "One Punch Man", "One Punch Man Season 3", keyword),
                    "an explicit alias, space, literal plus, or non-ASCII keyword must retain a Chinese Season title");
                Assert(!DanmuMatchScorer.IsEligibleSeasonCandidate(
                        emptyId, "One Punch Man", "One Punch Man Season 3", keyword) &&
                    !DanmuMatchScorer.IsEligibleSeasonCandidate(
                        emptyTitle, "One Punch Man", "One Punch Man Season 3", keyword) &&
                    !DanmuMatchScorer.IsEligibleSeasonCandidate(
                        movie, "One Punch Man", "One Punch Man Season 3", keyword),
                    "manual discovery must still reject unusable identifiers, titles, and identifiable Movies");

                var dandan = new ResultScraper("dandan", new[] { thirdSeason, emptyId, emptyTitle, movie });
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new AbstractScraper[] { dandan }, "One Punch Man", "One Punch Man Season 3", 2025, 12,
                        keyword, null)
                    .GetAwaiter().GetResult();
                Assert(dandan.Keywords.SequenceEqual(new[] { keyword }) &&
                    result.CanonicalCandidates.Select(candidate => candidate.Id).SequenceEqual(new[] { "dandan-s3" }) &&
                    result.Decision == "manual" && result.SelectedCandidate == null,
                    "manual MatchPreview must keep the DandanPlay third Season while preserving low-confidence manual selection");
            }

            Assert(!DanmuMatchScorer.IsEligibleSeasonCandidate(
                    thirdSeason, "Unrelated Series", "Unrelated Series Season 1", null),
                "automatic searches must continue rejecting unrelated results without title evidence");

            var successful = new ResultScraper("dandan", new[] { thirdSeason });
            var failed = new ThrowingScraper("failed");
            var partial = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { successful, failed }, "One Punch Man", "One Punch Man Season 3", 2025, 12,
                    "one punch man", null)
                .GetAwaiter().GetResult();
            Assert(partial.CanonicalCandidates.Any(candidate => candidate.Id == "dandan-s3") &&
                partial.HasCompletedProviders && partial.Decision == "manual" && partial.SelectedCandidate == null &&
                partial.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "failed"),
                "manual custom search must isolate provider failures without promoting a low-confidence candidate");
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
            Assert(!partial.IsComplete && partial.HasCompletedProviders &&
                   partial.CompletedProviderIds.SequenceEqual(new[] { "completed" }) &&
                   partial.HasProviderLocalFaults && partial.Decision == "confident" &&
                   partial.SelectedCandidate?.Id == "winner" && partial.Candidates.Count == 1 &&
                   partial.Candidates[0].MatchScore == partial.Candidates[0].Score &&
                   partial.Candidates[0].ScoreOrigin == "search-confidence" &&
                   partial.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "failed"),
                "provider-local failure must retain a normally confident completed-provider candidate and diagnostics");

            var ambiguous = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[]
                    {
                        new ResultScraper("ambiguous", new[]
                        {
                            Info("first", "SPY x FAMILY", "动漫", 25, 2022),
                            Info("second", "SPY x FAMILY", "动漫", 25, 2022),
                        }),
                        failed,
                    }, "SPY x FAMILY", "Season 1", 2022, 25, null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(ambiguous.HasCompletedProviders && ambiguous.SelectedCandidate == null &&
                   ambiguous.Decision == "manual" && ambiguous.CanonicalCandidates.Count == 2,
                "a provider fault must not turn an ambiguous completed-provider result into an automatic match");

            var allFailed = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { new ThrowingScraper("failed-only") },
                    "SPY x FAMILY", "Season 1", 2022, 25, null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(!allFailed.HasCompletedProviders && allFailed.HasProviderLocalFaults &&
                   allFailed.Decision == "retryable-incomplete" && allFailed.SelectedCandidate == null,
                "all-provider failure must remain retryable and never select a candidate");

            var selfCancelled = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { new SelfCancellingScraper("self-cancelled") },
                    "SPY x FAMILY", "Season 1", 2022, 25, null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(!selfCancelled.WasCancelled && selfCancelled.HasProviderLocalFaults &&
                   !selfCancelled.HasCompletedProviders && selfCancelled.Decision == "retryable-incomplete",
                "a provider-only cancellation without parent cancellation must remain a retryable provider-local fault");

            var providerPartial = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { new PartialDiagnosticScraper("partial", new[]
                    {
                        Info("partial-winner", "SPY x FAMILY", "动漫", 25, 2022),
                    }) }, "SPY x FAMILY", "Season 1", 2022, 25, null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(providerPartial.HasCompletedProviders && !providerPartial.IsComplete &&
                   providerPartial.HasProviderLocalFaults && providerPartial.SelectedCandidate?.Id == "partial-winner" &&
                   providerPartial.Decision == "confident" &&
                   providerPartial.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "partial_failure"),
                "a provider-native partial failure must retain its completed high-confidence candidate");

            var delayedProvider = new TaskCompletionSource<List<ScraperSearchInfo>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var timeoutPolicy = new BoundedSearchPolicy(new BoundedSearchPolicyOptions(
                TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200)));
            var timedOutSibling = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[]
                    {
                        new ResultScraper("timely", new[] { Info("timely-winner", "SPY x FAMILY", "动漫", 25, 2022) }),
                        new DeferredScraper("timed-out", delayedProvider),
                    }, "SPY x FAMILY", "Season 1", 2022, 25, null, null, timeoutPolicy, CancellationToken.None)
                .GetAwaiter().GetResult();
            delayedProvider.TrySetResult(new List<ScraperSearchInfo>());
            Assert(timedOutSibling.HasCompletedProviders && !timedOutSibling.IsComplete &&
                   timedOutSibling.HasProviderLocalFaults && timedOutSibling.SelectedCandidate?.Id == "timely-winner" &&
                   timedOutSibling.Decision == "confident" &&
                   timedOutSibling.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "timed_out"),
                "a ProviderTimedOut sibling must not make a completed high-confidence candidate provisional");

            var completedEmpty = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { new ResultScraper("empty", Array.Empty<ScraperSearchInfo>()) },
                    "SPY x FAMILY", "Season 1", 2022, 25, null, null, policy, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(completedEmpty.HasCompletedProviders && completedEmpty.IsComplete &&
                   completedEmpty.Decision == "no_match" && completedEmpty.SelectedCandidate == null,
                "a normally completed empty provider result must remain an ordinary no-match result");

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new AbstractScraper[] { completed }, "SPY x FAMILY", "Season 1", 2022, 25,
                        null, null, policy, cancelled.Token)
                    .GetAwaiter().GetResult();
                Assert(result.WasCancelled && !result.HasProviderLocalFaults &&
                       result.Decision == "cancelled" && result.SelectedCandidate == null,
                    "whole-operation cancellation must never expose a confirmable provisional candidate");
            }

            using (var cancelledAfterCompletion = new CancellationTokenSource())
            {
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                        new AbstractScraper[]
                        {
                            new CancellingSuccessfulScraper("completed-before-cancel", new[]
                            {
                                Info("completed-before-cancel", "SPY x FAMILY", "动漫", 25, 2022),
                            }, cancelledAfterCompletion),
                        }, "SPY x FAMILY", "Season 1", 2022, 25, null, null, policy,
                        cancelledAfterCompletion.Token)
                    .GetAwaiter().GetResult();
                Assert(result.WasCancelled && result.SelectedCandidate == null && result.Decision == "cancelled",
                    "parent cancellation after a provider completes must still clear automatic selection");
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

        private sealed class PartialDiagnosticScraper : ResultScraper
        {
            public PartialDiagnosticScraper(string id, IEnumerable<ScraperSearchInfo> results) : base(id, results) { }

            public override async Task<ScraperSearchResult> SearchForApiWithDiagnostics(
                string keyword, CancellationToken cancellationToken)
            {
                return new ScraperSearchResult
                {
                    Candidates = await SearchForApi(keyword).ConfigureAwait(false),
                    Diagnostics = new List<ScraperSearchDiagnostic>
                    {
                        new ScraperSearchDiagnostic { Status = "partial_failure", Message = "fixture partial failure" },
                    },
                };
            }

        }

        private sealed class SelfCancellingScraper : ResultScraper
        {
            public SelfCancellingScraper(string id) : base(id, Array.Empty<ScraperSearchInfo>()) { }

            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                return Task.FromCanceled<List<ScraperSearchInfo>>(cancellation.Token);
            }
        }

        private sealed class DeferredScraper : ResultScraper
        {
            private readonly TaskCompletionSource<List<ScraperSearchInfo>> _result;

            public DeferredScraper(string id, TaskCompletionSource<List<ScraperSearchInfo>> result)
                : base(id, Array.Empty<ScraperSearchInfo>())
            {
                _result = result;
            }

            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) => _result.Task;
        }

        private sealed class CancellingSuccessfulScraper : ResultScraper
        {
            private readonly CancellationTokenSource _cancellation;

            public CancellingSuccessfulScraper(
                string id,
                IEnumerable<ScraperSearchInfo> results,
                CancellationTokenSource cancellation) : base(id, results)
            {
                _cancellation = cancellation;
            }

            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                var result = base.SearchForApi(keyword);
                _cancellation.Cancel();
                return result;
            }
        }
    }
}
