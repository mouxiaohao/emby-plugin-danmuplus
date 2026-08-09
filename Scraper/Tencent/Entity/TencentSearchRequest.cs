using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Tencent.Entity
{
    public class TencentSearchRequest
    {
        [DataMember(Name="filterValue")]
        public string FilterValue { get; set; } = string.Empty;
        [DataMember(Name="retry")]
        public int Retry { get; set; } = 0;
        [DataMember(Name="query")]
        public string Query { get; set; }
        [DataMember(Name="pagenum")]
        public int PageNum { get; set; } = 0;
        [DataMember(Name="pagesize")]
        public int PageSize { get; set; } = 20;
        [DataMember(Name="adRequestInfo")]
        public string AdRequestInfo { get; set; } = string.Empty;
        [DataMember(Name="sdkRequestInfo")]
        public string SdkRequestInfo { get; set; } = string.Empty;
    }
}
