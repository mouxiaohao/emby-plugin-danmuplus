using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Exact plugin-owned Season ProviderId keys. This deliberately does not use
    /// suffix or prefix guessing: keys belonging to Emby and other plugins must
    /// remain untouched when a composite season is first persisted.
    /// </summary>
    public static class CompositeSeasonProviderPolicy
    {
        private static readonly ReadOnlyCollection<string> ProviderIdKeys =
            new ReadOnlyCollection<string>(new[]
            {
                "BilibiliID",
                "DandanID",
                "IqiyiID",
                "MgtvID",
                "TencentID",
                "YoukuID",
            });

        private static readonly ReadOnlyCollection<string> ManualProviderIdKeys =
            new ReadOnlyCollection<string>(ProviderIdKeys
                .Select(x => x + "Manual")
                .ToArray());

        private static readonly HashSet<string> AllPluginKeys =
            new HashSet<string>(ProviderIdKeys.Concat(ManualProviderIdKeys), StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> SeasonProviderIdKeys => ProviderIdKeys;
        public static IReadOnlyCollection<string> SeasonManualProviderIdKeys => ManualProviderIdKeys;

        public static bool IsPluginSeasonProviderKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && AllPluginKeys.Contains(key);
        }

        public static CompositeSeasonProviderClearPlan BuildClearPlan(IReadOnlyDictionary<string, string> current)
        {
            var remaining = new ProviderIdDictionary();
            var removed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in current ?? new Dictionary<string, string>())
            {
                if (IsPluginSeasonProviderKey(pair.Key))
                {
                    removed[pair.Key] = pair.Value;
                }
                else
                {
                    remaining[pair.Key] = pair.Value;
                }
            }

            return new CompositeSeasonProviderClearPlan(remaining, removed);
        }
    }

    public sealed class CompositeSeasonProviderClearPlan
    {
        internal CompositeSeasonProviderClearPlan(
            ProviderIdDictionary remainingProviderIds,
            IDictionary<string, string> removedProviderIds)
        {
            RemainingProviderIds = remainingProviderIds ?? new ProviderIdDictionary();
            RemovedProviderIds = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(removedProviderIds ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase));
        }

        public ProviderIdDictionary RemainingProviderIds { get; }
        public IReadOnlyDictionary<string, string> RemovedProviderIds { get; }
        public bool RequiresClear => RemovedProviderIds.Count > 0;
    }

    /// <summary>
    /// In-process authority for Season-level ProviderId writes. A confirmed
    /// composite task immediately raises a barrier; its first persisted file
    /// creates a permanent tombstone. Integrators persist the private state and
    /// apply the clear plan when <see cref="OnFilePersisted"/> requests it.
    /// </summary>
    public sealed class CompositeSeasonProviderWriteCoordinator
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, SeasonWriteState> _seasons =
            new Dictionary<string, SeasonWriteState>(StringComparer.Ordinal);
        private long _nextLeaseId;

        public CompositeSeasonProviderWriteLease BeginWrite(string seasonId, bool compositePlan)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
            {
                throw new ArgumentException("A Season id is required.", nameof(seasonId));
            }

            lock (_gate)
            {
                var state = GetOrCreate(seasonId);
                var lease = new CompositeSeasonProviderWriteLease(
                    seasonId,
                    ++_nextLeaseId,
                    ++state.Generation,
                    compositePlan);
                state.ActiveLeases.Add(lease.LeaseId);
                if (compositePlan)
                {
                    state.CompositeBarrierCount++;
                }

                return lease;
            }
        }

        public void RestoreTombstone(string seasonId)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
            {
                throw new ArgumentException("A Season id is required.", nameof(seasonId));
            }

            lock (_gate)
            {
                var state = GetOrCreate(seasonId);
                state.Tombstoned = true;
                state.Generation++;
            }
        }

        public bool CanWriteSeasonProviderId(CompositeSeasonProviderWriteLease lease)
        {
            if (lease == null || lease.IsCompositePlan)
            {
                return false;
            }

            lock (_gate)
            {
                return _seasons.TryGetValue(lease.SeasonId, out var state) &&
                       state.ActiveLeases.Contains(lease.LeaseId) &&
                       !state.Tombstoned &&
                       state.CompositeBarrierCount == 0 &&
                       lease.Generation == state.Generation;
            }
        }

        public CompositeSeasonFirstFileDecision OnFilePersisted(CompositeSeasonProviderWriteLease lease)
        {
            if (lease == null || !lease.IsCompositePlan)
            {
                return CompositeSeasonFirstFileDecision.None;
            }

            lock (_gate)
            {
                if (!_seasons.TryGetValue(lease.SeasonId, out var state) ||
                    !state.ActiveLeases.Contains(lease.LeaseId) ||
                    state.Tombstoned)
                {
                    return CompositeSeasonFirstFileDecision.None;
                }

                state.Tombstoned = true;
                state.Generation++;
                return new CompositeSeasonFirstFileDecision(lease.SeasonId, true);
            }
        }

        public void Complete(CompositeSeasonProviderWriteLease lease)
        {
            if (lease == null)
            {
                return;
            }

            lock (_gate)
            {
                if (!_seasons.TryGetValue(lease.SeasonId, out var state) ||
                    !state.ActiveLeases.Remove(lease.LeaseId))
                {
                    return;
                }

                if (lease.IsCompositePlan && state.CompositeBarrierCount > 0)
                {
                    state.CompositeBarrierCount--;
                }
            }
        }

        public bool IsTombstoned(string seasonId)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
            {
                return false;
            }

            lock (_gate)
            {
                return _seasons.TryGetValue(seasonId, out var state) && state.Tombstoned;
            }
        }

        private SeasonWriteState GetOrCreate(string seasonId)
        {
            if (!_seasons.TryGetValue(seasonId, out var state))
            {
                state = new SeasonWriteState();
                _seasons.Add(seasonId, state);
            }

            return state;
        }

        private sealed class SeasonWriteState
        {
            public long Generation { get; set; }
            public bool Tombstoned { get; set; }
            public int CompositeBarrierCount { get; set; }
            public HashSet<long> ActiveLeases { get; } = new HashSet<long>();
        }
    }

    public sealed class CompositeSeasonProviderWriteLease
    {
        internal CompositeSeasonProviderWriteLease(string seasonId, long leaseId, long generation, bool isCompositePlan)
        {
            SeasonId = seasonId;
            LeaseId = leaseId;
            Generation = generation;
            IsCompositePlan = isCompositePlan;
        }

        public string SeasonId { get; }
        public long LeaseId { get; }
        public long Generation { get; }
        public bool IsCompositePlan { get; }
    }

    public sealed class CompositeSeasonFirstFileDecision
    {
        internal static readonly CompositeSeasonFirstFileDecision None =
            new CompositeSeasonFirstFileDecision(null, false);

        internal CompositeSeasonFirstFileDecision(string seasonId, bool shouldPersistTombstoneAndClearProviderIds)
        {
            SeasonId = seasonId;
            ShouldPersistTombstoneAndClearProviderIds = shouldPersistTombstoneAndClearProviderIds;
        }

        public string SeasonId { get; }
        public bool ShouldPersistTombstoneAndClearProviderIds { get; }
    }
}
