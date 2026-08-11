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

        public bool IsStale(string key, long generation, out long committedGeneration)
        {
            return _committed.TryGetValue(key, out committedGeneration) &&
                   generation < committedGeneration;
        }

        public void MarkCommitted(string key, long generation)
        {
            _committed.AddOrUpdate(key, generation, (_, current) => Math.Max(current, generation));
        }
    }
}
