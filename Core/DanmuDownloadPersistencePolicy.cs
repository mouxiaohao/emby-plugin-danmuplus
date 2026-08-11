using System;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Core
{
    public static class DanmuDownloadPersistencePolicy
    {
        public static bool ShouldPersist(DanmuEpisodeDownloadOutcome outcome, string providerValueOverride = null)
        {
            if (outcome == null || !outcome.FilePersisted ||
                !(string.Equals(outcome.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(outcome.Status, "partial", StringComparison.OrdinalIgnoreCase)) ||
                string.IsNullOrWhiteSpace(outcome.ProviderId))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(providerValueOverride) ||
                   !string.IsNullOrWhiteSpace(outcome.ProviderValue);
        }
    }
}
