using System;
using System.Collections.Generic;

namespace Emby.Plugin.Danmu.Model
{
    /// <summary>
    /// A local episode as seen by the composite-season planner. The Emby ItemId,
    /// rather than a display number, is its identity.
    /// </summary>
    public sealed class CompositeSeasonLocalEpisode
    {
        public string ItemId { get; set; } = string.Empty;
        public int? EpisodeNumber { get; set; }

        /// <summary>Stable library order supplied by the Emby-facing adapter.</summary>
        public int? SortOrder { get; set; }
    }

    /// <summary>Stable upstream identity used for composite-season detection.</summary>
    public sealed class CompositeSeasonSourceIdentity : IEquatable<CompositeSeasonSourceIdentity>
    {
        public string ProviderId { get; set; } = string.Empty;
        // Canonical upstream identity used for composite detection.
        public string MediaId { get; set; } = string.Empty;
        // The verified request token accepted by GetMedia. Some providers return
        // a canonical media id that is not valid as a later lookup parameter.
        public string MediaLookupId { get; set; } = string.Empty;

        public bool IsValid => !string.IsNullOrWhiteSpace(ProviderId) &&
                               !string.IsNullOrWhiteSpace(MediaId);

        public bool Equals(CompositeSeasonSourceIdentity other)
        {
            return other != null &&
                   string.Equals(ProviderId, other.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(MediaId, other.MediaId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as CompositeSeasonSourceIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(ProviderId ?? string.Empty) * 397) ^
                    StringComparer.OrdinalIgnoreCase.GetHashCode(MediaId ?? string.Empty);
            }
        }
    }

    public sealed class CompositeSeasonSourceEpisode
    {
        public string EpisodeId { get; set; } = string.Empty;
        public string CommentId { get; set; } = string.Empty;
        public int? EpisodeNumber { get; set; }
    }

    /// <summary>An exact local-to-upstream mapping, safe to use for downloading.</summary>
    public sealed class CompositeSeasonEpisodeMapping
    {
        public string LocalEpisodeItemId { get; set; } = string.Empty;
        public CompositeSeasonSourceIdentity Source { get; set; } = new CompositeSeasonSourceIdentity();
        public string SourceEpisodeId { get; set; } = string.Empty;
        public string CommentId { get; set; } = string.Empty;
        public int? SourceEpisodeNumber { get; set; }
        public string Origin { get; set; } = string.Empty;
    }

    /// <summary>A contiguous gap in stable local order, presented as a temporary season.</summary>
    public sealed class CompositeSeasonUnmatchedRun
    {
        public List<CompositeSeasonLocalEpisode> Episodes { get; set; } =
            new List<CompositeSeasonLocalEpisode>();
    }

    public sealed class CompositeSeasonPlan
    {
        public List<CompositeSeasonLocalEpisode> OrderedEpisodes { get; set; } =
            new List<CompositeSeasonLocalEpisode>();
        public List<CompositeSeasonEpisodeMapping> Mappings { get; set; } =
            new List<CompositeSeasonEpisodeMapping>();
        public List<CompositeSeasonUnmatchedRun> UnmatchedRuns { get; set; } =
            new List<CompositeSeasonUnmatchedRun>();
        public bool IsComposite { get; set; }
    }

    /// <summary>
    /// Applies one verified source to the unmatched run containing LocalStartEpisodeItemId.
    /// A zero RequestedEpisodeCount means as much of the run as the source covers.
    /// </summary>
    public sealed class CompositeSeasonSegmentRequest
    {
        public string LocalStartEpisodeItemId { get; set; } = string.Empty;
        public int RequestedEpisodeCount { get; set; }
        public CompositeSeasonSourceIdentity Source { get; set; } = new CompositeSeasonSourceIdentity();
        public List<CompositeSeasonSourceEpisode> SourceEpisodes { get; set; } =
            new List<CompositeSeasonSourceEpisode>();
        public string SourceStartEpisodeId { get; set; } = string.Empty;
        public string Origin { get; set; } = "manual";
    }
}
