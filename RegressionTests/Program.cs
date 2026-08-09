using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;
using Emby.Plugin.Danmu.Scraper.Dandan;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Iqiyi;
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
            PreservesValidUnicodeWhileRemovingInvalidXmlScalars();
            RemovesInvalidXmlCharacterReferences();
            PreservesCharacterReferenceTextInsideCdata();
            RecoversIqiyiXmlContainingInvalidCharacters();
            RecoversBilibiliXmlContainingInvalidCharacters();
            SanitizesFinalXmlForEveryProviderAndAcceptsSmallValidOutput();
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

        private static void PreservesValidUnicodeWhileRemovingInvalidXmlScalars()
        {
            var source = "中文\t换行\n回车\remoji😀尾部" + '\u0001' + '\uFFFE' + '\uFFFF' +
                         "\uD800孤立高代理\uDC00孤立低代理";
            var sanitized = Xml10Sanitizer.SanitizeText(source);

            Assert(sanitized.Contains("中文\t换行\n回车\remoji😀尾部"),
                "valid Chinese, whitespace, and supplementary Unicode should be preserved");
            Assert(!sanitized.Contains('\u0001') && !sanitized.Contains('\uFFFE') &&
                   !sanitized.Contains('\uFFFF') && !sanitized.Contains("\uD800") &&
                   !sanitized.Contains("\uDC00"),
                "invalid XML scalars and isolated surrogates should be removed");
        }

        private static void RemovesInvalidXmlCharacterReferences()
        {
            const string source = "<root>保留&#10;&#x1F600;移除&#0;&#xB;&#65535;&#x110000;</root>";
            var sanitized = Xml10Sanitizer.SanitizeDocument(source);
            var document = new XmlDocument();
            document.LoadXml(sanitized);

            Assert(sanitized.Contains("&#10;") && sanitized.Contains("&#x1F600;"),
                "legal numeric XML character references should be preserved");
            Assert(!sanitized.Contains("&#0;") && !sanitized.Contains("&#xB;") &&
                   !sanitized.Contains("&#65535;") && !sanitized.Contains("&#x110000;"),
                "illegal numeric XML character references should be removed");
        }

        private static void PreservesCharacterReferenceTextInsideCdata()
        {
            const string source = "<root><![CDATA[字面文本 &#xFFFF; 和 &#0;]]><value>&#xFFFF;</value></root>";
            var sanitized = Xml10Sanitizer.SanitizeDocument(source);
            var document = new XmlDocument();
            document.LoadXml(sanitized);

            Assert(document.DocumentElement.FirstChild.Value == "字面文本 &#xFFFF; 和 &#0;",
                "numeric-reference-like text inside CDATA should remain literal and unchanged");
            Assert(document.DocumentElement.SelectSingleNode("value").InnerText == string.Empty,
                "an illegal numeric character reference outside CDATA should still be removed");
        }

        private static void RecoversIqiyiXmlContainingInvalidCharacters()
        {
            var xml = "<danmu><sum>1</sum><validSum>1</validSum><duration>1</duration>" +
                      "<data><entry><int>1</int><list><bulletInfo>" +
                      "<contentId>1</contentId><content>中文\n😀\uFFFF尾部&#0;</content>" +
                      "<font>1</font><color>FFFFFF</color><showTime>1</showTime>" +
                      "</bulletInfo></list></entry></data></danmu>";
            var cleaned = IqiyiApi.RemoveInvalidXmlChars(xml);
            var serializer = new XmlSerializer(typeof(IqiyiCommentDocument));
            IqiyiCommentDocument result;
            using (var reader = new StringReader(cleaned))
            {
                result = (IqiyiCommentDocument)serializer.Deserialize(reader);
            }

            Assert(result.Data[0].List[0].Content == "中文\n😀尾部",
                "Iqiyi fallback should remove invalid XML data without damaging valid comment text");
        }

        private static void RecoversBilibiliXmlContainingInvalidCharacters()
        {
            var xml = "<i><d p=\"1,1,25,16777215,0,0,user,1,1\">中文\n😀\uFFFF尾部&#0;</d></i>";
            var result = Emby.Plugin.Danmu.Scraper.Bilibili.Bilibili.ParseXml(xml);

            Assert(result.Items.Count == 1 && result.Items[0].Content == "中文\n😀尾部",
                "Bilibili XML fallback should recover a comment containing invalid XML data");
        }

        private static void SanitizesFinalXmlForEveryProviderAndAcceptsSmallValidOutput()
        {
            var providers = new[] { "BilibiliID", "IqiyiID", "TencentID", "YoukuID", "MgtvID", "DandanID" };
            foreach (var provider in providers)
            {
                var danmaku = new ScraperDanmaku
                {
                    ProviderId = provider,
                    Items = new List<ScraperDanmakuText>
                    {
                        new ScraperDanmakuText
                        {
                            Id = 1,
                            Progress = 1000,
                            MidHash = "用户\uFFFF😀",
                            Content = "中文\n😀\u0001\uFFFE\uFFFF尾部"
                        }
                    }
                };

                Assert(DanmuDownloadContent.HasUsableItems(danmaku),
                    "a single valid comment should be usable regardless of provider");
                var bytes = DanmuDownloadContent.Serialize(danmaku);
                Assert(bytes.Length < 1024,
                    "the regression fixture should remain below the removed one-kilobyte threshold");

                var document = new XmlDocument();
                document.LoadXml(Encoding.UTF8.GetString(bytes));
                var finalContent = document.DocumentElement.SelectSingleNode("d").InnerText;
                Assert(finalContent.Replace("\r\n", "\n") == "中文\n😀尾部",
                    "final XML should preserve valid text and remove invalid characters for " + provider +
                    "; actual=" + finalContent);
            }

            Assert(!DanmuDownloadContent.HasUsableItems(new ScraperDanmaku()),
                "an empty danmu result should not be treated as usable");
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
