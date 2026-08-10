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

        [DataMember(Name="manual")]
        public bool Manual { get; set; }

        [DataMember(Name="force")]
        public bool Force { get; set; }

        [DataMember(Name="forceRefresh")]
        public bool ForceRefresh { get; set; }

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
    }

    public class DanmuController : BaseApiService
    {
        private static readonly ConcurrentDictionary<string, DanmuDownloadTaskResult> DownloadTasks =
            new ConcurrentDictionary<string, DanmuDownloadTaskResult>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> DownloadTaskCancellations =
            new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim TrackedDownloadQueue = new SemaphoreSlim(1, 1);
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
                ItemId = item.Id.ToString(),
                ItemName = item.Name ?? string.Empty,
                ItemType = item is Series ? "Series" : item is Season ? "Season" :
                    item is Episode ? "Episode" : item is Movie ? "Movie" : item.GetType().Name,
            };

            if (item is Movie movie)
            {
                result.Target = await GetMovieMatchPreview(
                    movie,
                    request.Keyword,
                    request.Force || !string.IsNullOrWhiteSpace(request.Keyword)).ConfigureAwait(false);
                result.CanStart = result.Target.AutoSelected;
                result.Status = result.Target.Status;
                result.Message = result.Target.Message;
                return result;
            }

            if (item is Episode episode)
            {
                result.Target = await GetEpisodeMatchPreview(
                    episode,
                    request.Keyword,
                    request.Force || !string.IsNullOrWhiteSpace(request.Keyword)).ConfigureAwait(false);
                result.CanStart = result.Target.AutoSelected;
                result.Status = result.Target.Status;
                result.Message = result.Target.Message;
                return result;
            }

            var seasons = new List<Season>();
            if (item is Season season)
            {
                seasons.Add(season);
            }
            else if (item is Series series)
            {
                seasons.AddRange(series.GetSeasons(null, new DtoOptions(false))
                    .OfType<Season>()
                    .Where(x => !x.IndexNumber.HasValue || x.IndexNumber.Value != 0)
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
            }
            else
            {
                result.Status = "unsupported";
                result.Message = "仅支持电视剧和季";
                return result;
            }

            foreach (var currentSeason in seasons)
            {
                result.Seasons.Add(await GetSeasonMatchPreview(
                    currentSeason,
                    request.Keyword,
                    request.Force || !string.IsNullOrWhiteSpace(request.Keyword)).ConfigureAwait(false));
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
                if (item is Series)
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

        private async Task<DanmuItemMatchResult> GetMovieMatchPreview(
            Movie movie,
            string keywordOverride,
            bool forceSearch)
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
            if (DanmuMatchBindingHelper.TryGetSavedManualBinding(
                    forceSearch, scrapers, latest.ProviderIds, out var savedScraper, out var manualId))
            {
                    result.Status = "bound";
                    result.Message = "使用已经保存的电影手动匹配";
                    result.AutoSelected = true;
                    result.SelectedId = manualId;
                    result.SelectedSite = savedScraper.ProviderId;
                    result.SelectedSiteName = savedScraper.ProviderName;
                    result.Candidates.Add(new DanmuMatchCandidate
                    {
                        Id = manualId,
                        Site = savedScraper.ProviderId,
                        SiteName = savedScraper.ProviderName,
                        SourceOrder = savedScraper.DefaultOrder,
                        Name = "已手动绑定的电影",
                        Score = 1,
                        ManualBound = true,
                        Reason = "使用已保存的手动绑定",
                    });
                    return result;
            }

            var search = await DanmuMatchSearchEngine.SearchMovieAsync(
                scrapers,
                latest,
                keywordOverride,
                _logger).ConfigureAwait(false);
            result.Candidates = search.Candidates;
            result.SearchErrors = search.SearchErrors;
            var selected = DanmuMatchScorer.CanAutoSelect(result.Candidates)
                ? result.Candidates[0]
                : null;
            if (selected != null)
            {
                result.Status = "matched";
                result.Message = "已根据电影名和年份选出高置信度结果";
                result.AutoSelected = true;
                result.SelectedId = selected.Id;
                result.SelectedSite = selected.Site;
                result.SelectedSiteName = selected.SiteName;
            }
            else if (result.Candidates.Count == 0)
            {
                result.Status = "no_match";
                result.Message = result.SearchErrors.Count > 0
                    ? "没有搜索到电影候选，且部分网站搜索失败"
                    : "没有搜索到电影候选，可更换关键词重试";
            }
            else
            {
                result.Status = result.Candidates[0].Score >= 0.60 ? "ambiguous" : "no_match";
                result.Message = result.Status == "ambiguous"
                    ? "存在多个接近的电影结果，需要手动选择"
                    : "电影自动评分不足，需要手动选择或更换关键词";
            }

            return result;
        }

        private async Task<DanmuItemMatchResult> GetEpisodeMatchPreview(
            Episode episode,
            string keywordOverride,
            bool forceSearch)
        {
            var latest = _libraryManager.GetItemById(episode.Id) as Episode ?? episode;
            var season = latest.GetParent() as Season;
            var series = season?.GetParent() as Series;
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
            if (season == null)
            {
                result.Status = "unsupported";
                result.Message = "找不到本集所属季度";
                return result;
            }

            var seasonMatch = await GetSeasonMatchPreview(season, keywordOverride, forceSearch).ConfigureAwait(false);
            result.Candidates = seasonMatch.Candidates;
            result.SearchErrors = seasonMatch.SearchErrors;
            foreach (var candidate in result.Candidates)
            {
                var scraper = _scraperManager.All().FirstOrDefault(x =>
                    string.Equals(x.ProviderId, candidate.Site, StringComparison.OrdinalIgnoreCase));
                if (scraper == null)
                {
                    continue;
                }

                try
                {
                    var media = await scraper.GetMedia(season, candidate.Id).ConfigureAwait(false);
                    candidate.SuggestedEpisodeNumber = DanmuEpisodeMatchHelper.SuggestSourceEpisodeNumber(
                        latest.IndexNumber,
                        media?.Episodes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{0}] 解析单集候选失败: episode={1}, candidate={2}",
                        scraper.Name, latest.Name, candidate.Id);
                }
            }

            var selected = result.Candidates.FirstOrDefault(x =>
                string.Equals(x.Id, seasonMatch.SelectedId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Site, seasonMatch.SelectedSite, StringComparison.OrdinalIgnoreCase) &&
                x.SuggestedEpisodeNumber.HasValue);
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
            bool forceSearch)
        {
            var latest = _libraryManager.GetItemById(season.Id) as Season ?? season;
            var parent = latest.GetParent();
            var seriesName = parent?.Name ?? string.Empty;
            var seasonName = latest.Name ?? seriesName;
            var episodeResult = latest.GetEpisodes();
            var expectedEpisodes = episodeResult?.Items.Count(x =>
                !x.IndexNumber.HasValue || x.IndexNumber.Value > 0) ?? 0;
            var expectedYear = latest.ProductionYear;
            if ((!expectedYear.HasValue || expectedYear.Value <= 0) && episodeResult != null)
            {
                expectedYear = episodeResult.Items
                    .Where(x => x.ProductionYear.HasValue && x.ProductionYear.Value > 0)
                    .Select(x => x.ProductionYear)
                    .FirstOrDefault();
            }

            var result = new DanmuSeasonMatchResult
            {
                SeasonId = latest.Id.ToString(),
                SeriesId = parent?.Id.ToString() ?? string.Empty,
                SeasonName = seasonName,
                SeriesName = seriesName,
                SeasonNumber = latest.IndexNumber,
                Year = expectedYear,
                EpisodeCount = expectedEpisodes,
                Keyword = DanmuMatchScorer.ExtractSeasonKeyword(seriesName, seasonName),
            };

            var scrapers = _scraperManager.All();
            if (DanmuMatchBindingHelper.TryGetSavedManualBinding(
                    forceSearch, scrapers, latest.ProviderIds, out var savedScraper, out var manualId))
            {
                    result.Status = "bound";
                    result.Message = "使用已经保存的手动匹配";
                    result.AutoSelected = true;
                    result.SelectedId = manualId;
                    result.SelectedSite = savedScraper.ProviderId;
                    result.SelectedSiteName = savedScraper.ProviderName;
                    result.Candidates.Add(new DanmuMatchCandidate
                    {
                        Id = manualId,
                        Site = savedScraper.ProviderId,
                        SiteName = savedScraper.ProviderName,
                        SourceOrder = savedScraper.DefaultOrder,
                        Name = "已手动绑定的项目",
                        Score = 1,
                        ManualBound = true,
                        Reason = "使用已保存的手动绑定",
                    });
                    return result;
            }

            var search = await DanmuMatchSearchEngine.SearchSeasonAsync(
                scrapers,
                seriesName,
                seasonName,
                expectedYear,
                expectedEpisodes,
                keywordOverride,
                _logger).ConfigureAwait(false);
            result.Candidates = search.Candidates;
            result.SearchErrors = search.SearchErrors;
            var selected = DanmuMatchScorer.CanAutoSelect(result.Candidates)
                ? result.Candidates[0]
                : null;

            if (selected != null)
            {
                result.Status = "matched";
                result.Message = "已根据季名关键词、父剧名、年份和集数选出高置信度结果";
                result.AutoSelected = true;
                result.SelectedId = selected.Id;
                result.SelectedSite = selected.Site;
                result.SelectedSiteName = selected.SiteName;
                return result;
            }

            if (result.Candidates.Count == 0)
            {
                result.Status = "no_match";
                result.Message = result.SearchErrors.Count > 0
                    ? "没有搜索到候选项目，且部分网站搜索失败"
                    : "没有搜索到候选项目，可输入其他关键词重试";
                return result;
            }

            result.Status = result.Candidates[0].Score >= 0.60 ? "ambiguous" : "no_match";
            result.Message = result.Status == "ambiguous"
                ? "存在多个接近的结果，需要手动选择"
                : "自动评分不足，需要手动选择或换关键词搜索";
            return result;
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
                .Where(x => !x.IndexNumber.HasValue || x.IndexNumber.Value != 0)
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

            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null || string.IsNullOrWhiteSpace(request.CandidateId))
            {
                result.Message = "弹幕网站或候选 ID 无效";
                return result;
            }

            try
            {
                var media = await scraper.GetMedia(season, request.CandidateId).ConfigureAwait(false);
                if (media == null)
                {
                    result.Message = "该候选项目已失效或无法读取剧集信息";
                    return result;
                }

                var providerValue = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
                await _libraryManagerEventsHelper.SaveProviderId(
                    season,
                    scraper.ProviderId,
                    providerValue,
                    request.Manual).ConfigureAwait(false);
                _libraryManagerEventsHelper.QueueItem(season, EventType.Update);
                _libraryManagerEventsHelper.QueueItem(season, EventType.Update);

                result.Success = true;
                result.CandidateId = providerValue;
                result.Message = request.Manual
                    ? "已保存手动绑定并提交本季弹幕下载任务"
                    : "已绑定高置信度结果并提交本季弹幕下载任务";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 绑定弹幕候选失败: season={1}, candidate={2}",
                    scraper.Name, season.Name, request.CandidateId);
                result.Message = "绑定失败：" + ex.Message;
                return result;
            }
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

            try
            {
                var media = await scraper.GetMedia(movie, request.CandidateId).ConfigureAwait(false);
                if (media == null)
                {
                    result.Message = "电影候选已失效或无法读取";
                    return result;
                }

                var providerValue = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
                await _libraryManagerEventsHelper.SaveProviderId(
                    movie, scraper.ProviderId, providerValue, request.Manual).ConfigureAwait(false);
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

            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, request.Site, StringComparison.OrdinalIgnoreCase));
            if (scraper == null || string.IsNullOrWhiteSpace(request.CandidateId))
            {
                failed.SeasonName = season.Name ?? string.Empty;
                failed.Message = "弹幕网站或候选 ID 无效";
                return failed;
            }

            ScraperMedia media;
            try
            {
                media = await scraper.GetMedia(season, request.CandidateId).ConfigureAwait(false);
                if (media == null)
                {
                    failed.SeasonName = season.Name ?? string.Empty;
                    failed.SiteName = scraper.ProviderName;
                    failed.Message = "候选项目已失效或无法读取剧集信息";
                    return failed;
                }

                var providerValue = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
                await _libraryManagerEventsHelper.SaveProviderId(
                    season,
                    scraper.ProviderId,
                    providerValue,
                    request.Manual).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{0}] 创建可跟踪下载任务失败: season={1}", scraper.Name, season.Name);
                failed.SeasonName = season.Name ?? string.Empty;
                failed.SiteName = scraper.ProviderName;
                failed.Message = "准备下载失败：" + ex.Message;
                return failed;
            }

            var episodes = (season.GetEpisodes()?.Items ?? Array.Empty<BaseItem>())
                .OfType<Episode>()
                .Where(x => !x.IndexNumber.HasValue || x.IndexNumber.Value > 0)
                .OrderBy(x => x.IndexNumber ?? int.MaxValue)
                .ToList();
            var task = new DanmuDownloadTaskResult
            {
                TaskId = Guid.NewGuid().ToString("N"),
                SeasonId = season.Id.ToString(),
                SeriesId = request.SeriesId ?? string.Empty,
                SeasonName = season.Name ?? string.Empty,
                SeasonNumber = season.IndexNumber,
                SeasonYear = request.SeasonYear ?? season.ProductionYear,
                Site = scraper.ProviderId,
                SiteName = scraper.ProviderName,
                CandidateId = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id,
                Status = "queued",
                Message = "等待后台下载队列",
                Total = episodes.Count,
                ForceRefresh = request.ForceRefresh,
                Episodes = episodes.Select(x => new DanmuEpisodeDownloadResult
                {
                    ItemId = x.Id.ToString(),
                    EpisodeNumber = x.IndexNumber,
                    EpisodeName = x.Name ?? string.Empty,
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
                try
                {
                    if (episodes.Count == 0)
                    {
                        lock (task)
                        {
                            task.Status = "failed";
                            task.Message = "本季没有可下载的正片剧集";
                        }
                        return;
                    }

                    await TrackedDownloadQueue.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    enteredQueue = true;
                    cancellation.Token.ThrowIfCancellationRequested();
                    lock (task)
                    {
                        task.Status = "running";
                        task.Message = request.ForceRefresh
                            ? "正在强制刷新本季弹幕"
                            : "正在下载本季弹幕（7天内重复文件将跳过）";
                    }

                    for (var index = 0; index < episodes.Count; index++)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var episode = episodes[index];
                        var episodeResult = task.Episodes[index];
                        lock (task)
                        {
                            episodeResult.Status = "running";
                            episodeResult.Message = "正在下载";
                            task.Message = $"正在下载第 {episode.IndexNumber ?? index + 1} 集：{episode.Name}";
                        }

                        try
                        {
                            var outcome = await _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                                episode,
                                media,
                                scraper,
                                request.ForceRefresh).ConfigureAwait(false);
                            lock (task)
                            {
                                episodeResult.Status = outcome.Status;
                                episodeResult.Message = outcome.Message;
                                if (outcome.Status == "success")
                                {
                                    task.Succeeded++;
                                }
                                else if (outcome.Status == "partial")
                                {
                                    task.Partial++;
                                }
                                else
                                {
                                    task.Skipped++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[{0}] 可跟踪下载失败: season={1}, episode={2}",
                                scraper.Name, season.Name, episode.IndexNumber);
                            lock (task)
                            {
                                episodeResult.Status = "failed";
                                episodeResult.Message = ex.Message;
                                task.Failed++;
                            }
                        }
                        finally
                        {
                            lock (task)
                            {
                                task.Completed++;
                            }
                        }
                    }

                    lock (task)
                    {
                        UpdateCompletedTaskSummary(task);
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (task)
                    {
                        foreach (var episodeResult in task.Episodes.Where(x =>
                                     x.Status == "pending" || x.Status == "running"))
                        {
                            episodeResult.Status = "cancelled";
                            episodeResult.Message = "已强制停止";
                        }
                        task.Status = "cancelled";
                        task.Message = $"下载已停止：成功 {task.Succeeded} 集，重复已跳过 {task.Skipped} 集，失败 {task.Failed} 集";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{0}] 可跟踪下载任务异常终止: season={1}", scraper.Name, season.Name);
                    lock (task)
                    {
                        task.Status = "failed";
                        task.Message = "任务异常终止：" + ex.Message;
                    }
                }
                finally
                {
                    if (enteredQueue)
                    {
                        TrackedDownloadQueue.Release();
                    }
                    CancellationTokenSource removedCancellation;
                    if (DownloadTaskCancellations.TryRemove(task.TaskId, out removedCancellation))
                    {
                        removedCancellation.Dispose();
                    }
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

                var providerValue = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
                await _libraryManagerEventsHelper.SaveProviderId(
                    movie, scraper.ProviderId, providerValue, request.Manual).ConfigureAwait(false);
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
            return QueueSingleTargetDownload(
                task,
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
            if (sourceEpisodeNumber <= 0)
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
            try
            {
                media = await scraper.GetMedia(season, request.CandidateId).ConfigureAwait(false);
                if (media == null || !DanmuEpisodeMatchHelper.TryGetSourceEpisode(
                        media.Episodes, sourceEpisodeNumber, out var sourceEpisode) ||
                    string.IsNullOrWhiteSpace(sourceEpisode.CommentId))
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

            var task = CreateSingleTargetTask(episode, request, scraper, "Episode", sourceEpisodeNumber);
            task.SeasonId = season.Id.ToString();
            task.SeasonName = season.Name ?? string.Empty;
            task.SeasonNumber = season.IndexNumber;
            task.SeasonYear = season.ProductionYear;
            task.SeriesId = season.GetParent()?.Id.ToString() ?? string.Empty;
            task.CandidateId = string.IsNullOrWhiteSpace(media.Id) ? request.CandidateId : media.Id;
            return QueueSingleTargetDownload(
                task,
                () => _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                    episode, media, scraper, request.ForceRefresh, sourceEpisodeNumber),
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
                        EpisodeName = item.Name ?? string.Empty,
                        Status = "pending",
                        Message = "等待下载",
                    },
                },
            };
        }

        private DanmuDownloadTaskResult QueueSingleTargetDownload(
            DanmuDownloadTaskResult task,
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
                    lock (task)
                    {
                        itemResult.Status = outcome.Status;
                        itemResult.Message = outcome.Message;
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

            var scraper = _scraperManager.All().FirstOrDefault(x =>
                string.Equals(x.ProviderId, task.Site, StringComparison.OrdinalIgnoreCase));
            var candidateId = string.IsNullOrWhiteSpace(task.CandidateId)
                ? season.GetProviderId(task.Site)
                : task.CandidateId;
            if (scraper == null || string.IsNullOrWhiteSpace(candidateId))
            {
                lock (task)
                {
                    task.Message = "重试失败：原弹幕来源或季度绑定已经失效";
                }
                return Snapshot(task);
            }

            ScraperMedia media;
            try
            {
                media = await scraper.GetMedia(season, candidateId).ConfigureAwait(false);
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

            var cancellation = new CancellationTokenSource();
            if (!DownloadTaskCancellations.TryAdd(task.TaskId, cancellation))
            {
                cancellation.Dispose();
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
                try
                {
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

                    var outcome = await AwaitSingleTargetDownload(
                        _libraryManagerEventsHelper.DownloadEpisodeForProgress(
                            episode,
                            media,
                            scraper,
                            true,
                            task.SourceEpisodeNumber),
                        cancellation.Token,
                        task).ConfigureAwait(false);
                    lock (task)
                    {
                        episodeResult.Status = outcome.Status;
                        episodeResult.Message = outcome.Message;
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
                    lock (task)
                    {
                        UpdateCompletedTaskSummary(task);
                    }
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
            task.Status = task.Failed > 0
                ? "completed_with_errors"
                : (task.Partial > 0 ? "completed_with_warnings" : "completed");
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
                    Status = task.Status,
                    Message = task.Message,
                    Total = task.Total,
                    Completed = task.Completed,
                    Succeeded = task.Succeeded,
                    Skipped = task.Skipped,
                    Partial = task.Partial,
                    Failed = task.Failed,
                    ForceRefresh = task.ForceRefresh,
                    Episodes = task.Episodes.Select(x => new DanmuEpisodeDownloadResult
                    {
                        ItemId = x.ItemId,
                        EpisodeNumber = x.EpisodeNumber,
                        SourceEpisodeNumber = x.SourceEpisodeNumber,
                        EpisodeName = x.EpisodeName,
                        Status = x.Status,
                        Message = x.Message,
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
                foreach (var season in seasons)
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
