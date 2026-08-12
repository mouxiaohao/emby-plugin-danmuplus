using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Emby.Plugin.Danmu.Model
{
    public static class DanmuMatchScoreOrigin
    {
        public const string SearchConfidence = "search-confidence";
        public const string ExactEpisodeId = "exact-episode-id";
        public const string ExactBinding = "exact-binding";
    }

    public class DanmuMatchPreviewResult
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool CanStart { get; set; }
        public string MatchIntent { get; set; } = DanmuMatchIntent.Default;
        public string MatchOrigin { get; set; } = string.Empty;
        public string DecisionReason { get; set; } = string.Empty;
        public string ResolvedProviderId { get; set; } = string.Empty;
        public string ResolvedProviderIdKey { get; set; } = string.Empty;
        public string ResolvedScopeType { get; set; } = string.Empty;
        public string ResolvedScopeItemId { get; set; } = string.Empty;
        public string SearchScope { get; set; } = string.Empty;
        public string SearchOperationId { get; set; } = string.Empty;
        public List<DanmuSearchCompletionDiagnostic> SearchCompletionDiagnostics { get; set; } =
            new List<DanmuSearchCompletionDiagnostic>();
        public DanmuSelectedCandidatePreview SelectedCandidate { get; set; }
        public Dictionary<string, string> EnabledProviderIdKeys { get; set; } =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public List<DanmuSeasonMatchResult> Seasons { get; set; } = new List<DanmuSeasonMatchResult>();
        public DanmuItemMatchResult Target { get; set; }
    }

    public class DanmuItemMatchResult
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public string SeriesId { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public string SeasonName { get; set; } = string.Empty;
        public int? EpisodeNumber { get; set; }
        public int? Year { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool AutoSelected { get; set; }
        public string MatchIntent { get; set; } = DanmuMatchIntent.Default;
        public string MatchOrigin { get; set; } = string.Empty;
        public string DecisionReason { get; set; } = string.Empty;
        public string ResolvedProviderId { get; set; } = string.Empty;
        public string ResolvedProviderIdKey { get; set; } = string.Empty;
        public string ResolvedScopeType { get; set; } = string.Empty;
        public string ResolvedScopeItemId { get; set; } = string.Empty;
        public string SearchScope { get; set; } = string.Empty;
        public string SearchOperationId { get; set; } = string.Empty;
        public List<DanmuSearchCompletionDiagnostic> SearchCompletionDiagnostics { get; set; } =
            new List<DanmuSearchCompletionDiagnostic>();
        public DanmuSelectedCandidatePreview SelectedCandidate { get; set; }
        public Dictionary<string, string> EnabledProviderIdKeys { get; set; } =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public string SelectedId { get; set; } = string.Empty;
        public string SelectedSite { get; set; } = string.Empty;
        public string SelectedSiteName { get; set; } = string.Empty;
        public List<string> SearchErrors { get; set; } = new List<string>();
        public List<DanmuMatchCandidate> Candidates { get; set; } = new List<DanmuMatchCandidate>();
    }

    public class DanmuSeasonMatchResult
    {
        public string SeasonId { get; set; } = string.Empty;
        public string SeriesId { get; set; } = string.Empty;
        public string SeasonName { get; set; } = string.Empty;
        public string SeriesName { get; set; } = string.Empty;
        public int? SeasonNumber { get; set; }
        public int? Year { get; set; }
        public int EpisodeCount { get; set; }
        public string Keyword { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool AutoSelected { get; set; }
        public string MatchIntent { get; set; } = DanmuMatchIntent.Default;
        public string MatchOrigin { get; set; } = string.Empty;
        public string DecisionReason { get; set; } = string.Empty;
        public string ResolvedProviderId { get; set; } = string.Empty;
        public string ResolvedProviderIdKey { get; set; } = string.Empty;
        public string ResolvedScopeType { get; set; } = string.Empty;
        public string ResolvedScopeItemId { get; set; } = string.Empty;
        public string SearchScope { get; set; } = string.Empty;
        public string SearchOperationId { get; set; } = string.Empty;
        public List<DanmuSearchCompletionDiagnostic> SearchCompletionDiagnostics { get; set; } =
            new List<DanmuSearchCompletionDiagnostic>();
        public DanmuSelectedCandidatePreview SelectedCandidate { get; set; }
        public Dictionary<string, string> EnabledProviderIdKeys { get; set; } =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        public string SelectedId { get; set; } = string.Empty;
        public string SelectedSite { get; set; } = string.Empty;
        public string SelectedSiteName { get; set; } = string.Empty;
        public List<string> SearchErrors { get; set; } = new List<string>();
        public List<DanmuMatchCandidate> Candidates { get; set; } = new List<DanmuMatchCandidate>();

        // A season can be composed of more than one upstream season (or a
        // special).  These groups are a plugin-side presentation only; Emby's
        // actual Season membership is never altered.
        public bool RequiresCompositeMapping { get; set; }
        public CompositeSeasonPlan CompositePlan { get; set; }
        public List<DanmuCompositeSeasonGroup> CompositeGroups { get; set; } =
            new List<DanmuCompositeSeasonGroup>();
    }

    public class DanmuCompositeSeasonGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public bool IsTemporary { get; set; }
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string SourceStartEpisodeId { get; set; } = string.Empty;
        public int? SourceStartEpisodeNumber { get; set; }
        public string MatchOrigin { get; set; } = string.Empty;
        public double MatchScore { get; set; }
        public string ScoreOrigin { get; set; } = string.Empty;
        public string SelectionEvidenceToken { get; set; } = string.Empty;
        public List<DanmuCompositeEpisode> Episodes { get; set; } = new List<DanmuCompositeEpisode>();
    }

    public class DanmuCompositeEpisode
    {
        public string ItemId { get; set; } = string.Empty;
        public int? EpisodeNumber { get; set; }
        public string EpisodeName { get; set; } = string.Empty;
        public int? SourceEpisodeNumber { get; set; }
    }

    // This is intentionally compact. Comment IDs and arbitrary item mappings
    // are not accepted from the browser; the server re-fetches the selected
    // upstream media and derives each exact mapping itself.
    public class DanmuCompositeSeasonSelection
    {
        public string LocalStartEpisodeItemId { get; set; } = string.Empty;
        public int RequestedEpisodeCount { get; set; }
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string SourceStartEpisodeId { get; set; } = string.Empty;
        public int? SourceStartEpisodeNumber { get; set; }
        public string MatchOrigin { get; set; } = "manual";
        public string SelectionEvidenceToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// GET query binding in Emby 4.9 only supports scalar values. Composite
    /// selections therefore travel as one JSON string and are decoded here,
    /// before any candidate/source validation or download work begins.
    /// </summary>
    public static class DanmuCompositeSeasonSelectionJson
    {
        // The UI sends compact selections in a scalar GET value. Bound the
        // payload before allocating a deserialized object graph.
        public const int MaximumPayloadCharacters = 16 * 1024;
        public const int MaximumSelectionCount = 128;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public static bool TryParse(string json, out List<DanmuCompositeSeasonSelection> selections, out string error)
        {
            selections = new List<DanmuCompositeSeasonSelection>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            if (json.Length > MaximumPayloadCharacters)
            {
                error = "Composite selections JSON is too large.";
                return false;
            }

            var payload = json.Trim();
            if (!payload.StartsWith("[", StringComparison.Ordinal) || !payload.EndsWith("]", StringComparison.Ordinal))
            {
                error = "复合季选择参数必须是 JSON 数组。";
                return false;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<DanmuCompositeSeasonSelection>>(payload, Options);
                if (parsed == null || parsed.Any(selection => selection == null))
                {
                    error = "复合季选择参数包含无效项目。";
                    return false;
                }

                if (parsed.Count > MaximumSelectionCount)
                {
                    error = "Composite selections contain too many items.";
                    return false;
                }

                selections = parsed;
                return true;
            }
            catch (JsonException)
            {
                error = "复合季选择参数不是有效 JSON。";
                return false;
            }
            catch (Exception)
            {
                error = "无法读取复合季选择参数。";
                return false;
            }
        }
    }

    /// <summary>
    /// Strict scalar GET parser for the local Episodes the browser asks a
    /// composite preview to leave unmatched.  The runtime list is intentionally
    /// separate from the query-bound JSON string to remain compatible with
    /// Emby 4.9's scalar-only ValueParser.
    /// </summary>
    public static class DanmuExcludedLocalEpisodeItemIdsJson
    {
        public const int MaximumPayloadCharacters = DanmuCompositeSeasonSelectionJson.MaximumPayloadCharacters;
        public const int MaximumItemCount = DanmuCompositeSeasonSelectionJson.MaximumSelectionCount;
        public const int MaximumItemCharacters = 256;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public static bool TryParse(string json, out List<string> itemIds, out string error)
        {
            itemIds = new List<string>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            if (json.Length > MaximumPayloadCharacters)
            {
                error = "Excluded local episode ids JSON is too large.";
                return false;
            }

            var payload = json.Trim();
            if (!payload.StartsWith("[", StringComparison.Ordinal) || !payload.EndsWith("]", StringComparison.Ordinal))
            {
                error = "Excluded local episode ids must be a JSON array.";
                return false;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(payload, Options);
                if (parsed == null || parsed.Count > MaximumItemCount)
                {
                    error = parsed == null
                        ? "Excluded local episode ids contain an invalid item."
                        : "Excluded local episode ids contain too many items.";
                    return false;
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rawItemId in parsed)
                {
                    var itemId = rawItemId == null ? string.Empty : rawItemId.Trim();
                    if (itemId.Length == 0 || itemId.Length > MaximumItemCharacters)
                    {
                        error = "Excluded local episode ids contain an invalid item.";
                        return false;
                    }

                    if (seen.Add(itemId))
                    {
                        itemIds.Add(itemId);
                    }
                }

                return true;
            }
            catch (JsonException)
            {
                error = "Excluded local episode ids are not valid JSON.";
                return false;
            }
            catch (Exception)
            {
                error = "Excluded local episode ids could not be read.";
                return false;
            }
        }
    }

    public class DanmuMatchCandidate
    {
        public string Id { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public int SourceOrder { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? Year { get; set; }
        public int EpisodeSize { get; set; }
        public double Score { get; set; }
        public double MatchScore { get; set; }
        public string ScoreOrigin { get; set; } = DanmuMatchScoreOrigin.SearchConfidence;
        public string SelectionEvidenceToken { get; set; } = string.Empty;
        public double TitleScore { get; set; }
        public double ParentTitleScore { get; set; }
        public double KeywordScore { get; set; }
        public double YearScore { get; set; }
        public double EpisodeScore { get; set; }
        public bool ManualBound { get; set; }
        public string MatchOrigin { get; set; } = string.Empty;
        public string DecisionReason { get; set; } = string.Empty;
        public int? SuggestedEpisodeNumber { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// A compact, explicit preview of the backend-selected upstream candidate.
    /// It keeps clients from inferring a selection from candidate ordering.
    /// </summary>
    public class DanmuSelectedCandidatePreview
    {
        public string Id { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Score { get; set; }
        public double MatchScore { get; set; }
        public string ScoreOrigin { get; set; } = string.Empty;
        public string SelectionEvidenceToken { get; set; } = string.Empty;
        public int SourceOrder { get; set; }
        public string MatchOrigin { get; set; } = string.Empty;
        public string DecisionReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Read-only second-stage response for one manually selected Episode
    /// candidate. Source comment IDs are deliberately never exposed to the
    /// browser; confirmation submits only this exact source episode identity.
    /// </summary>
    public class DanmuSelectedCandidateDetailPreview
    {
        public string ItemId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string SearchOperationId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<DanmuSelectedCandidateSourceEpisode> Episodes { get; set; } =
            new List<DanmuSelectedCandidateSourceEpisode>();
    }

    public class DanmuSelectedCandidateSourceEpisode
    {
        public string Id { get; set; } = string.Empty;
        public int? Number { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    /// <summary>
    /// Per-provider completion evidence for a bounded search.  It is additive
    /// so existing clients can retain their legacy SearchErrors handling.
    /// </summary>
    public class DanmuSearchCompletionDiagnostic
    {
        public string Provider { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long ElapsedMilliseconds { get; set; }
        public bool TimedOut { get; set; }
        public bool Cancelled { get; set; }
    }

    public class DanmuBindResult
    {
        public bool Success { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public bool Manual { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
    }

    public class DanmuDownloadTaskResult
    {
        public string TaskId { get; set; } = string.Empty;
        public string TargetItemId { get; set; } = string.Empty;
        public string TargetItemName { get; set; } = string.Empty;
        public string TargetItemType { get; set; } = string.Empty;
        public int? SourceEpisodeNumber { get; set; }
        public string SeasonId { get; set; } = string.Empty;
        public string SeriesId { get; set; } = string.Empty;
        public string SeasonName { get; set; } = string.Empty;
        public int? SeasonNumber { get; set; }
        public int? SeasonYear { get; set; }
        public string Site { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string MatchOrigin { get; set; } = string.Empty;
        // Whether this task requires composite safety handling. This can remain
        // true after direct identities normalize to one upstream source, so the
        // Season binding is protected until the composite route has settled.
        public bool IsCompositePlan { get; set; }
        public long SeasonProviderWriteGeneration { get; set; }
        public bool SeasonProviderCommitted { get; set; }
        public string Status { get; set; } = "pending";
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Succeeded { get; set; }
        public int Skipped { get; set; }
        public int Partial { get; set; }
        public int Failed { get; set; }
        public bool ForceRefresh { get; set; }
        public List<DanmuEpisodeDownloadResult> Episodes { get; set; } = new List<DanmuEpisodeDownloadResult>();
    }

    public class DanmuDownloadStopResult
    {
        public bool Success { get; set; }
        public int StoppedTasks { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class DanmuSearchCancellationResult
    {
        public bool Success { get; set; }
        public string SearchOperationId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class DanmuEpisodeDownloadResult
    {
        public string ItemId { get; set; } = string.Empty;
        public int? EpisodeNumber { get; set; }
        public int? SourceEpisodeNumber { get; set; }
        public string EpisodeName { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string Message { get; set; } = string.Empty;
        public string SourceSite { get; set; } = string.Empty;
        public string SourceCandidateId { get; set; } = string.Empty;
        public string SourceEpisodeId { get; set; } = string.Empty;
        public string MatchOrigin { get; set; } = string.Empty;
    }

    public class DanmuEpisodeDownloadOutcome
    {
        public string Status { get; set; } = "success";
        public string Message { get; set; } = "下载成功";
        public int SegmentTotal { get; set; }
        public int SegmentFailed { get; set; }

        // A completed download deliberately carries its prospective binding instead
        // of persisting it itself.  The caller may have selected a timeout or
        // cancellation result while a provider continues running in the background.
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderValue { get; set; } = string.Empty;
        public bool FilePersisted { get; set; }
        public long ProviderWriteGeneration { get; set; }
    }

    public static class DanmuMatchIntent
    {
        public const string Default = "default";
        public const string Rematch = "rematch";
    }
}
