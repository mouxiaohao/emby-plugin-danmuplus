using System;
using System.Collections.Concurrent;
using System.Linq;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Core
{
    /// <summary>Bounded, process-local proof for carrying a server score across dialog requests.</summary>
    public sealed class DanmuCandidateEvidenceRegistry
    {
        private const int MaximumEntries = 2048;
        private const int MaximumMoviePartEntries = MaximumEntries * 4;
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, DanmuCandidateEvidence> _entries =
            new ConcurrentDictionary<string, DanmuCandidateEvidence>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, DanmuMoviePartEvidence> _movieParts =
            new ConcurrentDictionary<string, DanmuMoviePartEvidence>(StringComparer.Ordinal);

        public string Register(string seasonId, string site, string candidateId,
            double matchScore, string scoreOrigin, SourceMetadata sourceMetadata = null)
        {
            PurgeExpired();
            while (_entries.Count >= MaximumEntries)
            {
                var oldest = _entries.OrderBy(pair => pair.Value.ExpiresUtc).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(oldest.Key)) break;
                _entries.TryRemove(oldest.Key, out _);
            }
            var token = Guid.NewGuid().ToString("N");
            _entries[token] = new DanmuCandidateEvidence
            {
                SeasonId = seasonId ?? string.Empty,
                Site = site ?? string.Empty,
                CandidateId = candidateId ?? string.Empty,
                MatchScore = Math.Max(0, Math.Min(1, matchScore)),
                ScoreOrigin = scoreOrigin ?? string.Empty,
                SourceMetadata = sourceMetadata?.Clone(),
                ExpiresUtc = DateTime.UtcNow.Add(Lifetime),
            };
            return token;
        }

        public bool TryResolve(string token, string seasonId, string site, string candidateId,
            out DanmuCandidateEvidence evidence)
        {
            evidence = null;
            if (string.IsNullOrWhiteSpace(token) || !_entries.TryGetValue(token, out var found)) return false;
            if (found.ExpiresUtc <= DateTime.UtcNow)
            {
                _entries.TryRemove(token, out _);
                return false;
            }
            if (!string.Equals(found.SeasonId, seasonId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.Site, site, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase)) return false;
            evidence = found;
            return true;
        }

        public string RegisterMoviePart(
            string parentToken,
            string itemId,
            string site,
            string candidateId,
            ScraperMoviePart part)
        {
            if (part == null || !part.IsDownloadable || part.IsExplicitNonMain ||
                string.IsNullOrWhiteSpace(part.Id) ||
                !TryResolve(parentToken, itemId, site, candidateId, out _))
            {
                return string.Empty;
            }

            PurgeExpired();
            while (_movieParts.Count >= MaximumMoviePartEntries)
            {
                var oldest = _movieParts.OrderBy(pair => pair.Value.ExpiresUtc).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(oldest.Key)) break;
                _movieParts.TryRemove(oldest.Key, out _);
            }
            var token = Guid.NewGuid().ToString("N");
            _movieParts[token] = new DanmuMoviePartEvidence
            {
                ParentToken = parentToken ?? string.Empty,
                ItemId = itemId ?? string.Empty,
                Site = site ?? string.Empty,
                CandidateId = candidateId ?? string.Empty,
                PartId = part.Id,
                PartTitle = part.Title ?? string.Empty,
                Index = part.Index,
                ExpiresUtc = DateTime.UtcNow.Add(Lifetime),
            };
            return token;
        }

        public bool TryResolveMoviePart(
            string token,
            string parentToken,
            string itemId,
            string site,
            string candidateId,
            out DanmuMoviePartEvidence evidence)
        {
            evidence = null;
            if (string.IsNullOrWhiteSpace(token) ||
                !_movieParts.TryGetValue(token, out var found)) return false;
            if (found.ExpiresUtc <= DateTime.UtcNow)
            {
                _movieParts.TryRemove(token, out _);
                return false;
            }
            if (!TryResolve(parentToken, itemId, site, candidateId, out _) ||
                !string.Equals(found.ParentToken, parentToken, StringComparison.Ordinal) ||
                !string.Equals(found.ItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.Site, site, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(found.CandidateId, candidateId, StringComparison.OrdinalIgnoreCase)) return false;
            evidence = found;
            return true;
        }

        private void PurgeExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var pair in _entries.Where(pair => pair.Value.ExpiresUtc <= now).ToList())
                _entries.TryRemove(pair.Key, out _);
            foreach (var pair in _movieParts.Where(pair => pair.Value.ExpiresUtc <= now).ToList())
                _movieParts.TryRemove(pair.Key, out _);
        }
    }

    public sealed class DanmuCandidateEvidence
    {
        public string SeasonId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public string ScoreOrigin { get; set; } = string.Empty;
        public SourceMetadata SourceMetadata { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }

    public sealed class DanmuMoviePartEvidence
    {
        public string ParentToken { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string PartId { get; set; } = string.Empty;
        public string PartTitle { get; set; } = string.Empty;
        public int? Index { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }
}
