using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoSeason
    {
        [DataMember(Name="season_id")]
        [JsonPropertyName("season_id")]
        public long SeasonId { get; set; }

        [DataMember(Name="title")]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [DataMember(Name="cover")]
        [JsonPropertyName("cover")]
        public string Cover { get; set; }

        [DataMember(Name="evaluate")]
        [JsonPropertyName("evaluate")]
        public string Evaluate { get; set; } // Synopsis or description

        [DataMember(Name="pub_time")]
        [JsonPropertyName("pub_time")]
        public long PubTime { get; set; } // Unix timestamp for publish time

        [DataMember(Name="season_title")]
        [JsonPropertyName("season_title")]
        public string SeasonTitle { get; set; } // e.g., "第一季"

        [DataMember(Name="total_count")]
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; } // Total number of episodes in this season

        [DataMember(Name="episodes")]
        [JsonPropertyName("episodes")]
        public List<VideoEpisode> Episodes { get; set; }
    }
}
