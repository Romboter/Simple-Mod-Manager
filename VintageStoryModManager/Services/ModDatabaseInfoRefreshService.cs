using VintageStoryModManager.Models;
using Timer = System.Threading.Timer;

namespace VintageStoryModManager.Services;

/// <summary>
///     Orchestrates mod-database-info refreshes: filters and queues entries, cancels superseded
///     runs, fetches online info via ModDatabaseService (or offline via OfflineModDatabaseInfoBuilder),
///     batches results on a timer, and hands prepared results to the owner through the two apply
///     delegates. Dispatcher marshalling, entry mutation, and the entry/view-model dictionaries stay
///     with the owner — this service never writes a ModEntry.
/// </summary>
public sealed class ModDatabaseInfoRefreshService : IDisposable
{
    private static readonly int MaxConcurrentDatabaseRefreshes = DevConfig.MaxConcurrentDatabaseRefreshes;

    private readonly ModDatabaseService _databaseService;
    private readonly OfflineModDatabaseInfoBuilder _offlineInfoBuilder;
    private readonly ModLoadingTimingService _timingService;
    private readonly Func<string?> _installedGameVersionProvider;
    private readonly Func<bool> _requireExactVsVersionMatch;
    private readonly Func<bool> _allowModDetailsRefresh;
    private readonly Func<bool> _isInitialLoad;
    private readonly Func<bool> _isTagsColumnVisible;
    private readonly Func<bool> _areUserReportsVisible;
    private readonly Action<int> _onRefreshEnqueued;
    private readonly Action _onRefreshCompleted;
    private readonly Func<IReadOnlyList<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)>, Task> _applyBatchAsync;
    private readonly Func<ModEntry, ModDatabaseInfo, bool, Task> _applyImmediateAsync;

    private readonly object _databaseRefreshLock = new();
    private readonly HashSet<string> _suppressedTagEntries = new(StringComparer.OrdinalIgnoreCase);

    // Database info batching for improved UI performance
    private readonly object _databaseInfoBatchLock = new();
    private readonly List<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)> _pendingDatabaseInfoUpdates = new();
    private Timer? _databaseInfoBatchTimer;
    private const int DatabaseInfoBatchDelayMs = 50; // Batch updates every 50ms
    private const int DatabaseInfoBatchSize = 20; // Apply up to 20 updates per batch

    private Task? _databaseRefreshTask;
    private CancellationTokenSource? _databaseRefreshCts;

    public ModDatabaseInfoRefreshService(
        ModDatabaseService databaseService,
        OfflineModDatabaseInfoBuilder offlineInfoBuilder,
        ModLoadingTimingService timingService,
        Func<string?> installedGameVersionProvider,
        Func<bool> requireExactVsVersionMatch,
        Func<bool> allowModDetailsRefresh,
        Func<bool> isInitialLoad,
        Func<bool> isTagsColumnVisible,
        Func<bool> areUserReportsVisible,
        Action<int> onRefreshEnqueued,
        Action onRefreshCompleted,
        Func<IReadOnlyList<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)>, Task> applyBatchAsync,
        Func<ModEntry, ModDatabaseInfo, bool, Task> applyImmediateAsync)
    {
        ArgumentNullException.ThrowIfNull(databaseService);
        ArgumentNullException.ThrowIfNull(offlineInfoBuilder);
        ArgumentNullException.ThrowIfNull(timingService);
        ArgumentNullException.ThrowIfNull(installedGameVersionProvider);
        ArgumentNullException.ThrowIfNull(requireExactVsVersionMatch);
        ArgumentNullException.ThrowIfNull(allowModDetailsRefresh);
        ArgumentNullException.ThrowIfNull(isInitialLoad);
        ArgumentNullException.ThrowIfNull(isTagsColumnVisible);
        ArgumentNullException.ThrowIfNull(areUserReportsVisible);
        ArgumentNullException.ThrowIfNull(onRefreshEnqueued);
        ArgumentNullException.ThrowIfNull(onRefreshCompleted);
        ArgumentNullException.ThrowIfNull(applyBatchAsync);
        ArgumentNullException.ThrowIfNull(applyImmediateAsync);

        _databaseService = databaseService;
        _offlineInfoBuilder = offlineInfoBuilder;
        _timingService = timingService;
        _installedGameVersionProvider = installedGameVersionProvider;
        _requireExactVsVersionMatch = requireExactVsVersionMatch;
        _allowModDetailsRefresh = allowModDetailsRefresh;
        _isInitialLoad = isInitialLoad;
        _isTagsColumnVisible = isTagsColumnVisible;
        _areUserReportsVisible = areUserReportsVisible;
        _onRefreshEnqueued = onRefreshEnqueued;
        _onRefreshCompleted = onRefreshCompleted;
        _applyBatchAsync = applyBatchAsync;
        _applyImmediateAsync = applyImmediateAsync;
    }

    public void QueueRefresh(IEnumerable<ModEntry> entries, bool forceRefresh = false)
    {
        if (entries is null) return;

        if (!_allowModDetailsRefresh() && !forceRefresh) return;

        var pending = entries
            .Where(entry => entry != null
                            && !string.IsNullOrWhiteSpace(entry.ModId)
                            && (forceRefresh || NeedsDatabaseRefresh(entry)))
            .ToArray();

        if (pending.Length == 0) return;

        _onRefreshEnqueued(pending.Length);

        CancellationToken refreshToken;
        lock (_databaseRefreshLock)
        {
            _databaseRefreshCts?.Cancel();
            _databaseRefreshCts?.Dispose();
            _databaseRefreshCts = new CancellationTokenSource();
            refreshToken = _databaseRefreshCts.Token;
        }

        _databaseRefreshTask = Task.Run(() => RefreshDatabaseInfoBatchAsync(pending, refreshToken), refreshToken);
    }

    private async Task RefreshDatabaseInfoBatchAsync(ModEntry[] pending, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (InternetAccessManager.IsInternetAccessDisabled)
            {
                await PopulateOfflineDatabaseInfoAsync(pending, cancellationToken).ConfigureAwait(false);
                return;
            }

            // Use progressive loading strategy for large mod counts
            if (pending.Length > 100)
            {
                await RefreshDatabaseInfoProgressivelyAsync(pending, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var limiter = new SemaphoreSlim(MaxConcurrentDatabaseRefreshes, MaxConcurrentDatabaseRefreshes);
                var refreshTasks = pending
                    .Select(entry => RefreshDatabaseInfoAsync(entry, limiter, cancellationToken))
                    .ToArray();

                await Task.WhenAll(refreshTasks).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected when a newer refresh supersedes the current one.
        }
        catch (Exception)
        {
            // Swallow unexpected exceptions from the refresh loop.
        }
        finally
        {
            // Flush any pending batched updates when refresh completes
            FlushDatabaseInfoBatch();
        }
    }

    private bool NeedsDatabaseRefresh(ModEntry entry)
    {
        if (entry is null) return false;

        if (_isTagsColumnVisible()
            && TryGetTagSuppressionKey(entry, out var key)
            && key != null
            && _suppressedTagEntries.Contains(key))
            return true;

        return entry.DatabaseInfo == null || entry.DatabaseInfo.IsOfflineOnly;
    }

    private bool ShouldSkipOnlineDatabaseRefresh(ModDatabaseInfo? cachedInfo)
    {
        if (_isTagsColumnVisible()) return false;

        if (_areUserReportsVisible()) return false;

        if (cachedInfo is null || cachedInfo.IsOfflineOnly) return false;

        return true;
    }

    private static bool TryGetTagSuppressionKey(ModEntry entry, out string? key)
    {
        key = null;

        if (entry is null) return false;

        if (!string.IsNullOrWhiteSpace(entry.SourcePath))
        {
            key = entry.SourcePath;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(entry.ModId))
        {
            key = entry.ModId;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Progressively refreshes database info for large mod collections.
    /// First batch (visible items) gets high priority, rest are processed in background.
    /// </summary>
    private async Task RefreshDatabaseInfoProgressivelyAsync(ModEntry[] entries, CancellationToken cancellationToken)
    {
        const int priorityBatchSize = 50; // First 50 mods get immediate attention
        const int regularBatchSize = 20;  // Subsequent batches are smaller

        using var limiter = new SemaphoreSlim(MaxConcurrentDatabaseRefreshes, MaxConcurrentDatabaseRefreshes);

        // Process first batch with high priority (likely visible in UI)
        var priorityCount = Math.Min(priorityBatchSize, entries.Length);
        var priorityBatch = entries.Take(priorityCount).ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        var priorityTasks = priorityBatch
            .Select(entry => RefreshDatabaseInfoAsync(entry, limiter, cancellationToken))
            .ToArray();

        await Task.WhenAll(priorityTasks).ConfigureAwait(false);

        // Flush after priority batch to show initial results quickly
        FlushDatabaseInfoBatch();

        // Process remaining entries in smaller batches with delays to avoid overwhelming the system
        var remaining = entries.Skip(priorityCount).ToArray();
        for (int i = 0; i < remaining.Length; i += regularBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = remaining.Skip(i).Take(regularBatchSize).ToArray();
            var batchTasks = batch
                .Select(entry => RefreshDatabaseInfoAsync(entry, limiter, cancellationToken))
                .ToArray();

            await Task.WhenAll(batchTasks).ConfigureAwait(false);

            // Small delay between batches to keep UI responsive
            if (i + regularBatchSize < remaining.Length)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RefreshDatabaseInfoAsync(ModEntry entry, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);

        using var logScope = StatusLogService.BeginDebugScope(entry.Name, entry.ModId, "metadata");
        using var timingScope = _timingService.MeasureDatabaseInfoLoad();
        var cacheHit = false;
        var source = string.Empty;
        var tagCount = 0;
        var releaseCount = 0;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // On initial load, use cache-only approach for better startup performance
            // This skips expensive network checks and just uses whatever is cached
            if (_isInitialLoad())
            {
                ModDatabaseInfo? cachedInfo;
                using (_timingService.MeasureDbCacheLoad())
                {
                    cachedInfo = await _databaseService
                        .TryLoadCachedDatabaseInfoAsync(entry.ModId, entry.Version, _installedGameVersionProvider(),
                            _requireExactVsVersionMatch())
                        .ConfigureAwait(false);
                }

                if (cachedInfo != null)
                {
                    cacheHit = true;
                    using (_timingService.MeasureDbApplyInfo())
                    {
                        await ApplyDatabaseInfoAsync(entry, cachedInfo, false).ConfigureAwait(false);
                    }
                    tagCount = cachedInfo.Tags?.Count ?? 0;
                    releaseCount = cachedInfo.Releases?.Count ?? 0;
                    source = "cache";
                    return;
                }

                // No cache available, create offline info
                using (_timingService.MeasureDbOfflineInfo())
                {
                    await PopulateOfflineInfoForEntryAsync(entry).ConfigureAwait(false);
                }
                tagCount = entry.DatabaseInfo?.Tags?.Count ?? 0;
                releaseCount = entry.DatabaseInfo?.Releases?.Count ?? 0;
                source = "offline";
                return;
            }

            // For subsequent refreshes, use normal refresh logic with version checks
            ModDatabaseInfo? cachedInfo2;
            bool needsRefresh;
            using (_timingService.MeasureDbCacheLoad())
            {
                (cachedInfo2, needsRefresh) = await _databaseService
                    .TryLoadCachedDatabaseInfoWithRefreshCheckAsync(entry.ModId, entry.Version, _installedGameVersionProvider(),
                        _requireExactVsVersionMatch())
                    .ConfigureAwait(false);
            }

            cacheHit = cachedInfo2 != null;

            if (cachedInfo2 != null)
            {
                using (_timingService.MeasureDbApplyInfo())
                {
                    await ApplyDatabaseInfoAsync(entry, cachedInfo2, false).ConfigureAwait(false);
                }
                tagCount = cachedInfo2.Tags?.Count ?? 0;
                releaseCount = cachedInfo2.Releases?.Count ?? 0;

                if (InternetAccessManager.IsInternetAccessDisabled)
                {
                    source = "cache";
                    return;
                }

                // Skip network request if no refresh is needed (version unchanged)
                if (!needsRefresh)
                {
                    source = "cache";
                    return;
                }

                if (ShouldSkipOnlineDatabaseRefresh(cachedInfo2))
                {
                    source = "cache";
                    return;
                }
            }
            else if (InternetAccessManager.IsInternetAccessDisabled)
            {
                using (_timingService.MeasureDbOfflineInfo())
                {
                    await PopulateOfflineInfoForEntryAsync(entry).ConfigureAwait(false);
                }
                tagCount = entry.DatabaseInfo?.Tags?.Count ?? 0;
                releaseCount = entry.DatabaseInfo?.Releases?.Count ?? 0;
                source = "offline";
                return;
            }

            ModDatabaseInfo? info;
            try
            {
                // Pass the already-loaded cached info to avoid re-reading from disk
                using (_timingService.MeasureDbNetworkLoad())
                {
                    info = await _databaseService
                        .TryLoadDatabaseInfoAsync(entry.ModId, entry.Version, _installedGameVersionProvider(),
                            _requireExactVsVersionMatch(), cachedInfo2, cancellationToken, _timingService)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                source = cacheHit ? "cache" : "error";
                return;
            }

            if (info is null)
            {
                if (cacheHit)
                {
                    source = "cache";
                    return;
                }

                using (_timingService.MeasureDbOfflineInfo())
                {
                    await PopulateOfflineInfoForEntryAsync(entry).ConfigureAwait(false);
                }
                tagCount = entry.DatabaseInfo?.Tags?.Count ?? 0;
                releaseCount = entry.DatabaseInfo?.Releases?.Count ?? 0;
                source = "offline";
                return;
            }

            using (_timingService.MeasureDbApplyInfo())
            {
                await ApplyDatabaseInfoAsync(entry, info).ConfigureAwait(false);
            }
            tagCount = info.Tags?.Count ?? tagCount;
            releaseCount = info.Releases?.Count ?? releaseCount;
            source = info.IsOfflineOnly ? "offline" : "net";
        }
        finally
        {
            limiter.Release();
            if (logScope != null)
            {
                logScope.SetCacheStatus(cacheHit);
                if (!string.IsNullOrWhiteSpace(source)) logScope.SetDetail("src", source);

                logScope.SetDetail("tags", tagCount);
                logScope.SetDetail("rel", releaseCount);
            }

            _onRefreshCompleted();
        }
    }

    private async Task PopulateOfflineDatabaseInfoAsync(IEnumerable<ModEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry is not null)
                {
                    using (_timingService.MeasureDatabaseInfoLoad())
                    using (_timingService.MeasureDbOfflineInfo())
                    {
                        await PopulateOfflineInfoForEntryAsync(entry).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
                // Swallow unexpected exceptions for resilience.
            }
            finally
            {
                _onRefreshCompleted();
            }
    }

    private async Task PopulateOfflineInfoForEntryAsync(ModEntry entry)
    {
        if (entry is null) return;

        var cachedInfo = entry.DatabaseInfo;
        if (cachedInfo is null)
            cachedInfo = await _databaseService
                .TryLoadCachedDatabaseInfoAsync(entry.ModId, entry.Version, _installedGameVersionProvider(),
                    _requireExactVsVersionMatch())
                .ConfigureAwait(false);

        var offlineInfo = _offlineInfoBuilder.CreateOfflineDatabaseInfo(entry);

        var mergedInfo = OfflineModDatabaseInfoBuilder.MergeOfflineAndCachedInfo(offlineInfo, cachedInfo);
        if (mergedInfo is null) return;

        await ApplyDatabaseInfoAsync(entry, mergedInfo).ConfigureAwait(false);
    }

    private async Task ApplyDatabaseInfoAsync(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately = true)
    {
        if (info is null) return;

        var preparedInfo = PrepareDatabaseInfoForVisibility(entry, info);

        // During initial load or when we have many pending updates, use batching for better performance
        // This dramatically reduces dispatcher overhead by grouping updates together
        if (_isInitialLoad() || ShouldUseBatchedUpdates())
        {
            QueueDatabaseInfoUpdate(entry, preparedInfo, loadLogoImmediately);
            return;
        }

        // For individual updates (e.g., user-triggered refresh), apply immediately
        await _applyImmediateAsync(entry, preparedInfo, loadLogoImmediately).ConfigureAwait(false);
    }

    private bool ShouldUseBatchedUpdates()
    {
        lock (_databaseInfoBatchLock)
        {
            // Use batching if we already have pending updates to continue the batch
            return _pendingDatabaseInfoUpdates.Count > 0;
        }
    }

    private void QueueDatabaseInfoUpdate(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)
    {
        lock (_databaseInfoBatchLock)
        {
            // Add to batch queue
            _pendingDatabaseInfoUpdates.Add((entry, info, loadLogoImmediately));

            // If batch is full, flush immediately
            if (_pendingDatabaseInfoUpdates.Count >= DatabaseInfoBatchSize)
            {
                // Dispose timer before flushing to prevent race condition
                _databaseInfoBatchTimer?.Dispose();
                _databaseInfoBatchTimer = null;
                FlushDatabaseInfoBatchLocked();
            }
            else
            {
                // Otherwise, schedule a timer to flush soon
                // Dispose old timer first to prevent race condition
                _databaseInfoBatchTimer?.Dispose();
                _databaseInfoBatchTimer = new Timer(
                    _ =>
                    {
                        lock (_databaseInfoBatchLock)
                        {
                            // Only flush if this timer hasn't been disposed/replaced
                            if (_databaseInfoBatchTimer != null)
                            {
                                FlushDatabaseInfoBatchLocked();
                            }
                        }
                    },
                    null,
                    DatabaseInfoBatchDelayMs,
                    Timeout.Infinite);
            }
        }
    }

    private void FlushDatabaseInfoBatch()
    {
        lock (_databaseInfoBatchLock)
        {
            FlushDatabaseInfoBatchLocked();
        }
    }

    /// <summary>
    /// Flushes pending database info updates. Must be called while holding _databaseInfoBatchLock.
    /// Note: This does NOT trigger recursive batching because the batch apply delegate
    /// applies updates directly without going through ApplyDatabaseInfoAsync.
    /// </summary>
    private void FlushDatabaseInfoBatchLocked()
    {
        if (_pendingDatabaseInfoUpdates.Count == 0) return;

        var batch = new List<(ModEntry, ModDatabaseInfo, bool)>(_pendingDatabaseInfoUpdates);
        _pendingDatabaseInfoUpdates.Clear();

        _databaseInfoBatchTimer?.Dispose();
        _databaseInfoBatchTimer = null;

        // Apply the batch on the dispatcher
        // Note: This is intentionally fire-and-forget, but we log any failures
        _ = _applyBatchAsync(batch);
    }

    private ModDatabaseInfo PrepareDatabaseInfoForVisibility(ModEntry entry, ModDatabaseInfo info)
    {
        if (!_isTagsColumnVisible())
        {
            if (TryGetTagSuppressionKey(entry, out var key) && key != null) _suppressedTagEntries.Add(key);

            return OfflineModDatabaseInfoBuilder.CreateInfoWithoutTags(info);
        }

        if (TryGetTagSuppressionKey(entry, out var visibleKey) && visibleKey != null)
            _suppressedTagEntries.Remove(visibleKey);

        return info;
    }

    public void Dispose()
    {
        lock (_databaseInfoBatchLock)
        {
            _databaseInfoBatchTimer?.Dispose();
            _databaseInfoBatchTimer = null;
            _pendingDatabaseInfoUpdates.Clear();
        }

        lock (_databaseRefreshLock)
        {
            _databaseRefreshCts?.Cancel();
            _databaseRefreshCts?.Dispose();
            _databaseRefreshCts = null;
        }

        if (_databaseRefreshTask != null)
        {
            try
            {
                _databaseRefreshTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Ignore background refresh cancellations during shutdown.
            }
        }
    }
}
