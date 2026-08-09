using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Tencent.Entity
{
    public class TencentCommentDownloadResult
    {
        public List<TencentComment> Comments { get; set; } = new List<TencentComment>();
        public int SegmentTotal { get; set; }
        public int SegmentFailed { get; set; }
        public List<string> FailedSegmentNames { get; set; } = new List<string>();
    }

    public class TencentCommentResult
    {
        [DataMember(Name="segment_span")]
        public string SegmentSpan { get; set; }
        [DataMember(Name="segment_start")]
        public string SegmentStart { get; set; }

        [DataMember(Name="segment_index")]
        public Dictionary<long, TencentCommentSegment> SegmentIndex { get; set; }
    }

    public class TencentCommentSegment
    {
        [DataMember(Name="segment_name")]
        public string SegmentName { get; set; }
        [DataMember(Name="segment_start")]
        public string SegmentStart { get; set; }
    }
}
