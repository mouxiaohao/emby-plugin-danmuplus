using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoUgcSeason
    {
        [DataMember(Name = "id")]
        public long Id { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "sections")]
        public List<VideoUgcSection> Sections { get; set; }
    }

    public class VideoUgcSection
    {
        [DataMember(Name = "id")]
        public long Id { get; set; }

        [DataMember(Name = "episodes")]
        public List<VideoEpisode> Episodes { get; set; }
    }
}
