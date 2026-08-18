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
            ExplicitCancellationDisablesAutomaticSelection();
            RetainsGateUntilCancelledProviderStops();
            ProviderGateDoesNotStarveOtherProviders();
            IgnoresLegacyDeadlinesAndRequiresExplicitCancellation();
            ConcurrentRegistryDisposalCannotPublishUndeadOperation();
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
                Assert(source.Contains("SearchAsync(keyword, cancellationToken)") ||
                       source.Contains("SearchMergedAsync(keyword, cancellationToken)") ||
                       source.Contains("SearchMergedAsync(\n                keyword,\n                cancellationToken,"),
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
            var options = new BoundedSearchPolicyOptions();
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
            var options = new BoundedSearchPolicyOptions(maximumConcurrentProviders: 3);
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

        private static void ExplicitCancellationDisablesAutomaticSelection()
        {
            var policy = new BoundedSearchPolicy();
            var fast = new RecordingScraper("fast", Task.FromResult(new List<ScraperSearchInfo>
            {
                new ScraperSearchInfo { Id = "fast-1", Name = "Series Alpha", EpisodeSize = 12, Year = 2024 },
            }));
            var slowResult = new TaskCompletionSource<List<ScraperSearchInfo>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var slow = new RecordingScraper("slow", slowResult.Task);
            using (var operation = new CancellationTokenSource())
            {
                var search = DanmuMatchSearchEngine.SearchSeasonAsync(
                    new AbstractScraper[] { fast, slow },
                    "Series Alpha",
                    "Series Alpha Part 2",
                    2024,
                    12,
                    null,
                    null,
                    policy,
                    operation.Token);
                AwaitWithWatchdog(slow.Started, "the controllable provider must start");
                Assert(!search.IsCompleted,
                    "shared search must keep awaiting a provider until explicit cancellation");
                operation.Cancel();
                var result = AwaitWithWatchdog(search,
                    "explicit search cancellation must return without waiting for a non-cooperative provider");
                Assert(result.WasCancelled && !result.IsComplete &&
                       result.Decision == "cancelled" && result.SelectedCandidate == null,
                    "explicit cancellation may retain diagnostics candidates but must never auto-select one");
                Assert(!result.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "timed_out") &&
                       result.CompletionDiagnostics.Any(diagnostic => diagnostic.Status == "unstarted"),
                    "explicit cancellation must report unstarted work without manufacturing a timeout");
                Assert(result.CompletionDiagnostics.Select(diagnostic => diagnostic.Provider)
                           .SequenceEqual(new[] { "fast", "fast", "slow", "slow" }) &&
                       result.CompletionDiagnostics.Select(diagnostic => diagnostic.Status)
                           .SequenceEqual(new[] { "completed", "completed", "unstarted", "unstarted" }),
                    "cancelled diagnostics must retain configured-provider and planned-term order");
                slowResult.SetResult(new List<ScraperSearchInfo>());
            }
        }

        private static void RetainsGateUntilCancelledProviderStops()
        {
            var policy = new BoundedSearchPolicy(
                new BoundedSearchPolicyOptions(maximumConcurrentProviders: 1));
            var late = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var ownerCancellation = new CancellationTokenSource())
            {
                var ownerTask = policy.ExecuteAsync("late-site", ignored =>
                {
                    started.TrySetResult(true);
                    return late.Task;
                }, ownerCancellation.Token);
                AwaitWithWatchdog(started.Task, "the non-cooperative provider must start");
                Assert(!ownerTask.IsCompleted,
                    "elapsed time must not end an active shared search");
                ownerCancellation.Cancel();
                var cancelled = AwaitWithWatchdog(ownerTask,
                    "explicit cancellation must return the provider outcome promptly");
                Assert(cancelled.Status == BoundedSearchExecutionStatus.Cancelled &&
                       !cancelled.Settlement.IsCompleted,
                    "a cancelled provider must retain its lease until its actual task completes");

                using (var waitingCancellation = new CancellationTokenSource())
                {
                    var sameSite = policy.ExecuteAsync(
                        "late-site", ignored => Task.FromResult(1), waitingCancellation.Token);
                    var otherSite = policy.ExecuteAsync(
                        "other-site", ignored => Task.FromResult(2), waitingCancellation.Token);
                    Assert(!sameSite.IsCompleted && !otherSite.IsCompleted,
                        "both the provider-local and global gate must remain held before settlement");
                    waitingCancellation.Cancel();
                    Assert(AwaitWithWatchdog(sameSite, "same-site gate waiter cancellation").Status ==
                               BoundedSearchExecutionStatus.Cancelled &&
                           AwaitWithWatchdog(otherSite, "global gate waiter cancellation").Status ==
                               BoundedSearchExecutionStatus.Cancelled,
                        "gate waiters must observe explicit cancellation while the owner settles");
                }

                late.SetResult(1);
                AwaitWithWatchdog(cancelled.Settlement, "cancelled provider settlement");
            }
            Assert(policy.ExecuteAsync("other-site", ignored => Task.FromResult(1), CancellationToken.None)
                       .GetAwaiter().GetResult().Status == BoundedSearchExecutionStatus.Completed,
                "the gate must be released after the late provider finishes");
        }

        private static void ProviderGateDoesNotStarveOtherProviders()
        {
            var policy = new BoundedSearchPolicy(
                new BoundedSearchPolicyOptions(maximumConcurrentProviders: 3));
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

        private static void IgnoresLegacyDeadlinesAndRequiresExplicitCancellation()
        {
            var legacyOptions = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.Zero,
                interactiveOperationTimeout: TimeSpan.Zero,
                automaticOperationTimeout: TimeSpan.Zero);
            Assert(legacyOptions.ProviderCallTimeout == Timeout.InfiniteTimeSpan &&
                   legacyOptions.InteractiveOperationTimeout == Timeout.InfiniteTimeSpan &&
                   legacyOptions.AutomaticOperationTimeout == Timeout.InfiniteTimeSpan,
                "legacy timeout settings must resolve to explicit no-deadline compatibility values");
            var policy = new BoundedSearchPolicy(legacyOptions);
            using (var registry = new SearchOperationRegistry(legacyOptions))
            {
                Assert(registry.TryBegin("automatic-search-001", SearchOperationScope.Automatic,
                    out var operation, out _), "the automatic operation must register");
                using (operation)
                {
                    var late = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var execution = policy.ExecuteAsync("provider-without-deadline", _ =>
                    {
                        started.TrySetResult(true);
                        return late.Task;
                    }, operation.CancellationToken);
                    AwaitWithWatchdog(started.Task, "the no-deadline provider must start");
                    Assert(!operation.IsCancellationRequested && !execution.IsCompleted,
                        "neither provider nor automatic operation may self-cancel from elapsed time");
                    Assert(registry.TryCancel("automatic-search-001"),
                        "the registered automatic operation must accept explicit cancellation");
                    var cancelled = AwaitWithWatchdog(execution,
                        "explicit automatic cancellation must return promptly");
                    Assert(cancelled.Status == BoundedSearchExecutionStatus.Cancelled &&
                           operation.IsCancellationRequested && !cancelled.Settlement.IsCompleted,
                        "only explicit cancellation may end the operation before provider settlement");
                    late.SetResult(1);
                    AwaitWithWatchdog(cancelled.Settlement, "explicitly cancelled provider settlement");
                }
            }
        }

        private static void ConcurrentRegistryDisposalCannotPublishUndeadOperation()
        {
            for (var iteration = 0; iteration < 128; iteration++)
            {
                var registry = new SearchOperationRegistry();
                SearchOperationRegistry.SearchOperationLease lease = null;
                using (var start = new ManualResetEventSlim(false))
                {
                    var begin = Task.Run(() =>
                    {
                        start.Wait();
                        registry.TryBegin(
                            "dispose-race-" + iteration.ToString("D3"),
                            SearchOperationScope.Interactive,
                            out lease,
                            out _);
                    });
                    var dispose = Task.Run(() =>
                    {
                        start.Wait();
                        registry.Dispose();
                    });
                    start.Set();
                    AwaitWithWatchdog(Task.WhenAll(begin, dispose),
                        "concurrent search-operation registration and disposal");
                }

                Assert(registry.ActiveOperationCount == 0,
                    "concurrent Dispose must not leave a newly published operation active without a deadline");
                if (lease != null)
                {
                    var observedToken = lease.CancellationToken;
                    Assert(observedToken.IsCancellationRequested && lease.IsCancellationRequested,
                        "a lease returned across registry disposal must expose a stable cancelled token without throwing");
                }
                lease?.Dispose();
                registry.Dispose();
            }

            var disposedRegistry = new SearchOperationRegistry();
            Assert(disposedRegistry.TryBegin(
                    "stable-disposed-token",
                    SearchOperationScope.Interactive,
                    out var disposedLease,
                    out _),
                "the deterministic disposed-token fixture must publish a lease first");
            disposedRegistry.Dispose();
            Assert(disposedLease.CancellationToken.IsCancellationRequested &&
                   disposedLease.IsCancellationRequested,
                "a registry-disposed lease must retain an inspectable cancelled token after its source is disposed");
            disposedLease.Dispose();

            using (var registry = new SearchOperationRegistry())
            {
                Assert(registry.TryBegin(
                        "stable-live-token",
                        SearchOperationScope.Interactive,
                        out var liveLease,
                        out _),
                    "an undisposed registry must still publish an ordinary active lease");
                using (liveLease)
                {
                    var liveToken = liveLease.CancellationToken;
                    Assert(!liveToken.IsCancellationRequested && !liveLease.IsCancellationRequested,
                        "a normally published lease must remain active before explicit cancellation");
                    Assert(registry.TryCancel(liveLease.OperationId) &&
                           liveToken.IsCancellationRequested && liveLease.IsCancellationRequested,
                        "the stable token snapshot must continue observing explicit cancellation");
                }
            }

            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var source = File.ReadAllText(Path.Combine(root, "Core", "SearchOperationRegistry.cs"));
            var addIndex = source.IndexOf("if (!_operations.TryAdd(normalizedId, entry))", StringComparison.Ordinal);
            var postAddCheckIndex = source.IndexOf(
                "if (Volatile.Read(ref _disposed) != 0)",
                addIndex + 1,
                StringComparison.Ordinal);
            var exactRemoveIndex = source.IndexOf(
                "TryRemoveExact(normalizedId, entry)",
                postAddCheckIndex + 1,
                StringComparison.Ordinal);
            var cancelIndex = source.IndexOf(
                "entry.Source.Cancel();",
                exactRemoveIndex + 1,
                StringComparison.Ordinal);
            var disposeIndex = source.IndexOf(
                "entry.Source.Dispose();",
                cancelIndex + 1,
                StringComparison.Ordinal);
            Assert(addIndex >= 0 && postAddCheckIndex > addIndex &&
                   exactRemoveIndex > postAddCheckIndex && cancelIndex > exactRemoveIndex &&
                   disposeIndex > cancelIndex &&
                   source.Contains("new KeyValuePair<string, Entry>(operationId, expected)",
                       StringComparison.Ordinal) &&
                   source.Contains("Token = source.Token;", StringComparison.Ordinal) &&
                   source.Contains(": entry.Token;", StringComparison.Ordinal),
                "TryBegin must recheck disposal after TryAdd, remove only its exact entry, cancel and dispose it, and expose a stable token snapshot");
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
            var resolver = File.ReadAllText(Path.Combine(root, "Scraper", "DanmuProviderIdResolver.cs"));
            const string cancellationBoundary =
                "catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)";
            Assert(resolver.Contains(cancellationBoundary, StringComparison.Ordinal),
                "single-item ProviderId resolution must preserve caller cancellation");

            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var batchStart = controller.IndexOf(
                "private async Task<CompositePlanBuild> BuildCompositePlanAsync(",
                StringComparison.Ordinal);
            var batchEnd = controller.IndexOf(
                "private static DanmuCompositeSeasonSelection CloneSeasonPlanSelection(",
                batchStart,
                StringComparison.Ordinal);
            Assert(batchStart >= 0 && batchEnd > batchStart,
                "batch Season plan builder source boundary must remain discoverable");
            var batchBuilder = controller.Substring(batchStart, batchEnd - batchStart);
            Assert(!batchBuilder.Contains("DanmuProviderIdResolver", StringComparison.Ordinal),
                "r5 batch Series/Season reconstruction must not read Episode ProviderIds");
        }

        private static void VerifiesControllerCancelContract()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            Assert(controller.Contains("[DataMember(Name=\"searchOperationId\")]", StringComparison.Ordinal) &&
                   controller.Contains("[DataMember(Name=\"searchScope\")]", StringComparison.Ordinal) &&
                   controller.Contains("SearchOperations.TryCancel(searchOperationId)", StringComparison.Ordinal) &&
                   controller.Contains("if (search.WasCancelled)", StringComparison.Ordinal) &&
                   CountOccurrences(controller, "if (!search.HasCompletedProviders && !search.IsComplete)") == 3 &&
                   !controller.Contains("\"partial-confident\"", StringComparison.Ordinal),
                "Movie, Season/whole-Series, and temporary-range previews must prioritize cancellation and only retry when no provider completed");
            var temporary = Slice(controller,
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(",
                "private async Task PopulateCompositePreviewIfRequired(");
            Assert(temporary.Contains("response.Status = \"cancelled\"", StringComparison.Ordinal) &&
                   temporary.Contains("response.Status = response.Candidates.Count == 0 ? \"no_match\" : \"ambiguous\"", StringComparison.Ordinal) &&
                   temporary.Contains("\"manual-selection-required\"", StringComparison.Ordinal),
                "a temporary range with completed-provider candidates must return an ordinary selectable response, while cancellation stays fail-closed");
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
            Assert(movie.Contains("if (!CanUseAutomaticSearch(movieSearch))") &&
                   movie.Contains("else\n                                    {\n                                        selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate") &&
                   movie.IndexOf("LogIncompleteAutomaticSearch", StringComparison.Ordinal) <
                   movie.IndexOf("selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate", StringComparison.Ordinal),
                "an incomplete automatic Movie round must leave selection null before any binding or download path");

            var season = Slice(helper,
                "var search = await DanmuMatchSearchEngine.SearchSeasonAsync(\n                                scrapers,",
                "var media = await selectedScraper.GetMedia(season, selectedMediaId);");
            var seasonGuard = season.IndexOf("if (!CanUseAutomaticSearch(search))", StringComparison.Ordinal);
            var seasonContinue = season.IndexOf("continue;", seasonGuard, StringComparison.Ordinal);
            var seasonSelect = season.IndexOf("selectedCandidate = DanmuMatchScorer.SelectAutoCandidate", StringComparison.Ordinal);
            Assert(seasonGuard >= 0 && seasonContinue > seasonGuard && seasonSelect > seasonContinue,
                "an incomplete automatic Season round must continue before candidate selection, binding, or download");

            var automatic = Slice(helper,
                "private async Task<bool> DownloadAutomaticSeasonWithCompositePlan(",
                "private sealed class AutomaticSeasonPlanSnapshot");
            Assert(!automatic.Contains("while (plan.UnmatchedRuns.Count > 0)", StringComparison.Ordinal) &&
                   !automatic.Contains("SearchSeasonAsync", StringComparison.Ordinal) &&
                   !automatic.Contains("SelectResidualCandidate", StringComparison.Ordinal) &&
                   CountOccurrences(automatic, "CompositeSeasonPlanner.TryApplySegmentResolved(") == 1,
                "automatic Season matching must apply one initial selection through the shared resolver without residual search or looping");

            var incompleteGuard = automatic.IndexOf(
                "if (plan.Mappings.Count == 0 || plan.UnmatchedRuns.Count > 0) return false;",
                StringComparison.Ordinal);
            var preflight = automatic.IndexOf(
                "var preflight = await RebuildAutomaticPlanAsync(", StringComparison.Ordinal);
            var staleGuard = automatic.IndexOf(
                "preflight.Plan == null || !string.Equals(", preflight, StringComparison.Ordinal);
            var staleAbort = automatic.IndexOf("return false;", staleGuard, StringComparison.Ordinal);
            var beginWrite = automatic.IndexOf("BeginCompositeSeasonWrite(", StringComparison.Ordinal);
            var download = automatic.IndexOf("DownloadEpisodeForProgress(", StringComparison.Ordinal);
            var episodeBinding = automatic.IndexOf("PersistDownloadProviderIdAsync(", StringComparison.Ordinal);
            var seasonBinding = automatic.IndexOf("UpsertSeasonDisplayMirrorAsync(", StringComparison.Ordinal);
            Assert(incompleteGuard >= 0 && preflight > incompleteGuard && staleGuard > preflight &&
                   staleAbort > staleGuard && beginWrite > staleAbort && download > beginWrite &&
                   episodeBinding > download && seasonBinding > episodeBinding,
                "zero-mapping, unmatched, or stale automatic plans must return before the write lease, download, or binding paths");

            var rebuild = Slice(helper,
                "private async Task<AutomaticSeasonPlanSnapshot> RebuildAutomaticPlanAsync(",
                "internal static bool CanUseAutomaticSearch(");
            Assert(CountOccurrences(rebuild, "CompositeSeasonPlanner.TryApplySegmentResolved(") == 1 &&
                   !rebuild.Contains("SearchSeasonAsync", StringComparison.Ordinal) &&
                   !rebuild.Contains("DownloadEpisodeForProgress(", StringComparison.Ordinal) &&
                   !rebuild.Contains("PersistDownloadProviderIdAsync(", StringComparison.Ordinal) &&
                   !rebuild.Contains("UpsertSeasonDisplayMirrorAsync(", StringComparison.Ordinal) &&
                   !rebuild.Contains("BeginCompositeSeasonWrite(", StringComparison.Ordinal),
                "automatic rebuild must use the shared resolver and keep every stale or failed reconstruction read-only");
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

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private static void AwaitWithWatchdog(Task task, string operation)
        {
            if (Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult() != task)
            {
                throw new InvalidOperationException("Regression watchdog expired: " + operation);
            }

            task.GetAwaiter().GetResult();
        }

        private static TResult AwaitWithWatchdog<TResult>(Task<TResult> task, string operation)
        {
            AwaitWithWatchdog((Task)task, operation);
            return task.GetAwaiter().GetResult();
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
            private readonly TaskCompletionSource<bool> _started =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public RecordingScraper(string id, Task<List<ScraperSearchInfo>> result) : base(null)
            {
                _id = id;
                _result = result;
            }

            public override string Name => _id;
            public override string ProviderName => _id;
            public override string ProviderId => _id;
            public Task Started => _started.Task;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) => _result;
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
            {
                _started.TrySetResult(true);
                return _result;
            }
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
