using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    /// <summary>
    /// Presentation metadata from PGC's exact season-detail endpoint. Episode
    /// mapping continues to use the separately normalized ep/list response.
    /// </summary>
    public class VideoSeasonDetail
    {
        [DataMember(Name = "season_id")]
        [JsonPropertyName("season_id")]
        public long SeasonId { get; set; }

        [DataMember(Name = "title")]
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [DataMember(Name = "season_title")]
        [JsonPropertyName("season_title")]
        public string SeasonTitle { get; set; }

        [DataMember(Name = "total")]
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [DataMember(Name = "publish")]
        [JsonPropertyName("publish")]
        public VideoSeasonPublish Publish { get; set; }
    }

    public class VideoSeasonPublish
    {
        [DataMember(Name = "pub_time")]
        [JsonPropertyName("pub_time")]
        public string PubTime { get; set; }
    }
}
