using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Emby.Plugin.Danmu.Core
{
    public enum CompositeSeasonStateLookup
    {
        NotMarked,
        Marked,
        Unavailable,
    }

    /// <summary>
    /// Minimal, plugin-private record that tells the matching pipeline that a
    /// local Season cannot safely be represented by one upstream Season id.
    /// It intentionally contains no provider, source-media, or episode binding.
    /// </summary>
    public sealed class CompositeSeasonState
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; }
        public string SeasonId { get; set; }
        public string SeasonFingerprint { get; set; }
    }

    /// <summary>
    /// Persists composite-season tombstones independently of Emby's ProviderIds.
    /// A changed library item fingerprint makes an old record inapplicable. Bad,
    /// partial, and unreadable records fail closed: callers must search again.
    /// </summary>
    public sealed class CompositeSeasonStateStore
    {
        private const string FilePrefix = "composite-season-v1-";
        private const string FileSuffix = ".json";
        private static readonly ConcurrentDictionary<string, object> FileGates =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly string _directory;

        public CompositeSeasonStateStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A composite-season state directory is required.", nameof(directory));
            }

            _directory = Path.GetFullPath(directory);
        }

        public bool IsMarkedComposite(string seasonId, string seasonFingerprint)
        {
            return TryGet(seasonId, seasonFingerprint, out _);
        }

        public bool TryGet(string seasonId, string seasonFingerprint, out CompositeSeasonState state)
        {
            return GetStatus(seasonId, seasonFingerprint, out state) == CompositeSeasonStateLookup.Marked;
        }

        /// <summary>
        /// Distinguishes a genuinely absent marker from unreadable/corrupt state.
        /// Callers must treat Unavailable conservatively and never restore an
        /// upstream Season binding merely because private protection is unreadable.
        /// </summary>
        public CompositeSeasonStateLookup GetStatus(
            string seasonId,
            string seasonFingerprint,
            out CompositeSeasonState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(seasonFingerprint))
            {
                return CompositeSeasonStateLookup.Unavailable;
            }

            try
            {
                var path = GetPath(seasonId);
                if (!File.Exists(path))
                {
                    return CompositeSeasonStateLookup.NotMarked;
                }

                var json = File.ReadAllText(path, Encoding.UTF8);
                var candidate = JsonSerializer.Deserialize<CompositeSeasonState>(json);
                if (candidate == null || candidate.Version != CompositeSeasonState.CurrentVersion ||
                    string.IsNullOrWhiteSpace(candidate.SeasonId) ||
                    string.IsNullOrWhiteSpace(candidate.SeasonFingerprint))
                {
                    return CompositeSeasonStateLookup.Unavailable;
                }

                if (!string.Equals(candidate.SeasonId, seasonId, StringComparison.Ordinal))
                {
                    // The file name is derived from the requested Season id, so
                    // another id inside that record is corruption, not recycling.
                    return CompositeSeasonStateLookup.Unavailable;
                }

                if (!string.Equals(candidate.SeasonFingerprint, seasonFingerprint, StringComparison.Ordinal))
                {
                    // A complete valid record for another stable Season/Series
                    // identity is not corrupt; it is simply inapplicable.
                    return CompositeSeasonStateLookup.NotMarked;
                }

                state = candidate;
                return CompositeSeasonStateLookup.Marked;
            }
            catch (IOException)
            {
                return CompositeSeasonStateLookup.Unavailable;
            }
            catch (UnauthorizedAccessException)
            {
                return CompositeSeasonStateLookup.Unavailable;
            }
            catch (JsonException)
            {
                return CompositeSeasonStateLookup.Unavailable;
            }
        }

        /// <summary>
        /// Writes one self-contained record with a same-directory temporary file
        /// and atomic replacement where the host filesystem supports it.
        /// </summary>
        public void MarkComposite(string seasonId, string seasonFingerprint)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
            {
                throw new ArgumentException("A Season id is required.", nameof(seasonId));
            }

            if (string.IsNullOrWhiteSpace(seasonFingerprint))
            {
                throw new ArgumentException("A Season fingerprint is required.", nameof(seasonFingerprint));
            }

            Directory.CreateDirectory(_directory);
            var path = GetPath(seasonId);
            var gate = FileGates.GetOrAdd(path, _ => new object());
            lock (gate)
            {
                var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var state = new CompositeSeasonState
                    {
                        Version = CompositeSeasonState.CurrentVersion,
                        SeasonId = seasonId,
                        SeasonFingerprint = seasonFingerprint,
                    };
                    var json = JsonSerializer.Serialize(state);
                    using (var stream = new FileStream(
                        temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(json);
                        writer.Flush();
                        stream.Flush(true);
                    }

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Replace(temporaryPath, path, null);
                        }
                        catch (PlatformNotSupportedException ex)
                        {
                            throw new IOException(
                                "The host filesystem does not support atomic composite-season state replacement.", ex);
                        }
                    }
                    else
                    {
                        File.Move(temporaryPath, path);
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
        }

        private string GetPath(string seasonId)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(seasonId));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return Path.Combine(_directory, FilePrefix + builder + FileSuffix);
            }
        }

    }
}
