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

        /// <summary>
        /// Applies the durable composite transition after a mapped Episode file
        /// has been written.  The private marker is persisted before any Emby
        /// ProviderId is cleared, so a crash cannot leave a cleared Season that
        /// later gets silently rebound to one upstream Season.
        /// </summary>
        public async Task<bool> OnCompositeSeasonFilePersistedAsync(
            Season season,
            CompositeSeasonProviderWriteLease lease)
        {
            if (season == null || lease == null || !lease.IsCompositePlan ||
                !string.Equals(season.Id.ToString("N"), lease.SeasonId, StringComparison.Ordinal))
            {
                return false;
            }

            if (!_pendingCompositeSeasonFirstFiles.TryGetValue(lease.LeaseId, out var decision))
            {
                decision = _compositeSeasonWriteCoordinator.OnFilePersisted(lease);
            }

            if (!decision.ShouldPersistTombstoneAndClearProviderIds)
            {
                return false;
            }

            try
            {
                _compositeSeasonStateStore.Value.MarkComposite(
                    lease.SeasonId, GetCompositeSeasonFingerprint(season));
                _pendingCompositeSeasonFirstFiles.TryRemove(lease.LeaseId, out _);
                await ClearCompositeSeasonProviderIdsAsync(season).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                // Retain the decision so a later successful Episode in this task
                // can retry durable persistence without ever clearing first.
                _pendingCompositeSeasonFirstFiles.TryAdd(lease.LeaseId, decision);
                _logger.LogError(ex,
                    "[CompositeSeason] File persisted for Season {0}, but its private marker was not committed; Season ProviderIds were left unchanged.",
                    season.Name);
                return false;
            }
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

        public async Task ClearCompositeSeasonProviderIdsAsync(Season season)
        {
            var seasonId = season.Id.ToString("N");
            var seasonLock = _seasonProviderWriteLocks.GetOrAdd(seasonId, _ => new SemaphoreSlim(1, 1));
            await seasonLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var currentSeason = _libraryManager.GetItemById(season.Id) as Season ?? season;
                var clearPlan = CompositeSeasonProviderPolicy.BuildClearPlan(currentSeason.ProviderIds);
                if (!clearPlan.RequiresClear)
                {
                    return;
                }

                currentSeason.ProviderIds = clearPlan.RemainingProviderIds;
                await currentSeason.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None)
                    .ConfigureAwait(false);
                foreach (var removed in clearPlan.RemovedProviderIds)
                {
                    _logger.LogInformation(
                        "[CompositeSeason] Cleared Season ProviderId after first persisted file: season={0}, key={1}, value={2}",
                        currentSeason.Name, removed.Key, removed.Value);
                }
            }
            finally
            {
                seasonLock.Release();
            }
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
                                    if (!IsCompleteAutomaticSearch(movieSearch))
                                    {
                                        LogIncompleteAutomaticSearch(currentItem.Name, "movie", movieSearch);
                                    }
                                    else
                                    {
                                        selectedMovieCandidate = DanmuMatchScorer.SelectAutoCandidate(movieSearch.Candidates);
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
                var season = item as Season;
                if (item.HasAnyDanmuProviderIds() && (season == null || !IsCompositeSeason(season)))
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
                        foreach (var season in seasons)
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

            if (eventType == EventType.Add)
            {
                var queueUpdateMeta = new List<BaseItem>();
                foreach (var season in seasons)
                {
                    // // 虚拟季第一次请求忽略
                    // if (season.LocationType == LocationType.Virtual && season.IndexNumber is null)
                    // {
                    //     continue;
                    // }

                    if (season.IndexNumber.HasValue && season.IndexNumber == 0)
                    {
                        _logger.LogInformation("special特典文件夹不处理：name={0} number={1}", season.Name, season.IndexNumber);
                        continue;
                    }

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

                    // A composite Season intentionally has no authoritative
                    // ProviderId.  Even if a stale external write is present,
                    // its private tombstone requires a fresh candidate search.
                    var compositeSeason = IsCompositeSeason(currentItem ?? season);
                    if (compositeSeason)
                    {
                        await ClearCompositeSeasonProviderIdsAsync(currentItem ?? season).ConfigureAwait(false);
                    }

                    AbstractScraper selectedScraper = null;
                    string selectedMediaId = null;
                    DanmuMatchCandidate selectedCandidate = null;

                    if (!compositeSeason)
                    {
                        var providerDecision = await DanmuProviderIdResolver.ResolveAsync(
                            scrapers,
                            DanmuProviderIdResolver.GetSeasonScopes(currentItem ?? season),
                            _logger,
                            authoritativeSeries).ConfigureAwait(false);
                        if (providerDecision.Candidate != null)
                        {
                            selectedScraper = providerDecision.Scraper;
                            selectedMediaId = providerDecision.Candidate.Id;
                            selectedCandidate = providerDecision.Candidate;
                        }
                    }

                    // A saved manual binding always wins over automatic scoring when
                    // no configured provider identifier can be resolved.
                    var bindingProviderIds = DanmuProviderIdResolver.GetItemLocalProviderIds(
                        currentItem ?? season, authoritativeSeries, scrapers);
                    foreach (var scraper in compositeSeason
                        ? Enumerable.Empty<AbstractScraper>()
                        : scrapers)
                    {
                        if (selectedScraper == null &&
                            bindingProviderIds != null &&
                            bindingProviderIds.TryGetValue(scraper.ProviderId + "Manual", out var manualId) &&
                            !string.IsNullOrWhiteSpace(manualId))
                        {
                            selectedScraper = scraper;
                            selectedMediaId = manualId;
                            break;
                        }
                    }

                    try
                    {
                        if (selectedScraper == null)
                        {
                            var episodesResult = season.GetEpisodes();
                            var expectedEpisodes = episodesResult?.Items.Count(x =>
                                !x.IndexNumber.HasValue || x.IndexNumber.Value > 0) ?? 0;
                            var expectedYear = season.ProductionYear;
                            if ((!expectedYear.HasValue || expectedYear.Value <= 0) && episodesResult != null)
                            {
                                expectedYear = episodesResult.Items
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
                                _logger).ConfigureAwait(false);

                            if (!IsCompleteAutomaticSearch(search))
                            {
                                LogIncompleteAutomaticSearch(originalSeasonName, "season", search);
                                continue;
                            }

                            selectedCandidate = DanmuMatchScorer.SelectAutoCandidate(search.Candidates);
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
                        if (media == null)
                        {
                            _logger.LogInformation(
                                "[{0}]智能匹配成功，但获取不到视频信息. id: {1}",
                                selectedScraper.Name,
                                selectedMediaId);
                            continue;
                        }

                        var seasonProviderGeneration = BeginProviderWrite(season, selectedScraper.ProviderId);
                        if (!await DownloadAutomaticSeasonForImport(
                                season,
                                media,
                                selectedScraper,
                                selectedMediaId,
                                seasonProviderGeneration).ConfigureAwait(false))
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

            if (eventType == EventType.Update)
            {
                foreach (var season in seasons)
                {
                    // // 虚拟季第一次请求忽略
                    // if (season.LocationType == LocationType.Virtual && season.IndexNumber is null)
                    // {
                    //     continue;
                    // }

                    var queueUpdateMeta = new List<BaseItem>();
                    // GetEpisodes一定要取所有fields，要不然更新会导致重建虚拟season季信息
                    // TODO：可能出现未刮削完，就触发获取弹幕，导致GetEpisodes只能获取到部分剧集的情况
                    var episodesItem = season.GetEpisodes();
                    if (episodesItem == null)
                    {
                        continue;
                    }

                    var episodes = episodesItem.Items.ToList();
                    var originEpisodes = episodes;

                    // 不处理季文件夹下的特典和extras影片（动画经常会混在一起）
                    var episodesWithoutSP = episodes.Where(x => x.ParentIndexNumber != null && x.ParentIndexNumber > 0)
                        .ToList();
                    if (episodes.Count != episodesWithoutSP.Count)
                    {
                        _logger.LogInformation("{0}季存在{1}个特典或extra片段，忽略处理.", season.Name,
                            (episodes.Count - episodesWithoutSP.Count));
                        episodes = episodesWithoutSP;
                    }

                    var scrapers = _scraperManager.All();
                    foreach (var scraper in scrapers)
                    {
                        try
                        {
                            var providerVal = season.GetDanmuProviderId(scraper.ProviderId);
                            if (string.IsNullOrEmpty(providerVal))
                            {
                                continue;   
                            }

                            var media = await scraper.GetMedia(season, providerVal);
                            if (media == null)
                            {
                                _logger.LogInformation("[{0}]获取不到视频信息. ProviderId: {1}", scraper.Name, providerVal);
                                break;
                            }

                            // 剧集可能更新中
                            int dabmuEpisodesCount = media.Episodes.Count;
                            if (ignoreEpisodesMatch && dabmuEpisodesCount != episodes.Count)
                            {
                                _logger.Info("[{0}]剧集数不匹配. 可能是更新中进行强制更新: {1}, media.Episodes={2}, episodes.Count={3}", scraper.Name, providerVal, dabmuEpisodesCount, episodes.Count);
                            }

                            if (episodes.Count != dabmuEpisodesCount && originEpisodes.Count == dabmuEpisodesCount)
                            {
                                _logger.LogInformation("{0}季忽略特典或extra片段后媒体数={1}, 未忽略媒体数={2} == 获取弹幕媒体数={3}, 现在使用未忽略媒体数据", season.Name, episodes.Count, originEpisodes.Count, dabmuEpisodesCount);
                                episodes = originEpisodes;
                            }
                            
                            int minEpisodes = Math.Min(episodes.Count, dabmuEpisodesCount);
                            _logger.LogInformation("[{0}]匹配完成，媒体数={1}. 弹幕数={2}, 最终需要匹配数={3}, 弹幕工具={4}", season.Name, episodes.Count(), dabmuEpisodesCount, minEpisodes, scraper.Name);
                            for (var idx = 0; idx < minEpisodes; idx++)
                            {
                                var episode = episodes[idx];
                                var fileName = Path.GetFileName(episode.Path);
                                var indexNumber = episode.IndexNumber ?? 0;
                                if (indexNumber <= 0)
                                {
                                    _logger.LogInformation("[{0}]匹配失败，缺少集号. [{1}]{2}", scraper.Name, season.Name, fileName);
                                    continue;
                                }

                                if (indexNumber > dabmuEpisodesCount)
                                {
                                    _logger.LogInformation("[{0}]匹配失败，集号超过总集数，可能识别集号错误. [{1}]{2} indexNumber: {3}",
                                        scraper.Name, season.Name, fileName, indexNumber);
                                    continue;
                                }

                                if (ignoreEpisodesMatch || dabmuEpisodesCount == episodes.Count)
                                {
                                    var epId = media.Episodes[idx].Id;
                                    var commentId = media.Episodes[idx].CommentId;
                                    _logger.LogInformation("[{0}]成功匹配. {1}.{2} -> epId: {3} cid: {4}", scraper.Name,
                                        indexNumber, episode.Name, epId, commentId);

                                    var danmuXmlPath = Path.Combine(episode.ContainingFolderPath, episode.GetDanmuXmlPath(scraper.ProviderId));
                                    var lastWriteTime = this._fileSystem.GetLastWriteTime(danmuXmlPath);
                                    var diff = DateTime.Now - lastWriteTime;
                                    if (diff.TotalSeconds < 3600 * 24 * 7)
                                    {
                                        // 
                                        _logger.Info("{0}弹幕文件在7天内更新过, 忽略， 弹幕文件={1}", episode.Name, danmuXmlPath);
                                        continue;
                                    }

                                    try
                                    {
                                        // 下载弹幕
                                        var outcome = await DownloadDanmuForPersistence(scraper, episode, commentId).ConfigureAwait(false);
                                        if (outcome.FilePersisted && !string.IsNullOrWhiteSpace(epId))
                                        {
                                            await PersistDownloadProviderIdAsync(episode, outcome, epId).ConfigureAwait(false);
                                        }
                                    }
                                    catch (DanmuDownloadErrorException ex)
                                    {
                                        _logger.LogInformation("[{0}]弹幕下载失败，尝试匹配下一个. 失败原因={1}", scraper.Name, ex.Message);
                                        continue;
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation("[{0}]刷新弹幕失败, 集数不一致。video: {1}.{2} 弹幕数：{3} 集数：{4}",
                                        scraper.Name, indexNumber, episode.Name, dabmuEpisodesCount, episodes.Count);
                                }
                            }
                            
                            if (dabmuEpisodesCount < episodes.Count)
                            {
                                _logger.LogInformation("[{0}]未完全匹配，尝试下个插件继续匹配，媒体数={1}. 完成匹配数={2}, 未匹配数={3}, 弹幕工具={4}", season.Name, episodes.Count(), dabmuEpisodesCount, episodes.Count-dabmuEpisodesCount, scraper.Name);
                                continue;
                            }

                            if (!Config.OpenAllSource)
                            {
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

                    // 保存元数据
                    await ProcessQueuedUpdateMeta(queueUpdateMeta).ConfigureAwait(false);
                }
            }
        }


        private async Task SaveAutomaticSeasonProviderId(
            Season season,
            string providerId,
            string providerValue,
            long providerWriteGeneration)
        {
            var updateItem = _libraryManager.GetItemById(season.Id) as Season ?? season;
            await SaveProviderIdForGeneration(
                updateItem, providerId, providerValue, false, providerWriteGeneration).ConfigureAwait(false);
        }

        private async Task<bool> DownloadAutomaticSeasonForImport(
            Season season,
            ScraperMedia media,
            AbstractScraper scraper,
            string seasonProviderValue,
            long seasonProviderGeneration)
        {
            return await DownloadAutomaticSeasonWithCompositePlan(
                season, media, scraper, seasonProviderValue, seasonProviderGeneration).ConfigureAwait(false);

        }

        private async Task<bool> DownloadAutomaticSeasonWithCompositePlan(
            Season season, ScraperMedia primaryMedia, AbstractScraper primaryScraper,
            string primaryLookupId, long primaryGeneration)
        {
            var episodes = (season.GetEpisodes()?.Items ?? Array.Empty<BaseItem>()).OfType<Episode>()
                .Where(x => x.ParentIndexNumber.GetValueOrDefault() > 0)
                .OrderBy(x => x.IndexNumber ?? int.MaxValue).ToList();
            var direct = new List<CompositeSeasonEpisodeMapping>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var episode in episodes)
            {
                try
                {
                    var resolved = await DanmuProviderIdResolver.ResolveAsync(_scraperManager.All(),
                        new BaseItem[] { episode }, _logger).ConfigureAwait(false);
                    if (resolved.Candidate == null || resolved.Scraper == null ||
                        !string.Equals(resolved.ResolvedScopeType, "Episode", StringComparison.OrdinalIgnoreCase)) continue;
                    var mapping = CompositeSeasonMatchService.CreateDirectMapping(episode.Id.ToString(),
                        resolved.Scraper.ProviderId, resolved.Media, resolved.Candidate.Id);
                    var key = mapping == null ? string.Empty : mapping.Source.ProviderId + "\u001f" +
                        mapping.Source.MediaId + "\u001f" + mapping.SourceEpisodeId;
                    if (mapping != null && used.Add(key)) direct.Add(mapping);
                }
                catch (Exception ex) { _logger.LogError(ex, "[CompositeSeason] Auto direct mapping failed: {0}", episode.Name); }
            }
            if (!CompositeSeasonPlanner.TryCreatePlan(CompositeSeasonMatchService.GetLocalEpisodes(episodes), direct,
                    null, null, IsCompositeSeason(season), out var plan, out var error))
            {
                _logger.Error("[CompositeSeason] Auto plan rejected: season={0}, error={1}", season.Name, error);
                return false;
            }

            var seriesTitle = (season.GetParent() as Series)?.Name ?? season.Name ?? string.Empty;
            var primarySource = CompositeSeasonMatchService.GetSource(
                primaryScraper.ProviderId, primaryMedia, primaryLookupId);
            if (!CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, primarySource,
                    CompositeSeasonMatchService.GetSourceEpisodes(primaryMedia), "automatic-primary",
                    out plan, out var primaryExhausted, out error))
            {
                _logger.Error("[CompositeSeason] Automatic primary continuation rejected: season={0}, error={1}",
                    season.Name, error);
                return false;
            }

            // A source is excluded by its original provider lookup token only
            // after all of its verified source Episodes were consumed. This
            // lets an interior direct mapping continue the same source, yet
            // prevents an exhausted source from being selected again for a
            // later temporary group.
            var exhaustedSources = new List<CompositeSeasonSourceIdentity>();
            if (primaryExhausted) exhaustedSources.Add(primarySource);
            while (plan.UnmatchedRuns.Count > 0)
            {
                var run = plan.UnmatchedRuns[0];
                var search = await DanmuMatchSearchEngine.SearchSeasonAsync(_scraperManager.All(), seriesTitle,
                    seriesTitle, season.ProductionYear, run.Episodes.Count, seriesTitle, _logger).ConfigureAwait(false);
                if (!IsCompleteAutomaticSearch(search))
                {
                    LogIncompleteAutomaticSearch(season.Name, "residual-range", search);
                    // Plan construction precedes every file write.  Aborting
                    // here therefore leaves the residual run unmatched and
                    // prevents a partial round from binding or downloading
                    // even mappings that were already known.
                    return false;
                }
                // Exclusion happens before scoring/uniqueness selection, so
                // every exhausted source (not only the primary) cannot mask a
                // unique supplemental candidate on a later temporary group.
                var candidate = CompositeSeasonMatchService.SelectSupplementalCandidate(
                    search.Candidates, exhaustedSources);
                var sourceScraper = candidate == null ? null : _scraperManager.All().FirstOrDefault(x =>
                    string.Equals(x.ProviderId, candidate.Site, StringComparison.OrdinalIgnoreCase));
                if (sourceScraper == null) break;
                ScraperMedia sourceMedia;
                try { sourceMedia = await sourceScraper.GetMedia(season, candidate.Id).ConfigureAwait(false); }
                catch (Exception ex) { _logger.LogError(ex, "[CompositeSeason] Auto residual source failed: {0}", season.Name); break; }
                var source = CompositeSeasonMatchService.GetSource(sourceScraper.ProviderId, sourceMedia, candidate.Id);
                if (!CompositeSeasonMatchService.TryNormalizeAndContinueSource(plan, source,
                        CompositeSeasonMatchService.GetSourceEpisodes(sourceMedia), "automatic-residual",
                        out var continuedPlan, out var sourceExhausted, out error))
                {
                    _logger.LogInformation("[CompositeSeason] Ignored unusable residual source: season={0}, error={1}",
                        season.Name, error);
                    break;
                }
                plan = continuedPlan;
                if (sourceExhausted && !exhaustedSources.Any(existing =>
                        string.Equals(existing.ProviderId, source.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(existing.MediaLookupId, source.MediaLookupId, StringComparison.OrdinalIgnoreCase)))
                {
                    exhaustedSources.Add(source);
                }
            }

            if (plan.Mappings.Count == 0) return false;
            var byId = episodes.ToDictionary(x => x.Id.ToString(), StringComparer.OrdinalIgnoreCase);
            var sources = plan.Mappings.Select(x => x.Source).Distinct().ToList();
            var canSaveSeason = !plan.CompositeSafetyRequired && sources.Count == 1 &&
                plan.Mappings.All(x => !string.Equals(x.Origin, "episode-provider-id", StringComparison.OrdinalIgnoreCase));
            var savedSeason = false;
            var persisted = false;
            var lease = BeginCompositeSeasonWrite(season, plan.CompositeSafetyRequired);
            try
            {
                foreach (var mapping in plan.Mappings)
                {
                    if (!byId.TryGetValue(mapping.LocalEpisodeItemId, out var episode)) continue;
                    try
                    {
                        var sourceScraper = _scraperManager.All().FirstOrDefault(x => string.Equals(
                            x.ProviderId, mapping.Source.ProviderId, StringComparison.OrdinalIgnoreCase));
                        if (sourceScraper == null) continue;
                        var lookup = !string.IsNullOrWhiteSpace(mapping.Source.MediaLookupId)
                            ? mapping.Source.MediaLookupId : mapping.Source.MediaId;
                        var media = string.Equals(mapping.Origin, "episode-provider-id", StringComparison.OrdinalIgnoreCase)
                            ? await DanmuProviderIdResolver.ResolveDirectEpisodeMediaAsync(sourceScraper, episode, lookup, 1).ConfigureAwait(false)
                            : await sourceScraper.GetMedia(season, lookup).ConfigureAwait(false);
                        var sourceEpisode = (media?.Episodes ?? new List<ScraperEpisode>()).FirstOrDefault(x => x != null &&
                            string.Equals(x.Id, mapping.SourceEpisodeId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.CommentId));
                        if (sourceEpisode == null) continue;
                        var exact = new ScraperMedia { Id = media.Id, ProviderId = sourceScraper.ProviderId,
                            Episodes = new List<ScraperEpisode> { new ScraperEpisode { Id = sourceEpisode.Id,
                                CommentId = sourceEpisode.CommentId, EpisodeNumber = 1, Title = sourceEpisode.Title } } };
                        var outcome = await DownloadEpisodeForProgress(episode, exact, sourceScraper, false, 1).ConfigureAwait(false);
                        if (!outcome.FilePersisted) continue;
                        persisted = true;
                        await PersistDownloadProviderIdAsync(episode, outcome).ConfigureAwait(false);
                        if (plan.CompositeSafetyRequired) await OnCompositeSeasonFilePersistedAsync(season, lease).ConfigureAwait(false);
                        else if (canSaveSeason && !savedSeason)
                        {
                            await SaveAutomaticSeasonProviderId(season, sources[0].ProviderId,
                                !string.IsNullOrWhiteSpace(sources[0].MediaLookupId) ? sources[0].MediaLookupId : sources[0].MediaId,
                                primaryGeneration).ConfigureAwait(false);
                            savedSeason = true;
                        }
                    }
                    catch (Exception ex) { _logger.LogError(ex, "[CompositeSeason] Auto exact download failed: {0}", episode.Name); }
                }
            }
            finally { CompleteCompositeSeasonWrite(lease); }
            return persisted;
        }

        internal static bool IsCompleteAutomaticSearch(DanmuMatchSearchResult search)
        {
            return search != null && search.IsComplete;
        }

        private void LogIncompleteAutomaticSearch(
            string seasonName,
            string searchScope,
            DanmuMatchSearchResult search)
        {
            var diagnostics = search?.CompletionDiagnostics ?? new List<DanmuSearchCompletionDiagnostic>();
            var summary = string.Join(", ", diagnostics
                .Where(item => item != null &&
                    !string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase))
                .Select(item => (item.Provider ?? string.Empty) + ":" + (item.Status ?? string.Empty))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            _logger.LogInformation(
                "[SmartMatch] Automatic {0} search incomplete; no binding or download will run. season={1}, diagnostics={2}",
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
            if (!string.IsNullOrWhiteSpace(lookupId))
            {
                try
                {
                    mediaEpisode = await scraper.GetMediaEpisode(movie, lookupId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrWhiteSpace(media.CommentId))
                    {
                        throw;
                    }

                    _logger.Warn(
                        $"[{scraper.Name}] 电影 '{movie.Name}' 查询播放条目失败，改用搜索结果中的弹幕 ID：{ex.Message}");
                }
            }
            if ((mediaEpisode == null || string.IsNullOrWhiteSpace(mediaEpisode.CommentId)) &&
                !string.IsNullOrWhiteSpace(media.CommentId))
            {
                mediaEpisode = new ScraperEpisode
                {
                    Id = string.IsNullOrWhiteSpace(media.Id) ? lookupId : media.Id,
                    CommentId = media.CommentId,
                    Title = movie.Name ?? string.Empty,
                };
            }
            if (mediaEpisode == null || string.IsNullOrWhiteSpace(mediaEpisode.CommentId))
            {
                throw new DanmuDownloadErrorException("弹幕来源没有返回电影弹幕 ID");
            }

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

            // 下载弹幕xml文件
            var danmuPath = Path.Combine(item.ContainingFolderPath, item.GetDanmuXmlPath(scraper.ProviderId));
            try
            {
                await this._fileSystem.WriteAllBytesAsync(danmuPath, bytes, CancellationToken.None).ConfigureAwait(false);
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

                ProviderIdDictionary nextProviderIds;
                if (!manual && (updateItem is Season || updateItem is Episode))
                {
                    nextProviderIds = DanmuProviderIdWritePolicy.BuildSuccessfulWrite(
                        updateItem.ProviderIds,
                        registeredScrapers.Select(x => x.ProviderId),
                        providerId,
                        providerVal,
                        true);
                }
                else
                {
                    nextProviderIds = DanmuProviderIdWritePolicy.BuildSuccessfulWrite(
                        updateItem.ProviderIds,
                        Enumerable.Empty<string>(),
                        providerId,
                        providerVal,
                        false);
                    if (manual)
                    {
                        nextProviderIds[manualKey] = providerVal;
                    }
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
