using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;
using Emby.Plugin.Danmu.Scraper.Dandan;
using BilibiliMedia = Emby.Plugin.Danmu.Scraper.Bilibili.Entity.Media;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class Program
    {
        private static int Main()
        {
            MapsAnimeSeason();
            MapsLiveActionSeasonAndCleansTitle();
            UsesIdentifierFallbackOrder();
            OmitsMalformedRecords();
            OrdersAndSelectsCrossProviderTies();
            PreservesSameProviderTieAmbiguity();
            DeserializesAndNormalizesBilibiliEpisodes();
            ClassifiesOnlyExplicitNonMainTitles();
            ResolvesDandanCredentialsByCompletePair();
            RejectsIncompleteDandanCredentialsWithoutLeakingValues();
            EmbedsDandanCredentialSettings();
            Console.WriteLine("Danmu plugin regression checks passed.");
            return 0;
        }

        private static void MapsAnimeSeason()
        {
            var source = new BilibiliMedia
            {
                SeasonId = 46089,
                MediaId = 21087073,
                Title = "<em class=\"keyword\">葬送的芙莉莲</em>",
                SeasonTypeName = "番剧",
                PubDate = "2023-09-29 18:00:00",
                EpisodeSize = 28
            };

            Assert(BilibiliSearchResultMapper.TryMap(source, out var result, out _), "anime should map");
            Assert(result.Id == "46089", "anime should use season_id");
            Assert(result.Name == "葬送的芙莉莲", "highlight markup should be removed");
            Assert(result.Year == 2023 && result.EpisodeSize == 28 && result.Category == "番剧", "anime metadata should be preserved");
        }

        private static void MapsLiveActionSeasonAndCleansTitle()
        {
            var source = new BilibiliMedia
            {
                SeasonId = 34793,
                Title = "<em class=\"keyword\">半泽直树</em>",
                SeasonTypeName = "电视剧",
                PubTime = 1373126400,
                EpisodeSize = 10
            };

            Assert(BilibiliSearchResultMapper.TryMap(source, out var result, out _), "live-action season should map");
            Assert(result.Id == "34793" && result.Name == "半泽直树", "live-action identity should be preserved");
            Assert(result.Year == 2013 && result.EpisodeSize == 10 && result.Category == "电视剧", "live-action metadata should be preserved");
        }

        private static void UsesIdentifierFallbackOrder()
        {
            var source = new BilibiliMedia { SeasonId = 7, PgcSeasonId = 8, MediaId = 9, Title = "A" };
            Assert(BilibiliSearchResultMapper.ResolveId(source) == 7, "season_id should win");
            source.SeasonId = 0;
            Assert(BilibiliSearchResultMapper.ResolveId(source) == 8, "pgc_season_id should be the second choice");
            source.PgcSeasonId = 0;
            Assert(BilibiliSearchResultMapper.ResolveId(source) == 9, "media_id should be the final positive fallback");
        }

        private static void OmitsMalformedRecords()
        {
            Assert(!BilibiliSearchResultMapper.TryMap(new BilibiliMedia { Title = "No ID" }, out _, out _), "record without identifier should be omitted");
            Assert(!BilibiliSearchResultMapper.TryMap(new BilibiliMedia { SeasonId = 1, Title = " " }, out _, out _), "record without title should be omitted");
        }

        private static void OrdersAndSelectsCrossProviderTies()
        {
            var candidates = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("later", "LaterSite", 2, 0.90),
                Candidate("priority", "PrioritySite", 0, 0.90),
                Candidate("middle", "MiddleSite", 1, 0.82)
            });

            Assert(candidates[0].Id == "priority", "configured provider priority should order exact-score ties");
            Assert(DanmuMatchScorer.CanAutoSelect(candidates), "a unique candidate on the highest-priority tied provider should auto-bind");
            Assert(!DanmuMatchScorer.CanAutoSelect(candidates, false), "a priority tie should not stop parent-title search before fallback rounds");

            var unequal = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("higher-score", "LaterSite", 5, 0.91),
                Candidate("higher-priority", "PrioritySite", 0, 0.90)
            });
            Assert(unequal[0].Id == "higher-score", "provider priority must not outrank a higher score");
        }

        private static void PreservesSameProviderTieAmbiguity()
        {
            var candidates = DanmuMatchSearchEngine.OrderCandidates(new List<DanmuMatchCandidate>
            {
                Candidate("a", "PrioritySite", 0, 0.90),
                Candidate("b", "PrioritySite", 0, 0.90),
                Candidate("c", "LaterSite", 1, 0.90)
            });
            Assert(!DanmuMatchScorer.CanAutoSelect(candidates), "multiple top candidates within the highest-priority provider must remain ambiguous");
        }

        private static void DeserializesAndNormalizesBilibiliEpisodes()
        {
            const string json = "{\"season_id\":46089,\"episodes\":[" +
                                "{\"id\":101,\"title\":\"1\",\"long_title\":\"第1集预告\",\"badge\":\"预告\",\"badge_type\":1,\"section_type\":1,\"duration\":35000}," +
                                "{\"id\":201,\"title\":\"1\",\"long_title\":\"冒险开始\",\"badge_type\":0,\"section_type\":0,\"duration\":1500000}," +
                                "{\"id\":102,\"title\":\"2\",\"badge\":\"预告\",\"badge_type\":1,\"section_type\":1,\"duration\":40000}," +
                                "{\"id\":202,\"title\":\"2\",\"badge_type\":0,\"section_type\":0,\"duration\":1490000}]}";
            var season = JsonSerializer.Deserialize<VideoSeason>(json);
            Assert(season.SeasonId == 46089 && season.Episodes[0].SectionType == 1, "underscored Bilibili fields should deserialize explicitly");

            var normalized = BilibiliEpisodeNormalizer.Normalize(season.Episodes);
            Assert(normalized.Count == 2, "interleaved previews should be removed");
            Assert(normalized[0].Id == 201 && normalized[1].Id == 202, "full episodes should retain canonical numeric order");
        }

        private static void ClassifiesOnlyExplicitNonMainTitles()
        {
            Assert(EpisodeContentClassifier.IsExplicitNonMain("【预告】第8集"), "explicit preview marker should be excluded");
            Assert(EpisodeContentClassifier.IsExplicitNonMain("第8集预告"), "a Chinese preview suffix should be excluded");
            Assert(EpisodeContentClassifier.IsExplicitNonMain("PV 01"), "explicit PV marker should be excluded");
            Assert(!EpisodeContentClassifier.IsExplicitNonMain("第1集"), "ordinary episode title should remain");
            Assert(!EpisodeContentClassifier.IsExplicitNonMain("PVZ大战"), "PV letters inside a word should not be treated as a marker");
        }

        private static void ResolvesDandanCredentialsByCompletePair()
        {
            var configured = DandanCredentialResolver.Resolve(
                " configured-id ", " configured-secret ",
                "environment-id", "environment-secret",
                "legacy-id", "legacy-secret");
            Assert(configured.ApiId == "configured-id" && configured.ApiSecret == "configured-secret",
                "configured Dandan credentials should win and be trimmed");
            Assert(configured.Source == "插件配置", "credential source should identify plugin configuration");

            var environment = DandanCredentialResolver.Resolve(
                "", "", " environment-id ", " environment-secret ", "legacy-id", "legacy-secret");
            Assert(environment.ApiId == "environment-id" && environment.Source == "环境变量",
                "environment pair should be used when configuration is empty");

            var legacy = DandanCredentialResolver.Resolve("", "", "", "", " legacy-id ", " legacy-secret ");
            Assert(legacy.ApiId == "legacy-id" && legacy.Source == "内置配置",
                "legacy pair should remain a final compatibility fallback");
        }

        private static void RejectsIncompleteDandanCredentialsWithoutLeakingValues()
        {
            var message = CaptureCredentialError("LEAK_ID", "", "environment-id", "environment-secret");
            Assert(message.Contains("不完整"), "partial configured credentials should report an incomplete pair");
            Assert(!message.Contains("LEAK_ID"), "credential errors must not include the API ID");

            message = CaptureCredentialError("", "LEAK_SECRET", "environment-id", "environment-secret");
            Assert(!message.Contains("LEAK_SECRET"), "credential errors must not include the API Secret");

            message = CaptureCredentialError("", "", "", "");
            Assert(message.Contains("缺少"), "an empty credential chain should report missing credentials");
        }

        private static string CaptureCredentialError(
            string configuredId,
            string configuredSecret,
            string environmentId,
            string environmentSecret)
        {
            try
            {
                DandanCredentialResolver.Resolve(
                    configuredId, configuredSecret, environmentId, environmentSecret, "", "");
                throw new InvalidOperationException("expected credential resolution to fail");
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }
        }

        private static void EmbedsDandanCredentialSettings()
        {
            var assembly = typeof(DandanCredentialResolver).Assembly;
            var names = assembly.GetManifestResourceNames();
            var html = ReadResource(assembly, names.Single(x => x.EndsWith("configPage.html", StringComparison.OrdinalIgnoreCase)));
            var script = ReadResource(assembly, names.Single(x => x.EndsWith("config.js", StringComparison.OrdinalIgnoreCase)));

            Assert(html.Contains("id=\"DandanApiId\""), "settings page should contain the Dandan API ID input");
            Assert(html.Contains("id=\"DandanApiSecret\"") && html.Contains("type=\"password\""),
                "settings page should mask the Dandan API Secret");
            Assert(script.Contains("config.Dandan.ApiId") && script.Contains("config.Dandan.ApiSecret"),
                "settings script should load both Dandan credential values");
            Assert(script.Contains("dandan.ApiId") && script.Contains("dandan.ApiSecret") &&
                   script.Contains("dandan.WithRelatedDanmu") && script.Contains("dandan.ChConvert"),
                "settings script should save credentials without dropping existing Dandan options");
        }

        private static string ReadResource(System.Reflection.Assembly assembly, string name)
        {
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static DanmuMatchCandidate Candidate(string id, string site, int sourceOrder, double score)
        {
            return new DanmuMatchCandidate
            {
                Id = id,
                Name = id,
                Site = site,
                SiteName = site,
                SourceOrder = sourceOrder,
                Score = score
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
