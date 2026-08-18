using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Immutable concurrency settings for a bounded smart-match search.  The
    /// timeout constructor arguments and properties remain as compatibility
    /// shims for callers compiled against the earlier bounded-deadline API;
    /// shared search no longer owns elapsed-time deadlines.
    /// </summary>
    public sealed class BoundedSearchPolicyOptions
    {
        public BoundedSearchPolicyOptions(
            TimeSpan? providerCallTimeout = null,
            TimeSpan? interactiveOperationTimeout = null,
            TimeSpan? automaticOperationTimeout = null,
            int maximumConcurrentProviders = 3)
        {
            _ = providerCallTimeout;
            _ = interactiveOperationTimeout;
            _ = automaticOperationTimeout;
            ProviderCallTimeout = Timeout.InfiniteTimeSpan;
            InteractiveOperationTimeout = Timeout.InfiniteTimeSpan;
            AutomaticOperationTimeout = Timeout.InfiniteTimeSpan;
            MaximumConcurrentProviders = maximumConcurrentProviders;

            if (MaximumConcurrentProviders < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumConcurrentProviders));
            }
        }

        public TimeSpan ProviderCallTimeout { get; }

        public TimeSpan InteractiveOperationTimeout { get; }

        public TimeSpan AutomaticOperationTimeout { get; }

        public int MaximumConcurrentProviders { get; }
    }

    public enum BoundedSearchExecutionStatus
    {
        Completed,
        // Retained for binary/source compatibility. Shared search no longer
        // emits elapsed-time provider failures.
        ProviderTimedOut,
        Cancelled,
        Faulted,
    }

    /// <summary>
    /// Immediate outcome plus a settlement task.  On explicit cancellation
    /// the outcome is returned promptly, while Settlement remains incomplete
    /// until a legacy non-cooperative provider has actually stopped and its
    /// gate lease can be released safely.
    /// </summary>
    public sealed class BoundedSearchExecution<TResult>
    {
        internal BoundedSearchExecution(
            BoundedSearchExecutionStatus status,
            TResult result,
            Exception error,
            Task settlement)
        {
            Status = status;
            Result = result;
            Error = error;
            Settlement = settlement ?? Task.CompletedTask;
        }

        public BoundedSearchExecutionStatus Status { get; }

        public TResult Result { get; }

        public Exception Error { get; }

        public Task Settlement { get; }

        public bool IsTerminalProviderResult =>
            Status == BoundedSearchExecutionStatus.Completed ||
            Status == BoundedSearchExecutionStatus.Faulted;
    }

    /// <summary>
    /// Shared provider-search gate.  Callers should use <see cref="Shared"/>
    /// one injected instance for runtime searches so all operations observe
    /// the same global three-provider limit and the one-provider-per-site
    /// limit.
    /// </summary>
    public sealed class BoundedSearchPolicy
    {
        private readonly SemaphoreSlim _globalGate;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _providerGates =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        public BoundedSearchPolicy(BoundedSearchPolicyOptions options = null)
        {
            Options = options ?? new BoundedSearchPolicyOptions();
            _globalGate = new SemaphoreSlim(
                Options.MaximumConcurrentProviders,
                Options.MaximumConcurrentProviders);
        }

        public static BoundedSearchPolicy Shared { get; } = new BoundedSearchPolicy();

        public BoundedSearchPolicyOptions Options { get; }

        public async Task<BoundedSearchExecution<TResult>> ExecuteAsync<TResult>(
            string providerKey,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return Faulted<TResult>(new ArgumentException("A provider key is required.", nameof(providerKey)));
            }

            if (operation == null)
            {
                return Faulted<TResult>(new ArgumentNullException(nameof(operation)));
            }

            var normalizedProviderKey = providerKey.Trim();
            // Take the per-provider gate first.  A queue of calls for one site
            // must not occupy all global slots while it waits for that site's
            // single active request to finish.
            var providerGate = _providerGates.GetOrAdd(normalizedProviderKey, _ => new SemaphoreSlim(1, 1));
            var providerAcquire = await WaitForGateAsync(providerGate, cancellationToken).ConfigureAwait(false);
            if (providerAcquire != GateAcquireStatus.Acquired)
            {
                return FromAcquireStatus<TResult>(providerAcquire);
            }

            var globalAcquire = await WaitForGateAsync(_globalGate, cancellationToken).ConfigureAwait(false);
            if (globalAcquire != GateAcquireStatus.Acquired)
            {
                providerGate.Release();
                return FromAcquireStatus<TResult>(globalAcquire);
            }

            var lease = new GateLease(_globalGate, providerGate);
            var providerCancellation = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;
            var handoff = false;
            try
            {
                Task<TResult> providerTask;
                try
                {
                    providerTask = operation(providerCancellation?.Token ?? CancellationToken.None);
                    if (providerTask == null)
                    {
                        throw new InvalidOperationException("The provider search operation returned no task.");
                    }
                }
                catch (Exception ex)
                {
                    return Faulted<TResult>(ex);
                }

                var cancellationSignal = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                var winner = await Task.WhenAny(providerTask, cancellationSignal).ConfigureAwait(false);

                if (winner == providerTask)
                {
                    try
                    {
                        var result = await providerTask.ConfigureAwait(false);
                        return new BoundedSearchExecution<TResult>(
                            BoundedSearchExecutionStatus.Completed,
                            result,
                            null,
                            Task.CompletedTask);
                    }
                    catch (OperationCanceledException ex)
                    {
                        return new BoundedSearchExecution<TResult>(
                            BoundedSearchExecutionStatus.Cancelled,
                            default(TResult),
                            ex,
                            Task.CompletedTask);
                    }
                    catch (Exception ex)
                    {
                        return Faulted<TResult>(ex);
                    }
                }

                handoff = true;
                try
                {
                    providerCancellation?.Cancel();
                }
                catch (AggregateException)
                {
                    // A provider-owned cancellation callback must not release
                    // either gate while its underlying task is still running.
                }
                var settlement = ReleaseWhenProviderStopsAsync(providerTask, providerCancellation, lease);
                return new BoundedSearchExecution<TResult>(
                    BoundedSearchExecutionStatus.Cancelled,
                    default(TResult),
                    null,
                    settlement);
            }
            finally
            {
                if (!handoff)
                {
                    providerCancellation?.Dispose();
                    lease.Dispose();
                }
            }
        }

        private async Task<GateAcquireStatus> WaitForGateAsync(SemaphoreSlim gate, CancellationToken cancellationToken)
        {
            try
            {
                // Queueing is bounded only by an explicit caller/parent
                // cancellation. Shared search owns no elapsed-time budget.
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                return GateAcquireStatus.Acquired;
            }
            catch (OperationCanceledException)
            {
                return GateAcquireStatus.Cancelled;
            }
        }

        private static async Task ReleaseWhenProviderStopsAsync<TResult>(
            Task<TResult> providerTask,
            CancellationTokenSource providerCancellation,
            GateLease lease)
        {
            try
            {
                await providerTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The caller has already received its cancellation result.
                // Observing a late provider fault prevents it from
                // becoming unobserved while preserving that terminal result.
            }
            finally
            {
                providerCancellation?.Dispose();
                lease.Dispose();
            }
        }

        private static BoundedSearchExecution<TResult> Faulted<TResult>(Exception error)
        {
            return new BoundedSearchExecution<TResult>(
                BoundedSearchExecutionStatus.Faulted,
                default(TResult),
                error,
                Task.CompletedTask);
        }

        private static BoundedSearchExecution<TResult> FromAcquireStatus<TResult>(GateAcquireStatus status)
        {
            return new BoundedSearchExecution<TResult>(
                BoundedSearchExecutionStatus.Cancelled,
                default(TResult),
                null,
                Task.CompletedTask);
        }

        private enum GateAcquireStatus
        {
            Acquired,
            Cancelled,
        }

        private sealed class GateLease : IDisposable
        {
            private SemaphoreSlim _globalGate;
            private SemaphoreSlim _providerGate;

            public GateLease(SemaphoreSlim globalGate, SemaphoreSlim providerGate)
            {
                _globalGate = globalGate;
                _providerGate = providerGate;
            }

            public void Dispose()
            {
                var providerGate = Interlocked.Exchange(ref _providerGate, null);
                if (providerGate != null)
                {
                    providerGate.Release();
                }

                var globalGate = Interlocked.Exchange(ref _globalGate, null);
                if (globalGate != null)
                {
                    globalGate.Release();
                }
            }
        }
    }
}
