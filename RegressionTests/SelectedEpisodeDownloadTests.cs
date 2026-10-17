using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Controllers;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class SelectedEpisodeDownloadTests
    {
        public static void Run()
        {
            var directory = Path.Combine(Path.GetTempPath(), "danmu-selected-download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var originalFileSystem = BaseItem.FileSystem;
            var originalMediaSources = BaseItem.MediaSourceManager;
            var originalLibrary = BaseItem.LibraryManager;
            var originalScrapers = SingletonManager.ScraperManager;
            var originalJson = SingletonManager.JsonSerializer;
            try
            {
                BaseItem.FileSystem = CreateEntityDependency<MediaBrowser.Model.IO.IFileSystem>();
                BaseItem.MediaSourceManager = CreateEntityDependency<IMediaSourceManager>();
                var logs = DispatchProxy.Create<ILogManager, NullDependencyProxy>();
                var library = DispatchProxy.Create<ILibraryManager, NullDependencyProxy>();
                BaseItem.LibraryManager = library;
                SingletonManager.ScraperManager = new ScraperManager(logs);
                SingletonManager.JsonSerializer = DispatchProxy.Create<MediaBrowser.Model.Serialization.IJsonSerializer, NullDependencyProxy>();
                using (var helper = new LibraryManagerEventsHelper(
                    library, logs))
                {
                    DownloadsExactSelectedEpisode(helper, directory, 0, 1, "special");
                    DownloadsExactSelectedEpisode(helper, directory, null, null, "unknown");
                    DownloadsExactSelectedEpisode(helper, directory, 1, 47, "normal");
                    DownloadsExactSelectedEpisode(helper, directory, -1, 0, "incomplete");
                    RunsCapturedPlanAndRetry(helper, library, logs, directory);
                    RunsCapturedPlanAndRetry(helper, library, logs, directory, true);
                    RunsCachedSingleEpisodeSelection(helper, library, logs, directory);
                    ReportsActualProviderFailureWithoutReplacingXml(helper, directory);
                }
            }
            finally
            {
                BaseItem.FileSystem = originalFileSystem;
                BaseItem.MediaSourceManager = originalMediaSources;
                BaseItem.LibraryManager = originalLibrary;
                SingletonManager.ScraperManager = originalScrapers;
                SingletonManager.JsonSerializer = originalJson;
                Directory.Delete(directory, true);
            }
        }

        private static void RunsCapturedPlanAndRetry(
            LibraryManagerEventsHelper helper, ILibraryManager library, ILogManager logs, string directory, bool missingSeason = false)
        {
            var scraper = new RecordingScraper("plan" + missingSeason);
            SingletonManager.ScraperManager.Register(scraper);
            var controller = new DanmuController(BaseItem.FileSystem, logs, helper, library);
            var season = new Season { Id = Guid.NewGuid(), Name = "Specials", IndexNumber = 0 };
            var first = new Episode { Id = Guid.NewGuid(), Name = "Selected OAD", IndexNumber = 1,
                ParentIndexNumber = 0, Path = Path.Combine(directory, "queued-" + missingSeason + "-oad.strm") };
            var second = new Episode { Id = Guid.NewGuid(), Name = "Selected OVA", IndexNumber = 2,
                ParentIndexNumber = 0, Path = Path.Combine(directory, "queued-" + missingSeason + "-ova.strm") };
            var proxy = (NullDependencyProxy)(object)library;
            proxy.Items[season.Id.ToString()] = season;
            proxy.Items[first.Id.ToString()] = first;
            proxy.Items[second.Id.ToString()] = second;
            var plan = new CompositeSeasonPlan
            {
                UnmatchedRuns = new List<CompositeSeasonUnmatchedRun> { new CompositeSeasonUnmatchedRun() },
                Mappings = new List<CompositeSeasonEpisodeMapping>
                {
                    Mapping(first, scraper, "oad"), Mapping(second, scraper, "ova"),
                },
            };
            var buildType = typeof(DanmuController).GetNestedType("CompositePlanBuild", BindingFlags.NonPublic);
            var build = Activator.CreateInstance(buildType);
            buildType.GetProperty("Plan").SetValue(build, plan);
            buildType.GetProperty("Episodes").SetValue(build, new List<Episode> { first, second });
            buildType.GetProperty("PlanFingerprint").SetValue(build, "captured-selected-plan");
            var generation = SeasonPlanGenerationCoordinator.Shared.Begin(season.Id.ToString());
            Method("RegisterPreviewPlanFingerprint").Invoke(null, new[]
            {
                (object)season.Id.ToString(), generation, "captured-selected-plan", build,
            });

            // Another preview and changed metadata must not veto this selection.
            SeasonPlanGenerationCoordinator.Shared.Begin(season.Id.ToString());
            first.ParentIndexNumber = null;
            first.IndexNumber = null;
            if (missingSeason) proxy.Items.Remove(season.Id.ToString());
            File.WriteAllText(Path.Combine(directory, first.GetDanmuXmlPath(scraper.ProviderId)), "recent existing XML");
            scraper.OnRequest = () => SeasonPlanGenerationCoordinator.Shared.Begin(season.Id.ToString());
            var oldSeriesContext = Guid.NewGuid().ToString();
            proxy.ForbiddenItemIds.Add(oldSeriesContext);
            var externalSeasonId = Guid.NewGuid().ToString();
            if (!missingSeason) proxy.Items[externalSeasonId] = season;
            var request = new DanmuParams
            {
                Option = DanmuDispatchOption.StartTrackedDownload,
                Id = missingSeason ? season.Id.ToString("N") : externalSeasonId,
                SeriesId = oldSeriesContext, CompositePlan = true, PlanGeneration = generation,
                PlanFingerprint = "captured-selected-plan", ConfirmPartial = false, ForceRefresh = true,
                CompositeSelections = "[{}]",
            };
            var task = (DanmuDownloadTaskResult)controller.Any(request).GetAwaiter().GetResult();
            Assert(!string.IsNullOrEmpty(task.TaskId), "a superseded partial preview must start its selected mappings");
            task = WaitForTask(controller, task.TaskId);
            Assert(task.Succeeded == 2 && task.Failed == 0 && task.Skipped == 0 && scraper.Requests == 2,
                "confirmed Season mappings must complete through metadata changes and recent files");
            AssertXml(first, scraper, "oad-comment-1");
            AssertXml(second, scraper, "ova-comment-2");

            proxy.Items.Remove(season.Id.ToString());
            var tasks = (System.Collections.Concurrent.ConcurrentDictionary<string, DanmuDownloadTaskResult>)
                typeof(DanmuController).GetField("DownloadTasks", BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null);
            tasks[task.TaskId].SeriesId = oldSeriesContext;
            task = ((Task<DanmuDownloadTaskResult>)Method("RetryTrackedEpisode").Invoke(controller,
                new object[] { new DanmuParams { TaskId = task.TaskId, Id = first.Id.ToString() } }))
                .GetAwaiter().GetResult();
            task = WaitForTask(controller, task.TaskId);
            Assert(task.Failed == 0 && scraper.Requests == 3,
                "retry must execute the same chosen source even when Season metadata is unavailable");
            AssertXml(first, scraper, "oad-comment-3");

            scraper.Failure = new IOException("fixture retry provider unavailable");
            task = ((Task<DanmuDownloadTaskResult>)Method("RetryTrackedEpisode").Invoke(controller,
                new object[] { new DanmuParams { TaskId = task.TaskId, Id = first.Id.ToString() } }))
                .GetAwaiter().GetResult();
            task = WaitForTask(controller, task.TaskId);
            Assert(task.Failed == 1 && task.Episodes[0].Message == "fixture retry provider unavailable",
                "missing optional Season metadata must not conceal the actual retry failure");
            AssertXml(first, scraper, "oad-comment-3");

            scraper.Failure = null;
            task = ((Task<DanmuDownloadTaskResult>)Method("RetryTrackedEpisode").Invoke(controller,
                new object[] { new DanmuParams { TaskId = task.TaskId, Id = first.Id.ToString() } }))
                .GetAwaiter().GetResult();
            task = WaitForTask(controller, task.TaskId);
            Assert(task.Failed == 0 && scraper.Requests == 5,
                "a provider failure must release the queue and permit another retry of the selected source");
            AssertXml(first, scraper, "oad-comment-5");
        }

        private static CompositeSeasonEpisodeMapping Mapping(Episode episode, AbstractScraper scraper, string source)
        {
            return new CompositeSeasonEpisodeMapping
            {
                LocalEpisodeItemId = episode.Id.ToString(), SourceEpisodeId = source + "-id", CommentId = source + "-comment",
                Source = new CompositeSeasonSourceIdentity { ProviderId = scraper.ProviderId, MediaId = "ova-source" },
            };
        }

        private static void RunsCachedSingleEpisodeSelection(
            LibraryManagerEventsHelper helper, ILibraryManager library, ILogManager logs, string directory)
        {
            var scraper = new RecordingScraper("single");
            SingletonManager.ScraperManager.Register(scraper);
            var episode = new Episode { Id = Guid.NewGuid(), Name = "Standalone OAD", ParentIndexNumber = null,
                IndexNumber = null, Path = Path.Combine(directory, "standalone-oad.strm") };
            ((NullDependencyProxy)(object)library).Items[episode.Id.ToString()] = episode;
            var source = new ScraperEpisode { Id = "selected-single-id", CommentId = "selected-single-comment", EpisodeNumber = 13 };
            var media = new ScraperMedia { Episodes = new List<ScraperEpisode> { source } };
            Method("RegisterSelectedEpisodeSources").Invoke(null, new object[] { episode, scraper.ProviderId, "selected-ova", media });
            source.CommentId = "changed-upstream-comment";
            File.WriteAllText(Path.Combine(directory, episode.GetDanmuXmlPath(scraper.ProviderId)), "recent existing XML");
            var controller = new DanmuController(BaseItem.FileSystem, logs, helper, library);
            var request = new DanmuParams { Option = DanmuDispatchOption.StartTrackedDownload,
                Id = episode.Id.ToString(), Site = scraper.ProviderId,
                CandidateId = "selected-ova", SourceEpisodeId = "selected-single-id", ForceRefresh = false };
            var task = (DanmuDownloadTaskResult)controller.Any(request).GetAwaiter().GetResult();
            task = WaitForTask(controller, task.TaskId);
            Assert(task.Skipped == 1 && task.Failed == 0 && scraper.Requests == 0 &&
                File.ReadAllText(Path.Combine(directory, episode.GetDanmuXmlPath(scraper.ProviderId))) == "recent existing XML",
                "normal seven-day duplicate skipping must remain intact for a selected Episode");

            scraper.Failure = new IOException("fixture replay provider unavailable");
            var replay = (DanmuDownloadTaskResult)controller.Any(new DanmuParams
                { Option = DanmuDispatchOption.ReplaySevenDaySkipped, TaskId = task.TaskId }).GetAwaiter().GetResult();
            replay = WaitForTask(controller, replay.TaskId);
            Assert(replay.Failed == 1 && replay.Episodes[0].Message == "fixture replay provider unavailable" &&
                File.ReadAllText(Path.Combine(directory, episode.GetDanmuXmlPath(scraper.ProviderId))) == "recent existing XML",
                "seven-day replay must report the actual provider error without requiring Season metadata");
            scraper.Failure = null;
            replay = (DanmuDownloadTaskResult)controller.Any(new DanmuParams
                { Option = DanmuDispatchOption.RetryTrackedEpisode, TaskId = replay.TaskId, Id = episode.Id.ToString() })
                .GetAwaiter().GetResult();
            replay = WaitForTask(controller, replay.TaskId);
            Assert(replay.Succeeded == 1 && replay.Failed == 0 && scraper.Requests == 2,
                "retrying seven-day replay must reuse the selected source and release its leases");
            AssertXml(episode, scraper, "selected-single-comment-2");

            request.ForceRefresh = true;
            task = (DanmuDownloadTaskResult)controller.Any(request).GetAwaiter().GetResult();
            task = WaitForTask(controller, task.TaskId);
            Assert(task.Succeeded == 1 && task.Skipped == 0 && task.Failed == 0 && scraper.Requests == 3,
                "force refresh must bypass seven-day skipping while missing Season metadata must not veto the selected source");
            AssertXml(episode, scraper, "selected-single-comment-3");
        }

        private static void ReportsActualProviderFailureWithoutReplacingXml(LibraryManagerEventsHelper helper, string directory)
        {
            var scraper = new RecordingScraper { Failure = new IOException("fixture provider unavailable") };
            var episode = new Episode { Id = Guid.NewGuid(), ParentIndexNumber = 0, Name = "OAD with provider failure",
                Path = Path.Combine(directory, "failed-oad.strm") };
            var media = new ScraperMedia { Episodes = new List<ScraperEpisode>
                { new ScraperEpisode { Id = "chosen-id", CommentId = "chosen-comment", EpisodeNumber = 1 } } };
            var xmlPath = Path.Combine(directory, episode.GetDanmuXmlPath(scraper.ProviderId));
            File.WriteAllText(xmlPath, "original XML");
            var failed = false;
            try { helper.DownloadEpisodeForProgress(episode, media, scraper, true, 1).GetAwaiter().GetResult(); }
            catch (IOException ex) { failed = ex.Message == "fixture provider unavailable"; }
            Assert(failed && scraper.Requests == 1 && File.ReadAllText(xmlPath) == "original XML",
                "a confirmed special must reach its provider, report the actual failure, and preserve existing XML");
        }

        private static MethodInfo Method(string name) => typeof(DanmuController).GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        private static DanmuDownloadTaskResult WaitForTask(DanmuController controller, string taskId)
        {
            DanmuDownloadTaskResult result = null;
            var cancellations = (System.Collections.Concurrent.ConcurrentDictionary<string, CancellationTokenSource>)
                typeof(DanmuController).GetField("DownloadTaskCancellations", BindingFlags.NonPublic | BindingFlags.Static)
                    .GetValue(null);
            Assert(SpinWait.SpinUntil(() =>
            {
                result = (DanmuDownloadTaskResult)Method("GetDownloadProgress").Invoke(controller, new object[] { taskId });
                return result.Status != "queued" && result.Status != "running" && !cancellations.ContainsKey(taskId);
            }, TimeSpan.FromSeconds(5)), "the queued download must settle");
            return result;
        }

        private static void AssertXml(Episode episode, AbstractScraper scraper, string content)
        {
            var xml = new XmlDocument();
            xml.Load(Path.Combine(episode.ContainingFolderPath, episode.GetDanmuXmlPath(scraper.ProviderId)));
            Assert(xml.SelectSingleNode("/i/d")?.InnerText == content, "XML must contain exactly the selected source comments");
        }

        private static void DownloadsExactSelectedEpisode(
            LibraryManagerEventsHelper helper, string directory, int? seasonNumber, int? localNumber, string name)
        {
            var episode = new Episode
            {
                Id = Guid.NewGuid(), Name = "OAD " + name,
                Path = Path.Combine(directory, name + ".strm"),
                ParentIndexNumber = seasonNumber, IndexNumber = localNumber,
            };
            var media = new ScraperMedia
            {
                Id = "selected-ova",
                Episodes = new List<ScraperEpisode>
                {
                    new ScraperEpisode { Id = "unselected", CommentId = "wrong-comment", EpisodeNumber = 1 },
                    new ScraperEpisode { Id = "selected-oad", CommentId = "selected-comment", EpisodeNumber = 13, Title = "OAD" },
                },
            };
            Assert(DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(
                media, "selected-oad", out var exact, out _), "the user's selected OAD must resolve by source ID");
            var scraper = new RecordingScraper();
            var outcome = helper.DownloadEpisodeForProgress(episode, exact, scraper, false, 1).GetAwaiter().GetResult();
            Assert(outcome.FilePersisted && outcome.Status == "success", name + " must download its confirmed mapping");
            Assert(scraper.Requests == 1 && scraper.LastCommentId == "selected-comment",
                name + " must request only the chosen source, independently of local numbering");
            Assert(outcome.ProviderValue == "selected-oad", name + " must retain the chosen upstream episode ID");
            var path = Path.Combine(directory, episode.GetDanmuXmlPath(scraper.ProviderId));
            var xml = new XmlDocument();
            xml.Load(path);
            Assert(xml.SelectSingleNode("/i/d")?.InnerText == "selected-comment-1", name + " must write valid source XML");

            outcome = helper.DownloadEpisodeForProgress(episode, exact, scraper, true, 1).GetAwaiter().GetResult();
            xml.Load(path);
            Assert(outcome.FilePersisted && scraper.Requests == 2 &&
                xml.SelectSingleNode("/i/d")?.InnerText == "selected-comment-2",
                name + " retry must request and atomically replace the same selected source");
        }

        private sealed class RecordingScraper : AbstractScraper
        {
            private readonly string _suffix;
            public RecordingScraper(string suffix = "") : base(null) { _suffix = suffix; }
            public int Requests { get; private set; }
            public string LastCommentId { get; private set; }
            public Action OnRequest { get; set; }
            public Exception Failure { get; set; }
            public override string Name => "SelectedDownloadFixture" + _suffix;
            public override string ProviderName => Name;
            public override string ProviderId => "SelectedDownloadFixtureID" + _suffix;
            public override Task<ScraperDanmaku> GetDanmuContent(BaseItem item, string commentId)
            {
                LastCommentId = commentId;
                Requests++;
                OnRequest?.Invoke();
                if (Failure != null) return Task.FromException<ScraperDanmaku>(Failure);
                return Task.FromResult(new ScraperDanmaku
                {
                    Items = new List<ScraperDanmakuText>
                    {
                        new ScraperDanmakuText { Content = commentId + "-" + Requests, Mode = 1 },
                    },
                });
            }
            public override Task<List<ScraperSearchInfo>> Search(BaseItem item) => throw new InvalidOperationException("download must not search again");
            public override Task<string> SearchMediaId(BaseItem item) => throw new InvalidOperationException("download must not rematch");
            public override Task<ScraperMedia> GetMedia(BaseItem item, string id) => throw new InvalidOperationException("download must use frozen mappings");
            public override Task<ScraperEpisode> GetMediaEpisode(BaseItem item, string id) => throw new InvalidOperationException("download must use its chosen source");
        }

        private static T CreateEntityDependency<T>() where T : class
        {
            // Emby's filesystem interface includes Span methods that cannot be
            // boxed by DispatchProxy. Entity path helpers forward to System.IO;
            // the plugin's own filesystem still performs real atomic writes.
            var contract = typeof(T);
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("SelectedDownloadEntityFileSystem"), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Fixture").DefineType(
                "EntityFileSystem", TypeAttributes.Public, typeof(object), new[] { contract });
            type.DefineDefaultConstructor(MethodAttributes.Public);
            foreach (var method in contract.GetMethods())
            {
                var parameters = Array.ConvertAll(method.GetParameters(), parameter => parameter.ParameterType);
                var implementation = type.DefineMethod(method.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual, method.ReturnType, parameters);
                var il = implementation.GetILGenerator();
                var pathMethod = typeof(Path).GetMethod(method.Name, parameters);
                if (pathMethod != null && pathMethod.ReturnType == method.ReturnType)
                {
                    for (var index = 0; index < parameters.Length; index++) il.Emit(OpCodes.Ldarg, index + 1);
                    il.Emit(OpCodes.Call, pathMethod);
                }
                else if (method.Name == "IsPathFile" || method.Name == "SupportsPathNatively") il.Emit(OpCodes.Ldc_I4_1);
                else if (method.ReturnType != typeof(void))
                {
                    if (method.ReturnType.IsValueType)
                    {
                        var value = il.DeclareLocal(method.ReturnType);
                        il.Emit(OpCodes.Ldloca, value);
                        il.Emit(OpCodes.Initobj, method.ReturnType);
                        il.Emit(OpCodes.Ldloc, value);
                    }
                    else il.Emit(OpCodes.Ldnull);
                }
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(implementation, method);
            }
            return (T)Activator.CreateInstance(type.CreateType());
        }

        public class NullDependencyProxy : DispatchProxy
        {
            public Dictionary<string, BaseItem> Items { get; } = new Dictionary<string, BaseItem>();
            public HashSet<string> ForbiddenItemIds { get; } = new HashSet<string>();
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "SerializeToString") return "{}";
                if (targetMethod.Name == "GetItemById" && args.Length > 0)
                {
                    var id = args[0]?.ToString() ?? string.Empty;
                    if (ForbiddenItemIds.Contains(id)) throw new InvalidOperationException("download must not resolve matching context again");
                    return Items.TryGetValue(id, out var item) ? item : null;
                }
                if (targetMethod.ReturnType == typeof(ILogger)) return DispatchProxy.Create<ILogger, NullDependencyProxy>();
                if (targetMethod.ReturnType == typeof(void)) return null;
                if (targetMethod.ReturnType == typeof(Task)) return Task.CompletedTask;
                return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
