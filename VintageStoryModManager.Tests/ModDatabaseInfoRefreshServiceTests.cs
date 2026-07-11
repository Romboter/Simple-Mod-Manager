using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

/// <summary>
///     Covers <see cref="ModDatabaseInfoRefreshService"/>. <see cref="ModDatabaseService"/> is a real
///     instance, but entries use mod ids guaranteed to have no on-disk cache, so a real network request
///     is only reachable when an entry's cache-miss lookup falls through to the live fetch call — which
///     only happens when <c>isInitialLoad</c> is false. Every test in this class therefore defaults to
///     (or explicitly keeps) the harness's <c>IsInitialLoad = true</c>, which is network-safe on its own
///     regardless of <see cref="InternetAccessManager"/>'s global state, so most tests never touch that
///     static. The two tests that specifically need <c>isInitialLoad = false</c> or the offline-routing
///     branch scope their own narrow, try/finally-bounded toggle of
///     <see cref="InternetAccessManager.SetInternetAccessDisabled"/> (same pattern as
///     <c>SettingsMenuViewModelTests.InternetToggle_FiresStateCallback</c>) instead of doing it class-wide:
///     class-wide toggling was tried first and reliably broke the unrelated, parallel-running
///     <c>ModUpdatePollingServiceTests</c>, whose <c>FastCheck()</c> silently no-ops while that same global
///     flag is disabled.
/// </summary>
public sealed class ModDatabaseInfoRefreshServiceTests
{
    private sealed class Harness
    {
        public readonly object Lock = new();
        public readonly List<int> EnqueuedCounts = new();
        public int CompletedCount;
        public readonly List<IReadOnlyList<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)>> AppliedBatches = new();
        public readonly List<(ModEntry entry, ModDatabaseInfo info, bool loadLogoImmediately)> AppliedImmediates = new();
        public readonly SemaphoreSlim CompletedSignal = new(0);
        public readonly SemaphoreSlim AppliedSignal = new(0);

        public bool AllowModDetailsRefresh = true;
        public bool IsInitialLoad = true;
        public bool IsTagsColumnVisible = true;

        public int TotalAppliedItemCount
        {
            get
            {
                lock (Lock)
                {
                    return AppliedImmediates.Count + AppliedBatches.Sum(batch => batch.Count);
                }
            }
        }

        public async Task<bool> WaitForCompletedCountAsync(int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                lock (Lock)
                {
                    if (CompletedCount >= expected) return true;
                }

                await CompletedSignal.WaitAsync(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
            }

            lock (Lock)
            {
                return CompletedCount >= expected;
            }
        }

