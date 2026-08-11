using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Emby.Plugin.Danmu.Model
{
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
    }

    /// <summary>
    /// GET query binding in Emby 4.9 only supports scalar values. Composite
    /// selections therefore travel as one JSON string and are decoded here,
    /// before any candidate/source validation or download work begins.
    /// </summary>
    public static class DanmuCompositeSeasonSelectionJson
    {
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
        // Whether this composite-route task actually spans two or more verified
        // upstream media identities. Partial one-source downloads must not
        // permanently clear the Season binding.
        public bool IsCompositePlan { get; set; }
        public long SeasonProviderWriteGeneration { get; set; }
        public bool SeasonProviderCommitted { get; set; }
        public string Status { get; set; } = "pending";
        public string Message { get; set; } = string.Empty;
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
