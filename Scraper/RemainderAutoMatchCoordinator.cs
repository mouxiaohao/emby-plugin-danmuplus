using System;
using System.Collections.Generic;
using System.Linq;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Scraper
{
    public enum RemainderDecisionState { Selected, NotApplicable, Rejected, Unknown }
    public enum RemainderDecisionKind { None, Part, Metadata, MetadataCountWarning, LogicalSeason }
    public enum RemainderOperationPolicy { InteractiveRecursive, BackgroundNonRecursive }

    public sealed class RemainderCandidate
    {
        // Server-resolved provider identity. It is an operation fact, not a
        // display label, and is compared with the first-source lock before a
        // row can participate in any automatic remainder decision.
        public string ProviderId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string LookupId { get; set; } = string.Empty;
        public IList<string> Titles { get; set; } = new List<string>();
        public int? Year { get; set; }
        public int VerifiedEpisodeCount { get; set; }
        public bool DetailsComplete { get; set; } = true;
        public double LogicalSeasonScore { get; set; }
    }

    public sealed class RemainderDecisionInput
    {
        // Immutable for one interactive remainder operation. Empty retains the
        // provider-neutral core for callers that have not entered recursion.
        public string ProviderLock { get; set; } = string.Empty;
        public string ParentTitle { get; set; } = string.Empty;
        public IList<string> LastSelectedTitles { get; set; } = new List<string>();
        public int LogicalSeasonNumber { get; set; }
        public int? LastPartNumber { get; set; }
        public int? RemainderFirstYear { get; set; }
        public int RemainderEpisodeCount { get; set; }
        public IList<RemainderCandidate> CanonicalCandidates { get; set; } = new List<RemainderCandidate>();
        public ISet<string> UsedStableIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ISet<string> UsedLookupIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool CandidateCoverageComplete { get; set; } = true;
    }

    public sealed class RemainderDecision
    {
        public RemainderDecisionState State { get; set; }
        public RemainderDecisionKind Kind { get; set; }
        public RemainderCandidate Candidate { get; set; }
        public int? NextLogicalSeasonNumber { get; set; }
        public int? PartNumber { get; set; }
        public double FinalScore { get; set; }
        public int SimilarCandidateCount { get; set; }
        public int MatchingTupleCount { get; set; }
        public string AuthoritativeParentTitle { get; set; } = string.Empty;
    }

    /// <summary>In-memory search descriptor for a suffix which represents a
    /// later logical Season without changing the real Emby Season item.</summary>
    public sealed class LogicalSeasonSearchContext
    {
        public string ParentTitle { get; set; } = string.Empty;
        public int ExpectedLogicalSeasonNumber { get; set; }
        public int? FirstEpisodeYear { get; set; }
        public int SuffixEpisodeCount { get; set; }
    }

    public static class RemainderProgressGuard
    {
        public static bool CanCommit(bool generationCurrent, int unmatchedBefore, int unmatchedAfter, int newMappings) =>
            generationCurrent && unmatchedBefore >= 0 && unmatchedAfter >= 0 &&
            unmatchedAfter < unmatchedBefore && newMappings > 0;

        public sealed class MappingSnapshot
        {
            public string LocalId { get; set; } = string.Empty;
            public string ProviderId { get; set; } = string.Empty;
            public string MediaId { get; set; } = string.Empty;
            public string LookupId { get; set; } = string.Empty;
            public string SourceEpisodeId { get; set; } = string.Empty;
            public string CommentId { get; set; } = string.Empty;
            public int? SourceEpisodeNumber { get; set; }
            public string Origin { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
        }

        public static bool CanCommit(bool generationCurrent, int unmatchedBefore, int unmatchedAfter, int newMappings,
            IEnumerable<MappingSnapshot> before, IEnumerable<MappingSnapshot> after, ISet<string> previouslyUnmatched)
        {
            return CanCommit(generationCurrent, unmatchedBefore, unmatchedAfter, newMappings) &&
                   PreservesExistingMappings(before, after, previouslyUnmatched);
        }

        public static bool PreservesExistingMappings(IEnumerable<MappingSnapshot> before,
            IEnumerable<MappingSnapshot> after, ISet<string> previouslyUnmatched)
        {
            var oldByLocal = (before ?? Enumerable.Empty<MappingSnapshot>()).Where(x => x != null)
                .GroupBy(x => x.LocalId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            var newByLocal = (after ?? Enumerable.Empty<MappingSnapshot>()).Where(x => x != null)
                .GroupBy(x => x.LocalId ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
            if (oldByLocal.Count != (before ?? Enumerable.Empty<MappingSnapshot>()).Count(x => x != null) ||
                newByLocal.Count != (after ?? Enumerable.Empty<MappingSnapshot>()).Count(x => x != null)) return false;
            foreach (var old in oldByLocal)
            {
                if (!newByLocal.TryGetValue(old.Key, out var current) || !Same(old.Value, current)) return false;
            }
            var allowed = previouslyUnmatched ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return newByLocal.Keys.Where(key => !oldByLocal.ContainsKey(key)).All(allowed.Contains);
        }

        public static bool TryGetUniqueMaximalSuffix(IEnumerable<string> eligibleOrder,
            IEnumerable<IEnumerable<string>> unmatchedRuns, out List<string> suffix)
        {
            suffix = new List<string>();
            var ordered = (eligibleOrder ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var runs = (unmatchedRuns ?? Enumerable.Empty<IEnumerable<string>>()).Where(x => x != null)
                .Select(x => x.Where(id => !string.IsNullOrWhiteSpace(id)).ToList()).Where(x => x.Count > 0).ToList();
            if (ordered.Count == 0 || runs.Count != 1) return false;
            var candidate = runs[0];
            var start = ordered.FindIndex(id => string.Equals(id, candidate[0], StringComparison.OrdinalIgnoreCase));
            if (start < 0 || ordered.Count - start != candidate.Count ||
                !ordered.Skip(start).SequenceEqual(candidate, StringComparer.OrdinalIgnoreCase)) return false;
            suffix = candidate;
            return true;
        }

        private static bool Same(MappingSnapshot left, MappingSnapshot right) =>
            string.Equals(left.ProviderId, right.ProviderId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.MediaId, right.MediaId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.LookupId, right.LookupId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.SourceEpisodeId, right.SourceEpisodeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.CommentId, right.CommentId, StringComparison.OrdinalIgnoreCase) &&
            left.SourceEpisodeNumber == right.SourceEpisodeNumber &&
            string.Equals(left.Origin, right.Origin, StringComparison.Ordinal) &&
            string.Equals(left.Token, right.Token, StringComparison.Ordinal);
    }

    /// <summary>
    /// Evaluates whether the provider fixed by the first authoritative source
    /// completed its search plan. A sibling provider fault is irrelevant, but
    /// absent or non-completed evidence for the locked provider fails closed.
    /// Candidate rows deliberately play no role here: a filtered empty pool is
    /// still distinct from a completed no-candidate provider search.
    /// </summary>
    public static class RemainderProviderCompletion
    {
        public static bool IsClosed(IEnumerable<DanmuSearchCompletionDiagnostic> diagnostics, string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId)) return false;
            var providerDiagnostics = (diagnostics ?? Enumerable.Empty<DanmuSearchCompletionDiagnostic>())
                .Where(diagnostic => diagnostic != null && string.Equals(diagnostic.Provider, providerId,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            return providerDiagnostics.Count > 0 && providerDiagnostics.All(diagnostic =>
                !diagnostic.TimedOut && !diagnostic.Cancelled &&
                string.Equals(diagnostic.Status, "completed", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Pure, fail-closed three-tier decision core. Provider detail and
    /// authoritative plan rebuilding remain the caller's responsibility.</summary>
    public static class RemainderAutoMatchCoordinator
    {
        public static RemainderDecision Decide(RemainderDecisionInput input)
        {
            if (input == null || !input.CandidateCoverageComplete) return Result(RemainderDecisionState.Unknown);
            var canonical = (input.CanonicalCandidates ?? new List<RemainderCandidate>())
                .Where(candidate => candidate != null && MatchesProviderLock(input, candidate))
                .ToList();
            var eligible = canonical
                .Where(candidate => candidate != null && candidate.DetailsComplete)
                .Where(candidate => candidate.VerifiedEpisodeCount > 3)
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.StableId) && !input.UsedStableIds.Contains(candidate.StableId))
                .Where(candidate => string.IsNullOrWhiteSpace(candidate.LookupId) || !input.UsedLookupIds.Contains(candidate.LookupId))
                .ToList();
            if (canonical.Any(candidate => !candidate.DetailsComplete))
                return Result(RemainderDecisionState.Unknown);

            var family = eligible.Where(candidate => IsSameFamily(input, candidate)).ToList();
            var nonConflicting = family.Where(candidate => !HasConflictingSeason(candidate, input.LogicalSeasonNumber)).ToList();
            var part = DecidePart(input, nonConflicting);
            if (part.State != RemainderDecisionState.NotApplicable) return part;
            var metadata = DecideMetadata(input, nonConflicting);
            if (metadata.State != RemainderDecisionState.NotApplicable) return metadata;
            return Result(RemainderDecisionState.NotApplicable);
        }

        private static RemainderDecision DecidePart(RemainderDecisionInput input, IList<RemainderCandidate> candidates)
        {
            var parts = new List<Tuple<RemainderCandidate, int>>();
            foreach (var candidate in candidates)
            {
                var status = ParseFamilyPart(input, candidate, out var part);
                if (status == PartTitleParseStatus.Malformed) return Result(RemainderDecisionState.Rejected);
                if (status == PartTitleParseStatus.Valid) parts.Add(Tuple.Create(candidate, part));
            }
            if (parts.Count == 0) return Result(RemainderDecisionState.NotApplicable);
            var expected = input.LastPartNumber ?? 1;
            var next = expected + 1;
            var exact = DistinctStable(parts.Where(value => value.Item2 == next).Select(value => value.Item1));
            if (exact.Count == 1) return Result(RemainderDecisionState.Selected, RemainderDecisionKind.Part, exact[0], null, next);
            return Result(RemainderDecisionState.Rejected);
        }

        private static RemainderDecision DecideMetadata(RemainderDecisionInput input, IList<RemainderCandidate> candidates)
        {
            if (!input.RemainderFirstYear.HasValue || input.RemainderFirstYear <= 0) return Result(RemainderDecisionState.NotApplicable);
            var partless = candidates.Where(candidate => ParseFamilyPart(input, candidate, out _) == PartTitleParseStatus.Absent).ToList();
            var partlessStable = DistinctStable(partless);
            var sameYear = DistinctStable(partlessStable.Where(candidate => candidate.Year == input.RemainderFirstYear));
            var exact = sameYear.Where(candidate => candidate.VerifiedEpisodeCount == input.RemainderEpisodeCount).ToList();
            if (exact.Count == 1) return Result(RemainderDecisionState.Selected, RemainderDecisionKind.Metadata, exact[0], null, null, 0, partlessStable.Count, exact.Count);
            if (exact.Count > 1 || sameYear.Count > 1) return Result(RemainderDecisionState.Rejected);
            if (sameYear.Count == 1 && partlessStable.Count == 1) return Result(RemainderDecisionState.Selected, RemainderDecisionKind.MetadataCountWarning, sameYear[0], null, null, 0, 1, 0);
            return Result(RemainderDecisionState.NotApplicable);
        }

        public static RemainderDecision DecideLogicalSeason(RemainderDecisionInput input, IEnumerable<RemainderCandidate> candidates)
        {
            var lockedCandidates = (candidates ?? Enumerable.Empty<RemainderCandidate>())
                .Where(candidate => candidate != null && MatchesProviderLock(input, candidate)).ToList();
            if (input == null || !input.CandidateCoverageComplete || !input.RemainderFirstYear.HasValue || input.RemainderFirstYear <= 0 ||
                lockedCandidates.Any(candidate => !candidate.DetailsComplete)) return Result(RemainderDecisionState.Unknown);
            var expected = input.LogicalSeasonNumber + 1;
            var winners = DistinctStable(lockedCandidates.Where(candidate => candidate.DetailsComplete && candidate.VerifiedEpisodeCount > 3)
                .Where(candidate => !input.UsedStableIds.Contains(candidate.StableId) && (string.IsNullOrWhiteSpace(candidate.LookupId) || !input.UsedLookupIds.Contains(candidate.LookupId)))
                .Where(candidate => !HasConflictingSeason(candidate, expected))
                .Where(candidate => candidate.LogicalSeasonScore >= DanmuMatchScorer.AutomaticConfidenceThreshold));
            return winners.Count == 1
                ? Result(RemainderDecisionState.Selected, RemainderDecisionKind.LogicalSeason, winners[0], expected, null, winners[0].LogicalSeasonScore, 1, 0, DanmuMatchScorer.Normalize(input.ParentTitle))
                : Result(winners.Count > 1 ? RemainderDecisionState.Rejected : RemainderDecisionState.NotApplicable);
        }

        private static bool IsSameFamily(RemainderDecisionInput input, RemainderCandidate candidate)
        {
            var parent = DanmuMatchScorer.Normalize(input.ParentTitle);
            if (parent.Length < 2) return false;
            var lastTitles = (input.LastSelectedTitles ?? new List<string>()).Where(title => !string.IsNullOrWhiteSpace(title)).ToList();
            var candidateTitles = (candidate.Titles ?? new List<string>()).Where(title => !string.IsNullOrWhiteSpace(title)).ToList();
            if (lastTitles.Count == 0 || candidateTitles.Count == 0) return false;

            // A confirmed source may expose several server-resolved title channels.  The
            // first non-parent identity in that complete set closes the parent-only S1
            // exception for the whole source; do not let a generic alias re-open it per
            // title pair.
            var lastIdentityCores = GetIdentityCores(lastTitles, input.ParentTitle, parent);
            if (lastIdentityCores.Count > 0)
            {
                var candidateIdentityCores = GetIdentityCores(candidateTitles, input.ParentTitle, parent);
                return lastIdentityCores.Any(lastCore => candidateIdentityCores.Any(candidateCore =>
                    SharesIdentityCore(lastCore, candidateCore)));
            }

            // The logical-S1 parent-only exception is deliberately narrower than a
            // title-pair Any(): every authoritative channel on both sides must reduce to
            // the same parent.  It is not a substitute for a real identity core in S00
            // or later logical seasons. A malformed parent-qualified Part is admitted
            // here only so ParseFamilyPart can retain its terminal Rejected outcome; it
            // never becomes an identity core or a positive family match by itself.
            return input.LogicalSeasonNumber == 1 &&
                   HasOnlyParentFamilyChannels(lastTitles, input.ParentTitle, parent) &&
                   HasOnlyParentOrMalformedPartChannels(candidateTitles, input.ParentTitle, parent);
        }

        private static List<string> GetIdentityCores(IEnumerable<string> titles, string parentTitle, string parent) =>
            (titles ?? Enumerable.Empty<string>())
                .Select(title => RemoveFamilySyntax(title, parentTitle, parent))
                .Where(core => IsNonParentIdentityCore(core, parent))
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private static bool IsNonParentIdentityCore(string core, string parent) =>
            !string.IsNullOrWhiteSpace(core) && !string.Equals(core, parent, StringComparison.Ordinal) &&
            DanmuMatchScorer.IsIdentityBearingLooseTitle(core);

        private static bool HasOnlyParentFamilyChannels(IEnumerable<string> titles, string parentTitle, string parent)
        {
            var channels = (titles ?? Enumerable.Empty<string>()).Where(title => !string.IsNullOrWhiteSpace(title)).ToList();
            return channels.Count > 0 && channels.All(title => string.Equals(
                RemoveFamilySyntax(title, parentTitle, parent), parent, StringComparison.Ordinal));
        }

        private static bool HasOnlyParentOrMalformedPartChannels(IEnumerable<string> titles, string parentTitle, string parent)
        {
            var channels = (titles ?? Enumerable.Empty<string>()).Where(title => !string.IsNullOrWhiteSpace(title)).ToList();
            return channels.Count > 0 && channels.All(title => string.Equals(
                RemoveFamilySyntax(title, parentTitle, parent), parent, StringComparison.Ordinal) ||
                IsParentQualifiedMalformedPartOnly(title, parentTitle));
        }

        private static bool SharesIdentityCore(string lastCore, string candidateCore)
        {
            if (!DanmuMatchScorer.IsIdentityBearingLooseTitle(lastCore) ||
                !DanmuMatchScorer.IsIdentityBearingLooseTitle(candidateCore)) return false;

            // The previous loose-score-only comparison made near spellings such as
            // ArcA/ArcB look related.  Automatic remainder work needs an actual shared
            // normalized core: exact equality or an explicit continuation extension.
            // This keeps the existing normalization/identity floor while failing closed
            // for merely similar, different arcs.
            return string.Equals(lastCore, candidateCore, StringComparison.Ordinal) ||
                   lastCore.IndexOf(candidateCore, StringComparison.Ordinal) >= 0 ||
                   candidateCore.IndexOf(lastCore, StringComparison.Ordinal) >= 0;
        }

        private static bool IsParentQualifiedMalformedPartOnly(string title, string parentTitle)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(parentTitle)) return false;
            var raw = title.Normalize(System.Text.NormalizationForm.FormKC);
            var rawParent = parentTitle.Normalize(System.Text.NormalizationForm.FormKC);
            var parentIndex = raw.IndexOf(rawParent, StringComparison.OrdinalIgnoreCase);
            if (parentIndex < 0) return false;
            var residual = raw.Remove(parentIndex, rawParent.Length);
            residual = System.Text.RegularExpressions.Regex.Replace(residual,
                @"(?:第?[0-9一二三四五六七八九十]+季|season\s*\d+|s\s*\d+)", string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).TrimStart();
            return residual.StartsWith("part", StringComparison.OrdinalIgnoreCase) || residual.StartsWith("部分", StringComparison.Ordinal)
                ? PartTitleParser.Analyze(residual, out _) == PartTitleParseStatus.Malformed
                : false;
        }

        private static bool MatchesProviderLock(RemainderDecisionInput input, RemainderCandidate candidate)
        {
            return string.IsNullOrWhiteSpace(input?.ProviderLock) ||
                   string.Equals(input.ProviderLock, candidate?.ProviderId, StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveFamilySyntax(string title, string parentTitle, string parent)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(parent)) return string.Empty;
            var raw = title.Normalize(System.Text.NormalizationForm.FormKC);
            var normalized = DanmuMatchScorer.Normalize(raw);
            if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
            var containedParent = normalized.IndexOf(parent, StringComparison.Ordinal) >= 0;
            // Strip grammar from the uncollapsed channel: otherwise
            // "Parent Part2 Part3" loses the separator before the parser sees it.
            var rawWithoutParent = raw;
            var rawParent = (parentTitle ?? string.Empty).Normalize(System.Text.NormalizationForm.FormKC);
            var parentIndex = string.IsNullOrWhiteSpace(rawParent) ? -1 : raw.IndexOf(rawParent, StringComparison.OrdinalIgnoreCase);
            if (parentIndex >= 0) rawWithoutParent = raw.Remove(parentIndex, rawParent.Length);
            normalized = DanmuMatchScorer.Normalize(PartTitleParser.RemoveValidExpressions(rawWithoutParent));
            if (containedParent) normalized = normalized.Replace(parent, string.Empty);
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized,
                @"(?:第?[0-9一二三四五六七八九十]+季|season\d+|s\d+)", string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            normalized = DanmuMatchScorer.Normalize(PartTitleParser.RemoveValidExpressions(normalized));
            if (normalized.Length == 0) return containedParent ? parent : string.Empty;
            return DanmuMatchScorer.IsIdentityBearingLooseTitle(normalized) ? normalized : string.Empty;
        }

        private static bool HasConflictingSeason(RemainderCandidate candidate, int activeSeason)
        {
            return !DanmuMatchScorer.IsExplicitSeasonCompatible(candidate.Titles, activeSeason);
        }

        private static PartTitleParseStatus ParseFamilyPart(RemainderDecisionInput input, RemainderCandidate candidate, out int partNumber)
        {
            partNumber = 0; var values = new HashSet<int>(); var sawMalformed = false;
            var parent = DanmuMatchScorer.Normalize(input.ParentTitle);
            if (!IsSameFamily(input, candidate) || parent.Length < 2) return PartTitleParseStatus.Absent;
            var hasLastIdentityCore = GetIdentityCores(input.LastSelectedTitles, input.ParentTitle, parent).Count > 0;
            foreach (var title in candidate.Titles ?? new List<string>())
            {
                foreach (var last in input.LastSelectedTitles ?? new List<string>())
                {
                    if (!AreFamilyChannelsCompatible(last, title, input.ParentTitle, parent, hasLastIdentityCore)) continue;
                    var status = PartTitleParser.AnalyzeForFamily(title, input.ParentTitle, last, out var part);
                    if (status == PartTitleParseStatus.Malformed) sawMalformed = true;
                    if (status == PartTitleParseStatus.Valid) values.Add(part);
                }
            }
            if (sawMalformed || values.Count > 1) return PartTitleParseStatus.Malformed;
            if (values.Count == 0) return PartTitleParseStatus.Absent;
            partNumber = values.First(); return PartTitleParseStatus.Valid;
        }

        private static bool AreFamilyChannelsCompatible(string last, string title, string parentTitle, string parent,
            bool hasLastIdentityCore)
        {
            var lastCore = RemoveFamilySyntax(last, parentTitle, parent);
            var candidateCore = RemoveFamilySyntax(title, parentTitle, parent);
            if (hasLastIdentityCore)
                return IsNonParentIdentityCore(lastCore, parent) && IsNonParentIdentityCore(candidateCore, parent) &&
                       SharesIdentityCore(lastCore, candidateCore);
            return string.Equals(lastCore, parent, StringComparison.Ordinal) &&
                   (string.Equals(candidateCore, parent, StringComparison.Ordinal) ||
                    IsParentQualifiedMalformedPartOnly(title, parentTitle));
        }

        private static List<RemainderCandidate> DistinctStable(IEnumerable<RemainderCandidate> values) => values
            .GroupBy(candidate => candidate.StableId, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();

        private static RemainderDecision Result(RemainderDecisionState state, RemainderDecisionKind kind = RemainderDecisionKind.None,
            RemainderCandidate candidate = null, int? logicalSeason = null, int? partNumber = null, double finalScore = 0,
            int similarCandidateCount = 0, int matchingTupleCount = 0, string authoritativeParentTitle = "") => new RemainderDecision
            { State = state, Kind = kind, Candidate = candidate, NextLogicalSeasonNumber = logicalSeason, PartNumber = partNumber, FinalScore = finalScore,
                SimilarCandidateCount = similarCandidateCount, MatchingTupleCount = matchingTupleCount, AuthoritativeParentTitle = authoritativeParentTitle ?? string.Empty };
    }
}