        public async Task<bool> WaitForAppliedItemCountAsync(int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (TotalAppliedItemCount >= expected) return true;

                await AppliedSignal.WaitAsync(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
            }

            return TotalAppliedItemCount >= expected;
        }
    }

    private static int _modIdCounter;

    private static ModEntry CreateEntry(ModDatabaseInfo? databaseInfo = null)
    {
        var id = Interlocked.Increment(ref _modIdCounter);
        var modId = $"no-cache-mod-{Guid.NewGuid():N}-{id}";
        return new ModEntry
        {
            ModId = modId,
            Name = modId,
            Version = "1.0.0",
            SourcePath = $@"C:\data\Mods\{modId}.zip",
            SourceKind = ModSourceKind.ZipArchive,
            DatabaseInfo = databaseInfo
        };
    }

    private static (ModDatabaseInfoRefreshService Service, Harness Harness) CreateService()
    {
        var harness = new Harness();

        var service = new ModDatabaseInfoRefreshService(
            new ModDatabaseService(),
            new OfflineModDatabaseInfoBuilder(() => null, () => false),
            new ModLoadingTimingService(),
            () => null,
            () => false,
            () => harness.AllowModDetailsRefresh,
            () => harness.IsInitialLoad,
            () => harness.IsTagsColumnVisible,
            () => false,
            count =>
            {
                lock (harness.Lock) harness.EnqueuedCounts.Add(count);
            },
            () =>
            {
                lock (harness.Lock) harness.CompletedCount++;
                harness.CompletedSignal.Release();
            },
            batch =>
            {
                lock (harness.Lock) harness.AppliedBatches.Add(batch);
                harness.AppliedSignal.Release();
                return Task.CompletedTask;
            },
            (entry, info, loadLogoImmediately) =>
            {
                lock (harness.Lock) harness.AppliedImmediates.Add((entry, info, loadLogoImmediately));
                harness.AppliedSignal.Release();
                return Task.CompletedTask;
            });

        return (service, harness);
    }

    [Fact]
    public void QueueRefresh_GateClosed_NoForce_NoOp()
    {
        var (service, harness) = CreateService();
        harness.AllowModDetailsRefresh = false;
        var entry = CreateEntry();

        service.QueueRefresh(new[] { entry }, forceRefresh: false);

        lock (harness.Lock)
        {
            Assert.Empty(harness.EnqueuedCounts);
            Assert.Equal(0, harness.CompletedCount);
        }

        service.Dispose();
    }

    [Fact]
    public async Task QueueRefresh_GateClosed_Force_Runs()
    {
        var (service, harness) = CreateService();
        harness.AllowModDetailsRefresh = false;
        var entry = CreateEntry();

        service.QueueRefresh(new[] { entry }, forceRefresh: true);

        lock (harness.Lock)
        {
            Assert.Equal(new[] { 1 }, harness.EnqueuedCounts);
        }

        var completed = await harness.WaitForCompletedCountAsync(1, TimeSpan.FromSeconds(5));
        Assert.True(completed);

        service.Dispose();
    }

    [Fact]
    public void QueueRefresh_FiltersNullAndBlankModIds()
    {
        var (service, harness) = CreateService();
        var blank = new ModEntry { ModId = "   ", Name = "blank", SourcePath = @"C:\data\Mods\blank.zip" };
        var good = CreateEntry();

        service.QueueRefresh(new[] { null!, blank, good }, forceRefresh: true);

        lock (harness.Lock)
        {
            Assert.Equal(new[] { 1 }, harness.EnqueuedCounts);
        }

        service.Dispose();
    }

    [Fact]
    public void QueueRefresh_NoForce_UsesNeedsDatabaseRefresh()
    {
        var (service, harness) = CreateService();
        var upToDate = CreateEntry(new ModDatabaseInfo { IsOfflineOnly = false, LatestVersion = "1.0.0" });
        var needsRefresh = CreateEntry();

        service.QueueRefresh(new[] { upToDate, needsRefresh }, forceRefresh: false);

        lock (harness.Lock)
        {
            Assert.Equal(new[] { 1 }, harness.EnqueuedCounts);
        }

        service.Dispose();
    }

    [Fact]
    public async Task NeedsRefresh_SuppressedTagEntry_TagsVisible_ReturnsTrue()
    {
        var (service, harness) = CreateService();
        harness.IsTagsColumnVisible = false;
        var entry = CreateEntry();

        // Seed suppression: force-queue with the tags column hidden so PrepareDatabaseInfoForVisibility
        // records the entry's suppression key.
        service.QueueRefresh(new[] { entry }, forceRefresh: true);
        var seeded = await harness.WaitForCompletedCountAsync(1, TimeSpan.FromSeconds(5));
        Assert.True(seeded);

        // The service never writes entry.DatabaseInfo (boundary rule), so simulate what the real VM
        // callback would have applied: a non-offline info that would otherwise fail NeedsDatabaseRefresh.
        entry.DatabaseInfo = new ModDatabaseInfo { IsOfflineOnly = false, LatestVersion = "1.0.0" };

        lock (harness.Lock) harness.EnqueuedCounts.Clear();
        harness.IsTagsColumnVisible = true;

        service.QueueRefresh(new[] { entry }, forceRefresh: false);

        lock (harness.Lock)
        {
            Assert.Equal(new[] { 1 }, harness.EnqueuedCounts);
        }

        service.Dispose();
    }

    [Fact]
    public async Task Requeue_CancelsPriorRun()
    {
        // The requeue cancels the first run's token before its background Task.Run body typically gets
        // scheduled, so RefreshDatabaseInfoBatchAsync's very first ThrowIfCancellationRequested() aborts
        // the whole run before it ever processes an entry or calls onRefreshCompleted. The second
        // (uncancelled) run then completes and applies normally. Both totals are asserted with headroom
        // for the (rare, harmless) case where the first run gets a small head start before cancellation.
        var (service, harness) = CreateService();
        var firstBatch = Enumerable.Range(0, 40).Select(_ => CreateEntry()).ToArray();
        var secondBatch = Enumerable.Range(0, 40).Select(_ => CreateEntry()).ToArray();

        service.QueueRefresh(firstBatch, forceRefresh: true);
        service.QueueRefresh(secondBatch, forceRefresh: true);

        var completed = await harness.WaitForCompletedCountAsync(secondBatch.Length, TimeSpan.FromSeconds(10));
        lock (harness.Lock)
        {
            Assert.True(completed,
                $"Expected at least {secondBatch.Length} completions, got {harness.CompletedCount}");
        }

        // Give any in-flight apply delegates (and any head-started first-run stragglers) a brief window
        // to land, then confirm the combined total never reached full double-processing.
        await Task.Delay(100);

        int completedTotal;
        lock (harness.Lock) completedTotal = harness.CompletedCount;
        var appliedTotal = harness.TotalAppliedItemCount;

        Assert.True(appliedTotal >= secondBatch.Length,
            $"Expected the second (uncancelled) run to fully apply; applied={appliedTotal}");
        Assert.True(completedTotal < firstBatch.Length + secondBatch.Length,
            $"Expected requeue to cancel the first run before it fully completed; completed={completedTotal}");
        Assert.True(appliedTotal < firstBatch.Length + secondBatch.Length,
            $"Expected requeue to cancel some of the first run's real work; applied={appliedTotal}");

        service.Dispose();
    }

    [Fact]
    public async Task Offline_PopulatesViaBuilder_AppliesMerged()
    {
        var priorState = InternetAccessManager.IsInternetAccessDisabled;
        InternetAccessManager.SetInternetAccessDisabled(true);
        try
        {
            var (service, harness) = CreateService();
            var entry = CreateEntry();

            service.QueueRefresh(new[] { entry }, forceRefresh: true);

            var applied = await harness.WaitForAppliedItemCountAsync(1, TimeSpan.FromSeconds(5));
            Assert.True(applied);
            Assert.Equal(1, harness.TotalAppliedItemCount);

            service.Dispose();
        }
        finally
        {
            InternetAccessManager.SetInternetAccessDisabled(priorState);
        }
    }

    [Fact]
    public async Task InitialLoad_RoutesToBatch()
    {
        var (service, harness) = CreateService();
        harness.IsInitialLoad = true;
        var entries = Enumerable.Range(0, 10).Select(_ => CreateEntry()).ToArray();

        service.QueueRefresh(entries, forceRefresh: true);

        var applied = await harness.WaitForAppliedItemCountAsync(entries.Length, TimeSpan.FromSeconds(5));
        Assert.True(applied);

        lock (harness.Lock)
        {
            Assert.Empty(harness.AppliedImmediates);
            Assert.NotEmpty(harness.AppliedBatches);
            Assert.Equal(entries.Length, harness.AppliedBatches.Sum(batch => batch.Count));
        }

        service.Dispose();
    }

    [Fact]
    public async Task PostInitial_SmallSet_RoutesToImmediate()
    {
        var priorState = InternetAccessManager.IsInternetAccessDisabled;
        InternetAccessManager.SetInternetAccessDisabled(true);
        try
        {
            var (service, harness) = CreateService();
            harness.IsInitialLoad = false;
            var entry = CreateEntry();

            service.QueueRefresh(new[] { entry }, forceRefresh: true);

            var applied = await harness.WaitForAppliedItemCountAsync(1, TimeSpan.FromSeconds(5));
            Assert.True(applied);

            lock (harness.Lock)
            {
                Assert.Single(harness.AppliedImmediates);
                Assert.Empty(harness.AppliedBatches);
            }

            service.Dispose();
        }
        finally
        {
            InternetAccessManager.SetInternetAccessDisabled(priorState);
        }
    }

    [Fact]
    public async Task Dispose_MidRun_StopsCleanly()
    {
        var (service, harness) = CreateService();
        harness.IsInitialLoad = true;
        var entries = Enumerable.Range(0, 50).Select(_ => CreateEntry()).ToArray();

        service.QueueRefresh(entries, forceRefresh: true);

        service.Dispose();
        service.Dispose(); // second dispose must not throw

        await Task.Delay(50);
    }
}
