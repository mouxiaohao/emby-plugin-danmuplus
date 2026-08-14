using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoEpisode
    {
        [DataMember(Name="id")]
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [DataMember(Name="aid")]
        [JsonPropertyName("aid")]
        public long? AId { get; set; } // Made nullable to match usage in Bilibili.cs

        [DataMember(Name="bvid")]
        [JsonPropertyName("bvid")]
        public string BvId { get; set; }

        [DataMember(Name="cid")]
        [JsonPropertyName("cid")]
        public long CId { get; set; }

        [DataMember(Name="title")]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [DataMember(Name="long_title")]
        [JsonPropertyName("long_title")]
        public string LongTitle { get; set; }

        [DataMember(Name="cover")]
        [JsonPropertyName("cover")]
        public string Cover { get; set; }

        [DataMember(Name="pub_time")]
        [JsonPropertyName("pub_time")]
        public long PubTime { get; set; } // Unix timestamp

        [DataMember(Name="duration")]
        [JsonPropertyName("duration")]
        public long Duration { get; set; } // Duration in milliseconds for PGC episodes

        [DataMember(Name="badge")]
        [JsonPropertyName("badge")]
        public string Badge { get; set; } // e.g., "会员", "预告"

        [DataMember(Name="badge_type")]
        [JsonPropertyName("badge_type")]
        public int BadgeType { get; set; } // Numeric type for the badge

        [JsonPropertyName("section_type")]
        public int? SectionType { get; set; }

        [JsonIgnore]
        public SourceMetadata SourceMetadata { get; set; }
    }
}
