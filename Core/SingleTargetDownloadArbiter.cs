using System;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Resolves a single-target provider operation without allowing a provider that
    /// ignores cancellation to change the already selected terminal result.
    /// </summary>
    public static class SingleTargetDownloadArbiter
    {
        public static async Task<DanmuEpisodeDownloadOutcome> AwaitAsync(
            Task<DanmuEpisodeDownloadOutcome> providerTask,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Action<Exception> onLateProviderFailure = null,
            Action onTimeout = null,
            string timeoutMessage = null)
        {
            if (providerTask == null)
            {
                throw new ArgumentNullException(nameof(providerTask));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var timeoutTask = Task.Delay(timeout);
            var cancellationSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Task completedTask;
            using (cancellationToken.Register(() => cancellationSignal.TrySetResult(true)))
            {
                completedTask = await Task.WhenAny(
                    providerTask,
                    timeoutTask,
                    cancellationSignal.Task).ConfigureAwait(false);
            }

            if (cancellationSignal.Task.IsCompleted || cancellationToken.IsCancellationRequested)
            {
                ObserveLateFailure(providerTask, onLateProviderFailure);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (completedTask == providerTask)
            {
                var outcome = await providerTask.ConfigureAwait(false);
                // Cancellation has priority until the provider outcome is handed
                // back to the caller, including the both-already-completed race.
                cancellationToken.ThrowIfCancellationRequested();
                return outcome;
            }

            // A provider can ignore the cancellation token. Observe a later fault,
            // but never await or apply its result after the terminal decision.
            ObserveLateFailure(providerTask, onLateProviderFailure);

            cancellationToken.ThrowIfCancellationRequested();
            onTimeout?.Invoke();
            return new DanmuEpisodeDownloadOutcome
            {
                Status = "skipped",
                Message = timeoutMessage ?? $"下载超过 {FormatTimeout(timeout)}，已自动跳过",
            };
        }

        private static void ObserveLateFailure(
            Task<DanmuEpisodeDownloadOutcome> providerTask,
            Action<Exception> onLateProviderFailure)
        {
            _ = providerTask.ContinueWith(
                lateTask => onLateProviderFailure?.Invoke(lateTask.Exception),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private static string FormatTimeout(TimeSpan timeout)
        {
            if (timeout.TotalMilliseconds < 1000)
            {
                return $"{Math.Max(0, timeout.TotalMilliseconds):0.##} 毫秒";
            }

            return $"{Math.Max(0, timeout.TotalSeconds):0.##} 秒";
        }
    }
}
