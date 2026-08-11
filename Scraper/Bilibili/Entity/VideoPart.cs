using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoPart
    {
        [DataMember(Name="cid")]
        [JsonPropertyName("cid")]
        public long Cid { get; set; }
        [DataMember(Name="page")]
        [JsonPropertyName("page")]
        public int Page { get; set; }
        
        [DataMember(Name="part")]
        [JsonPropertyName("part")]
        public string PartName { get; set; }

        [DataMember(Name="duration")]
        [JsonPropertyName("duration")]
        public long Duration { get; set; } // Duration in seconds
    }
}
