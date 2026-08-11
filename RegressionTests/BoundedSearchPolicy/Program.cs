using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using Emby.Plugin.Danmu.Scraper.Dandan;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Iqiyi;
using Emby.Plugin.Danmu.Scraper.Tencent;
using Emby.Plugin.Danmu.Scraper.Youku;
using Emby.Plugin.Danmu.Scrapers.Mgtv;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Danmu.BoundedSearchPolicyRegression
{
    internal static class Program
    {
        private static int Main()
        {
            KeepsTwoInteractiveDialogOperationsIndependent();
            KeepsTwoDialogsAndAutomaticWorkGloballyProviderBoundedAndFair();
            IncompletePlannedCallsDisableAutomaticSelection();
            RetainsGateUntilLateProviderStops();
            ProviderGateDoesNotStarveOtherProviders();
            DistinguishesProviderAndOverallTimeouts();
            CancelsOperationRegisteredAfterCancelRequest();
            ProviderIdResolutionPropagatesCallerCancellation();
            VerifiesControllerCancelContract();
            ConcreteProviderApiSearchesObserveCancellation();
            VerifiesConcreteProviderTokenForwardingContracts();
            PreservesStableConfiguredProviderOrdering();
            VerifiesEveryIncompleteAutomaticPathStopsBeforeBindingOrDownload();
            Console.WriteLine("Bounded-search policy regression checks passed.");
            return 0;
        }

        private static void ConcreteProviderApiSearchesObserveCancellation()
        {
            var logManager = DispatchProxy.Create<ILogManager, NoOpDispatchProxy>();
            var jsonSerializer = DispatchProxy.Create<IJsonSerializer, NoOpDispatchProxy>();

            VerifyConcretePreCancelledToken(
                httpClient => new Bilibili(logManager, httpClient),
                "Bilibili");
            VerifyConcretePreCancelledToken(
                httpClient => new Dandan(logManager, jsonSerializer, httpClient),
                "Dandan");
            VerifyConcretePreCancelledToken(
                httpClient => new Iqiyi(httpClient, logManager),
                "Iqiyi");
            VerifyConcretePreCancelledToken(
                httpClient => new Mgtv(logManager, httpClient),
                "Mgtv");
            VerifyConcretePreCancelledToken(
                httpClient => new Tencent(logManager, httpClient),
                "Tencent");
            VerifyConcretePreCancelledToken(
                httpClient => new Youku(logManager, httpClient),
                "Youku");
        }

        private static void VerifiesConcreteProviderTokenForwardingContracts()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var providerFiles = new[]
            {
                "Scraper/Bilibili/Bilibili.cs",
                "Scraper/Dandan/Dandan.cs",
                "Scraper/Iqiyi/Iqiyi.cs",
                "Scraper/Mgtv/Mgtv.cs",
                "Scraper/Tencent/Tencent.cs",
                "Scraper/Youku/Youku.cs",
            };

            foreach (var relativePath in providerFiles)
            {
                var source = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
                    .Replace("\r\n", "\n");
                Assert(source.Contains("return SearchForApi(keyword, CancellationToken.None);"),
                    relativePath + " legacy API search must delegate to the cancellation-aware overload");
                Assert(source.Contains("CancellationToken cancellationToken)"),
                    relativePath + " must override the cancellation-aware API search contract");
                Assert(source.Contains("SearchAsync(keyword, cancellationToken)"),
                    relativePath + " must forward the caller token to its provider API search");
            }
        }

        private static void VerifyConcretePreCancelledToken(
            Func<IHttpClient, AbstractScraper> createProvider,
            string providerName)
        {
            var httpClient = DispatchProxy.Create<IHttpClient, NoOpDispatchProxy>();
            var provider = createProvider(httpClient);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                try
                {
                    provider.SearchForApi("cancellation-contract-" + providerName, cancellation.Token)
                        .GetAwaiter().GetResult();
                    throw new InvalidOperationException(
                        providerName + " concrete SearchForApi overload must observe a pre-cancelled caller token");
                }
                catch (OperationCanceledException)
                {
                    // Bilibili owns a standard HttpClient rather than the Emby IHttpClient adapter.
                }
            }
        }


        private static void KeepsTwoInteractiveDialogOperationsIndependent()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromMilliseconds(50),
                interactiveOperationTimeout: TimeSpan.FromSeconds(1),
                automaticOperationTimeout: TimeSpan.FromSeconds(1));
            using (var registry = new SearchOperationRegistry(options))
            {
                Assert(registry.TryBegin("dialog-search-001", SearchOperationScope.Interactive, out var first, out _),
                    "the first dialog must register its client operation id");
                Assert(registry.TryBegin("dialog-search-002", SearchOperationScope.Interactive, out var second, out _),
                    "a second dialog must not replace the first operation");
                Assert(registry.TryCancel("dialog-search-001") && first.IsCancellationRequested,
                    "CancelSearch must cancel only the requested dialog");
                Assert(!second.IsCancellationRequested,
                    "cancelling one dialog must not cancel another dialog's search");
                first.Dispose();
                second.Dispose();
            }
        }

        private static void KeepsTwoDialogsAndAutomaticWorkGloballyProviderBoundedAndFair()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromSeconds(2),
                interactiveOperationTimeout: TimeSpan.FromSeconds(2),
                automaticOperationTimeout: TimeSpan.FromSeconds(2),
                maximumConcurrentProviders: 3);
            var policy = new BoundedSearchPolicy(options);
            using (var registry = new SearchOperationRegistry(options))
            {
                Assert(registry.TryBegin("dialog-gate-001", SearchOperationScope.Interactive, out var firstDialog, out _),
                    "the first dialog operation must register");
                Assert(registry.TryBegin("dialog-gate-002", SearchOperationScope.Interactive, out var secondDialog, out _),
                    "the second dialog operation must register");
                Assert(registry.TryBegin("automatic-gate-001", SearchOperationScope.Automatic, out var automatic, out _),
                    "automatic work must coexist with both dialogs");
                using (firstDialog)
                using (secondDialog)
                using (automatic)
                {
                    var releaseOwner = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var activeSameProvider = 0;
                    var maximumSameProvider = 0;
                    Func<Task<int>, Task<int>> tracked = async providerTask =>
                    {
                        var active = Interlocked.Increment(ref activeSameProvider);
                        UpdateMaximum(ref maximumSameProvider, active);
                        ownerStarted.TrySetResult(true);
                        try
                        {
                            return await providerTask.ConfigureAwait(false);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref activeSameProvider);
                        }
                    };

                    var owner = policy.ExecuteAsync("shared-site", _ => tracked(releaseOwner.Task),
                        firstDialog.CancellationToken);
                    ownerStarted.Task.GetAwaiter().GetResult();
                    var second = policy.ExecuteAsync("shared-site", _ => tracked(Task.FromResult(2)),
                        secondDialog.CancellationToken);
                    var background = policy.ExecuteAsync("shared-site", _ => tracked(Task.FromResult(3)),
                        automatic.CancellationToken);

                    var otherProvider = policy.ExecuteAsync("other-site", _ => Task.FromResult(4),
                        automatic.CancellationToken).GetAwaiter().GetResult();
                    Assert(otherProvider.Status == BoundedSearchExecutionStatus.Completed &&
                           otherProvider.Result == 4 && !second.IsCompleted && !background.IsCompleted,
                        "same-provider dialog/background waiters must not starve a different provider or overlap their owner");

                    releaseOwner.SetResult(1);
                    Assert(owner.GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed &&
                           second.GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed &&
                           background.GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed &&
                           maximumSameProvider == 1,
                        "two dialogs plus automatic work must keep at most one in-flight call for the same provider");
                }
            }
        }

        private static void IncompletePlannedCallsDisableAutomaticSelection()
        {
            var policy = new BoundedSearchPolicy(new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromMilliseconds(20),
                interactiveOperationTimeout: TimeSpan.FromMilliseconds(80),
                automaticOperationTimeout: TimeSpan.FromMilliseconds(80)));
            var fast = new RecordingScraper("fast", Task.FromResult(new List<ScraperSearchInfo>
            {
                new ScraperSearchInfo { Id = "fast-1", Name = "Series Alpha", EpisodeSize = 12, Year = 2024 },
            }));
            var slow = new RecordingScraper("slow", new TaskCompletionSource<List<ScraperSearchInfo>>(
                TaskCreationOptions.RunContinuationsAsynchronously).Task);
            using (var operation = new CancellationTokenSource(TimeSpan.FromMilliseconds(70)))
            {
                var result = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { fast, slow },
                    "Series Alpha",
                    "Season 1",
                    2024,
                    12,
                    null,
                    null,
                    policy,
                    operation.Token).GetAwaiter().GetResult();
                Assert(!result.IsComplete && result.Candidates.Any(candidate => candidate.Site == "fast"),
                    "a planned timeout must preserve successful candidates but mark the aggregate incomplete");
                Assert(result.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "timed_out") &&
                       result.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "unstarted"),
                    "a late non-cooperative call must report both timeout and later unstarted planned work");
                Assert(result.CompletionDiagnostics.Select(diagnostic => diagnostic.Provider)
                           .SequenceEqual(new[] { "fast", "fast", "slow", "slow" }) &&
                       result.CompletionDiagnostics.Select(diagnostic => diagnostic.Status)
                           .SequenceEqual(new[] { "completed", "completed", "timed_out", "unstarted" }),
                    "partial diagnostics must retain configured-provider and planned-term order regardless of completion timing");
            }
        }

        private static void RetainsGateUntilLateProviderStops()
        {
            var policy = new BoundedSearchPolicy(new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromMilliseconds(20),
                interactiveOperationTimeout: TimeSpan.FromMilliseconds(100),
                automaticOperationTimeout: TimeSpan.FromMilliseconds(100),
                maximumConcurrentProviders: 1));
            var late = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var timedOut = policy.ExecuteAsync("late-site", ignored => late.Task, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(timedOut.Status == BoundedSearchExecutionStatus.ProviderTimedOut && !timedOut.Settlement.IsCompleted,
                "a timed-out provider must retain its lease until its actual task completes");
            using (var waitingOperation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30)))
            {
                var blocked = policy.ExecuteAsync("other-site", ignored => Task.FromResult(1), waitingOperation.Token)
                    .GetAwaiter().GetResult();
                Assert(blocked.Status == BoundedSearchExecutionStatus.Cancelled,
                    "the global gate wait must consume the dialog operation budget");
            }

            late.SetResult(1);
            timedOut.Settlement.GetAwaiter().GetResult();
            Assert(policy.ExecuteAsync("other-site", ignored => Task.FromResult(1), CancellationToken.None)
                       .GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed,
                "the gate must be released after the late provider finishes");
        }

        private static void ProviderGateDoesNotStarveOtherProviders()
        {
            var policy = new BoundedSearchPolicy(new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromSeconds(1),
                interactiveOperationTimeout: TimeSpan.FromSeconds(2),
                automaticOperationTimeout: TimeSpan.FromSeconds(2),
                maximumConcurrentProviders: 3));
            var late = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = policy.ExecuteAsync("same-site", ignored => late.Task, CancellationToken.None);
            var queuedOne = policy.ExecuteAsync("same-site", ignored => Task.FromResult(2), CancellationToken.None);
            var queuedTwo = policy.ExecuteAsync("same-site", ignored => Task.FromResult(3), CancellationToken.None);
            var other = policy.ExecuteAsync("other-site", ignored => Task.FromResult(4), CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(other.Status == BoundedSearchExecutionStatus.Completed,
                "same-provider waiters must not consume all global provider slots");

            late.SetResult(1);
            Assert(first.GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed,
                "the first same-provider call must settle normally");
            Assert(queuedOne.GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed &&
                   queuedTwo.GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed,
                "queued same-provider calls must run serially after the owner releases its gate");
        }

        private static void DistinguishesProviderAndOverallTimeouts()
        {
            var providerPolicy = new BoundedSearchPolicy(new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromMilliseconds(20),
                interactiveOperationTimeout: TimeSpan.FromSeconds(1),
                automaticOperationTimeout: TimeSpan.FromSeconds(1)));
            var providerLate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var providerTimeout = providerPolicy.ExecuteAsync("provider-timeout", _ => providerLate.Task,
                    CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(providerTimeout.Status == BoundedSearchExecutionStatus.ProviderTimedOut &&
                   !providerTimeout.Settlement.IsCompleted,
                "the per-provider deadline must report ProviderTimedOut and retain the late-task gate");
            providerLate.SetResult(1);
            providerTimeout.Settlement.GetAwaiter().GetResult();

            var overallOptions = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromSeconds(1),
                interactiveOperationTimeout: TimeSpan.FromSeconds(1),
                automaticOperationTimeout: TimeSpan.FromMilliseconds(35));
            var overallPolicy = new BoundedSearchPolicy(overallOptions);
            using (var registry = new SearchOperationRegistry(overallOptions))
            {
                Assert(registry.TryBegin("automatic-timeout-001", SearchOperationScope.Automatic,
                    out var operation, out _), "the automatic overall-timeout operation must register");
                using (operation)
                {
                    var overallLate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var overallTimeout = overallPolicy.ExecuteAsync("overall-timeout", _ => overallLate.Task,
                            operation.CancellationToken)
                        .GetAwaiter().GetResult();
                    Assert(overallTimeout.Status == BoundedSearchExecutionStatus.Cancelled &&
                           operation.IsCancellationRequested && !overallTimeout.Settlement.IsCompleted,
                        "the automatic overall deadline must cancel the operation distinctly from provider timeout");
                    overallLate.SetResult(1);
                    overallTimeout.Settlement.GetAwaiter().GetResult();
                }
            }
        }

        private static void CancelsOperationRegisteredAfterCancelRequest()
        {
            using (var registry = new SearchOperationRegistry())
            {
                Assert(registry.TryCancel("pre-cancel-001"),
                    "an early cancel must reserve a bounded cancellation tombstone");
                Assert(registry.TryBegin("pre-cancel-001", SearchOperationScope.Interactive, out var operation, out _),
                    "the subsequent request must register deterministically");
                using (operation)
                {
                    Assert(operation.IsCancellationRequested,
                        "an operation registered after cancellation must start cancelled rather than run");
                }
            }
        }

        private static void ProviderIdResolutionPropagatesCallerCancellation()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var scraper = new CancellingProviderIdScraper(cancellation);
                var episode = new Episode { IndexNumber = 1 };
                episode.ProviderIds[scraper.ProviderId] = "cancelled-direct-id";
                var propagated = false;
                try
                {
                    DanmuProviderIdResolver.ResolveAsync(
                            new AbstractScraper[] { scraper },
                            new BaseItem[] { episode },
                            null,
                            cancellationToken: cancellation.Token)
                        .GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    propagated = true;
                }

                Assert(propagated,
                    "caller cancellation during ProviderId resolution must propagate instead of becoming unresolved");
            }

            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var resolver = File.ReadAllText(Path.Combine(root, "Scraper", "DanmuProviderIdResolver.cs"));
            const string cancellationBoundary =
                "catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)";
            Assert(controller.Contains(cancellationBoundary, StringComparison.Ordinal) &&
                   resolver.Contains(cancellationBoundary, StringComparison.Ordinal),
                "composite direct reconstruction and ProviderId resolution must preserve caller cancellation");
        }

        private static void VerifiesControllerCancelContract()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            Assert(controller.Contains("[DataMember(Name=\"searchOperationId\")]", StringComparison.Ordinal) &&
                   controller.Contains("[DataMember(Name=\"searchScope\")]", StringComparison.Ordinal) &&
                   controller.Contains("SearchOperations.TryCancel(searchOperationId)", StringComparison.Ordinal) &&
                   controller.Contains("if (!search.IsComplete)", StringComparison.Ordinal),
                "Controller must bind operation scalar fields, route CancelSearch, and gate automatic selection on complete search coverage");
        }

        private static void PreservesStableConfiguredProviderOrdering()
        {
            var candidates = new[]
            {
                Candidate("later-z", "Later", 1),
                Candidate("first-b", "First", 0),
                Candidate("later-a", "Later", 1),
                Candidate("first-a", "First", 0),
            };
            var forward = DanmuMatchSearchEngine.OrderCandidates(candidates).Select(candidate => candidate.Id).ToArray();
            var reverse = DanmuMatchSearchEngine.OrderCandidates(candidates.Reverse()).Select(candidate => candidate.Id).ToArray();
            Assert(forward.SequenceEqual(new[] { "first-a", "first-b", "later-a", "later-z" }) &&
                   reverse.SequenceEqual(forward),
                "equal-score results must be stable by configured provider order and deterministic identity tie-breakers");
        }

        private static void VerifiesEveryIncompleteAutomaticPathStopsBeforeBindingOrDownload()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var helper = File.ReadAllText(Path.Combine(root, "LibraryManagerEventsHelper.cs"))
                .Replace("\r\n", "\n");

            var movie = Slice(helper,
                "var movieSearch = await DanmuMatchSearchEngine.SearchMovieAsync(",
                "movieMatchSearched = true;");
            Assert(movie.Contains("if (!IsCompleteAutomaticSearch(movieSearch))") &&
                   movie.Contains("else\n                                    {\n                                        selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate") &&
                   movie.IndexOf("LogIncompleteAutomaticSearch", StringComparison.Ordinal) <
                   movie.IndexOf("selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate", StringComparison.Ordinal),
                "an incomplete automatic Movie round must leave selection null before any binding or download path");

            var season = Slice(helper,
                "var search = await DanmuMatchSearchEngine.SearchSeasonAsync(\n                                scrapers,",
                "var media = await selectedScraper.GetMedia(season, selectedMediaId);");
            var seasonGuard = season.IndexOf("if (!IsCompleteAutomaticSearch(search))", StringComparison.Ordinal);
            var seasonContinue = season.IndexOf("continue;", seasonGuard, StringComparison.Ordinal);
            var seasonSelect = season.IndexOf("selectedCandidate = DanmuMatchScorer.SelectAutoCandidate", StringComparison.Ordinal);
            Assert(seasonGuard >= 0 && seasonContinue > seasonGuard && seasonSelect > seasonContinue,
                "an incomplete automatic Season round must continue before candidate selection, binding, or download");

            var residual = Slice(helper,
                "while (plan.UnmatchedRuns.Count > 0)",
                "if (plan.Mappings.Count == 0) return false;");
            var residualGuard = residual.IndexOf("if (!IsCompleteAutomaticSearch(search))", StringComparison.Ordinal);
            var residualAbort = residual.IndexOf("return false;", residualGuard, StringComparison.Ordinal);
            var residualSelect = residual.IndexOf("SelectSupplementalCandidate", StringComparison.Ordinal);
            Assert(residualGuard >= 0 && residualAbort > residualGuard && residualSelect > residualAbort,
                "an incomplete residual round must abort before supplemental selection, binding, or any mapped download");
        }

        private static DanmuMatchCandidate Candidate(string id, string site, int sourceOrder)
        {
            return new DanmuMatchCandidate
            {
                Id = id,
                Name = id,
                Site = site,
                SiteName = site,
                SourceOrder = sourceOrder,
                Score = 0.50,
                TitleScore = 0.50,
            };
        }

        private static string Slice(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert(start >= 0 && end > start,
                "source-contract markers must remain discoverable: " + startMarker + " -> " + endMarker);
            return source.Substring(start, end - start);
        }

        private static void UpdateMaximum(ref int target, int value)
        {
            while (true)
            {
                var observed = Volatile.Read(ref target);
                if (value <= observed || Interlocked.CompareExchange(ref target, value, observed) == observed)
                {
                    return;
                }
            }
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Emby.Plugin.Danmu.csproj")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate the plugin repository root.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class RecordingScraper : AbstractScraper
        {
            private readonly string _id;
            private readonly Task<List<ScraperSearchInfo>> _result;

            public RecordingScraper(string id, Task<List<ScraperSearchInfo>> result) : base(null)
            {
                _id = id;
                _result = result;
            }

            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) => _result;
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) => _result;
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => Task.FromResult<ScraperEpisode>(null);
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) => Task.FromResult<ScraperDanmaku>(null);
        }

        private sealed class CancellingProviderIdScraper : AbstractScraper
        {
            private readonly CancellationTokenSource _cancellation;

            public CancellingProviderIdScraper(CancellationTokenSource cancellation) : base(null)
            {
                _cancellation = cancellation;
            }

            public override string Name => "cancelling-provider";
            public override string ProviderName => Name;
            public override string ProviderId => "CancellingID";
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) =>
                Task.FromResult<ScraperMedia>(null);
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
            {
                _cancellation.Cancel();
                return Task.FromCanceled<ScraperEpisode>(_cancellation.Token);
            }
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) =>
                Task.FromResult<ScraperDanmaku>(null);
        }
    }

    public class NoOpDispatchProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            var returnType = targetMethod.ReturnType;
            if (returnType == typeof(ILogger))
            {
                return DispatchProxy.Create<ILogger, NoOpDispatchProxy>();
            }

            if (returnType == typeof(void))
            {
                return null;
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }

}
