using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class SearchResult
    {
        [DataMember(Name="page")]
        public int Page { get; set; }

        [DataMember(Name="pagesize")]
        public int PageSize { get; set; }

        [DataMember(Name="numResults")]
        public int NumResults { get; set; }

        [DataMember(Name="numPages")]
        public int NumPages { get; set; }
        
        [DataMember(Name="result")]
        public List<Media> Result { get; set; }

        public List<BilibiliSearchDiagnostic> Diagnostics { get; set; } =
            new List<BilibiliSearchDiagnostic>();

        // Typed media pages are an optional enhancement of the aggregate
        // search.  Keep their terminal failures visible to server-side cache
        // and logging decisions without serializing them or turning them into
        // a frontend partial-failure diagnostic.
        [JsonIgnore]
        [IgnoreDataMember]
        public List<BilibiliSearchDiagnostic> SuppressedDiagnostics { get; set; } =
            new List<BilibiliSearchDiagnostic>();

        [JsonIgnore]
        [IgnoreDataMember]
        public bool SessionAvailable { get; set; } = true;
    }

    public sealed class BilibiliSearchDiagnostic
    {
        public string SearchType { get; set; }
        public int Page { get; set; }
        public string Message { get; set; }
    }
}
