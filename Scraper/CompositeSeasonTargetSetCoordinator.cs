using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Model;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>
    /// One orchestration shape for Series (many targets) and Season (one
    /// target) previews.  The supplied builder remains the sole authority for
    /// detail resolution and Episode-plan construction.
    /// </summary>
    public static class CompositeSeasonTargetSetCoordinator
    {
        public static async Task<List<DanmuSeasonMatchResult>> BuildAsync(
            IEnumerable<CompositeSeasonTargetRequest> targets,
            CancellationToken cancellationToken)
        {
            var ordered = (targets ?? Enumerable.Empty<CompositeSeasonTargetRequest>()).ToList();
            if (ordered.Any(target => target == null || string.IsNullOrWhiteSpace(target.SeasonId) ||
                                      target.BuildPreviewAsync == null))
            {
                throw new ArgumentException("Every Season target requires a stable id and preview builder.", nameof(targets));
            }
            if (ordered.GroupBy(target => target.SeasonId, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                throw new ArgumentException("A target set cannot contain the same Season twice.", nameof(targets));
            }

            var results = new List<DanmuSeasonMatchResult>(ordered.Count);
            foreach (var target in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var child = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    child.CancelAfter(BoundedSearchPolicy.Shared.Options.InteractiveOperationTimeout);
                    var result = await target.BuildPreviewAsync(child.Token, cancellationToken).ConfigureAwait(false);
                if (result == null || !string.Equals(result.SeasonId, target.SeasonId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A Season target returned a mismatched preview.");
                }
                results.Add(result);
                }
            }
            return results;
        }
    }

    public sealed class CompositeSeasonTargetRequest
    {
        public string SeasonId { get; set; } = string.Empty;
        public Func<CancellationToken, CancellationToken, Task<DanmuSeasonMatchResult>> BuildPreviewAsync { get; set; }
    }
}
