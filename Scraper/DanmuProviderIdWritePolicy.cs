using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Entities;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Builds the exact metadata dictionary for an accepted persisted write.
    /// Only registered ordinary danmu keys participate; Manual and unrelated
    /// metadata-provider keys are deliberately opaque to this policy.
    /// </summary>
    public static class DanmuProviderIdWritePolicy
    {
        public static ProviderIdDictionary BuildSuccessfulWrite(
            IReadOnlyDictionary<string, string> current,
            IEnumerable<string> registeredProviderIds,
            string selectedProviderId,
            string selectedProviderValue,
            bool enforceOrdinaryUniqueness)
        {
            var result = new ProviderIdDictionary();
            if (current != null)
            {
                foreach (var pair in current)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            if (enforceOrdinaryUniqueness)
            {
                foreach (var providerId in (registeredProviderIds ?? Enumerable.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!string.Equals(providerId, selectedProviderId, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Remove(providerId);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedProviderId) &&
                !string.IsNullOrWhiteSpace(selectedProviderValue))
            {
                result[selectedProviderId] = selectedProviderValue;
            }

            return result;
        }
    }
}
