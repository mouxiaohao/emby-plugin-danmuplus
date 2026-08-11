using System;
using System.Collections.Concurrent;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>
    /// Tracks the most recently committed write generation for each item/provider.
    /// Callers provide the serialization around repository writes; this type keeps
    /// the comparison deterministic and independently testable.
    /// </summary>
    public sealed class ProviderWriteGenerationTracker
    {
        private readonly ConcurrentDictionary<string, long> _committed =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, long> _started =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        public void MarkStarted(string key, long generation)
        {
            _started.AddOrUpdate(key, generation, (_, current) => Math.Max(current, generation));
        }

        public bool IsStale(string key, long generation, out long committedGeneration)
        {
            _committed.TryGetValue(key, out committedGeneration);
            _started.TryGetValue(key, out var startedGeneration);
            var latest = Math.Max(committedGeneration, startedGeneration);
            committedGeneration = latest;
            return generation < latest;
        }

        public void MarkCommitted(string key, long generation)
        {
            _committed.AddOrUpdate(key, generation, (_, current) => Math.Max(current, generation));
        }
    }
}
