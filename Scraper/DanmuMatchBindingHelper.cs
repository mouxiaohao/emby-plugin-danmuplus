using System;
using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Scraper
{
    public static class DanmuMatchBindingHelper
    {
        public static bool TryGetSavedManualBinding(
            bool forceSearch,
            IEnumerable<AbstractScraper> scrapers,
            IReadOnlyDictionary<string, string> providerIds,
            out AbstractScraper scraper,
            out string manualId)
        {
            scraper = null;
            manualId = string.Empty;
            if (forceSearch || scrapers == null || providerIds == null)
            {
                return false;
            }

            foreach (var candidate in scrapers)
            {
                if (candidate != null &&
                    providerIds.TryGetValue(candidate.ProviderId + "Manual", out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    scraper = candidate;
                    manualId = value;
                    return true;
                }
            }

            return false;
        }
    }
}
