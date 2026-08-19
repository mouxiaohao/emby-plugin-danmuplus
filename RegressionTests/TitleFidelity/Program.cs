using System;
using System.Collections.Generic;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.TitleFidelityTests
{
    internal static class Program
    {
        private static int Main()
        {
            NormalizesCompatibilityVariantsWithoutSymbolRules();
            PreservesArbitrarySymbolSequences();
            KeepsLooseNormalizationAndMismatchScoringUnchanged();
            AppliesEvidenceThroughMoviePrimaryAndSeasonParentPaths();
            AppliesStrictCompleteTitleSeasonFallback();
            SelectsStudentCouncilStarAndPreservesTrueTies();
            KeepsSubThresholdFidelityAtOrdinaryConfidence();
            PreservesThresholdTieProviderAliasAndMovieBoundaries();
            SeparatesSeasonAndParentAliasRoles();
            Console.WriteLine("Title fidelity regression checks passed.");
            return 0;
        }

        private static void NormalizesCompatibilityVariantsWithoutSymbolRules()
        {
            var equivalentPairs = new[]
            {
                ("Title\uFF0A", "title*"),
                ("Ａ Ｂ！", "a b!"),
                ("Name\u3000#", "name #"),
            };

            foreach (var pair in equivalentPairs)
            {
                Assert(
                    DanmuMatchScorer.NormalizeFidelity(pair.Item1) ==
                    DanmuMatchScorer.NormalizeFidelity(pair.Item2),
                    "NFKC, case, and whitespace normalization should be generic");
            }
        }

        private static void PreservesArbitrarySymbolSequences()
        {
            var values = new[]
            {
                "Title!",
                "Title!!",
                "Title!?",
                "Title\u27E1!?",
                "Title\u27E1?!",
                "Title\u2606\u2234",
            };
            var fidelityForms = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var fidelity = DanmuMatchScorer.NormalizeFidelity(value);
                Assert(fidelityForms.Add(fidelity),
                    "fidelity must distinguish arbitrary symbol type, count, and order: " + value);
                Assert(fidelity.Length > DanmuMatchScorer.NormalizeFidelity("Title").Length,
                    "one-character and symbol-only suffixes must survive fidelity normalization");
            }

            Assert(DanmuMatchScorer.Normalize("Title!") == DanmuMatchScorer.Normalize("Title\u27E1?!"),
                "loose normalization must retain its existing punctuation/symbol folding");
        }

        private static void KeepsLooseNormalizationAndMismatchScoringUnchanged()
        {
            var matched = ScoreMovie("matched", "Title!?", "Title!?", 2024);
            var mismatched = ScoreMovie("mismatched", "Title!?", "Title!!", 2024);
            var markerless = ScoreMovie("markerless", "Title!?", "Title", 2024);

            Assert(matched.Score == mismatched.Score && mismatched.Score == markerless.Score,
                "fidelity mismatch must not subtract from the unchanged loose score");
            Assert(matched.TitleScore == mismatched.TitleScore && mismatched.TitleScore == markerless.TitleScore,
                "fidelity mismatch must not alter loose title evidence");
            Assert(matched.FidelityTitleEvidence > 0 &&
                   mismatched.FidelityTitleEvidence == 0 &&
                   markerless.FidelityTitleEvidence == 0,
                "only exact fidelity equality should add positive evidence");
        }

        private static void AppliesEvidenceThroughMoviePrimaryAndSeasonParentPaths()
        {
            var movie = ScoreMovie("movie", "Alias\uFF0A", "Alias*", 2024);
            Assert(movie.FidelityTitleEvidence == 1,
                "the production Movie primary-title path should use fidelity evidence");

            var sourceAlias = DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo
                {
                    Id = "source-alias",
                    Name = "Localized",
                    Category = "movie",
                    Year = 2024,
                    Aliases = new List<string> { "Alias*" },
                },
                "BilibiliID", "Bilibili", 0, "Alias\uFF0A", 2024);
            Assert(sourceAlias.FidelityTitleEvidence == 1,
                "a provider alias carried by ScraperSearchInfo must use the production fidelity channel");
            Assert(sourceAlias.TitleScore == 1 && sourceAlias.Score >= 0.90,
                "a provider alias must participate in loose Movie base scoring, not only fidelity tie-breaking");

            var localAlias = DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo
                {
                    Id = "local-alias",
                    Name = "Alias*",
                    Category = "movie",
                    Year = 2024,
                },
                "BilibiliID", "Bilibili", 0, "Localized", 2024,
                new[] { "Alias\uFF0A" });
            Assert(localAlias.FidelityTitleEvidence == 1,
                "a local OriginalTitle-style alias must use the same fidelity channel");
            Assert(localAlias.TitleScore == 1 && localAlias.Score >= 0.90,
                "a local alias must participate in loose Movie base scoring");

            var seasonAliasSource = new ScraperSearchInfo
            {
                Id = "season-source-alias",
                Name = "Unrelated localized title",
                Category = "anime",
                Year = 2024,
                EpisodeSize = 13,
                Aliases = new List<string> { "Parent Alias*" },
            };
            Assert(DanmuMatchScorer.IsEligibleSeasonCandidate(
                    seasonAliasSource, "Parent Alias\uFF0A", "Season 2", null) &&
                   DanmuMatchScorer.Score(seasonAliasSource, "BilibiliID", "Bilibili", 0,
                       "Parent Alias\uFF0A", "Season 2", 2024, 13).ParentTitleScore == 1,
                "a source alias must participate in Season loose eligibility and base scoring");

            var season = DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = "season",
                    Name = "Unrelated display title",
                    Category = "anime",
                    Year = 2024,
                    EpisodeSize = 13,
                    SourceMetadata = new SourceMetadata
                    {
                        Title = "Parent\u27E1!?",
                        Year = 2024,
                        Category = "anime",
                    },
                },
                "DandanID",
                "DandanPlay",
                0,
                "Parent\u27E1!?",
                "Season 2",
                2024,
                13);
            Assert(season.FidelityTitleEvidence == 1,
                "the applicable source parent-media title should use the same fidelity channel");
            Assert(season.SourceMetadata != null && season.SourceMetadata.Title == "Parent\u27E1!?",
                "scoring should retain a cloned source metadata value");
        }

        private static void AppliesStrictCompleteTitleSeasonFallback()
        {
            const string parent = "妄想学生会";
            const string localSeason = "妄想学生会＊";
            var markerless = ScoreStrictSeason(
                "markerless-s2", parent, localSeason, parent, 2);
            var starred = ScoreStrictSeason(
                "starred-s2", parent, localSeason, "妄想学生会*", 2);
            var differentSymbol = ScoreStrictSeason(
                "different-symbol-s2", parent, localSeason, "妄想学生会!", 2);
            var differentCount = ScoreStrictSeason(
                "different-count-s2", parent, localSeason, "妄想学生会**", 2);
            var differentStar = ScoreStrictSeason(
                "different-star-s2", parent, localSeason, "妄想学生会★", 2);

            Assert(starred.ParentTitleScore == 1 && starred.KeywordScore == 1 &&
                   starred.YearScore == 1 && starred.Score == 1 &&
                   markerless.KeywordScore == 0 && markerless.Score == 0.80 &&
                   differentSymbol.KeywordScore == 0 && differentSymbol.Score == 0.80 &&
                   differentCount.KeywordScore == 0 && differentCount.Score == 0.80 &&
                   differentStar.KeywordScore == 0 && differentStar.Score == 0.80,
                "only NFKC-equivalent complete Season titles may recover the existing 20-point Season component");
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[]
                   {
                       markerless, differentSymbol, differentCount, differentStar, starred,
                   }) == starred,
                "妄想学生会＊/* must be the unique ordinary automatic candidate while markerless and mismatched symbols remain at 80");

            var sourceRemainderOnly = ScoreStrictSeason(
                "source-remainder", parent, localSeason, "妄想学生会*续篇", 2);
            var localRemainderOnly = ScoreStrictSeason(
                "local-remainder", parent, "妄想学生会续篇＊", "妄想学生会*", 2);
            var bothRemainders = ScoreStrictSeason(
                "both-remainders", parent, "妄想学生会续篇＊", "妄想学生会续篇*", 2);
            Assert(sourceRemainderOnly.KeywordScore == 0 &&
                   localRemainderOnly.KeywordScore == 0,
                "strict complete-title fallback must stay disabled when only one loose parent-stripped remainder is empty");
            Assert(bothRemainders.KeywordScore == 1 && bothRemainders.Score == 1,
                "when both residuals are non-empty, the existing exact residual score must remain one and must not receive an additive fallback bonus");

            var parentOnly = ScoreStrictSeason(
                "parent-only", localSeason, localSeason, "妄想学生会*", 2);
            var localParentAlias = ScoreStrictSeason(
                "local-parent-alias-guard", parent, localSeason, "妄想学生会*", 2,
                new[] { localSeason });
            Assert(parentOnly.KeywordScore == 0 && parentOnly.Score == 0.80 &&
                   localParentAlias.KeywordScore == 0 && localParentAlias.Score == 0.80,
                "a complete local Season equal to the primary or local parent identity must not count the parent twice");

            var parentPunctuation = ScoreStrictSeason(
                "parent-punctuation", "妄想学生会！", localSeason, "妄想学生会*", 2);
            var kOn = ScoreStrictSeason(
                "k-on", "K-ON!", "K-ON!!", "K-ON！！", 2);
            Assert(parentPunctuation.KeywordScore == 1 && parentPunctuation.Score == 1 &&
                   kOn.ParentTitleScore == 1 && kOn.KeywordScore == 1 && kOn.Score == 1,
                "parent punctuation must remain fidelity-sensitive, including K-ON! to NFKC-equivalent K-ON!!");

            var conflictingMarker = ScoreStrictSeason(
                "conflicting-marker", "妄想学生会 第三季",
                "妄想学生会 第三季＊", "妄想学生会 第三季*", 2);
            Assert(conflictingMarker.ParentTitleScore == 1 &&
                   conflictingMarker.KeywordScore == 0 && conflictingMarker.Score == 0.80,
                "a conflicting explicit Season marker must reject strict complete-title recovery before fidelity equality");

            var crossChannel = DanmuMatchScorer.Score(new ScraperSearchInfo
            {
                Id = "cross-channel",
                Name = "本地父剧",
                Category = "anime",
                Year = 2014,
                EpisodeSize = 13,
                SourceMetadata = new SourceMetadata { Title = "英文父剧*" },
            }, "DandanID", "DandanPlay", 0, "本地父剧", "英文父剧＊", 2014, 13,
                new[] { "英文父剧" }, null, true, 2);
            Assert(crossChannel.ParentTitleScore == 1 && crossChannel.KeywordScore == 0 &&
                   crossChannel.Score == 0.80,
                "parent evidence from one source-title item must not combine with strict equality from another item");

            var firstSeason = ScoreStrictSeason(
                "first-season", parent, parent, parent, 1);
            Assert(firstSeason.ParentTitleScore == 1 && firstSeason.KeywordScore == 1 &&
                   firstSeason.Score == 1,
                "the pre-existing Season 1 empty-remainder rule must run before the strict fallback");

            var movie = ScoreMovie(
                "strict-fallback-movie", localSeason, "妄想学生会*", 2014);
            Assert(movie.TitleScore == 1 && movie.YearScore == 1 && movie.Score == 1,
                "the strict complete-title Season fallback must not change Movie scoring");
        }

        private static void SelectsStudentCouncilStarAndPreservesTrueTies()
        {
            const string localTitle = "\u5984\u60F3\u5B66\u751F\u4F1A\uFF0A";
            var markerless = ScoreSeason("markerless", localTitle, "\u5984\u60F3\u5B66\u751F\u4F1A");
            var starred = ScoreSeason("starred", localTitle, "\u5984\u60F3\u5B66\u751F\u4F1A*");

            Assert(markerless.Score == starred.Score && markerless.Score >= 0.90,
                "the motivating candidates should retain equal loose confidence");
            Assert(markerless.FidelityTitleEvidence == 0 && starred.FidelityTitleEvidence == 2,
                "NFKC-compatible starred fidelity should be the sole extra evidence");
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { markerless, starred }) == starred,
                "fidelity evidence should make the starred candidate uniquely highest");

            var firstTie = ScoreSeason("first", localTitle, "\u5984\u60F3\u5B66\u751F\u4F1A*");
            var secondTie = ScoreSeason("second", localTitle, "\u5984\u60F3\u5B66\u751F\u4F1A\uFF0A");
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { firstTie, secondTie }) == null &&
                   DanmuMatchScorer.SelectAutoCandidate(new[] { secondTie, firstTie }) == null,
                "a genuine same-provider evidence tie must remain ambiguous regardless of source order");
        }

        private static void KeepsSubThresholdFidelityAtOrdinaryConfidence()
        {
            const string localSeries = "妄想学生会";
            const string localSeason = "妄想学生会＊";
            var markerlessScored = ScoreSeasonWithRoles(
                "markerless-scored", localSeries, localSeason, "妄想学生会");
            var starredScored = ScoreSeasonWithRoles(
                "starred-scored", localSeries, localSeason, "妄想学生会*");
            Assert(markerlessScored.FidelityTitleEvidence == 1 &&
                   starredScored.FidelityTitleEvidence == 2,
                "the live-equivalent parent match must rank below the exact Season title match");

            var markerless = Candidate("markerless", 0.85, 1);
            var starred = Candidate("starred", 0.85, 2);
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { markerless, starred }) == null &&
                   markerless.MatchScore == 0.85 && starred.MatchScore == 0.85,
                "a unique rank-2 Season exact match at 0.85 must remain below automatic confidence");
            Assert(DanmuMatchScorer.GetEffectiveConfidence(starred, new[] { markerless, starred }) == 0.85 &&
                   typeof(DanmuMatchScorer).GetField("FidelityBridgeBaseFloor") == null &&
                   typeof(DanmuMatchScorer).GetField("FidelityBridgeBonus") == null,
                "no fidelity bridge constant or effective-confidence promotion may remain");

            var nearThreshold = Candidate("near-threshold", 0.89, 2);
            var nearThresholdPeer = Candidate("near-threshold-peer", 0.89, 0);
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { nearThresholdPeer, nearThreshold }) == null &&
                   nearThreshold.MatchScore == 0.89 && nearThresholdPeer.MatchScore == 0.89,
                "fidelity must not promote an ordinary 0.89 score to the 0.90 threshold");

            var firstExact = Candidate("first-exact", 0.90, 2);
            var secondExact = Candidate("second-exact", 0.90, 2);
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { firstExact, secondExact }) == null &&
                   DanmuMatchScorer.SelectAutoCandidate(new[] { secondExact, firstExact }) == null &&
                   firstExact.MatchScore == 0.90 && secondExact.MatchScore == 0.90,
                "same-score candidates with the same fidelity must remain ambiguous regardless of input order");

            var seasonExact = Candidate("season-exact", 0.90, 2);
            var parentExact = Candidate("parent-exact", 0.90, 1);
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { parentExact, seasonExact }) == seasonExact &&
                   seasonExact.MatchScore == 0.90 && parentExact.MatchScore == 0.90,
                "at the ordinary threshold, stronger fidelity must still resolve an equal-score tie");
        }

        private static void PreservesThresholdTieProviderAliasAndMovieBoundaries()
        {
            var alreadyConfident = Candidate("already-confident", 0.98, 2);
            var confidentPeer = Candidate("confident-peer", 0.98, 0);
            Assert(DanmuMatchScorer.GetEffectiveConfidence(
                       alreadyConfident, new[] { alreadyConfident, confidentPeer }) == 0.98,
                "fidelity must not boost a candidate that already clears the threshold");

            var higherExisting = Candidate("higher-existing", 0.99, 0);
            Assert(DanmuMatchScorer.SelectAutoCandidate(
                       new[] { alreadyConfident, confidentPeer, higherExisting }) == higherExisting,
                "rank-2 evidence at 0.98 must not overtake an existing 0.99 candidate");

            var fidelityAtThreshold = Candidate("fidelity-at-threshold", 0.90, 2);
            var ordinaryHigher = Candidate("ordinary-higher", 0.91, 0);
            Assert(DanmuMatchScorer.SelectAutoCandidate(
                       new[] { fidelityAtThreshold, ordinaryHigher }) == ordinaryHigher &&
                   fidelityAtThreshold.MatchScore == 0.90 && ordinaryHigher.MatchScore == 0.91,
                "an ordinary 0.91 score must beat 0.90 fidelity evidence");

            var preferredThreshold = Candidate("preferred-threshold", 0.90, 2, 0);
            var preferredPeer = Candidate("preferred-peer", 0.90, 0, 0);
            var lowerProvider = Candidate("lower-provider", 0.99, 0, 1);
            Assert(DanmuMatchScorer.SelectAutoCandidate(
                       new[] { lowerProvider, preferredPeer, preferredThreshold }) == preferredThreshold,
                "provider priority must still apply after effective-confidence filtering");

            var alias = Candidate("tmdb-alias", 0.80, 0);
            alias.MatchOrigin = "tmdb-alias";
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { alias }) == alias &&
                   alias.MatchScore == 0.80,
                "the independent TMDB alias threshold must remain 0.80");

            var movieExact = Candidate("movie-exact", 0.85, 1);
            var moviePeer = Candidate("movie-peer", 0.85, 0);
            Assert(DanmuMatchScorer.SelectAutoCandidate(new[] { movieExact, moviePeer }) == null &&
                   movieExact.MatchScore == 0.85 && moviePeer.MatchScore == 0.85,
                "Movie fidelity at 0.85 must remain below automatic confidence");
        }

        private static void SeparatesSeasonAndParentAliasRoles()
        {
            var sourceAlias = DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = "source-season-alias",
                    Name = "Localized",
                    Category = "anime",
                    Year = 2024,
                    EpisodeSize = 12,
                    Aliases = new List<string> { "Target Season*" },
                },
                "DandanID", "DandanPlay", 0, "Target", "Target Season＊", 2024, 12);
            Assert(sourceAlias.FidelityTitleEvidence == 2,
                "a source alias matching the local Season title must retain rank 2");

            var localSeasonAlias = DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = "local-season-alias",
                    Name = "Target Season*",
                    Category = "anime",
                    Year = 2024,
                    EpisodeSize = 12,
                },
                "DandanID", "DandanPlay", 0, "Target", "Localized Season", 2024, 12,
                null, new[] { "Target Season＊" });
            Assert(localSeasonAlias.FidelityTitleEvidence == 2,
                "Season.OriginalTitle must remain a rank-2 local Season alias");

            var localParentAlias = DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = "local-parent-alias",
                    Name = "Target Parent*",
                    Category = "anime",
                    Year = 2024,
                    EpisodeSize = 12,
                },
                "DandanID", "DandanPlay", 0, "Localized Parent", "Season 2", 2024, 12,
                new[] { "Target Parent＊" }, null);
            var noLocalParentAlias = DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = "no-local-parent-alias",
                    Name = "Target Parent*",
                    Category = "anime",
                    Year = 2024,
                    EpisodeSize = 12,
                },
                "DandanID", "DandanPlay", 0, "Localized Parent", "Season 2", 2024, 12);
            Assert(localParentAlias.FidelityTitleEvidence == 1 &&
                   localParentAlias.ParentTitleScore == noLocalParentAlias.ParentTitleScore &&
                   localParentAlias.ParentTitleScore > 0 && localParentAlias.ParentTitleScore < 1,
                "Series.OriginalTitle may remain rank-1 fidelity evidence but ordinary parent scoring must use only the authoritative title's continuous evidence");

            var genericSeason = ScoreSeasonWithRoles(
                "generic-season", "Target Parent*", "Season 2", "Season 2");
            Assert(genericSeason.FidelityTitleEvidence == 0,
                "a generic Season label must never create rank-2 fidelity evidence");

            var punctuationMismatch = ScoreSeasonWithRoles(
                "punctuation-mismatch", "Target", "Target!?", "Target!!");
            Assert(punctuationMismatch.FidelityTitleEvidence == 0,
                "a different arbitrary punctuation sequence must not receive positive evidence");
        }

        private static DanmuMatchCandidate ScoreMovie(
            string id,
            string localTitle,
            string sourceTitle,
            int year)
        {
            return DanmuMatchScorer.ScoreMovie(
                new ScraperSearchInfo
                {
                    Id = id,
                    Name = sourceTitle,
                    Category = "movie",
                    Year = year,
                },
                "BilibiliID",
                "Bilibili",
                0,
                localTitle,
                year);
        }

        private static DanmuMatchCandidate ScoreSeason(string id, string localTitle, string sourceTitle)
        {
            return DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = id,
                    Name = sourceTitle,
                    Category = "anime",
                    Year = 2014,
                    EpisodeSize = 13,
                },
                "DandanID",
                "DandanPlay",
                0,
                localTitle,
                localTitle,
                2014,
                13,
                null,
                null,
                true,
                1);
        }

        private static DanmuMatchCandidate ScoreSeasonWithRoles(
            string id,
            string localSeries,
            string localSeason,
            string sourceTitle)
        {
            return DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = id,
                    Name = sourceTitle,
                    Category = "anime",
                    Year = 2014,
                    EpisodeSize = 13,
                },
                "DandanID", "DandanPlay", 0,
                localSeries, localSeason, 2014, 13);
        }

        private static DanmuMatchCandidate ScoreStrictSeason(
            string id,
            string localSeries,
            string localSeason,
            string sourceTitle,
            int expectedSeasonNumber,
            IEnumerable<string> localSeriesAliases = null)
        {
            return DanmuMatchScorer.Score(
                new ScraperSearchInfo
                {
                    Id = id,
                    Name = sourceTitle,
                    Category = "anime",
                    Year = 2014,
                    EpisodeSize = 13,
                },
                "DandanID", "DandanPlay", 0,
                localSeries, localSeason, 2014, 13,
                localSeriesAliases, null, true, expectedSeasonNumber);
        }

        private static DanmuMatchCandidate Candidate(
            string id,
            double score,
            int fidelity,
            int sourceOrder = 0)
        {
            return new DanmuMatchCandidate
            {
                Id = id,
                Site = "DandanID",
                SourceOrder = sourceOrder,
                Score = score,
                MatchScore = score,
                FidelityTitleEvidence = fidelity,
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
