using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Configuration;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;
using Emby.Plugin.Danmu.Scraper.Bilibili; // 为了访问 Bilibili.ScraperProviderId
using Emby.Plugin.Danmu.Scraper.Iqiyi;   // 为了访问 Iqiyi.ScraperProviderId
using IFileSystem = Emby.Plugin.Danmu.Core.IFileSystem;

namespace Emby.Plugin.Danmu
{
    public class LibraryManagerEventsHelper : IDisposable
    {
        private readonly List<LibraryEvent> _queuedEvents;
        private readonly IMemoryCache _memoryCache;

        private bool ignoreEpisodesMatch = true;

        private readonly MemoryCacheEntryOptions _pendingAddExpiredOption = new MemoryCacheEntryOptions()
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };

        private readonly MemoryCacheEntryOptions _danmuUpdatedExpiredOption = new MemoryCacheEntryOptions()
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) };

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private Timer _queueTimer;
        private readonly ScraperManager _scraperManager;

        // Provider writes can be completed out of order by concurrent downloads.
        // Generations are allocated when an operation starts; commits for one
        // item/provider are serialized so an older completion cannot win later.
        private long _providerWriteGeneration;
        private readonly ProviderWriteGenerationTracker _providerWriteTracker =
            new ProviderWriteGenerationTracker();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _providerWriteLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        // A composite season has no single authoritative upstream season id.
        // Keep its durable marker outside Emby's ProviderIds and serialize all
        // Season metadata writes (including writes from different providers).
        private readonly CompositeSeasonProviderWriteCoordinator _compositeSeasonWriteCoordinator =
            new CompositeSeasonProviderWriteCoordinator();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _seasonProviderWriteLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, int> _activeCompositeSeasonBarriers =
            new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<long, CompositeSeasonFirstFileDecision> _pendingCompositeSeasonFirstFiles =
            new ConcurrentDictionary<long, CompositeSeasonFirstFileDecision>();
        private readonly Lazy<CompositeSeasonStateStore> _compositeSeasonStateStore;
        
        // 为强制下载任务设计的专用队列和信号量
        private readonly ConcurrentQueue<LibraryEvent> _forceEventQueue = new ConcurrentQueue<LibraryEvent>();
        private readonly SemaphoreSlim _forceQueueSignal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public PluginConfiguration Config
        {
            get { return Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration(); }
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryManagerEventsHelper"/> class.
        /// </summary>
        /// <param name="libraryManager">The <see cref="ILibraryManager"/>.</param>
        /// <param name="api">The <see cref="BilibiliApi"/>.</param>
        /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
        /// <param name="logManager"></param>
        /// <param name="scraperManager"></param>
        public LibraryManagerEventsHelper(ILibraryManager libraryManager, ILogManager logManager)
        {
            _queuedEvents = new List<LibraryEvent>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());

            _libraryManager = libraryManager;
            // 新增媒体信息
            _libraryManager.ItemAdded += (sender, args) =>
            {
                var argsItem = args.Item;
                QueueItem(argsItem, EventType.Add);
                QueueItem(argsItem, EventType.Update);
                QueueItem(argsItem, EventType.Update);
            };
            _logger = logManager.getDefaultLogger(GetType().ToString());
            _scraperManager = SingletonManager.ScraperManager;
            _fileSystem = FileSystem.instant;
            _compositeSeasonStateStore = new Lazy<CompositeSeasonStateStore>(CreateCompositeSeasonStateStore);
            
            // 启动强制下载任务的后台处理循环
            Task.Run(async () => await ProcessForceQueueLoopAsync(_cancellationTokenSource.Token).ConfigureAwait(false));
        }

        /// <summary>
        /// Produces the stable identity used by the plugin-private composite-season
        /// marker.  It deliberately uses stable Season/Series ownership only,
        /// never provider ids, titles, or mutable episode membership.  A library
        /// rescan may add or renumber Episodes without making a known composite
        /// Season safe to bind back to one upstream media record.
        /// </summary>
        public string GetCompositeSeasonFingerprint(Season season)
        {
            if (season == null || season.Id == Guid.Empty)
            {
                throw new ArgumentException("A persisted Season is required.", nameof(season));
            }

            var parentSeries = season.GetParent() as Series;
            var source = new StringBuilder("composite-season-fingerprint-v1\n");
            source.Append(season.Id.ToString("N")).Append('\n');
            source.Append(parentSeries?.Id.ToString("N") ?? string.Empty).Append('\n');

            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(source.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Rehydrates a durable composite marker into the in-process provider
        /// write coordinator.  A stale marker with a different fingerprint is
        /// intentionally ignored so matching starts fresh.
        /// </summary>
        public bool RestoreCompositeSeasonTombstone(Season season)
        {
            if (season == null || season.Id == Guid.Empty)
            {
                return false;
            }

            var seasonId = season.Id.ToString("N");
            try
            {
                var status = _compositeSeasonStateStore.Value.GetStatus(
                    seasonId, GetCompositeSeasonFingerprint(season), out _);
                if (status == CompositeSeasonStateLookup.NotMarked)
                {
                    return false;
                }

                _compositeSeasonWriteCoordinator.RestoreTombstone(seasonId);
                if (status == CompositeSeasonStateLookup.Unavailable)
                {
                    _logger.Error(
                        "[CompositeSeason] Private state is unreadable for Season {0}; conservatively blocking Season ProviderId writes.",
                        season.Name);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[CompositeSeason] Unable to restore private state for Season {0}; provider-id matching will remain available.",
                    season.Name);
                return false;
            }
        }

        public bool IsCompositeSeason(Season season)
        {
            if (season == null || season.Id == Guid.Empty)
            {
                return false;
            }

            return RestoreCompositeSeasonTombstone(season) ||
                   _compositeSeasonWriteCoordinator.IsTombstoned(season.Id.ToString("N"));
        }

        /// <summary>
        /// Starts a Season metadata lease. Controllers should start a composite
        /// lease before downloading its first mapped Episode and always complete
        /// it in a finally block.
        /// </summary>
        public CompositeSeasonProviderWriteLease BeginCompositeSeasonWrite(Season season, bool compositePlan)
        {
            if (season == null || season.Id == Guid.Empty)
            {
                throw new ArgumentException("A persisted Season is required.", nameof(season));
            }

            RestoreCompositeSeasonTombstone(season);
            var lease = _compositeSeasonWriteCoordinator.BeginWrite(season.Id.ToString("N"), compositePlan);
            if (compositePlan)
            {
                _activeCompositeSeasonBarriers.AddOrUpdate(lease.SeasonId, 1, (_, count) => count + 1);
            }

            return lease;
        }

        public void CompleteCompositeSeasonWrite(CompositeSeasonProviderWriteLease lease)
        {
            if (lease == null)
            {
                return;
            }

            _pendingCompositeSeasonFirstFiles.TryRemove(lease.LeaseId, out _);
            _compositeSeasonWriteCoordinator.Complete(lease);
            if (!lease.IsCompositePlan)
            {
                return;
            }

            while (_activeCompositeSeasonBarriers.TryGetValue(lease.SeasonId, out var count))
            {
                if (count <= 1)
                {
                    if (_activeCompositeSeasonBarriers.TryRemove(lease.SeasonId, out _))
                    {
                        break;
                    }
                }
                else if (_activeCompositeSeasonBarriers.TryUpdate(lease.SeasonId, count - 1, count))
                {
                    break;
                }
            }
        }

        private CompositeSeasonStateStore CreateCompositeSeasonStateStore()
        {
            var plugin = Plugin.Instance;
            if (plugin == null || string.IsNullOrWhiteSpace(plugin.DataFolderPath))
            {
                throw new InvalidOperationException("Plugin data folder is unavailable for composite-season state.");
            }

            return new CompositeSeasonStateStore(
                Path.Combine(plugin.DataFolderPath, "composite-seasons"));
        }

        private bool IsSeasonProviderIdWriteBlocked(Season season)
        {
            if (season == null || season.Id == Guid.Empty)
            {
                return false;
            }

            var seasonId = season.Id.ToString("N");
            return IsCompositeSeason(season) ||
                   _activeCompositeSeasonBarriers.ContainsKey(seasonId);
        }

        /// <summary>
        /// Queues an item to be added to trakt.
        /// </summary>
        /// <param name="item"> The <see cref="BaseItem"/>.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        public void QueueItem(BaseItem item, EventType eventType)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // 对于强制事件(手动搜索触发)，将其加入专用队列，由后台任务依次处理
            if (eventType == EventType.Force)
            {
                _logger.Info("将强制下载任务加入队列: {0}", item.Name);
                var libraryEvent = new LibraryEvent { Item = item, EventType = eventType };
                _forceEventQueue.Enqueue(libraryEvent);
                _forceQueueSignal.Release(); // 发送信号，通知处理器有新任务
                return;
            }

            // 对于其他事件类型(Add, Update)，使用现有的延迟队列逻辑
            lock (_queuedEvents)
            {
                if (_queueTimer == null)
                {
                    _queueTimer = new Timer(
                        OnQueueTimerCallback,
                        null,
                        TimeSpan.FromMilliseconds(10000),
                        Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _queueTimer.Change(TimeSpan.FromMilliseconds(10000), Timeout.InfiniteTimeSpan);
                }

                _queuedEvents.Add(new LibraryEvent { Item = item, EventType = eventType });
            }
        }

        /// <summary>
        /// Wait for timer callback to be completed.
        /// </summary>
        private async void OnQueueTimerCallback(object state)
        {
            try
            {
                await OnQueueTimerCallbackInternal().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnQueueTimerCallbackInternal");
            }
        }
        
        /// <summary>
        /// 循环处理强制下载队列中的任务。
        /// </summary>
        private async Task ProcessForceQueueLoopAsync(CancellationToken cancellationToken)
        {
            _logger.Info("强制下载任务队列处理器已启动。");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 检查队列是否为空。如果为空，则等待信号。
                    if (_forceEventQueue.IsEmpty)
                    {
                        _logger.Debug("强制下载队列为空，等待新任务...");
                        await _forceQueueSignal.WaitAsync(cancellationToken);
                    }

                    // 尝试从队列中取出任务
                    if (_forceEventQueue.TryDequeue(out var libraryEvent)) 
                    {
                        _logger.Info("开始处理队列中的强制下载任务: {0}", libraryEvent.Item.Name);
                        try
                        {
                            if (libraryEvent.Item is Movie)
                            {
                                await ProcessQueuedMovieEvents(new[] { libraryEvent }, EventType.Force).ConfigureAwait(false);
                            }
                            else if (libraryEvent.Item is Episode)
                            {
                                await ProcessQueuedEpisodeEvents(new[] { libraryEvent }, EventType.Force).ConfigureAwait(false);
                            }
                            _logger.Info("已完成强制下载任务: {0}", libraryEvent.Item.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "处理强制下载任务时发生错误: {0}", libraryEvent.Item.Name);
                        }

                        // 每个任务处理完毕后，等待2秒
                        _logger.Debug("强制下载任务处理完成，等待2秒...");
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("强制下载任务队列处理器已停止。");
                    break; // 正常退出
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "强制下载任务队列处理器发生意外错误。");
                }
            }
        }

        /// <summary>
        /// Wait for timer to be completed.
        /// </summary>
        private async Task OnQueueTimerCallbackInternal()
        {
            // _logger.LogInformation("Timer elapsed - processing queued items");
            List<LibraryEvent> queue;

            lock (_queuedEvents)
            {
                if (!_queuedEvents.Any())
                {
                    _logger.LogInformation("No events... stopping queue timer");
                    return;
                }

                queue = _queuedEvents.ToList();
                _queuedEvents.Clear();
            }

            var queuedMovieAdds = new List<LibraryEvent>();
            var queuedMovieUpdates = new List<LibraryEvent>();
            var queuedMovieForces = new List<LibraryEvent>();
            var queuedEpisodeAdds = new List<LibraryEvent>();
            var queuedEpisodeUpdates = new List<LibraryEvent>();
            var queuedEpisodeForces = new List<LibraryEvent>();
            var queuedShowAdds = new List<LibraryEvent>();
            var queuedShowUpdates = new List<LibraryEvent>();
            var queuedSeasonAdds = new List<LibraryEvent>();
            var queuedSeasonUpdates = new List<LibraryEvent>();

            // add事件可能会在获取元数据完之前执行，导致可能会中断元数据获取，通过pending集合把add事件延缓到获取元数据后再执行（获取完元数据后，一般会多推送一个update事件）
            foreach (var ev in queue)
            {
                // item所在的媒体库不启用弹幕插件，忽略处理
                if (IsIgnoreItem(ev.Item))
                {
                    continue;
                }

                if (ev.Item is Movie movie)
                {
                    if (ev.EventType == EventType.Add)
                    {
                        _logger.LogInformation("Movie add: {0}", movie.Name);
                        _memoryCache.Set<LibraryEvent>(movie.Id, ev, _pendingAddExpiredOption);
                    }
                    else if (ev.EventType == EventType.Update)
                    {
                        _logger.LogInformation("Movie update: {0}", movie.Name);
                        if (_memoryCache.TryGetValue<LibraryEvent>(movie.Id, out LibraryEvent addMovieEv))
                        {
                            queuedMovieAdds.Add(addMovieEv);
                            _memoryCache.Remove(movie.Id);
                        }
                        else
                        {
                            queuedMovieUpdates.Add(ev);
                        }
                    }
                    else if (ev.EventType == EventType.Force)
                    {
                        _logger.LogInformation("Movie force: {0}", movie.Name);
                        queuedMovieForces.Add(ev);
                    }
                }
                else if (ev.Item is Series series)
                {
                    if (ev.EventType == EventType.Add)
                    {
                        _logger.LogInformation("Series add: {0}", series.Name);
                        // 处理系列添加逻辑...
                    }
                    else if (ev.EventType == EventType.Update)
                    {
                        _logger.LogInformation("Series update: {0}", series.Name);
                        // 处理系列更新逻辑...
                    }
                }
                else if (ev.Item is Season season)
                {
                    var seasonId = season.GetSeasonId().ToString();
                    if (ev.EventType == EventType.Add)
                    {
                        _logger.LogInformation("Season add: {0}, id={1}", season.Name, seasonId);
                        _memoryCache.Set<LibraryEvent>(seasonId, ev, _pendingAddExpiredOption);
                    }
                    else if (ev.EventType == EventType.Update)
                    {
                        bool tryGetValue = _memoryCache.TryGetValue<LibraryEvent>(seasonId, out LibraryEvent addSeasonEv);
                        _logger.LogInformation("Season update: {0}, id={1}, tryGetValue={2}", season.Name, seasonId, tryGetValue);
                        if (tryGetValue)
                        {
                            queuedSeasonAdds.Add(addSeasonEv);
                            _memoryCache.Remove(seasonId);
                        }
                        else if (queuedSeasonAdds.Any(candidate =>
                                     candidate.Item != null && candidate.Item.Id == season.Id))
                        {
                            // ItemAdded deliberately emits duplicate Update notifications. Once
                            // this batch has promoted the pending Add, do not run the same fresh
                            // identifier-free Season plan a second time.
                            _logger.LogInformation(
                                "[SmartMatch] Suppress duplicate Season Update after Add: season={0}, id={1}",
                                season.Name, seasonId);
                        }
                        else
                        {
                            queuedSeasonUpdates.Add(ev);
                        }
                    }
                }
                else if (ev.Item is Episode episode)
                {
                    if (ev.EventType == EventType.Update)
                    {
                        _logger.LogInformation("Episode update: {0}.{1}", episode.IndexNumber, episode.Name);
                        queuedEpisodeUpdates.Add(ev);
                    }
                    else if (ev.EventType == EventType.Force)
                    {
                        _logger.LogInformation("Episode force: {0}.{1}", episode.IndexNumber, episode.Name);
                        queuedEpisodeForces.Add(ev);
                    }
                }
            }

            // 对于剧集，处理顺序也很重要（Add事件后，会刷新元数据，导致会同时推送Update事件）
            await ProcessQueuedMovieEvents(queuedMovieAdds, EventType.Add).ConfigureAwait(false);
            await ProcessQueuedMovieEvents(queuedMovieUpdates, EventType.Update).ConfigureAwait(false);

            await ProcessQueuedShowEvents(queuedShowAdds, EventType.Add).ConfigureAwait(false);
            await ProcessQueuedSeasonEvents(queuedSeasonAdds, EventType.Add).ConfigureAwait(false);
            await ProcessQueuedEpisodeEvents(queuedEpisodeAdds, EventType.Add).ConfigureAwait(false);

            await ProcessQueuedShowEvents(queuedShowUpdates, EventType.Update).ConfigureAwait(false);
            await ProcessQueuedSeasonEvents(queuedSeasonUpdates, EventType.Update).ConfigureAwait(false);
            await ProcessQueuedEpisodeEvents(queuedEpisodeUpdates, EventType.Update).ConfigureAwait(false);

            await ProcessQueuedMovieEvents(queuedMovieForces, EventType.Force).ConfigureAwait(false);
            await ProcessQueuedEpisodeEvents(queuedEpisodeForces, EventType.Force).ConfigureAwait(false);
        }

        public bool IsIgnoreItem(BaseItem item)
        {
            // item所在的媒体库不启用弹幕插件，忽略处理
            var libraryOptions = _libraryManager.GetLibraryOptions(item);
            if (libraryOptions != null && libraryOptions.DisabledSubtitleFetchers.Contains(Plugin.Instance?.Name))
            {
                this._logger.LogInformation($"媒体库已关闭danmu插件, 忽略处理[{item.Name}].");
                return true;
            }

            return false;
        }


        /// <summary>
        /// Processes queued movie events.
        /// </summary>
        /// <param name="events">The <see cref="LibraryEvent"/> enumerable.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <returns>Task.</returns>
        public async Task ProcessQueuedMovieEvents(IReadOnlyCollection<LibraryEvent> events, EventType eventType)
        {
            if (events.Count == 0)
            {
                return;
            }

            var movies = new HashSet<Movie>(events
                .Select(lev => lev.Item as Movie) // 显式进行类型转换
                .Where(lev => lev != null && !string.IsNullOrEmpty(lev.Name))); // 确保movie 不是 null 之后再检查 Name 属性

            // 新增事件也会触发update，不需要处理Add
            // 更新，判断是否有bvid，有的话刷新弹幕文件
            if (eventType == EventType.Add)
            {
                // var queueUpdateMeta = new List<BaseItem>();
                var enabledScrapers = _scraperManager.All().ToList();
                foreach (var item in movies)
                {
                    DanmuMatchCandidate selectedMovieCandidate = null;
                    var movieMatchSearched = false;
                    foreach (var scraper in enabledScrapers)
                    {
                        try
                        {
                            // 读取最新数据，要不然取不到年份信息
                            if (!movieMatchSearched)
                            {
                                var currentItem = _libraryManager.GetItemById(item.InternalId) as Movie ?? item;
                                var providerDecision = await DanmuProviderIdResolver.ResolveAsync(
                                    enabledScrapers, DanmuProviderIdResolver.GetMovieScopes(currentItem), _logger).ConfigureAwait(false);
                                selectedMovieCandidate = providerDecision.Candidate;
                                if (selectedMovieCandidate == null &&
                                    DanmuMatchBindingHelper.TryGetSavedManualBinding(
                                        false,
                                        enabledScrapers,
                                        currentItem.ProviderIds,
                                        out var boundScraper,
                                        out var boundId))
                                {
                                    selectedMovieCandidate = new DanmuMatchCandidate
                                    {
                                        Id = boundId,
                                        Site = boundScraper.ProviderId,
                                        SiteName = boundScraper.ProviderName,
                                        Name = currentItem.Name,
                                        MatchOrigin = "binding",
                                        DecisionReason = "saved-binding",
                                    };
                                }
                                if (selectedMovieCandidate == null)
                                {
                                    var movieSearch = await DanmuMatchSearchEngine.SearchMovieAsync(
                                        enabledScrapers, currentItem, null, _logger).ConfigureAwait(false);
                                    if (!CanUseAutomaticSearch(movieSearch))
                                    {
                                        LogIncompleteAutomaticSearch(currentItem.Name, "movie", movieSearch);
                                    }
                                    else
                                    {
                                        selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate(movieSearch.CanonicalCandidates);
                                    }
                                }
                                movieMatchSearched = true;
                            }
                            if (selectedMovieCandidate == null ||
                                !string.Equals(scraper.ProviderId, selectedMovieCandidate.Site, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var mediaId = selectedMovieCandidate.Id;

                            var media = await scraper.GetMedia(item, mediaId);
                            if (media != null)
                            {
                                // media.ProviderId 应该由 scraper 的 GetMedia 方法设置。
                                // 如果未设置，为安全起见在此处设置，但最好在 scraper 中完成。
                                if (string.IsNullOrEmpty(media.ProviderId)) media.ProviderId = scraper.ProviderId;

                                string idToUseForDanmakuProcessing = string.Empty;

                                if (media.ProviderId == Bilibili.ScraperProviderId)
                                {
                                    // 对于B站电影, media.CommentId 应该是主内容的 ep_id
                                    idToUseForDanmakuProcessing = media.CommentId;
                                    if (string.IsNullOrEmpty(idToUseForDanmakuProcessing) && media.Episodes.Any())
                                    {
                                        idToUseForDanmakuProcessing = media.Episodes.First().CommentId; // 备选，使用第一个分P的 ep_id
                                    }
                                    _logger.LogInformation($"[{scraper.Name}] Bilibili Movie Add: 确定用于弹幕处理的 ep_id '{idToUseForDanmakuProcessing}'.");
                                }
                                else // 对于爱奇艺、腾讯、优酷等电影
                                {
                                    // 假设 media.CommentId 已被各自的 scraper 填充为 DownloadDanmu 方法直接需要的ID (例如爱奇艺的 TvId)
                                    idToUseForDanmakuProcessing = media.CommentId;
                                    _logger.LogInformation($"[{scraper.Name}] Non-Bilibili Movie Add: 使用 media.CommentId '{idToUseForDanmakuProcessing}' 进行弹幕处理.");
                                }

                                _logger.LogInformation("[{0}]匹配成功：name='{1}', SearchMediaId='{2}', IdForDanmakuProcessing='{3}'", 
                                    scraper.Name, item.Name, mediaId, idToUseForDanmakuProcessing);

                                // 更新epid元数据
                                // 对于电影，ProviderId 存储的是搜索时用的ID (mediaId, 如B站的season_id, 爱奇艺的LinkId, 腾讯的cid, 优酷的show_id)
                                // The binding is persisted only after a valid XML file is written.
                                // 可以考虑额外存储一个特定于播放的 ep_id，如果 Emby 支持多个 ProviderId 或自定义字段
                                // 例如: item.SetProviderId($"{scraper.ProviderId}_Playable", idToUseForDanmakuProcessing);

                                // 下载弹幕
                                if (!string.IsNullOrEmpty(idToUseForDanmakuProcessing)) {
                                    // 对于B站, DownloadDanmu 会调用 GetMediaEpisode 并传入此 ep_id 来获取 aid,cid
                                    // 对于爱奇艺, DownloadDanmu 会直接使用此 TvId
                                    var outcome = await this.DownloadMovieForProgress(item, media, scraper, false).ConfigureAwait(false);
                                    if (outcome.FilePersisted &&
                                        (string.Equals(outcome.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(outcome.Status, "partial", StringComparison.OrdinalIgnoreCase)))
                                    {
                                        await PersistDownloadProviderIdAsync(item, outcome, mediaId).ConfigureAwait(false);
                                    }
                                } else {
                                    _logger.Warn($"[{scraper.Name}]为电影 '{item.Name}' (SearchMediaId: {mediaId}) 未能从GetMedia结果中确定有效的ID (media.CommentId 或首个 episode 的 CommentId) 用于下载弹幕. media.Id='{media.Id}', media.CommentId='{media.CommentId}'");
                                }
                                if (!Config.OpenAllSource)
                                {
                                    break;
                                }
                            }
                        }
                        catch (DanmuDownloadErrorException ex)
                        {
                            _logger.LogError(ex, "[{0}]弹幕下载失败，尝试匹配下一个. 失败原因={1}", scraper.Name, ex.Message);
                        }
                        catch (FrequentlyRequestException ex)
                        {
                            _logger.LogError(ex, "[{0}]api接口触发风控，中止执行，请稍候再试.", scraper.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[{0}]Exception handled processing movie events", scraper.Name);
                        }
                    }
                }

                // await ProcessQueuedUpdateMeta(queueUpdateMeta).ConfigureAwait(false);
            }


            // 更新
            if (eventType == EventType.Update)
            {
                foreach (var item in movies)
                {
                    foreach (var scraper in _scraperManager.All())
                    {
                        try
                        {
                            var providerVal = item.GetProviderId(scraper.ProviderId);
                            if (!string.IsNullOrEmpty(providerVal))
                            {
                                // providerVal 是存储的 season_id/media_id (例如 '41175')
                                // 需要先调用 GetMedia 来获取包含实际 ep_id 的 ScraperMedia
                                var media = await scraper.GetMedia(item, providerVal);
                                if (media != null) // media.Id is LinkId for Iqiyi, BVID for Bilibili. media.CommentId is TvId for Iqiyi, ep_id for Bilibili.
                                {
                                    if (string.IsNullOrEmpty(media.ProviderId)) media.ProviderId = scraper.ProviderId;

                                    string idForGetMediaEpisode = string.Empty;
                                    if (media.ProviderId == Bilibili.ScraperProviderId)
                                    {
                                        idForGetMediaEpisode = media.CommentId; // 对于B站电影，这应该是主要内容的 ep_id
                                        if (string.IsNullOrEmpty(idForGetMediaEpisode) && media.Episodes.Any())
                                        {
                                            idForGetMediaEpisode = media.Episodes.First().CommentId; // 备选方案
                                        }
                                        _logger.LogInformation($"[{scraper.Name}] B站电影更新：用于 GetMediaEpisode 的ID (ep_id): '{idForGetMediaEpisode}'.");
                                    }
                                    else // For Iqiyi, Tencent, Youku movies
                                    {
                                        // 这些提供商的 GetMediaEpisode 方法可能期望接收主要的媒体ID (providerVal)
                                        // 或者，根据它们各自的实现，也可能期望接收特定的可播放ID (media.CommentId)。
                                        // 我们假设这些提供商的电影 GetMediaEpisode 方法期望接收的是之前存储的ID (providerVal)。
                                        // 如果它们的 GetMediaEpisode 方法被设计为接收 media.CommentId (例如，爱奇艺的 TvId)，那么就应该使用 media.CommentId。
                                        // 对于腾讯视频，GetMediaEpisode 方法期望接收 cid (即 providerVal / media.Id)。
                                        // 对于爱奇艺，GetMediaEpisode 方法期望接收 LinkId (即 providerVal / media.Id)。
                                        // 对于优酷，GetMediaEpisode 方法期望接收 show_id (即 providerVal / media.Id)。
                                        idForGetMediaEpisode = providerVal; // 使用最初存储的ID (LinkId, cid, show_id)
                                        _logger.LogInformation($"[{scraper.Name}] 非B站 ({media.ProviderId}) 电影更新：用于 GetMediaEpisode 的ID: '{idForGetMediaEpisode}' (使用已存储的 providerVal). media.CommentId 为 '{media.CommentId}'");
                                    }

                                    if (!string.IsNullOrEmpty(idForGetMediaEpisode))
                                    {
                                        var episodeDetails = await scraper.GetMediaEpisode(item, idForGetMediaEpisode);
                                        if (episodeDetails != null && !string.IsNullOrEmpty(episodeDetails.CommentId))
                                        {
                                            // episodeDetails.CommentId is the FINAL ID for danmaku (e.g. "aid,cid" for Bili, "TvId" for Iqiyi)
                                            _logger.LogInformation("[{0}]为电影 '{1}' (ProviderVal: {2}, ID for GetMediaEpisode: {3}) 成功获取剧集信息，最终CommentId for Danmaku: {4}", scraper.Name, item.Name, providerVal, idForGetMediaEpisode, episodeDetails.CommentId);
                                            await this.DownloadMovieForProgress(item, media, scraper, false).ConfigureAwait(false);
                                        } else
                                        {
                                             _logger.Warn($"[{scraper.Name}]为电影 '{item.Name}' (ProviderVal: {providerVal}, ID for GetMediaEpisode: {idForGetMediaEpisode}) 调用 GetMediaEpisode 返回了 null 或无效CommentId。");
                                        }
                                    } else {
                                        _logger.Warn($"[{scraper.Name}]为电影 '{item.Name}' (ProviderVal: {providerVal}) 调用 GetMedia 后未能确定有效的 ID for GetMediaEpisode.");
                                    }
                                } else {
                                    _logger.Warn($"[{scraper.Name}]为电影 '{item.Name}' (ProviderVal: {providerVal}) 调用 GetMedia 返回了 null。");
                                }
                                // TODO：兼容支持用户设置seasonId？？？
                                break;
                            }
                        }
                        catch (FrequentlyRequestException ex)
                        {
                            _logger.LogError(ex, "api接口触发风控，中止执行，请稍候再试.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception handled processing queued movie events");
                        }
                    }
                }
            }

            // 强制刷新指定来源弹幕
            if (eventType == EventType.Force)
            {
                foreach (var queueItem in movies)
                {
                    // 找到选择的scraper
                    var scraper = _scraperManager.All()
                        .FirstOrDefault(x => queueItem.ProviderIds.ContainsKey(x.ProviderId));
                    if (scraper == null)
                    {
                        continue;
                    }

                    // 获取选择的弹幕Id
                    var mediaId = queueItem.GetProviderId(scraper.ProviderId);
                    if (string.IsNullOrEmpty(mediaId))
                    {
                        continue;
                    }

                    // 获取最新的item数据
                    var item = _libraryManager.GetItemById(queueItem.Id);
                    var media = await scraper.GetMedia(item, mediaId);
                    if (media != null)
                    {
                        // 确定用于获取剧集详情的ID。
                        // 对于B站电影，需要使用ep_id，它存储在media.CommentId中。
                        // 对于其他提供商，通常使用主要的媒体ID，它存储在media.Id中。
                        string idForEpisodeDetails;
                        if (scraper.ProviderId == Bilibili.ScraperProviderId)
                        {
                            idForEpisodeDetails = media.CommentId;
                        }
                        else
                        {
                            idForEpisodeDetails = media.Id;
                        }

                        _logger.LogInformation($"[{scraper.Name}] 强制刷新电影 '{item.Name}': 使用 ID '{idForEpisodeDetails}' 来获取剧集详情。");
                        var episode = await scraper.GetMediaEpisode(item, idForEpisodeDetails);
                        if (episode != null)
                        {
                            // 下载弹幕xml文件
                            var outcome = await this.DownloadMovieForProgress((Movie)item, media, scraper, true).ConfigureAwait(false);
                            if (outcome.FilePersisted &&
                                (string.Equals(outcome.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(outcome.Status, "partial", StringComparison.OrdinalIgnoreCase)))
                            {
                                await PersistDownloadProviderIdAsync(item, outcome, mediaId).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            _logger.Warn($"[{scraper.Name}] 强制刷新电影 '{item.Name}': 使用 ID '{idForEpisodeDetails}' 调用 GetMediaEpisode 后返回 null。");
                        }
                    }
                }
            }
        }

        public async Task UpdateSeason(BaseItem item, bool force=false)
        {
            if (!force)
            {
                if (!(item is Season) && item.HasAnyDanmuProviderIds())
                {
                    return;
                }
            }

            EventType eventType = force ? EventType.Force : EventType.Add;
            List<LibraryEvent> libraryEvents = new List<LibraryEvent>() { new LibraryEvent(){Item= item, EventType = eventType} };
            if (item is Season)
            {
                await ProcessQueuedSeasonEvents(libraryEvents, eventType);
            }
            else if (item is Episode)
            {
                await ProcessQueuedEpisodeEvents(libraryEvents, eventType);
            }
        }

        /// <summary>
        /// Processes queued show events.
        /// </summary>
        /// <param name="events">The <see cref="LibraryEvent"/> enumerable.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <returns>Task.</returns>
        public async Task ProcessQueuedShowEvents(IReadOnlyCollection<LibraryEvent> events, EventType eventType)
        {
            if (events.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Processing {Count} shows with event type {EventType}", events.Count, eventType);

            var series = new HashSet<Series>(events
                .Select(lev => lev.Item as Series) // 显式进行类型转换
                .Where(lev => lev != null && !string.IsNullOrEmpty(lev.Name)));

            try
            {
                if (eventType == EventType.Update)
                {
                    foreach (var item in series)
                    {
                        var seasons = item.GetSeasons(null, new DtoOptions(false));
                        foreach (var season in seasons.Where(candidate =>
                                     candidate.IndexNumber.HasValue && candidate.IndexNumber.Value > 0))
                        {
                            // 发现season保存元数据，不会推送update事件，这里通过series的update事件推送刷新
                            QueueItem(season, eventType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception handled processing queued show events");
            }
        }

        /// <summary>
        /// Processes queued season events.
        /// </summary>
        /// <param name="events">The <see cref="LibraryEvent"/> enumerable.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <returns>Task.</returns>
        public async Task ProcessQueuedSeasonEvents(IReadOnlyCollection<LibraryEvent> events, EventType eventType)
        {
            if (events.Count == 0)
            {
                return;
            }

            _logger.Info("Processing count={0} seasons with event type {1}", events.Count, eventType);
            var seasons = new HashSet<Season>(events
                .Select(lev => lev.Item as Season) // 显式进行类型转换
                .Where(lev => lev != null && !string.IsNullOrEmpty(lev.Name)));

            if (eventType == EventType.Add || eventType == EventType.Update)
            {
                var queueUpdateMeta = new List<BaseItem>();
                foreach (var season in seasons)
                {
                    // // 虚拟季第一次请求忽略
                    // if (season.LocationType == LocationType.Virtual && season.IndexNumber is null)
                    // {
                    //     continue;
                    // }

                    // Unattended/media-import work is positive-Season only.
                    // Reject S0 before inventory construction, provider search,
                    // planning, download, binding, or metadata persistence.
                    if (!season.IndexNumber.HasValue || season.IndexNumber.Value <= 0)
                    {
                        _logger.LogInformation(
                            "[SmartMatch] Automatic Season target number is unavailable: name={0} number={1}",
                            season.Name, season.IndexNumber);
                        continue;
                    }

                    var automaticGeneration = SeasonPlanGenerationCoordinator.Shared.Begin(
                        season.Id.ToString());

                    var series = season.GetParent();
                    var authoritativeSeries = series is Series parentSeries
                        ? _libraryManager.GetItemById(parentSeries.InternalId) as Series ?? parentSeries
                        : null;
                    var originalSeasonName = season.Name;
                    var scrapers = _scraperManager.All();

                    // Read the latest season metadata before matching.  The manual API
                    // and this automatic library-import path intentionally share the
                    // same cross-provider search engine and global score ordering.
                    var currentItem = _libraryManager.GetItemById(season.Id) as Season;
                    if (currentItem != null)
                    {
                        season.ProductionYear = currentItem.ProductionYear;
                        originalSeasonName = currentItem.Name ?? originalSeasonName;
                    }

                    AbstractScraper selectedScraper = null;
                    string selectedMediaId = null;
                    DanmuMatchCandidate selectedCandidate = null;

                    try
                    {
                        if (selectedScraper == null)
                        {
                            if (!TryBuildAutomaticPlanningContext(season, out var planning, out var ownershipError))
                            {
                                _logger.LogInformation("[SmartMatch] Automatic Season ownership rejected: season={0}, error={1}",
                                    season.Name, ownershipError);
                                continue;
                            }
                            if (eventType == EventType.Update && planning.Episodes.All(episode =>
                                    scrapers.Any(candidate =>
                                    {
                                        var path = Path.Combine(episode.ContainingFolderPath,
                                            episode.GetDanmuXmlPath(candidate.ProviderId));
                                        return DateTime.Now - _fileSystem.GetLastWriteTime(path) <
                                               TimeSpan.FromDays(7);
                                    })))
                            {
                                _logger.LogInformation(
                                    "[SmartMatch] Automatic Season Update is fresh; skip rematch: season={0}",
                                    season.Name);
                                continue;
                            }
                            var expectedEpisodes = planning.Episodes.Count;
                            var expectedYear = season.ProductionYear;
                            if (!expectedYear.HasValue || expectedYear.Value <= 0)
                            {
                                expectedYear = planning.Episodes
                                    .Where(x => x.ProductionYear.HasValue && x.ProductionYear.Value > 0)
                                    .Select(x => x.ProductionYear)
                                    .FirstOrDefault();
                            }

                            var search = await DanmuMatchSearchEngine.SearchSeasonAsync(
                                scrapers,
                                series?.Name ?? string.Empty,
                                originalSeasonName,
                                expectedYear,
                                expectedEpisodes,
                                null,
                                _logger,
                                new[] { series?.OriginalTitle },
                                new[] { season.OriginalTitle },
                                season).ConfigureAwait(false);

                            if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(
                                    season.Id.ToString(), automaticGeneration))
                            {
                                _logger.LogInformation(
                                    "[SmartMatch] Automatic Season search was superseded: season={0}",
                                    season.Name);
                                continue;
                            }

                            if (!CanUseAutomaticSearch(search))
                            {
                                LogIncompleteAutomaticSearch(originalSeasonName, "season", search);
                                continue;
                            }

                            selectedCandidate = DanmuMatchScorer.SelectAutoCandidate(search.CanonicalCandidates);
                            if (selectedCandidate == null)
                            {
                                var top = search.Candidates.FirstOrDefault();
                                _logger.LogInformation(
                                    "[智能匹配] 新季入库未达到自动选择条件：series={0}, season={1}, candidates={2}, top={3}, score={4}",
                                    series?.Name,
                                    originalSeasonName,
                                    search.Candidates.Count,
                                    top?.Name,
                                    top?.Score);
                                continue;
                            }

                            selectedScraper = scrapers.FirstOrDefault(x =>
                                string.Equals(x.ProviderId, selectedCandidate.Site, StringComparison.OrdinalIgnoreCase));
                            selectedMediaId = selectedCandidate.Id;
                        }

                        if (selectedScraper == null || string.IsNullOrWhiteSpace(selectedMediaId))
                        {
                            _logger.LogInformation("[智能匹配] 找不到候选对应的弹幕来源：season={0}", originalSeasonName);
                            continue;
                        }

                        var media = await selectedScraper.GetMedia(season, selectedMediaId);
                        if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(
                                season.Id.ToString(), automaticGeneration))
                        {
                            _logger.LogInformation(
                                "[SmartMatch] Automatic Season media lookup was superseded: season={0}",
                                season.Name);
                            continue;
                        }
                        if (media == null)
                        {
                            _logger.LogInformation(
                                "[{0}]智能匹配成功，但获取不到视频信息. id: {1}",
                                selectedScraper.Name,
                                selectedMediaId);
                            continue;
                        }

                        if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(
                                season.Id.ToString(), automaticGeneration))
                            continue;
                        var seasonProviderGeneration = BeginProviderWrite(season, selectedScraper.ProviderId);
                        if (!await DownloadAutomaticSeasonForImport(
                                season,
                                media,
                                selectedScraper,
                                selectedMediaId,
                                selectedCandidate,
                                seasonProviderGeneration,
                                automaticGeneration).ConfigureAwait(false))
                        {
                            _logger.LogInformation(
                                "[{0}] 匹配到季度但没有成功写入有效弹幕文件，不保存 ProviderId: {1}",
                                selectedScraper.Name,
                                originalSeasonName);
                            continue;
                        }

                        _logger.LogInformation(
                            "[{0}]全站智能匹配成功：series={1}, season={2}, ProviderId={3}, title={4}, score={5}",
                            selectedScraper.Name,
                            series?.Name,
                            originalSeasonName,
                            selectedMediaId,
                            selectedCandidate?.Name ?? "已手动绑定",
                            selectedCandidate?.Score ?? 1);
                    }
                    catch (FrequentlyRequestException ex)
                    {
                        _logger.LogError(ex, "api接口触发风控，中止执行，请稍候再试.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception handled processing season events");
                    }
                    finally
                    {
                        season.Name = originalSeasonName;
                    }
                }

                // 保存元数据
                await ProcessQueuedUpdateMeta(queueUpdateMeta).ConfigureAwait(false);
            }

            // r5 seals the legacy ProviderId-driven Update path below. Add and Update
            // events both run the fresh identifier-free target planner above, and this
            // mutually-exclusive branch is retained only as inert compatibility source
            // until the next cleanup release.
        }


        private bool TryBuildAutomaticPlanningContext(
            Season season, out SeasonPlanningContext context, out string error)
        {
            return SeasonTargetPlanningCoordinator.TryBuild(season, out context, out error);
        }

        private async Task<bool> DownloadAutomaticSeasonForImport(
            Season season,
            ScraperMedia media,
            AbstractScraper scraper,
            string seasonProviderValue,
            DanmuMatchCandidate selectedCandidate,
            long seasonProviderGeneration,
            long automaticGeneration)
        {
            return await DownloadAutomaticSeasonWithCompositePlan(
                season, media, scraper, seasonProviderValue, selectedCandidate,
                seasonProviderGeneration, automaticGeneration).ConfigureAwait(false);

        }

        private async Task<bool> DownloadAutomaticSeasonWithCompositePlan(
            Season season, ScraperMedia primaryMedia, AbstractScraper primaryScraper,
            string primaryLookupId, DanmuMatchCandidate primaryCandidate, long primaryGeneration,
            long automaticGeneration)
        {
            if (season?.IndexNumber.GetValueOrDefault() <= 0) return false;
            var seasonId = season.Id.ToString();
            if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration))
                return false;
            if (!TryBuildAutomaticPlanningContext(season, out var context, out var ownershipError))
            {
                _logger.LogInformation("[CompositeSeason] Automatic ownership rejected: season={0}, error={1}",
                    season.Name, ownershipError);
                return false;
            }
            var episodes = context.Episodes;
            if (!CompositeSeasonPlanner.TryCreatePlan(context.LocalEpisodes,
                    Enumerable.Empty<CompositeSeasonEpisodeMapping>(),
                    null, null, false, out var plan, out var error))
            {
                _logger.Error("[CompositeSeason] Auto plan rejected: season={0}, error={1}", season.Name, error);
                return false;
            }

            var primarySource = CompositeSeasonMatchService.GetSource(
                primaryScraper.ProviderId, primaryMedia, primaryLookupId);
            var primaryEpisodes = CompositeSeasonMatchService.GetSourceEpisodes(primaryMedia);
            var automaticSelection = CreateAutomaticSelection(plan, primaryScraper.ProviderId,
                primaryLookupId, primaryEpisodes, "automatic-primary", primaryCandidate);
            var automaticRequest = CreateAutomaticSegmentRequest(
                automaticSelection, primarySource, primaryEpisodes,
                SourceMetadata.MergeDetailWithSnapshot(
                    CompositeSeasonMatchService.GetSourceMetadata(primaryMedia),
                    primaryCandidate?.SourceMetadata),
                primaryCandidate?.MatchScore ?? 0,
                primaryCandidate?.ScoreOrigin ?? string.Empty);
            if (!CompositeSeasonPlanner.TryApplySegmentResolved(plan, automaticRequest,
                    out plan, out var primaryResolution, out error))
            {
                _logger.Error("[CompositeSeason] Automatic primary continuation rejected: season={0}, error={1}",
                    season.Name, error);
                return false;
            }
            automaticSelection.ServerResolvedAlignmentMode = primaryResolution.Mode;
            automaticSelection.ServerSourceEpisodes = CloneSourceEpisodes(primaryEpisodes);
            automaticSelection.ServerConsideredLocalEpisodeItemIds = primaryResolution.ConsideredLocalEpisodes
                .Select(item => item.ItemId ?? string.Empty).ToList();
            var automaticSelections = new List<DanmuCompositeSeasonSelection> { automaticSelection };

            // Automatic matching is fail-closed until every local Episode has
            // an authoritative source mapping.
            if (plan.Mappings.Count == 0 || plan.UnmatchedRuns.Count > 0) return false;
            var planFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, automaticSelections, plan);
            if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration))
                return false;
            var preflight = await RebuildAutomaticPlanAsync(season, automaticSelections).ConfigureAwait(false);
            if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration) ||
                preflight.Plan == null || !string.Equals(
                    planFingerprint, preflight.PlanFingerprint, StringComparison.Ordinal))
            {
                _logger.LogInformation("[CompositeSeason] Automatic plan became stale before download: season={0}",
                    season.Name);
                return false;
            }
            var byId = episodes.ToDictionary(x => x.Id.ToString(), StringComparer.OrdinalIgnoreCase);
            var sources = plan.Mappings.Select(x => x.Source).Distinct().ToList();
            var canSaveSeason = plan.CanPersistCompleteSeasonBinding;
            var persisted = false;
            var acceptedCount = 0;
            var anyFailed = false;
            var lease = BeginCompositeSeasonWrite(season, plan.CompositeSafetyRequired);
            try
            {
                foreach (var mapping in plan.Mappings)
                {
                    if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration))
                    {
                        anyFailed = true;
                        break;
                    }
                    if (!byId.TryGetValue(mapping.LocalEpisodeItemId, out var episode))
                    {
                        anyFailed = true;
                        continue;
                    }
                    try
                    {
                        var sourceScraper = _scraperManager.All().FirstOrDefault(x => string.Equals(
                            x.ProviderId, mapping.Source.ProviderId, StringComparison.OrdinalIgnoreCase));
                        if (sourceScraper == null)
                        {
                            anyFailed = true;
                            continue;
                        }
                        var lookup = !string.IsNullOrWhiteSpace(mapping.Source.MediaLookupId)
                            ? mapping.Source.MediaLookupId : mapping.Source.MediaId;
                        var media = string.Equals(mapping.Origin, "episode-provider-id", StringComparison.OrdinalIgnoreCase)
                            ? await DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(sourceScraper, episode, lookup, 1).ConfigureAwait(false)
                            : await sourceScraper.GetMedia(season, lookup).ConfigureAwait(false);
                        var sourceEpisode = (media?.Episodes ?? new List<ScraperEpisode>()).FirstOrDefault(x => x != null &&
                            string.Equals(x.Id, mapping.SourceEpisodeId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.CommentId));
                        if (sourceEpisode == null || string.IsNullOrWhiteSpace(mapping.CommentId) ||
                            !string.Equals(sourceEpisode.CommentId, mapping.CommentId, StringComparison.Ordinal))
                        {
                            anyFailed = true;
                            continue;
                        }
                        var exact = new ScraperMedia { Id = media.Id, ProviderId = sourceScraper.ProviderId,
                            Episodes = new List<ScraperEpisode> { new ScraperEpisode { Id = sourceEpisode.Id,
                                CommentId = sourceEpisode.CommentId, EpisodeNumber = 1, Title = sourceEpisode.Title } } };
                        if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration))
                        {
                            anyFailed = true;
                            break;
                        }
                        var outcome = await DownloadEpisodeForProgress(episode, exact, sourceScraper, false, 1).ConfigureAwait(false);
                        if (!SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration))
                        {
                            anyFailed = true;
                            break;
                        }
                        if (!outcome.FilePersisted)
                        {
                            anyFailed = true;
                            continue;
                        }
                        persisted = true;
                        acceptedCount++;
                        await PersistDownloadProviderIdAsync(episode, outcome).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        anyFailed = true;
                        _logger.LogError(ex, "[CompositeSeason] Auto exact download failed: {0}", episode.Name);
                    }
                }
            }
            finally { CompleteCompositeSeasonWrite(lease); }
            var generationIsCurrent = SeasonPlanGenerationCoordinator.Shared.IsCurrent(
                seasonId, automaticGeneration);
            var terminalPlan = generationIsCurrent
                ? await RebuildAutomaticPlanAsync(season, automaticSelections).ConfigureAwait(false)
                : new AutomaticSeasonPlanSnapshot();
            generationIsCurrent = SeasonPlanGenerationCoordinator.Shared.IsCurrent(
                seasonId, automaticGeneration);
            var staleStructure = !generationIsCurrent || terminalPlan.Plan == null || !string.Equals(
                planFingerprint, terminalPlan.PlanFingerprint, StringComparison.Ordinal);
            if (persisted && canSaveSeason && sources.Count == 1)
            {
                var terminal = new SeasonDisplayMirrorCommit
                {
                    SeasonId = seasonId, Generation = automaticGeneration,
                    ProviderId = sources[0].ProviderId, CanonicalMediaId = sources[0].MediaId,
                    EligibleEpisodeCount = context.LocalEpisodes.Count,
                    MappedEpisodeCount = plan.Mappings.Count,
                    TerminalEpisodeCount = acceptedCount,
                    AcceptedEpisodeCount = acceptedCount,
                    StableSourceCount = 1,
                    HasUnmatchedEpisodes = plan.UnmatchedRuns.Count > 0,
                    Failed = anyFailed,
                    StaleStructure = staleStructure,
                    HasCanonicalSeasonIdentity = !string.IsNullOrWhiteSpace(sources[0].MediaId) &&
                        !sources[0].MediaId.StartsWith("direct-episode-provider:", StringComparison.OrdinalIgnoreCase),
                };
                if (SeasonDisplayMirrorPolicy.CanCommit(terminal, out _) &&
                    SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration))
                {
                    await UpsertSeasonDisplayMirrorAsync(season, sources[0].ProviderId,
                        sources[0].MediaId, primaryGeneration).ConfigureAwait(false);
                }
            }
            return persisted && !anyFailed && !staleStructure &&
                   SeasonPlanGenerationCoordinator.Shared.IsCurrent(seasonId, automaticGeneration) &&
                   acceptedCount == plan.Mappings.Count;
        }

        private sealed class AutomaticSeasonPlanSnapshot
        {
            public SeasonPlanningContext Context { get; set; }
            public CompositeSeasonPlan Plan { get; set; }
            public string PlanFingerprint { get; set; } = string.Empty;
        }

        private static DanmuCompositeSeasonSelection CreateAutomaticSelection(
            CompositeSeasonPlan plan, string site, string candidateId,
            IReadOnlyList<CompositeSeasonSourceEpisode> sourceEpisodes,
            string origin, DanmuMatchCandidate candidate)
        {
            return new DanmuCompositeSeasonSelection
            {
                MappingProtocolVersion = DanmuMappingProtocol.CurrentVersion,
                AlignmentIntent = DanmuCompositeAlignmentIntentWire.DefaultZeroOffset,
                LocalStartEpisodeItemId = plan?.UnmatchedRuns?.FirstOrDefault()?.Episodes?
                    .FirstOrDefault()?.ItemId ?? string.Empty,
                RequestedEpisodeCount = 0,
                Site = site ?? string.Empty,
                CandidateId = candidateId ?? string.Empty,
                SourceStartEpisodeId = sourceEpisodes?.FirstOrDefault()?.EpisodeId ?? string.Empty,
                MatchOrigin = origin ?? string.Empty,
                SelectionEvidenceToken = candidate?.SelectionEvidenceToken ?? string.Empty,
                ServerSourceMetadata = candidate?.SourceMetadata?.Clone(),
                ServerSourceEpisodes = CloneSourceEpisodes(sourceEpisodes),
            };
        }

        private static CompositeSeasonSegmentRequest CreateAutomaticSegmentRequest(
            DanmuCompositeSeasonSelection selection,
            CompositeSeasonSourceIdentity source,
            IReadOnlyList<CompositeSeasonSourceEpisode> sourceEpisodes,
            SourceMetadata sourceMetadata,
            double matchScore,
            string scoreOrigin)
        {
            return new CompositeSeasonSegmentRequest
            {
                LocalStartEpisodeItemId = selection.LocalStartEpisodeItemId,
                RequestedEpisodeCount = selection.RequestedEpisodeCount,
                Source = source,
                SourceEpisodes = CloneSourceEpisodes(sourceEpisodes),
                SourceStartEpisodeId = selection.SourceStartEpisodeId,
                SourceStartEpisodeNumber = selection.SourceStartEpisodeNumber,
                AlignmentIntent = CompositeSeasonAlignmentIntent.DefaultZeroOffset,
                Origin = selection.MatchOrigin,
                MatchScore = matchScore,
                ScoreOrigin = scoreOrigin ?? string.Empty,
                SelectionEvidenceToken = selection.SelectionEvidenceToken,
                SourceMetadata = sourceMetadata?.Clone(),
            };
        }

        private static List<CompositeSeasonSourceEpisode> CloneSourceEpisodes(
            IEnumerable<CompositeSeasonSourceEpisode> episodes)
        {
            return (episodes ?? Enumerable.Empty<CompositeSeasonSourceEpisode>())
                .Select(episode => new CompositeSeasonSourceEpisode
                {
                    EpisodeId = episode?.EpisodeId ?? string.Empty,
                    CommentId = episode?.CommentId ?? string.Empty,
                    EpisodeNumber = episode?.EpisodeNumber,
                    SourceOrdinal = episode?.SourceOrdinal ?? 0,
                }).ToList();
        }

        private async Task<AutomaticSeasonPlanSnapshot> RebuildAutomaticPlanAsync(
            Season season, IEnumerable<DanmuCompositeSeasonSelection> selections)
        {
            var snapshot = new AutomaticSeasonPlanSnapshot();
            if (season?.IndexNumber.GetValueOrDefault() <= 0) return snapshot;
            if (!TryBuildAutomaticPlanningContext(season, out var context, out _)) return snapshot;
            snapshot.Context = context;
            if (!CompositeSeasonPlanner.TryCreatePlan(context.LocalEpisodes, null,
                    out var plan, out _)) return snapshot;
            foreach (var selection in selections ?? Enumerable.Empty<DanmuCompositeSeasonSelection>())
            {
                if (selection == null || !DanmuMappingProtocol.IsCurrent(selection.MappingProtocolVersion) ||
                    !DanmuCompositeAlignmentIntentWire.TryParse(selection.AlignmentIntent, out var alignmentIntent) ||
                    !DanmuMappingProtocol.IsAllowedBatchOrigin(selection.MatchOrigin)) return snapshot;
                var scraper = _scraperManager.All().FirstOrDefault(candidate => string.Equals(
                    candidate.ProviderId, selection.Site, StringComparison.OrdinalIgnoreCase));
                if (scraper == null) return snapshot;
                ScraperMedia media;
                try { media = await scraper.GetMedia(season, selection.CandidateId).ConfigureAwait(false); }
                catch { return snapshot; }
                var source = CompositeSeasonMatchService.GetSource(
                    scraper.ProviderId, media, selection.CandidateId);
                var sourceEpisodes = CompositeSeasonMatchService.GetSourceEpisodes(media);
                var request = new CompositeSeasonSegmentRequest
                {
                    LocalStartEpisodeItemId = selection.LocalStartEpisodeItemId,
                    RequestedEpisodeCount = selection.RequestedEpisodeCount,
                    Source = source,
                    SourceEpisodes = sourceEpisodes,
                    SourceStartEpisodeId = selection.SourceStartEpisodeId,
                    SourceStartEpisodeNumber = selection.SourceStartEpisodeNumber,
                    AlignmentIntent = alignmentIntent,
                    Origin = selection.MatchOrigin,
                    SelectionEvidenceToken = selection.SelectionEvidenceToken,
                    SourceMetadata = SourceMetadata.MergeDetailWithSnapshot(
                        CompositeSeasonMatchService.GetSourceMetadata(media),
                        selection.ServerSourceMetadata),
                };
                if (!CompositeSeasonPlanner.TryApplySegmentResolved(
                        plan, request, out plan, out var resolution, out _)) return snapshot;
                selection.ServerResolvedAlignmentMode = resolution.Mode;
                selection.ServerSourceEpisodes = CloneSourceEpisodes(sourceEpisodes);
                selection.ServerConsideredLocalEpisodeItemIds = resolution.ConsideredLocalEpisodes
                    .Select(item => item.ItemId ?? string.Empty).ToList();
            }
            snapshot.Plan = plan;
            snapshot.PlanFingerprint = SeasonPlanningContextBuilder.CreatePlanFingerprint(
                context, selections, plan);
            return snapshot;
        }

        internal static bool CanUseAutomaticSearch(DanmuMatchSearchResult search)
        {
            return search != null && !search.WasCancelled && search.HasCompletedProviders;
        }

        private void LogIncompleteAutomaticSearch(
            string seasonName,
            string searchScope,
            DanmuMatchSearchResult search)
        {
            if (search?.WasCancelled == true)
            {
                _logger.LogInformation(
                    "[SmartMatch] Automatic {0} search was cancelled; no binding or download will run. season={1}",
                    searchScope,
                    seasonName);
                return;
            }
            var diagnostics = search?.CompletionDiagnostics ?? new List<DanmuSearchCompletionDiagnostic>();
            var summary = string.Join(", ", diagnostics
                .Where(item => item != null &&
                    !string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase))
                .Select(item => (item.Provider ?? string.Empty) + ":" + (item.Status ?? string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            _logger.LogInformation(
                "[SmartMatch] Automatic {0} search has no completed-provider coverage; no binding or download will run. season={1}, diagnostics={2}",
                searchScope,
                seasonName,
                summary);
        }

        /// <summary>
        /// Processes queued episode events.
        /// </summary>
        /// <param name="events">The <see cref="LibraryEvent"/> enumerable.</param>
        /// <param name="eventType">The <see cref="EventType"/>.</param>
        /// <returns>Task.</returns>
        public async Task ProcessQueuedEpisodeEvents(IReadOnlyCollection<LibraryEvent> events, EventType eventType)
        {
            if (events.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Processing {Count} episodes with event type {EventType}", events.Count, eventType);

            var episodes = new HashSet<Episode>(events
                .Select(lev => lev.Item as Episode) // 显式进行类型转换
                .Where(lev => lev != null && !string.IsNullOrEmpty(lev.Name))); // 确保movie 不是 null 之后再检查 Name 属性

            // 判断epid，有的话刷新弹幕文件
            if (eventType == EventType.Update)
            {
                foreach (var item in episodes)
                {
                    foreach (var scraper in _scraperManager.All())
                    {
                        try
                        {
                            var providerVal = item.GetProviderId(scraper.ProviderId);
                            if (string.IsNullOrEmpty(providerVal))
                            {
                                providerVal = await GetEpisodeDanmuIdBySeason(item.Season, item, scraper).ConfigureAwait(false);
                                if (string.IsNullOrEmpty(providerVal))
                                {
                                    continue;
                                }
                            }

                            var episode = await scraper.GetMediaEpisode(item, providerVal);
                            if (episode != null)
                            {
                                // 下载弹幕xml文件
                                var outcome = await DownloadDanmuForPersistence(scraper, item, episode.CommentId).ConfigureAwait(false);
                                if (outcome.FilePersisted && !string.IsNullOrWhiteSpace(episode.Id))
                                {
                                    await PersistDownloadProviderIdAsync(item, outcome, episode.Id).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (DanmuDownloadErrorException ex)
                        {
                            _logger.LogError(ex, "[{0}]弹幕下载失败，尝试匹配下一个. 失败原因={1}", scraper.Name, ex.Message);
                        }
                        catch (FrequentlyRequestException ex)
                        {
                            _logger.LogError(ex, "api接口触发风控，中止执行，请稍候再试.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception handled processing queued movie events");
                        }
                    }
                }
            }


            // 强制刷新指定来源弹幕（手动搜索强刷忽略集数不一致处理）
            if (eventType == EventType.Force)
            {
                foreach (var queueItem in episodes)
                {
                    // 找到选择的scraper
                    var scraper = _scraperManager.All()
                        .FirstOrDefault(x => queueItem.ProviderIds.ContainsKey(x.ProviderId));
                    if (scraper == null)
                    {
                        continue;
                    }

                    // 获取选择的弹幕Id
                    var mediaId = queueItem.GetProviderId(scraper.ProviderId);
                    if (string.IsNullOrEmpty(mediaId))
                    {
                        continue;
                    }

                    // 获取最新的item数据
                    var item = _libraryManager.GetItemById(queueItem.Id);
                    var season = ((Episode)item).Season;
                    if (season == null)
                    {
                        continue;
                    }

                    var media = await scraper.GetMedia(season, mediaId);
                    // _logger.LogInformation("查询弹幕信息 media= " + media.ToJson());
                    if (media != null)
                    {
                        // 下载一集弹幕
                        var seasonProviderGeneration = BeginProviderWrite(season, scraper.ProviderId);
                        var filePersisted = await downloadOneEpisode((Episode)item, media, scraper).ConfigureAwait(false);
                        if (filePersisted && !string.IsNullOrWhiteSpace(media.Id))
                        {
                            await SaveProviderIdForGeneration(
                                season, scraper.ProviderId, media.Id, false, seasonProviderGeneration).ConfigureAwait(false);
                        }
                        // // 更新所有剧集元数据，GetEpisodes一定要取所有fields，要不然更新会导致重建虚拟season季信息
                        // var episodeItemResult = season.GetEpisodes();
                        // var episodeList = episodeItemResult.Items;
                        // for (var idx = 0; idx < episodeList.Count(); idx++)
                        // {
                        //     var episode = episodeList[idx];
                        //     var fileName = Path.GetFileName(episode.Path);
                        //
                        //     // 没对应剧集号的，忽略处理
                        //     var indexNumber = episode.IndexNumber ?? 0;
                        //     if (indexNumber < 1 || indexNumber > media.Episodes.Count)
                        //     {
                        //         _logger.LogInformation("[{0}]缺少集号或集号超过弹幕数，忽略处理. [{1}]{2}, indexNumber={3}, mediaCount={4}", scraper.Name, season.Name, fileName, indexNumber, media.Episodes.Count);
                        //         continue;
                        //     }
                        //
                        //     // 特典或extras影片不处理（动画经常会放在季文件夹下）
                        //     if (episode.ParentIndexNumber == null || episode.ParentIndexNumber == 0)
                        //     {
                        //         _logger.LogInformation("[{0}]缺少季号，可能是特典或extras影片，忽略处理. [{1}]{2}", scraper.Name,
                        //             season.Name, fileName);
                        //         continue;
                        //     }
                        //
                        //     var epId = media.Episodes[indexNumber - 1].Id;
                        //     var commentId = media.Episodes[indexNumber - 1].CommentId;
                        //
                        //     // 下载弹幕xml文件
                        //     await this.DownloadDanmu(scraper, episode, commentId, true).ConfigureAwait(false);
                        //
                        //     // 更新剧集元数据
                        //     await ForceSaveProviderId(episode, scraper.ProviderId, epId);
                        // }
                    }
                }
            }
        }

        private async Task<bool> downloadOneEpisode(Episode episode, ScraperMedia media, AbstractScraper scraper)
        {
            var fileName = Path.GetFileName(episode.Path);
            var indexNumber = episode.IndexNumber ?? 0;
            if (indexNumber < 1 || indexNumber > media.Episodes.Count)
            {
                _logger.LogInformation("[{0}]缺少集号或集号超过弹幕数，忽略处理. [{1}]{2}, indexNumber={3}, mediaCount={4}", scraper.Name, episode.Name, fileName, indexNumber, media.Episodes.Count);
                return false;
            }
            // 特典或extras影片不处理（动画经常会放在季文件夹下）
            if (episode.ParentIndexNumber == null || episode.ParentIndexNumber == 0)
            {
                _logger.LogInformation("[{0}]缺少季号，可能是特典或extras影片，忽略处理. [{1}]{2}", scraper.Name, episode.Name, fileName);
                return false;
            }

            var epId = media.Episodes[indexNumber - 1].Id;
            var commentId = media.Episodes[indexNumber - 1].CommentId;

            // 下载弹幕xml文件
            var outcome = await DownloadDanmuForPersistence(scraper, episode, commentId, true).ConfigureAwait(false);

            // 更新剧集元数据
            if (outcome.FilePersisted && !string.IsNullOrWhiteSpace(epId))
            {
                await PersistDownloadProviderIdAsync(episode, outcome, epId).ConfigureAwait(false);
            }
            return outcome.FilePersisted;
        }

        /// <summary>
        /// 为前端进度任务下载单集。与旧队列不同，本方法把无法下载、内容为空等情况作为异常返回，
        /// 这样调用方可以准确记录每一集的成功或失败结果。
        /// </summary>
        public async Task<DanmuEpisodeDownloadOutcome> DownloadEpisodeForProgress(
            Episode episode,
            ScraperMedia media,
            AbstractScraper scraper,
            bool forceRefresh,
            int? sourceEpisodeNumber = null)
        {
            if (episode == null || media == null || scraper == null)
            {
                throw new ArgumentException("剧集、媒体信息或弹幕来源无效");
            }

            var indexNumber = sourceEpisodeNumber ?? episode.IndexNumber ?? 0;
            if (!DanmuEpisodeMatchHelper.TryGetSourceEpisode(
                    media.Episodes, indexNumber, out var mediaEpisode))
            {
                throw new DanmuDownloadErrorException(
                    $"缺少集号或集号超过弹幕数（本地第 {indexNumber} 集，来源共 {media.Episodes.Count} 集）");
            }

            if (episode.ParentIndexNumber == null || episode.ParentIndexNumber == 0)
            {
                throw new DanmuDownloadErrorException("缺少季号，可能是特典或 extras 影片");
            }

            if (string.IsNullOrWhiteSpace(mediaEpisode.CommentId))
            {
                throw new DanmuDownloadErrorException("弹幕来源没有返回该集的弹幕 ID");
            }

            return await DownloadItemForProgress(
                episode,
                mediaEpisode,
                scraper,
                forceRefresh).ConfigureAwait(false);
        }

        public async Task<DanmuEpisodeDownloadOutcome> DownloadMovieForProgress(
            Movie movie,
            ScraperMedia media,
            AbstractScraper scraper,
            bool forceRefresh)
        {
            if (movie == null || media == null || scraper == null)
            {
                throw new ArgumentException("电影、媒体信息或弹幕来源无效");
            }

            var lookupId = DanmuMovieMatchHelper.ResolveEpisodeLookupId(scraper.ProviderId, media);

            ScraperEpisode mediaEpisode = null;
            Exception lookupFailure = null;
            if (!string.IsNullOrWhiteSpace(lookupId))
            {
                try
                {
                    mediaEpisode = await scraper.GetMediaEpisode(movie, lookupId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lookupFailure = ex;
                    if (string.IsNullOrWhiteSpace(media.SelectedMoviePartId) &&
                        !string.IsNullOrWhiteSpace(media.CommentId))
                    {
                        _logger.Warn(
                            $"[{scraper.Name}] 电影 '{movie.Name}' 查询播放条目失败，改用搜索结果中的弹幕 ID：{ex.Message}");
                    }
                }
            }
            mediaEpisode = DanmuMovieMatchHelper.ResolveEpisodeForDownload(
                media, mediaEpisode, lookupId, lookupFailure);
            if (string.IsNullOrWhiteSpace(mediaEpisode.Title)) mediaEpisode.Title = movie.Name ?? string.Empty;

            return await DownloadItemForProgress(
                movie,
                mediaEpisode,
                scraper,
                forceRefresh,
                true).ConfigureAwait(false);
        }

        private async Task<DanmuEpisodeDownloadOutcome> DownloadItemForProgress(
            BaseItem item,
            ScraperEpisode mediaEpisode,
            AbstractScraper scraper,
            bool forceRefresh,
            bool saveItemProviderId = true)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FileNameWithoutExtension) ||
                string.IsNullOrWhiteSpace(item.ContainingFolderPath))
            {
                throw new DanmuDownloadErrorException("媒体项缺少可写入的文件路径");
            }

            var providerWriteGeneration = BeginProviderWrite(item, scraper.ProviderId);
            var danmuPath = Path.Combine(
                item.ContainingFolderPath,
                item.GetDanmuXmlPath(scraper.ProviderId));
            var fileExists = _fileSystem.Exists(danmuPath);
            var lastWriteTime = fileExists ? _fileSystem.GetLastWriteTime(danmuPath) : DateTime.MinValue;
            if (DanmuDownloadPolicy.ShouldSkipExistingDanmu(
                    forceRefresh, fileExists, lastWriteTime, DateTime.Now))
            {
                    _logger.LogInformation(
                        "[{0}]弹幕文件在7天内更新过，重复已跳过：{1}.{2}，文件={3}",
                        scraper.Name,
                        item.IndexNumber,
                        item.Name,
                        danmuPath);
                    return new DanmuEpisodeDownloadOutcome
                    {
                        Status = "skipped",
                        Message = "重复已跳过",
                        SkipReason = SevenDayReplayPolicy.RecentFileSkipReason,
                        ProviderId = saveItemProviderId ? scraper.ProviderId : string.Empty,
                        ProviderWriteGeneration = providerWriteGeneration,
                    };
            }

            var danmaku = await scraper.GetDanmuContent(item, mediaEpisode.CommentId).ConfigureAwait(false);
            if (danmaku == null)
            {
                throw new DanmuDownloadErrorException("弹幕来源返回空内容");
            }

            var isPartial = danmaku.SegmentFailed > 0 && danmaku.SegmentFailed < danmaku.SegmentTotal;
            if (danmaku.SegmentTotal > 0 && danmaku.SegmentFailed >= danmaku.SegmentTotal)
            {
                throw new DanmuDownloadErrorException(
                    $"全部 {danmaku.SegmentTotal} 个弹幕分段下载失败");
            }

            // 部分分段失败时，仍保存其他已下载分段组成的合法 XML；不再以文件大小判断内容是否有效。
            var bytes = DanmuDownloadContent.Serialize(danmaku);

            await SaveDanmu(scraper, item, bytes).ConfigureAwait(false);
            if (isPartial)
            {
                return new DanmuEpisodeDownloadOutcome
                {
                    Status = "partial",
                    Message = $"部分弹幕缺失（{danmaku.SegmentFailed}/{danmaku.SegmentTotal} 个分段）",
                    SegmentTotal = danmaku.SegmentTotal,
                    SegmentFailed = danmaku.SegmentFailed,
                    ProviderId = saveItemProviderId ? scraper.ProviderId : string.Empty,
                    ProviderValue = saveItemProviderId ? mediaEpisode.Id ?? string.Empty : string.Empty,
                    FilePersisted = true,
                    ProviderWriteGeneration = providerWriteGeneration,
                };
            }

            return new DanmuEpisodeDownloadOutcome
            {
                Status = "success",
                Message = "下载成功",
                SegmentTotal = danmaku.SegmentTotal,
                ProviderId = saveItemProviderId ? scraper.ProviderId : string.Empty,
                ProviderValue = saveItemProviderId ? mediaEpisode.Id ?? string.Empty : string.Empty,
                FilePersisted = true,
                ProviderWriteGeneration = providerWriteGeneration,
            };
        }


        // 调用UpdateToRepositoryAsync后，但未完成时，会导致GetEpisodes返回缺少正在处理的集数，所以采用统一最后处理
        private Task ProcessQueuedUpdateMeta(List<BaseItem> queue)
        {
            if (queue == null || queue.Count <= 0)
            {
                return Task.CompletedTask;
            }

            foreach (var queueItem in queue)
            {
                // 获取最新的item数据
                var queueItemId = queueItem.Id;
                if (Guid.Empty.Equals(queueItemId) && queueItem is Season)
                {
                    queueItemId = queueItem.GetParent().Id;
                    _logger.LogInformation("当前是Season={0}, 并且不存在相应的id，使用Series信息={1}", queueItem.Name, queueItemId);
                }
                
                var item = _libraryManager.GetItemById(queueItemId);
                if (item != null)
                {
                    // 合并新添加的provider id
                    foreach (var pair in queueItem.ProviderIds)
                    {
                        if (string.IsNullOrEmpty(pair.Value))
                        {
                            continue;
                        }

                        item.ProviderIds[pair.Key] = pair.Value;
                    }

                    item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                    // Console.WriteLine(JsonSerializer.Serialize(item));
                    // await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
                }
            }

            _logger.LogInformation("更新epid到元数据完成。item数：{0}", queue.Count);
            return Task.CompletedTask;
        }

        public async Task DownloadDanmu(AbstractScraper scraper, BaseItem item, string commentId,
            bool ignoreCheck = false)
        {
            await DownloadDanmuForPersistence(scraper, item, commentId, ignoreCheck).ConfigureAwait(false);
        }

        private async Task<DanmuEpisodeDownloadOutcome> DownloadDanmuForPersistence(AbstractScraper scraper, BaseItem item, string commentId,
            bool ignoreCheck = false)
        {
            var providerWriteGeneration = BeginProviderWrite(item, scraper?.ProviderId);
            // 下载弹幕xml文件
            var checkDownloadedKey = $"{item.Id}_{commentId}";
            try
            {
                // 弹幕7天内更新过，忽略处理（有时Update事件会重复执行）
                if (!SingletonManager.IsDebug && !ignoreCheck && _memoryCache.TryGetValue(checkDownloadedKey, out var latestDownloaded))
                {
                    _logger.LogInformation("[{0}]最近7天已更新过弹幕xml，忽略处理：{1}.{2}", scraper.Name, item.IndexNumber,
                        item.Name);
                    return new DanmuEpisodeDownloadOutcome
                    {
                        Status = "skipped",
                        ProviderId = scraper.ProviderId,
                        ProviderWriteGeneration = providerWriteGeneration,
                    };
                }

                _memoryCache.Set(checkDownloadedKey, true, _danmuUpdatedExpiredOption);
                var danmaku = await scraper.GetDanmuContent(item, commentId);
                if (danmaku != null)
                {
                    var bytes = DanmuDownloadContent.Serialize(danmaku);

                    await this.SaveDanmu(scraper, item, bytes);
                    this._logger.LogInformation("[{0}]弹幕下载成功：name={1}.{2} commentId={3}", scraper.Name,
                        item.IndexNumber ?? 1, item.Name, commentId);
                    return new DanmuEpisodeDownloadOutcome
                    {
                        Status = "success",
                        ProviderId = scraper.ProviderId,
                        FilePersisted = true,
                        ProviderWriteGeneration = providerWriteGeneration,
                    };
                }
                else
                {
                    _memoryCache.Remove(checkDownloadedKey);
                    throw new DanmuDownloadErrorException("弹幕来源返回空内容");
                }
            }
            catch (Exception ex)
            {
                if (ex is DanmuDownloadErrorException)
                {
                    throw;
                }
                
                _memoryCache.Remove(checkDownloadedKey);
                _logger.LogError(ex, "[{0}]Exception handled download danmu file. name={1}", scraper.Name, item.Name);
                return new DanmuEpisodeDownloadOutcome
                {
                    Status = "failed",
                    ProviderId = scraper?.ProviderId ?? string.Empty,
                    ProviderWriteGeneration = providerWriteGeneration,
                };
            }
        }

        private bool IsRepeatAction(BaseItem item, string checkDownloadedKey)
        {
            // 单元测试时为null
            if (item.FileNameWithoutExtension == null) return false;

            // 通过xml文件属性判断（多线程时判断有误）
            var danmuPath = Path.Combine(item.ContainingFolderPath, item.FileNameWithoutExtension + ".xml");
            if (!this._fileSystem.Exists(danmuPath))
            {
                return false;
            }

            var lastWriteTime = this._fileSystem.GetLastWriteTime(danmuPath);
            var diff = DateTime.Now - lastWriteTime;
            return diff.TotalSeconds < 3600 * 24 * 7;
        }

        private async Task SaveDanmu(AbstractScraper scraper, BaseItem item, byte[] bytes)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.FileNameWithoutExtension) ||
                string.IsNullOrWhiteSpace(item.ContainingFolderPath))
            {
                throw new DanmuDownloadErrorException("媒体项缺少可写入的文件路径");
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new DanmuDownloadErrorException("弹幕来源没有可持久化的有效内容");
            }

            // Write alongside the final file, then atomically replace it. A
            // forced seven-day replay must never truncate a valid recent XML
            // when a disk or permission failure occurs midway through writing.
            var danmuPath = Path.Combine(item.ContainingFolderPath, item.GetDanmuXmlPath(scraper.ProviderId));
            try
            {
                await WriteDanmuAtomicallyAsync(_fileSystem, danmuPath, bytes).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.ErrorException("文件写入异常 danmuPath={0}", e, danmuPath);
                throw;
            }

            if (this.Config.ToAss && bytes.Length > 0)
            {
                var assConfig = new Danmaku2Ass.Config();
                assConfig.Title = item.Name;
                if (!string.IsNullOrEmpty(this.Config.AssFont.Trim()))
                {
                    assConfig.FontName = this.Config.AssFont;
                }

                if (!string.IsNullOrEmpty(this.Config.AssFontSize.Trim()))
                {
                    assConfig.BaseFontSize = this.Config.AssFontSize.Trim().ToInt();
                }

                if (!string.IsNullOrEmpty(this.Config.AssTextOpacity.Trim()))
                {
                    assConfig.TextOpacity = this.Config.AssTextOpacity.Trim().ToFloat();
                }

                if (!string.IsNullOrEmpty(this.Config.AssLineCount.Trim()))
                {
                    assConfig.LineCount = this.Config.AssLineCount.Trim().ToInt();
                }

                if (!string.IsNullOrEmpty(this.Config.AssSpeed.Trim()))
                {
                    assConfig.TuneDuration = this.Config.AssSpeed.Trim().ToInt() - 8;
                }

                var assPath = Path.Combine(item.ContainingFolderPath, item.FileNameWithoutExtension + ".chs[" + scraper.ProviderId + "_danmu].ass");
                Danmaku2Ass.Bilibili.GetInstance().Create(bytes, assConfig, assPath);
            }
        }

        private static async Task WriteDanmuAtomicallyAsync(IFileSystem fileSystem, string danmuPath, byte[] bytes)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrWhiteSpace(danmuPath)) throw new ArgumentException("A target path is required.", nameof(danmuPath));
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("Content is required.", nameof(bytes));

            var temporaryPath = danmuPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await fileSystem.WriteAllBytesAsync(temporaryPath, bytes, CancellationToken.None).ConfigureAwait(false);
                if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length != bytes.Length)
                {
                    throw new IOException("The temporary danmu file could not be completely verified.");
                }

                if (File.Exists(danmuPath))
                {
                    try
                    {
                        File.Replace(temporaryPath, danmuPath, null);
                    }
                    catch (PlatformNotSupportedException ex)
                    {
                        throw new IOException("The host filesystem does not support atomic danmu replacement.", ex);
                    }
                }
                else
                {
                    File.Move(temporaryPath, danmuPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public async Task SaveProviderId(BaseItem item, string providerId, string providerVal, bool manual)
        {
            var generation = BeginProviderWrite(item, providerId);
            await SaveProviderIdForGeneration(item, providerId, providerVal, manual, generation).ConfigureAwait(false);
        }

        private async Task SaveProviderIdForGeneration(
            BaseItem item,
            string providerId,
            string providerVal,
            bool manual,
            long generation)
        {
            _logger.Info("SaveProviderId item={0}, providerId={1}, providerVal={2}, manual={3}",
                item?.GetParent(), providerId, providerVal, manual);
            if (item == null || string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(providerVal))
            {
                return;
            }

            var updateItem = item;
            // Season 不存在需要更新到 Series上
            if (Guid.Empty.Equals(updateItem.Id))
            {
                _logger.Warn("Skip ProviderId persistence for an item without its own id: {0}", item.Name);
                return;
            }

            // 先清空旧弹幕的所有元数据
            // 保存指定弹幕元数据
            var writeKey = GetProviderWriteKey(item, providerId);
            var writeLock = _providerWriteLocks.GetOrAdd(writeKey, _ => new SemaphoreSlim(1, 1));
            var seasonForWrite = updateItem as Season;
            SemaphoreSlim seasonWriteLock = null;
            if (seasonForWrite != null)
            {
                seasonWriteLock = _seasonProviderWriteLocks.GetOrAdd(
                    seasonForWrite.Id.ToString("N"), _ => new SemaphoreSlim(1, 1));
                await seasonWriteLock.WaitAsync().ConfigureAwait(false);
            }
            await writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_providerWriteTracker.IsStale(writeKey, generation, out var latestGeneration))
                {
                    _logger.Info("Skip stale ProviderId write: item={0}, providerId={1}, generation={2}, committed={3}",
                        item.Name, providerId, generation, latestGeneration);
                    return;
                }

                if (seasonForWrite != null &&
                    CompositeSeasonProviderPolicy.IsPluginSeasonProviderKey(providerId) &&
                    IsSeasonProviderIdWriteBlocked(seasonForWrite))
                {
                    _logger.LogInformation(
                        "[CompositeSeason] Blocked Season ProviderId write: season={0}, provider={1}, generation={2}",
                        seasonForWrite.Name, providerId, generation);
                    _providerWriteTracker.MarkCommitted(writeKey, generation);
                    return;
                }

                var manualKey = providerId + "Manual";
                var registeredScrapers = _scraperManager.AllWithNoEnabled();
                if (updateItem is Season)
                {
                    var parentSeries = updateItem.GetParent() as Series;
                    var authoritativeParentSeries = parentSeries == null
                        ? null
                        : _libraryManager.GetItemById(parentSeries.InternalId) as Series ?? parentSeries;
                    updateItem.ProviderIds = DanmuProviderIdResolver.GetItemLocalProviderIds(
                        updateItem, authoritativeParentSeries, registeredScrapers);
                }

                var nextProviderIds = DanmuProviderIdWritePolicy.BuildSuccessfulWrite(
                    updateItem.ProviderIds,
                    Enumerable.Empty<string>(),
                    providerId,
                    providerVal,
                    false);
                if (manual)
                {
                    nextProviderIds[manualKey] = providerVal;
                }

                if (ProviderIdsEqual(updateItem.ProviderIds, nextProviderIds))
                {
                    _providerWriteTracker.MarkCommitted(writeKey, generation);
                    return;
                }

                updateItem.ProviderIds = nextProviderIds;
                await updateItem.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None)
                    .ConfigureAwait(false);
                _providerWriteTracker.MarkCommitted(writeKey, generation);
            }
            finally
            {
                writeLock.Release();
                seasonWriteLock?.Release();
            }
        }

        /// <summary>
        /// r4 write-only display mirror. This deliberately bypasses the legacy
        /// successful-write cleanup policy: only the verified ordinary target
        /// key is upserted; Manual, other provider, and foreign keys survive.
        /// </summary>
        public async Task UpsertSeasonDisplayMirrorAsync(
            Season season, string providerId, string canonicalMediaId, long generation)
        {
            if (season == null || string.IsNullOrWhiteSpace(providerId) ||
                string.IsNullOrWhiteSpace(canonicalMediaId) || generation <= 0 ||
                !CompositeSeasonProviderPolicy.SeasonProviderIdKeys.Contains(
                    providerId, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("A verified ordinary Season provider identity is required.");
            }

            var writeKey = GetProviderWriteKey(season, providerId);
            var writeLock = _providerWriteLocks.GetOrAdd(writeKey, _ => new SemaphoreSlim(1, 1));
            var seasonLock = _seasonProviderWriteLocks.GetOrAdd(
                season.Id.ToString("N"), _ => new SemaphoreSlim(1, 1));
            await seasonLock.WaitAsync().ConfigureAwait(false);
            await writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_providerWriteTracker.IsStale(writeKey, generation, out _)) return;
                var latest = _libraryManager.GetItemById(season.Id) as Season ?? season;
                var next = new ProviderIdDictionary();
                foreach (var pair in latest.ProviderIds ?? new ProviderIdDictionary()) next[pair.Key] = pair.Value;
                next[providerId] = canonicalMediaId;
                if (!ProviderIdsEqual(latest.ProviderIds, next))
                {
                    latest.ProviderIds = next;
                    await latest.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                _providerWriteTracker.MarkCommitted(writeKey, generation);
            }
            finally
            {
                writeLock.Release();
                seasonLock.Release();
            }
        }

        private static bool ProviderIdsEqual(
            IReadOnlyDictionary<string, string> left,
            IReadOnlyDictionary<string, string> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            return left.All(pair => right.TryGetValue(pair.Key, out var value) &&
                                    string.Equals(pair.Value, value, StringComparison.Ordinal));
        }

        public Task PersistDownloadProviderIdAsync(BaseItem item, DanmuEpisodeDownloadOutcome outcome)
        {
            return PersistDownloadProviderIdAsync(item, outcome, null);
        }

        public Task PersistDownloadProviderIdAsync(
            BaseItem item,
            DanmuEpisodeDownloadOutcome outcome,
            string providerValueOverride)
        {
            if (!DanmuDownloadPersistencePolicy.ShouldPersist(outcome, providerValueOverride))
            {
                return Task.CompletedTask;
            }

            var providerValue = string.IsNullOrWhiteSpace(providerValueOverride)
                ? outcome.ProviderValue
                : providerValueOverride;
            var generation = outcome.ProviderWriteGeneration > 0
                ? outcome.ProviderWriteGeneration
                : BeginProviderWrite(item, outcome.ProviderId);
            return SaveProviderIdForGeneration(item, outcome.ProviderId, providerValue, false, generation);
        }

        public long BeginProviderWrite(BaseItem item, string providerId)
        {
            if (item == null || string.IsNullOrWhiteSpace(providerId))
            {
                return 0;
            }

            var generation = Interlocked.Increment(ref _providerWriteGeneration);
            _providerWriteTracker.MarkStarted(GetProviderWriteKey(item, providerId), generation);
            return generation;
        }

        public Task PersistDownloadProviderIdAsync(
            BaseItem item,
            DanmuEpisodeDownloadOutcome outcome,
            string providerValueOverride,
            long providerWriteGeneration)
        {
            if (!DanmuDownloadPersistencePolicy.ShouldPersist(outcome, providerValueOverride) ||
                providerWriteGeneration <= 0)
            {
                return Task.CompletedTask;
            }

            return SaveProviderIdForGeneration(
                item, outcome.ProviderId, providerValueOverride, false, providerWriteGeneration);
        }

        private static string GetProviderWriteKey(BaseItem item, string providerId)
        {
            return item.Id.ToString("N") + "\u001f" + providerId;
        }

        private Task ForceSaveProviderId(BaseItem item, string providerId, string providerVal)
        {
            return SaveProviderId(item, providerId, providerVal, false);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _queueTimer?.Dispose();
                _cancellationTokenSource.Cancel();
                _forceQueueSignal.Dispose();
                _cancellationTokenSource.Dispose();
            }
        }

        public async Task<string> GetEpisodeDanmuIdBySeason(Season season, Episode episode, AbstractScraper scraper)
        {
            if (season == null || episode == null)
            {
                return null;
            }
            
            var providerVal = season.GetDanmuProviderId(scraper.ProviderId);
            if (string.IsNullOrEmpty(providerVal))
            {
                return providerVal;
            }

            var episodesItem = season.GetEpisodes();
            if (episodesItem == null)
            {
                return null;
            }
            var episodes = episodesItem.Items.ToList();
            if (episodes.Count == 0)
            {
                return null;
            }
            
            string cacheKey = $"{season.GetSeasonId().ToString()}_{scraper.ProviderId}";
            if (!_memoryCache.TryGetValue(cacheKey, out ScraperMedia media))
            {
                media = await scraper.GetMedia(season, providerVal);
                _memoryCache.Set(cacheKey, media);
                if (media == null)
                {
                    _logger.LogInformation("[{0}]获取不到视频信息. ProviderId: {1}", scraper.Name, providerVal);
                    return null;
                }
            }

            // 剧集可能更新中
            if (ignoreEpisodesMatch && media.Episodes.Count != episodes.Count)
            {
                _logger.Info("[{0}]剧集数不匹配. 可能是更新中进行强制更新: {1}, media.Episodes={2}, episodes.Count={3}", scraper.Name, providerVal, media.Episodes.Count, episodes.Count);
            }

            // 获取
            var fileName = Path.GetFileName(episode.Path);
            int episodeIndexNumber = episode.IndexNumber ?? 0;
            if (episodeIndexNumber < 1 || episodeIndexNumber>media.Episodes.Count)
            {
                _logger.LogInformation("[{0}]缺少集号或集号超过弹幕数，忽略处理. [{1}]{2}, indexNumber={3}, mediaCount={4}", scraper.Name, season.Name, fileName, episodeIndexNumber, media.Episodes.Count);
                return null;
            }
            
            // 特典或extras影片不处理（动画经常会放在季文件夹下）
            if (episode.ParentIndexNumber == null || episode.ParentIndexNumber == 0)
            {
                _logger.LogInformation("[{0}]缺少季号，可能是特典或extras影片，忽略处理. [{1}]{2}", scraper.Name,
                    season.Name, fileName);
                return null;
            }
            
            var epId = media.Episodes[episodeIndexNumber - 1].Id;
            return epId;
        }
    }
}
