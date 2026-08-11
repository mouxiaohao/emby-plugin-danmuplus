using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Controllers;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.EpisodeSelectionPolicyRegression
{
    internal static class Program
    {
        private static int Main()
        {
            SelectsExactlyOneEpisodeFromSixtyCandidates();
            RejectsMissingOrUnusableExactEpisode();
            DirectProviderIdDetailUsesExactEpisodeResolver();
            HighConfidenceEpisodeDiscoveryDoesNotResolveDetail();
            VerifiesTwoPhaseControllerContract();
            Console.WriteLine("Episode selection policy regression checks passed.");
            return 0;
        }

        private static void SelectsExactlyOneEpisodeFromSixtyCandidates()
        {
            var media = new ScraperMedia
            {
                Id = "candidate-60",
                Episodes = Enumerable.Range(1, 60).Select(number => new ScraperEpisode
                {
                    Id = "episode-" + number,
                    CommentId = "comment-" + number,
                    EpisodeNumber = number,
                    Title = "Episode " + number,
                }).ToList(),
            };
            Assert(DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(
                    media, "episode-47", out var exact, out var source),
                "a selected candidate must resolve only its exact upstream episode");
            Assert(source.Id == "episode-47" && exact.Episodes.Count == 1 &&
                   exact.Episodes[0].Id == "episode-47" && exact.Episodes[0].EpisodeNumber == 1,
                "the download media must be one exact episode rather than a positional candidate list");
        }

        private static void RejectsMissingOrUnusableExactEpisode()
        {
            var media = new ScraperMedia
            {
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "gone", CommentId = string.Empty, EpisodeNumber = 9 },
                },
            };
            Assert(!DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(media, "gone", out _, out _),
                "an episode without a current comment id must fail that selection, not fall back by number");
            Assert(!DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(media, "missing", out _, out _),
                "a missing sourceEpisodeId must never positionally select another episode");
            Assert(typeof(DanmuSelectedCandidateSourceEpisode).GetProperty("CommentId") == null,
                "the detail-preview wire DTO must not disclose comment identifiers");
        }

        private static void DirectProviderIdDetailUsesExactEpisodeResolver()
        {
            var scraper = new InstrumentedScraper("EpisodeID");
            var episode = new Episode { IndexNumber = 7 };
            episode.ProviderIds[scraper.ProviderId] = "direct-episode-token";
            var method = typeof(DanmuController).GetMethod(
                "ResolveSelectedCandidateDetailAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert(method != null, "the selected-candidate resolver must remain independently testable");

            var directTask = (Task<ScraperMedia>)method.Invoke(null, new object[]
            {
                episode, new Season(), scraper, "direct-episode-token",
            });
            var direct = directTask.GetAwaiter().GetResult();
            Assert(scraper.MediaCalls == 0 && scraper.MediaEpisodeCalls == 1 &&
                   direct?.Episodes.Single().Id == "direct-episode-token" &&
                   direct.Episodes.Single().CommentId == "exact-comment",
                "an ordinary Episode-local ProviderId detail preview must call only the exact Episode resolver");

            var searchedTask = (Task<ScraperMedia>)method.Invoke(null, new object[]
            {
                episode, new Season(), scraper, "searched-media-candidate",
            });
            searchedTask.GetAwaiter().GetResult();
            Assert(scraper.MediaCalls == 1 && scraper.MediaEpisodeCalls == 1,
                "a searched candidate must use GetMedia while preserving the direct-token branch above");
        }

        private static void HighConfidenceEpisodeDiscoveryDoesNotResolveDetail()
        {
            var scraper = new InstrumentedScraper("HighConfidence", new List<ScraperSearchInfo>
            {
                new ScraperSearchInfo
                {
                    Id = "high-confidence-candidate",
                    Name = "Exact Series",
                    Year = 2024,
                    EpisodeSize = 12,
                },
            });
            var search = DanmuMatchSearchEngine.SearchSeasonAsync(
                new AbstractScraper[] { scraper },
                "Exact Series", "Season 1", 2024, 12, null, null).GetAwaiter().GetResult();
            Assert(DanmuMatchScorer.SelectAutoCandidate(search.Candidates) != null && scraper.MediaCalls == 0,
                "even a high-confidence Episode phase-one candidate search must perform zero media detail calls");
        }

        private static void VerifiesTwoPhaseControllerContract()
        {
            var root = FindRepositoryRoot(AppContext.BaseDirectory);
            var controller = File.ReadAllText(Path.Combine(root, "Core", "Controllers", "DanmuController.cs"));
            var episodePreviewStart = controller.IndexOf(
                "private async Task<DanmuItemMatchResult> GetEpisodeMatchPreview", StringComparison.Ordinal);
            var episodePreviewEnd = controller.IndexOf(
                "private async Task<DanmuSeasonMatchResult> GetSeasonMatchPreview", episodePreviewStart,
                StringComparison.Ordinal);
            var episodePreview = controller.Substring(episodePreviewStart, episodePreviewEnd - episodePreviewStart);
            Assert(controller.Contains("GetSelectedCandidatePreview", StringComparison.Ordinal) &&
                   controller.Contains("ResolveSelectedCandidateDetailAsync", StringComparison.Ordinal) &&
                   controller.Contains("DanmuExactEpisodeSelectionHelper.TryCreateExactMedia", StringComparison.Ordinal) &&
                   controller.Contains("[DataMember(Name=\"sourceEpisodeId\")]", StringComparison.Ordinal),
                "controller must expose explicit detail selection and exact source-id confirmation");
            Assert(!episodePreview.Contains("foreach (var candidate in result.Candidates)", StringComparison.Ordinal) &&
                   !episodePreview.Contains("GetMedia(season, candidate.Id)", StringComparison.Ordinal) &&
                   episodePreview.Contains("metadataOnly: true", StringComparison.Ordinal),
                "Episode candidate discovery must not issue detail requests for every search result");
            Assert(controller.Contains("effectiveForceSearch = forceSearch || compositeMarked || metadataOnly", StringComparison.Ordinal) &&
                   controller.Contains("if (!metadataOnly)", StringComparison.Ordinal),
                "metadata-only Episode discovery must bypass Season bindings and suppress high-confidence composite detail");
            Assert(controller.Contains("task.CandidateId = request.CandidateId", StringComparison.Ordinal) &&
                   !controller.Contains("season.GetProviderId(task.Site)", StringComparison.Ordinal),
                "retry must retain the selected lookup token and must not fall back to a Season binding");
        }

        private static string FindRepositoryRoot(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Emby.Plugin.Danmu.csproj")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate plugin repository root.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class InstrumentedScraper : AbstractScraper
        {
            private readonly string _providerId;
            private readonly List<ScraperSearchInfo> _searchResults;

            public InstrumentedScraper(string providerId, List<ScraperSearchInfo> searchResults = null) : base(null)
            {
                _providerId = providerId;
                _searchResults = searchResults ?? new List<ScraperSearchInfo>();
            }

            public int MediaCalls { get; private set; }
            public int MediaEpisodeCalls { get; private set; }
            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) => Task.FromResult(_searchResults);
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) => Task.FromResult(_searchResults);
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id)
            {
                MediaCalls++;
                return Task.FromResult(new ScraperMedia
                {
                    Id = id,
                    Episodes = new List<ScraperEpisode>
                    {
                        new ScraperEpisode { Id = "searched-source", CommentId = "searched-comment", EpisodeNumber = 1 },
                    },
                });
            }
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
            {
                MediaEpisodeCalls++;
                return Task.FromResult(new ScraperEpisode
                {
                    Id = id,
                    CommentId = "exact-comment",
                    ParentMediaId = "exact-parent",
                    EpisodeNumber = 7,
                    Title = "Exact source episode",
                });
            }
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) =>
                Task.FromResult<ScraperDanmaku>(null);
        }
    }
}
