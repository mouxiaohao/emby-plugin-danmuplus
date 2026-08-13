using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Emby.Plugin.Danmu.Core;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class CompositeSeasonStateTests
    {
        public static void Run()
        {
            PersistsOnlyVersionedCompositeTombstones();
            RejectsMarkersWhenTheStableSeasonOwnershipChanges();
            ClearsOnlyExactPluginOwnedSeasonKeys();
            PreventsSeasonProviderWritesAcrossCompositeGenerations();
        }

        private static void PersistsOnlyVersionedCompositeTombstones()
        {
            var directory = Path.Combine(Path.GetTempPath(), "danmu-composite-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new CompositeSeasonStateStore(directory);
                const string seasonId = "season-123";
                const string fingerprint = "series-42|season-1|38";
                store.MarkComposite(seasonId, fingerprint);

                Program.Assert(store.TryGet(seasonId, fingerprint, out var state) &&
                               state.Version == CompositeSeasonState.CurrentVersion &&
                               state.SeasonId == seasonId &&
                               state.SeasonFingerprint == fingerprint,
                    "a composite marker must persist only its version, Season id, and fingerprint");
                var json = File.ReadAllText(Directory.GetFiles(directory, "*.json").Single(), Encoding.UTF8);
                Program.Assert(!json.Contains("DandanID") && !json.Contains("CommentId") && !json.Contains("MediaId"),
                    "the private marker must not retain upstream bindings that could become stale after a rematch");
                Program.Assert(!store.IsMarkedComposite(seasonId, "changed-fingerprint"),
                    "a marker for a changed Season must be ignored rather than reused");

                var record = Directory.GetFiles(directory, "*.json").Single();
                File.WriteAllText(record, "{ not valid json", Encoding.UTF8);
                Program.Assert(!store.IsMarkedComposite(seasonId, fingerprint),
                    "a corrupt private marker must not be treated as a valid record");
                Program.Assert(store.GetStatus(seasonId, fingerprint, out _) ==
                               CompositeSeasonStateLookup.Unavailable,
                    "a corrupt private marker must be distinguishable from a missing marker so callers can block writes conservatively");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void RejectsMarkersWhenTheStableSeasonOwnershipChanges()
        {
            var directory = Path.Combine(Path.GetTempPath(), "danmu-composite-fingerprint-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new CompositeSeasonStateStore(directory);
                const string seasonId = "season-stable-item";
                // A caller-owned fingerprint must change whenever stable local
                // membership/order is materially changed (new files, removal,
                // renumbering, or reassignment), even if the Season ItemId stays.
                const string originalOwnership = "series-9|season-item|episode-a,episode-b,episode-c";
                const string addedEpisode = "series-9|season-item|episode-a,episode-b,episode-c,episode-d";
                const string renumberedOrder = "series-9|season-item|episode-b,episode-a,episode-c";
                store.MarkComposite(seasonId, originalOwnership);

                Program.Assert(store.IsMarkedComposite(seasonId, originalOwnership),
                    "the unchanged stable Season ownership must retain its tombstone");
                Program.Assert(!store.IsMarkedComposite(seasonId, addedEpisode) &&
                               !store.IsMarkedComposite(seasonId, renumberedOrder),
                    "dynamic local membership or stable ordering changes must force fresh matching instead of inheriting an old tombstone");

                var record = Directory.GetFiles(directory, "*.json").Single();
                File.WriteAllText(record, "{\"Version\":1,\"SeasonId\":\"season-stable-item\"}", Encoding.UTF8);
                Program.Assert(!store.IsMarkedComposite(seasonId, originalOwnership),
                    "a partial marker must be treated conservatively as unavailable and must never suppress a fresh search");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void ClearsOnlyExactPluginOwnedSeasonKeys()
        {
            var current = new ProviderIdDictionary
            {
                ["BilibiliID"] = "bili",
                ["BilibiliIDManual"] = "bili-manual",
                ["DandanID"] = "dandan",
                ["DandanIDManual"] = "dandan-manual",
                ["IqiyiID"] = "iqiyi",
                ["MgtvIDManual"] = "mgtv-manual",
                ["TencentID"] = "tencent",
                ["YoukuIDManual"] = "youku-manual",
                ["Tmdb"] = "tmdb-id",
                ["ForeignPluginID"] = "foreign-id",
                ["ForeignPluginIDManual"] = "foreign-manual",
            };

            var plan = CompositeSeasonProviderPolicy.BuildClearPlan(current);
            Program.Assert(plan.RequiresClear &&
                           plan.RemovedProviderIds.Count == 8 &&
                           !plan.RemainingProviderIds.ContainsKey("BilibiliID") &&
                           !plan.RemainingProviderIds.ContainsKey("DandanIDManual") &&
                           plan.RemainingProviderIds["Tmdb"] == "tmdb-id" &&
                           plan.RemainingProviderIds["ForeignPluginID"] == "foreign-id" &&
                           plan.RemainingProviderIds["ForeignPluginIDManual"] == "foreign-manual",
                "composite cleanup must remove every exact registered ProviderId/Manual key and preserve foreign metadata");
            Program.Assert(CompositeSeasonProviderPolicy.SeasonProviderIdKeys.Count == 6 &&
                           CompositeSeasonProviderPolicy.SeasonManualProviderIdKeys.Count == 6 &&
                           !CompositeSeasonProviderPolicy.IsPluginSeasonProviderKey("ForeignPluginID"),
                "the plugin key directory must be exact and include disabled-provider Manual keys");
        }

        private static void PreventsSeasonProviderWritesAcrossCompositeGenerations()
        {
            var coordinator = new CompositeSeasonProviderWriteCoordinator();
            var oldOrdinary = coordinator.BeginWrite("season-1", false);
            var latestOrdinary = coordinator.BeginWrite("season-1", false);
            Program.Assert(!coordinator.CanWriteSeasonProviderId(oldOrdinary) &&
                           coordinator.CanWriteSeasonProviderId(latestOrdinary),
                "a newer ordinary write generation must supersede an older pending one");

            var composite = coordinator.BeginWrite("season-1", true);
            Program.Assert(!coordinator.CanWriteSeasonProviderId(latestOrdinary),
                "a confirmed composite plan must raise its Season write barrier before any file succeeds");
            var first = coordinator.OnFilePersisted(composite);
            Program.Assert(first.ShouldPersistTombstoneAndClearProviderIds &&
                           first.SeasonId == "season-1" &&
                           !coordinator.OnFilePersisted(composite).ShouldPersistTombstoneAndClearProviderIds,
                "only the first persisted composite file may claim the marker-and-clear action");
            coordinator.Complete(composite);
            var afterTombstone = coordinator.BeginWrite("season-1", false);
            Program.Assert(coordinator.IsTombstoned("season-1") &&
                           !coordinator.CanWriteSeasonProviderId(afterTombstone),
                "a tombstone must prevent late or future tasks from restoring a Season ProviderId");

            var failureOnly = new CompositeSeasonProviderWriteCoordinator();
            var failedComposite = failureOnly.BeginWrite("season-2", true);
            failureOnly.Complete(failedComposite);
            var normalAfterFailure = failureOnly.BeginWrite("season-2", false);
            Program.Assert(!failureOnly.IsTombstoned("season-2") &&
                           failureOnly.CanWriteSeasonProviderId(normalAfterFailure),
                "a composite task with no persisted file must release only its temporary barrier");
        }
    }
}
