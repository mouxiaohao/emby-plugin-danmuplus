using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class SevenDayReplayTests
    {
        public static void Run()
        {
            FreezesOnlyRecentFileSkipsAndExcludesAcceptedLineage();
            RejectsNonTerminalTaskStates();
            PreservesFrozenProviderScopeAndSeparatesEpisodeFromSeasonProviderIds();
            HoldsEpisodeLeaseUntilLateProviderSettles();
            PreservesExistingXmlWhenTemporaryWriteFails();
            PreservesServerOwnedReplayAndWriteSafetyContracts();
        }

        private static void FreezesOnlyRecentFileSkipsAndExcludesAcceptedLineage()
        {
            var recent = new DanmuEpisodeDownloadResult
            {
                ItemId = "recent", Status = "skipped",
                SkipReason = SevenDayReplayPolicy.RecentFileSkipReason,
                SourceSite = "DandanID", SourceCandidateId = "source", SourceEpisodeId = "episode",
                FrozenCommentId = "comment", SourceScopeType = "Episode",
            };
            var legacySkip = new DanmuEpisodeDownloadResult { ItemId = "legacy", Status = "skipped" };
            var partial = new DanmuEpisodeDownloadResult
            {
                ItemId = "partial", Status = "skipped",
                SkipReason = SevenDayReplayPolicy.RecentFileSkipReason,
            };
            var frozen = SevenDayReplayPolicy.FreezeEligibleEpisodes(
                new[] { recent, legacySkip, partial },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "partial" });

            Assert(frozen.Count == 1 && frozen[0].ItemId == "recent",
                "only the machine-readable seven-day file skip may be replayed");
            Assert(frozen[0].SourceScopeType == "Episode" && frozen[0].FrozenCommentId == "comment",
                "the replay snapshot must retain the exact provider/media/EpisodeId/CommentId tuple");
            frozen[0].SourceEpisodeId = "mutated";
            Assert(recent.SourceEpisodeId == "episode",
                "the replay scope must be a copied snapshot, not the origin task evidence");
            recent.FrozenCommentId = string.Empty;
            Assert(SevenDayReplayPolicy.FreezeEligibleEpisodes(new[] { recent }, null).Count == 0,
                "seven-day replay must fail closed when the captured CommentId is absent");
        }

        private static void RejectsNonTerminalTaskStates()
        {
            Assert(!SevenDayReplayPolicy.IsTerminal("running") &&
                   !SevenDayReplayPolicy.IsTerminal("queued") &&
                   !SevenDayReplayPolicy.IsTerminal("failed") &&
                   !SevenDayReplayPolicy.IsTerminal("cancelled") &&
                   SevenDayReplayPolicy.IsTerminal("completed_with_errors"),
                "replay must begin only after an eligible completed origin state");
        }

        private static void PreservesFrozenProviderScopeAndSeparatesEpisodeFromSeasonProviderIds()
        {
            var directEpisode = new DanmuEpisodeDownloadResult
            {
                MatchOrigin = "provider-id", SourceScopeType = "Episode",
                SourceSite = "DandanID", SourceCandidateId = "episode-id",
            };
            var seasonMedia = new DanmuEpisodeDownloadResult
            {
                MatchOrigin = "provider-id", SourceScopeType = "Season",
                SourceSite = "DandanID", SourceCandidateId = "season-id",
            };
            var isDirect = typeof(Emby.Plugin.Danmu.Core.Controllers.DanmuController).GetMethod(
                "IsDirectEpisodeProviderMapping", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(isDirect != null, "replay must classify provider ids through frozen source scope");
            Assert((bool)isDirect.Invoke(null, new object[] { directEpisode }),
                "an Episode-scoped provider id must retain the direct episode resolver path");
            Assert(!(bool)isDirect.Invoke(null, new object[] { seasonMedia }),
                "a Season-scoped provider id with the same MatchOrigin must not use the direct episode resolver path");

            var normalize = typeof(Emby.Plugin.Danmu.Core.Controllers.DanmuController).GetMethod(
                "NormalizeFrozenReplayProviderOutcome", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(normalize != null, "replay must guard direct-provider writeback by source scope");
            var directOutcome = new DanmuEpisodeDownloadOutcome { FilePersisted = true };
            var seasonOutcome = new DanmuEpisodeDownloadOutcome
            {
                FilePersisted = true, ProviderId = "existing", ProviderValue = "existing-value",
            };
            normalize.Invoke(null, new object[] { directEpisode, directOutcome });
            normalize.Invoke(null, new object[] { seasonMedia, seasonOutcome });
            Assert(directOutcome.ProviderId == "DandanID" && directOutcome.ProviderValue == "episode-id",
                "only an Episode-scoped direct provider id may be persisted back to the local Episode");
            Assert(seasonOutcome.ProviderId == "existing" && seasonOutcome.ProviderValue == "existing-value",
                "a Season-scoped provider id must never be written into the local Episode provider-id field");
        }

        private static void PreservesServerOwnedReplayAndWriteSafetyContracts()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "Core", "Controllers", "DanmuController.cs"));
            var source = File.ReadAllText(sourcePath);
            Assert(source.Contains("ReplaySevenDaySkipped(danmuParams.TaskId)") &&
                   source.Contains("Status = \"not_found\"") &&
                   source.Contains("ErrorCode = \"origin_task_required\"") &&
                   source.Contains("origin.ReplayChildTaskId = child.TaskId") &&
                   source.Contains("ForceRefresh = true"),
                "replay must accept only retained origin task state, fail closed after restart, link one idempotent child, and force refresh");
            Assert(Count(source, "TryAcquireEpisodeRetryLease(") >= 3 &&
                   source.Contains("var replayEpisodeLeases = new Dictionary<string, SemaphoreSlim>") &&
                   source.Contains("foreach (var episodeLease in episodeLeases"),
                "bulk replay and per-Episode retry must share leases that are frozen before the child starts and released at settlement");
            var downloadIndex = source.IndexOf("DownloadEpisodeForProgress(\r\n", StringComparison.Ordinal);
            if (downloadIndex < 0)
            {
                downloadIndex = source.IndexOf("DownloadEpisodeForProgress(\n", StringComparison.Ordinal);
            }
            var persistIndex = source.IndexOf("PersistProviderIdAfterAcceptedOutcome(episode, outcome)",
                Math.Max(0, downloadIndex), StringComparison.Ordinal);
            Assert(downloadIndex >= 0 && persistIndex > downloadIndex,
                "replay must reuse safe download persistence and update ProviderIds only after an accepted write outcome");

            Assert(source.Contains("var isSevenDayReplayTask = string.Equals(task.TargetItemType, \"SevenDayReplay\"") &&
                   source.Contains("var useFrozenEpisodeSource = isCompositeTask || isSevenDayReplayTask") &&
                   source.Contains("PrepareFrozenReplayAsync(season, episode, episodeResult)") &&
                   source.Contains("var confirmedSourceNumber = sourceEpisode.EpisodeNumber ?? request.SourceEpisodeNumber") &&
                   source.Contains("CreateSingleTargetTask(episode, request, scraper, \"Episode\", confirmedSourceNumber)") &&
                   source.Contains("task.Episodes[0].SourceEpisodeId = sourceEpisode.Id ?? string.Empty") &&
                   source.Contains("task.Episodes[0].FrozenCommentId = sourceEpisode.CommentId ?? string.Empty") &&
                   source.Contains("var requiresCompositeTransition = task.IsCompositePlan"),
                "the first task must retain the provider's real Episode number and exact EpisodeId/CommentId while replay keeps the composite write barrier");
            var retryStart = source.IndexOf(
                "private async Task<DanmuDownloadTaskResult> RetryTrackedEpisode", StringComparison.Ordinal);
            var retryEnd = source.IndexOf(
                "private async Task<DanmuDownloadTaskResult> RetryTrackedMovie", retryStart,
                StringComparison.Ordinal);
            var retryBody = retryStart >= 0 && retryEnd > retryStart
                ? source.Substring(retryStart, retryEnd - retryStart)
                : string.Empty;
            Assert(retryBody.Contains("DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(") &&
                   retryBody.Contains("PrepareFrozenReplayAsync(season, episode, episodeResult)") &&
                   (retryBody.Contains("DownloadEpisodeForProgress(\r\n                        episode,\r\n                        media,\r\n                        scraper,\r\n                        true,\r\n                        1)") ||
                    retryBody.Contains("DownloadEpisodeForProgress(\n                        episode,\n                        media,\n                        scraper,\n                        true,\n                        1)")),
                "ordinary, composite, and replay retries all execute their exact one-Episode media at ordinal 1 rather than reusing a real source number such as E5");
            Assert(source.Contains("string.IsNullOrWhiteSpace(episodeResult.FrozenCommentId)") &&
                   source.Contains("sourceEpisode.CommentId, episodeResult.FrozenCommentId") &&
                   source.Contains("revalidatedSourceEpisode.CommentId") &&
                   source.Contains("FrozenCommentId = x.FrozenCommentId"),
                "replay and retry must retain and revalidate the exact CommentId instead of substituting current number/position data");
            Assert(source.Contains("SourceScopeType = isDirectEpisodeProviderId ? \"Episode\" : \"Season\";") &&
                   source.Contains("SourceScopeType = \"Season\",") &&
                   source.Contains("var isDirectMapping = IsDirectEpisodeProviderMapping(episodeResult);") &&
                   source.Contains("!isDirectMapping && !IsSeasonProviderMapping(episodeResult)") &&
                   Count(source, "NormalizeFrozenReplayProviderOutcome(episodeResult, outcome);") == 2 &&
                   !source.Contains("string.Equals(episode?.MatchOrigin, \"provider-id\""),
                "initial downloads, snapshots, replay preparation, and failed retries must use frozen Episode/Season scope instead of ambiguous provider-id origin, including direct-id writeback");
            Assert(source.Contains("replayProviderTasks[episodeResult.ItemId] = providerTask") &&
                   source.Contains("ReleaseLeaseWhenProviderSettles(") &&
                   source.Contains("Task<DanmuEpisodeDownloadOutcome> providerTask = null"),
                "replay and ordinary retry must retain their per-episode lease until an ignored-cancellation provider task actually settles");

            var helperPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "LibraryManagerEventsHelper.cs"));
            var helper = File.ReadAllText(helperPath);
            Assert(helper.Contains("WriteDanmuAtomicallyAsync(_fileSystem, danmuPath, bytes)") &&
                   helper.Contains("File.Replace(temporaryPath, danmuPath, null)") &&
                   helper.Contains("File.Move(temporaryPath, danmuPath)") &&
                   helper.Contains("if (File.Exists(temporaryPath))") &&
                   !helper.Contains("WriteAllBytesAsync(danmuPath, bytes"),
                "forced replay XML writes must stage and validate a sibling temporary file, atomically replace only after success, and never directly truncate the final XML");
        }

        private static void HoldsEpisodeLeaseUntilLateProviderSettles()
        {
            var provider = new TaskCompletionSource<DanmuEpisodeDownloadOutcome>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (var lease = new SemaphoreSlim(1, 1))
            {
                Assert(lease.Wait(0), "test setup must acquire the episode lease");
                SingleTargetDownloadArbiter.ReleaseLeaseWhenProviderSettles(provider.Task, lease);
                Assert(!lease.Wait(0),
                    "an immediate retry must remain blocked after the UI has timed out or cancelled while the provider task is unfinished");

                provider.SetResult(new DanmuEpisodeDownloadOutcome { Status = "success" });
                Assert(SpinWait.SpinUntil(() => lease.Wait(0), TimeSpan.FromSeconds(1)),
                    "the delayed completion continuation must release the lease only after the provider task settles");
            }
        }

        private static void PreservesExistingXmlWhenTemporaryWriteFails()
        {
            var directory = Path.Combine(Path.GetTempPath(), "danmu-replay-atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, "episode.xml");
            var original = Encoding.UTF8.GetBytes("existing recent XML");
            File.WriteAllBytes(target, original);
            try
            {
                var method = typeof(Emby.Plugin.Danmu.LibraryManagerEventsHelper).GetMethod(
                    "WriteDanmuAtomicallyAsync",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert(method != null, "the atomic danmu writer must remain available to the replay persistence path");

                var failed = false;
                try
                {
                    ((Task)method.Invoke(null, new object[]
                    {
                        new FailingTemporaryFileSystem(), target, Encoding.UTF8.GetBytes("replacement")
                    })).GetAwaiter().GetResult();
                }
                catch (IOException)
                {
                    failed = true;
                }

                Assert(failed, "an I/O failure while writing the staged replay XML must be returned to the caller");
                Assert(ByteArraysEqual(File.ReadAllBytes(target), original),
                    "an I/O failure while staging a forced replay must preserve the original recent XML");
                Assert(Directory.GetFiles(directory, "*.tmp").Length == 0,
                    "a failed staged replay write must clean up its temporary sibling file");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private sealed class FailingTemporaryFileSystem : IFileSystem
        {
            public bool Exists(string path) => File.Exists(path);
            public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);
            public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
            {
                File.WriteAllBytes(path, new[] { bytes[0] });
                return Task.FromException(new IOException("simulated staged write failure"));
            }

            public Task WriteAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private static bool ByteArraysEqual(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length) return false;
            for (var index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index]) return false;
            }
            return true;
        }

        private static int Count(string value, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
