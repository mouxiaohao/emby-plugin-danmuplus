using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Resolves Emby external identifiers in configured provider order.  Provider
    /// priority is intentionally outside the item hierarchy: a Series identifier
    /// on an earlier enabled provider beats an Episode identifier on a later one.
    /// </summary>
    public static class DanmuProviderIdResolver
    {
        public static Dictionary<string, string> GetEnabledProviderIdKeys(
            IEnumerable<AbstractScraper> scraperSource)
        {
            return (scraperSource ?? Enumerable.Empty<AbstractScraper>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ProviderId))
                .GroupBy(x => x.ProviderId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().ProviderId, StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<DanmuMatchDecision> ResolveAsync(
            IEnumerable<AbstractScraper> scraperSource,
            IEnumerable<BaseItem> itemScopes,
            ILogger logger)
        {
            var decision = new DanmuMatchDecision();
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>())
                .Where(x => x != null)
                .ToList();
            var scopes = (itemScopes ?? Enumerable.Empty<BaseItem>())
                .Where(x => x != null)
                .ToList();
            var foundIdentifier = false;

            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                var key = scraper.ProviderId;
                foreach (var scope in scopes)
                {
                    if (scope.ProviderIds == null ||
                        !scope.ProviderIds.TryGetValue(key, out var externalId) ||
                        string.IsNullOrWhiteSpace(externalId))
                    {
                        continue;
                    }

                    foundIdentifier = true;
                    try
                    {
                        ScraperMedia media;
                        if (scope is Episode)
                        {
                            media = await ResolveDirectEpisodeMediaAsync(
                                scraper, (Episode)scope, externalId, ((Episode)scope).IndexNumber ?? 0)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            media = await scraper.GetMedia(scope, externalId).ConfigureAwait(false);
                        }
                        if (!IsUsable(media))
                        {
                            decision.Diagnostics.Add("provider-id-unresolved:" + scraper.ProviderId);
                            continue;
                        }

                        var resolvedId = string.IsNullOrWhiteSpace(media.Id) ? externalId : media.Id;
                        decision.Scraper = scraper;
                        decision.Media = media;
                        decision.MatchOrigin = "provider-id";
                        decision.DecisionReason = "provider-id";
                        decision.ResolvedProviderId = resolvedId;
                        decision.ResolvedProviderIdKey = key;
                        decision.ResolvedScopeType = GetScopeType(scope);
                        decision.ResolvedScopeItemId = scope.Id.ToString();
                        decision.Candidate = new DanmuMatchCandidate
                        {
                            Id = resolvedId,
                            Site = scraper.ProviderId,
                            SiteName = scraper.ProviderName,
                            SourceOrder = sourceOrder,
                            Name = "标题未知",
                            Score = 1,
                            MatchOrigin = decision.MatchOrigin,
                            DecisionReason = decision.DecisionReason,
                            Reason = "使用已保存的 ProviderId",
                        };
                        ApplyResolvedUpstreamMetadata(decision.Candidate, media);
                        return decision;
                    }
                    catch (Exception ex)
                    {
                        decision.Diagnostics.Add("provider-id-unresolved:" + scraper.ProviderId);
                        logger?.LogError(ex, "[{0}] ProviderId 解析失败: item={1}, id={2}",
                            scraper.Name, scope.Name, externalId);
                    }
                }
            }

            if (foundIdentifier && decision.Diagnostics.Count == 0)
            {
                decision.Diagnostics.Add("provider-id-unresolved");
            }

            return decision;
        }

        /// <summary>
        /// A ProviderId saved directly on an Emby Episode is an upstream episode
        /// identifier, not a season/media identifier.  Convert that exact episode
        /// response into the one-item media shape expected by the tracked download
        /// path without ever routing it through GetMedia.
        /// </summary>
        public static async Task<ScraperMedia> ResolveDirectEpisodeMediaAsync(
            AbstractScraper scraper,
            Episode episode,
            string providerId,
            int sourceEpisodeNumber)
        {
            if (scraper == null || episode == null || string.IsNullOrWhiteSpace(providerId))
            {
                return null;
            }

            var sourceEpisode = await scraper.GetMediaEpisode(episode, providerId).ConfigureAwait(false);
            if (sourceEpisode == null || string.IsNullOrWhiteSpace(sourceEpisode.CommentId))
            {
                return null;
            }

            return new ScraperMedia
            {
                Id = providerId,
                ProviderId = scraper.ProviderId,
                Title = sourceEpisode.Title ?? string.Empty,
                EpisodeCount = 1,
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode
                    {
                        Id = sourceEpisode.Id,
                        CommentId = sourceEpisode.CommentId,
                        Title = sourceEpisode.Title,
                        // The direct id already identifies this exact source episode.
                        // Prefer a reliable upstream number, then the local number;
                        // a one-item direct result can safely use position 1.
                        EpisodeNumber = sourceEpisode.EpisodeNumber.GetValueOrDefault() > 0
                            ? sourceEpisode.EpisodeNumber
                            : sourceEpisodeNumber > 0 ? sourceEpisodeNumber : 1,
                    },
                },
            };
        }

        private static bool IsUsable(ScraperMedia media)
        {
            return media != null &&
                   (!string.IsNullOrWhiteSpace(media.Id) ||
                    !string.IsNullOrWhiteSpace(media.CommentId) ||
                   (media.Episodes != null && media.Episodes.Count > 0));
        }

        private static void ApplyResolvedUpstreamMetadata(DanmuMatchCandidate candidate, ScraperMedia media)
        {
            if (candidate == null || media == null)
            {
                return;
            }

            var usableEpisodeCount = (media.Episodes ?? new List<ScraperEpisode>())
                .Count(x => x != null && !string.IsNullOrWhiteSpace(x.CommentId));
            var declaredCount = media.EpisodeCount.GetValueOrDefault();

            // Never label a local Emby scope name as the upstream title.
            candidate.Name = !string.IsNullOrWhiteSpace(media.Title)
                ? media.Title
                : "标题未知";
            candidate.Category = media.Category ?? string.Empty;
            candidate.Year = media.Year;
            candidate.EpisodeSize = declaredCount > 0 ? declaredCount : usableEpisodeCount;
        }

        private static string GetScopeType(BaseItem scope)
        {
            if (scope is Episode) return "Episode";
            if (scope is Season) return "Season";
            if (scope is Series) return "Series";
            return scope?.GetType().Name ?? string.Empty;
        }
    }

    public sealed class DanmuMatchDecision
    {
        public AbstractScraper Scraper { get; set; }
        public ScraperMedia Media { get; set; }
        public DanmuMatchCandidate Candidate { get; set; }
        public string MatchOrigin { get; set; } = string.Empty;
        public string DecisionReason { get; set; } = string.Empty;
        public string ResolvedProviderId { get; set; } = string.Empty;
        public string ResolvedProviderIdKey { get; set; } = string.Empty;
        public string ResolvedScopeType { get; set; } = string.Empty;
        public string ResolvedScopeItemId { get; set; } = string.Empty;
        public List<string> Diagnostics { get; } = new List<string>();
    }
}
