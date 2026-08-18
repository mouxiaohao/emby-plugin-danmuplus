using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace Emby.Plugin.Danmu.Core
{
    public enum SearchOperationScope
    {
        Interactive,
        Automatic,
    }

    /// <summary>
    /// Owns server-side cancellation tokens for bounded searches.  A client
    /// supplies the operation id so its later cancellation request addresses
    /// the same server operation; invalid or duplicate ids are rejected rather
    /// than silently replacing another request.
    /// </summary>
    public sealed class SearchOperationRegistry : IDisposable
    {
        private static readonly Regex OperationIdPattern = new Regex(
            "^[A-Za-z0-9_-]{8,128}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ConcurrentDictionary<string, Entry> _operations =
            new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        // A cancel request may arrive immediately before its GET request has
        // registered. Keep a small, short-lived tombstone so that the later
        // operation observes that explicit cancellation deterministically.
        private readonly ConcurrentDictionary<string, DateTime> _preCancelledOperations =
            new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private int _disposed;

        private const int MaximumPreCancelledOperations = 256;
        private static readonly TimeSpan PreCancelledOperationLifetime = TimeSpan.FromMinutes(1);

        public SearchOperationRegistry(BoundedSearchPolicyOptions options = null)
        {
            // Retain the optional parameter for source compatibility. Search
            // operation lifetime is now controlled only by explicit cancel,
            // lease disposal, or registry disposal.
            _ = options;
        }

        public int ActiveOperationCount => _operations.Count;

        public bool TryBegin(
            string operationId,
            SearchOperationScope scope,
            out SearchOperationLease operation,
            out string error)
        {
            operation = null;
            error = string.Empty;
            if (Volatile.Read(ref _disposed) != 0)
            {
                error = "Search operation registry is unavailable.";
                return false;
            }

            if (!TryNormalizeOperationId(operationId, out var normalizedId))
            {
                error = "Search operation id must be 8-128 URL-safe characters.";
                return false;
            }

            PrunePreCancelledOperations();
            var cancelledBeforeRegistration = _preCancelledOperations.TryRemove(normalizedId, out _);
            var source = new CancellationTokenSource();
            _ = scope;
            var entry = new Entry(source);
            if (!_operations.TryAdd(normalizedId, entry))
            {
                source.Dispose();
                error = "Search operation id is already active.";
                return false;
            }

            // Dispose may have completed its dictionary drain after the first
            // availability check but before this entry was published. Recheck
            // after TryAdd and remove only this exact entry so another in-flight
            // registration with the same id can never be removed accidentally.
            if (Volatile.Read(ref _disposed) != 0)
            {
                if (TryRemoveExact(normalizedId, entry))
                {
                    entry.Source.Cancel();
                    entry.Source.Dispose();
                }
                error = "Search operation registry is unavailable.";
                return false;
            }

            // Close the small race between consuming the tombstone and adding
            // the active entry. A later TryCancel sees the active entry and
            // cancels it directly.
            cancelledBeforeRegistration |= _preCancelledOperations.TryRemove(normalizedId, out _);

            operation = new SearchOperationLease(this, normalizedId, entry);
            if (cancelledBeforeRegistration)
            {
                source.Cancel();
            }
            return true;
        }

        public bool TryCancel(string operationId)
        {
            if (!TryNormalizeOperationId(operationId, out var normalizedId))
            {
                return false;
            }

            if (!_operations.TryGetValue(normalizedId, out var entry))
            {
                PrunePreCancelledOperations();
                if (_preCancelledOperations.Count >= MaximumPreCancelledOperations)
                {
                    return false;
                }

                _preCancelledOperations[normalizedId] = DateTime.UtcNow.Add(PreCancelledOperationLifetime);
                return true;
            }

            try
            {
                entry.Source.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public bool IsActive(string operationId)
        {
            return !string.IsNullOrWhiteSpace(operationId) &&
                   _operations.ContainsKey(operationId.Trim());
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            foreach (var pair in _operations)
            {
                if (_operations.TryRemove(pair.Key, out var entry))
                {
                    entry.Source.Cancel();
                    entry.Source.Dispose();
                }
            }

            _preCancelledOperations.Clear();
        }

        private void Complete(string operationId, Entry expected)
        {
            if (TryRemoveExact(operationId, expected))
            {
                expected.Source.Dispose();
            }
        }

        private bool TryRemoveExact(string operationId, Entry expected)
        {
            return ((ICollection<KeyValuePair<string, Entry>>)_operations).Remove(
                new KeyValuePair<string, Entry>(operationId, expected));
        }

        private static bool TryNormalizeOperationId(string operationId, out string normalizedId)
        {
            normalizedId = string.Empty;
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return false;
            }

            normalizedId = operationId.Trim();
            return OperationIdPattern.IsMatch(normalizedId);
        }

        private void PrunePreCancelledOperations()
        {
            var now = DateTime.UtcNow;
            foreach (var pair in _preCancelledOperations)
            {
                if (pair.Value <= now)
                {
                    _preCancelledOperations.TryRemove(pair.Key, out _);
                }
            }
        }

        internal sealed class Entry
        {
            public Entry(CancellationTokenSource source)
            {
                Source = source;
                // Capture the token before this entry can be published. The
                // registry may cancel and dispose Source concurrently with
                // TryBegin returning its lease, but the token snapshot remains
                // safe to inspect after the source has been disposed.
                Token = source.Token;
            }

            public CancellationTokenSource Source { get; }

            public CancellationToken Token { get; }
        }

        public sealed class SearchOperationLease : IDisposable
        {
            private SearchOperationRegistry _registry;
            private Entry _entry;

            internal SearchOperationLease(SearchOperationRegistry registry, string operationId, Entry entry)
            {
                _registry = registry;
                _entry = entry;
                OperationId = operationId;
            }

            public string OperationId { get; }

            public CancellationToken CancellationToken
            {
                get
                {
                    var entry = Volatile.Read(ref _entry);
                    return entry == null
                        ? new CancellationToken(true)
                        : entry.Token;
                }
            }

            public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

            public void Dispose()
            {
                var registry = Interlocked.Exchange(ref _registry, null);
                var entry = Interlocked.Exchange(ref _entry, null);
                if (registry != null && entry != null)
                {
                    registry.Complete(OperationId, entry);
                }
            }
        }
    }
}
