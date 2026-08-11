using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoUgcSeason
    {
        [DataMember(Name = "id")]
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [DataMember(Name = "title")]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [DataMember(Name = "sections")]
        [JsonPropertyName("sections")]
        public List<VideoUgcSection> Sections { get; set; }
    }

    public class VideoUgcSection
    {
        [DataMember(Name = "id")]
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [DataMember(Name = "episodes")]
        [JsonPropertyName("episodes")]
        public List<VideoEpisode> Episodes { get; set; }
    }
}
