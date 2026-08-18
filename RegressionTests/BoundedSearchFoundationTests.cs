using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.RegressionTests
{
    /// <summary>
    /// Independent r2 foundation checks. The main regression dispatcher can
    /// invoke Run when the controller integration lands without coupling this
    /// primitive coverage to that later work.
    /// </summary>
    public static class BoundedSearchFoundationTests
    {
        public static void Run()
        {
            PreservesScalarJsonContracts();
            RegistersClientOperationIdsWithoutDeadlines();
            RetainsGateUntilExplicitlyCancelledProviderSettles();
        }

        private static void PreservesScalarJsonContracts()
        {
            Assert(DanmuCompositeSeasonSelectionJson.TryParse(null, out var emptySelections, out _) &&
                   emptySelections.Count == 0,
                "empty compositeSelections must remain backward compatible");
            Assert(DanmuCompositeSeasonSelectionJson.TryParse("[]", out var explicitEmptySelections, out _) &&
                   explicitEmptySelections.Count == 0,
                "an empty compositeSelections array must be accepted");
            Assert(!DanmuCompositeSeasonSelectionJson.TryParse("[", out _, out var malformedSelectionError) &&
                   !string.IsNullOrWhiteSpace(malformedSelectionError),
                "malformed compositeSelections must be rejected with an error");

            var excessiveSelections = "[" + string.Join(",", Enumerable.Repeat("{}", DanmuCompositeSeasonSelectionJson.MaximumSelectionCount + 1)) + "]";
            Assert(!DanmuCompositeSeasonSelectionJson.TryParse(excessiveSelections, out _, out _),
                "compositeSelections must enforce its item limit");

            Assert(DanmuExcludedLocalEpisodeItemIdsJson.TryParse("[\"episode-a\",\"episode-b\"]", out var exclusions, out _) &&
                   exclusions.SequenceEqual(new[] { "episode-a", "episode-b" }),
                "excluded local Episode ids must decode from the scalar JSON array");
            Assert(DanmuExcludedLocalEpisodeItemIdsJson.TryParse("[\"episode-a\",\"EPISODE-A\"]", out var deduplicated, out _) &&
                   deduplicated.SequenceEqual(new[] { "episode-a" }),
                "duplicate excluded local Episode ids must be normalized case-insensitively");
            Assert(!DanmuExcludedLocalEpisodeItemIdsJson.TryParse("{\"id\":\"episode-a\"}", out _, out _),
                "non-array excluded local Episode JSON must be rejected");

            var responseTypes = new[]
            {
                typeof(DanmuMatchPreviewResult),
                typeof(DanmuItemMatchResult),
                typeof(DanmuSeasonMatchResult),
            };
            foreach (var responseType in responseTypes)
            {
                Assert(responseType.GetProperty("ResolvedScopeType") != null &&
                       responseType.GetProperty("ResolvedScopeItemId") != null &&
                       responseType.GetProperty("SearchScope") != null &&
                       responseType.GetProperty("SearchOperationId") != null &&
                       responseType.GetProperty("SearchCompletionDiagnostics") != null &&
                       responseType.GetProperty("SelectedCandidate") != null,
                    "every match preview response must expose the additive r2 search contract");
            }
        }

        private static void RegistersClientOperationIdsWithoutDeadlines()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.Zero,
                interactiveOperationTimeout: TimeSpan.Zero,
                automaticOperationTimeout: TimeSpan.Zero);
            Assert(options.ProviderCallTimeout == Timeout.InfiniteTimeSpan &&
                   options.InteractiveOperationTimeout == Timeout.InfiniteTimeSpan &&
                   options.AutomaticOperationTimeout == Timeout.InfiniteTimeSpan,
                "legacy timeout inputs must be inert compatibility shims");
            using (var registry = new SearchOperationRegistry(options))
            {
                Assert(!registry.TryBegin("short", SearchOperationScope.Interactive, out _, out _),
                    "short client operation ids must be rejected");
                Assert(registry.TryBegin("interactive-01", SearchOperationScope.Interactive, out var interactive, out _),
                    "a valid client operation id must register a server CTS");
                Assert(!registry.TryBegin("interactive-01", SearchOperationScope.Interactive, out _, out _),
                    "an active operation id must not be replaced");
                Assert(registry.TryCancel("interactive-01") && interactive.IsCancellationRequested,
                    "cancel must address the registered client operation id");
                interactive.Dispose();
                Assert(!registry.IsActive("interactive-01"),
                    "disposing an operation lease must remove its server CTS");

                Assert(registry.TryBegin("interactive-02", SearchOperationScope.Interactive,
                    out var interactiveWithoutDeadline, out _),
                    "a second valid interactive operation should register");
                Assert(registry.TryBegin("automatic-0002", SearchOperationScope.Automatic,
                    out var automaticWithoutDeadline, out _),
                    "a valid automatic operation should register");
                Assert(!interactiveWithoutDeadline.IsCancellationRequested &&
                       !automaticWithoutDeadline.IsCancellationRequested,
                    "neither search scope may manufacture an elapsed-time cancellation");
                Assert(registry.TryCancel("automatic-0002") &&
                       automaticWithoutDeadline.IsCancellationRequested &&
                       !interactiveWithoutDeadline.IsCancellationRequested,
                    "only an explicit operation id cancellation may cancel the registered CTS");
                interactiveWithoutDeadline.Dispose();
                automaticWithoutDeadline.Dispose();
            }
        }

        private static void RetainsGateUntilExplicitlyCancelledProviderSettles()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.Zero,
                interactiveOperationTimeout: TimeSpan.Zero,
                automaticOperationTimeout: TimeSpan.Zero,
                maximumConcurrentProviders: 1);
            var policy = new BoundedSearchPolicy(options);
            var lateProvider = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var providerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var ownerCancellation = new CancellationTokenSource())
            {
                var firstTask = policy.ExecuteAsync("site-a", _ =>
                {
                    providerStarted.TrySetResult(true);
                    return lateProvider.Task;
                }, ownerCancellation.Token);
                providerStarted.Task.GetAwaiter().GetResult();
                Assert(!firstTask.IsCompleted,
                    "a provider must remain pending until it settles or the caller explicitly cancels");

                ownerCancellation.Cancel();
                var first = firstTask.GetAwaiter().GetResult();
                Assert(first.Status == BoundedSearchExecutionStatus.Cancelled &&
                       !first.Settlement.IsCompleted,
                    "explicit cancellation must return promptly without releasing a non-cooperative provider lease");

                using (var waitingCancellation = new CancellationTokenSource())
                {
                    var sameProvider = policy.ExecuteAsync(
                        "site-a", _ => Task.FromResult(2), waitingCancellation.Token);
                    var otherProvider = policy.ExecuteAsync(
                        "site-b", _ => Task.FromResult(3), waitingCancellation.Token);
                    Assert(!sameProvider.IsCompleted && !otherProvider.IsCompleted,
                        "the per-provider and global gates must remain owned until the underlying task settles");
                    waitingCancellation.Cancel();
                    Assert(sameProvider.GetAwaiter().GetResult().Status ==
                               BoundedSearchExecutionStatus.Cancelled &&
                           otherProvider.GetAwaiter().GetResult().Status ==
                               BoundedSearchExecutionStatus.Cancelled,
                        "blocked gate waiters must still observe explicit cancellation");
                }

                lateProvider.SetResult(1);
                first.Settlement.GetAwaiter().GetResult();
            }
            var afterSettlement = policy.ExecuteAsync("site-b", _ => Task.FromResult(2), CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(afterSettlement.Status == BoundedSearchExecutionStatus.Completed && afterSettlement.Result == 2,
                "the global gate must release only after the late provider finishes");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
