using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Emby.Plugin.Danmu.Core
{
    public static class DanmuMappingProtocol
    {
        public const int CurrentVersion = 20;

        private static readonly HashSet<string> ForbiddenBatchOrigins = new HashSet<string>(
            new[] { "episode-provider-id", "provider-id", "exact-binding", "binding", "direct" },
            StringComparer.OrdinalIgnoreCase);

        public static bool IsCurrent(int version) => version == CurrentVersion;

        public static bool IsAllowedBatchOrigin(string origin)
        {
            return !string.IsNullOrWhiteSpace(origin) && !ForbiddenBatchOrigins.Contains(origin) &&
                   origin.IndexOf("provider-id", StringComparison.OrdinalIgnoreCase) < 0 &&
                   origin.IndexOf("exact", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }

    /// <summary>
    /// In-process generation authority for one Season plan. A later preview,
    /// rematch, metadata event, or download intent supersedes older work before
    /// it is allowed to mirror a Season identifier.
    /// </summary>
    public sealed class SeasonPlanGenerationCoordinator
    {
        private readonly ConcurrentDictionary<string, long> _generations =
            new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private long _nextGeneration;

        public long Begin(string seasonId)
        {
            if (string.IsNullOrWhiteSpace(seasonId)) throw new ArgumentException("Season id is required.", nameof(seasonId));
            var generation = Interlocked.Increment(ref _nextGeneration);
            _generations.AddOrUpdate(seasonId, generation, (_, current) => Math.Max(current, generation));
            return generation;
        }

        public bool IsCurrent(string seasonId, long generation)
        {
            return generation > 0 && !string.IsNullOrWhiteSpace(seasonId) &&
                   _generations.TryGetValue(seasonId, out var current) && current == generation;
        }
    }

    public sealed class SeasonDisplayMirrorCommit
    {
        public string SeasonId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public string CanonicalMediaId { get; set; } = string.Empty;
        public int EligibleEpisodeCount { get; set; }
        public int MappedEpisodeCount { get; set; }
        public int TerminalEpisodeCount { get; set; }
        public int AcceptedEpisodeCount { get; set; }
        public int StableSourceCount { get; set; }
        public bool HasUnmatchedEpisodes { get; set; }
        public bool HasOverlapOrDuplicate { get; set; }
        public bool Cancelled { get; set; }
        public bool Failed { get; set; }
        public bool StaleStructure { get; set; }
        public bool HasCanonicalSeasonIdentity { get; set; }
    }

    public static class SeasonDisplayMirrorPolicy
    {
        public static bool CanCommit(SeasonDisplayMirrorCommit value, out string reason)
        {
            reason = string.Empty;
            if (value == null) { reason = "missing-commit"; return false; }
            if (value.Generation <= 0) { reason = "missing-generation"; return false; }
            if (value.Cancelled) { reason = "cancelled"; return false; }
            if (value.Failed) { reason = "failed"; return false; }
            if (value.StaleStructure) { reason = "stale-structure"; return false; }
            if (value.HasUnmatchedEpisodes || value.HasOverlapOrDuplicate) { reason = "partial-or-unsafe"; return false; }
            if (value.StableSourceCount != 1) { reason = "not-single-source"; return false; }
            if (value.EligibleEpisodeCount <= 0 || value.MappedEpisodeCount != value.EligibleEpisodeCount ||
                value.TerminalEpisodeCount != value.MappedEpisodeCount || value.AcceptedEpisodeCount <= 0)
            { reason = "incomplete-terminal-state"; return false; }
            if (!value.HasCanonicalSeasonIdentity || string.IsNullOrWhiteSpace(value.ProviderId) ||
                string.IsNullOrWhiteSpace(value.CanonicalMediaId))
            { reason = "episode-only-source"; return false; }
            return true;
        }
    }
}
