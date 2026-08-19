using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Controllers;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Dandan;
using Emby.Plugin.Danmu.Scraper.Entity;
using DandanEpisode = Emby.Plugin.Danmu.Scraper.Dandan.Entity.Episode;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class CompositeSeasonPlannerTests
    {
        public static void Run()
        {
            AlignsSparseAndGappedSegmentsByExplicitNumbers();
            PreservesExplicitAnchorsAndRequestedLocalRows();
            FallsBackPositionallyForEveryUnreliableNumberShape();
            ResolvesOnlyAuthoritativeSourceAnchorsAndFailsClosedOnOverflow();
            PreservesSourceNumberProvenanceAndOrdinalOrder();
            AdvancesContinuationBySegmentWindowFrontiers();
            ContinuesOwningWindowsWithForwardOnlyIndependentModes();
            FingerprintsConsideredUnmappedGapRowsAndRejectsLegacyEntryPoints();
            RoundTripsAuthoritativeCompactSelectionsWithoutLeakingServerEvidence();
            PreservesExplicitEvidenceAndBuildsRemainingRuns();
            SupportsSourceStartsAndPartialCoverage();
            MapsFrierenThirtyEightEpisodesAcrossTwoUpstreamSeasons();
            KeepsMarkedFrierenEpisodeEvidenceWhenNoCandidateIsUsable();
            KeepsMarkedPreviewDirectEvidenceAheadOfFreshSearch();
            ContinuesPrimaryAcrossInteriorExactEpisode();
            SelectsSupplementalAfterPrimaryExhaustion();
            ContinuesSupplementalAcrossSpecialAndReentrantDirectEvidence();
            ParsesCompositeSelectionsFromScalarQueryJson();
            SupportsCompositeMappingForAnyLocalSeason();
            DoesNotClassifySingleSourcePartialCoverageAsComposite();
            MapsTwentyFiveEpisodePartSourcesWithBindingSafety();
            CoordinatesSingletonAndSeriesTargetSetsIdentically();
            CoordinatesWithoutChildDeadlineAndPropagatesExplicitCancellation();
            MapsMultipleSpecialRunsWithoutChangingLocalSeasonMembership();
            SeparatesCanonicalMediaIdentityFromLookupToken();
            RejectsOverlapsAndUnverifiedMappings();
            IdentifiesCompositeSourcesByProviderAndMediaId();
            KeepsDirectEpisodeEvidenceFromFalselyCreatingCompositeSources();
            SortsByStableLocalIdentityWithoutDependingOnDisplayNumbers();
            ValidatesAndAppliesAuthoritativeExclusionsBeforeDirectEvidence();
            SplitsEditableRunsAndRestoresOnlyTheRequestedRange();
            RetainsCompositeSafetyForSubsetAndZeroPersist();
            RebuildsPreviewAndDownloadFromTheSameExclusionAwarePlan();
            RejectsIncompleteAutomaticSeasonAndResidualSearches();
            VerifiesLockedProviderCompletionSeam();
            PreservesDirectMetadataAcrossRemoveReplacementAndRestore();
            RetainsCompositeSafetyWhenReplacementCollapsesToOneSource();
            RejectsForeignAndStaleTemporaryRangesWithoutMutatingThePlan();
            VerifiesControllerParityMetadataAndDialogResetContracts();
            PreservesServerCandidateScoreAcrossOwningPlansAndGroups();
            PreservesSourceMetadataAcrossEveryBindingEntryPoint();
            ProjectsBoundedSourceEpisodeNamesWithoutChangingPlanAuthority();
            PreservesExactBindingScoreIntoSelectedCandidate();
            DerivesSourceSurplusOnlyFromAppliedAuthoritativeDetails();
        }

        private static void AlignsSparseAndGappedSegmentsByExplicitNumbers()
        {
            var sparseNumbers = Enumerable.Range(1, 6).Concat(Enumerable.Range(10, 4)).ToArray();
            var sparse = sparseNumbers.Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "spy-" + number, EpisodeNumber = number, SortOrder = number,
            }).ToList();
            var request = Segment("spy-1", "DandanID", "spy-s3", "source-1",
                Enumerable.Range(1, 13).Select(number => Source("source-" + number, "comment-" + number, number)));
            request.AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset;
            Assert(CompositeSeasonPlanner.TryResolveSegment(sparse, request, out var resolved, out var error), error);
            Assert(resolved.Mode == CompositeSeasonAlignmentMode.NumberAware &&
                   resolved.ConsideredLocalEpisodes.Count == 10 && resolved.Mappings.Count == 10 &&
                   resolved.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "spy-10").SourceEpisodeId == "source-10" &&
                   !resolved.Mappings.Any(mapping => mapping.SourceEpisodeId == "source-7" ||
                                                    mapping.SourceEpisodeId == "source-8" ||
                                                    mapping.SourceEpisodeId == "source-9"),
                "Spy Family S3 sparse local inventory must preserve E1-E6/E10-E13 coordinates");

            var sourceGap = Segment("local-29", "DandanID", "gap-source", "gap-1", new[]
            {
                Source("gap-1", "gap-comment-1", 1), Source("gap-3", "gap-comment-3", 3),
            });
            sourceGap.AlignmentIntent = CompositeSeasonAlignmentIntent.ExplicitAnchor;
            Assert(CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(29, 31), sourceGap,
                out resolved, out error), error);
            Assert(resolved.Mappings.Select(mapping => mapping.LocalEpisodeItemId + ":" + mapping.SourceEpisodeId)
                       .SequenceEqual(new[] { "local-29:gap-1", "local-31:gap-3" }),
                "a missing source coordinate must leave only the corresponding local row unmatched");

            var startsAtTen = Segment("local-10", "DandanID", "start-ten", "ten-1",
                Enumerable.Range(1, 12).Select(number => Source("ten-" + number, "ten-comment-" + number, number)));
            startsAtTen.AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset;
            Assert(CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(10, 12), startsAtTen,
                out resolved, out error), error);
            Assert(resolved.Mappings.Select(mapping => mapping.SourceEpisodeNumber)
                       .SequenceEqual(new int?[] { 10, 11, 12 }),
                "an inventory beginning at local E10 must still use zero-offset source E10");

            var scopedLocals = LocalEpisodes(1, 2);
            scopedLocals.Add(new CompositeSeasonLocalEpisode
            {
                ItemId = "excluded-foreign", EpisodeNumber = null, SortOrder = 3,
            });
            var excludedMapping = Mapping("excluded-foreign", "DandanID", "foreign",
                "foreign-source", "foreign-comment");
            Assert(CompositeSeasonPlanner.TryCreatePlan(scopedLocals, new[] { excludedMapping },
                out var scopedPlan, out error), error);
            var scopedRequest = Segment("local-1", "DandanID", "eligible", "eligible-1", new[]
            {
                Source("eligible-1", "eligible-comment-1", 1),
                Source("eligible-2", "eligible-comment-2", 2),
            });
            scopedRequest.AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset;
            Assert(CompositeSeasonPlanner.TryApplySegmentResolved(scopedPlan, scopedRequest,
                    out _, out resolved, out error) && resolved.Mode == CompositeSeasonAlignmentMode.NumberAware,
                "already excluded or mapped out-of-range rows must not change eligible segment reliability: " + error);
        }

        private static void PreservesExplicitAnchorsAndRequestedLocalRows()
        {
            var frieren = new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "frieren-29", EpisodeNumber = 29, SortOrder = 29 },
                new CompositeSeasonLocalEpisode { ItemId = "frieren-31", EpisodeNumber = 31, SortOrder = 31 },
            };
            var request = Segment("frieren-29", "DandanID", "frieren-s2", "frieren-source-1",
                Enumerable.Range(1, 3).Select(number => Source("frieren-source-" + number,
                    "frieren-comment-" + number, number)));
            request.AlignmentIntent = CompositeSeasonAlignmentIntent.ExplicitAnchor;
            Assert(CompositeSeasonPlanner.TryResolveSegment(frieren, request, out var resolved, out var error), error);
            Assert(resolved.Mappings.Select(mapping => mapping.LocalEpisodeItemId + ":" + mapping.SourceEpisodeNumber)
                       .SequenceEqual(new[] { "frieren-29:1", "frieren-31:3" }),
                "Frieren local E29->source E1 must retain its affine delta across missing local E30");

            var shifted = Segment("local-1", "DandanID", "shifted", "shifted-5",
                Enumerable.Range(1, 8).Select(number => Source("shifted-" + number,
                    "shifted-comment-" + number, number)), 2);
            shifted.AlignmentIntent = CompositeSeasonAlignmentIntent.ExplicitAnchor;
            Assert(CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(1, 4), shifted,
                out resolved, out error), error);
            Assert(resolved.ConsideredLocalEpisodes.Count == 2 && resolved.Mappings.Count == 2 &&
                   resolved.Mappings[0].SourceEpisodeNumber == 5 && resolved.Mappings[1].SourceEpisodeNumber == 6,
                "an explicit first-segment E1->source E5 anchor must override zero offset and count local rows");

            var gappedSources = new[]
            {
                Source("continued-1", "continued-comment-1", 1),
                Source("continued-3", "continued-comment-3", 3),
            };
            var continuedSource = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "continued",
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 31), null,
                out var continuedPlan, out error), error);
            Assert(CompositeSeasonPlanner.TryApplyRemainingSourceEpisodes(
                continuedPlan, continuedSource, gappedSources, "manual",
                out continuedPlan, out error), error);
            Assert(continuedPlan.Mappings.Select(mapping => mapping.LocalEpisodeItemId + ":" + mapping.SourceEpisodeId)
                       .SequenceEqual(new[] { "local-29:continued-1", "local-31:continued-3" }) &&
                   RunIds(continuedPlan.UnmatchedRuns.Single()) == "local-30",
                "a numeric gap must be considered once and must not be re-anchored from applied-count progress");

            var existing = Mapping("local-29", "DandanID", "continued", "continued-1", "continued-comment-1");
            existing.SourceEpisodeNumber = 1;
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 31), new[] { existing },
                out var reconstructed, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                reconstructed, continuedSource, gappedSources, "reconstructed",
                out reconstructed, out _, out error), error);
            Assert(reconstructed.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-31").SourceEpisodeId ==
                   "continued-3" && RunIds(reconstructed.UnmatchedRuns.Single()) == "local-30",
                "an existing local E29->source E1 mapping must establish the same affine anchor for E31->source E3");

            var conflicting = new[]
            {
                Mapping("local-29", "DandanID", "continued", "continued-1", "continued-comment-1"),
                Mapping("local-30", "DandanID", "continued", "continued-3", "continued-comment-3"),
            };
            conflicting[0].SourceEpisodeNumber = 1;
            conflicting[1].SourceEpisodeNumber = 3;
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 31), conflicting,
                out var conflictingPlan, out error), error);
            Assert(!CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                    conflictingPlan, continuedSource, gappedSources, "reconstructed",
                    out _, out _, out error) && error.Contains("conflict",
                    StringComparison.OrdinalIgnoreCase),
                "conflicting existing offsets inside one window must fail closed instead of re-anchoring");
        }

        private static void FallsBackPositionallyForEveryUnreliableNumberShape()
        {
            var invalidLocalNumbers = new int?[] { null, 0, -1 };
            foreach (var invalid in invalidLocalNumbers)
            {
                var locals = LocalEpisodes(1, 2);
                locals[1].EpisodeNumber = invalid;
                AssertFallback(locals, new[]
                {
                    Source("source-1", "comment-1", 1), Source("source-2", "comment-2", 2),
                }, "unreliable local " + (invalid?.ToString() ?? "null"));
            }
            var duplicateLocals = LocalEpisodes(1, 2);
            duplicateLocals[1].EpisodeNumber = 1;
            AssertFallback(duplicateLocals, new[]
            {
                Source("source-1", "comment-1", 1), Source("source-2", "comment-2", 2),
            }, "duplicate local");

            foreach (var invalid in new int?[] { null, 0, -1 })
            {
                AssertFallback(LocalEpisodes(1, 2), new[]
                {
                    SourceNullable("source-1", "comment-1", 1, 1),
                    SourceNullable("source-2", "comment-2", invalid, 2),
                }, "unreliable source " + (invalid?.ToString() ?? "null"));
            }
            AssertFallback(LocalEpisodes(1, 2), new[]
            {
                Source("source-1", "comment-1", 1), Source("source-2", "comment-2", 1),
            }, "duplicate source");

            void AssertFallback(IEnumerable<CompositeSeasonLocalEpisode> locals,
                IEnumerable<CompositeSeasonSourceEpisode> sources, string fixture)
            {
                var sourceList = sources.ToList();
                var fallback = Segment(locals.First().ItemId, "DandanID", fixture,
                    sourceList[0].EpisodeId, sourceList);
                fallback.AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset;
                Assert(CompositeSeasonPlanner.TryResolveSegment(locals, fallback,
                    out var resolution, out var error), fixture + ": " + error);
                Assert(resolution.Mode == CompositeSeasonAlignmentMode.PositionalFallback &&
                       resolution.Diagnostic == "positional-fallback: unreliable local or source numbering" &&
                       resolution.Mappings.Count == 2,
                    fixture + " must choose one stable positional mode for the whole segment");
            }
        }

        private static void ResolvesOnlyAuthoritativeSourceAnchorsAndFailsClosedOnOverflow()
        {
            var sources = Enumerable.Range(1, 3)
                .Select(number => Source("anchor-" + number, "anchor-comment-" + number, number)).ToList();
            var numberOnly = Segment("local-1", "DandanID", "number-only", string.Empty, sources);
            numberOnly.SourceStartEpisodeNumber = 2;
            numberOnly.AlignmentIntent = CompositeSeasonAlignmentIntent.ExplicitAnchor;
            Assert(CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(1, 2), numberOnly,
                out var resolved, out var error) && resolved.Mappings[0].SourceEpisodeId == "anchor-2", error);

            numberOnly.SourceStartEpisodeId = "anchor-3";
            numberOnly.SourceStartEpisodeNumber = 1;
            Assert(CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(1, 1), numberOnly,
                out resolved, out error) && resolved.Mappings[0].SourceEpisodeId == "anchor-3",
                "an exact SourceStartEpisodeId must override a conflicting number-only hint");

            var unreliable = new[]
            {
                SourceNullable("anchor-1", "comment-1", 1, 1),
                SourceNullable("anchor-null", "comment-null", null, 2),
            };
            var rejected = Segment("local-1", "DandanID", "unreliable-number-only", string.Empty, unreliable);
            rejected.SourceStartEpisodeNumber = 1;
            Assert(!CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(1, 1), rejected,
                    out _, out error) && error.Contains("number-only", StringComparison.OrdinalIgnoreCase),
                "a unique requested number inside an otherwise unreliable source scope must fail closed");

            var overflowLocals = new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "overflow-1", EpisodeNumber = 1 },
                new CompositeSeasonLocalEpisode { ItemId = "overflow-max", EpisodeNumber = int.MaxValue },
            };
            var overflow = Segment("overflow-1", "DandanID", "overflow", "overflow-source-2", new[]
            {
                Source("overflow-source-1", "overflow-comment-1", 1),
                Source("overflow-source-2", "overflow-comment-2", 2),
            });
            overflow.AlignmentIntent = CompositeSeasonAlignmentIntent.ExplicitAnchor;
            Assert(!CompositeSeasonPlanner.TryResolveSegment(overflowLocals, overflow,
                    out _, out error) && error.Contains("overflow", StringComparison.OrdinalIgnoreCase),
                "checked explicit-anchor arithmetic must fail before producing partial mappings");
        }

        private static void PreservesSourceNumberProvenanceAndOrdinalOrder()
        {
            var media = new ScraperMedia
            {
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "ordinal-a", CommentId = "comment-a", EpisodeNumber = null },
                    new ScraperEpisode { Id = "ordinal-b", CommentId = "comment-b", EpisodeNumber = 0 },
                    new ScraperEpisode { Id = "ordinal-c", CommentId = "comment-c", EpisodeNumber = -1 },
                },
            };
            var projected = CompositeSeasonMatchService.GetSourceEpisodes(media);
            Assert(projected.Select(source => source.EpisodeNumber).SequenceEqual(new int?[] { null, 0, -1 }) &&
                   projected.Select(source => source.SourceOrdinal).SequenceEqual(new[] { 1, 2, 3 }),
                "source projection must preserve nullable provider numbers separately from stable ordinals");

            var reordered = new[]
            {
                SourceNullable("ordinal-c", "comment-c", null, 30),
                SourceNullable("ordinal-a", "comment-a", null, 10),
                SourceNullable("ordinal-b", "comment-b", null, 20),
            };
            var request = Segment("local-1", "DandanID", "ordinal", "ordinal-a", reordered);
            Assert(CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(1, 3), request,
                out var resolved, out var error), error);
            Assert(resolved.Mappings.Select(mapping => mapping.SourceEpisodeId)
                       .SequenceEqual(new[] { "ordinal-a", "ordinal-b", "ordinal-c" }),
                "positional fallback must use stable SourceOrdinal rather than incidental list order");

            foreach (var invalidSources in new[]
            {
                new[] { Source("", "comment", 1) },
                new[] { Source("duplicate", "comment-1", 1), Source("duplicate", "comment-2", 2) },
                new[] { Source("blank-comment", "", 1) },
            })
            {
                var invalid = Segment("local-1", "DandanID", "invalid", invalidSources[0].EpisodeId, invalidSources);
                Assert(!CompositeSeasonPlanner.TryResolveSegment(LocalEpisodes(1, 1), invalid,
                    out _, out _), "blank/duplicate source identity and blank CommentId must remain structural failures");
            }
        }

        private static void AdvancesContinuationBySegmentWindowFrontiers()
        {
            var continuing = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "window-source",
            };
            var foreign = Mapping("local-32", "DandanID", "foreign-boundary",
                "foreign-1", "foreign-comment-1");
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 35), new[] { foreign },
                out var plan, out var error), error);
            var gapped = new[]
            {
                SourceNullable("window-1", "window-comment-1", 1, 1),
                SourceNullable("window-3", "window-comment-3", 3, 2),
                SourceNullable("window-4", "window-comment-4", 4, 3),
                SourceNullable("window-5", "window-comment-5", 5, 4),
            };
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                plan, continuing, gapped, "window", out plan, out var exhausted, out error), error);
            Assert(exhausted &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").SourceEpisodeId == "window-1" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-31").SourceEpisodeId == "window-3" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-33").SourceEpisodeId == "window-4" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-34").SourceEpisodeId == "window-5" &&
                   plan.UnmatchedRuns.SelectMany(run => run.Episodes).Select(local => local.ItemId)
                       .OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(new[] { "local-30", "local-35" }),
                "numeric gaps must advance the frontier; a foreign boundary consumes no source and later windows must not back-pick");

            var differentOffsets = new[]
            {
                Mapping("local-29", "DandanID", "window-source", "offset-1", "offset-comment-1"),
                Mapping("local-31", "DandanID", "foreign-boundary", "foreign-2", "foreign-comment-2"),
                Mapping("local-32", "DandanID", "window-source", "offset-3", "offset-comment-3"),
            };
            differentOffsets[0].SourceEpisodeNumber = 1;
            differentOffsets[2].SourceEpisodeNumber = 3;
            var offsetSources = Enumerable.Range(1, 4)
                .Select(number => SourceNullable("offset-" + number, "offset-comment-" + number,
                    number, number)).ToList();
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 33), differentOffsets,
                out plan, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                plan, continuing, offsetSources, "window", out plan, out exhausted, out error), error);
            Assert(exhausted &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-30").SourceEpisodeId == "offset-2" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-33").SourceEpisodeId == "offset-4",
                "same-source exact mappings may establish different affine offsets after a foreign-source boundary");

            var positionalLocals = Enumerable.Range(1, 5).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "positional-local-" + number,
                EpisodeNumber = null,
                SortOrder = number,
            }).ToList();
            var positionalForeign = Mapping("positional-local-3", "DandanID", "foreign-boundary",
                "positional-foreign", "positional-foreign-comment");
            var positionalSources = Enumerable.Range(1, 4).Select(number =>
                SourceNullable("positional-source-" + number, "positional-comment-" + number,
                    null, number)).ToList();
            Assert(CompositeSeasonPlanner.TryCreatePlan(positionalLocals, new[] { positionalForeign },
                out plan, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                plan, continuing, positionalSources, "window", out plan, out exhausted, out error), error);
            Assert(exhausted &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "positional-local-1").SourceEpisodeId == "positional-source-1" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "positional-local-2").SourceEpisodeId == "positional-source-2" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "positional-local-4").SourceEpisodeId == "positional-source-3" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "positional-local-5").SourceEpisodeId == "positional-source-4",
                "positional windows must resume at the next frontier without charging the foreign boundary");

            var sparseFrontierLocals = new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "frontier-29", EpisodeNumber = 29, SortOrder = 1 },
                new CompositeSeasonLocalEpisode { ItemId = "frontier-31", EpisodeNumber = 31, SortOrder = 2 },
                new CompositeSeasonLocalEpisode { ItemId = "frontier-boundary", EpisodeNumber = 32, SortOrder = 3 },
            };
            var frontierBoundary = Mapping("frontier-boundary", "DandanID", "foreign-boundary",
                "frontier-foreign", "frontier-foreign-comment");
            var frontierSources = Enumerable.Range(1, 3).Select(number =>
                SourceNullable("frontier-source-" + number, "frontier-comment-" + number,
                    number, number)).ToList();
            Assert(CompositeSeasonPlanner.TryCreatePlan(sparseFrontierLocals, new[] { frontierBoundary },
                out plan, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                plan, continuing, frontierSources, "window", out plan, out exhausted, out error), error);
            Assert(exhausted && plan.Mappings.Any(mapping => mapping.SourceEpisodeId == "frontier-source-1") &&
                   plan.Mappings.Any(mapping => mapping.SourceEpisodeId == "frontier-source-3") &&
                   !plan.Mappings.Any(mapping => mapping.SourceEpisodeId == "frontier-source-2"),
                "frontier exhaustion must remain true after advancing past an unused source coordinate");
        }

        private static void FingerprintsConsideredUnmappedGapRowsAndRejectsLegacyEntryPoints()
        {
            var local = LocalEpisodes(1, 3);
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "fingerprint-gap", MediaLookupId = "fingerprint-gap",
            };
            var sourceEpisodes = new List<CompositeSeasonSourceEpisode>
            {
                SourceNullable("fingerprint-source-1", "fingerprint-comment-1", 1, 1),
                SourceNullable("fingerprint-source-3", "fingerprint-comment-3", 3, 2),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            var request = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = "local-1",
                RequestedEpisodeCount = 3,
                Source = source,
                SourceEpisodes = sourceEpisodes,
                SourceStartEpisodeId = "fingerprint-source-1",
                AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset,
            };
            Assert(CompositeSeasonPlanner.TryApplySegmentResolved(
                plan, request, out plan, out var resolution, out error), error);
            Assert(resolution.ConsideredLocalEpisodes.Count == 3 && plan.Mappings.Count == 2 &&
                   !plan.Mappings.Any(mapping => mapping.LocalEpisodeItemId == "local-2"),
                "the fingerprint fixture must contain one considered local row left unmapped by a source coordinate gap");

            var context = new SeasonPlanningContext
            {
                SeriesId = "fingerprint-series",
                SeasonId = "fingerprint-season",
                TargetSeasonNumber = 1,
                StructureFingerprint = "fingerprint-structure",
            };
            var selection = new DanmuCompositeSeasonSelection
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                AlignmentIntent = DanmuCompositeAlignmentIntentWire.DefaultZeroOffset,
                ServerResolvedAlignmentMode = resolution.Mode,
                Site = source.ProviderId,
                CandidateId = source.MediaLookupId,
                LocalStartEpisodeItemId = "local-1",
                RequestedEpisodeCount = 3,
                SourceStartEpisodeId = "fingerprint-source-1",
                MatchOrigin = "manual",
                SelectionEvidenceToken = "fingerprint-evidence",
                ServerSourceEpisodes = sourceEpisodes,
                ServerConsideredLocalEpisodeItemIds = new List<string> { "local-1", "local-3" },
            };
            var withoutGap = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, plan);
            selection.ServerConsideredLocalEpisodeItemIds = new List<string>
            {
                "local-1", "local-2", "local-3",
            };
            var withGap = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, plan);
            Assert(!string.Equals(withoutGap, withGap, StringComparison.Ordinal) &&
                   plan.Mappings.Count == 2,
                "adding a considered but source-gap-unmapped local ItemId must stale the digest even when final exact mappings are unchanged");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var controllerBuild = SliceSource(controller,
                "private async Task<CompositePlanBuild> BuildCompositePlanAsync(",
                "private static bool ShouldReportVerifiedSourceEpisodeSurplus(");
            var library = File.ReadAllText(Path.Combine(
                repositoryRoot, "LibraryManagerEventsHelper.cs")).Replace("\r\n", "\n");
            var automaticBuild = SliceSource(library,
                "private async Task<bool> DownloadAutomaticSeasonWithCompositePlan(",
                "private static DanmuCompositeSeasonSelection CreateAutomaticSelection(");
            var automaticRebuild = SliceSource(library,
                "private async Task<AutomaticSeasonPlanSnapshot> RebuildAutomaticPlanAsync(",
                "internal static bool CanUseAutomaticSearch(");
            foreach (var productionPath in new[] { controllerBuild, automaticBuild, automaticRebuild })
            {
                Assert(!productionPath.Contains("TryContinueSourceAcrossSegmentWindows(", StringComparison.Ordinal) &&
                       !productionPath.Contains("TryNormalizeAndContinueSource(", StringComparison.Ordinal) &&
                       !productionPath.Contains("TryApplyRemainingSourceEpisodes(", StringComparison.Ordinal),
                    "submitted controller and automatic build/rebuild paths must use the authoritative submitted-segment resolver, never legacy continuation helpers");
            }
        }

        private static void ContinuesOwningWindowsWithForwardOnlyIndependentModes()
        {
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "owning-windows", MediaLookupId = "owning-windows",
            };
            var positionalRows = new List<CompositeSeasonLocalEpisode>
            {
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "pos-owning-a", EpisodeNumber = null, SortOrder = 1,
                    ParentSeasonNumber = 1, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "pos-boundary", EpisodeNumber = 50, SortOrder = 2,
                    ParentSeasonNumber = 0, Ownership = CompositeSeasonOwnershipKind.Supplemental,
                },
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "pos-owning-b", EpisodeNumber = null, SortOrder = 3,
                    ParentSeasonNumber = 1, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
            };
            var positionalSources = new[]
            {
                SourceNullable("pos-source-1", "pos-comment-1", null, 1),
                SourceNullable("pos-source-2", "pos-comment-2", null, 2),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(
                positionalRows, null, out var positionalPlan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    positionalPlan, source, positionalSources, "automatic-primary",
                    out positionalPlan, out error), error);
            Assert(positionalPlan.Mappings.Count == 2 &&
                   positionalPlan.Mappings.Single(mapping =>
                       mapping.LocalEpisodeItemId == "pos-owning-a").SourceEpisodeId == "pos-source-1" &&
                   positionalPlan.Mappings.Single(mapping =>
                       mapping.LocalEpisodeItemId == "pos-owning-b").SourceEpisodeId == "pos-source-2" &&
                   !positionalPlan.Mappings.Any(mapping => mapping.LocalEpisodeItemId == "pos-boundary"),
                "multiple positional owning windows must carry a forward-only ordinal and treat non-owning rows as zero-consumption boundaries");

            var switchingRows = new List<CompositeSeasonLocalEpisode>
            {
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "switch-positional", EpisodeNumber = null, SortOrder = 1,
                    ParentSeasonNumber = 1, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "switch-boundary", EpisodeNumber = 99, SortOrder = 2,
                    ParentSeasonNumber = 0, Ownership = CompositeSeasonOwnershipKind.Supplemental,
                },
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "switch-numeric-10", EpisodeNumber = 10, SortOrder = 3,
                    ParentSeasonNumber = 1, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "switch-numeric-11", EpisodeNumber = 11, SortOrder = 4,
                    ParentSeasonNumber = 1, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
            };
            var switchingSources = new[]
            {
                SourceNullable("switch-source-1", "switch-comment-1", 1, 1),
                SourceNullable("switch-source-10", "switch-comment-10", 10, 2),
                SourceNullable("switch-source-11", "switch-comment-11", 11, 3),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(
                switchingRows, null, out var switchingPlan, out error), error);
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    switchingPlan, source, switchingSources, "automatic-primary",
                    out switchingPlan, out error), error);
            Assert(switchingPlan.Mappings.Count == 3 &&
                   switchingPlan.Mappings.Single(mapping =>
                       mapping.LocalEpisodeItemId == "switch-positional").SourceEpisodeId == "switch-source-1" &&
                   switchingPlan.Mappings.Single(mapping =>
                       mapping.LocalEpisodeItemId == "switch-numeric-10").SourceEpisodeId == "switch-source-10" &&
                   switchingPlan.Mappings.Single(mapping =>
                       mapping.LocalEpisodeItemId == "switch-numeric-11").SourceEpisodeId == "switch-source-11",
                "a later owning window must independently switch from positional fallback to number-aware alignment without reusing the first source Episode");
        }

        private static void RoundTripsAuthoritativeCompactSelectionsWithoutLeakingServerEvidence()
        {
            var local = new List<CompositeSeasonLocalEpisode>
            {
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "roundtrip-local-10", EpisodeNumber = 10, SortOrder = 1,
                    ParentSeasonNumber = 3, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "roundtrip-local-12", EpisodeNumber = 12, SortOrder = 2,
                    ParentSeasonNumber = 3, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
            };
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "roundtrip-media", MediaLookupId = "roundtrip-candidate",
            };
            var sourceEpisodes = new List<CompositeSeasonSourceEpisode>
            {
                SourceNullable("roundtrip-source-10", "private-comment-10", 10, 1),
                SourceNullable("roundtrip-source-12", "private-comment-12", 12, 2),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            var request = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = "roundtrip-local-10",
                RequestedEpisodeCount = 0,
                Source = source,
                SourceEpisodes = sourceEpisodes,
                SourceStartEpisodeId = "roundtrip-source-10",
                SourceStartEpisodeNumber = 10,
                AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset,
                Origin = "manual",
                SelectionEvidenceToken = "roundtrip-evidence",
            };
            Assert(CompositeSeasonPlanner.TryApplySegmentResolved(
                plan, request, out plan, out var resolution, out error), error);
            var authoritative = new DanmuCompositeSeasonSelection
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                AlignmentIntent = DanmuCompositeAlignmentIntentWire.DefaultZeroOffset,
                PlanGeneration = 73,
                LocalStartEpisodeItemId = "roundtrip-local-10",
                RequestedEpisodeCount = 0,
                Site = source.ProviderId,
                CandidateId = source.MediaLookupId,
                SourceStartEpisodeId = "roundtrip-source-10",
                SourceStartEpisodeNumber = 10,
                MatchOrigin = "manual",
                SelectionEvidenceToken = "roundtrip-evidence",
                ServerResolvedAlignmentMode = resolution.Mode,
                ServerSourceEpisodes = sourceEpisodes,
                ServerConsideredLocalEpisodeItemIds = resolution.ConsideredLocalEpisodes
                    .Select(episode => episode.ItemId).ToList(),
            };
            var context = new SeasonPlanningContext
            {
                SeriesId = "roundtrip-series",
                SeasonId = "roundtrip-season",
                TargetSeasonNumber = 3,
                StructureFingerprint = "roundtrip-structure",
            };
            var expectedFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { authoritative }, plan);

            var projection = typeof(DanmuController).GetMethod(
                "ToResponseCompositeSelections", BindingFlags.NonPublic | BindingFlags.Static);
            Assert(projection != null, "authoritative previews must expose one compact selection projection");
            var compact = (List<DanmuCompositeSeasonSelection>)projection.Invoke(
                null, new object[] { new[] { authoritative } });
            var json = JsonSerializer.Serialize(compact);
            Assert(json.Contains("\"RequestedEpisodeCount\":0", StringComparison.Ordinal) &&
                   json.Contains("\"LocalStartEpisodeItemId\":\"roundtrip-local-10\"", StringComparison.Ordinal) &&
                   !json.Contains("ServerSource", StringComparison.Ordinal) &&
                   !json.Contains("ServerResolvedAlignmentMode", StringComparison.Ordinal) &&
                   !json.Contains("ServerConsideredLocalEpisodeItemIds", StringComparison.Ordinal) &&
                   !json.Contains("CommentId", StringComparison.Ordinal) &&
                   !json.Contains("CompositeSeasonEpisodeMapping", StringComparison.Ordinal),
                "the response compact selection must preserve count/start intent without exposing exact mappings, CommentIds, resolved mode, or server provenance");
            Assert(DanmuCompositeSeasonSelectionJson.TryParse(json, out var parsed, out error), error);
            Assert(parsed.Count == 1 && parsed[0].RequestedEpisodeCount == 0 &&
                   parsed[0].LocalStartEpisodeItemId == "roundtrip-local-10" &&
                   parsed[0].SourceStartEpisodeId == "roundtrip-source-10",
                "the compact response must roundtrip through the strict browser submission parser without inferring fields from presentation groups");

            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var rebuiltPlan, out error), error);
            var rebuiltRequest = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = parsed[0].LocalStartEpisodeItemId,
                RequestedEpisodeCount = parsed[0].RequestedEpisodeCount,
                Source = source,
                SourceEpisodes = sourceEpisodes,
                SourceStartEpisodeId = parsed[0].SourceStartEpisodeId,
                SourceStartEpisodeNumber = parsed[0].SourceStartEpisodeNumber,
                AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset,
                Origin = parsed[0].MatchOrigin,
                SelectionEvidenceToken = parsed[0].SelectionEvidenceToken,
            };
            Assert(CompositeSeasonPlanner.TryApplySegmentResolved(
                rebuiltPlan, rebuiltRequest, out rebuiltPlan, out var rebuiltResolution, out error), error);
            parsed[0].ServerResolvedAlignmentMode = rebuiltResolution.Mode;
            parsed[0].ServerSourceEpisodes = sourceEpisodes;
            parsed[0].ServerConsideredLocalEpisodeItemIds = rebuiltResolution.ConsideredLocalEpisodes
                .Select(episode => episode.ItemId).ToList();
            var rebuiltFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, parsed, rebuiltPlan);
            Assert(string.Equals(expectedFingerprint, rebuiltFingerprint, StringComparison.Ordinal),
                "compact serialization, strict parse, and authoritative sparse-gap rebuild must reproduce the original fingerprint exactly");

            parsed[0].CandidateId = "changed-candidate";
            var changedFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, parsed, rebuiltPlan);
            Assert(!string.Equals(expectedFingerprint, changedFingerprint, StringComparison.Ordinal),
                "changing a compact candidate identity must stale authoritative fingerprint validation");

            DanmuCompositeSeasonSelection FreshFingerprintSelection()
            {
                return new DanmuCompositeSeasonSelection
                {
                    MappingProtocolVersion = authoritative.MappingProtocolVersion,
                    AlignmentIntent = authoritative.AlignmentIntent,
                    PlanGeneration = authoritative.PlanGeneration,
                    LocalStartEpisodeItemId = authoritative.LocalStartEpisodeItemId,
                    RequestedEpisodeCount = authoritative.RequestedEpisodeCount,
                    Site = authoritative.Site,
                    CandidateId = authoritative.CandidateId,
                    SourceStartEpisodeId = authoritative.SourceStartEpisodeId,
                    SourceStartEpisodeNumber = authoritative.SourceStartEpisodeNumber,
                    MatchOrigin = authoritative.MatchOrigin,
                    SelectionEvidenceToken = authoritative.SelectionEvidenceToken,
                    ServerResolvedAlignmentMode = authoritative.ServerResolvedAlignmentMode,
                    ServerSourceEpisodes = sourceEpisodes,
                    ServerConsideredLocalEpisodeItemIds = authoritative.ServerConsideredLocalEpisodeItemIds.ToList(),
                };
            }

            var fingerprintedMutations = new Action<DanmuCompositeSeasonSelection>[]
            {
                selection => selection.MappingProtocolVersion--,
                selection => selection.AlignmentIntent = DanmuCompositeAlignmentIntentWire.ExplicitAnchor,
                selection => selection.LocalStartEpisodeItemId = "roundtrip-local-12",
                selection => selection.RequestedEpisodeCount = 1,
                selection => selection.Site = "ChangedSite",
                selection => selection.CandidateId = "changed-candidate",
                selection => selection.SourceStartEpisodeId = "roundtrip-source-12",
                selection => selection.SourceStartEpisodeNumber = 12,
                selection => selection.MatchOrigin = "scored",
                selection => selection.SelectionEvidenceToken = "changed-evidence",
            };
            foreach (var mutate in fingerprintedMutations)
            {
                var changed = FreshFingerprintSelection();
                mutate(changed);
                Assert(!string.Equals(expectedFingerprint,
                        SeasonPlanningContextBuilder.CreatePlanFingerprint(
                            context, new[] { changed }, plan), StringComparison.Ordinal),
                    "every compact planning field other than separately fenced generation must participate in fingerprint staleness");
            }
            var generationCoordinator = new SeasonPlanGenerationCoordinator();
            var compactGeneration = generationCoordinator.Begin(context.SeasonId);
            Assert(generationCoordinator.IsCurrent(context.SeasonId, compactGeneration),
                "the compact selection generation must initially be current");
            generationCoordinator.Begin(context.SeasonId);
            Assert(!generationCoordinator.IsCurrent(context.SeasonId, compactGeneration),
                "PlanGeneration changes are fenced by the generation authority independently of the fingerprint digest");

            var unnumberedLocal = new List<CompositeSeasonLocalEpisode>
            {
                new CompositeSeasonLocalEpisode
                {
                    ItemId = "unnumbered-local-10", EpisodeNumber = 10, SortOrder = 1,
                    ParentSeasonNumber = 3, Ownership = CompositeSeasonOwnershipKind.Owning,
                },
            };
            var unnumberedSourceEpisodes = new List<CompositeSeasonSourceEpisode>
            {
                SourceNullable("unnumbered-exact-id", "private-unnumbered-comment", null, 1),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(
                unnumberedLocal, null, out var unnumberedPlan, out error), error);
            var unnumberedRequest = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = "unnumbered-local-10",
                RequestedEpisodeCount = 0,
                Source = source,
                SourceEpisodes = unnumberedSourceEpisodes,
                SourceStartEpisodeId = "unnumbered-exact-id",
                SourceStartEpisodeNumber = 0,
                AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset,
                Origin = "manual",
                SelectionEvidenceToken = "unnumbered-evidence",
            };
            Assert(CompositeSeasonPlanner.TryApplySegmentResolved(
                    unnumberedPlan, unnumberedRequest, out unnumberedPlan,
                    out var unnumberedResolution, out error), error);
            Assert(unnumberedResolution.Mode == CompositeSeasonAlignmentMode.PositionalFallback &&
                   unnumberedPlan.Mappings.Single().SourceEpisodeId == "unnumbered-exact-id",
                "an unnumbered provider source must resolve by its exact EpisodeId and must not reinterpret wire number zero as an ordinal");
            var unnumberedSelection = new DanmuCompositeSeasonSelection
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                AlignmentIntent = DanmuCompositeAlignmentIntentWire.DefaultZeroOffset,
                PlanGeneration = 74,
                LocalStartEpisodeItemId = "unnumbered-local-10",
                RequestedEpisodeCount = 0,
                Site = source.ProviderId,
                CandidateId = source.MediaLookupId,
                SourceStartEpisodeId = "unnumbered-exact-id",
                SourceStartEpisodeNumber = 0,
                MatchOrigin = "manual",
                SelectionEvidenceToken = "unnumbered-evidence",
                ServerResolvedAlignmentMode = unnumberedResolution.Mode,
                ServerSourceEpisodes = unnumberedSourceEpisodes,
                ServerConsideredLocalEpisodeItemIds = unnumberedResolution.ConsideredLocalEpisodes
                    .Select(episode => episode.ItemId).ToList(),
            };
            var unnumberedFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { unnumberedSelection }, unnumberedPlan);
            var compactUnnumbered = (List<DanmuCompositeSeasonSelection>)projection.Invoke(
                null, new object[] { new[] { unnumberedSelection } });
            var unnumberedJson = JsonSerializer.Serialize(compactUnnumbered);
            Assert(unnumberedJson.Contains("\"SourceStartEpisodeNumber\":0", StringComparison.Ordinal),
                "the server-created V28 selection must freeze an observed missing provider number as wire zero");
            Assert(DanmuCompositeSeasonSelectionJson.TryParse(
                unnumberedJson, out var parsedUnnumbered, out error), error);
            Assert(parsedUnnumbered.Single().SourceStartEpisodeNumber == 0 &&
                   parsedUnnumbered.Single().SourceStartEpisodeId == "unnumbered-exact-id",
                "the browser-equivalent compact roundtrip must retain wire zero beside the authoritative exact EpisodeId");
            Assert(CompositeSeasonPlanner.TryCreatePlan(
                unnumberedLocal, null, out var rebuiltUnnumberedPlan, out error), error);
            var parsedUnnumberedRequest = new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = parsedUnnumbered[0].LocalStartEpisodeItemId,
                RequestedEpisodeCount = parsedUnnumbered[0].RequestedEpisodeCount,
                Source = source,
                SourceEpisodes = unnumberedSourceEpisodes,
                SourceStartEpisodeId = parsedUnnumbered[0].SourceStartEpisodeId,
                SourceStartEpisodeNumber = parsedUnnumbered[0].SourceStartEpisodeNumber,
                AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset,
                Origin = parsedUnnumbered[0].MatchOrigin,
                SelectionEvidenceToken = parsedUnnumbered[0].SelectionEvidenceToken,
            };
            Assert(CompositeSeasonPlanner.TryApplySegmentResolved(
                    rebuiltUnnumberedPlan, parsedUnnumberedRequest, out rebuiltUnnumberedPlan,
                    out var rebuiltUnnumberedResolution, out error), error);
            parsedUnnumbered[0].ServerResolvedAlignmentMode = rebuiltUnnumberedResolution.Mode;
            parsedUnnumbered[0].ServerSourceEpisodes = unnumberedSourceEpisodes;
            parsedUnnumbered[0].ServerConsideredLocalEpisodeItemIds = rebuiltUnnumberedResolution
                .ConsideredLocalEpisodes.Select(episode => episode.ItemId).ToList();
            Assert(rebuiltUnnumberedPlan.Mappings.Single().SourceEpisodeId == "unnumbered-exact-id" &&
                   string.Equals(unnumberedFingerprint,
                       SeasonPlanningContextBuilder.CreatePlanFingerprint(
                           context, parsedUnnumbered, rebuiltUnnumberedPlan), StringComparison.Ordinal),
                "wire zero plus exact EpisodeId must rebuild the same source mapping and fingerprint");
            parsedUnnumbered[0].SourceStartEpisodeNumber = 1;
            Assert(!string.Equals(unnumberedFingerprint,
                    SeasonPlanningContextBuilder.CreatePlanFingerprint(
                        context, parsedUnnumbered, rebuiltUnnumberedPlan), StringComparison.Ordinal),
                "mutating the frozen unnumbered wire value must stale the plan fingerprint");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            Assert(Count(controller, "CompositeSelections = ToResponseCompositeSelections(") == 3,
                "direct, initial candidate, and composite revalidation preview paths must all return the exact ordered compact selections used by their fingerprint");
            Assert(controller.Contains(
                    "SourceStartEpisodeNumber = sourceEpisodes[0].EpisodeNumber ?? 0", StringComparison.Ordinal),
                "the controller-created initial authoritative selection must freeze nullable provider numbering before BuildCompositePlanAsync and fingerprinting");
        }

        private static void PreservesExplicitEvidenceAndBuildsRemainingRuns()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "frieren-s1", "source-1", "comment-1"),
                Mapping("local-2", "DandanID", "frieren-s1", "source-2", "comment-2"),
                Mapping("local-4", "DandanID", "frieren-s1", "source-4", "comment-4"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, out var plan, out var error), error);
            Assert(plan.Mappings.Count == 3 && plan.Mappings.All(mapping => mapping.Origin == "direct"),
                "explicit/direct mappings must remain the planning baseline");
            Assert(plan.UnmatchedRuns.Count == 2 && RunIds(plan.UnmatchedRuns[0]) == "local-3" &&
                   RunIds(plan.UnmatchedRuns[1]) == "local-5",
                "mapped episodes must split gaps into maximum contiguous runs");

            var request = Segment("local-3", "DandanID", "frieren-ova", "ova-1", new[] { Source("ova-1", "ova-comment", 1) });
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, request, out plan, out var applied, out error), error);
            Assert(applied == 1 && plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-3").Source.MediaId == "frieren-ova",
                "a manual mapping must only fill its selected unmatched run");
            Assert(plan.UnmatchedRuns.Count == 1 && RunIds(plan.UnmatchedRuns[0]) == "local-5",
                "remaining gaps must be recomputed after every mapping");
        }

        private static void SupportsSourceStartsAndPartialCoverage()
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(29, 38), null, out var plan, out var error), error);
            var request = Segment("local-29", "DandanID", "frieren-s2", "source-4", new[]
            {
                Source("source-1", "comment-1", 1), Source("source-2", "comment-2", 2),
                Source("source-3", "comment-3", 3), Source("source-4", "comment-4", 4),
                Source("source-5", "comment-5", 5), Source("source-6", "comment-6", 6),
            }, 10);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, request, out plan, out var applied, out error), error);
            Assert(applied == 3 && plan.Mappings[0].LocalEpisodeItemId == "local-29" &&
                   plan.Mappings[0].SourceEpisodeId == "source-4" && RunIds(plan.UnmatchedRuns.Single()) ==
                   "local-32,local-33,local-34,local-35,local-36,local-37,local-38",
                "a short source must map only its verified prefix from the selected source start");

            var fill = Segment("local-32", "DandanID", "frieren-s2b", "part-1", Enumerable.Range(1, 8)
                .Select(number => Source("part-" + number, "part-comment-" + number, number)));
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, fill, out plan, out applied, out error), error);
            Assert(applied == 7 && plan.UnmatchedRuns.Count == 0,
                "a long source must not overflow the selected local run");
        }

        private static void MapsFrierenThirtyEightEpisodesAcrossTwoUpstreamSeasons()
        {
            var directS1 = Enumerable.Range(1, 28).Select(number => new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = "local-" + number,
                Source = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "frieren-s1" },
                SourceEpisodeId = "s1-" + number,
                CommentId = "s1-comment-" + number,
                SourceEpisodeNumber = number,
                Origin = "episode-provider-id",
            });
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 38), directS1, out var plan, out var error), error);
            Assert(plan.Mappings.Count == 28 && RunIds(plan.UnmatchedRuns.Single()) ==
                   string.Join(",", Enumerable.Range(29, 10).Select(number => "local-" + number)),
                "direct Frieren S1 E1-E28 evidence must leave E29-E38 as one temporary group");

            var secondSeason = Segment("local-29", "DandanID", "frieren-s2", "s2-1",
                Enumerable.Range(1, 10).Select(number => Source("s2-" + number, "s2-comment-" + number, number)));
            Assert(CompositeSeasonPlanner.TryApplySegment(plan, secondSeason, out plan, out var applied, out error), error);
            Assert(applied == 10 && plan.IsComposite && plan.UnmatchedRuns.Count == 0 && plan.Mappings.Count == 38,
                "Frieren 38 = upstream S1(28) + S2(10) must become a complete two-source plan without a temporary group");
            Assert(plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").Source.MediaId == "frieren-s2" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").SourceEpisodeNumber == 1,
                "the second local range must start at the independently verified S2 E1, not continue S1 numbering");
        }

        private static void KeepsMarkedFrierenEpisodeEvidenceWhenNoCandidateIsUsable()
        {
            // This is the restart/ambiguous-search safety case: every local
            // Episode already has an exact Dandan binding, but the Season is
            // marked composite and no automatic candidate may be selected.
            // The planner must therefore retain direct evidence instead of
            // emitting 38 temporary/unmatched episodes.
            var direct = new List<CompositeSeasonEpisodeMapping>();
            foreach (var number in Enumerable.Range(1, 28))
            {
                direct.Add(DirectDandanMapping(number, "17617", "17617" + number.ToString("0000")));
            }
            foreach (var number in Enumerable.Range(1, 10))
            {
                direct.Add(DirectDandanMapping(28 + number, "18886", "18886" + number.ToString("0000")));
            }

            Assert(DandanEpisodeId.TryGetAnimeId("176170001", out var firstParent) && firstParent == 17617 &&
                   DandanEpisodeId.TryGetAnimeId("188860010", out var secondParent) && secondParent == 18886 &&
                   !DandanEpisodeId.TryGetAnimeId("18886x001", out _) &&
                   !DandanEpisodeId.TryGetAnimeId("1888", out _),
                "Dandan Episode ProviderIds must derive only their candidate parent 17617/18886 from a strict numeric four-digit suffix");
            var verified = DandanEpisodeId.CreateVerifiedEpisode("188860010", new[]
            {
                new DandanEpisode { EpisodeId = 188860009, EpisodeTitle = "E09", EpisodeNumber = "9" },
                new DandanEpisode { EpisodeId = 188860010, EpisodeTitle = "E10", EpisodeNumber = "10" },
            });
            Assert(verified?.Id == "188860010" && verified.CommentId == "188860010" &&
                   verified.ParentMediaId == "18886" && verified.EpisodeNumber == 10 &&
                   DandanEpisodeId.CreateVerifiedEpisode("188860010", new[]
                   {
                       new DandanEpisode { EpisodeId = 176170010, EpisodeTitle = "wrong parent", EpisodeNumber = "10" },
                   }) == null,
                "a Dandan direct Episode mapping must be created only from the exact full EpisodeId returned by its parent detail");

            var firstResolver = new DirectEpisodeFakeScraper("DandanID", new ScraperEpisode
            {
                Id = "176170001", CommentId = "176170001", ParentMediaId = "17617", EpisodeNumber = 1,
            });
            var secondResolver = new DirectEpisodeFakeScraper("DandanID", new ScraperEpisode
            {
                Id = "188860001", CommentId = "188860001", ParentMediaId = "18886", EpisodeNumber = 1,
            });
            var firstMedia = DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                firstResolver, new Episode { IndexNumber = 1 }, "176170001", 1).GetAwaiter().GetResult();
            var secondMedia = DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                secondResolver, new Episode { IndexNumber = 29 }, "188860001", 1).GetAwaiter().GetResult();
            var firstMapping = CompositeSeasonMatchService.CreateDirectMapping("local-1", "DandanID", firstMedia, "176170001");
            var secondMapping = CompositeSeasonMatchService.CreateDirectMapping("local-29", "DandanID", secondMedia, "188860001");
            Assert(firstMapping.Source.MediaId == "17617" && firstMapping.Source.MediaLookupId == "176170001" &&
                   secondMapping.Source.MediaId == "18886" && secondMapping.Source.MediaLookupId == "188860001" &&
                   firstResolver.MediaCalls == 0 && secondResolver.MediaCalls == 0 &&
                   firstResolver.MediaEpisodeCalls == 1 && secondResolver.MediaEpisodeCalls == 1,
                "direct Episode resolution must preserve the exact EpisodeId lookup token while exposing the verified parent AnimeId as canonical source identity");
            Assert(CompositeSeasonPlanner.TryCreatePlan(new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "local-1", SortOrder = 1 },
                new CompositeSeasonLocalEpisode { ItemId = "local-29", SortOrder = 2 },
            }, new[] { firstMapping, secondMapping }, out var directPlan, out var directError) && directPlan.IsComposite,
                "two direct Dandan Episodes with parents 17617 and 18886 must produce distinct stable composite sources; " + directError);

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 38), direct, out var plan, out var error), error);
            var stableGroups = plan.Mappings
                .GroupBy(mapping => mapping.Source)
                .OrderBy(group => group.Key.MediaId, StringComparer.Ordinal)
                .ToList();
            Assert(plan.Mappings.Count == 38 && plan.UnmatchedRuns.Count == 0 && plan.IsComposite &&
                   stableGroups.Count == 2 && stableGroups.Single(group => group.Key.MediaId == "17617").Count() == 28 &&
                   stableGroups.Single(group => group.Key.MediaId == "18886").Count() == 10,
                "a marked 38-episode Frieren Season must keep all exact Episode DandanIDs as 17617(28)+18886(10), " +
                "be composite, and never regress to an all-unmatched preview when candidate search is absent or ambiguous");
            Assert(plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").Source.MediaId == "18886" &&
                   plan.Mappings.Single(mapping => mapping.LocalEpisodeItemId == "local-29").SourceEpisodeId == "188860001",
                "the restart preview must preserve the exact S2 parent and episode identity for local E29");
        }

        private static void KeepsMarkedPreviewDirectEvidenceAheadOfFreshSearch()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            Assert(!controller.Contains("compositeMarked") &&
                    !controller.Contains("BuildCompositePlanAsync(latest, null, true") &&
                    controller.Contains("InitializeDecision(result, scrapers, true);"),
                "r4 Season preview must ignore durable markers and local Episode IDs and begin from a fresh explicit plan");
        }

        private static void SupportsCompositeMappingForAnyLocalSeason()
        {
            // The behavior must not be coupled to Season 1: this represents a
            // later local season containing an upstream continuation and an OVA.
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 15), null, out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "BilibiliID", "later-s3", "s3-1",
                    Enumerable.Range(1, 12).Select(number => Source("s3-" + number, "c3-" + number, number))),
                out plan, out var applied, out error), error);
            Assert(applied == 12 && RunIds(plan.UnmatchedRuns.Single()) == "local-13,local-14,local-15",
                "a non-first local season must expose its own remaining temporary group");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-13", "BilibiliID", "later-special", "sp-1",
                    Enumerable.Range(1, 3).Select(number => Source("sp-" + number, "spc-" + number, number))),
                out plan, out applied, out error), error);
            Assert(applied == 3 && plan.IsComposite && plan.UnmatchedRuns.Count == 0,
                "a later local season must be able to complete with an independently selected special");
        }

        private static void ContinuesPrimaryAcrossInteriorExactEpisode()
        {
            var direct = new[] { Mapping("local-13", "DandanID", "direct-episode-provider:DandanID", "s1-13", "c13") };
            direct[0].Origin = "episode-provider-id";
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 38), direct, out var plan, out var error), error);
            var primary = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "s1", MediaLookupId = "s1-lookup" };
            var allPrimaryEpisodes = Enumerable.Range(1, 28).Select(x => Source("s1-" + x, "c" + x, x));
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, primary, allPrimaryEpisodes,
                "automatic-primary", out plan, out var exhausted, out error), error);
            Assert(plan.UnmatchedRuns.Single().Episodes[0].ItemId == "local-29" &&
                   exhausted && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-14").SourceEpisodeId == "s1-14",
                "an interior exact primary episode must continue S1 in order and exhaust it before the residual run");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-29", "DandanID", "s2", "s2-1",
                    Enumerable.Range(1, 10).Select(x => Source("s2-" + x, "s2c" + x, x))),
                out plan, out _, out error), error);
            Assert(plan.IsComposite && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-29").SourceEpisodeId == "s2-1",
                "after the primary is exhausted, the residual must begin at verified S2 E1");
        }

        private static void SelectsSupplementalAfterPrimaryExhaustion()
        {
            var primary = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "s1", MediaLookupId = "s1-lookup" };
            var supplemental = new CompositeSeasonSourceIdentity { ProviderId = "DandanID", MediaId = "s2", MediaLookupId = "s2-lookup" };
            var candidates = new List<DanmuMatchCandidate>
            {
                new DanmuMatchCandidate { Id = "s1-lookup", Site = "DandanID", SourceOrder = 0, Score = 0.99 },
                new DanmuMatchCandidate { Id = "s2-lookup", Site = "DandanID", SourceOrder = 0, Score = 0.95 },
                new DanmuMatchCandidate { Id = "s3-lookup", Site = "DandanID", SourceOrder = 0, Score = 0.93 },
            };
            Assert(CompositeSeasonMatchService.SelectSupplementalCandidate(candidates, Enumerable.Empty<CompositeSeasonSourceIdentity>())?.Id == "s1-lookup" &&
                   CompositeSeasonMatchService.SelectSupplementalCandidate(candidates, new[] { primary })?.Id == "s2-lookup" &&
                   CompositeSeasonMatchService.SelectSupplementalCandidate(candidates, new[] { primary, supplemental })?.Id == "s3-lookup",
                "all exhausted sources must be filtered before unique high-confidence supplemental selection");
        }

        private static void ContinuesSupplementalAcrossSpecialAndReentrantDirectEvidence()
        {
            var primary = Enumerable.Range(1, 28).Select(number => Mapping("local-" + number,
                "DandanID", "s1", "s1-" + number, "s1c-" + number)).ToList();
            primary.Add(Mapping("local-34", "DandanID", "special", "sp-1", "spc-1"));
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 39), primary, out var plan, out var error), error);
            var secondSource = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "s2", MediaLookupId = "s2-lookup",
            };
            var secondEpisodes = Enumerable.Range(1, 10).Select(number => Source("s2-" + number, "s2c-" + number, number));
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, secondSource, secondEpisodes,
                "automatic-residual", out plan, out var exhausted, out error), error);
            Assert(exhausted && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-35").SourceEpisodeId == "s2-6" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-35").SourceEpisodeNumber == 6,
                "a supplemental source must continue across an intervening direct special instead of restarting at E1");

            var reentrantDirect = Enumerable.Range(1, 28).Select(number => Mapping("local-" + number,
                "DandanID", "s1", "s1-" + number, "s1c-" + number)).ToList();
            reentrantDirect.Add(Mapping("local-34", "DandanID", "special", "sp-1", "spc-1"));
            foreach (var number in Enumerable.Range(1, 5))
            {
                var direct = Mapping("local-" + (28 + number), "DandanID",
                    "direct-episode-provider:DandanID", "s2-" + number, "s2c-" + number);
                direct.Origin = "episode-provider-id";
                direct.Source.MediaLookupId = "s2-direct-" + number;
                reentrantDirect.Add(direct);
            }
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 39), reentrantDirect, out plan, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, secondSource, secondEpisodes,
                "automatic-residual", out plan, out exhausted, out error), error);
            Assert(exhausted && plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-29").Source.MediaId == "s2" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-29").Source.MediaLookupId == "s2-direct-1" &&
                   plan.Mappings.Single(x => x.LocalEpisodeItemId == "local-35").SourceEpisodeId == "s2-6",
                "re-entry must normalize direct S2 placeholders, preserve their lookup tokens, and resume at S2 E6 after the special");
        }

        private static void ParsesCompositeSelectionsFromScalarQueryJson()
        {
            var property = typeof(DanmuParams).GetProperty("CompositeSelections");
            var member = property?.GetCustomAttribute<DataMemberAttribute>();
            Assert(property?.PropertyType == typeof(string) && property.Name == "CompositeSelections" &&
                   member?.Name == "compositeSelections",
                "the Emby GET-bound compositeSelections CLR/DataMember contract must remain one scalar JSON string");
            Assert(typeof(DanmuParams).GetProperty("ParsedCompositeSelections")?.PropertyType ==
                   typeof(List<DanmuCompositeSeasonSelection>) &&
                   typeof(DanmuParams).GetProperty("ParsedCompositeSelections")
                       .GetCustomAttribute<IgnoreDataMemberAttribute>() != null,
                "the parsed runtime selections must not be exposed to the Emby GET binder");

            const string frontendPayload = "[{\"LocalStartEpisodeItemId\":\"episode-29\",\"RequestedEpisodeCount\":10,\"Site\":\"DandanID\",\"CandidateId\":\"frieren-s2\",\"SourceStartEpisodeId\":\"s2-1\",\"SourceStartEpisodeNumber\":1,\"MatchOrigin\":\"manual\"}]";
            Assert(DanmuCompositeSeasonSelectionJson.TryParse(frontendPayload, out var parsed, out var error) &&
                   parsed.Count == 1 && parsed[0].CandidateId == "frieren-s2" &&
                   parsed[0].RequestedEpisodeCount == 10 && string.IsNullOrEmpty(error),
                "the frontend JSON.stringify payload must deserialize into a compact composite selection");
            Assert(DanmuCompositeSeasonSelectionJson.TryParse("[]", out parsed, out error) && parsed.Count == 0,
                "an empty array must remain compatible with direct-only composite plans");
            Assert(!DanmuCompositeSeasonSelectionJson.TryParse("{not-json", out parsed, out error) &&
                   parsed.Count == 0 && !string.IsNullOrWhiteSpace(error),
                "malformed composite JSON must be safely rejected with a readable error");
        }

        private static void DoesNotClassifySingleSourcePartialCoverageAsComposite()
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 12), null, out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "DandanID", "one-source", "one-1",
                    Enumerable.Range(1, 8).Select(number => Source("one-" + number, "one-comment-" + number, number))),
                out plan, out var applied, out error), error);
            Assert(applied == 8 && !plan.IsComposite && plan.SeasonBindingUnsafe &&
                   !plan.CanPersistCompleteSeasonBinding &&
                   RunIds(plan.UnmatchedRuns.Single()) == "local-9,local-10,local-11,local-12",
                "a partial one-source plan is not composite, but must still block and clear a stale complete Season binding after persistence");
        }

        private static void MapsTwentyFiveEpisodePartSourcesWithBindingSafety()
        {
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 25), null,
                out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "DandanID", "spy-family-part-1", "part1-1",
                    Enumerable.Range(1, 12).Select(number =>
                        Source("part1-" + number, "part1-comment-" + number, number))),
                out plan, out var applied, out error), error);
            Assert(applied == 12 && plan.Mappings.Count == 12 &&
                   RunIds(plan.UnmatchedRuns.Single()) == string.Join(",",
                       Enumerable.Range(13, 13).Select(number => "local-" + number)) &&
                   plan.SeasonBindingUnsafe && !plan.CanPersistCompleteSeasonBinding,
                "Spy x Family Part 1 must map 12/25 and leave one maximal 13-Episode temporary run");

            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-13", "DandanID", "spy-family-part-2", "part2-1",
                    Enumerable.Range(1, 13).Select(number =>
                        Source("part2-" + number, "part2-comment-" + number, number))),
                out plan, out applied, out error), error);
            Assert(applied == 13 && plan.Mappings.Count == 25 && plan.UnmatchedRuns.Count == 0 &&
                   plan.IsComposite && plan.SeasonBindingUnsafe && !plan.CanPersistCompleteSeasonBinding,
                "supplemental Part 2 must reach full Episode coverage while remaining binding-unsafe because it has two sources");
        }

        private static void CoordinatesSingletonAndSeriesTargetSetsIdentically()
        {
            Func<string, CompositeSeasonTargetRequest> target = seasonId =>
                new CompositeSeasonTargetRequest
                {
                    SeasonId = seasonId,
                    BuildPreviewAsync = (ignored, parent) => Task.FromResult(new DanmuSeasonMatchResult
                    {
                        SeasonId = seasonId,
                        Status = "matched",
                    }),
                };
            var singleton = CompositeSeasonTargetSetCoordinator.BuildAsync(
                new[] { target("season-1") }, default).GetAwaiter().GetResult();
            var series = CompositeSeasonTargetSetCoordinator.BuildAsync(
                new[] { target("season-1"), target("season-2") }, default).GetAwaiter().GetResult();
            Assert(singleton.Count == 1 && singleton[0].SeasonId == series[0].SeasonId &&
                   series.Select(result => result.SeasonId).SequenceEqual(new[] { "season-1", "season-2" }),
                "single-Season and whole-Series entry points must use the same stable target-set coordinator contract");
        }

        private static void CoordinatesWithoutChildDeadlineAndPropagatesExplicitCancellation()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<DanmuSeasonMatchResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var observedExecutionToken = default(CancellationToken);
            var observedParentToken = default(CancellationToken);
            using (var cancellation = new CancellationTokenSource())
            {
                var build = CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
                {
                    new CompositeSeasonTargetRequest
                    {
                        SeasonId = "season-no-deadline",
                        BuildPreviewAsync = (executionToken, parentToken) =>
                        {
                            observedExecutionToken = executionToken;
                            observedParentToken = parentToken;
                            started.TrySetResult(true);
                            return release.Task;
                        },
                    },
                }, cancellation.Token);
                AwaitWithWatchdog(started.Task, "the composite target must start");
                Assert(!build.IsCompleted && observedExecutionToken == cancellation.Token &&
                       observedParentToken == cancellation.Token,
                    "the coordinator must forward its caller token directly without creating a child deadline");
                release.SetResult(new DanmuSeasonMatchResult
                {
                    SeasonId = "season-no-deadline",
                    Status = "matched",
                });
                var result = AwaitWithWatchdog(build, "the released composite target must settle");
                Assert(result.Count == 1 && result[0].SeasonId == "season-no-deadline",
                    "a long-running target must complete normally when its provider settles");
            }

            var cancelledStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (var cancellation = new CancellationTokenSource())
            {
                var build = CompositeSeasonTargetSetCoordinator.BuildAsync(new[]
                {
                    new CompositeSeasonTargetRequest
                    {
                        SeasonId = "season-explicit-cancel",
                        BuildPreviewAsync = (executionToken, parentToken) =>
                        {
                            cancelledStarted.TrySetResult(true);
                            return AwaitExplicitCancellationAsync(executionToken);
                        },
                    },
                }, cancellation.Token);
                AwaitWithWatchdog(cancelledStarted.Task, "the cancellable composite target must start");
                cancellation.Cancel();
                Assert(Task.WhenAny(build, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult() == build,
                    "explicit parent cancellation must reach the active target promptly");
                var cancelled = false;
                try
                {
                    build.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert(cancelled,
                    "the coordinator must preserve explicit parent cancellation rather than converting it to a timeout");
            }
        }

        private static async Task<DanmuSeasonMatchResult> AwaitExplicitCancellationAsync(
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("An infinite cancellation wait completed without cancellation.");
        }

        private static void MapsMultipleSpecialRunsWithoutChangingLocalSeasonMembership()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "main", "main-1", "main-c1"),
                Mapping("local-2", "DandanID", "main", "main-2", "main-c2"),
                Mapping("local-5", "DandanID", "main", "main-5", "main-c5"),
                Mapping("local-6", "DandanID", "main", "main-6", "main-c6"),
                Mapping("local-8", "DandanID", "main", "main-8", "main-c8"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 8), direct, out var plan, out var error), error);
            Assert(RunIds(plan.UnmatchedRuns[0]) == "local-3,local-4" && RunIds(plan.UnmatchedRuns[1]) == "local-7",
                "separate holes must be retained as separate temporary groups in stable local order");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-3", "DandanID", "special-a", "a-1", new[] { Source("a-1", "a-c1", 1), Source("a-2", "a-c2", 2) }),
                out plan, out var applied, out error), error);
            Assert(applied == 2 && RunIds(plan.UnmatchedRuns.Single()) == "local-7",
                "mapping one special group must not move or absorb another unmatched group");
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-7", "DandanID", "special-b", "b-1", new[] { Source("b-1", "b-c1", 1) }),
                out plan, out applied, out error), error);
            Assert(applied == 1 && plan.UnmatchedRuns.Count == 0 && plan.IsComposite,
                "multiple independently selected special groups must complete without changing local episode identities");
        }

        private static void SeparatesCanonicalMediaIdentityFromLookupToken()
        {
            var resolved = new ScraperMedia
            {
                // This is the canonical identity returned by the provider detail
                // response, while the token below is what GetMedia accepts.
                Id = "canonical-frieren-s2",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "source-e1", CommentId = "comment-e1", EpisodeNumber = 1 },
                },
            };
            var source = CompositeSeasonMatchService.GetSource("DandanID", resolved, "lookup-token-s2");
            Assert(source.MediaId == "canonical-frieren-s2" && source.MediaLookupId == "lookup-token-s2",
                "canonical media identity and the provider lookup token must remain separate");

            var sameCanonicalDifferentLookup = new CompositeSeasonSourceIdentity
            {
                ProviderId = "dandanid", MediaId = "CANONICAL-FRIEREN-S2", MediaLookupId = "retry-token-s2",
            };
            Assert(source.Equals(sameCanonicalDifferentLookup),
                "composite classification must use provider plus canonical media identity, never a transient lookup token");

            var direct = CompositeSeasonMatchService.CreateDirectMapping(
                "local-1", "DandanID", resolved, "direct-episode-lookup-token");
            Assert(direct != null && direct.Source.MediaId == "canonical-frieren-s2" &&
                   direct.Source.MediaLookupId == "direct-episode-lookup-token" &&
                   direct.SourceEpisodeId == "source-e1" && direct.CommentId == "comment-e1",
                "a direct Episode ProviderId mapping must keep its exact lookup token while retaining canonical media ownership");
        }

        private static void RejectsOverlapsAndUnverifiedMappings()
        {
            var local = LocalEpisodes(1, 2);
            Assert(!CompositeSeasonPlanner.TryCreatePlan(local, new[]
            {
                Mapping("local-1", "DandanID", "s1", "source-1", "comment-1"),
                Mapping("local-1", "DandanID", "s1", "source-2", "comment-2"),
            }, out _, out var error) && error.Contains("only be mapped once"),
                "overlapping local mappings must be rejected");

            Assert(!CompositeSeasonPlanner.TryCreatePlan(local, new[]
            {
                Mapping("local-1", "DandanID", "s1", "source-1", "comment-1"),
                Mapping("local-2", "DandanID", "s1", "source-1", "comment-1"),
            }, out _, out error) && error.Contains("source episode"),
                "one source episode cannot silently serve two local episodes");

            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out error), error);
            Assert(!CompositeSeasonPlanner.TryApplySegment(plan,
                Segment("local-1", "DandanID", "s1", "source-1", new[] { Source("source-1", string.Empty, 1) }),
                out _, out _, out error) && error.Contains("CommentId"),
                "unverified source episodes must never enter a download plan");
        }

        private static void IdentifiesCompositeSourcesByProviderAndMediaId()
        {
            var mappings = new[]
            {
                Mapping("local-1", "DandanID", "frieren-s1", "source-1", "comment-1"),
                Mapping("local-2", "dandanid", "FRIEREN-S1", "source-2", "comment-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), mappings, out var plan, out var error), error);
            Assert(!plan.IsComposite, "provider/media identity equality must be stable and case-insensitive");

            mappings[1] = Mapping("local-2", "DandanID", "frieren-s2", "source-1", "comment-1");
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), mappings, out plan, out error), error);
            Assert(plan.IsComposite, "different media IDs must classify a season as composite");
        }

        private static void SortsByStableLocalIdentityWithoutDependingOnDisplayNumbers()
        {
            var local = new[]
            {
                new CompositeSeasonLocalEpisode { ItemId = "episode-c", SortOrder = 3 },
                new CompositeSeasonLocalEpisode { ItemId = "episode-a", EpisodeNumber = 1, SortOrder = 20 },
                new CompositeSeasonLocalEpisode { ItemId = "episode-b", EpisodeNumber = 1, SortOrder = 10 },
                new CompositeSeasonLocalEpisode { ItemId = "episode-d", SortOrder = 2 },
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            Assert(string.Join(",", plan.OrderedEpisodes.Select(episode => episode.ItemId)) == "episode-d,episode-c,episode-b,episode-a",
                "duplicate and missing display numbers must retain stable ItemId-based episode identity");
        }

        private static void KeepsDirectEpisodeEvidenceFromFalselyCreatingCompositeSources()
        {
            var mappings = new[]
            {
                Mapping("local-1", "DandanID", "direct-episode-provider:DandanID", "direct-1", "comment-1"),
                Mapping("local-2", "DandanID", "direct-episode-provider:DandanID", "direct-2", "comment-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), mappings, out var plan, out var error), error);
            Assert(!plan.IsComposite,
                "multiple exact Episode ProviderIds from one provider must not falsely classify a Season as composite");

            var withSecondSeason = mappings.Concat(new[]
            {
                Mapping("local-3", "DandanID", "frieren-s2", "s2-1", "s2-comment-1"),
            });
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 3), withSecondSeason, out plan, out error), error);
            Assert(plan.IsComposite,
                "direct Episode evidence plus a separately verified upstream Season must classify as composite");
        }

        private static void ValidatesAndAppliesAuthoritativeExclusionsBeforeDirectEvidence()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-4", "DandanID", "s2", "s2-2", "s2c-2"),
                Mapping("local-5", "DandanID", "s2", "s2-3", "s2c-3"),
            };
            Assert(!CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, null,
                    new[] { "local-3", "foreign" }, false, out _, out var error) &&
                   error.Contains("outside the target season"),
                "a single foreign exclusion must reject the whole authoritative draft");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, null,
                new[] { "local-5", "local-3", "local-5", "local-4" }, false, out var plan, out error), error);
            Assert(string.Join(",", plan.EffectiveExcludedLocalEpisodeItemIds) == "local-3,local-4,local-5" &&
                   plan.Mappings.Select(x => x.LocalEpisodeItemId).SequenceEqual(new[] { "local-1", "local-2" }) &&
                   RunIds(plan.UnmatchedRuns.Single()) == "local-3,local-4,local-5",
                "trailing exclusions must deduplicate in authoritative local order and suppress direct evidence before runs are built");

            var replacement = new[]
            {
                Mapping("local-3", "DandanID", "s3", "s3-1", "s3c-1"),
                Mapping("local-4", "DandanID", "s3", "s3-2", "s3c-2"),
                Mapping("local-5", "DandanID", "s3", "s3-3", "s3c-3"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct, replacement,
                new[] { "local-3", "local-4", "local-5" }, false, out plan, out error), error);
            Assert(plan.Mappings.Skip(2).All(x => x.Source.MediaId == "s3") && plan.UnmatchedRuns.Count == 0,
                "a verified replacement must occupy the removed trailing range without changing retained mappings");
        }

        private static void SplitsEditableRunsAndRestoresOnlyTheRequestedRange()
        {
            var mappings = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "special", "sp-1", "spc-1"),
                Mapping("local-3", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-4", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-5", "DandanID", "s2", "s2-2", "s2c-2"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), mappings, out var plan, out var error), error);
            var cards = CompositeSeasonPlanner.GetEditableMappedRuns(plan);
            Assert(cards.Count == 4 &&
                   string.Join(",", cards.Select(card => string.Join("/", card.Mappings.Select(x => x.LocalEpisodeItemId)))) ==
                   "local-1,local-2,local-3,local-4/local-5",
                "S1-special-S1 must render as three independent source cards, with contiguous S2 kept together");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), mappings, null,
                new[] { "local-2" }, false, out plan, out error), error);
            Assert(RunIds(plan.UnmatchedRuns.Single()) == "local-2" &&
                   plan.Mappings.Any(x => x.LocalEpisodeItemId == "local-1") &&
                   plan.Mappings.Any(x => x.LocalEpisodeItemId == "local-3"),
                "removing an interior special must leave both neighboring source cards intact");
            Assert(CompositeSeasonPlanner.TryRestoreExcludedLocalEpisodeItemIds(LocalEpisodes(1, 5),
                plan.EffectiveExcludedLocalEpisodeItemIds, new[] { "local-2" }, out var restored, out error) &&
                   restored.Count == 0,
                "Restore must remove only its own local ids from dialog intent");
            Assert(!CompositeSeasonPlanner.TryRestoreExcludedLocalEpisodeItemIds(LocalEpisodes(1, 5),
                plan.EffectiveExcludedLocalEpisodeItemIds, new[] { "local-3" }, out _, out error) &&
                   error.Contains("currently excluded"),
                "Restore must reject a non-excluded foreign-to-the-draft local range");
        }

        private static void RetainsCompositeSafetyForSubsetAndZeroPersist()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 3), direct, null,
                new[] { "local-3" }, false, out var plan, out var error), error);
            Assert(!plan.IsComposite && plan.CompositeSafetyRequired,
                "a pre-exclusion two-source plan must retain composite safety after a one-source subset draft");
            Assert(!CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, false) &&
                   CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, true),
                "zero persisted files must never create a marker/cleanup transition, while the first persisted file keeps the barrier");
        }

        private static void RebuildsPreviewAndDownloadFromTheSameExclusionAwarePlan()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            Assert(controller.Contains("ExcludedLocalEpisodeItemIds") &&
                   controller.Contains("ParsedExcludedLocalEpisodeItemIds") &&
                   controller.Contains("DanmuExcludedLocalEpisodeItemIdsJson.TryParse"),
                "the scalar GET exclusion contract must be parsed before preview/download dispatch");
            Assert(controller.Contains("BuildCompositePlanAsync(latest, request.ParsedCompositeSelections, false,") &&
                    controller.Contains("BuildCompositePlanAsync(season, request.ParsedCompositeSelections, false,") &&
                    controller.Contains("MergeEpisodeExclusions") &&
                    controller.Contains("TryBuildOwnedPlanningContext") &&
                    !controller.Contains("CompositeSeasonTargetOwnership.Resolve(inventories)"),
                "composite preview and tracked download must rebuild from the same parsed exclusions and target-season scope");
            Assert(controller.Contains("TryCreatePlan(local, mappings, null,\n                    effectiveExclusions, durableCompositeMarker") &&
                    controller.Contains("TryCreatePlan(local, mappings, replacementMappings,\n                    effectiveExclusions, durableCompositeMarker"),
                "the controller must validate exclusions before direct evidence and then rebuild confirmed replacements");
            Assert(controller.Contains("IsCompositePlan = build.Plan.SeasonBindingUnsafe") &&
                   controller.Contains("CanPersistCompleteSeasonBinding") &&
                    controller.Contains("CommitSeasonDisplayMirrorAfterTerminalAsync"),
                "subset and incomplete downloads must retain the Season-binding barrier, while only complete safe plans may persist a Season binding");
            var automatic = File.ReadAllText(Path.Combine(repositoryRoot, "LibraryManagerEventsHelper.cs"))
                .Replace("\r\n", "\n");
            Assert(automatic.Contains("null, null, false, out var plan") &&
                    automatic.Contains("plan.CanPersistCompleteSeasonBinding") &&
                    automatic.Contains("BeginCompositeSeasonWrite(season, plan.CompositeSafetyRequired)") &&
                    automatic.Contains("SeasonDisplayMirrorPolicy.CanCommit") &&
                    !automatic.Contains("OnCompositeSeasonFilePersistedAsync"),
                "automatic downloads must preserve the lease and defer Season mirrors until terminal success");
        }

        private static void VerifiesLockedProviderCompletionSeam()
        {
            var closedAWithFaultedB = new[]
            {
                new DanmuSearchCompletionDiagnostic { Provider = "provider-a", Status = "completed" },
                new DanmuSearchCompletionDiagnostic { Provider = "provider-b", Status = "failed" },
            };
            Assert(RemainderProviderCompletion.IsClosed(closedAWithFaultedB, "provider-a"),
                "a completed locked provider may recurse even when an unrelated provider failed");
            Assert(!RemainderProviderCompletion.IsClosed(new[]
            {
                new DanmuSearchCompletionDiagnostic { Provider = "provider-a", Status = "timed_out", TimedOut = true },
            }, "provider-a") &&
                !RemainderProviderCompletion.IsClosed(null, "provider-a"),
                "timed-out or absent locked-provider diagnostics must stop before remainder details or commits");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var populate = SliceSource(controller,
                "private async Task PopulateCompositePreviewIfRequired(",
                "// This loop deliberately sits above BuildCompositePlanAsync:");
            var extension = SliceSource(controller,
                "private async Task<CompositePlanBuild> ExtendInteractiveRemainderPlanAsync(",
                "private static RemainderAuthoritativeSnapshot CreateRemainderSnapshot(");
            Assert(controller.Contains("RemainderOperationPolicy.InteractiveRecursive, authoritativeSearch.CompletionDiagnostics,\n                authoritativeSearch.WasCancelled") &&
                   controller.Contains("RemainderOperationPolicy.InteractiveRecursive, search.CompletionDiagnostics,\n                        search.WasCancelled"),
                "manual and scored first-segment paths must pass server-owned completion diagnostics and global cancellation separately");
            Assert(populate.Contains("!canonicalSearchWasCancelled") &&
                   !populate.Contains("canonicalComplete"),
                "the preview gate must preserve global cancellation without applying aggregate search completeness to locked recursion");
            Assert(extension.Contains("RemainderProviderCompletion.IsClosed(canonicalCompletionDiagnostics, providerLock)") &&
                   extension.Contains("logicalSearch.CompletionDiagnostics, providerLock") &&
                   !extension.Contains("!logicalSearch.IsComplete") &&
                   extension.IndexOf("RemainderProviderCompletion.IsClosed(canonicalCompletionDiagnostics, providerLock)", StringComparison.Ordinal) <
                   extension.IndexOf("foreach (var candidate in currentCandidates)", StringComparison.Ordinal) &&
                   extension.IndexOf("RemainderProviderCompletion.IsClosed(canonicalCompletionDiagnostics, providerLock)", StringComparison.Ordinal) <
                   extension.IndexOf("CandidateEvidence.RegisterRemainder", StringComparison.Ordinal),
                "the production remainder seam must evaluate initial and fresh logical pools only through the locked provider diagnostics before remainder detail or commit");
        }

        private static void RejectsIncompleteAutomaticSeasonAndResidualSearches()
        {
            var completeness = typeof(Emby.Plugin.Danmu.LibraryManagerEventsHelper).GetMethod(
                "CanUseAutomaticSearch",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert(completeness != null,
                "automatic matching must expose one completed-provider predicate for initial Season and Movie searches");

            var unique = new DanmuMatchCandidate
            {
                Site = "DandanID", Id = "unique", Name = "Unique", Score = 0.99, SourceOrder = 0,
            };
            var incomplete = new DanmuMatchSearchResult
            {
                IsComplete = false,
                CompletedProviderCount = 1,
                Candidates = new List<DanmuMatchCandidate> { unique },
                CompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>
                {
                    new DanmuSearchCompletionDiagnostic { Provider = "Bilibili", Status = "timed_out", TimedOut = true },
                    new DanmuSearchCompletionDiagnostic { Provider = "Bilibili", Status = "unstarted", Cancelled = true },
                },
            };
            Assert((bool)completeness.Invoke(null, new object[] { incomplete }),
                "a unique completed-provider candidate must remain usable after a sibling provider fault");

            var allFailed = new DanmuMatchSearchResult
            {
                IsComplete = false,
                CompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>
                {
                    new DanmuSearchCompletionDiagnostic { Provider = "Bilibili", Status = "failed" },
                },
            };
            Assert(!(bool)completeness.Invoke(null, new object[] { allFailed }),
                "all-provider failure must stop before candidate selection, binding, or download");

            var cancelled = new DanmuMatchSearchResult
            {
                CompletedProviderCount = 1,
                WasCancelled = true,
            };
            Assert(!(bool)completeness.Invoke(null, new object[] { cancelled }),
                "parent or user cancellation must override completed-provider coverage");

            var complete = new DanmuMatchSearchResult
            {
                IsComplete = true,
                CompletedProviderCount = 1,
                Candidates = new List<DanmuMatchCandidate> { unique },
            };
            Assert((bool)completeness.Invoke(null, new object[] { complete }) &&
                   ReferenceEquals(DanmuMatchScorer.SelectAutoCandidate(complete.Candidates), unique),
                "a complete uniquely confident automatic result must preserve the r1 selection behavior");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var automatic = File.ReadAllText(Path.Combine(repositoryRoot, "LibraryManagerEventsHelper.cs"))
                .Replace("\r\n", "\n");
            var initialGuard = automatic.IndexOf(
                "if (!CanUseAutomaticSearch(search))\n                            {\n                                LogIncompleteAutomaticSearch(originalSeasonName, \"season\", search);\n                                continue;",
                StringComparison.Ordinal);
            var initialSelection = automatic.IndexOf(
                "selectedCandidate = DanmuMatchScorer.SelectAutoCandidate(search.CanonicalCandidates);",
                StringComparison.Ordinal);
            var automaticSeasonPath = SliceSource(automatic,
                "private async Task<bool> DownloadAutomaticSeasonWithCompositePlan(",
                "private static DanmuCompositeSeasonSelection CreateAutomaticSelection(");
            var movieGuard = automatic.IndexOf(
                "if (!CanUseAutomaticSearch(movieSearch))",
                StringComparison.Ordinal);
            var movieSelection = automatic.IndexOf(
                "selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate(movieSearch.CanonicalCandidates);",
                StringComparison.Ordinal);
            Assert(initialGuard >= 0 && initialGuard < initialSelection,
                "initial automatic Season search must reject missing completed-provider coverage before selecting a candidate");
            Assert(!automaticSeasonPath.Contains("residual-range", StringComparison.Ordinal) &&
                   !automaticSeasonPath.Contains("SelectResidualCandidate(", StringComparison.Ordinal) &&
                   !automaticSeasonPath.Contains("automatic-residual", StringComparison.Ordinal) &&
                   automaticSeasonPath.Contains(
                       "var automaticSelections = new List<DanmuCompositeSeasonSelection> { automaticSelection };",
                       StringComparison.Ordinal) &&
                   automaticSeasonPath.Contains("TryApplySegmentResolved(", StringComparison.Ordinal) &&
                   automaticSeasonPath.Contains("RebuildAutomaticPlanAsync(", StringComparison.Ordinal) &&
                   automaticSeasonPath.Contains("DownloadEpisodeForProgress(", StringComparison.Ordinal),
                "automatic library import must use only its initial authoritative selection, rebuild, and download path without residual search or selection");
            Assert(movieGuard >= 0 && movieGuard < movieSelection,
                "automatic Movie search without completed-provider coverage must reject selection, binding, or downloading");
        }

        private static void PreservesDirectMetadataAcrossRemoveReplacementAndRestore()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-4", "DandanID", "s2", "s2-2", "s2c-2"),
            };
            foreach (var mapping in direct)
            {
                mapping.Origin = "episode-provider-id";
            }
            var durableSnapshot = direct.Select(MappingSnapshot).ToArray();

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, null,
                    new[] { "local-3", "local-4" }, false, out var removed, out var error), error);
            Assert(RunIds(removed.UnmatchedRuns.Single()) == "local-3,local-4" &&
                   removed.Mappings.Select(mapping => mapping.LocalEpisodeItemId)
                       .SequenceEqual(new[] { "local-1", "local-2" }),
                "removing a direct trailing run must retain every mapping outside that exact local range");
            Assert(direct.Select(MappingSnapshot).SequenceEqual(durableSnapshot),
                "session removal must not mutate durable direct ProviderId/source evidence supplied to the planner");

            var replacement = new[]
            {
                Mapping("local-3", "BilibiliID", "s2-rematch", "r-1", "rc-1"),
                Mapping("local-4", "BilibiliID", "s2-rematch", "r-2", "rc-2"),
            };
            replacement[0].Origin = replacement[1].Origin = "manual";
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, replacement,
                    new[] { "local-3", "local-4" }, false, out var rematched, out error), error);
            Assert(rematched.UnmatchedRuns.Count == 0 &&
                   rematched.Mappings.Where(mapping => mapping.LocalEpisodeItemId == "local-3" ||
                                                       mapping.LocalEpisodeItemId == "local-4")
                       .All(mapping => mapping.Source.MediaId == "s2-rematch" && mapping.Origin == "manual"),
                "a confirmed replacement must fill only the excluded direct run with its exact verified source");
            Assert(direct.Select(MappingSnapshot).SequenceEqual(durableSnapshot),
                "replacement planning must leave the original Episode metadata evidence byte-for-byte unchanged");

            Assert(CompositeSeasonPlanner.TryRestoreExcludedLocalEpisodeItemIds(
                    LocalEpisodes(1, 4),
                    rematched.EffectiveExcludedLocalEpisodeItemIds,
                    new[] { "local-3", "local-4" },
                    out var restoredExclusions,
                    out error) && restoredExclusions.Count == 0,
                error);
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, null,
                    restoredExclusions, false, out var restored, out error), error);
            Assert(restored.Mappings.Select(MappingSnapshot).SequenceEqual(durableSnapshot),
                "Restore plus removal of the run's replacement intent must reconstruct unchanged direct evidence");
        }

        private static void RetainsCompositeSafetyWhenReplacementCollapsesToOneSource()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-3", "DandanID", "s2", "s2-1", "s2c-1"),
                Mapping("local-4", "DandanID", "s2", "s2-2", "s2c-2"),
            };
            var sameSourceReplacement = new[]
            {
                Mapping("local-3", "DandanID", "s1", "s1-3", "s1c-3"),
                Mapping("local-4", "DandanID", "s1", "s1-4", "s1c-4"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), direct, sameSourceReplacement,
                    new[] { "local-3", "local-4" }, false, out var plan, out var error), error);
            Assert(!plan.IsComposite && plan.CompositeSafetyRequired &&
                   plan.Mappings.Select(mapping => mapping.Source.MediaId).Distinct().Single() == "s1",
                "a same-source replacement may collapse the executable plan to one source but must not downgrade pre-exclusion composite safety");
            Assert(!CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, false) &&
                   CompositeSeasonPlanner.ShouldApplyCompositeSafetyAfterPersist(plan, true),
                "same-source replacement safety must still be persistence-gated and remain inert for zero files");
        }

        private static void RejectsForeignAndStaleTemporaryRangesWithoutMutatingThePlan()
        {
            var direct = new[]
            {
                Mapping("local-1", "DandanID", "s1", "s1-1", "s1c-1"),
                Mapping("local-2", "DandanID", "s1", "s1-2", "s1c-2"),
                Mapping("local-5", "DandanID", "s1", "s1-5", "s1c-5"),
            };
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 5), direct,
                    out var plan, out var error), error);
            var beforeMappings = plan.Mappings.Select(MappingSnapshot).ToArray();
            var beforeRuns = plan.UnmatchedRuns.Select(RunIds).ToArray();

            Assert(!DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "foreign-item", 2, out _, out _) &&
                   !DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "local-4", 1, out _, out _) &&
                   !DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "local-3", 1, out _, out _),
                "foreign starts, stale shifted starts, and stale shortened counts must all be rejected");
            Assert(plan.Mappings.Select(MappingSnapshot).SequenceEqual(beforeMappings) &&
                   plan.UnmatchedRuns.Select(RunIds).SequenceEqual(beforeRuns),
                "foreign or stale temporary-range validation must leave the authoritative plan unchanged");
            Assert(DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                       plan, "local-3", 2, out var run, out error) && RunIds(run) == "local-3,local-4",
                error);
        }

        private static void VerifiesControllerParityMetadataAndDialogResetContracts()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var preview = SliceSource(controller,
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview",
                "private async Task PopulateCompositePreviewIfRequired");
            var download = SliceSource(controller,
                "private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload",
                "private async Task<DanmuDownloadTaskResult> StartTrackedSingleEpisodeDownload");
            var builder = SliceSource(controller,
                "private async Task<CompositePlanBuild> BuildCompositePlanAsync",
                "private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload");

            Assert(preview.Contains("request.ParsedCompositeSelections, false,") &&
                    preview.Contains("effectiveExclusions") &&
                    download.Contains("request.ParsedCompositeSelections, false,\n                request.ParsedExcludedLocalEpisodeItemIds"),
                "preview and download must pass the identical exclusion and selection collections into the authoritative builder");
            Assert(builder.Contains("TryCreatePlan(local, mappings, null,\n                    effectiveExclusions, durableCompositeMarker") &&
                    builder.Contains("TryCreatePlan(local, mappings, replacementMappings,\n                    effectiveExclusions, durableCompositeMarker") &&
                    builder.Contains("TryBuildOwnedPlanningContext"),
                "the shared builder must apply exclusions to direct evidence before replaying verified replacement selections");
            Assert(!preview.Contains("SaveProviderId", StringComparison.Ordinal) &&
                   !preview.Contains("UpdateItem", StringComparison.Ordinal) &&
                   !preview.Contains("SetProviderId", StringComparison.Ordinal) &&
                   !builder.Contains("SaveProviderId", StringComparison.Ordinal) &&
                   !builder.Contains("UpdateItem", StringComparison.Ordinal) &&
                   !builder.Contains("SetProviderId", StringComparison.Ordinal),
                "preview, removal, rematch, and range validation must not write durable ProviderIds or library metadata");

            var frontend = File.ReadAllText(Path.Combine(
                repositoryRoot, "Frontend", "DanmuSmartMatch.CustomCssJS.js")).Replace("\r\n", "\n");
            var dialog = SliceSource(frontend, "function openDialog(title)", "function setBusy(dialog, message, search)");
            Assert(dialog.Contains("compositeDraft: { exclusions: {}, removedRuns: {} }") &&
                   dialog.Contains("dialog.compositeDraft = { exclusions: {}, removedRuns: {} };") &&
                   dialog.IndexOf("dialog.compositeDraft = { exclusions: {}, removedRuns: {} };", StringComparison.Ordinal) <
                   dialog.IndexOf("overlay.remove();", StringComparison.Ordinal),
                "each dialog must own a fresh composite draft and clear it synchronously before close disposal");
            Assert(frontend.Contains("var draft = dialog && dialog.compositeDraft;") &&
                   !frontend.Contains("localStorage", StringComparison.OrdinalIgnoreCase) &&
                   !frontend.Contains("sessionStorage", StringComparison.OrdinalIgnoreCase),
                "composite exclusions must remain dialog-local and must not survive into a later dialog through browser storage");

            var restoreHandler = SliceSource(frontend,
                "restore.addEventListener(\"click\", async function ()",
                "container.appendChild(restore);");
            var filterIndex = restoreHandler.IndexOf("filterCompositeSelectionsByItemIds(", StringComparison.Ordinal);
            var restoreIndex = restoreHandler.IndexOf("restoreCompositeRun(dialog, season, removed.itemIds)", StringComparison.Ordinal);
            var requestIndex = restoreHandler.IndexOf("requestAuthoritativeCompositePlan", StringComparison.Ordinal);
            Assert(filterIndex >= 0 && restoreIndex > filterIndex && requestIndex > restoreIndex &&
                   restoreHandler.Contains("compositeRequestSelections(selections, season), removed.itemIds") &&
                   restoreHandler.Contains("cloneCompositeSelections(removed.selections)") &&
                   restoreHandler.Contains("currentSelections.concat(restoreSelections)"),
                "Restore must filter replacements by the run's real ItemIds, restore only its saved snapshot, and do both before rebuilding direct evidence");
        }

        private static void PreservesExactBindingScoreIntoSelectedCandidate()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var seasonPreview = SliceSource(controller,
                "private async Task<DanmuSeasonMatchResult> GetSeasonMatchPreview(",
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(");
            Assert(!seasonPreview.Contains("TryGetSavedManualBinding") &&
                    !seasonPreview.Contains("GetSeasonScopes(latest)") &&
                    seasonPreview.Contains("DanmuMatchSearchEngine.SearchSeasonAsync("),
                "r4 Season discovery must ignore saved identifiers and register evidence only from fresh search");

            var selectedMapper = SliceSource(controller,
                "private static DanmuSelectedCandidatePreview ToSelectedCandidate(",
                "private static void StampSeasonCandidateEvidence(");
            Assert(selectedMapper.Contains("MatchScore = candidate.MatchScore") &&
                   selectedMapper.Contains("ScoreOrigin = candidate.ScoreOrigin") &&
                   selectedMapper.Contains("SelectionEvidenceToken = candidate.SelectionEvidenceToken"),
                "the selected card must retain the server score, closed provenance, and opaque evidence token");
        }

        private static void PreservesServerCandidateScoreAcrossOwningPlansAndGroups()
        {
            var local = Enumerable.Range(1, 3).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "score-local-" + number,
                EpisodeNumber = number,
                ParentSeasonNumber = number < 3 ? 1 : 0,
                OriginalEpisodeNumber = number,
                SortOrder = number,
                Ownership = number < 3
                    ? CompositeSeasonOwnershipKind.Owning
                    : CompositeSeasonOwnershipKind.Supplemental,
            }).ToList();
            Assert(CompositeSeasonPlanner.TryCreatePlan(local, null, out var plan, out var error), error);
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID",
                MediaId = "score-season",
                MediaLookupId = "score-candidate",
            };
            var episodes = new[]
            {
                Source("score-source-1", "score-comment-1", 1),
                Source("score-source-2", "score-comment-2", 2),
            };
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                    plan, source, episodes, "scored", 0.93, DanmuMatchScoreOrigin.SearchConfidence,
                    "opaque-evidence", out plan, out error), error);
            Assert(plan.Mappings.Count == 2 && plan.Mappings.All(mapping =>
                       Math.Abs(mapping.MatchScore - 0.93) < 0.0000001 &&
                       mapping.ScoreOrigin == DanmuMatchScoreOrigin.SearchConfidence &&
                       mapping.SelectionEvidenceToken == "opaque-evidence"),
                "the initial owning candidate must retain its server score and closed evidence on every mapping: " +
                string.Join(";", plan.Mappings.Select(mapping => mapping.LocalEpisodeItemId + ":" +
                    mapping.MatchScore + ":" + mapping.ScoreOrigin + ":" + mapping.SelectionEvidenceToken)));

            var groups = CompositeSeasonMatchService.ToGroups(plan, Enumerable.Empty<Episode>());
            var mapped = groups.Single(group => !group.IsTemporary);
            var unmatched = groups.Single(group => group.IsTemporary);
            Assert(mapped.MatchScore.HasValue && Math.Abs(mapped.MatchScore.Value - 0.93) < 0.0000001 &&
                   mapped.ScoreOrigin == DanmuMatchScoreOrigin.SearchConfidence &&
                   mapped.SelectionEvidenceToken == "opaque-evidence",
                "mapped virtual groups must expose the actual server candidate score and origin");
            var mappedJson = JsonSerializer.Serialize(mapped);
            Assert(mappedJson.Contains("\"MatchScore\":0.93", StringComparison.Ordinal) &&
                   mappedJson.Contains("\"ScoreOrigin\":\"search-confidence\"", StringComparison.Ordinal),
                "the mapped-group wire response must include the real score and its closed origin");
            mapped.MatchScore = 0;
            Assert(JsonSerializer.Serialize(mapped).Contains("\"MatchScore\":0", StringComparison.Ordinal),
                "an explicit server score of zero on a mapped group must remain distinguishable from unmatched");
            Assert(!unmatched.MatchScore.HasValue,
                "unmatched temporary groups must omit a score instead of serializing a fabricated zero");
            Assert(JsonSerializer.Serialize(unmatched).Contains("\"MatchScore\":null", StringComparison.Ordinal) &&
                   !JsonSerializer.Serialize(unmatched).Contains("\"MatchScore\":0", StringComparison.Ordinal),
                "a serializer that emits nulls must still distinguish an unmatched group from score zero");

            Assert(CompositeSeasonPlanner.TryCreatePlan(plan.OrderedEpisodes, plan.Mappings,
                    out var rebuilt, out error), error);
            Assert(rebuilt.Mappings.All(mapping => Math.Abs(mapping.MatchScore - 0.93) < 0.0000001 &&
                       mapping.ScoreOrigin == DanmuMatchScoreOrigin.SearchConfidence &&
                       mapping.SelectionEvidenceToken == "opaque-evidence"),
                "authoritative preview/download reconstruction must preserve candidate evidence");

            var exact = CompositeSeasonMatchService.CreateDirectMapping("exact-local", "DandanID",
                new ScraperMedia
                {
                    Id = "exact-parent",
                    Episodes = new List<ScraperEpisode>
                    {
                        new ScraperEpisode
                        {
                            Id = "exact-episode",
                            CommentId = "exact-comment",
                            EpisodeNumber = 1,
                        },
                    },
                }, "exact-token");
            Assert(exact != null && Math.Abs(exact.MatchScore - 1) < 0.0000001 &&
                   exact.ScoreOrigin == DanmuMatchScoreOrigin.ExactEpisodeId &&
                   exact.Origin == "episode-provider-id",
                "an exact single-Episode identifier must remain closed exact evidence, not a browser score");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var compositeBuild = SliceSource(controller,
                "private async Task<CompositePlanBuild> BuildCompositePlanAsync(",
                "private static bool ShouldReportVerifiedSourceEpisodeSurplus(");
            var requestBlock = SliceSource(compositeBuild,
                "var request = new CompositeSeasonSegmentRequest",
                "selection.ServerResolvedAlignmentMode = segmentResolution.Mode;");
            var resolvedApply = requestBlock.IndexOf("TryApplySegmentResolved(", StringComparison.Ordinal);
            var resolvedScore = requestBlock.IndexOf(
                "MatchScore = selectionEvidence.MatchScore", StringComparison.Ordinal);
            var resolvedScoreOrigin = requestBlock.IndexOf(
                "ScoreOrigin = selectionEvidence.ScoreOrigin", StringComparison.Ordinal);
            var resolvedEvidenceToken = requestBlock.IndexOf(
                "SelectionEvidenceToken = selection.SelectionEvidenceToken", StringComparison.Ordinal);
            Assert(resolvedApply >= 0 && resolvedScore >= 0 && resolvedScore < resolvedApply &&
                   resolvedScoreOrigin >= 0 && resolvedScoreOrigin < resolvedApply &&
                   resolvedEvidenceToken >= 0 && resolvedEvidenceToken < resolvedApply,
                "BuildCompositePlanAsync must place server-resolved score, score origin, and closed evidence token on the authoritative request before applying the segment");
            var model = File.ReadAllText(Path.Combine(
                repositoryRoot, "Model", "DanmuMatchResult.cs")).Replace("\r\n", "\n");
            var groupModel = SliceSource(model,
                "public class DanmuCompositeSeasonGroup",
                "public class DanmuCompositeEpisode");
            Assert(!groupModel.Contains("JsonIgnore") && !groupModel.Contains("IgnoreDataMember") &&
                   !groupModel.Contains("EmitDefaultValue") &&
                   groupModel.Contains("public double? MatchScore { get; set; }"),
                "Emby/ServiceStack must see a plain nullable score: mapped values serialize, temporary nulls use its default omission policy");
        }

        private static void PreservesSourceMetadataAcrossEveryBindingEntryPoint()
        {
            var metadataJson = System.Text.Json.JsonSerializer.Serialize(
                new SourceMetadata { Title = "Visible", Year = 2024, Category = "Anime" });
            Assert(!metadataJson.Contains("HasValue", StringComparison.OrdinalIgnoreCase),
                "SourceMetadata payloads must expose only title, year, and category");
            var fidelityProperty = typeof(DanmuMatchCandidate).GetProperty("FidelityTitleEvidence");
            Assert(fidelityProperty.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true).Any() &&
                   fidelityProperty.GetCustomAttributes(typeof(System.Runtime.Serialization.IgnoreDataMemberAttribute), true).Any(),
                "internal fidelity evidence must be hidden from both System.Text.Json and Emby/ServiceStack-style payload serializers");
            var metadata = new SourceMetadata { Title = "Upstream Season", Year = 2024, Category = "Anime" };
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID", MediaId = "upstream-season", MediaLookupId = "lookup-season",
            };
            var sourceEpisodes = new[]
            {
                Source("source-1", "comment-1", 1), Source("source-2", "comment-2", 2),
            };
            var owningLocal = LocalEpisodes(1, 2);
            owningLocal.ForEach(episode =>
            {
                episode.Ownership = CompositeSeasonOwnershipKind.Owning;
                episode.ParentSeasonNumber = 1;
            });

            Assert(CompositeSeasonPlanner.TryCreatePlan(owningLocal, null, out var automatic, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                automatic, source, sourceEpisodes, "automatic-primary", .95, "search-confidence", "auto-token",
                metadata, out automatic, out error), error);
            AssertMetadata(automatic, "automatic");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), null, out var manual, out error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(manual, new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = "local-1", RequestedEpisodeCount = 2, Source = source,
                SourceEpisodes = sourceEpisodes.ToList(), SourceStartEpisodeId = "source-1",
                Origin = "manual", SourceMetadata = metadata,
            }, out manual, out _, out error), error);
            AssertMetadata(manual, "manual");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), null, out var supplemental, out error), error);
            Assert(CompositeSeasonMatchService.TryNormalizeAndContinueSource(
                supplemental, source, sourceEpisodes, "automatic-residual", .9, "search-confidence", "supp-token",
                metadata, out supplemental, out _, out error), error);
            AssertMetadata(supplemental, "supplementary");

            var directMedia = new ScraperMedia
            {
                Id = source.MediaId, Title = metadata.Title, Year = metadata.Year, Category = metadata.Category,
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "source-1", CommentId = "comment-1", EpisodeNumber = 1 },
                },
            };
            var directMapping = CompositeSeasonMatchService.CreateDirectMapping(
                "local-1", source.ProviderId, directMedia, source.MediaLookupId);
            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 1), new[] { directMapping },
                out var direct, out error), error);
            AssertMetadata(direct, "direct temporary");

            void AssertMetadata(CompositeSeasonPlan plan, string entryPoint)
            {
                Assert(plan.Mappings.All(mapping => mapping.SourceMetadata?.Title == metadata.Title &&
                    mapping.SourceMetadata.Year == metadata.Year && mapping.SourceMetadata.Category == metadata.Category),
                    entryPoint + " mappings must preserve selected source metadata");
                var group = CompositeSeasonMatchService.ToGroups(plan, Enumerable.Empty<Episode>())
                    .Single(item => !item.IsTemporary);
                Assert(group.SourceMetadata?.Title == metadata.Title && group.SourceMetadata.Year == metadata.Year &&
                       group.SourceMetadata.Category == metadata.Category,
                    entryPoint + " segment-to-collection reconstruction must preserve source metadata");
            }
        }

        private static void ProjectsBoundedSourceEpisodeNamesWithoutChangingPlanAuthority()
        {
            var longTitle = "  " + new string('x',
                CompositeSeasonMatchService.MaximumSourceEpisodeNameLength + 37) + "  ";
            var resolvedMedia = new ScraperMedia
            {
                Id = "title-source",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode
                    {
                        Id = "title-source-1",
                        CommentId = "title-comment-1",
                        EpisodeNumber = 1,
                        Title = longTitle,
                    },
                    new ScraperEpisode
                    {
                        Id = "title-source-2",
                        CommentId = "title-comment-2",
                        EpisodeNumber = 2,
                        Title = "",
                    },
                },
            };
            var source = new CompositeSeasonSourceIdentity
            {
                ProviderId = "DandanID",
                MediaId = "title-source",
                MediaLookupId = "title-source-lookup",
            };
            var sourceEpisodes = CompositeSeasonMatchService.GetSourceEpisodes(resolvedMedia);
            var sourceEpisodeNames = CompositeSeasonMatchService.GetSourceEpisodeNames(resolvedMedia, source);
            Assert(sourceEpisodes.Count == 2 &&
                   sourceEpisodeNames[CompositeSeasonMatchService.GetSourceEpisodeNameKey(source, "title-source-1")].Length ==
                       CompositeSeasonMatchService.MaximumSourceEpisodeNameLength &&
                   sourceEpisodeNames[CompositeSeasonMatchService.GetSourceEpisodeNameKey(source, "title-source-1")]
                       .All(character => character == 'x') &&
                   sourceEpisodeNames[CompositeSeasonMatchService.GetSourceEpisodeNameKey(source, "title-source-2")] == string.Empty,
                "source Episode titles must be trimmed, bounded, and keep an empty-title fallback");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 2), null,
                out var plan, out var error), error);
            Assert(CompositeSeasonPlanner.TryApplySegment(plan,
                new CompositeSeasonSegmentRequest
                {
                    LocalStartEpisodeItemId = "local-1",
                    RequestedEpisodeCount = 2,
                    Source = source,
                    SourceEpisodes = sourceEpisodes,
                    SourceStartEpisodeId = "title-source-1",
                    Origin = "manual",
                }, out plan, out _, out error), error);
            Assert(plan.Mappings.All(mapping =>
                       typeof(CompositeSeasonEpisodeMapping).GetProperty("SourceEpisodeName") == null),
                "the authoritative mapping model must remain free of source display titles");

            var groups = CompositeSeasonMatchService.ToGroups(
                plan, Enumerable.Empty<Episode>(), sourceEpisodeNames);
            var mapped = groups.Single(group => !group.IsTemporary);
            Assert(mapped.Episodes[0].SourceEpisodeName == sourceEpisodeNames[
                       CompositeSeasonMatchService.GetSourceEpisodeNameKey(source, "title-source-1")] &&
                   mapped.Episodes[1].SourceEpisodeName == string.Empty &&
                   JsonSerializer.Serialize(mapped.Episodes[0]).Contains("\"SourceEpisodeName\"", StringComparison.Ordinal),
                "the public composite-group projection must expose the bounded source title without source identity fields");

            var context = new SeasonPlanningContext
            {
                SeriesId = "title-series",
                SeasonId = "title-season",
                TargetSeasonNumber = 1,
                StructureFingerprint = "title-structure",
            };
            var selection = new DanmuCompositeSeasonSelection
            {
                Site = source.ProviderId,
                CandidateId = source.MediaLookupId,
                LocalStartEpisodeItemId = "local-1",
                RequestedEpisodeCount = 2,
                SourceStartEpisodeId = "title-source-1",
                MatchOrigin = "manual",
                SelectionEvidenceToken = "title-evidence",
            };
            var baselineFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, plan);
            sourceEpisodeNames[CompositeSeasonMatchService.GetSourceEpisodeNameKey(
                source, "title-source-1")] = "different-display-title";
            var displayTitleChangedFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, new[] { selection }, plan);
            Assert(baselineFingerprint == displayTitleChangedFingerprint &&
                   typeof(DanmuCompositeSeasonSelection).GetProperty("SourceEpisodeName") == null &&
                   typeof(DanmuEpisodeDownloadResult).GetProperty("SourceEpisodeName") == null,
                "source display titles must not alter fingerprints, compact selections, or download task snapshots");
        }

        private static void DerivesSourceSurplusOnlyFromAppliedAuthoritativeDetails()
        {
            var predicate = typeof(DanmuController).GetMethod(
                "ShouldReportVerifiedSourceEpisodeSurplus",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert(predicate != null,
                "the controller must keep one closed predicate for authoritative source-surplus evidence");

            bool Reports(int authoritativeSourceCount, int localCount, int appliedCount) =>
                (bool)predicate.Invoke(null, new object[]
                {
                    authoritativeSourceCount, localCount, appliedCount,
                });

            var candidateOverstates = new DanmuMatchCandidate { EpisodeSize = 99 };
            var candidateUnderstates = new DanmuMatchCandidate { EpisodeSize = 1 };
            Assert(candidateOverstates.EpisodeSize > 3 && !Reports(2, 3, 2) &&
                   candidateUnderstates.EpisodeSize < 3 && Reports(4, 3, 3),
                "candidate EpisodeSize must not override the opposite count resolved from authoritative provider details");
            Assert(!Reports(4, 3, 0),
                "a source with no successfully applied mapping must never publish surplus state");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 3), null,
                out var localSmallerPlan, out var error), error);
            var longerSource = Enumerable.Range(1, 4)
                .Select(number => Source("long-source-" + number, "long-comment-" + number, number))
                .ToList();
            Assert(CompositeSeasonPlanner.TryApplySegment(localSmallerPlan,
                Segment("local-1", "DandanID", "long-source", "long-source-1", longerSource),
                out localSmallerPlan, out var localSmallerApplied, out error), error);
            Assert(localSmallerApplied == 3 && localSmallerPlan.Mappings.Count == 3 &&
                   localSmallerPlan.UnmatchedRuns.Count == 0 &&
                   Reports(longerSource.Count, 3, localSmallerApplied),
                "a verified longer source must remain mappable and publish the advisory state");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 3), null,
                out var equalPlan, out error), error);
            var equalSource = Enumerable.Range(1, 3)
                .Select(number => Source("equal-source-" + number, "equal-comment-" + number, number))
                .ToList();
            Assert(CompositeSeasonPlanner.TryApplySegment(equalPlan,
                Segment("local-1", "DandanID", "equal-source", "equal-source-1", equalSource),
                out equalPlan, out var equalApplied, out error), error);
            Assert(equalApplied == 3 && !Reports(equalSource.Count, 3, equalApplied),
                "equal authoritative source and local counts must not publish surplus state");

            Assert(CompositeSeasonPlanner.TryCreatePlan(LocalEpisodes(1, 4), null,
                out var localLargerPlan, out error), error);
            var shorterSource = Enumerable.Range(1, 3)
                .Select(number => Source("short-source-" + number, "short-comment-" + number, number))
                .ToList();
            Assert(CompositeSeasonPlanner.TryApplySegment(localLargerPlan,
                Segment("local-1", "DandanID", "short-source", "short-source-1", shorterSource),
                out localLargerPlan, out var localLargerApplied, out error), error);
            Assert(localLargerApplied == 3 && !Reports(shorterSource.Count, 4, localLargerApplied) &&
                   localLargerPlan.UnmatchedRuns.Count == 1 &&
                   RunIds(localLargerPlan.UnmatchedRuns[0]) == "local-4",
                "a shorter source must keep the established unmatched-run workflow without a surplus advisory");

            var firstIndependentSource = Reports(2, 3, 2);
            var secondIndependentSource = Reports(2, 3, 1);
            Assert(2 + 2 > 3 && !(firstIndependentSource || secondIndependentSource),
                "several source counts must be compared independently and never summed into synthetic surplus");

            Assert(typeof(DanmuSeasonMatchResult).GetProperty(
                       "HasVerifiedSourceEpisodeSurplus") != null &&
                   !new DanmuSeasonMatchResult().HasVerifiedSourceEpisodeSurplus &&
                   typeof(DanmuMatchCandidate).GetProperty(
                       "HasVerifiedSourceEpisodeSurplus") == null &&
                   typeof(DanmuCompositeSeasonSelection).GetProperty(
                       "HasVerifiedSourceEpisodeSurplus") == null &&
                   typeof(DanmuParams).GetProperty(
                       "HasVerifiedSourceEpisodeSurplus") == null,
                "verified source surplus must remain response-only and absent from candidates and requests");
            var responseJson = JsonSerializer.Serialize(new DanmuSeasonMatchResult
            {
                HasVerifiedSourceEpisodeSurplus = true,
            });
            Assert(responseJson.Contains(
                    "\"HasVerifiedSourceEpisodeSurplus\":true", StringComparison.Ordinal),
                "the authoritative Season response must project the advisory state on the wire");

            var repositoryRoot = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", ".."));
            var controller = File.ReadAllText(Path.Combine(
                repositoryRoot, "Core", "Controllers", "DanmuController.cs")).Replace("\r\n", "\n");
            var builder = SliceSource(controller,
                "private async Task<CompositePlanBuild> BuildCompositePlanAsync(",
                "private static DanmuCompositeSeasonSelection CloneSeasonPlanSelection(");
            Assert(builder.Contains(
                       "var sourceEpisodes = CompositeSeasonMatchService.GetSourceEpisodes(media);") &&
                   builder.Contains(
                       "sourceEpisodes.Count, context.LocalEpisodes.Count, appliedMappings.Count") &&
                   builder.Contains("hasVerifiedSourceEpisodeSurplus |=") &&
                   builder.Contains("plan.Mappings.Count > 0") &&
                   !builder.Contains("EpisodeSize"),
                "BuildCompositePlanAsync must derive the advisory only from applied authoritative detail Episodes");
            Assert(builder.IndexOf("build.HasVerifiedSourceEpisodeSurplus =", StringComparison.Ordinal) >
                   builder.IndexOf("build.Plan = plan;", StringComparison.Ordinal),
                "failed, cancelled, or zero-plan builds must retain the response default false");

            var compositePreview = SliceSource(controller,
                "private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(",
                "private async Task PopulateCompositePreviewIfRequired(");
            var populatedPreview = SliceSource(controller,
                "private async Task PopulateCompositePreviewIfRequired(",
                "private static bool IsRematch(");
            Assert(compositePreview.Contains(
                       "response.HasVerifiedSourceEpisodeSurplus = build.HasVerifiedSourceEpisodeSurplus;") &&
                   compositePreview.IndexOf(
                       "response.HasVerifiedSourceEpisodeSurplus =", StringComparison.Ordinal) >
                   compositePreview.IndexOf("if (build.Plan == null)", StringComparison.Ordinal) &&
                   populatedPreview.Contains(
                       "result.HasVerifiedSourceEpisodeSurplus = direct.HasVerifiedSourceEpisodeSurplus;") &&
                   populatedPreview.Contains(
                       "result.HasVerifiedSourceEpisodeSurplus = build.HasVerifiedSourceEpisodeSurplus;"),
                "whole-Series, single-Season, and explicit plan rebuild responses must project the current authoritative state");
        }

        private static string MappingSnapshot(CompositeSeasonEpisodeMapping mapping)
        {
            return string.Join("|", new[]
            {
                mapping?.LocalEpisodeItemId ?? string.Empty,
                mapping?.Source?.ProviderId ?? string.Empty,
                mapping?.Source?.MediaId ?? string.Empty,
                mapping?.Source?.MediaLookupId ?? string.Empty,
                mapping?.SourceEpisodeId ?? string.Empty,
                mapping?.CommentId ?? string.Empty,
                mapping?.SourceEpisodeNumber?.ToString() ?? string.Empty,
                mapping?.Origin ?? string.Empty,
            });
        }

        private static string SliceSource(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert(start >= 0 && end > start,
                "source-contract markers must remain discoverable: " + startMarker + " -> " + endMarker);
            return source.Substring(start, end - start);
        }

        private static List<CompositeSeasonLocalEpisode> LocalEpisodes(int first, int last) =>
            Enumerable.Range(first, last - first + 1).Select(number => new CompositeSeasonLocalEpisode
            {
                ItemId = "local-" + number, EpisodeNumber = number, SortOrder = number,
            }).ToList();

        private static CompositeSeasonEpisodeMapping Mapping(string local, string provider, string media, string source, string comment) =>
            new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = local,
                Source = new CompositeSeasonSourceIdentity { ProviderId = provider, MediaId = media },
                SourceEpisodeId = source, CommentId = comment, Origin = "direct",
            };

        private static CompositeSeasonEpisodeMapping DirectDandanMapping(int localEpisodeNumber, string parentMediaId,
            string episodeId) =>
            new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = "local-" + localEpisodeNumber,
                Source = new CompositeSeasonSourceIdentity
                {
                    ProviderId = "DandanID",
                    MediaId = parentMediaId,
                    MediaLookupId = episodeId,
                },
                SourceEpisodeId = episodeId,
                CommentId = "chat-" + episodeId,
                SourceEpisodeNumber = localEpisodeNumber <= 28 ? localEpisodeNumber : localEpisodeNumber - 28,
                Origin = "episode-provider-id",
            };

        private static CompositeSeasonSegmentRequest Segment(
            string local, string provider, string media, string sourceStart,
            IEnumerable<CompositeSeasonSourceEpisode> sources, int count = 0) =>
            new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = local, RequestedEpisodeCount = count,
                Source = new CompositeSeasonSourceIdentity { ProviderId = provider, MediaId = media },
                SourceStartEpisodeId = sourceStart, SourceEpisodes = sources.ToList(),
            };

        private static CompositeSeasonSourceEpisode Source(string id, string comment, int number) =>
            new CompositeSeasonSourceEpisode { EpisodeId = id, CommentId = comment, EpisodeNumber = number };

        private static CompositeSeasonSourceEpisode SourceNullable(
            string id, string comment, int? number, int ordinal) =>
            new CompositeSeasonSourceEpisode
            {
                EpisodeId = id,
                CommentId = comment,
                EpisodeNumber = number,
                SourceOrdinal = ordinal,
            };

        private static string RunIds(CompositeSeasonUnmatchedRun run) =>
            string.Join(",", run.Episodes.Select(episode => episode.ItemId));

        private static void AwaitWithWatchdog(Task task, string operation)
        {
            if (Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult() != task)
            {
                throw new InvalidOperationException("Regression watchdog expired: " + operation);
            }

            task.GetAwaiter().GetResult();
        }

        private static TResult AwaitWithWatchdog<TResult>(Task<TResult> task, string operation)
        {
            AwaitWithWatchdog((Task)task, operation);
            return task.GetAwaiter().GetResult();
        }

        private static int Count(string value, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class DirectEpisodeFakeScraper : AbstractScraper
        {
            private readonly string _providerId;
            private readonly ScraperEpisode _episode;

            public DirectEpisodeFakeScraper(string providerId, ScraperEpisode episode) : base(null)
            {
                _providerId = providerId;
                _episode = episode;
            }

            public int MediaCalls { get; private set; }
            public int MediaEpisodeCalls { get; private set; }
            public override string Name => _providerId;
            public override string ProviderName => _providerId;
            public override string ProviderId => _providerId;
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<string> SearchMediaId(BaseItem item) => Task.FromResult(string.Empty);
            public override Task<List<ScraperSearchInfo>> SearchForApi(string keyword) =>
                Task.FromResult(new List<ScraperSearchInfo>());
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id)
            {
                MediaCalls++;
                return Task.FromResult<ScraperMedia>(null);
            }
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id)
            {
                MediaEpisodeCalls++;
                return Task.FromResult(_episode);
            }
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId) =>
                Task.FromResult<ScraperDanmaku>(null);
        }
    }
}
