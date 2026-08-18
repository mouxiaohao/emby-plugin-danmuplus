using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Scraper.Entity
{
    public class ScraperMedia
    {
        /// <summary>
        /// item是电影/季时，使用本id作为元数据值
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// item是季时，本CommentId用不到
        /// </summary>
        public string CommentId { get; set; }

        /// <summary>
        /// 增加数据来源名称 (e.g., "BilibiliID", "IqiyiID").
        /// </summary>
        public string ProviderId { get; set; }

        // Optional metadata returned by an identifier-specific upstream detail
        // response. These values are deliberately separate from local Emby
        // metadata so an exact ProviderId preview cannot fabricate provenance.
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string Category { get; set; } = string.Empty;
        public int? EpisodeCount { get; set; }
        public SourceMetadata SourceMetadata { get; set; }
        /// <summary>
        /// Server-resolved Movie leaf override. It is never accepted as a raw
        /// browser value; controllers set it only after scoped evidence checks.
        /// </summary>
        [JsonIgnore]
        [IgnoreDataMember]
        public string SelectedMoviePartId { get; set; } = string.Empty;
        public string PartTitle { get; set; } = string.Empty;
        public List<ScraperEpisode> Episodes { get; set; } = new List<ScraperEpisode>();

    }
    
    public class ScraperEpisode
    {
        /// <summary>
        /// 当item是剧集时，使用本id作为元数据值
        /// </summary>
        public string Id { get; set; }
        public string CommentId { get; set; }
        /// <summary>
        /// Stable parent media identity when an exact Episode response can
        /// verify it. Empty means the provider exposed only an episode token.
        /// </summary>
        public string ParentMediaId { get; set; }
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Provider source episode number when the upstream response exposes one
        /// explicitly or it can be parsed unambiguously from the episode title.
        /// Null retains compatibility with providers/legacy data that do not
        /// expose reliable numbering.
        /// </summary>
        public int? EpisodeNumber { get; set; }
        /// <summary>Optional parent media/season metadata, never the episode title.</summary>
        public SourceMetadata SourceMetadata { get; set; }
    }

    /// <summary>
    /// Provider-internal Movie leaf that can be verified and downloaded
    /// independently. Public responses expose an opaque token and Title only.
    /// </summary>
    public sealed class ScraperMoviePart
    {
        [JsonIgnore]
        [IgnoreDataMember]
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int? Index { get; set; }
        public bool IsDownloadable { get; set; }
        public bool IsExplicitNonMain { get; set; }
    }
}
