using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper.Entity
{
    /// <summary>
    /// Provider search output that keeps usable candidates when an optional
    /// upstream request fails and reports that failure to the coordinator.
    /// </summary>
    public sealed class ScraperSearchResult
    {
        public List<ScraperSearchInfo> Candidates { get; set; } = new List<ScraperSearchInfo>();

        public List<ScraperSearchDiagnostic> Diagnostics { get; set; } =
            new List<ScraperSearchDiagnostic>();
    }

    public sealed class ScraperSearchDiagnostic
    {
        public string Status { get; set; } = "failed";

        public string Message { get; set; } = string.Empty;

        public bool TimedOut { get; set; }

        public bool Cancelled { get; set; }
    }
}
