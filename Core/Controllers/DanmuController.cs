using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Emby.Plugin.Danmu.Core.Controllers.Dto;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.Danmu.Core.Controllers
{
    [Route("/plugin/danmu/{id}")]
    [Route("/api/danmu/{id}")]
    [Route("/plugin/danmu/raw/{id}")]
    [Route("/api/danmu/{id}/raw")]
    [Route("/api/danmu/search")]
    public class DanmuParams : IReturn<object>
    {
        [DataMember(Name="id")]
        public string Id { get; set; } = string.Empty;

        // MatchCandidateDetails accepts itemId from new dialog clients while
        // keeping the established route {id} form compatible.
        [DataMember(Name="itemId")]
        public string ItemId { get; set; } = string.Empty;
        
        [DataMember(Name="needSites")]
        public List<string> NeedSites { get; set; } = new List<string>();
        
        [DataMember(Name="option")]
        public string Option { get; set; } = DanmuDispatchOption.DownloadXml;
        
        [DataMember(Name="keyword")]
        public string Keyword { get; set; } = string.Empty;

        [DataMember(Name="site")]
        public string Site { get; set; } = string.Empty;

        [DataMember(Name="candidateId")]
        public string CandidateId { get; set; } = string.Empty;

        [DataMember(Name="selectionEvidenceToken")]
        public string SelectionEvidenceToken { get; set; } = string.Empty;

        [DataMember(Name="candidateEvidence")]
        public string CandidateEvidence { get; set; } = string.Empty;

        [DataMember(Name="moviePartToken")]
        public string MoviePartToken { get; set; } = string.Empty;

        [DataMember(Name="generation")]
        public string Generation { get; set; } = string.Empty;

        [DataMember(Name="manual")]
        public bool Manual { get; set; }

        [DataMember(Name="force")]
        public bool Force { get; set; }

        [DataMember(Name="mode")]
        public string Mode { get; set; } = DanmuMatchIntent.Default;

        [DataMember(Name="rematch")]
        public bool Rematch { get; set; }

        [DataMember(Name="forceRefresh")]
        public bool ForceRefresh { get; set; }

        [DataMember(Name="parentTitleRematch")]
        public bool ParentTitleRematch { get; set; }

        [DataMember(Name="taskId")]
        public string TaskId { get; set; } = string.Empty;

        [DataMember(Name="seriesId")]
        public string SeriesId { get; set; } = string.Empty;

        [DataMember(Name="seasonName")]
        public string SeasonName { get; set; } = string.Empty;

        [DataMember(Name="seasonNumber")]
        public int? SeasonNumber { get; set; }

        [DataMember(Name="seasonYear")]
        public int? SeasonYear { get; set; }

        [DataMember(Name="sourceEpisodeNumber")]
        public int? SourceEpisodeNumber { get; set; }

        [DataMember(Name="sourceEpisodeId")]
        public string SourceEpisodeId { get; set; } = string.Empty;

        // Emby 4.9 GET binding is scalar-only. The browser sends a stable
        // operation id so a later CancelSearch request can address the same
        // server-side CTS; searchScope is echoed in preview diagnostics.
        [DataMember(Name="searchOperationId")]
        public string SearchOperationId { get; set; } = string.Empty;

        [DataMember(Name="searchScope")]
        public string SearchScope { get; set; } = string.Empty;

        [DataMember(Name="mappingProtocolVersion")]
        public int MappingProtocolVersion { get; set; }

        [DataMember(Name="planGeneration")]
        public long PlanGeneration { get; set; }

        [DataMember(Name="planFingerprint")]
        public string PlanFingerprint { get; set; } = string.Empty;

        [DataMember(Name="compositeSelections")]
        public string CompositeSelections { get; set; } = string.Empty;

        // As with compositeSelections, Emby 4.9 binds GET query values only
        // as scalars.  The compact JSON is parsed once before preview or
        // download planning; it is dialog intent, not durable metadata.
        [DataMember(Name="excludedLocalEpisodeItemIds")]
        public string ExcludedLocalEpisodeItemIds { get; set; } = string.Empty;

        // Emby 4.9's GET ValueParser only binds scalar query values. This is
        // populated explicitly by DanmuController for MatchPreview/download;
        // it must never be handed back to the request binder or serialized.
        [System.Runtime.Serialization.IgnoreDataMember]
        public List<DanmuCompositeSeasonSelection> ParsedCompositeSelections { get; set; } =
            new List<DanmuCompositeSeasonSelection>();

        [System.Runtime.Serialization.IgnoreDataMember]
        public List<string> ParsedExcludedLocalEpisodeItemIds { get; set; } = new List<string>();

        // A composite download may contain only already-verified direct Episode
        // mappings, in which case there are deliberately no browser selections.
        [DataMember(Name="compositePlan")]
        public bool CompositePlan { get; set; }

        [DataMember(Name="confirmPartial")]
        public bool ConfirmPartial { get; set; }

        // Context for searching one temporary group.  The server consumes these
        // values only to build a verified preview; they never become a download
        // mapping without a later compact selection from the browser.
        [DataMember(Name="compositeStartEpisodeItemId")]
        public string CompositeStartEpisodeItemId { get; set; } = string.Empty;

        [DataMember(Name="compositeEpisodeCount")]
        public int CompositeEpisodeCount { get; set; }
    }

    public class DanmuController : BaseApiService
    {
        private static readonly ConcurrentDictionary<string, DanmuDownloadTaskResult> DownloadTasks =
            new ConcurrentDictionary<string, DanmuDownloadTaskResult>(StringComparer.OrdinalIgnoreCase);
        private static readonly DanmuCandidateEvidenceRegistry CandidateEvidence =
            new DanmuCandidateEvidenceRegistry();
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> DownloadTaskCancellations =
            new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        // A seven-day replay is a new task, so task-level cancellation alone
        // cannot prevent it racing the original task's single-episode retry.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> EpisodeRetryLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim TrackedDownloadQueue = new SemaphoreSlim(1, 1);
        private static readonly SearchOperationRegistry SearchOperations =
            new SearchOperationRegistry(BoundedSearchPolicy.Shared.Options);
        private static readonly SeasonPlanGenerationCoordinator SeasonPlanGenerations =
            SeasonPlanGenerationCoordinator.Shared;
        private static readonly ConcurrentDictionary<string, string> SeasonPreviewPlanFingerprints =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ILibraryManager _libraryManager;
        private readonly LibraryManagerEventsHelper _libraryManagerEventsHelper;
        private readonly MediaBrowser.Model.IO.IFileSystem _fileSystem;
        private readonly ScraperManager _scraperManager;
        
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmuController"/> class.
        /// </summary>
        /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="logManager"></param>
        public DanmuController(
            MediaBrowser.Model.IO.IFileSystem fileSystem,
            ILogManager logManager,
            LibraryManagerEventsHelper libraryManagerEventsHelper,
            ILibraryManager libraryManager)
        {
            
            _fileSystem = fileSystem;
            _logger = logManager.getDefaultLogger();
            _libraryManager = libraryManager;
            _libraryManagerEventsHelper = libraryManagerEventsHelper;
            _scraperManager = SingletonManager.ScraperManager;
            _jsonSerializer = SingletonManager.JsonSerializer;
        }

        /// <summary>
        /// 获取弹幕文件内容.
        /// </summary>
        /// <returns>xml弹幕文件内容</returns>
        public async Task<object> Any(DanmuParams danmuParams)
        {
            if (!TryPrepareCompositeSelections(danmuParams, out var compositeError))
            {
                return DanmuDispatchOption.MatchPreview.Equals(danmuParams?.Option)
                    ? (object)InvalidCompositePreview(danmuParams, compositeError)
                    : InvalidCompositeDownload(danmuParams, compositeError);
            }
            _logger.Info("当前请求信息 danmuParams={0}", danmuParams.ToJson());
            
            // 获取json格式弹幕
            if (DanmuDispatchOption.GetJsonById.Equals(danmuParams.Option))
            {
                return await GetDanmuForJson(danmuParams).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.Refresh.Equals(danmuParams.Option))
            {
                return await Refresh(danmuParams.Id);
            }

            if (DanmuDispatchOption.MatchPreview.Equals(danmuParams.Option))
            {
                return await GetMatchPreview(danmuParams).ConfigureAwait(false);
            }

            if (string.Equals("MatchCandidateDetails", danmuParams.Option, StringComparison.OrdinalIgnoreCase))
            {
                return await GetMatchCandidateDetails(danmuParams).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.GetSelectedCandidatePreview.Equals(danmuParams.Option))
            {
                return await GetSelectedCandidatePreview(danmuParams).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.CancelSearch.Equals(danmuParams.Option))
            {
                return CancelSearch(danmuParams.SearchOperationId);
            }

            if (DanmuDispatchOption.BindMatch.Equals(danmuParams.Option))
            {
                return await BindMatch(danmuParams).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.StartTrackedDownload.Equals(danmuParams.Option))
            {
                return await StartTrackedDownload(danmuParams).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.GetDownloadProgress.Equals(danmuParams.Option))
            {
                return GetDownloadProgress(danmuParams.TaskId);
            }

            if (DanmuDispatchOption.RetryTrackedEpisode.Equals(danmuParams.Option))
            {
                return await RetryTrackedEpisode(danmuParams).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.ReplaySevenDaySkipped.Equals(danmuParams.Option))
            {
                return await ReplaySevenDaySkipped(danmuParams.TaskId).ConfigureAwait(false);
            }

            if (DanmuDispatchOption.StopAllTrackedDownloads.Equals(danmuParams.Option))
            {
                return StopAllTrackedDownloads();
            }

            if (DanmuDispatchOption.SearchDanmu.Equals(danmuParams.Option))
            {
                return await SearchDanmu(danmuParams.Keyword); 
            }

            // 获取支持的站点弹幕信息
            if (DanmuDispatchOption.GetAllSupportSite.Equals(danmuParams.Option))
            {
                DanmuResultDto result = new DanmuResultDto();
                var allWithNoEnabled = _scraperManager.AllWithNoEnabled();
                List<DanmuSourceDto> sources = new List<DanmuSourceDto>(allWithNoEnabled.Count);
                foreach (AbstractScraper scraper in allWithNoEnabled)
                {
                    DanmuSourceDto source = new DanmuSourceDto();
                    source.Source = scraper.ProviderId;
                    source.SourceName = scraper.ProviderName;
                    source.Opened = scraper.DefaultEnable;
                    sources.Add(source);
                }
                result.Data = sources;
                return result;
            }

            if (DanmuDispatchOption.DownloadXml.Equals(danmuParams.Option))
            {
                return await Download(danmuParams.Id);
            }

            return "暂不支持的操作: " + danmuParams.Option;
        }

        private static bool TryPrepareCompositeSelections(DanmuParams request, out string error)
        {
            error = string.Empty;
            if (request == null)
            {
                error = "复合季请求为空。";
                return false;
            }

            if (!DanmuCompositeSeasonSelectionJson.TryParse(request.CompositeSelections,
                    out var selections, out error))
            {
                return false;
            }

            if (!DanmuExcludedLocalEpisodeItemIdsJson.TryParse(request.ExcludedLocalEpisodeItemIds,
                    out var exclusions, out error))
            {
                return false;
            }

            request.ParsedCompositeSelections = selections;
            request.ParsedExcludedLocalEpisodeItemIds = exclusions;
            return true;
        }

        private static DanmuMatchPreviewResult InvalidCompositePreview(DanmuParams request, string error)
        {
            return new DanmuMatchPreviewResult
            {
                ItemId = request?.Id ?? string.Empty,
                Status = "invalid_request",
                Message = string.IsNullOrWhiteSpace(error) ? "复合季选择参数无效。" : error,
                CanStart = false,
            };
        }

        private static DanmuDownloadTaskResult InvalidCompositeDownload(DanmuParams request, string error)
        {
            return new DanmuDownloadTaskResult
            {
                SeasonId = request?.Id ?? string.Empty,
                Status = "failed",
                Message = string.IsNullOrWhiteSpace(error) ? "复合季选择参数无效。" : error,
            };
        }

        private static DanmuSearchCancellationResult CancelSearch(string searchOperationId)
        {
            var cancelled = SearchOperations.TryCancel(searchOperationId);
            return new DanmuSearchCancellationResult
            {
                Success = cancelled,
                SearchOperationId = searchOperationId ?? string.Empty,
                Status = cancelled ? "cancelled" : "not_found",
                Message = cancelled
                    ? "Search cancellation was requested."
                    : "Search operation was not found or has already completed.",
            };
        }

        private async Task<DanmuResultDto> GetDanmuForJson(DanmuParams danmuParams)
        {
            var currentItem = _libraryManager.GetItemById(danmuParams.Id);
            if (currentItem == null)
            {
                return new DanmuResultDto();
            }

            List<string> sites = danmuParams.NeedSites;
            DanmuResultDto danmuResultDto = new DanmuResultDto();
            if (sites == null || sites.Count == 0)
            {
                var count = _scraperManager.All().Count;
                if (count == 0)
                {
                    return danmuResultDto;
                }

                sites = _scraperManager
                    .All()
                    .Select(s => s.ProviderId)
                    .ToList();
            }
            
            List<DanmuSourceDto> danmuSources= new List<DanmuSourceDto>(sites.Count);
            List<Task<DanmuSourceDto>> danmuSourceTasks= new List<Task<DanmuSourceDto>>(sites.Count);
            danmuResultDto.Data = danmuSources;
            
            foreach (string site in sites)
            {
                if (site == null)
                {
                    continue;
                }
                
                Task<DanmuSourceDto> danmuSourceTask = GetDanmuSourceDto(currentItem, site);
                danmuSourceTasks.Add(danmuSourceTask);
            }
            
            danmuSourceTasks.Add(GetDanmuSourceDto(currentItem, null));
            await Task.WhenAll(danmuSourceTasks).ConfigureAwait(false);
            foreach (Task<DanmuSourceDto?> danmuSourceTask in danmuSourceTasks)
            {
                var danmuSourceDto = danmuSourceTask.GetAwaiter().GetResult();
                if (danmuSourceDto != null && (string.IsNullOrEmpty(danmuSourceDto.Source) || "其他".Equals(danmuSourceDto.Source) || sites.Contains(danmuSourceDto.Source)))
                {
                    danmuSources.Add(danmuSourceDto);
                }
            }
            
            _logger.Info("任务添加完成 准备输出 danmuResultDto={0}", danmuResultDto.ToJson());
            return danmuResultDto;
        }

        private Task<DanmuSourceDto> GetDanmuSourceDto(BaseItem currentItem, string? site)
        {
            var danmuPath = Path.Combine(
                currentItem.ContainingFolderPath,
                currentItem.FileNameWithoutExtension + (site != null ? "_" + site : string.Empty) + ".xml");
            var fileMeta = _fileSystem.GetFileInfo(danmuPath);
            if (!fileMeta.Exists)
            {
                return Task.FromResult<DanmuSourceDto>(null);
            }

            var xmlDocument = new XmlDocument();
            xmlDocument.Load(danmuPath);
            XmlElement xmlNode = xmlDocument.DocumentElement;
            if (xmlNode == null)
            {
                return Task.FromResult<DanmuSourceDto>(null);
            }
            DanmuSourceDto danmuSourceDto = new DanmuSourceDto();
            List<DanmuEventDTO> danmuEventDtos = new List<DanmuEventDTO>();
            foreach (XmlNode node in xmlNode.ChildNodes) //4.遍历根节点（根节点包含所有节点）
            {
                // _logger.Info("XmlNode.InnerText={0}", node.InnerText);
                if ("sourceprovider".Equals(node.Name))
                {
                    danmuSourceDto.Source = node.InnerText;
                }
                else if ("datasize".Equals(node.Name) && danmuEventDtos.Count == 0)
                {
                    danmuEventDtos = new List<DanmuEventDTO>(int.Parse(node.InnerText));
                }
                else if ("d".Equals(node.Name) && node is XmlElement)
                {
                    DanmuEventDTO danmuEvent = new DanmuEventDTO();
                    danmuEvent.M = node.InnerText;
                    danmuEvent.P = ((XmlElement)node).GetAttribute("p");
                    danmuEventDtos.Add(danmuEvent);
                }
            }

            if (danmuSourceDto.Source == null)
            {
                danmuSourceDto.Source = "其他";
                if (danmuEventDtos.Count == 0)
                {
                    return Task.FromResult<DanmuSourceDto>(null);
                }
            }

            danmuSourceDto.DanmuEvents = danmuEventDtos;
            return Task.FromResult(danmuSourceDto);
        }

        /// <summary>
        /// 获取弹幕文件内容.
        /// </summary>
        /// <returns>xml弹幕文件内容</returns>
        // public async Task<ActionResult> Download(string id)
        // {
        //     if (string.IsNullOrEmpty(id))
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     var currentItem = _libraryManager.GetItemById(id);
        //     if (currentItem == null)
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     var danmuPath = Path.Combine(currentItem.ContainingFolderPath,
        //         currentItem.FileNameWithoutExtension + ".xml");
        //     var fileMeta = _fileSystem.GetFileInfo(danmuPath);
        //     if (!fileMeta.Exists)
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     return File(System.IO.File.ReadAllBytes(danmuPath), "text/xml");
        // }
        //
        /// <summary>
        /// 查找弹幕
        /// </summary>
        // [Route("/api/danmu/search")]
        // [HttpGet]
        public async Task<IEnumerable<MediaInfo>> SearchDanmu(string keyword)
        {
            var list = new List<MediaInfo>();
        
            if (string.IsNullOrEmpty(keyword))
            {
                return list;
            }
        
            _logger.Info("_scraperManager.all = {0}", _scraperManager.All());
            foreach (var scraper in _scraperManager.All())
            {
                try
                {
                    var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
                    var result = await scraper.SearchForApi(keyword).ConfigureAwait(false);
                    foreach (var searchInfo in result)
                    {
                        list.Add(new MediaInfo()
                        {
                            Id = searchInfo.Id,
                            Name = searchInfo.Name,
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{0}]Exception handled processing search movie [{1}]", scraper.Name,
                        keyword);
                }
            }
        
            return list;
        }
        
        public async Task<object> Download(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ResourceNotFoundException();
            }

            var currentItem = _libraryManager.GetItemById(id);
            if (currentItem == null)
            {
                throw new ResourceNotFoundException();
            }

            var readOnlyCollection = _scraperManager.All();
            foreach (AbstractScraper abstractScraper in readOnlyCollection)
            {
               var danmuPath = Path.Combine(currentItem.ContainingFolderPath, currentItem.FileNameWithoutExtension + "_" + abstractScraper.ProviderId + ".xml");
               var fileMeta = _fileSystem.GetFileInfo(danmuPath);
               if (fileMeta.Exists)
               {
                   return File.ReadAllBytes(danmuPath);
               }
            }
            
            var defaultDanmuPath = Path.Combine(currentItem.ContainingFolderPath, currentItem.FileNameWithoutExtension + ".xml");
            var defaultFileMeta = _fileSystem.GetFileInfo(defaultDanmuPath);
            if (defaultFileMeta.Exists)
            {
                return File.ReadAllBytes(defaultDanmuPath);
            }
            return null;
        }

        private async Task<DanmuMatchPreviewResult> GetMatchPreview(DanmuParams request)
        {
            var requestedOperationId = string.IsNullOrWhiteSpace(request?.SearchOperationId)
                ? Guid.NewGuid().ToString("N")
                : request.SearchOperationId.Trim();
            if (!SearchOperations.TryBegin(
                    requestedOperationId,
                    SearchOperationScope.Interactive,
                    out var operation,
                    out var error))
            {
                return new DanmuMatchPreviewResult
                {
                    ItemId = request?.Id ?? string.Empty,
                    Status = "invalid_request",
                    Message = error,
                    CanStart = false,
                    SearchOperationId = requestedOperationId,
                    SearchScope = GetSearchScope(request),
                };
            }

            using (operation)
            {
                var result = await GetMatchPreviewCore(request, operation.CancellationToken).ConfigureAwait(false);
                ApplySearchOperation(result, requestedOperationId, GetSearchScope(request));
                return result;
            }
        }

        /// <summary>
        /// Second phase of manual Episode matching. The initial candidate
        /// search intentionally does not resolve every candidate; this endpoint
        /// resolves exactly the one candidate the user selected and returns
        /// only safe source Episode identity/number/title fields.
        /// </summary>
        private async Task<DanmuSelectedCandidateDetailPreview> GetSelectedCandidatePreview(DanmuParams request)
        {
            var response = new DanmuSelectedCandidateDetailPreview
            {
                ItemId = request?.Id ?? string.Empty,
                Site = request?.Site ?? string.Empty,
                CandidateId = request?.CandidateId ?? string.Empty,
            };
            var movie = string.IsNullOrWhiteSpace(request?.Id)
                ? null
                : _libraryManager.GetItemById(request.Id) as Movie;
            if (movie != null)
            {
                return await GetSelectedMoviePartPreview(movie, request, response).ConfigureAwait(false);
            }
            var episode = string.IsNullOrWhiteSpace(request?.Id)
                ? null
                : _libraryManager.GetItemById(request.Id) as Episode;
            var season = episode?.GetParent() as Season;
            var scraper = _scraperManager.All().FirstOrDefault(candidate =>
                string.Equals(candidate.ProviderId, request?.Site, StringComparison.OrdinalIgnoreCase));
            if (episode == null || season == null || scraper == null ||
                string.IsNullOrWhiteSpace(request?.CandidateId))
            {
                response.Status = "invalid_request";
                response.Message = "Episode, provider site, and candidate id are required.";
                return response;
            }

            // Validate target-bound evidence before any provider detail call.
            // Manual keyword cards reuse this ordinary evidence boundary.
            if (!CandidateEvidence.TryResolve(request.SelectionEvidenceToken,
                    episode.Id.ToString(), scraper.ProviderId, request.CandidateId, out _))
            {
                response.Status = "invalid_or_stale_evidence";
                response.Message = "Episode candidate evidence is invalid or expired.";
                return response;
            }

            response.ItemId = episode.Id.ToString();
            response.Site = scraper.ProviderId;
            response.SiteName = scraper.ProviderName;
            var operationId = string.IsNullOrWhiteSpace(request.SearchOperationId)
                ? Guid.NewGuid().ToString("N")
                : request.SearchOperationId.Trim();
            response.SearchOperationId = operationId;
            if (!SearchOperations.TryBegin(
                    operationId,
                    SearchOperationScope.Interactive,
                    out var operation,
                    out var error))
            {
                response.Status = "invalid_request";
                response.Message = error;
                return response;
            }

            using (operation)
            {
                var execution = await BoundedSearchPolicy.Shared.ExecuteAsync(
                    scraper.ProviderId,
                    ignored => ResolveSelectedCandidateDetailAsync(
                        episode, season, scraper, request.CandidateId),
                    operation.CancellationToken).ConfigureAwait(false);
                if (execution.Status != BoundedSearchExecutionStatus.Completed)
                {
                    response.Status = execution.Status == BoundedSearchExecutionStatus.ProviderTimedOut
                        ? "timed_out"
                        : execution.Status == BoundedSearchExecutionStatus.Cancelled ? "cancelled" : "failed";
                    response.Message = execution.Error?.Message ??
                        "Selected candidate detail resolution did not complete.";
                    return response;
                }

                response.Episodes = (execution.Result?.Episodes ?? new List<ScraperEpisode>())
                    .Where(sourceEpisode => sourceEpisode != null &&
                        !string.IsNullOrWhiteSpace(sourceEpisode.Id) &&
                        !string.IsNullOrWhiteSpace(sourceEpisode.CommentId))
                    .OrderBy(sourceEpisode => sourceEpisode.EpisodeNumber ?? int.MaxValue)
                    .ThenBy(sourceEpisode => sourceEpisode.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(sourceEpisode => new DanmuSelectedCandidateSourceEpisode
                    {
                        Id = sourceEpisode.Id,
                        Number = sourceEpisode.EpisodeNumber,
                        Title = sourceEpisode.Title ?? string.Empty,
                    })
                    .ToList();
                response.Status = response.Episodes.Count > 0 ? "ready" : "no_usable_episodes";
                response.Message = response.Episodes.Count > 0
                    ? "Selected candidate source episodes are ready."
                    : "Selected candidate has no usable source episodes.";
                return response;
            }
        }

        private async Task<DanmuSelectedCandidateDetailPreview> GetSelectedMoviePartPreview(
            Movie movie,
            DanmuParams request,
            DanmuSelectedCandidateDetailPreview response)
        {
            var scraper = _scraperManager.All().FirstOrDefault(candidate =>
                string.Equals(candidate.ProviderId, request?.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null || string.IsNullOrWhiteSpace(request?.CandidateId) ||
                !CandidateEvidence.TryResolve(request.SelectionEvidenceToken,
                    movie.Id.ToString(), request.Site, request.CandidateId, out var candidateEvidence))
            {
                response.Status = "invalid_or_stale_evidence";
                response.Message = "Movie candidate evidence is invalid or expired.";
                return response;
            }

            response.ItemId = movie.Id.ToString();
            response.Site = scraper.ProviderId;
            response.SiteName = scraper.ProviderName;
            try
            {
                var detailExecution = await BoundedSearchPolicy.Shared.ExecuteAsync(
                    scraper.ProviderId,
                    ignored => scraper.GetMedia(movie, request.CandidateId),
                    CancellationToken.None).ConfigureAwait(false);
                if (detailExecution.Status != BoundedSearchExecutionStatus.Completed ||
                    detailExecution.Result == null)
                {
                    response.Status = "unresolved";
                    response.Message = "Movie detail could not be verified.";
                    return response;
                }

                var partExecution = await BoundedSearchPolicy.Shared.ExecuteAsync(
                    scraper.ProviderId,
                    token => scraper.GetMovieParts(movie, request.CandidateId, token),
                    CancellationToken.None).ConfigureAwait(false);
                var parts = partExecution.Status == BoundedSearchExecutionStatus.Completed
                    ? MoviePartPolicy.GetUsableParts(partExecution.Result)
                    : new List<ScraperMoviePart>();
                var detailMetadata = CompositeSeasonMatchService.GetSourceMetadata(detailExecution.Result);
                response.SourceMetadata = SourceMetadata.MergeDetailWithSnapshot(
                    detailMetadata, candidateEvidence.SourceMetadata);
                for (var index = 0; index < parts.Count; index++)
                {
                    var part = parts[index];
                    var token = CandidateEvidence.RegisterMoviePart(
                        request.SelectionEvidenceToken,
                        movie.Id.ToString(),
                        scraper.ProviderId,
                        request.CandidateId,
                        part);
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    var isDefault = response.MovieParts.Count == 0;
                    response.MovieParts.Add(new DanmuMoviePartChoice
                    {
                        Token = token,
                        PartTitle = part.Title ?? string.Empty,
                        Index = part.Index,
                        Selected = isDefault,
                    });
                }

                response.PartTitle = response.MovieParts.FirstOrDefault(choice => choice.Selected)?.PartTitle ??
                    string.Empty;
                response.Status = "ready";
                response.Message = response.MovieParts.Count > 1
                    ? "Movie parts are ready for optional selection."
                    : "Movie candidate is ready.";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] Movie part preview failed: movie={1}", scraper.Name, movie.Name);
                response.Status = "unresolved";
                response.Message = "Movie part preview could not be resolved.";
                return response;
            }
        }

        /// <summary>
        /// Resolves one selected Episode candidate without confusing an
        /// Episode-local ProviderId lookup token with a Season/media id.
        /// </summary>
        private static Task<ScraperMedia> ResolveSelectedCandidateDetailAsync(
            Episode episode,
            Season season,
            AbstractScraper scraper,
            string candidateId)
        {
            var directEpisodeProviderId = episode?.GetProviderId(scraper?.ProviderId);
            var isDirectEpisodeProviderId = !string.IsNullOrWhiteSpace(candidateId) &&
                string.Equals(directEpisodeProviderId, candidateId, StringComparison.OrdinalIgnoreCase);
            return isDirectEpisodeProviderId
                ? DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                    scraper, episode, candidateId, episode?.IndexNumber ?? 0)
                : scraper.GetMedia(season, candidateId);
        }

        private async Task<DanmuMatchPreviewResult> GetMatchPreviewCore(
            DanmuParams request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                throw new ResourceNotFoundException();
            }

            var item = _libraryManager.GetItemById(request.Id);
            if (item == null)
            {
                throw new ResourceNotFoundException();
            }

            var result = new DanmuMatchPreviewResult
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                ItemId = item.Id.ToString(),
                ItemName = item.Name ?? string.Empty,
                ItemType = item is Series ? "Series" : item is Season ? "Season" :
                    item is Episode ? "Episode" : item is Movie ? "Movie" : item.GetType().Name,
            };
            var rematch = IsRematch(request);
            var manualKeyword = IsManualKeyword(request);
            if (request.ParentTitleRematch)
            {
                if (HasMixedParentTitleRematchIntent(request))
                {
                    result.Status = "invalid_request";
                    result.Message = "Parent-title rematch cannot be combined with another search or selection intent.";
                    result.CanStart = false;
                    result.SearchCompletionDiagnostics.Add(new DanmuSearchCompletionDiagnostic
                    {
                        Status = "invalid_request",
                        Message = result.Message,
                    });
                    return result;
                }

                if (!TryResolveParentTitleRematchTarget(item, request,
                        out var rematchSeason, out var authoritativeParentSeries,
                        out var rematchResolutionError))
                {
                    var parentTitleUnavailable = string.Equals(
                        rematchResolutionError, "parent-title-unavailable", StringComparison.Ordinal);
                    result.Status = parentTitleUnavailable ? "incomplete" : "invalid_request";
                    result.DecisionReason = parentTitleUnavailable
                        ? "parent-title-unavailable" : string.Empty;
                    result.Message = parentTitleUnavailable
                        ? "The authoritative parent Series title is unavailable; retry after refreshing library metadata."
                        : "Parent-title rematch requires exactly one authoritative target Season.";
                    result.CanStart = false;
                    result.SearchCompletionDiagnostics.Add(new DanmuSearchCompletionDiagnostic
                    {
                        Status = parentTitleUnavailable ? "invalid_metadata" : "invalid_request",
                        Message = result.Message,
                    });
                    return result;
                }

                var parentTitleSeason = await GetSeasonMatchPreview(
                    rematchSeason,
                    authoritativeParentSeries.Name,
                    true,
                    preserveProvidedSeason: true,
                    explicitParentSeries: authoritativeParentSeries,
                    cancellationToken: cancellationToken,
                    parentCancellationToken: cancellationToken).ConfigureAwait(false);
                result.Seasons.Add(parentTitleSeason);
                result.CanStart = parentTitleSeason.AutoSelected;
                result.Status = parentTitleSeason.Status;
                result.Message = parentTitleSeason.Message;
                result.MatchIntent = parentTitleSeason.MatchIntent;
                CopyDecision(result, parentTitleSeason);
                return result;
            }

            result.MatchIntent = manualKeyword
                ? DanmuMatchIntent.ManualKeyword
                : rematch ? DanmuMatchIntent.Rematch : DanmuMatchIntent.Default;
            result.EnabledProviderIdKeys = DanmuProviderIdResolver.GetEnabledProviderIdKeys(_scraperManager.All());

            if (manualKeyword && string.IsNullOrWhiteSpace(request.Keyword))
            {
                result.Status = "invalid_request";
                result.Message = "A manual search keyword is required.";
                result.CanStart = false;
                result.SearchCompletionDiagnostics.Add(new DanmuSearchCompletionDiagnostic
                {
                    Status = "invalid_request",
                    Message = result.Message,
                });
                return result;
            }

            if (item is Movie movie)
            {
                result.Target = await GetMovieMatchPreview(
                    movie,
                    request.Keyword,
                    rematch,
                    cancellationToken,
                    manualKeyword).ConfigureAwait(false);
                if (!manualKeyword)
                {
                    StampCandidateEvidence(movie, result.Target.Candidates);
                }
                result.CanStart = result.Target.AutoSelected;
                result.Status = result.Target.Status;
                result.Message = result.Target.Message;
                CopyDecision(result, result.Target);
                return result;
            }

            if (item is Episode episode)
            {
                result.Target = await GetEpisodeMatchPreview(
                    episode,
                    request.Keyword,
                    rematch,
                    cancellationToken,
                    manualKeyword).ConfigureAwait(false);
                // Season discovery evidence is intentionally target-bound. An
                // Episode card therefore gets its own proof without re-searching
                // or resolving source media.
                if (!manualKeyword)
                {
                    StampCandidateEvidence(episode, result.Target.Candidates);
                }
                result.CanStart = result.Target.AutoSelected;
                result.Status = result.Target.Status;
                result.Message = result.Target.Message;
                CopyDecision(result, result.Target);
                return result;
            }

            var seasons = new List<Season>();
            if (item is Season season)
            {
                seasons.Add(season);
            }
            else if (item is Series series)
            {
                // The Series helper returns lightweight Season projections that
                // can omit ProviderIds. Query the library directly without a
                // DTO projection for provider-ID
                // precedence, while retaining the existing filter/order/UI
                // context selection below.
                seasons.AddRange(_libraryManager.GetItemList(new InternalItemsQuery
                {
                    ParentIds = new[] { series.InternalId },
                    IncludeItemTypes = new[] { "Season" },
                    Recursive = false,
                })
                    .OfType<Season>()
                    .OrderBy(x => x.IndexNumber ?? int.MaxValue));

                // 某些 Emby 返回的季度对象无法再仅凭 ItemId 从全局媒体库回查。
                // 手动匹配时前端同时提交父剧和季度上下文，用于重新定位用户正在调整的季度。
                if (request.SeasonNumber.HasValue || request.SeasonYear.HasValue ||
                    !string.IsNullOrWhiteSpace(request.SeasonName))
                {
                    var selectedSeason = SelectSeasonByContext(seasons, request);
                    seasons = selectedSeason == null
                        ? new List<Season>()
                        : new List<Season> { selectedSeason };
                }

                // r5 whole-Series target enumeration is intentionally limited
                // to known positive Seasons. Season 0 remains available only
                // through an explicitly selected Season item.
                seasons = seasons.Where(candidate =>
                        candidate.IndexNumber.HasValue && candidate.IndexNumber.Value > 0)
                    .ToList();
            }
            else
            {
                result.Status = "unsupported";
                result.Message = "仅支持电视剧和季";
                return result;
            }

            if (manualKeyword && item is Series && seasons.Count != 1)
            {
                result.Status = "invalid_request";
                result.Message = "Manual keyword Series search requires exactly one target Season.";
                result.CanStart = false;
                return result;
            }

            var targetRequests = seasons.Select(currentSeason =>
            {
                var targetId = currentSeason.Id.ToString();
                return new CompositeSeasonTargetRequest
                {
                    SeasonId = targetId,
                    BuildPreviewAsync = (targetCancellation, parentCancellation) =>
                        manualKeyword
                            ? ShouldUseCompositeSeasonPlanPreview(request)
                                ? GetCompositeSeasonPlanPreview(currentSeason, request, targetCancellation,
                                    parentCancellation, null)
                                : GetSeasonMatchPreview(
                                    currentSeason,
                                    request.Keyword,
                                    true,
                                    cancellationToken: targetCancellation,
                                    parentCancellationToken: parentCancellation,
                                    manualKeywordDiscovery: true,
                                    evidenceTarget: currentSeason)
                            : !string.IsNullOrWhiteSpace(request.Site) &&
                        !string.IsNullOrWhiteSpace(request.CandidateId)
                            ? GetSelectedSeasonCandidatePlanPreview(currentSeason, request, targetCancellation,
                                null)
                            : ShouldUseCompositeSeasonPlanPreview(request)
                            ? GetCompositeSeasonPlanPreview(currentSeason, request, targetCancellation,
                                parentCancellation, null)
                            : GetSeasonMatchPreview(
                                currentSeason,
                                request.Keyword,
                                rematch,
                                item is Series,
                                item as Series,
                                request.CompositeStartEpisodeItemId,
                                request.CompositeEpisodeCount,
                                targetCancellation,
                                parentCancellationToken: parentCancellation,
                                targetOwnershipExclusions: null),
                };
            }).ToList();
            result.Seasons.AddRange(await CompositeSeasonTargetSetCoordinator.BuildAsync(
                targetRequests, cancellationToken).ConfigureAwait(false));

            if (TryApplySingleManualKeywordSeasonSummary(result, manualKeyword))
            {
                return result;
            }

            if (item is Season && result.Seasons.Count == 1)
            {
                CopyDecision(result, result.Seasons[0]);
            }

            result.CanStart = result.Seasons.Count > 0 && result.Seasons.All(x => x.AutoSelected);
            if (result.CanStart)
            {
                result.Status = "matched";
                result.Message = item is Series
                    ? "所有季度都已找到高置信度结果"
                    : result.Seasons[0].Message;
            }
            else
            {
                if (item is Series && result.Seasons.Any(x => string.Equals(
                    x.Status, "cancelled", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Status = "cancelled";
                    result.Message = "Series search was cancelled; no provisional result can be confirmed.";
                }
                else if (item is Series && result.Seasons.Any(x =>
                    string.Equals(x.Status, "incomplete", StringComparison.OrdinalIgnoreCase) ||
                    x.DecisionReason.StartsWith("partial-", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.DecisionReason, "retryable-incomplete", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Status = "incomplete";
                    result.Message = "Some provider searches were incomplete; completed-provider candidates remain available for review.";
                }
                else if (item is Series)
                {
                    result.Status = result.Seasons.Any(x => x.AutoSelected) ? "partial" :
                        result.Seasons.Any(x => x.Status == "ambiguous") ? "ambiguous" : "no_match";
                    result.Message = "所有季度均已完成搜索；无法唯一匹配的季度需要分别手动选择";
                }
                else
                {
                    result.Status = result.Seasons.Any(x => x.Status == "ambiguous") ? "ambiguous" : "no_match";
                    result.Message = result.Seasons.FirstOrDefault()?.Message ?? "没有可用的匹配结果";
                }
            }

            return result;
        }

        // Candidate-card inspection is intentionally narrower than selected
        // candidate planning: validate preview evidence first, then resolve one
        // candidate and return no binding, plan, persistence, or download state.
        private async Task<DanmuMatchCandidateDetailResult> GetMatchCandidateDetails(DanmuParams request)
        {
            var response = new DanmuMatchCandidateDetailResult
            {
                Generation = request?.Generation ?? string.Empty,
            };
            var targetId = !string.IsNullOrWhiteSpace(request?.ItemId) ? request.ItemId : request?.Id;
            var evidenceToken = !string.IsNullOrWhiteSpace(request?.CandidateEvidence)
                ? request.CandidateEvidence : request?.SelectionEvidenceToken;
            if (string.IsNullOrWhiteSpace(targetId) || !IsSafeMatchCandidateSite(request?.Site) ||
                !IsSafeMatchCandidateId(request?.CandidateId) || string.IsNullOrWhiteSpace(evidenceToken))
            {
                response.Message = "invalid-candidate-detail-request";
                response.Retryable = true;
                return response;
            }

            var target = _libraryManager.GetItemById(targetId);
            var season = target as Season;
            var episode = target as Episode;
            if (episode != null) season = episode.GetParent() as Season;
            if (target == null || season == null)
            {
                response.Message = "candidate-detail-target-not-found";
                response.Retryable = true;
                return response;
            }

            var scraper = _scraperManager.All().FirstOrDefault(candidate => candidate != null &&
                string.Equals(candidate.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null)
            {
                response.Message = "candidate-detail-provider-disabled";
                response.Retryable = true;
                return response;
            }

            // This check is deliberately before ResolveMatchCandidateDetailsMediaAsync.
            // It prevents a forged provider/id from becoming an upstream request.
            if (!CandidateEvidence.TryResolve(evidenceToken, target.Id.ToString(), scraper.ProviderId,
                    request.CandidateId, out _))
            {
                response.Message = "candidate-detail-evidence-stale";
                response.Retryable = true;
                return response;
            }

            try
            {
                var media = await ResolveMatchCandidateDetailsMediaAsync(
                    target, season, scraper, request.CandidateId).ConfigureAwait(false);
                response.SourceEpisodes = (media?.Episodes ?? new List<ScraperEpisode>())
                    .Where(source => source != null && !string.IsNullOrWhiteSpace(source.Id) &&
                        !string.IsNullOrWhiteSpace(source.CommentId))
                    .OrderBy(source => source.EpisodeNumber ?? int.MaxValue)
                    .ThenBy(source => source.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(source => new DanmuSelectedCandidateSourceEpisode
                    {
                        Id = source.Id,
                        Number = source.EpisodeNumber,
                        Title = source.Title ?? string.Empty,
                    })
                    .ToList();
                if (response.SourceEpisodes.Count == 0)
                {
                    response.Message = "candidate-detail-no-episodes";
                    response.Retryable = true;
                    return response;
                }
                if (episode != null)
                {
                    response.SuggestedEpisodeNumber = DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(
                        episode.IndexNumber, media.Episodes);
                    if (!response.SuggestedEpisodeNumber.HasValue && response.SourceEpisodes.Count == 1)
                    {
                        response.SuggestedEpisodeNumber = response.SourceEpisodes[0].Number;
                    }
                }
                response.Success = true;
                response.Message = "candidate-detail-ready";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] Candidate detail resolution failed for target={1}",
                    scraper.Name, target.Name);
                response.Message = "candidate-detail-unresolved";
                response.Retryable = true;
                return response;
            }
        }

        private static Task<ScraperMedia> ResolveMatchCandidateDetailsMediaAsync(
            BaseItem target, Season season, AbstractScraper scraper, string candidateId)
        {
            var episode = target as Episode;
            var directEpisodeProviderId = episode?.GetProviderId(scraper?.ProviderId);
            return !string.IsNullOrWhiteSpace(directEpisodeProviderId) &&
                   string.Equals(directEpisodeProviderId, candidateId, StringComparison.OrdinalIgnoreCase)
                ? DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                    scraper, episode, candidateId, episode.IndexNumber ?? 0)
                : scraper.GetMedia(season, candidateId);
        }

        private static bool IsSafeMatchCandidateId(string candidateId)
        {
            return !string.IsNullOrWhiteSpace(candidateId) && candidateId.Length <= 512 &&
                candidateId.All(character => !char.IsControl(character)) &&
                candidateId.IndexOfAny(new[] { '/', '\\', ':', '?', '#' }) < 0 &&
                candidateId.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static bool IsSafeMatchCandidateSite(string site)
        {
            return !string.IsNullOrWhiteSpace(site) && site.Length <= 128 &&
                site.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-');
        }

        /// <summary>
        /// Read-only phase two for one selected Season candidate.  It resolves
        /// only that candidate and converts it to the same explicit plan used
        /// by Series and Season downloads.
        /// </summary>
        private async Task<DanmuSeasonMatchResult> GetSelectedSeasonCandidatePlanPreview(
            Season season,
            DanmuParams request,
            CancellationToken cancellationToken,
            IReadOnlyCollection<string> targetOwnershipExclusions = null)
        {
            var latest = _libraryManager.GetItemById(season.Id) as Season ?? season;
            var parent = latest.GetParent() as Series;
            var scopeAvailable = TryBuildOwnedPlanningContext(latest, out var planningContext,
                out var scopeError);
            var episodes = planningContext?.Episodes ?? new List<Episode>();
            var scraper = _scraperManager.All().FirstOrDefault(candidate => string.Equals(
                candidate.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            var result = new DanmuSeasonMatchResult
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                PlanGeneration = request.PlanGeneration,
                SeasonId = latest.Id.ToString(),
                SeriesId = parent?.Id.ToString() ?? string.Empty,
                SeasonName = latest.Name ?? string.Empty,
                SeriesName = parent?.Name ?? string.Empty,
                SeasonNumber = latest.IndexNumber,
                Year = latest.ProductionYear,
                EpisodeCount = episodes.Count,
                Status = "retryable",
                DecisionReason = "selected-candidate-detail",
                MatchOrigin = "manual",
                SelectedId = request.CandidateId ?? string.Empty,
                SelectedSite = request.Site ?? string.Empty,
                SelectedSiteName = scraper?.ProviderName ?? string.Empty,
            };
            ApplySeasonScopeSummary(result, planningContext);
            if (!scopeAvailable)
            {
                result.Status = "invalid_request";
                result.DecisionReason = scopeError;
                result.Message = "The target Season Episode inventory is unavailable or has no eligible Episodes.";
                result.SearchErrors.Add(scopeError);
                return result;
            }
            if (!DanmuMappingProtocol.IsCurrent(request.MappingProtocolVersion) ||
                !SeasonPlanGenerations.IsCurrent(latest.Id.ToString(), request.PlanGeneration))
            {
                result.Status = "stale_protocol";
                result.DecisionReason = "stale-protocol-generation";
                result.Message = "The Season mapping draft is stale; search again.";
                result.SearchErrors.Add("stale-protocol-generation");
                return result;
            }
            if (scraper == null || string.IsNullOrWhiteSpace(request.CandidateId))
            {
                result.Message = "The selected Season provider or candidate is unavailable.";
                result.SearchErrors.Add("selected-season-candidate-invalid");
                return result;
            }

            if (!CandidateEvidence.TryResolve(request.SelectionEvidenceToken,
                    latest.Id.ToString(), scraper.ProviderId, request.CandidateId,
                    out var candidateEvidence))
            {
                result.Message = "Selected candidate evidence expired or does not belong to this Season; search again.";
                result.SearchErrors.Add("selection-evidence-required");
                result.DecisionReason = "selection-evidence-required";
                return result;
            }

            var candidate = new DanmuMatchCandidate
            {
                Id = request.CandidateId,
                Site = scraper.ProviderId,
                SiteName = scraper.ProviderName,
                Name = "Selected Season candidate",
                MatchOrigin = "manual",
                DecisionReason = "manual-selection",
                Score = candidateEvidence.MatchScore,
                MatchScore = candidateEvidence.MatchScore,
                ScoreOrigin = candidateEvidence.ScoreOrigin,
                SelectionEvidenceToken = request.SelectionEvidenceToken,
            };
            result.Candidates.Add(candidate);
            await PopulateCompositePreviewIfRequired(
                latest, result, candidate, "manual",
                request.CompositeStartEpisodeItemId, request.CompositeEpisodeCount,
                cancellationToken, targetOwnershipExclusions).ConfigureAwait(false);
            if (result.CompositePlan == null)
            {
                result.Status = cancellationToken.IsCancellationRequested ? "cancelled" : "retryable";
                result.Message = "Selected Season candidate detail could not be resolved; retry without changing the draft.";
                result.DecisionReason = "selected-candidate-detail-incomplete";
                return result;
            }

            result.AutoSelected = result.CompositePlan.UnmatchedRuns.Count == 0;
            if (result.CompositePlan.UnmatchedRuns.Count == 0)
            {
                result.Status = "matched";
                result.DecisionReason = "authoritative-season-plan";
                result.Message = "Selected Season candidate was resolved to an authoritative Episode mapping plan.";
            }
            return result;
        }

        private async Task<DanmuItemMatchResult> GetMovieMatchPreview(
            Movie movie,
            string keywordOverride,
            bool forceSearch,
            CancellationToken cancellationToken,
            bool manualKeywordDiscovery = false)
        {
            var latest = _libraryManager.GetItemById(movie.Id) as Movie ?? movie;
            var result = new DanmuItemMatchResult
            {
                ItemId = latest.Id.ToString(),
                ItemName = latest.Name ?? string.Empty,
                ItemType = "Movie",
                ParentName = latest.Name ?? string.Empty,
                Year = latest.ProductionYear,
                Keyword = string.IsNullOrWhiteSpace(keywordOverride) ? latest.Name ?? string.Empty : keywordOverride,
            };
            var scrapers = _scraperManager.All();
            if (manualKeywordDiscovery)
            {
                result.MatchIntent = DanmuMatchIntent.ManualKeyword;
                result.EnabledProviderIdKeys = DanmuProviderIdResolver.GetEnabledProviderIdKeys(scrapers);
                var manualSearch = await DanmuMatchSearchEngine.SearchMovieAsync(
                    scrapers, latest, keywordOverride, _logger, BoundedSearchPolicy.Shared,
                    cancellationToken, latest, retainZeroScoreCandidates: true,
                    manualKeywordDiscovery: true).ConfigureAwait(false);
                ApplyManualKeywordSearchResult(result, latest, manualSearch);
                return result;
            }

            InitializeDecision(result, scrapers, forceSearch);
            if (!forceSearch)
            {
                var providerDecision = await DanmuProviderIdResolver.ResolveAsync(
                    scrapers, DanmuProviderIdResolver.GetMovieScopes(latest), _logger,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                result.SearchErrors.AddRange(providerDecision.Diagnostics);
                if (providerDecision.Candidate != null)
                {
                    ApplyProviderDecision(result, providerDecision);
                    return result;
                }
            }

            if (DanmuMatchBindingHelper.TryGetSavedManualBinding(
                    forceSearch,
                    scrapers,
                    DanmuProviderIdResolver.GetItemLocalProviderIds(latest, scrapers),
                    out var savedScraper,
                    out var manualId))
            {
                    result.Status = "bound";
                    result.Message = "使用已经保存的电影手动匹配";
                    result.AutoSelected = true;
                    result.SelectedId = manualId;
                    result.SelectedSite = savedScraper.ProviderId;
                    result.SelectedSiteName = savedScraper.ProviderName;
                    result.MatchOrigin = "binding";
                    result.DecisionReason = "binding";
                    result.Candidates.Add(new DanmuMatchCandidate
                    {
                        Id = manualId,
                        Site = savedScraper.ProviderId,
                        SiteName = savedScraper.ProviderName,
                        SourceOrder = GetSourceOrder(scrapers, savedScraper),
                        Name = "已手动绑定的电影",
                        Score = 1,
                        MatchScore = 1,
                        ScoreOrigin = DanmuMatchScoreOrigin.ExactBinding,
                        ManualBound = true,
                        MatchOrigin = "binding",
                        DecisionReason = "binding",
                        Reason = "使用已保存的手动绑定",
                    });
                    return result;
            }

            var search = await DanmuMatchSearchEngine.SearchMovieAsync(
                scrapers,
                latest,
                keywordOverride,
                _logger,
                BoundedSearchPolicy.Shared,
                cancellationToken).ConfigureAwait(false);
            result.Candidates = search.Candidates;
            result.SearchErrors.AddRange(search.SearchErrors);
            result.SearchCompletionDiagnostics.AddRange(search.CompletionDiagnostics);
            var selected = search.SelectedCandidate ??
                DanmuMatchScorer.SelectAutoCandidate(search.CanonicalCandidates);
            if (search.WasCancelled)
            {
                result.Status = "cancelled";
                result.DecisionReason = "cancelled";
                result.Message = "The interactive search was cancelled; its provisional candidates cannot be confirmed.";
                return result;
            }
            if (!search.HasCompletedProviders && !search.IsComplete)
            {
                result.Status = "incomplete";
                result.DecisionReason = string.IsNullOrWhiteSpace(search.Decision)
                    ? "retryable-incomplete" : search.Decision;
                result.Message = "No provider search completed; retry before selecting a candidate.";
                return result;
            }
            if (selected != null)
            {
                result.Status = "matched";
                result.Message = "已根据电影名和年份选出高置信度结果";
                result.AutoSelected = true;
                result.SelectedId = selected.Id;
                result.SelectedSite = selected.Site;
                result.SelectedSiteName = selected.SiteName;
                result.MatchOrigin = search.UsedTmdbAlias ? "tmdb-alias" : "scored";
                result.DecisionReason = search.UsedTmdbAlias
                    ? "tmdb-alias-high-confidence" : "confident-site-priority";
            }
            else if (result.Candidates.Count == 0)
            {
                result.Status = "no_match";
                result.DecisionReason = result.SearchErrors.Any(x => x.StartsWith("provider-id-unresolved", StringComparison.OrdinalIgnoreCase))
                    ? "provider-id-unresolved" : "no-candidates";
                result.Message = result.SearchErrors.Count > 0
                    ? "没有搜索到电影候选，且部分网站搜索失败"
                    : "没有搜索到电影候选，可更换关键词重试";
            }
            else
            {
                result.Status = result.Candidates[0].Score >= 0.60 ? "ambiguous" : "no_match";
                result.DecisionReason = "low-confidence";
                result.Message = result.Status == "ambiguous"
                    ? "存在多个接近的电影结果，需要手动选择"
                    : "电影自动评分不足，需要手动选择或更换关键词";
            }

            return result;
        }

        private async Task<DanmuItemMatchResult> GetEpisodeMatchPreview(
            Episode episode,
            string keywordOverride,
            bool forceSearch,
            CancellationToken cancellationToken,
            bool manualKeywordDiscovery = false)
        {
            var latest = _libraryManager.GetItemById(episode.Id) as Episode ?? episode;
            var season = latest.GetParent() as Season;
            var series = season?.GetParent() as Series;
            var authoritativeSeries = series == null
                ? null
                : _libraryManager.GetItemById(series.InternalId) as Series ?? series;
            var result = new DanmuItemMatchResult
            {
                ItemId = latest.Id.ToString(),
                ItemName = latest.Name ?? string.Empty,
                ItemType = "Episode",
                ParentName = series?.Name ?? string.Empty,
                SeriesId = series?.Id.ToString() ?? string.Empty,
                SeasonId = season?.Id.ToString() ?? string.Empty,
                SeasonName = season?.Name ?? string.Empty,
                EpisodeNumber = latest.IndexNumber,
                Year = latest.ProductionYear ?? season?.ProductionYear,
                Keyword = string.IsNullOrWhiteSpace(keywordOverride) ? series?.Name ?? string.Empty : keywordOverride,
            };
            var episodeScrapers = _scraperManager.All();
            if (manualKeywordDiscovery)
            {
                result.MatchIntent = DanmuMatchIntent.ManualKeyword;
                result.EnabledProviderIdKeys = DanmuProviderIdResolver.GetEnabledProviderIdKeys(episodeScrapers);
            }
            if (season == null)
            {
                result.Status = "unsupported";
                result.Message = "找不到本集所属季度";
                return result;
            }

            if (manualKeywordDiscovery)
            {
                var manualSeasonMatch = await GetSeasonMatchPreview(
                    season,
                    keywordOverride,
                    true,
                    explicitParentSeries: authoritativeSeries,
                    cancellationToken: cancellationToken,
                    metadataOnly: true,
                    parentCancellationToken: cancellationToken,
                    manualKeywordDiscovery: true,
                    evidenceTarget: latest).ConfigureAwait(false);
                result.Candidates = manualSeasonMatch.Candidates;
                result.SearchErrors.AddRange(manualSeasonMatch.SearchErrors);
                result.SearchCompletionDiagnostics.AddRange(manualSeasonMatch.SearchCompletionDiagnostics);
                result.Status = manualSeasonMatch.Status;
                result.Message = manualSeasonMatch.Message;
                result.DecisionReason = manualSeasonMatch.DecisionReason;
                return result;
            }

            InitializeDecision(result, episodeScrapers, forceSearch);
            if (!forceSearch)
            {
                var providerDecision = await DanmuProviderIdResolver.ResolveAsync(
                    episodeScrapers,
                    DanmuProviderIdResolver.GetSingleEpisodeDirectScopes(latest),
                    _logger,
                    authoritativeSeries,
                    cancellationToken).ConfigureAwait(false);
                result.SearchErrors.AddRange(providerDecision.Diagnostics);
                if (providerDecision.Candidate != null)
                {
                    providerDecision.Candidate.SuggestedEpisodeNumber =
                        DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(latest.IndexNumber, providerDecision.Media?.Episodes);
                    if (!providerDecision.Candidate.SuggestedEpisodeNumber.HasValue &&
                        string.Equals(providerDecision.ResolvedScopeType, "Episode", StringComparison.OrdinalIgnoreCase))
                    {
                        // A direct Episode ProviderId is already an exact selection;
                        // use the resolved one-item source number when local metadata
                        // has no reliable IndexNumber.
                        providerDecision.Candidate.SuggestedEpisodeNumber = providerDecision.Media?.Episodes?
                            .FirstOrDefault()?.EpisodeNumber;
                    }
                    ApplyProviderDecision(result, providerDecision);
                    if (!providerDecision.Candidate.SuggestedEpisodeNumber.HasValue)
                    {
                        result.Status = "ambiguous";
                        result.AutoSelected = false;
                        result.DecisionReason = "provider-id";
                        result.Message = "ProviderId 已解析，但需要选择来源集数";
                    }
                    return result;
                }
            }

            var seasonMatch = await GetSeasonMatchPreview(
                season, keywordOverride, forceSearch, false, authoritativeSeries,
                cancellationToken: cancellationToken,
                metadataOnly: true).ConfigureAwait(false);
            result.Candidates = seasonMatch.Candidates;
            result.SearchErrors.AddRange(seasonMatch.SearchErrors);
            CopyDecision(result, seasonMatch);
            // Candidate discovery is metadata-only. Exact source-episode
            // detail is resolved later for one explicit candidate.
            DanmuMatchCandidate selected = null;
            if (selected != null)
            {
                result.Status = seasonMatch.Status;
                result.Message = "已匹配本集候选和来源集数";
                result.AutoSelected = true;
                result.SelectedId = selected.Id;
                result.SelectedSite = selected.Site;
                result.SelectedSiteName = selected.SiteName;
            }
            else if (result.Candidates.Count == 0)
            {
                result.Status = "no_match";
                result.Message = seasonMatch.Message;
            }
            else
            {
                result.Status = "ambiguous";
                result.Message = "请选择候选并确认来源集数";
            }

            return result;
        }

        private async Task<DanmuSeasonMatchResult> GetSeasonMatchPreview(
            Season season,
            string keywordOverride,
            bool forceSearch,
            bool preserveProvidedSeason = false,
            Series explicitParentSeries = null,
            string compositeStartEpisodeItemId = null,
            int compositeEpisodeCount = 0,
            CancellationToken cancellationToken = default(CancellationToken),
            bool metadataOnly = false,
            CancellationToken parentCancellationToken = default(CancellationToken),
            IReadOnlyCollection<string> targetOwnershipExclusions = null,
            bool manualKeywordDiscovery = false,
            BaseItem evidenceTarget = null)
        {
            // Series preview supplies an authoritative non-projected Season
            // object. Do not replace it with the Guid lookup projection, which
            // may discard ProviderIds. Direct Season/Episode behavior remains
            // unchanged.
            var latest = preserveProvidedSeason
                ? season
                : _libraryManager.GetItemById(season.Id) as Season ?? season;
            var parent = latest.GetParent();
            var parentSeries = explicitParentSeries ?? parent as Series;
            var authoritativeParentSeries = parentSeries == null
                ? null
                : _libraryManager.GetItemById(parentSeries.InternalId) as Series ?? parentSeries;
            var seriesName = authoritativeParentSeries?.Name ?? string.Empty;
            var seasonName = latest.Name ?? seriesName;
            var scopeAvailable = TryBuildOwnedPlanningContext(latest, out var planningContext,
                out var scopeError);
            var expectedEpisodes = planningContext?.Episodes.Count ?? 0;
            var expectedYear = latest.ProductionYear;
            if (!expectedYear.HasValue || expectedYear.Value <= 0)
            {
                expectedYear = (planningContext?.Episodes ?? new List<Episode>())
                    .Where(x => x.ProductionYear.HasValue && x.ProductionYear.Value > 0)
                    .Select(x => x.ProductionYear)
                    .FirstOrDefault();
            }

            var result = new DanmuSeasonMatchResult
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                PlanGeneration = SeasonPlanGenerations.Begin(latest.Id.ToString()),
                SeasonId = latest.Id.ToString(),
                SeriesId = authoritativeParentSeries?.Id.ToString() ?? string.Empty,
                SeasonName = seasonName,
                SeriesName = seriesName,
                SeasonNumber = latest.IndexNumber,
                Year = expectedYear,
                EpisodeCount = expectedEpisodes,
                Keyword = manualKeywordDiscovery
                    ? keywordOverride ?? string.Empty
                    : DanmuMatchScorer.ExtractSeasonKeyword(seriesName, seasonName),
            };
            if (manualKeywordDiscovery)
            {
                result.MatchIntent = DanmuMatchIntent.ManualKeyword;
                result.AutoSelected = false;
                result.EnabledProviderIdKeys = DanmuProviderIdResolver.GetEnabledProviderIdKeys(
                    _scraperManager.All());
            }
            ApplySeasonScopeSummary(result, planningContext);
            if (!scopeAvailable)
            {
                result.Status = "invalid_request";
                result.DecisionReason = scopeError;
                result.Message = "The target Season Episode inventory is unavailable or has no eligible Episodes.";
                result.SearchErrors.Add(scopeError);
                return result;
            }

            var scrapers = _scraperManager.All();
            // r4 Series/Season planning is identifier-free. Durable markers and
            // every local ProviderId/manual binding remain metadata only.
            // Episode phase-one discovery is metadata-only: it must bypass
            // Season ProviderIds/manual bindings and never resolve media detail.
            InitializeDecision(result, scrapers, true);

            var search = await DanmuMatchSearchEngine.SearchSeasonAsync(
                scrapers,
                seriesName,
                seasonName,
                expectedYear,
                expectedEpisodes,
                keywordOverride,
                _logger,
                BoundedSearchPolicy.Shared,
                cancellationToken,
                parentCancellationToken == default(CancellationToken) ? cancellationToken : parentCancellationToken,
                new[] { authoritativeParentSeries?.OriginalTitle },
                new[] { latest.OriginalTitle },
                latest,
                manualKeywordDiscovery: manualKeywordDiscovery)
                .ConfigureAwait(false);
            if (manualKeywordDiscovery)
            {
                ApplyManualKeywordSearchResult(result, evidenceTarget ?? latest, search);
                return result;
            }
            result.SearchErrors.AddRange(search.SearchErrors);
            result.SearchCompletionDiagnostics.AddRange(search.CompletionDiagnostics);
            var selected = search.SelectedCandidate ??
                DanmuMatchScorer.SelectAutoCandidate(search.CanonicalCandidates);
            if (search.WasCancelled)
            {
                result.Status = "cancelled";
                result.DecisionReason = "cancelled";
                result.Message = "The interactive search was cancelled; its provisional candidates cannot be confirmed.";
                return result;
            }
            if (!search.HasCompletedProviders && !search.IsComplete)
            {
                result.Status = "incomplete";
                result.DecisionReason = string.IsNullOrWhiteSpace(search.Decision)
                    ? "retryable-incomplete" : search.Decision;
                result.Message = "No provider search completed; retry before selecting a candidate.";
                return result;
            }

            if (search.ParentTitleRematchAvailable && selected == null)
            {
                result.SearchCompletionDiagnostics = result.SearchCompletionDiagnostics
                    .Where(diagnostic => !IsTmdbAliasDiagnostic(diagnostic))
                    .ToList();
                result.SearchErrors = result.SearchErrors
                    .Where(error => !IsTmdbAliasError(error))
                    .ToList();
                result.ParentTitleRematchAvailable = true;
                result.Status = "no_match";
                result.Message = "未找到匹配结果，可重新匹配。";
                result.AutoSelected = false;
                result.MatchOrigin = string.Empty;
                result.DecisionReason = string.Empty;
                result.SelectedCandidate = null;
                result.SelectedId = string.Empty;
                result.SelectedSite = string.Empty;
                result.SelectedSiteName = string.Empty;
                result.Candidates.Clear();
                return result;
            }

            result.Candidates = search.Candidates;
            StampSeasonCandidateEvidence(latest, result.Candidates);

            if (selected != null)
            {
                result.Status = "matched";
                result.Message = "已根据季名关键词、父剧名、年份和集数选出高置信度结果";
                result.AutoSelected = true;
                result.SelectedId = selected.Id;
                result.SelectedSite = selected.Site;
                result.SelectedSiteName = selected.SiteName;
                result.MatchOrigin = search.UsedTmdbAlias ? "tmdb-alias" : "scored";
                result.DecisionReason = search.UsedTmdbAlias
                    ? "tmdb-alias-high-confidence" : "confident-site-priority";
                if (!metadataOnly)
                {
                    await PopulateCompositePreviewIfRequired(latest, result, selected, "scored",
                        compositeStartEpisodeItemId, compositeEpisodeCount,
                        cancellationToken, targetOwnershipExclusions).ConfigureAwait(false);
                }
                return result;
            }

            if (result.Candidates.Count == 0)
            {
                result.Status = "no_match";
                result.DecisionReason = result.SearchErrors.Any(x => x.StartsWith("provider-id-unresolved", StringComparison.OrdinalIgnoreCase))
                    ? "provider-id-unresolved" : "no-candidates";
                result.Message = result.SearchErrors.Count > 0
                    ? "没有搜索到候选项目，且部分网站搜索失败"
                    : "没有搜索到候选项目，可输入其他关键词重试";
                return result;
            }

            result.Status = result.Candidates[0].Score >= 0.60 ? "ambiguous" : "no_match";
            result.DecisionReason = "low-confidence";
            result.Message = result.Status == "ambiguous"
                ? "存在多个接近的结果，需要手动选择"
                : "自动评分不足，需要手动选择或换关键词搜索";
            return result;
        }

        /// <summary>
        /// Rebuilds a browser's compact composite intent against live source
        /// responses.  This branch deliberately does not let a normal Season
        /// candidate overwrite the already confirmed virtual-season plan.
        /// </summary>
        private async Task<DanmuSeasonMatchResult> GetCompositeSeasonPlanPreview(
            Season season,
            DanmuParams request,
            CancellationToken cancellationToken,
            CancellationToken parentCancellationToken = default(CancellationToken),
            IReadOnlyCollection<string> targetOwnershipExclusions = null)
        {
            var latest = _libraryManager.GetItemById(season.Id) as Season ?? season;
            var parent = latest.GetParent() as Series;
            var effectiveExclusions = MergeEpisodeExclusions(
                request.ParsedExcludedLocalEpisodeItemIds, targetOwnershipExclusions);
            var build = await BuildCompositePlanAsync(latest, request.ParsedCompositeSelections, false,
                    effectiveExclusions, cancellationToken)
                .ConfigureAwait(false);
            var episodes = build.Episodes;
            var response = new DanmuSeasonMatchResult
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                PlanGeneration = request.PlanGeneration,
                SeasonId = latest.Id.ToString(),
                SeriesId = parent?.Id.ToString() ?? string.Empty,
                SeasonName = latest.Name ?? string.Empty,
                SeriesName = parent?.Name ?? string.Empty,
                SeasonNumber = latest.IndexNumber,
                Year = latest.ProductionYear,
                EpisodeCount = episodes.Count,
                Keyword = request.Keyword ?? string.Empty,
                RequiresCompositeMapping = true,
                Status = "partial",
                AutoSelected = false,
                DecisionReason = "composite-season",
            };
            if (IsManualKeyword(request))
            {
                response.MatchIntent = DanmuMatchIntent.ManualKeyword;
            }
            ApplySeasonScopeSummary(response, build.Context);
            if (!DanmuMappingProtocol.IsCurrent(request.MappingProtocolVersion) ||
                !SeasonPlanGenerations.IsCurrent(latest.Id.ToString(), request.PlanGeneration))
            {
                response.Status = "stale_protocol";
                response.DecisionReason = "stale-protocol-generation";
                response.Message = "The Season mapping draft is stale; search again.";
                response.SearchErrors.Add("stale-protocol-generation");
                return response;
            }
            if (build.Plan == null)
            {
                response.Status = "ambiguous";
                response.Message = "复合季映射需要重新确认：" + build.Error;
                response.SearchErrors.Add("composite-plan-invalid:" + build.Error);
                return response;
            }

            response.CompositePlan = build.Plan;
            response.PlanFingerprint = build.PlanFingerprint;
            RegisterPreviewPlanFingerprint(latest.Id.ToString(), response.PlanGeneration,
                build.PlanFingerprint);
            response.CompositeGroups = CompositeSeasonMatchService.ToGroups(
                build.Plan, build.Episodes, build.SourceEpisodeNames);
            response.Message = build.Plan.UnmatchedRuns.Count == 0
                ? "复合季映射已由服务器重新验证。"
                : "复合季映射已保留；其余临时季可继续匹配。";

            if (!IsTemporaryRangeSearch(request))
            {
                return response;
            }

            // Browser range fields are intent only. Validate them against the
            // same exclusion-aware plan returned to the browser before making
            // a provider call, so a stale dialog cannot search a shifted or
            // shortened portion of an unmatched run.
            if (!DanmuTemporaryRangeSearchPolicy.TryResolveUnmatchedRun(
                    build.Plan,
                    request.CompositeStartEpisodeItemId,
                    request.CompositeEpisodeCount,
                    out var range,
                    out var rangeError))
            {
                response.Status = "invalid_request";
                response.Message = "Temporary range is no longer valid: " + rangeError;
                response.DecisionReason = "temporary-range-invalid";
                response.SearchErrors.Add("temporary-range-invalid:" + rangeError);
                return response;
            }

            // Respect an edited keyword. For an initial request, use Series
            // title first, then Season title. Two empty titles are retryable
            // input, never an empty provider API call.
            var manualKeyword = IsManualKeyword(request);
            string searchKeyword;
            if (manualKeyword)
            {
                searchKeyword = request.Keyword;
            }
            else if (!DanmuTemporaryRangeSearchPolicy.TryResolveSearchKeyword(
                         request.Keyword,
                         parent?.Name,
                         latest.Name,
                         out searchKeyword))
            {
                response.Status = "retryable";
                response.Message = "Temporary range search needs a Series or Season title.";
                response.DecisionReason = "temporary-range-keyword-required";
                response.SearchErrors.Add("temporary-range-keyword-required");
                return response;
            }

            // Temporary ranges are never allowed to reuse a Season ProviderId
            // or plugin binding. They search only the verified current run.
            if (manualKeyword)
            {
                var manualSearch = await DanmuMatchSearchEngine.SearchSeasonAsync(
                    _scraperManager.All(), parent?.Name ?? string.Empty, latest.Name ?? string.Empty,
                    latest.ProductionYear, range.Episodes.Count, searchKeyword, _logger,
                    BoundedSearchPolicy.Shared, cancellationToken,
                    parentCancellationToken == default(CancellationToken)
                        ? cancellationToken
                        : parentCancellationToken,
                    new[] { parent?.OriginalTitle },
                    new[] { latest.OriginalTitle },
                    latest,
                    manualKeywordDiscovery: true).ConfigureAwait(false);
                response.Keyword = searchKeyword;
                ApplyManualKeywordSearchResult(response, latest, manualSearch);
                return response;
            }

            var search = await DanmuMatchSearchEngine.SearchSeasonAsync(
                    _scraperManager.All(), parent?.Name ?? string.Empty, latest.Name ?? string.Empty,
                    latest.ProductionYear, range.Episodes.Count, searchKeyword, _logger,
                    BoundedSearchPolicy.Shared, cancellationToken,
                    parentCancellationToken == default(CancellationToken) ? cancellationToken : parentCancellationToken,
                    new[] { parent?.OriginalTitle },
                    new[] { latest.OriginalTitle },
                    latest)
                .ConfigureAwait(false);
            response.Keyword = searchKeyword;
            response.Candidates = search.Candidates;
            StampSeasonCandidateEvidence(latest, response.Candidates);
            response.SearchErrors.AddRange(search.SearchErrors);
            response.SearchCompletionDiagnostics.AddRange(search.CompletionDiagnostics);
            if (search.WasCancelled)
            {
                response.Status = "cancelled";
                response.AutoSelected = false;
                response.DecisionReason = "cancelled";
                response.Message = "Temporary range search was cancelled; the draft was not changed.";
                return response;
            }
            if (!search.HasCompletedProviders && !search.IsComplete)
            {
                response.Status = "retryable";
                response.AutoSelected = false;
                response.DecisionReason = "search-incomplete";
                response.Message = "Temporary range search did not complete; retry without changing the draft.";
                response.SearchErrors.Add("search-incomplete");
                return response;
            }
            response.Status = response.Candidates.Count == 0 ? "no_match" : "ambiguous";
            response.DecisionReason = response.Candidates.Count == 0 ? "no-candidates" : "manual-selection-required";
            response.Message = response.Candidates.Count == 0
                ? "Temporary range search returned no candidates."
                : "Choose a candidate to validate the temporary range mapping.";
            return response;
        }

        /// <summary>
        /// Keeps the normal one-source response intact. When that source covers
        /// only part of the local Season (or the Season was previously marked
        /// composite), expose the verified portion plus an explicit temporary
        /// group for the remainder.
        /// </summary>
        private async Task PopulateCompositePreviewIfRequired(
            Season season,
            DanmuSeasonMatchResult result,
            DanmuMatchCandidate candidate,
            string origin,
            string compositeStartEpisodeItemId = null,
            int compositeEpisodeCount = 0,
            CancellationToken cancellationToken = default(CancellationToken),
            IReadOnlyCollection<string> targetOwnershipExclusions = null)
        {
            if (season == null || result == null || candidate == null ||
                string.IsNullOrWhiteSpace(candidate.Site) || string.IsNullOrWhiteSpace(candidate.Id))
            {
                return;
            }

            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, candidate.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null)
            {
                return;
            }

            ScraperMedia media;
            try
            {
                var resolution = await BoundedSearchPolicy.Shared.ExecuteAsync(
                    scraper.ProviderId,
                    ignored => scraper.GetMedia(season, candidate.Id),
                    cancellationToken).ConfigureAwait(false);
                if (resolution.Status != BoundedSearchExecutionStatus.Completed)
                {
                    result.SearchErrors.Add("composite-preview-detail:" +
                        resolution.Status.ToString().ToLowerInvariant());
                    return;
                }

                media = resolution.Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CompositeSeason] Preview source verification failed: season={0}, candidate={1}",
                    season.Name, candidate.Id);
                return;
            }

            var sourceEpisodes = CompositeSeasonMatchService.GetSourceEpisodes(media);
            if (!TryBuildOwnedPlanningContext(season, out var targetContext, out var targetScopeError))
            {
                result.SearchErrors.Add(targetScopeError);
                return;
            }
            ApplySeasonScopeSummary(result, targetContext);
            if (sourceEpisodes.Count == 0 || targetContext.Episodes.Count == 0)
            {
                return;
            }

            // Direct Episode identifiers are exact evidence and always win.  Do
            // not fabricate a positional continuation after sparse direct
            // evidence: expose its remaining runs for explicit user matching.
            var direct = await BuildCompositePlanAsync(season, null, false,
                targetOwnershipExclusions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (direct.Plan == null)
            {
                result.SearchErrors.Add("composite-direct-plan-invalid:" + direct.Error);
                return;
            }

            var requestedStart = compositeStartEpisodeItemId;
            if (string.IsNullOrWhiteSpace(requestedStart) && direct.Plan.UnmatchedRuns.Count == 0)
            {
                RegisterPreviewPlanFingerprint(season.Id.ToString(), result.PlanGeneration,
                    direct.PlanFingerprint);
                result.PlanFingerprint = direct.PlanFingerprint;
                result.CompositePlan = direct.Plan;
                result.CompositeGroups = CompositeSeasonMatchService.ToGroups(
                    direct.Plan, direct.Episodes, direct.SourceEpisodeNames);
                result.RequiresCompositeMapping = true;
                result.AutoSelected = false;
                result.Status = "partial";
                result.DecisionReason = "composite-season";
                result.Message = "已保留精确单集映射；其余剧集请继续匹配下方临时季。";
                return;
            }

            if (string.IsNullOrWhiteSpace(requestedStart))
            {
                requestedStart = direct.Plan.UnmatchedRuns[0].Episodes[0].ItemId;
            }

            var build = await BuildCompositePlanAsync(season, new[]
            {
                new DanmuCompositeSeasonSelection
                {
                    MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                    PlanGeneration = result.PlanGeneration,
                    LocalStartEpisodeItemId = requestedStart,
                    RequestedEpisodeCount = compositeEpisodeCount,
                    Site = scraper.ProviderId,
                    CandidateId = candidate.Id,
                    SourceStartEpisodeId = sourceEpisodes[0].EpisodeId,
                    MatchOrigin = origin ?? string.Empty,
                    SelectionEvidenceToken = candidate.SelectionEvidenceToken,
                },
            }, false, targetOwnershipExclusions, cancellationToken).ConfigureAwait(false);
            if (build.Plan == null)
            {
                result.SearchErrors.Add("composite-plan-invalid:" + build.Error);
                return;
            }

            result.CompositePlan = build.Plan;
            result.PlanFingerprint = build.PlanFingerprint;
            RegisterPreviewPlanFingerprint(season.Id.ToString(), result.PlanGeneration,
                build.PlanFingerprint);
            result.CompositeGroups = CompositeSeasonMatchService.ToGroups(
                build.Plan, build.Episodes, build.SourceEpisodeNames);
            result.RequiresCompositeMapping = true;
            if (build.Plan.UnmatchedRuns.Count > 0)
            {
                result.AutoSelected = false;
                result.Status = "partial";
                result.DecisionReason = "composite-season";
                result.Message = "来源只覆盖部分剧集；请继续匹配下方临时季。";
            }
        }

        private static bool IsRematch(DanmuParams request)
        {
            return request != null &&
                   (request.Rematch || request.Force ||
                    string.Equals(request.Mode, DanmuMatchIntent.Rematch, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsManualKeyword(DanmuParams request)
        {
            return string.Equals(request?.Mode, DanmuMatchIntent.ManualKeyword, StringComparison.Ordinal);
        }

        private static bool IsTmdbAliasDiagnostic(DanmuSearchCompletionDiagnostic diagnostic)
        {
            return IsTmdbAliasLabel(diagnostic?.Provider);
        }

        private static bool IsTmdbAliasError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            var prefix = error.TrimStart().TrimStart('[', '(', '{').Split(':', ']', ')', '}', ' ')[0];
            return IsTmdbAliasLabel(prefix);
        }

        private static bool IsTmdbAliasLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
            return normalized.StartsWith("tmdb", StringComparison.Ordinal);
        }

        private static bool HasMixedParentTitleRematchIntent(DanmuParams request)
        {
            if (request == null)
            {
                return true;
            }

            var mode = request.Mode ?? string.Empty;
            return !string.IsNullOrWhiteSpace(request.Keyword) ||
                   !string.IsNullOrWhiteSpace(request.ItemId) ||
                   (request.NeedSites?.Count ?? 0) > 0 ||
                   (!string.IsNullOrWhiteSpace(mode) &&
                    !string.Equals(mode, DanmuMatchIntent.Default, StringComparison.OrdinalIgnoreCase)) ||
                   request.Manual || request.Rematch || request.Force || request.ForceRefresh ||
                   !string.IsNullOrWhiteSpace(request.Site) ||
                   !string.IsNullOrWhiteSpace(request.CandidateId) ||
                   !string.IsNullOrWhiteSpace(request.SelectionEvidenceToken) ||
                   !string.IsNullOrWhiteSpace(request.CandidateEvidence) ||
                   !string.IsNullOrWhiteSpace(request.MoviePartToken) ||
                   !string.IsNullOrWhiteSpace(request.Generation) ||
                   request.SourceEpisodeNumber.HasValue ||
                   !string.IsNullOrWhiteSpace(request.SourceEpisodeId) ||
                   request.CompositePlan || request.ConfirmPartial ||
                   !string.IsNullOrWhiteSpace(request.CompositeSelections) ||
                   !string.IsNullOrWhiteSpace(request.ExcludedLocalEpisodeItemIds) ||
                   (request.ParsedCompositeSelections?.Count ?? 0) > 0 ||
                   (request.ParsedExcludedLocalEpisodeItemIds?.Count ?? 0) > 0 ||
                   !string.IsNullOrWhiteSpace(request.CompositeStartEpisodeItemId) ||
                   request.CompositeEpisodeCount != 0 ||
                   IsTemporaryRangeSearch(request);
        }

        private bool TryResolveParentTitleRematchTarget(
            BaseItem item,
            DanmuParams request,
            out Season season,
            out Series authoritativeParentSeries,
            out string error)
        {
            season = null;
            authoritativeParentSeries = null;
            error = "invalid-target-season";

            if (item is Season directSeason)
            {
                season = _libraryManager.GetItemById(directSeason.Id) as Season ?? directSeason;
                var parent = season.GetParent() as Series;
                authoritativeParentSeries = parent == null
                    ? null
                    : _libraryManager.GetItemById(parent.InternalId) as Series ?? parent;
            }
            else if (item is Series directSeries)
            {
                authoritativeParentSeries = _libraryManager.GetItemById(directSeries.Id) as Series ?? directSeries;
                var candidates = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    ParentIds = new[] { authoritativeParentSeries.InternalId },
                    IncludeItemTypes = new[] { "Season" },
                    Recursive = false,
                }).OfType<Season>();
                season = SelectUniqueParentTitleRematchSeason(candidates, request);
            }

            if (season == null || authoritativeParentSeries == null)
            {
                season = null;
                authoritativeParentSeries = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(authoritativeParentSeries.Name))
            {
                error = "parent-title-unavailable";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.SeriesId) &&
                !string.Equals(request.SeriesId, authoritativeParentSeries.Id.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                season = null;
                authoritativeParentSeries = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static Season SelectUniqueParentTitleRematchSeason(
            IEnumerable<Season> candidateSeasons,
            DanmuParams request)
        {
            var candidates = (candidateSeasons ?? Enumerable.Empty<Season>())
                .Where(candidate => candidate != null)
                .Where(candidate => !request.SeasonNumber.HasValue ||
                    candidate.IndexNumber == request.SeasonNumber)
                .Where(candidate => !request.SeasonYear.HasValue ||
                    GetSeasonYear(candidate) == request.SeasonYear)
                .Where(candidate => string.IsNullOrWhiteSpace(request.SeasonName) ||
                    string.Equals(candidate.Name, request.SeasonName, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static bool ShouldUseCompositeSeasonPlanPreview(DanmuParams request)
        {
            return IsManualKeyword(request)
                ? IsTemporaryRangeSearch(request)
                : request?.CompositePlan == true || IsTemporaryRangeSearch(request);
        }

        private static bool TryApplySingleManualKeywordSeasonSummary(
            DanmuMatchPreviewResult result,
            bool manualKeyword)
        {
            if (!manualKeyword || result?.Seasons == null || result.Seasons.Count != 1)
            {
                return false;
            }

            var season = result.Seasons[0];
            CopyDecision(result, season);
            result.CanStart = false;
            result.Status = season.Status;
            result.Message = season.Message;
            return true;
        }

        private static bool IsTemporaryRangeSearch(DanmuParams request)
        {
            return string.Equals(request?.SearchScope, "temporary-range", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetSourceOrder(IEnumerable<AbstractScraper> scrapers, AbstractScraper scraper)
        {
            var index = (scrapers ?? Enumerable.Empty<AbstractScraper>())
                .ToList()
                .FindIndex(x => ReferenceEquals(x, scraper));
            return index < 0 ? scraper?.DefaultOrder ?? int.MaxValue : index;
        }

        private static void InitializeDecision(
            DanmuItemMatchResult result, IEnumerable<AbstractScraper> scrapers, bool rematch)
        {
            result.MatchIntent = rematch ? DanmuMatchIntent.Rematch : DanmuMatchIntent.Default;
            result.EnabledProviderIdKeys = DanmuProviderIdResolver.GetEnabledProviderIdKeys(scrapers);
        }

        private static string GetSearchScope(DanmuParams request)
        {
            return string.IsNullOrWhiteSpace(request?.SearchScope)
                ? "interactive"
                : request.SearchScope.Trim();
        }

        private static void ApplySearchOperation(
            DanmuMatchPreviewResult result,
            string operationId,
            string searchScope)
        {
            if (result == null)
            {
                return;
            }

            result.SearchOperationId = operationId ?? string.Empty;
            result.SearchScope = searchScope ?? string.Empty;
            if (result.Target != null)
            {
                ApplySearchOperation(result.Target, operationId, searchScope);
                result.SearchCompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>(
                    result.Target.SearchCompletionDiagnostics);
                result.SelectedCandidate = result.Target.SelectedCandidate;
            }
            else if (result.Seasons != null)
            {
                foreach (var season in result.Seasons)
                {
                    ApplySearchOperation(season, operationId, searchScope);
                    result.SearchCompletionDiagnostics.AddRange(season.SearchCompletionDiagnostics);
                }

                if (result.Seasons.Count == 1)
                {
                    result.SelectedCandidate = result.Seasons[0].SelectedCandidate;
                }
            }
        }

        private static void ApplySearchOperation(
            DanmuItemMatchResult result,
            string operationId,
            string searchScope)
        {
            result.SearchOperationId = operationId ?? string.Empty;
            result.SearchScope = searchScope ?? string.Empty;
            EnsureSelectedCandidate(result);
        }

        private static void ApplySearchOperation(
            DanmuSeasonMatchResult result,
            string operationId,
            string searchScope)
        {
            result.SearchOperationId = operationId ?? string.Empty;
            result.SearchScope = searchScope ?? string.Empty;
            EnsureSelectedCandidate(result);
        }

        private static void SetIncompleteSearchResult(DanmuItemMatchResult result)
        {
            result.AutoSelected = false;
            result.Status = "incomplete";
            result.DecisionReason = "search-incomplete";
            result.Message = "One or more planned provider searches did not complete; choose manually or retry.";
        }

        private static void SetIncompleteSearchResult(DanmuSeasonMatchResult result)
        {
            result.AutoSelected = false;
            result.Status = "incomplete";
            result.DecisionReason = "search-incomplete";
            result.Message = "One or more planned provider searches did not complete; choose manually or retry.";
        }

        private static void EnsureSelectedCandidate(DanmuItemMatchResult result)
        {
            if (result.SelectedCandidate != null || string.IsNullOrWhiteSpace(result.SelectedId))
            {
                return;
            }

            var candidate = result.Candidates.FirstOrDefault(item =>
                string.Equals(item.Id, result.SelectedId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Site, result.SelectedSite, StringComparison.OrdinalIgnoreCase));
            result.SelectedCandidate = ToSelectedCandidate(candidate, result.MatchOrigin, result.DecisionReason);
        }

        private static void EnsureSelectedCandidate(DanmuSeasonMatchResult result)
        {
            if (result.SelectedCandidate != null || string.IsNullOrWhiteSpace(result.SelectedId))
            {
                return;
            }

            var candidate = result.Candidates.FirstOrDefault(item =>
                string.Equals(item.Id, result.SelectedId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Site, result.SelectedSite, StringComparison.OrdinalIgnoreCase));
            result.SelectedCandidate = ToSelectedCandidate(candidate, result.MatchOrigin, result.DecisionReason);
        }

        private static DanmuSelectedCandidatePreview ToSelectedCandidate(
            DanmuMatchCandidate candidate,
            string matchOrigin,
            string decisionReason)
        {
            if (candidate == null)
            {
                return null;
            }

            return new DanmuSelectedCandidatePreview
            {
                Id = candidate.Id,
                Site = candidate.Site,
                SiteName = candidate.SiteName,
                Name = candidate.Name,
                Score = candidate.Score,
                MatchScore = candidate.MatchScore,
                ScoreOrigin = candidate.ScoreOrigin,
                SelectionEvidenceToken = candidate.SelectionEvidenceToken,
                SourceOrder = candidate.SourceOrder,
                MatchOrigin = string.IsNullOrWhiteSpace(candidate.MatchOrigin)
                    ? matchOrigin ?? string.Empty
                    : candidate.MatchOrigin,
                DecisionReason = string.IsNullOrWhiteSpace(candidate.DecisionReason)
                    ? decisionReason ?? string.Empty
                    : candidate.DecisionReason,
                SourceMetadata = candidate.SourceMetadata?.Clone(),
                PartTitle = candidate.PartTitle,
                MovieParts = candidate.MovieParts == null
                    ? new List<DanmuMoviePartChoice>()
                    : candidate.MovieParts.Select(part => new DanmuMoviePartChoice
                    {
                        Token = part.Token,
                        PartTitle = part.PartTitle,
                        Index = part.Index,
                        Selected = part.Selected,
                    }).ToList(),
            };
        }

        private static void InitializeDecision(
            DanmuSeasonMatchResult result, IEnumerable<AbstractScraper> scrapers, bool rematch)
        {
            result.MatchIntent = rematch ? DanmuMatchIntent.Rematch : DanmuMatchIntent.Default;
            result.EnabledProviderIdKeys = DanmuProviderIdResolver.GetEnabledProviderIdKeys(scrapers);
        }

        private static void StampSeasonCandidateEvidence(
            Season season, IEnumerable<DanmuMatchCandidate> candidates)
        {
            StampCandidateEvidence(season, candidates);
        }

        private static void StampCandidateEvidence(
            BaseItem target, IEnumerable<DanmuMatchCandidate> candidates)
        {
            if (target == null) return;
            foreach (var candidate in candidates ?? Enumerable.Empty<DanmuMatchCandidate>())
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Site) ||
                    string.IsNullOrWhiteSpace(candidate.Id)) continue;
                var score = candidate.MatchScore > 0 ? candidate.MatchScore : candidate.Score;
                var origin = string.IsNullOrWhiteSpace(candidate.ScoreOrigin)
                    ? "search-confidence" : candidate.ScoreOrigin;
                candidate.MatchScore = score;
                candidate.ScoreOrigin = origin;
                candidate.SelectionEvidenceToken = CandidateEvidence.Register(
                    target.Id.ToString(), candidate.Site, candidate.Id, score, origin,
                    candidate.SourceMetadata);
            }
        }

        private static void ApplyManualKeywordSearchResult(
            DanmuItemMatchResult result,
            BaseItem evidenceTarget,
            DanmuMatchSearchResult search)
        {
            result.MatchIntent = DanmuMatchIntent.ManualKeyword;
            result.AutoSelected = false;
            result.SelectedCandidate = null;
            result.SelectedId = string.Empty;
            result.SelectedSite = string.Empty;
            result.SelectedSiteName = string.Empty;
            result.Candidates = search.Candidates;
            result.SearchErrors.AddRange(search.SearchErrors);
            result.SearchCompletionDiagnostics.AddRange(search.CompletionDiagnostics);
            var presentation = DescribeManualKeywordSearch(search, result.Candidates.Count);
            if (presentation.ClearCandidates)
            {
                result.Candidates.Clear();
            }
            else
            {
                StampCandidateEvidence(evidenceTarget, result.Candidates);
            }
            result.Status = presentation.Status;
            result.DecisionReason = presentation.DecisionReason;
            result.Message = presentation.Message;
        }

        private static void ApplyManualKeywordSearchResult(
            DanmuSeasonMatchResult result,
            BaseItem evidenceTarget,
            DanmuMatchSearchResult search)
        {
            result.MatchIntent = DanmuMatchIntent.ManualKeyword;
            result.AutoSelected = false;
            result.SelectedCandidate = null;
            result.SelectedId = string.Empty;
            result.SelectedSite = string.Empty;
            result.SelectedSiteName = string.Empty;
            result.Candidates = search.Candidates;
            result.SearchErrors.AddRange(search.SearchErrors);
            result.SearchCompletionDiagnostics.AddRange(search.CompletionDiagnostics);
            var presentation = DescribeManualKeywordSearch(search, result.Candidates.Count);
            if (presentation.ClearCandidates)
            {
                result.Candidates.Clear();
            }
            else
            {
                StampCandidateEvidence(evidenceTarget, result.Candidates);
            }
            result.Status = presentation.Status;
            result.DecisionReason = presentation.DecisionReason;
            result.Message = presentation.Message;
        }

        private static ManualKeywordSearchPresentation DescribeManualKeywordSearch(
            DanmuMatchSearchResult search,
            int candidateCount)
        {
            if (search?.CompletionDiagnostics?.Any(diagnostic =>
                    string.Equals(diagnostic?.Status, "invalid_request",
                        StringComparison.OrdinalIgnoreCase)) == true)
            {
                return new ManualKeywordSearchPresentation(
                    "invalid_request", "manual-keyword-required",
                    "A manual search keyword is required.", true);
            }
            if (search?.WasCancelled == true)
            {
                return new ManualKeywordSearchPresentation(
                    "cancelled", "cancelled", "Manual search was cancelled.", true);
            }
            if (search != null && !search.HasCompletedProviders && !search.IsComplete)
            {
                return new ManualKeywordSearchPresentation(
                    "incomplete", "retryable-incomplete",
                    "No provider search completed; retry the manual search.", false);
            }
            return candidateCount == 0
                ? new ManualKeywordSearchPresentation(
                    "no_match", "no-candidates",
                    "Manual search returned no candidates.", false)
                : new ManualKeywordSearchPresentation(
                    "ambiguous", "manual-selection-required",
                    "Choose a candidate to continue with authoritative validation.", false);
        }

        private sealed class ManualKeywordSearchPresentation
        {
            public ManualKeywordSearchPresentation(
                string status,
                string decisionReason,
                string message,
                bool clearCandidates)
            {
                Status = status;
                DecisionReason = decisionReason;
                Message = message;
                ClearCandidates = clearCandidates;
            }

            public string Status { get; }
            public string DecisionReason { get; }
            public string Message { get; }
            public bool ClearCandidates { get; }
        }

        private static void ApplyProviderDecision(DanmuItemMatchResult result, DanmuMatchDecision decision)
        {
            result.Status = "matched";
            result.Message = "已使用本地 ProviderId 解析匹配";
            result.AutoSelected = true;
            result.SelectedId = decision.Candidate.Id;
            result.SelectedSite = decision.Candidate.Site;
            result.SelectedSiteName = decision.Candidate.SiteName;
            result.MatchOrigin = decision.MatchOrigin;
            result.DecisionReason = decision.DecisionReason;
            result.ResolvedProviderId = decision.ResolvedProviderId;
            result.ResolvedProviderIdKey = decision.ResolvedProviderIdKey;
            result.ResolvedScopeType = decision.ResolvedScopeType;
            result.ResolvedScopeItemId = decision.ResolvedScopeItemId;
            result.Candidates.Add(decision.Candidate);
        }

        private static void ApplyProviderDecision(DanmuSeasonMatchResult result, DanmuMatchDecision decision)
        {
            result.Status = "matched";
            result.Message = "已使用本地 ProviderId 解析匹配";
            result.AutoSelected = true;
            result.SelectedId = decision.Candidate.Id;
            result.SelectedSite = decision.Candidate.Site;
            result.SelectedSiteName = decision.Candidate.SiteName;
            result.MatchOrigin = decision.MatchOrigin;
            result.DecisionReason = decision.DecisionReason;
            result.ResolvedProviderId = decision.ResolvedProviderId;
            result.ResolvedProviderIdKey = decision.ResolvedProviderIdKey;
            result.ResolvedScopeType = decision.ResolvedScopeType;
            result.ResolvedScopeItemId = decision.ResolvedScopeItemId;
            result.Candidates.Add(decision.Candidate);
        }

        private static void CopyDecision(DanmuMatchPreviewResult target, DanmuItemMatchResult source)
        {
            target.MatchOrigin = source.MatchOrigin;
            target.DecisionReason = source.DecisionReason;
            target.ResolvedProviderId = source.ResolvedProviderId;
            target.ResolvedProviderIdKey = source.ResolvedProviderIdKey;
            target.ResolvedScopeType = source.ResolvedScopeType;
            target.ResolvedScopeItemId = source.ResolvedScopeItemId;
            target.SearchScope = source.SearchScope;
            target.SearchOperationId = source.SearchOperationId;
            target.SearchCompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>(
                source.SearchCompletionDiagnostics);
            target.SelectedCandidate = source.SelectedCandidate;
        }

        private static void CopyDecision(DanmuMatchPreviewResult target, DanmuSeasonMatchResult source)
        {
            target.MatchOrigin = source.MatchOrigin;
            target.DecisionReason = source.DecisionReason;
            target.ResolvedProviderId = source.ResolvedProviderId;
            target.ResolvedProviderIdKey = source.ResolvedProviderIdKey;
            target.ResolvedScopeType = source.ResolvedScopeType;
            target.ResolvedScopeItemId = source.ResolvedScopeItemId;
            target.SearchScope = source.SearchScope;
            target.SearchOperationId = source.SearchOperationId;
            target.SearchCompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>(
                source.SearchCompletionDiagnostics);
            target.SelectedCandidate = source.SelectedCandidate;
        }

        private static void CopyDecision(DanmuItemMatchResult target, DanmuSeasonMatchResult source)
        {
            target.MatchIntent = source.MatchIntent;
            target.MatchOrigin = source.MatchOrigin;
            target.DecisionReason = source.DecisionReason;
            target.ResolvedProviderId = source.ResolvedProviderId;
            target.ResolvedProviderIdKey = source.ResolvedProviderIdKey;
            target.ResolvedScopeType = source.ResolvedScopeType;
            target.ResolvedScopeItemId = source.ResolvedScopeItemId;
            target.EnabledProviderIdKeys = source.EnabledProviderIdKeys;
            target.SearchScope = source.SearchScope;
            target.SearchOperationId = source.SearchOperationId;
            target.SearchCompletionDiagnostics = new List<DanmuSearchCompletionDiagnostic>(
                source.SearchCompletionDiagnostics);
            target.SelectedCandidate = source.SelectedCandidate;
        }

        private static void ApplySeasonScopeSummary(
            DanmuSeasonMatchResult result, SeasonPlanningContext context)
        {
            if (result == null || context == null) return;
            result.DisplayedEpisodeCount = context.DisplayedEpisodeCount;
            result.EligibleEpisodeCount = context.LocalEpisodes.Count;
            result.IgnoredParentZeroEpisodeCount = context.ParentZeroOutOfScopeCount;
            result.IgnoredOtherSeasonEpisodeCount = context.OtherSeasonOutOfScopeCount;
            result.IgnoredUnknownParentEpisodeCount = context.UnknownParentOutOfScopeCount;
            result.IgnoredInvalidEpisodeCount = context.InvalidIdentityCount;
        }

        /// <summary>
        /// 解析请求中的季度。季度对象有时能从父剧上下文中枚举出来，
        /// 却无法再仅凭请求中的 ItemId 从全局媒体库回查。
        /// 这与视频是否为 STRM、媒体库路径类型或弹幕网站无关。
        /// 因此前端同时提交父剧、季号、年份和名称，在直接查询失败时进行上下文解析。
        /// </summary>
        private Season ResolveSeason(DanmuParams request)
        {
            if (!string.IsNullOrWhiteSpace(request.Id))
            {
                var direct = _libraryManager.GetItemById(request.Id) as Season;
                if (direct != null)
                {
                    return direct;
                }
            }

            if (string.IsNullOrWhiteSpace(request.SeriesId))
            {
                return null;
            }

            var series = _libraryManager.GetItemById(request.SeriesId) as Series;
            if (series == null)
            {
                return null;
            }

            var seasons = series.GetSeasons(null, new DtoOptions(false))
                .OfType<Season>()
                .ToList();
            if (seasons.Count == 0)
            {
                return null;
            }

            var resolved = SelectSeasonByContext(seasons, request);
            if (resolved != null)
            {
                _logger.Info(
                    "通过父剧上下文定位季度: series={0}, season={1}, number={2}, year={3}",
                    series.Name,
                    resolved.Name,
                    resolved.IndexNumber,
                    GetSeasonYear(resolved));
            }
            return resolved;
        }

        /// <summary>
        /// 综合季度 ID、季号、年份和名称选择唯一结果。
        /// 不因某一个字段恰好唯一就提前返回，避免季号与年份/名称冲突时选错季度。
        /// </summary>
        private static Season SelectSeasonByContext(IEnumerable<Season> candidateSeasons, DanmuParams request)
        {
            var seasons = candidateSeasons?.ToList() ?? new List<Season>();
            if (seasons.Count == 0)
            {
                return null;
            }

            var ranked = seasons.Select(season =>
            {
                var score = 0;
                var matched = false;

                if (!string.IsNullOrWhiteSpace(request.Id))
                {
                    if (string.Equals(season.Id.ToString(), request.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 1000;
                        matched = true;
                    }
                }

                if (request.SeasonNumber.HasValue)
                {
                    if (season.IndexNumber == request.SeasonNumber)
                    {
                        score += 80;
                        matched = true;
                    }
                    else
                    {
                        score -= 25;
                    }
                }

                if (request.SeasonYear.HasValue)
                {
                    if (GetSeasonYear(season) == request.SeasonYear)
                    {
                        score += 60;
                        matched = true;
                    }
                    else
                    {
                        score -= 20;
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.SeasonName))
                {
                    if (string.Equals(season.Name, request.SeasonName, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 40;
                        matched = true;
                    }
                    else
                    {
                        score -= 10;
                    }
                }

                return new { Season = season, Score = score, Matched = matched };
            })
            .Where(x => x.Matched)
            .OrderByDescending(x => x.Score)
            .ToList();

            if (ranked.Count == 0)
            {
                return seasons.Count == 1 ? seasons[0] : null;
            }

            return ranked.Count == 1 || ranked[0].Score > ranked[1].Score
                ? ranked[0].Season
                : null;
        }

        private static int? GetSeasonYear(Season season)
        {
            var year = season?.ProductionYear;
            if (year.HasValue && year.Value > 0)
            {
                return year;
            }

            return season?.GetEpisodes()?.Items
                .Where(item => item.ProductionYear.HasValue && item.ProductionYear.Value > 0)
                .Select(item => item.ProductionYear)
                .FirstOrDefault();
        }

        private async Task<DanmuBindResult> BindMatch(DanmuParams request)
        {
            var directItem = string.IsNullOrWhiteSpace(request.Id) ? null : _libraryManager.GetItemById(request.Id);
            if (directItem is Movie movie)
            {
                return await BindMovieMatch(movie, request).ConfigureAwait(false);
            }

            var result = new DanmuBindResult
            {
                SeasonId = request.Id ?? string.Empty,
                Site = request.Site ?? string.Empty,
                CandidateId = request.CandidateId ?? string.Empty,
                Manual = request.Manual,
            };

            var season = ResolveSeason(request);
            if (season == null)
            {
                result.Message = "找不到指定季度";
                return result;
            }

            result.ErrorCode = "mapping_required";
            result.Message = "Season binding requires an authoritative Episode mapping preview.";
            return result;
        }

        private async Task<DanmuBindResult> BindMovieMatch(Movie movie, DanmuParams request)
        {
            var result = new DanmuBindResult
            {
                ItemId = movie.Id.ToString(),
                ItemType = "Movie",
                Site = request.Site ?? string.Empty,
                CandidateId = request.CandidateId ?? string.Empty,
                Manual = request.Manual,
            };
            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null || string.IsNullOrWhiteSpace(request.CandidateId))
            {
                result.Message = "弹幕网站或候选 ID 无效";
                return result;
            }

            if (!CandidateEvidence.TryResolve(request.SelectionEvidenceToken,
                    movie.Id.ToString(), scraper.ProviderId, request.CandidateId, out _))
            {
                result.Message = "电影候选证据已失效，请重新预览";
                return result;
            }

            DanmuMoviePartEvidence selectedPart = null;
            if (!string.IsNullOrWhiteSpace(request.MoviePartToken) &&
                !CandidateEvidence.TryResolveMoviePart(
                    request.MoviePartToken,
                    request.SelectionEvidenceToken,
                    movie.Id.ToString(),
                    scraper.ProviderId,
                    request.CandidateId,
                    out selectedPart))
            {
                result.Message = "电影正片版本选择已失效或不属于当前候选";
                return result;
            }

            try
            {
                var media = await scraper.GetMedia(movie, request.CandidateId).ConfigureAwait(false);
                if (media == null)
                {
                    result.Message = "电影候选已失效或无法读取";
                    return result;
                }

                if (selectedPart != null)
                {
                    media.SelectedMoviePartId = selectedPart.PartId;
                    media.PartTitle = selectedPart.PartTitle;
                }

                var providerValue = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
                if (request.Manual)
                {
                    await SaveManualBindingAsync(movie, scraper.ProviderId, providerValue).ConfigureAwait(false);
                }
                result.Success = true;
                result.CandidateId = providerValue;
                result.Message = "电影匹配已保存";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 绑定电影候选失败: movie={1}, candidate={2}",
                    scraper.Name, movie.Name, request.CandidateId);
                result.Message = "绑定失败：" + ex.Message;
            }

            return result;
        }

        private async Task<DanmuDownloadTaskResult> StartTrackedDownload(DanmuParams request)
        {
            var directItem = string.IsNullOrWhiteSpace(request.Id) ? null : _libraryManager.GetItemById(request.Id);
            if (directItem is Movie movie)
            {
                return await StartTrackedMovieDownload(movie, request).ConfigureAwait(false);
            }
            if (directItem is Episode episode)
            {
                return await StartTrackedSingleEpisodeDownload(episode, request).ConfigureAwait(false);
            }

            if (request.CompositePlan || request.ParsedCompositeSelections.Count > 0 ||
                request.ParsedExcludedLocalEpisodeItemIds.Count > 0)
            {
                return await StartTrackedCompositeSeasonDownload(request).ConfigureAwait(false);
            }

            var failed = new DanmuDownloadTaskResult
            {
                SeasonId = request.Id ?? string.Empty,
                Site = request.Site ?? string.Empty,
                Status = "failed",
            };

            var season = ResolveSeason(request);
            if (season == null)
            {
                failed.Message = "找不到指定季度";
                return failed;
            }

            failed.SeasonName = season.Name ?? string.Empty;
            failed.ErrorCode = "mapping_required";
            failed.Message = "Season download requires an authoritative Episode mapping preview.";
            return failed;

        }

        private sealed class CompositePlanBuild
        {
            public CompositeSeasonPlan Plan { get; set; }
            public SeasonPlanningContext Context { get; set; }
            public List<Episode> Episodes { get; set; } = new List<Episode>();
            // Preview-only names from the media responses already resolved in
            // this build. This lookup is deliberately not part of the plan or
            // its reconstruction/download state.
            public Dictionary<string, string> SourceEpisodeNames { get; } =
                new Dictionary<string, string>(StringComparer.Ordinal);
            public string StructureFingerprint { get; set; } = string.Empty;
            public string PlanFingerprint { get; set; } = string.Empty;
            public List<DanmuCompositeSeasonSelection> Selections { get; set; } =
                new List<DanmuCompositeSeasonSelection>();
            public List<string> ExcludedItemIds { get; set; } = new List<string>();
            public string Error { get; set; } = string.Empty;
        }

        /// <summary>
        /// Rebuilds every submitted mapping from live scraper responses.  The
        /// browser provides only candidate/start identifiers, never CommentIds
        /// or arbitrary local-to-source assignments.
        /// </summary>
        private static List<string> MergeEpisodeExclusions(
            IEnumerable<string> requested,
            IEnumerable<string> ownership)
        {
            return (requested ?? Enumerable.Empty<string>())
                .Concat(ownership ?? Enumerable.Empty<string>())
                .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool TryBuildOwnedPlanningContext(
            Season season, out SeasonPlanningContext context, out string error)
        {
            return SeasonTargetPlanningCoordinator.TryBuild(season, out context, out error);
        }

        private async Task<CompositePlanBuild> BuildCompositePlanAsync(
            Season season,
            IEnumerable<DanmuCompositeSeasonSelection> selections,
            bool ignoredLegacyDirectEpisodeProviderIds,
            IEnumerable<string> excludedLocalEpisodeItemIds = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var build = new CompositePlanBuild();
            if (!TryBuildOwnedPlanningContext(season, out var context, out var ownershipError))
            {
                build.Error = ownershipError;
                build.Context = context;
                return build;
            }

            build.Context = context;
            build.Episodes = context.Episodes;
            build.StructureFingerprint = context.StructureFingerprint;
            var local = context.LocalEpisodes;
            var mappings = new List<CompositeSeasonEpisodeMapping>();
            var effectiveExclusions = MergeEpisodeExclusions(excludedLocalEpisodeItemIds, null);
            var canonicalSelections = (selections ?? Enumerable.Empty<DanmuCompositeSeasonSelection>())
                .Select(CloneSeasonPlanSelection).ToList();
            build.Selections = canonicalSelections;
            build.ExcludedItemIds = effectiveExclusions.ToList();

            // The durable marker is independent evidence: a partial dialog
            // draft must not turn a historically composite Season back into a
            // normal single-Season write path.
            var durableCompositeMarker = false;
            if (!CompositeSeasonPlanner.TryCreatePlan(local, mappings, null,
                    effectiveExclusions, durableCompositeMarker, out var plan, out var error))
            {
                build.Error = error;
                return build;
            }

            var replacementMappings = new List<CompositeSeasonEpisodeMapping>();
            foreach (var selection in canonicalSelections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (selection == null || string.IsNullOrWhiteSpace(selection.Site) ||
                    string.IsNullOrWhiteSpace(selection.CandidateId))
                {
                    build.Error = "Composite source selection is incomplete.";
                    return build;
                }
                if (!DanmuMappingProtocol.IsCurrent(selection.MappingProtocolVersion) ||
                    !DanmuMappingProtocol.IsAllowedBatchOrigin(selection.MatchOrigin))
                {
                    build.Error = "stale-protocol-local-identifier-origin";
                    return build;
                }

                var scraper = _scraperManager.All().FirstOrDefault(x =>
                    string.Equals(x.ProviderId, selection.Site, StringComparison.OrdinalIgnoreCase));
                if (scraper == null)
                {
                    build.Error = "The selected danmu provider is no longer enabled.";
                    return build;
                }

                if (!CandidateEvidence.TryResolve(selection.SelectionEvidenceToken,
                        season.Id.ToString(), selection.Site, selection.CandidateId,
                        out var selectionEvidence))
                {
                    build.Error = "Selected candidate evidence expired or belongs to another Season.";
                    return build;
                }

                ScraperMedia media;
                try
                {
                    var resolution = await BoundedSearchPolicy.Shared.ExecuteAsync(
                        scraper.ProviderId,
                        ignored => scraper.GetMedia(season, selection.CandidateId),
                        cancellationToken).ConfigureAwait(false);
                    if (resolution.Status != BoundedSearchExecutionStatus.Completed)
                    {
                        build.Error = resolution.Status == BoundedSearchExecutionStatus.Cancelled
                            ? "Composite source resolution was cancelled."
                            : "Composite source resolution timed out or failed.";
                        return build;
                    }

                    media = resolution.Result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CompositeSeason] Unable to resolve source: site={0}, candidate={1}",
                        selection.Site, selection.CandidateId);
                    build.Error = "The selected source could not be verified.";
                    return build;
                }

                var sourceEpisodes = CompositeSeasonMatchService.GetSourceEpisodes(media);
                var sourceStart = selection.SourceStartEpisodeId;
                if (string.IsNullOrWhiteSpace(sourceStart))
                {
                    var requestedSourceNumber = selection.SourceStartEpisodeNumber.GetValueOrDefault();
                    var selectedSource = requestedSourceNumber > 0
                        ? sourceEpisodes.FirstOrDefault(x => x.EpisodeNumber == requestedSourceNumber) ??
                          sourceEpisodes.ElementAtOrDefault(requestedSourceNumber - 1)
                        : sourceEpisodes.FirstOrDefault();
                    sourceStart = selectedSource?.EpisodeId ?? string.Empty;
                }

                var requestSource = CompositeSeasonMatchService.GetSource(
                    scraper.ProviderId, media, selection.CandidateId);
                foreach (var sourceEpisodeName in CompositeSeasonMatchService.GetSourceEpisodeNames(
                             media, requestSource))
                {
                    build.SourceEpisodeNames[sourceEpisodeName.Key] = sourceEpisodeName.Value;
                }
                var request = new CompositeSeasonSegmentRequest
                {
                    LocalStartEpisodeItemId = selection.LocalStartEpisodeItemId,
                    RequestedEpisodeCount = selection.RequestedEpisodeCount,
                    Source = requestSource,
                    SourceEpisodes = sourceEpisodes,
                    SourceStartEpisodeId = sourceStart,
                    Origin = string.IsNullOrWhiteSpace(selection.MatchOrigin) ? "manual" : selection.MatchOrigin,
                    MatchScore = selectionEvidence.MatchScore,
                    ScoreOrigin = selectionEvidence.ScoreOrigin,
                    SelectionEvidenceToken = selection.SelectionEvidenceToken,
                    SourceMetadata = SourceMetadata.MergeDetailWithSnapshot(
                        CompositeSeasonMatchService.GetSourceMetadata(media),
                        selectionEvidence.SourceMetadata),
                };
                var beforeLocalIds = new HashSet<string>(plan.Mappings.Select(x => x.LocalEpisodeItemId),
                    StringComparer.OrdinalIgnoreCase);
                var localStart = local.FirstOrDefault(item => string.Equals(
                    item.ItemId, request.LocalStartEpisodeItemId, StringComparison.OrdinalIgnoreCase));
                var isInitialOwningCandidate = selection.RequestedEpisodeCount <= 0 &&
                    localStart?.Ownership == CompositeSeasonOwnershipKind.Owning;
                if (isInitialOwningCandidate)
                {
                    if (!CompositeSeasonPlanner.TryApplyRemainingOwningSourceEpisodes(
                            plan, request.Source, sourceEpisodes, request.Origin,
                            request.MatchScore, request.ScoreOrigin, request.SelectionEvidenceToken,
                            request.SourceMetadata,
                            out plan, out error))
                    {
                        build.Error = error;
                        return build;
                    }
                }
                else if (!CompositeSeasonPlanner.TryApplySegment(plan, request, out plan, out _, out error))
                {
                    build.Error = error;
                    return build;
                }
                var appliedMappings = plan.Mappings
                    .Where(mapping => !beforeLocalIds.Contains(mapping.LocalEpisodeItemId))
                    .ToList();
                if (appliedMappings.Count > 0)
                {
                    selection.LocalStartEpisodeItemId = appliedMappings[0].LocalEpisodeItemId;
                    selection.RequestedEpisodeCount = appliedMappings.Count;
                    selection.SourceStartEpisodeId = appliedMappings[0].SourceEpisodeId;
                    selection.SourceStartEpisodeNumber = appliedMappings[0].SourceEpisodeNumber;
                }
                replacementMappings.AddRange(appliedMappings
                    .Select(mapping => new CompositeSeasonEpisodeMapping
                    {
                        LocalEpisodeItemId = mapping.LocalEpisodeItemId,
                        Source = new CompositeSeasonSourceIdentity
                        {
                            ProviderId = mapping.Source?.ProviderId ?? string.Empty,
                            MediaId = mapping.Source?.MediaId ?? string.Empty,
                            MediaLookupId = mapping.Source?.MediaLookupId ?? string.Empty,
                        },
                        SourceEpisodeId = mapping.SourceEpisodeId,
                        CommentId = mapping.CommentId,
                        SourceEpisodeNumber = mapping.SourceEpisodeNumber,
                        Origin = mapping.Origin,
                        MatchScore = mapping.MatchScore,
                        ScoreOrigin = mapping.ScoreOrigin,
                        SelectionEvidenceToken = mapping.SelectionEvidenceToken,
                        SourceMetadata = mapping.SourceMetadata?.Clone(),
                    }));
            }

            // Recreate from the exact same inputs used by preview/download.
            // In particular, exclusions remove old direct evidence first,
            // whereas the just verified replacements remain executable.
            if (!CompositeSeasonPlanner.TryCreatePlan(local, mappings, replacementMappings,
                    effectiveExclusions, durableCompositeMarker, out plan, out error))
            {
                build.Error = error;
                return build;
            }

            build.Plan = plan;
            build.PlanFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, canonicalSelections, plan);
            return build;
        }

        private static DanmuCompositeSeasonSelection CloneSeasonPlanSelection(
            DanmuCompositeSeasonSelection selection)
        {
            if (selection == null) return null;
            return new DanmuCompositeSeasonSelection
            {
                MappingProtocolVersion = selection.MappingProtocolVersion,
                PlanGeneration = selection.PlanGeneration,
                LocalStartEpisodeItemId = selection.LocalStartEpisodeItemId ?? string.Empty,
                RequestedEpisodeCount = selection.RequestedEpisodeCount,
                Site = selection.Site ?? string.Empty,
                CandidateId = selection.CandidateId ?? string.Empty,
                SourceStartEpisodeId = selection.SourceStartEpisodeId ?? string.Empty,
                SourceStartEpisodeNumber = selection.SourceStartEpisodeNumber,
                MatchOrigin = selection.MatchOrigin ?? string.Empty,
                SelectionEvidenceToken = selection.SelectionEvidenceToken ?? string.Empty,
                ServerSourceMetadata = selection.ServerSourceMetadata?.Clone(),
            };
        }

        private static string PreviewPlanKey(string seasonId, long generation) =>
            (seasonId ?? string.Empty) + "\u001f" + generation;

        private static void RegisterPreviewPlanFingerprint(
            string seasonId, long generation, string fingerprint)
        {
            if (generation <= 0 || string.IsNullOrWhiteSpace(seasonId) ||
                string.IsNullOrWhiteSpace(fingerprint)) return;
            SeasonPreviewPlanFingerprints[PreviewPlanKey(seasonId, generation)] = fingerprint;
        }

        private async Task<DanmuDownloadTaskResult> StartTrackedCompositeSeasonDownload(DanmuParams request)
        {
            var failed = new DanmuDownloadTaskResult
            {
                SeasonId = request.Id ?? string.Empty,
                Status = "failed",
            };
            var season = ResolveSeason(request);
            if (season == null)
            {
                failed.Message = "找不到指定季。";
                return failed;
            }

            if (!DanmuMappingProtocol.IsCurrent(request.MappingProtocolVersion) ||
                !SeasonPlanGenerations.IsCurrent(season.Id.ToString(), request.PlanGeneration) ||
                request.ParsedCompositeSelections.Any(selection => selection == null ||
                    selection.PlanGeneration != request.PlanGeneration ||
                    !DanmuMappingProtocol.IsCurrent(selection.MappingProtocolVersion) ||
                    !DanmuMappingProtocol.IsAllowedBatchOrigin(selection.MatchOrigin)))
            {
                failed.SeasonName = season.Name ?? string.Empty;
                failed.ErrorCode = "stale_protocol";
                failed.Message = "The Season mapping protocol or generation is stale; preview again.";
                return failed;
            }
            var capturedPlanGeneration = request.PlanGeneration;

            var build = await BuildCompositePlanAsync(season, request.ParsedCompositeSelections, false,
                request.ParsedExcludedLocalEpisodeItemIds).ConfigureAwait(false);
            if (build.Plan == null)
            {
                failed.SeasonName = season.Name ?? string.Empty;
                failed.Message = "复合季映射无效：" + build.Error;
                return failed;
            }
            if (!SeasonPreviewPlanFingerprints.TryGetValue(
                    PreviewPlanKey(season.Id.ToString(), capturedPlanGeneration),
                    out var previewFingerprint) ||
                string.IsNullOrWhiteSpace(request.PlanFingerprint) ||
                !string.Equals(request.PlanFingerprint, previewFingerprint, StringComparison.Ordinal) ||
                !string.Equals(previewFingerprint, build.PlanFingerprint, StringComparison.Ordinal))
            {
                failed.SeasonName = season.Name ?? string.Empty;
                failed.ErrorCode = "stale_plan";
                failed.Message = "The authoritative Season plan changed after preview; preview again.";
                return failed;
            }
            if (build.Plan.Mappings.Count == 0)
            {
                failed.SeasonName = season.Name ?? string.Empty;
                failed.Message = "没有可验证的剧集映射可供下载。";
                return failed;
            }
            if (build.Plan.UnmatchedRuns.Count > 0 && !request.ConfirmPartial)
            {
                failed.SeasonName = season.Name ?? string.Empty;
                failed.ErrorCode = "partial_confirmation_required";
                failed.Message = "The authoritative plan is partial; confirm mapped-only download explicitly.";
                return failed;
            }

            var byItemId = build.Episodes.ToDictionary(x => x.Id.ToString(), StringComparer.OrdinalIgnoreCase);
            var distinctSources = build.Plan.Mappings
                .Select(x => x.Source)
                .Distinct()
                .ToList();
            var canPersistSingleSeasonSource = build.Plan.CanPersistCompleteSeasonBinding;
            var singleSource = canPersistSingleSeasonSource ? distinctSources[0] : null;
            var singleSourceGeneration = singleSource == null
                ? 0
                : _libraryManagerEventsHelper.BeginProviderWrite(season, singleSource.ProviderId);
            var task = new DanmuDownloadTaskResult
            {
                TaskId = Guid.NewGuid().ToString("N"),
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                PlanGeneration = capturedPlanGeneration,
                SeasonId = season.Id.ToString(),
                SeriesId = season.GetParent()?.Id.ToString() ?? string.Empty,
                SeasonName = season.Name ?? string.Empty,
                SeasonNumber = season.IndexNumber,
                SeasonYear = season.ProductionYear,
                TargetItemType = "CompositeSeason",
                // This is intentionally the write-safety decision, not the
                // final visible source count. A subset of a former composite
                // Season must still clear Season ProviderIds after its first
                // persisted file.
                IsCompositePlan = build.Plan.SeasonBindingUnsafe,
                Site = singleSource?.ProviderId ?? string.Empty,
                CandidateId = !string.IsNullOrWhiteSpace(singleSource?.MediaLookupId)
                    ? singleSource.MediaLookupId
                    : singleSource?.MediaId ?? string.Empty,
                SeasonProviderValue = singleSource?.MediaId ?? string.Empty,
                SeasonStructureFingerprint = build.StructureFingerprint,
                SeasonPlanFingerprint = build.PlanFingerprint,
                SeasonPlanSelections = build.Selections.Select(CloneSeasonPlanSelection).ToList(),
                SeasonPlanExcludedItemIds = build.ExcludedItemIds.ToList(),
                SeasonMirrorEligible = canPersistSingleSeasonSource && singleSource != null &&
                    !string.IsNullOrWhiteSpace(singleSource.MediaId) &&
                    !singleSource.MediaId.StartsWith("direct-episode-provider:", StringComparison.OrdinalIgnoreCase),
                SeasonProviderWriteGeneration = singleSourceGeneration,
                Status = "queued",
                Message = "等待后台下载队列",
                Total = build.Plan.Mappings.Count,
                ForceRefresh = request.ForceRefresh,
                Episodes = build.Plan.Mappings.Select(mapping => new DanmuEpisodeDownloadResult
                {
                    ItemId = mapping.LocalEpisodeItemId,
                    EpisodeNumber = byItemId[mapping.LocalEpisodeItemId].IndexNumber,
                    EpisodeName = byItemId[mapping.LocalEpisodeItemId].Name ?? string.Empty,
                    SourceEpisodeNumber = mapping.SourceEpisodeNumber,
                    SourceSite = mapping.Source.ProviderId,
                    SourceCandidateId = !string.IsNullOrWhiteSpace(mapping.Source.MediaLookupId)
                        ? mapping.Source.MediaLookupId
                        : mapping.Source.MediaId,
                    SourceEpisodeId = mapping.SourceEpisodeId,
                    // Composite mappings are built by resolving the selected
                    // Season/media candidate.  Preserve that scope even when
                    // the selected candidate itself originated from a
                    // provider-id decision.
                    SourceScopeType = "Season",
                    MatchOrigin = mapping.Origin,
                    Status = "pending",
                    Message = "等待下载",
                }).ToList(),
            };
            DownloadTasks[task.TaskId] = task;
            var cancellation = new CancellationTokenSource();
            DownloadTaskCancellations[task.TaskId] = cancellation;

            _ = Task.Run(async () =>
            {
                var enteredQueue = false;
                CompositeSeasonProviderWriteLease lease = null;
                try
                {
                    lease = _libraryManagerEventsHelper.BeginCompositeSeasonWrite(season, task.IsCompositePlan);
                    await TrackedDownloadQueue.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    enteredQueue = true;
                    var preflightBuild = await BuildCompositePlanAsync(
                        season, task.SeasonPlanSelections, false, task.SeasonPlanExcludedItemIds,
                        cancellation.Token).ConfigureAwait(false);
                    if (preflightBuild.Plan == null ||
                        !string.Equals(task.SeasonPlanFingerprint,
                            preflightBuild.PlanFingerprint, StringComparison.Ordinal) ||
                        !SeasonPlanGenerations.IsCurrent(season.Id.ToString(), task.PlanGeneration))
                    {
                        lock (task) task.ErrorCode = "stale_plan";
                        throw new DanmuDownloadErrorException(
                            "The Season structure or mapping generation changed; preview again before downloading.");
                    }
                    lock (task)
                    {
                        task.Status = "running";
                        task.Message = "正在下载复合季弹幕";
                    }

                    foreach (var episodeResult in task.Episodes)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (!byItemId.TryGetValue(episodeResult.ItemId, out var episode))
                        {
                            throw new DanmuDownloadErrorException("本地剧集已不存在。");
                        }
                        lock (task)
                        {
                            episodeResult.Status = "running";
                            episodeResult.Message = "正在下载";
                        }
                        try
                        {
                            var scraper = _scraperManager.All().FirstOrDefault(x => string.Equals(
                                x.ProviderId, episodeResult.SourceSite, StringComparison.OrdinalIgnoreCase));
                            if (scraper == null)
                            {
                                throw new DanmuDownloadErrorException("来源站点已不可用。");
                            }
                            // Direct Episode ProviderIds identify an upstream
                            // episode, not a media/season. Keep that path exact
                            // instead of calling GetMedia with an episode id.
                            var isDirectMapping = IsDirectEpisodeProviderMapping(episodeResult);
                            var media = isDirectMapping
                                ? await DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                                    scraper, episode, episodeResult.SourceCandidateId, 1).ConfigureAwait(false)
                                : await scraper.GetMedia(season, episodeResult.SourceCandidateId).ConfigureAwait(false);
                            var sourceEpisode = (media?.Episodes ?? new List<ScraperEpisode>()).FirstOrDefault(x =>
                                x != null && string.Equals(x.Id, episodeResult.SourceEpisodeId, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(x.CommentId));
                            if (sourceEpisode == null)
                            {
                                throw new DanmuDownloadErrorException("来源剧集已变更，无法验证原映射。");
                            }
                            // A one-item media collection avoids any positional fallback: this
                            // local ItemId always downloads the source episode verified above.
                            var exactMedia = new ScraperMedia
                            {
                                Id = media.Id,
                                ProviderId = scraper.ProviderId,
                                Episodes = new List<ScraperEpisode>
                                {
                                    new ScraperEpisode
                                    {
                                        Id = sourceEpisode.Id,
                                        CommentId = sourceEpisode.CommentId,
                                        EpisodeNumber = 1,
                                        Title = sourceEpisode.Title,
                                    },
                                },
                            };
                            var outcome = await _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                                episode, exactMedia, scraper, request.ForceRefresh, 1).ConfigureAwait(false);
                            await PersistProviderIdAfterAcceptedOutcome(episode, outcome).ConfigureAwait(false);
                            lock (task)
                            {
                                episodeResult.Status = outcome.Status;
                                episodeResult.Message = outcome.Message;
                                episodeResult.SkipReason = outcome.SkipReason ?? string.Empty;
                                if (outcome.Status == "success") task.Succeeded++;
                                else if (outcome.Status == "partial") task.Partial++;
                                else task.Skipped++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[CompositeSeason] Download failed: season={0}, episode={1}",
                                season.Name, episodeResult.EpisodeNumber);
                            lock (task)
                            {
                                episodeResult.Status = "failed";
                                episodeResult.Message = ex.Message;
                                task.Failed++;
                            }
                        }
                        finally
                        {
                            lock (task) task.Completed++;
                        }
                    }
                    lock (task) UpdateCompletedTaskSummary(task);
                    await CommitSeasonDisplayMirrorAfterTerminalAsync(season, task).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    lock (task)
                    {
                        foreach (var item in task.Episodes.Where(x => x.Status == "pending" || x.Status == "running"))
                        {
                            item.Status = "cancelled";
                            item.Message = "已强制停止";
                        }
                        task.Status = "cancelled";
                        RecalculateTaskCounts(task);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CompositeSeason] Task failed: season={0}", season.Name);
                    lock (task)
                    {
                        task.Status = "failed";
                        task.Message = ex.Message;
                    }
                }
                finally
                {
                    _libraryManagerEventsHelper.CompleteCompositeSeasonWrite(lease);
                    if (enteredQueue) TrackedDownloadQueue.Release();
                    if (DownloadTaskCancellations.TryRemove(task.TaskId, out var removed)) removed.Dispose();
                }
            });

            return Snapshot(task);
        }

        private async Task<DanmuDownloadTaskResult> StartTrackedMovieDownload(Movie movie, DanmuParams request)
        {
            var failed = FailedTarget(movie, request, "Movie");
            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null || string.IsNullOrWhiteSpace(request.CandidateId))
            {
                failed.Message = "弹幕网站或电影候选 ID 无效";
                return failed;
            }

            if (!CandidateEvidence.TryResolve(request.SelectionEvidenceToken,
                    movie.Id.ToString(), scraper.ProviderId, request.CandidateId,
                    out var candidateEvidence))
            {
                failed.SiteName = scraper.ProviderName;
                failed.Message = "电影候选证据已失效，请重新预览";
                return failed;
            }

            DanmuMoviePartEvidence selectedPart = null;
            if (!string.IsNullOrWhiteSpace(request.MoviePartToken) &&
                !CandidateEvidence.TryResolveMoviePart(
                    request.MoviePartToken,
                    request.SelectionEvidenceToken,
                    movie.Id.ToString(),
                    scraper.ProviderId,
                    request.CandidateId,
                    out selectedPart))
            {
                failed.SiteName = scraper.ProviderName;
                failed.Message = "电影正片版本选择已失效或不属于当前候选";
                return failed;
            }

            ScraperMedia media;
            try
            {
                media = await scraper.GetMedia(movie, request.CandidateId).ConfigureAwait(false);
                if (media == null)
                {
                    failed.SiteName = scraper.ProviderName;
                    failed.Message = "电影候选已失效或无法读取";
                    return failed;
                }

                media.SourceMetadata = SourceMetadata.MergeDetailWithSnapshot(
                    CompositeSeasonMatchService.GetSourceMetadata(media),
                    candidateEvidence.SourceMetadata);
                if (selectedPart != null)
                {
                    media.SelectedMoviePartId = selectedPart.PartId;
                    media.PartTitle = selectedPart.PartTitle;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 创建电影下载任务失败: movie={1}", scraper.Name, movie.Name);
                failed.SiteName = scraper.ProviderName;
                failed.Message = "准备电影下载失败：" + ex.Message;
                return failed;
            }

            var task = CreateSingleTargetTask(movie, request, scraper, "Movie", null);
            task.CandidateId = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
            task.PartTitle = media.PartTitle ?? string.Empty;
            task.SelectedMoviePartId = media.SelectedMoviePartId ?? string.Empty;
            return QueueSingleTargetDownload(
                task,
                movie,
                () => _libraryManagerEventsHelper.DownloadMovieForProgress(
                    movie, media, scraper, request.ForceRefresh),
                request.ForceRefresh ? "正在强制刷新电影弹幕" : "正在下载电影弹幕");
        }

        private async Task<DanmuDownloadTaskResult> StartTrackedSingleEpisodeDownload(
            Episode episode,
            DanmuParams request)
        {
            var failed = FailedTarget(episode, request, "Episode");
            var season = episode.GetParent() as Season;
            if (season == null)
            {
                failed.Message = "找不到本集所属季度";
                return failed;
            }

            var sourceEpisodeNumber = request.SourceEpisodeNumber ?? 0;
            if (sourceEpisodeNumber <= 0 && string.IsNullOrWhiteSpace(request.SourceEpisodeId))
            {
                failed.Message = "来源集数必须是正整数";
                return failed;
            }

            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null || string.IsNullOrWhiteSpace(request.CandidateId))
            {
                failed.Message = "弹幕网站或季度候选 ID 无效";
                return failed;
            }

            ScraperMedia media;
            ScraperEpisode sourceEpisode;
            var directEpisodeProviderId = episode.GetProviderId(scraper.ProviderId);
            var isDirectEpisodeProviderId = string.Equals(
                directEpisodeProviderId,
                request.CandidateId,
                StringComparison.OrdinalIgnoreCase);
            try
            {
                var resolvedMedia = isDirectEpisodeProviderId
                    ? await DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                        scraper, episode, request.CandidateId, sourceEpisodeNumber).ConfigureAwait(false)
                    : await scraper.GetMedia(season, request.CandidateId).ConfigureAwait(false);
                if (!DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(
                        resolvedMedia, request.SourceEpisodeId, out media, out sourceEpisode))
                {
                    failed.SiteName = scraper.ProviderName;
                    failed.Message = $"候选中不存在可下载的第 {sourceEpisodeNumber} 集";
                    return failed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 创建单集下载任务失败: episode={1}", scraper.Name, episode.Name);
                failed.SiteName = scraper.ProviderName;
                failed.Message = "准备本集下载失败：" + ex.Message;
                return failed;
            }

            var confirmedSourceNumber = sourceEpisode.EpisodeNumber ?? request.SourceEpisodeNumber;
            var task = CreateSingleTargetTask(episode, request, scraper, "Episode", confirmedSourceNumber);
            task.SeasonId = season.Id.ToString();
            task.SeasonName = season.Name ?? string.Empty;
            task.SeasonNumber = season.IndexNumber;
            task.SeasonYear = season.ProductionYear;
            task.SeriesId = season.GetParent()?.Id.ToString() ?? string.Empty;
            // CandidateId is the original lookup token. The canonical media id
            // is not a safe substitute when a provider uses a distinct id.
            task.CandidateId = request.CandidateId;
            task.MatchOrigin = isDirectEpisodeProviderId ? "provider-id" : string.Empty;
            task.Episodes[0].MatchOrigin = task.MatchOrigin;
            task.Episodes[0].SourceScopeType = isDirectEpisodeProviderId ? "Episode" : "Season";
            return QueueSingleTargetDownload(
                task,
                episode,
                () => _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                    episode, media, scraper, request.ForceRefresh, 1),
                request.ForceRefresh
                    ? $"正在强制刷新本地第 {episode.IndexNumber ?? 0} 集（来源第 {sourceEpisodeNumber} 集）"
                    : $"正在下载本地第 {episode.IndexNumber ?? 0} 集（来源第 {sourceEpisodeNumber} 集）");
        }

        private DanmuDownloadTaskResult FailedTarget(BaseItem item, DanmuParams request, string itemType)
        {
            return new DanmuDownloadTaskResult
            {
                TargetItemId = item.Id.ToString(),
                TargetItemName = item.Name ?? string.Empty,
                TargetItemType = itemType,
                Site = request.Site ?? string.Empty,
                CandidateId = request.CandidateId ?? string.Empty,
                Status = "failed",
            };
        }

        private DanmuDownloadTaskResult CreateSingleTargetTask(
            BaseItem item,
            DanmuParams request,
            AbstractScraper scraper,
            string itemType,
            int? sourceEpisodeNumber)
        {
            return new DanmuDownloadTaskResult
            {
                TaskId = Guid.NewGuid().ToString("N"),
                TargetItemId = item.Id.ToString(),
                TargetItemName = item.Name ?? string.Empty,
                TargetItemType = itemType,
                SourceEpisodeNumber = sourceEpisodeNumber,
                Site = scraper.ProviderId,
                SiteName = scraper.ProviderName,
                CandidateId = request.CandidateId ?? string.Empty,
                Status = "queued",
                Message = "等待后台下载队列",
                Total = 1,
                ForceRefresh = request.ForceRefresh,
                Episodes = new List<DanmuEpisodeDownloadResult>
                {
                    new DanmuEpisodeDownloadResult
                    {
                        ItemId = item.Id.ToString(),
                        EpisodeNumber = item.IndexNumber,
                        SourceEpisodeNumber = sourceEpisodeNumber,
                        SourceEpisodeId = request.SourceEpisodeId ?? string.Empty,
                        SourceSite = scraper.ProviderId,
                        SourceCandidateId = request.CandidateId ?? string.Empty,
                        // A normal episode match stores the selected Season
                        // candidate.  StartTrackedSingleEpisodeDownload
                        // upgrades this to Episode only after it verifies an
                        // exact Episode ProviderId on the local item.
                        SourceScopeType = "Season",
                        EpisodeName = item.Name ?? string.Empty,
                        Status = "pending",
                        Message = "等待下载",
                        MatchOrigin = string.Empty,
                    },
                },
            };
        }

        private DanmuDownloadTaskResult QueueSingleTargetDownload(
            DanmuDownloadTaskResult task,
            BaseItem targetItem,
            Func<Task<DanmuEpisodeDownloadOutcome>> download,
            string runningMessage)
        {
            DownloadTasks[task.TaskId] = task;
            var cancellation = new CancellationTokenSource();
            DownloadTaskCancellations[task.TaskId] = cancellation;
            _ = Task.Run(async () =>
            {
                var enteredQueue = false;
                var itemResult = task.Episodes[0];
                try
                {
                    await TrackedDownloadQueue.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    enteredQueue = true;
                    cancellation.Token.ThrowIfCancellationRequested();
                    lock (task)
                    {
                        task.Status = "running";
                        task.Message = runningMessage;
                        itemResult.Status = "running";
                        itemResult.Message = "正在下载";
                    }

                    var outcome = await AwaitSingleTargetDownload(
                        download(), cancellation.Token, task).ConfigureAwait(false);
                    NormalizeAcceptedProviderOutcome(task, outcome);
                    await PersistProviderIdAfterAcceptedOutcome(targetItem, outcome).ConfigureAwait(false);
                    lock (task)
                    {
                        itemResult.Status = outcome.Status;
                        itemResult.Message = outcome.Message;
                        itemResult.SkipReason = outcome.SkipReason ?? string.Empty;
                        UpdateCompletedTaskSummary(task);
                        task.Message = $"处理完成：成功 {task.Succeeded}，部分缺失 {task.Partial}，重复已跳过 {task.Skipped}，失败 {task.Failed}";
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (task)
                    {
                        itemResult.Status = "cancelled";
                        itemResult.Message = "已强制停止";
                        task.Status = "cancelled";
                        task.Message = "下载已停止";
                        RecalculateTaskCounts(task);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{0}] 单目标下载失败: type={1}, item={2}",
                        task.SiteName, task.TargetItemType, task.TargetItemName);
                    lock (task)
                    {
                        itemResult.Status = "failed";
                        itemResult.Message = ex.Message;
                        UpdateCompletedTaskSummary(task);
                        task.Message = "下载失败：" + ex.Message;
                    }
                }
                finally
                {
                    if (enteredQueue)
                    {
                        TrackedDownloadQueue.Release();
                    }
                    if (DownloadTaskCancellations.TryRemove(task.TaskId, out var removedCancellation))
                    {
                        removedCancellation.Dispose();
                    }
                }
            });

            return Snapshot(task);
        }

        private async Task<DanmuEpisodeDownloadOutcome> AwaitSingleTargetDownload(
            Task<DanmuEpisodeDownloadOutcome> providerTask,
            CancellationToken cancellationToken,
            DanmuDownloadTaskResult task)
        {
            var outcome = await SingleTargetDownloadArbiter.AwaitAsync(
                providerTask,
                TimeSpan.FromSeconds(180),
                cancellationToken,
                exception => _logger.LogError(
                    exception,
                    "[{0}] 单目标下载在任务结束后返回异常: type={1}, item={2}",
                    task.SiteName,
                    task.TargetItemType,
                    task.TargetItemName),
                () =>
                {
                    _logger.Warn(
                        "[{0}] 单目标下载超过 180 秒，已自动跳过: type={1}, item={2}",
                        task.SiteName,
                        task.TargetItemType,
                        task.TargetItemName);
                }).ConfigureAwait(false);
            return outcome;
        }

        private async Task PersistProviderIdAfterAcceptedOutcome(
            BaseItem item,
            DanmuEpisodeDownloadOutcome outcome)
        {
            try
            {
                await _libraryManagerEventsHelper.PersistDownloadProviderIdAsync(item, outcome).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProviderId 写回失败，但弹幕文件已保留: item={0}", item?.Name);
                if (outcome != null)
                {
                    outcome.Message = (outcome.Message ?? string.Empty) + "；ProviderId 写回失败：" + ex.Message;
                }
            }
        }

        private async Task CommitSeasonDisplayMirrorAfterTerminalAsync(
            Season season, DanmuDownloadTaskResult task)
        {
            if (season == null || task == null || !task.SeasonMirrorEligible) return;
            if (!TryBuildOwnedPlanningContext(season, out var currentContext, out _)) return;
            var terminalBuild = await BuildCompositePlanAsync(
                season, task.SeasonPlanSelections, false, task.SeasonPlanExcludedItemIds)
                .ConfigureAwait(false);
            var commit = new SeasonDisplayMirrorCommit
            {
                SeasonId = season.Id.ToString(),
                Generation = task.PlanGeneration,
                ProviderId = task.Site,
                CanonicalMediaId = task.SeasonProviderValue,
                EligibleEpisodeCount = task.Total,
                MappedEpisodeCount = task.Episodes.Count,
                TerminalEpisodeCount = task.Episodes.Count(item =>
                    string.Equals(item.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Status, "partial", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Status, "skipped", StringComparison.OrdinalIgnoreCase)),
                AcceptedEpisodeCount = task.Succeeded + task.Partial + task.Skipped,
                StableSourceCount = task.Episodes.Select(item =>
                    (item.SourceSite ?? string.Empty) + "\u001f" + (item.SourceCandidateId ?? string.Empty))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                HasUnmatchedEpisodes = task.Total != currentContext.LocalEpisodes.Count,
                HasOverlapOrDuplicate = task.Episodes.Select(item => item.ItemId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() != task.Episodes.Count,
                Cancelled = string.Equals(task.Status, "cancelled", StringComparison.OrdinalIgnoreCase),
                Failed = task.Failed > 0 || string.Equals(task.Status, "failed", StringComparison.OrdinalIgnoreCase),
                StaleStructure = terminalBuild.Plan == null ||
                    !string.Equals(task.SeasonPlanFingerprint,
                        terminalBuild.PlanFingerprint, StringComparison.Ordinal) ||
                    !SeasonPlanGenerations.IsCurrent(season.Id.ToString(), task.PlanGeneration),
                HasCanonicalSeasonIdentity = !string.IsNullOrWhiteSpace(task.SeasonProviderValue) &&
                    !task.SeasonProviderValue.StartsWith("direct-episode-provider:", StringComparison.OrdinalIgnoreCase),
            };
            if (!SeasonDisplayMirrorPolicy.CanCommit(commit, out var reason))
            {
                task.SeasonMirrorWarning = "Season identifier not updated: " + reason;
                return;
            }

            try
            {
                await _libraryManagerEventsHelper.UpsertSeasonDisplayMirrorAsync(
                    season, task.Site, task.SeasonProviderValue,
                    task.SeasonProviderWriteGeneration).ConfigureAwait(false);
                task.SeasonProviderCommitted = true;
            }
            catch (Exception ex)
            {
                task.SeasonMirrorWarning = "Season identifier update failed: " + ex.Message;
                _logger.LogError(ex, "Season display mirror upsert failed after completed download: season={0}",
                    season.Name);
            }
        }

        private static void NormalizeAcceptedProviderOutcome(
            DanmuDownloadTaskResult task,
            DanmuEpisodeDownloadOutcome outcome)
        {
            if (task == null || outcome == null || !outcome.FilePersisted ||
                (string.Equals(task.MatchOrigin, "provider-id", StringComparison.OrdinalIgnoreCase) &&
                 !string.IsNullOrWhiteSpace(task.CandidateId)))
            {
                if (task != null && outcome != null && outcome.FilePersisted &&
                    string.Equals(task.MatchOrigin, "provider-id", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(task.CandidateId))
                {
                    // Provider-id-origin work is already backed by this exact value;
                    // preserve it so the writer's idempotent check skips writeback.
                    outcome.ProviderId = task.Site;
                    outcome.ProviderValue = task.CandidateId;
                }
                return;
            }

            if (!string.Equals(task.TargetItemType, "Movie", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(task.CandidateId))
            {
                return;
            }

            // Movie downloaders may resolve a playable episode identifier internally.
            // Persist the validated Movie candidate instead, because it is the value
            // that can be passed back to GetMedia on the next preview/import.
            outcome.ProviderId = task.Site;
            outcome.ProviderValue = task.CandidateId;
        }

        private async Task SaveManualBindingAsync(BaseItem item, string providerId, string providerValue)
        {
            if (item == null || string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(providerValue))
            {
                return;
            }

            if (item is Season)
            {
                throw new DanmuDownloadErrorException("复合季不能保存单一来源的手动季绑定。");
            }

            // Use the same generation + per-season lock as automatic and
            // tracked writeback, so a late manual request cannot race a
            // composite first-file tombstone transition.
            await _libraryManagerEventsHelper.SaveProviderId(item, providerId, providerValue, true)
                .ConfigureAwait(false);
        }

        private sealed class FrozenReplayPreparation
        {
            public AbstractScraper Scraper { get; set; }
            public ScraperMedia Media { get; set; }
        }

        private async Task<DanmuDownloadTaskResult> ReplaySevenDaySkipped(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId) || !DownloadTasks.TryGetValue(taskId, out var requestedTask))
            {
                return new DanmuDownloadTaskResult
                {
                    TaskId = taskId ?? string.Empty,
                    Status = "not_found",
                    Message = "找不到原下载任务，可能是 Emby 已重启",
                };
            }

            if (!string.IsNullOrWhiteSpace(requestedTask.ReplayOriginTaskId))
            {
                var rejectedChild = Snapshot(requestedTask);
                rejectedChild.Status = "not_eligible";
                rejectedChild.ErrorCode = "origin_task_required";
                rejectedChild.Message = "只能使用原下载任务发起七天跳过重放";
                return rejectedChild;
            }

            var origin = ResolveReplayOrigin(requestedTask);
            DanmuDownloadTaskResult child;
            var replayEpisodeLeases = new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
            lock (origin)
            {
                if (!SevenDayReplayPolicy.IsTerminal(origin.Status))
                {
                    var rejected = Snapshot(origin);
                    rejected.ErrorCode = "origin_not_terminal";
                    rejected.Message = "原下载任务尚未结束，不能重放七天内跳过的剧集";
                    return rejected;
                }

                if (!string.IsNullOrWhiteSpace(origin.ReplayChildTaskId) &&
                    DownloadTasks.TryGetValue(origin.ReplayChildTaskId, out var existingChild))
                {
                    return Snapshot(existingChild);
                }

                var acceptedItemIds = GetAcceptedReplayLineageItemIds(origin.TaskId);
                var frozenEpisodes = SevenDayReplayPolicy.FreezeEligibleEpisodes(
                    origin.Episodes, acceptedItemIds);
                var availableEpisodes = new List<DanmuEpisodeDownloadResult>();
                foreach (var episode in frozenEpisodes)
                {
                    if (!TryAcquireEpisodeRetryLease(episode.ItemId, out var lease))
                    {
                        continue;
                    }

                    replayEpisodeLeases[episode.ItemId] = lease;
                    availableEpisodes.Add(episode);
                }
                frozenEpisodes = availableEpisodes;
                origin.ReplayEligibleCount = frozenEpisodes.Count;
                origin.ReplayEligible = frozenEpisodes.Count > 0;
                if (frozenEpisodes.Count == 0)
                {
                    var ineligible = Snapshot(origin);
                    ineligible.Status = "not_eligible";
                    ineligible.ErrorCode = "no_replayable_seven_day_skips";
                    ineligible.Message = "原任务没有可重放的七天内文件跳过记录";
                    return ineligible;
                }

                child = CreateSevenDayReplayTask(origin, frozenEpisodes);
                // Link before the background task starts, making concurrent
                // requests idempotently observe this exact child task.
                origin.ReplayChildTaskId = child.TaskId;
                DownloadTasks[child.TaskId] = child;
            }

            return QueueSevenDayReplay(origin, child, replayEpisodeLeases);
        }

        private static DanmuDownloadTaskResult ResolveReplayOrigin(DanmuDownloadTaskResult task)
        {
            var current = task;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (current != null && !string.IsNullOrWhiteSpace(current.ReplayOriginTaskId) &&
                   seen.Add(current.TaskId ?? string.Empty) &&
                   DownloadTasks.TryGetValue(current.ReplayOriginTaskId, out var parent))
            {
                current = parent;
            }
            return current ?? task;
        }

        private static HashSet<string> GetAcceptedReplayLineageItemIds(string originTaskId)
        {
            var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in DownloadTasks.Values)
            {
                if (candidate == null || string.Equals(candidate.TaskId, originTaskId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ResolveReplayOrigin(candidate).TaskId, originTaskId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lock (candidate)
                {
                    foreach (var episode in candidate.Episodes.Where(episode =>
                                 string.Equals(episode.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(episode.Status, "partial", StringComparison.OrdinalIgnoreCase)))
                    {
                        accepted.Add(episode.ItemId ?? string.Empty);
                    }
                }
            }
            return accepted;
        }

        private static DanmuDownloadTaskResult CreateSevenDayReplayTask(
            DanmuDownloadTaskResult origin,
            IEnumerable<DanmuEpisodeDownloadResult> frozenEpisodes)
        {
            var replayEpisodes = (frozenEpisodes ?? Enumerable.Empty<DanmuEpisodeDownloadResult>())
                .Select(SevenDayReplayPolicy.CloneEpisode)
                .Where(episode => episode != null)
                .ToList();
            foreach (var episode in replayEpisodes)
            {
                episode.Status = "pending";
                episode.Message = "等待重放";
                episode.SkipReason = string.Empty;
            }

            return new DanmuDownloadTaskResult
            {
                TaskId = Guid.NewGuid().ToString("N"),
                TargetItemId = origin.TargetItemId,
                TargetItemName = origin.TargetItemName,
                TargetItemType = "SevenDayReplay",
                SourceEpisodeNumber = origin.SourceEpisodeNumber,
                SeasonId = origin.SeasonId,
                SeriesId = origin.SeriesId,
                SeasonName = origin.SeasonName,
                SeasonNumber = origin.SeasonNumber,
                SeasonYear = origin.SeasonYear,
                Site = origin.Site,
                SiteName = origin.SiteName,
                CandidateId = origin.CandidateId,
                MatchOrigin = origin.MatchOrigin,
                IsCompositePlan = origin.IsCompositePlan,
                MappingProtocolVersion = origin.MappingProtocolVersion,
                PlanGeneration = origin.PlanGeneration,
                SeasonProviderValue = origin.SeasonProviderValue,
                SeasonStructureFingerprint = origin.SeasonStructureFingerprint,
                SeasonPlanFingerprint = origin.SeasonPlanFingerprint,
                ReplayOriginTaskId = origin.TaskId,
                ReplayKind = SevenDayReplayPolicy.ReplayKind,
                Status = "queued",
                Message = "等待七天跳过重放队列",
                Total = replayEpisodes.Count,
                ForceRefresh = true,
                Episodes = replayEpisodes,
            };
        }

        private DanmuDownloadTaskResult QueueSevenDayReplay(
            DanmuDownloadTaskResult origin,
            DanmuDownloadTaskResult task,
            IReadOnlyDictionary<string, SemaphoreSlim> episodeLeases)
        {
            var cancellation = new CancellationTokenSource();
            DownloadTaskCancellations[task.TaskId] = cancellation;
            _ = Task.Run(async () =>
            {
                var enteredQueue = false;
                CompositeSeasonProviderWriteLease compositeLease = null;
                var replayProviderTasks = new Dictionary<string, Task<DanmuEpisodeDownloadOutcome>>(
                    StringComparer.OrdinalIgnoreCase);
                try
                {
                    var season = ResolveSeason(new DanmuParams
                    {
                        Id = task.SeasonId,
                        SeriesId = task.SeriesId,
                        SeasonName = task.SeasonName,
                        SeasonNumber = task.SeasonNumber,
                        SeasonYear = task.SeasonYear,
                    });
                    if (season == null)
                    {
                        throw new DanmuDownloadErrorException("重放失败：找不到原季度媒体项");
                    }

                    compositeLease = _libraryManagerEventsHelper.BeginCompositeSeasonWrite(season, task.IsCompositePlan);
                    await TrackedDownloadQueue.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    enteredQueue = true;
                    lock (task)
                    {
                        task.Status = "running";
                        task.Message = "正在强制重放七天内跳过的弹幕";
                    }

                    foreach (var episodeResult in task.Episodes)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (!Guid.TryParse(episodeResult.ItemId, out var episodeId) ||
                            !(_libraryManager.GetItemById(episodeId) is Episode episode))
                        {
                            lock (task)
                            {
                                episodeResult.Status = "failed";
                                episodeResult.Message = "重放失败：找不到原剧集媒体项";
                            }
                            continue;
                        }

                        try
                        {
                            lock (task)
                            {
                                episodeResult.Status = "running";
                                episodeResult.Message = "正在强制重新下载";
                            }
                            var preparation = await PrepareFrozenReplayAsync(season, episode, episodeResult)
                                .ConfigureAwait(false);
                            var providerTask = _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                                episode, preparation.Media, preparation.Scraper, true, 1);
                            replayProviderTasks[episodeResult.ItemId] = providerTask;
                            var outcome = await AwaitSingleTargetDownload(
                                providerTask,
                                cancellation.Token, task).ConfigureAwait(false);
                            NormalizeFrozenReplayProviderOutcome(episodeResult, outcome);
                            await PersistProviderIdAfterAcceptedOutcome(episode, outcome).ConfigureAwait(false);
                            lock (task)
                            {
                                episodeResult.Status = outcome.Status;
                                episodeResult.Message = outcome.Message;
                                episodeResult.SkipReason = outcome.SkipReason ?? string.Empty;
                            }
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            _logger.LogError(ex, "七天跳过重放失败: season={0}, episode={1}",
                                season.Name, episode.IndexNumber);
                            lock (task)
                            {
                                episodeResult.Status = "failed";
                                episodeResult.Message = ex.Message;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (task)
                    {
                        foreach (var episode in task.Episodes.Where(episode => episode.Status == "pending" || episode.Status == "running"))
                        {
                            episode.Status = "cancelled";
                            episode.Message = "重放已强制停止";
                        }
                        task.Status = "cancelled";
                        task.Message = "七天跳过重放已停止";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "七天跳过重放任务失败: task={0}", task.TaskId);
                    lock (task)
                    {
                        foreach (var episode in task.Episodes.Where(episode => episode.Status == "pending" || episode.Status == "running"))
                        {
                            episode.Status = "failed";
                            episode.Message = ex.Message;
                        }
                        task.Status = "failed";
                        task.Message = "七天跳过重放失败：" + ex.Message;
                    }
                }
                finally
                {
                    _libraryManagerEventsHelper.CompleteCompositeSeasonWrite(compositeLease);
                    lock (task)
                    {
                        RecalculateTaskCounts(task);
                        if (!string.Equals(task.Status, "cancelled", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(task.Status, "failed", StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateCompletedTaskSummary(task);
                        }
                    }
                    RefreshReplayEligibility(origin);
                    if (enteredQueue) TrackedDownloadQueue.Release();
                    if (DownloadTaskCancellations.TryRemove(task.TaskId, out var removedCancellation))
                    {
                        removedCancellation.Dispose();
                    }
                    foreach (var episodeLease in episodeLeases ??
                             new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase))
                    {
                        replayProviderTasks.TryGetValue(episodeLease.Key, out var providerTask);
                        SingleTargetDownloadArbiter.ReleaseLeaseWhenProviderSettles(
                            providerTask, episodeLease.Value);
                    }
                }
            });

            return Snapshot(task);
        }

        private async Task<FrozenReplayPreparation> PrepareFrozenReplayAsync(
            Season season,
            Episode episode,
            DanmuEpisodeDownloadResult episodeResult)
        {
            if (string.IsNullOrWhiteSpace(episodeResult.SourceSite) ||
                string.IsNullOrWhiteSpace(episodeResult.SourceCandidateId) ||
                string.IsNullOrWhiteSpace(episodeResult.SourceEpisodeId))
            {
                throw new DanmuDownloadErrorException("重放失败：原任务缺少已验证的来源剧集证据");
            }

            var scraper = _scraperManager.All().FirstOrDefault(candidate => string.Equals(
                candidate.ProviderId, episodeResult.SourceSite, StringComparison.OrdinalIgnoreCase));
            if (scraper == null)
            {
                throw new DanmuDownloadErrorException("重放失败：原弹幕来源已不可用");
            }

            var isDirectMapping = IsDirectEpisodeProviderMapping(episodeResult);
            if (!isDirectMapping && !IsSeasonProviderMapping(episodeResult))
            {
                throw new DanmuDownloadErrorException("重放失败：原任务缺少已冻结的来源范围类型");
            }
            var resolved = isDirectMapping
                ? await DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                    scraper, episode, episodeResult.SourceCandidateId, 1).ConfigureAwait(false)
                : await scraper.GetMedia(season, episodeResult.SourceCandidateId).ConfigureAwait(false);
            var sourceEpisode = (resolved?.Episodes ?? new List<ScraperEpisode>()).FirstOrDefault(candidate =>
                candidate != null && string.Equals(candidate.Id, episodeResult.SourceEpisodeId,
                    StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(candidate.CommentId));
            if (sourceEpisode == null)
            {
                throw new DanmuDownloadErrorException("重放失败：来源剧集已变更，无法验证原映射");
            }

            return new FrozenReplayPreparation
            {
                Scraper = scraper,
                Media = new ScraperMedia
                {
                    Id = resolved.Id,
                    ProviderId = scraper.ProviderId,
                    Episodes = new List<ScraperEpisode>
                    {
                        new ScraperEpisode
                        {
                            Id = sourceEpisode.Id,
                            CommentId = sourceEpisode.CommentId,
                            EpisodeNumber = 1,
                            Title = sourceEpisode.Title,
                        },
                    },
                },
            };
        }

        private static bool IsDirectEpisodeProviderMapping(DanmuEpisodeDownloadResult episode)
        {
            return string.Equals(episode?.SourceScopeType, "Episode", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSeasonProviderMapping(DanmuEpisodeDownloadResult episode)
        {
            return string.Equals(episode?.SourceScopeType, "Season", StringComparison.OrdinalIgnoreCase);
        }

        private static void NormalizeFrozenReplayProviderOutcome(
            DanmuEpisodeDownloadResult episode,
            DanmuEpisodeDownloadOutcome outcome)
        {
            if (outcome == null || !outcome.FilePersisted || !IsDirectEpisodeProviderMapping(episode)) return;
            outcome.ProviderId = episode.SourceSite;
            outcome.ProviderValue = episode.SourceCandidateId;
        }

        private static bool TryAcquireEpisodeRetryLease(string episodeItemId, out SemaphoreSlim lease)
        {
            lease = EpisodeRetryLocks.GetOrAdd(episodeItemId ?? string.Empty, ignored => new SemaphoreSlim(1, 1));
            return lease.Wait(0);
        }

        private static void RefreshReplayEligibility(DanmuDownloadTaskResult origin)
        {
            if (origin == null) return;
            lock (origin)
            {
                var frozen = SevenDayReplayPolicy.FreezeEligibleEpisodes(
                    origin.Episodes, GetAcceptedReplayLineageItemIds(origin.TaskId));
                origin.ReplayEligibleCount = frozen.Count;
                origin.ReplayEligible = frozen.Count > 0;
            }
        }

        private async Task<DanmuDownloadTaskResult> RetryTrackedEpisode(DanmuParams request)
        {
            if (string.IsNullOrWhiteSpace(request.TaskId) ||
                !DownloadTasks.TryGetValue(request.TaskId, out var task))
            {
                return new DanmuDownloadTaskResult
                {
                    TaskId = request.TaskId ?? string.Empty,
                    Status = "not_found",
                    Message = "找不到原下载任务，可能是 Emby 已重启",
                };
            }

            DanmuEpisodeDownloadResult episodeResult;
            lock (task)
            {
                episodeResult = task.Episodes.FirstOrDefault(x =>
                    string.Equals(x.ItemId, request.Id, StringComparison.OrdinalIgnoreCase));
                if (episodeResult == null)
                {
                    task.Message = "重试失败：原任务中找不到指定剧集";
                    return Snapshot(task);
                }
            }

            // 一个季度的单集重试仍进入同一个后台串行队列，避免连续点击造成并发抓取。
            if (DownloadTaskCancellations.ContainsKey(task.TaskId))
            {
                lock (task)
                {
                    task.Message = "该季度已有下载或重试正在执行，请稍后再试";
                }
                return Snapshot(task);
            }

            if (string.Equals(task.TargetItemType, "Movie", StringComparison.OrdinalIgnoreCase))
            {
                return await RetryTrackedMovie(request, task, episodeResult).ConfigureAwait(false);
            }

            var season = ResolveSeason(new DanmuParams
            {
                Id = task.SeasonId,
                SeriesId = task.SeriesId,
                SeasonName = task.SeasonName,
                SeasonNumber = task.SeasonNumber,
                SeasonYear = task.SeasonYear,
            });
            if (!Guid.TryParse(request.Id, out var episodeId) || season == null ||
                !(_libraryManager.GetItemById(episodeId) is Episode episode))
            {
                lock (task)
                {
                    task.Message = "重试失败：找不到季度或剧集媒体项";
                }
                return Snapshot(task);
            }

            var isCompositeTask = string.Equals(task.TargetItemType, "CompositeSeason", StringComparison.OrdinalIgnoreCase);
            var isSevenDayReplayTask = string.Equals(task.TargetItemType, "SevenDayReplay", StringComparison.OrdinalIgnoreCase);
            var useFrozenEpisodeSource = isCompositeTask || isSevenDayReplayTask;
            var retryBuild = isCompositeTask
                ? await BuildCompositePlanAsync(
                    season, task.SeasonPlanSelections, false, task.SeasonPlanExcludedItemIds)
                    .ConfigureAwait(false)
                : null;
            if (isCompositeTask &&
                (retryBuild?.Plan == null ||
                 !string.Equals(task.SeasonPlanFingerprint,
                     retryBuild.PlanFingerprint, StringComparison.Ordinal) ||
                 !SeasonPlanGenerations.IsCurrent(season.Id.ToString(), task.PlanGeneration) ||
                 !retryBuild.Context.LocalEpisodes.Any(item => string.Equals(
                     item.ItemId, episodeResult.ItemId, StringComparison.OrdinalIgnoreCase))))
            {
                lock (task)
                {
                    task.ErrorCode = "stale_plan";
                    task.Message = "Retry rejected because the target Season Episode scope changed; preview again." +
                                   (string.IsNullOrWhiteSpace(retryBuild?.Error)
                                       ? string.Empty
                                       : " (" + retryBuild.Error + ")");
                }
                return Snapshot(task);
            }
            var requiresCompositeTransition = task.IsCompositePlan;
            AbstractScraper scraper = null;
            ScraperMedia media;
            try
            {
                if (useFrozenEpisodeSource)
                {
                    var preparation = await PrepareFrozenReplayAsync(season, episode, episodeResult)
                        .ConfigureAwait(false);
                    scraper = preparation.Scraper;
                    media = preparation.Media;
                }
                else
                {
                    scraper = _scraperManager.All().FirstOrDefault(x =>
                        string.Equals(x.ProviderId, task.Site, StringComparison.OrdinalIgnoreCase));
                    var candidateId = task.CandidateId;
                    if (scraper == null || string.IsNullOrWhiteSpace(candidateId))
                    {
                        throw new DanmuDownloadErrorException("重试失败：原弹幕来源或季度绑定已经失效");
                    }
                    var isDirectMapping = IsDirectEpisodeProviderMapping(episodeResult);
                    if (!isDirectMapping && !IsSeasonProviderMapping(episodeResult))
                    {
                        throw new DanmuDownloadErrorException("重试失败：原任务缺少已冻结的来源范围类型");
                    }
                    var resolved = isDirectMapping
                        ? await DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(
                            scraper,
                            episode,
                            candidateId,
                            1).ConfigureAwait(false)
                        : await scraper.GetMedia(season, candidateId).ConfigureAwait(false);
                    if (!DanmuExactEpisodeSelectionHelper.TryCreateExactMedia(
                            resolved, episodeResult.SourceEpisodeId, out media, out _))
                    {
                        throw new DanmuDownloadErrorException("The upstream episode can no longer be verified.");
                    }
                }
                if (media == null)
                {
                    throw new DanmuDownloadErrorException("弹幕来源无法读取该季度信息");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 准备单集重试失败: season={1}, episode={2}",
                    scraper?.Name ?? task.SiteName, season.Name, episode.IndexNumber);
                lock (task)
                {
                    task.Message = "重试准备失败：" + ex.Message;
                }
                return Snapshot(task);
            }

            if (!TryAcquireEpisodeRetryLease(episode.Id.ToString(), out var episodeRetryLease))
            {
                lock (task)
                {
                    task.Message = "该剧集已有重试或七天跳过重放正在执行，请稍后再试";
                }
                return Snapshot(task);
            }

            var cancellation = new CancellationTokenSource();
            if (!DownloadTaskCancellations.TryAdd(task.TaskId, cancellation))
            {
                cancellation.Dispose();
                episodeRetryLease.Release();
                lock (task)
                {
                    task.Message = "该季度已有下载或重试正在执行，请稍后再试";
                }
                return Snapshot(task);
            }

            lock (task)
            {
                episodeResult.Status = "queued";
                episodeResult.Message = "等待重试";
                RecalculateTaskCounts(task);
                task.Status = "queued";
                task.Message = $"第 {episode.IndexNumber ?? 0} 集已加入重试队列";
            }

            _ = Task.Run(async () =>
            {
                var enteredQueue = false;
                CompositeSeasonProviderWriteLease compositeLease = null;
                Task<DanmuEpisodeDownloadOutcome> providerTask = null;
                try
                {
                    if (requiresCompositeTransition)
                    {
                        // A retry can be the first successful write of an
                        // otherwise failed composite task, so it needs the
                        // same barrier/tombstone transition as the original.
                        compositeLease = _libraryManagerEventsHelper.BeginCompositeSeasonWrite(season, true);
                    }
                    await TrackedDownloadQueue.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    enteredQueue = true;
                    cancellation.Token.ThrowIfCancellationRequested();
                    lock (task)
                    {
                        task.Status = "running";
                        episodeResult.Status = "running";
                        episodeResult.Message = "正在强制重新下载";
                        task.Message = $"正在重试第 {episode.IndexNumber ?? 0} 集：{episode.Name}";
                    }

                    providerTask = _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                        episode,
                        media,
                        scraper,
                        true,
                        useFrozenEpisodeSource ? 1 : task.SourceEpisodeNumber);
                    var outcome = await AwaitSingleTargetDownload(
                        providerTask,
                        cancellation.Token,
                        task).ConfigureAwait(false);
                    // Retry reuses the per-episode frozen evidence as well.
                    // Only an Episode-scoped direct ProviderId may replace the
                    // downloader's resolved episode binding with the original
                    // direct identifier; a Season/media candidate must never
                    // be copied onto the Episode.
                    NormalizeFrozenReplayProviderOutcome(episodeResult, outcome);
                    await PersistProviderIdAfterAcceptedOutcome(episode, outcome).ConfigureAwait(false);
                    lock (task)
                    {
                        episodeResult.Status = outcome.Status;
                        episodeResult.Message = outcome.Message;
                        episodeResult.SkipReason = outcome.SkipReason ?? string.Empty;
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (task)
                    {
                        episodeResult.Status = "cancelled";
                        episodeResult.Message = "重试已强制停止";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{0}] 单集重试失败: season={1}, episode={2}",
                        scraper.Name, season.Name, episode.IndexNumber);
                    lock (task)
                    {
                        episodeResult.Status = "failed";
                        episodeResult.Message = ex.Message;
                    }
                }
                finally
                {
                    _libraryManagerEventsHelper.CompleteCompositeSeasonWrite(compositeLease);
                    lock (task)
                    {
                        UpdateCompletedTaskSummary(task);
                    }
                    await CommitSeasonDisplayMirrorAfterTerminalAsync(season, task).ConfigureAwait(false);
                    if (enteredQueue)
                    {
                        TrackedDownloadQueue.Release();
                    }
                    if (DownloadTaskCancellations.TryRemove(task.TaskId, out var removedCancellation))
                    {
                        removedCancellation.Dispose();
                    }
                    SingleTargetDownloadArbiter.ReleaseLeaseWhenProviderSettles(providerTask, episodeRetryLease);
                }
            });

            return Snapshot(task);
        }

        private async Task<DanmuDownloadTaskResult> RetryTrackedMovie(
            DanmuParams request,
            DanmuDownloadTaskResult task,
            DanmuEpisodeDownloadResult movieResult)
        {
            if (!Guid.TryParse(request.Id, out var movieId) ||
                !(_libraryManager.GetItemById(movieId) is Movie movie))
            {
                lock (task)
                {
                    task.Message = "重试失败：找不到电影媒体项";
                }
                return Snapshot(task);
            }

            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, task.Site, StringComparison.OrdinalIgnoreCase));
            var candidateId = string.IsNullOrWhiteSpace(task.CandidateId)
                ? movie.GetProviderId(task.Site)
                : task.CandidateId;
            if (scraper == null || string.IsNullOrWhiteSpace(candidateId))
            {
                lock (task)
                {
                    task.Message = "重试失败：原弹幕来源或电影绑定已经失效";
                }
                return Snapshot(task);
            }

            ScraperMedia media;
            try
            {
                media = await scraper.GetMedia(movie, candidateId).ConfigureAwait(false);
                if (media == null)
                {
                    throw new DanmuDownloadErrorException("弹幕来源无法读取电影信息");
                }
                media.SelectedMoviePartId = task.SelectedMoviePartId ?? string.Empty;
                media.PartTitle = task.PartTitle ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 准备电影重试失败: movie={1}", scraper.Name, movie.Name);
                lock (task)
                {
                    task.Message = "重试准备失败：" + ex.Message;
                }
                return Snapshot(task);
            }

            lock (task)
            {
                movieResult.Status = "queued";
                movieResult.Message = "等待重试";
                task.Status = "queued";
                task.Message = "电影已加入重试队列";
                task.ForceRefresh = true;
                RecalculateTaskCounts(task);
            }

            return QueueSingleTargetDownload(
                task,
                movie,
                () => _libraryManagerEventsHelper.DownloadMovieForProgress(movie, media, scraper, true),
                "正在强制重新下载电影弹幕");
        }

        private static void RecalculateTaskCounts(DanmuDownloadTaskResult task)
        {
            task.Succeeded = task.Episodes.Count(x => x.Status == "success");
            task.Partial = task.Episodes.Count(x => x.Status == "partial");
            task.Skipped = task.Episodes.Count(x => x.Status == "skipped");
            task.Failed = task.Episodes.Count(x => x.Status == "failed");
            task.Completed = task.Episodes.Count(x =>
                x.Status == "success" || x.Status == "partial" || x.Status == "skipped" ||
                x.Status == "failed" || x.Status == "cancelled");
        }

        private static void UpdateCompletedTaskSummary(DanmuDownloadTaskResult task)
        {
            RecalculateTaskCounts(task);
            task.ReplayEligibleCount = task.Episodes.Count(SevenDayReplayPolicy.IsRecentFileSkip);
            task.Status = task.Failed > 0
                ? "completed_with_errors"
                : (task.Partial > 0 ? "completed_with_warnings" : "completed");
            task.ReplayEligible = task.ReplayEligibleCount > 0;
            task.Message = $"本季处理完成：成功 {task.Succeeded} 集，部分弹幕缺失 {task.Partial} 集，" +
                           $"重复已跳过 {task.Skipped} 集，失败 {task.Failed} 集";
        }

        private DanmuDownloadStopResult StopAllTrackedDownloads()
        {
            var stopped = 0;
            foreach (var pair in DownloadTaskCancellations.ToArray())
            {
                if (DownloadTasks.TryGetValue(pair.Key, out var task))
                {
                    lock (task)
                    {
                        if (task.Status == "completed" || task.Status == "completed_with_warnings" ||
                            task.Status == "completed_with_errors" ||
                            task.Status == "failed" || task.Status == "cancelled")
                        {
                            continue;
                        }
                        task.Status = "stopping";
                        task.Message = "正在强制停止下载";
                    }
                }

                try
                {
                    pair.Value.Cancel();
                    stopped++;
                }
                catch (ObjectDisposedException)
                {
                    // 任务恰好在取消请求到达时完成，无需再次处理。
                }
            }

            return new DanmuDownloadStopResult
            {
                Success = true,
                StoppedTasks = stopped,
                Message = stopped > 0
                    ? $"已请求停止 {stopped} 个等待中或执行中的智能下载任务"
                    : "当前没有等待中或执行中的智能下载任务",
            };
        }

        private DanmuDownloadTaskResult GetDownloadProgress(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId) || !DownloadTasks.TryGetValue(taskId, out var task))
            {
                return new DanmuDownloadTaskResult
                {
                    TaskId = taskId ?? string.Empty,
                    Status = "not_found",
                    Message = "找不到下载任务，可能是 Emby 已重启",
                };
            }

            return Snapshot(task);
        }

        private static DanmuDownloadTaskResult Snapshot(DanmuDownloadTaskResult task)
        {
            lock (task)
            {
                return new DanmuDownloadTaskResult
                {
                    TaskId = task.TaskId,
                    TargetItemId = task.TargetItemId,
                    TargetItemName = task.TargetItemName,
                    TargetItemType = task.TargetItemType,
                    SourceEpisodeNumber = task.SourceEpisodeNumber,
                    SeasonId = task.SeasonId,
                    SeriesId = task.SeriesId,
                    SeasonName = task.SeasonName,
                    SeasonNumber = task.SeasonNumber,
                    SeasonYear = task.SeasonYear,
                    Site = task.Site,
                    SiteName = task.SiteName,
                    CandidateId = task.CandidateId,
                    PartTitle = task.PartTitle,
                    MatchOrigin = task.MatchOrigin,
                    MappingProtocolVersion = task.MappingProtocolVersion,
                    PlanGeneration = task.PlanGeneration,
                    ErrorCode = task.ErrorCode,
                    SeasonProviderValue = task.SeasonProviderValue,
                    SeasonStructureFingerprint = task.SeasonStructureFingerprint,
                    SeasonPlanFingerprint = task.SeasonPlanFingerprint,
                    SeasonMirrorEligible = task.SeasonMirrorEligible,
                    SeasonMirrorWarning = task.SeasonMirrorWarning,
                    SeasonProviderWriteGeneration = task.SeasonProviderWriteGeneration,
                    SeasonProviderCommitted = task.SeasonProviderCommitted,
                    Status = task.Status,
                    Message = task.Message,
                    Total = task.Total,
                    Completed = task.Completed,
                    Succeeded = task.Succeeded,
                    Skipped = task.Skipped,
                    Partial = task.Partial,
                    Failed = task.Failed,
                    ForceRefresh = task.ForceRefresh,
                    ReplayEligible = task.ReplayEligible,
                    ReplayEligibleCount = task.ReplayEligibleCount,
                    ReplayOriginTaskId = task.ReplayOriginTaskId,
                    ReplayChildTaskId = task.ReplayChildTaskId,
                    ReplayKind = task.ReplayKind,
                    Episodes = task.Episodes.Select(x => new DanmuEpisodeDownloadResult
                    {
                        ItemId = x.ItemId,
                        EpisodeNumber = x.EpisodeNumber,
                        SourceEpisodeNumber = x.SourceEpisodeNumber,
                        EpisodeName = x.EpisodeName,
                        SourceSite = x.SourceSite,
                        SourceCandidateId = x.SourceCandidateId,
                        SourceEpisodeId = x.SourceEpisodeId,
                        SourceScopeType = x.SourceScopeType,
                        MatchOrigin = x.MatchOrigin,
                        Status = x.Status,
                        Message = x.Message,
                        SkipReason = x.SkipReason,
                    }).ToList(),
                };
            }
        }
        
        //
        // /// <summary>
        // /// 查找弹幕
        // /// </summary>
        // [Route("/api/{site}/danmu/search")]
        // [HttpGet]
        // public async Task<IEnumerable<MediaInfo>> SearchDanmuBySite(string site, string keyword)
        // {
        //     var list = new List<MediaInfo>();
        //
        //     if (string.IsNullOrEmpty(keyword))
        //     {
        //         return list;
        //     }
        //
        //
        //     foreach (var scraper in _scraperManager.All())
        //     {
        //         try
        //         {
        //             var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
        //             if (scraperId != site)
        //             {
        //                 continue;
        //             }
        //
        //             var result = await scraper.SearchForApi(keyword).ConfigureAwait(false);
        //             foreach (var searchInfo in result)
        //             {
        //                 list.Add(new MediaInfo()
        //                 {
        //                     Id = searchInfo.Id,
        //                     Name = searchInfo.Name,
        //                     Category = searchInfo.Category,
        //                     Year = searchInfo.Year == null ? string.Empty : searchInfo.Year.ToString(),
        //                     EpisodeSize = searchInfo.EpisodeSize,
        //                     Site = scraper.Name,
        //                     SiteId = scraperId,
        //                 });
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "[{0}]Exception handled processing search movie [{1}]", scraper.Name,
        //                 keyword);
        //         }
        //     }
        //
        //     return list;
        // }
        //
        // /// <summary>
        // /// 查找弹幕
        // /// </summary>
        // [Route("/api/{site}/danmu/{id}/episodes")]
        // [HttpGet]
        // public async Task<IEnumerable<EpisodeInfo>> GetDanmuEpisodesBySite(string site, string id)
        // {
        //     var list = new List<EpisodeInfo>();
        //
        //     if (string.IsNullOrEmpty(id))
        //     {
        //         return list;
        //     }
        //
        //
        //     foreach (var scraper in _scraperManager.All())
        //     {
        //         try
        //         {
        //             var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
        //             if (scraperId != site)
        //             {
        //                 continue;
        //             }
        //
        //             var result = await scraper.GetEpisodesForApi(id).ConfigureAwait(false);
        //             foreach (var (ep, idx) in result.WithIndex())
        //             {
        //                 list.Add(new EpisodeInfo()
        //                 {
        //                     Id = ep.Id,
        //                     CommentId = ep.CommentId,
        //                     Number = idx + 1,
        //                     Title = ep.Title,
        //                 });
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "[{0}]Exception handled processing get episodes [{1}]", scraper.Name, id);
        //         }
        //     }
        //
        //     return list;
        // }
        //
        //
        // /// <summary>
        // /// 下载弹幕.
        // /// </summary>
        // [Route("/api/{site}/danmu/{cid}/download")]
        // [HttpGet]
        // public async Task<ActionResult> DownloadByCommentID(string site, string cid)
        // {
        //     if (string.IsNullOrEmpty(cid))
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     foreach (var scraper in this._scraperManager.All())
        //     {
        //         var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
        //         if (scraperId == site)
        //         {
        //             var danmaku = await scraper.DownloadDanmuForApi(cid).ConfigureAwait(false);
        //             if (danmaku != null)
        //             {
        //                 var bytes = danmaku.ToXml();
        //                 return File(bytes, "text/xml");
        //             }
        //         }
        //     }
        //
        //     throw new ResourceNotFoundException();
        // }
        //
        // /// <summary>
        // /// 跳转链接.
        // /// </summary>
        // [Route("goto")]
        // [HttpGet]
        // public RedirectResult GoTo(string provider, string id, string type)
        // {
        //     var url = $"/";
        //     switch (provider)
        //     {
        //         case "bilibili":
        //             if (id.StartsWith("BV"))
        //             {
        //                 url = $"https://www.bilibili.com/video/{id}/";
        //             }
        //             else
        //             {
        //                 if (type == "movie")
        //                 {
        //                     url = $"https://www.bilibili.com/bangumi/play/ep{id}";
        //                 }
        //                 else
        //                 {
        //                     url = $"https://www.bilibili.com/bangumi/play/ss{id}";
        //                 }
        //             }
        //
        //             break;
        //         default:
        //             break;
        //     }
        //
        //     return Redirect(url);
        // }
        //
        //
        /// <summary>
        /// 重新获取对应的弹幕id.
        /// </summary>
        /// <returns>请求结果</returns>
        public Task<String> Refresh(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ResourceNotFoundException();
            }
        
            var item = _libraryManager.GetItemById(id);
            if (item == null)
            {
                throw new ResourceNotFoundException();
            }
        
            if (item is Movie || item is Season)
            {
                _logger.Info("Movie {0}, {1}", item.Name, item.GetType());
                _libraryManagerEventsHelper.QueueItem(item, Model.EventType.Add);
                _libraryManagerEventsHelper.QueueItem(item, Model.EventType.Update);
                _libraryManagerEventsHelper.QueueItem(item, Model.EventType.Update);
            }
            else if (item is Episode)
            {
                _logger.Info("Episode {0}, {1}", item.Name, item.GetType());
                _libraryManagerEventsHelper.QueueItem(item, Model.EventType.Update);
            }
            else if (item is Series)
            {
                var seasons = ((Series)item).GetSeasons(null, new DtoOptions(false));
                foreach (var season in seasons.Where(candidate =>
                             candidate.IndexNumber.HasValue && candidate.IndexNumber.Value > 0))
                {
                    _logger.Info("season = {0}, type={1}, Guid.Empty={2}", season.Name, season.GetType(), Guid.Empty.Equals(season.Id));
                    _libraryManagerEventsHelper.QueueItem(season, Model.EventType.Add);
                    _libraryManagerEventsHelper.QueueItem(season, Model.EventType.Update);
                    _libraryManagerEventsHelper.QueueItem(season, Model.EventType.Update);
                }
            }
        
            return Task.FromResult("ok");
        }
    }
}
