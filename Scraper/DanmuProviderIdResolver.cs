using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// Resolves Emby external identifiers in configured provider order within an
    /// explicit item-local scope. Series identifiers are never inferred here.
    /// </summary>
    public static class DanmuProviderIdResolver
    {
        public static BaseItem[] GetMovieScopes(Movie movie)
        {
            return movie == null ? Array.Empty<BaseItem>() : new BaseItem[] { movie };
        }

        public static BaseItem[] GetSeasonScopes(Season season)
        {
            return season == null ? Array.Empty<BaseItem>() : new BaseItem[] { season };
        }

        public static BaseItem[] GetEpisodeScopes(Episode episode, Season season)
        {
            return new BaseItem[] { episode, season }.Where(x => x != null).ToArray();
        }

        /// <summary>
        /// Direct evidence for a single-Episode decision must be owned by that
        /// Episode. The containing Season is search context, not Episode-local
        /// ProviderId evidence.
        /// </summary>
        public static BaseItem[] GetSingleEpisodeDirectScopes(Episode episode)
        {
            return episode == null ? Array.Empty<BaseItem>() : new BaseItem[] { episode };
        }

        public static ProviderIdDictionary GetItemLocalProviderIds(
            BaseItem item,
            IEnumerable<AbstractScraper> scraperSource)
        {
            return GetItemLocalProviderIds(
                item,
                item is Season ? item.GetParent() as Series : null,
                scraperSource);
        }

        public static ProviderIdDictionary GetItemLocalProviderIds(
            BaseItem item,
            Series parentSeries,
            IEnumerable<AbstractScraper> scraperSource)
        {
            var result = new ProviderIdDictionary();
            if (item?.ProviderIds != null)
            {
                foreach (var pair in item.ProviderIds)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            // Scope, not value equality, establishes ownership. A legitimate
            // Season ID may equal a stale/ignored Series ID (for example when a
            // Series identifier points to its latest Season), so comparing values
            // would incorrectly discard the Season's own configured-priority ID.
            return result;
        }

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
            ILogger logger,
            Series parentSeries = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var decision = new DanmuMatchDecision();
            var scrapers = (scraperSource ?? Enumerable.Empty<AbstractScraper>())
                .Where(x => x != null)
                .ToList();
            var scopes = (itemScopes ?? Enumerable.Empty<BaseItem>())
                .Where(x => x != null && !(x is Series))
                .ToList();
            var foundIdentifier = false;

            for (var sourceOrder = 0; sourceOrder < scrapers.Count; sourceOrder++)
            {
                var scraper = scrapers[sourceOrder];
                var key = scraper.ProviderId;
                foreach (var scope in scopes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var localProviderIds = scope is Season && parentSeries != null
                        ? GetItemLocalProviderIds(scope, parentSeries, scrapers)
                        : GetItemLocalProviderIds(scope, scrapers);
                    if (!localProviderIds.TryGetValue(key, out var externalId) ||
                        string.IsNullOrWhiteSpace(externalId))
                    {
                        continue;
                    }

                    foundIdentifier = true;
                    try
                    {
                        var resolution = await BoundedSearchPolicy.Shared.ExecuteAsync(
                            scraper.ProviderId,
                            ignored => scope is Episode
                                ? ResolveDirectEpisodeMediaAsync(
                                    scraper, (Episode)scope, externalId, ((Episode)scope).IndexNumber ?? 0)
                                : scraper.GetMedia(scope, externalId),
                            cancellationToken).ConfigureAwait(false);
                        if (resolution.Status != BoundedSearchExecutionStatus.Completed)
                        {
                            // A caller cancellation is an operation boundary,
                            // not a failed ProviderId lookup. Preserve it for
                            // the interactive/automatic operation coordinator.
                            cancellationToken.ThrowIfCancellationRequested();
                            decision.Diagnostics.Add("provider-id-unresolved:" + scraper.ProviderId);
                            continue;
                        }

                        var media = resolution.Result;
                        if (!IsUsable(media))
                        {
                            decision.Diagnostics.Add("provider-id-unresolved:" + scraper.ProviderId);
                            continue;
                        }

                        // An Episode ProviderId is a precise lookup token even
                        // if its verified response also reveals a canonical
                        // parent media identity. Keep the token in Candidate.Id
                        // for exact download/retry; the media carries the
                        // stable parent identity for composite planning.
                        var resolvedId = scope is Episode
                            ? externalId
                            : string.IsNullOrWhiteSpace(media.Id) ? externalId : media.Id;
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
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
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
                // The lookup token remains the saved Episode ProviderId. When
                // a provider verified parent ownership, expose that canonical
                // media identity so composite planning can distinguish S1/S2.
                Id = !string.IsNullOrWhiteSpace(sourceEpisode.ParentMediaId)
                    ? sourceEpisode.ParentMediaId
                    : providerId,
                ProviderId = scraper.ProviderId,
                Title = sourceEpisode.Title ?? string.Empty,
                EpisodeCount = 1,
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode
                    {
                        Id = sourceEpisode.Id,
                        CommentId = sourceEpisode.CommentId,
                        ParentMediaId = sourceEpisode.ParentMediaId,
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
