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
            RegistersClientOperationIdsAndDeadlines();
            RetainsGateForLateNonCooperativeProvider();
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

        private static void RegistersClientOperationIdsAndDeadlines()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromMilliseconds(100),
                interactiveOperationTimeout: TimeSpan.FromMilliseconds(40),
                automaticOperationTimeout: TimeSpan.FromMilliseconds(100));
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

                Assert(registry.TryBegin("interactive-02", SearchOperationScope.Interactive, out var deadline, out _),
                    "a second valid operation should register");
                Thread.Sleep(80);
                Assert(deadline.IsCancellationRequested,
                    "the injected interactive overall deadline must cancel its CTS");
                deadline.Dispose();
            }
        }

        private static void RetainsGateForLateNonCooperativeProvider()
        {
            var options = new BoundedSearchPolicyOptions(
                providerCallTimeout: TimeSpan.FromMilliseconds(25),
                interactiveOperationTimeout: TimeSpan.FromMilliseconds(100),
                automaticOperationTimeout: TimeSpan.FromMilliseconds(150),
                maximumConcurrentProviders: 1);
            var policy = new BoundedSearchPolicy(options);
            var lateProvider = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = policy.ExecuteAsync("site-a", _ => lateProvider.Task, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert(first.Status == BoundedSearchExecutionStatus.ProviderTimedOut && !first.Settlement.IsCompleted,
                "a non-cooperative provider must time out without releasing its gate lease");

            using (var waitingOperation = new CancellationTokenSource(TimeSpan.FromMilliseconds(35)))
            {
                var blocked = policy.ExecuteAsync("site-b", _ => Task.FromResult(2), waitingOperation.Token)
                    .GetAwaiter().GetResult();
                Assert(blocked.Status == BoundedSearchExecutionStatus.Cancelled,
                    "a global provider gate wait must consume the enclosing operation deadline");
            }

            lateProvider.SetResult(1);
            first.Settlement.GetAwaiter().GetResult();
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
