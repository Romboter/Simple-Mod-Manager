using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using Application = System.Windows.Application;

namespace VintageStoryModManager.Services;

/// <summary>
///     Owns the community "user report" (vote) feature end to end: fetching/caching vote summaries
///     per mod+version with ETag-based conditional requests, submitting/removing votes, and the
///     operation-count bookkeeping used to drive busy state during report fetches.
/// </summary>
public sealed class UserReportsCoordinator : IDisposable
{
    private static readonly int MaxConcurrentUserReportRefreshes = DevConfig.MaxConcurrentUserReportRefreshes;

    private readonly string _voteEtagCachePath;
    private readonly object _voteEtagPersistenceLock = new();
    private readonly Dictionary<string, string> _userReportEtags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _latestReleaseUserReportEtags = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _userReportOperationLock = new();
    private int _activeUserReportOperations;

    private readonly SemaphoreSlim _userReportRefreshLimiter =
        new(MaxConcurrentUserReportRefreshes, MaxConcurrentUserReportRefreshes);

    private readonly ModVersionVoteService _voteService = new();

    private readonly Func<IDisposable> _beginBusyScope;
    private readonly Func<string?> _installedGameVersionProvider;
    private readonly Func<IEnumerable<ModListItemViewModel>> _installedModSubscriptionsProvider;
    private readonly Func<IEnumerable<ModListItemViewModel>> _searchResultSubscriptionsProvider;
    private readonly Func<bool> _allowModDetailsRefreshProvider;

    private bool _areUserReportsVisible = true;
    private bool _hasEnabledUserReportFetching;
    private bool _hasFetchedUserReportsThisSession;

    public event EventHandler<MainViewModel.ModUserReportChangedEventArgs>? UserReportVoteSubmitted;

    public UserReportsCoordinator(
        string voteEtagCachePath,
        Func<IDisposable> beginBusyScope,
        Func<string?> installedGameVersionProvider,
        Func<IEnumerable<ModListItemViewModel>> installedModSubscriptionsProvider,
        Func<IEnumerable<ModListItemViewModel>> searchResultSubscriptionsProvider,
        Func<bool> allowModDetailsRefreshProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(voteEtagCachePath);
        ArgumentNullException.ThrowIfNull(beginBusyScope);
        ArgumentNullException.ThrowIfNull(installedGameVersionProvider);
        ArgumentNullException.ThrowIfNull(installedModSubscriptionsProvider);
        ArgumentNullException.ThrowIfNull(searchResultSubscriptionsProvider);
        ArgumentNullException.ThrowIfNull(allowModDetailsRefreshProvider);

        _voteEtagCachePath = voteEtagCachePath;
        _beginBusyScope = beginBusyScope;
        _installedGameVersionProvider = installedGameVersionProvider;
        _installedModSubscriptionsProvider = installedModSubscriptionsProvider;
        _searchResultSubscriptionsProvider = searchResultSubscriptionsProvider;
        _allowModDetailsRefreshProvider = allowModDetailsRefreshProvider;

        _hasEnabledUserReportFetching = FirebaseAnonymousAuthenticator.HasPersistedState();

        LoadVoteEtagsFromDisk();
    }

    public void Dispose()
    {
        _userReportRefreshLimiter.Dispose();
        _voteService.Dispose();
    }

    /// <summary>
    ///     Whether the user-reports column/feature is currently visible. Mirrors the former
    ///     <c>MainViewModel._areUserReportsVisible</c> field, which had one remaining reader outside the
    ///     moved cluster (<c>ShouldSkipOnlineDatabaseRefresh</c>).
    /// </summary>
    public bool IsVisible => _areUserReportsVisible;

    /// <summary>
    ///     Updates the user-reports-visible flag, mirroring the behavior of the former
    ///     <c>MainViewModel.SetUserReportsColumnVisibility</c>. Returns <see langword="true" /> if the
    ///     visibility actually changed (the caller still needs to decide whether to re-enable fetching,
    ///     since that also depends on the caller's <c>_allowModDetailsRefresh</c> flag).
    /// </summary>
    public bool SetVisibility(bool isVisible)
    {
        if (_areUserReportsVisible == isVisible) return false;

        _areUserReportsVisible = isVisible;

        if (!isVisible)
        {
            _hasEnabledUserReportFetching = false;
            _hasFetchedUserReportsThisSession = false;
        }

        return true;
    }

    public void QueueLatestReleaseUserReportRefresh(ModListItemViewModel mod)
    {
        if (mod is null) return;

        if (!_areUserReportsVisible) return;

        _ = RunUserReportOperationAsync(
            ct => RefreshLatestReleaseUserReportCoreAsync(mod, true, ct),
            CancellationToken.None);
    }

    public Task<ModVersionVoteSummary?> RefreshLatestReleaseUserReportAsync(
        ModListItemViewModel mod,
        CancellationToken cancellationToken = default)
    {
        return RunUserReportOperationAsync(
            ct => RefreshLatestReleaseUserReportCoreAsync(mod, false, ct),
            cancellationToken);
    }

    private async Task<ModVersionVoteSummary?> RefreshLatestReleaseUserReportCoreAsync(
        ModListItemViewModel mod,
        bool suppressErrors,
        CancellationToken cancellationToken)
    {
        if (mod is null) return null;

        var latestReleaseVersion = mod.LatestRelease?.Version;
        if (string.IsNullOrWhiteSpace(latestReleaseVersion))
        {
            await InvokeOnDispatcherAsync(mod.ClearLatestReleaseUserReport, cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreLatestReleaseUserReportEtag(mod.ModId, null, null);
            return null;
        }

        if (string.Equals(mod.LatestReleaseUserReportVersion, latestReleaseVersion, StringComparison.OrdinalIgnoreCase)
            && mod.LatestReleaseUserReportSummary is not null)
            return mod.LatestReleaseUserReportSummary;

        var installedGameVersion = _installedGameVersionProvider();

        if (string.IsNullOrWhiteSpace(installedGameVersion))
        {
            await InvokeOnDispatcherAsync(mod.ClearLatestReleaseUserReport, cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreLatestReleaseUserReportEtag(mod.ModId, latestReleaseVersion, null);
            return null;
        }

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            await InvokeOnDispatcherAsync(mod.ClearLatestReleaseUserReport, cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreLatestReleaseUserReportEtag(mod.ModId, latestReleaseVersion, null);
            return null;
        }

        try
        {
            var etag = GetVoteEtag(_latestReleaseUserReportEtags, "latest", mod.ModId, latestReleaseVersion,
                installedGameVersion);

            var result = await _voteService
                .GetVoteSummaryIfChangedAsync(
                    mod.ModId,
                    latestReleaseVersion,
                    installedGameVersion,
                    etag,
                    cancellationToken)
                .ConfigureAwait(false);

            var summary = result.Summary ?? mod.LatestReleaseUserReportSummary;

            if (!result.IsNotModified || mod.LatestReleaseUserReportSummary is null)
                if (summary is not null)
                    await InvokeOnDispatcherAsync(
                            () => mod.ApplyLatestReleaseUserReportSummary(summary),
                            cancellationToken,
                            DispatcherPriority.Background)
                        .ConfigureAwait(false);

            StoreLatestReleaseUserReportEtag(mod.ModId, latestReleaseVersion, result.ETag);

            return summary;
        }
        catch (InternetAccessDisabledException)
        {
            await InvokeOnDispatcherAsync(mod.ClearLatestReleaseUserReport, cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreLatestReleaseUserReportEtag(mod.ModId, latestReleaseVersion, null);
            return null;
        }
        catch (Exception ex)
        {
            if (!suppressErrors)
                StatusLogService.AppendStatus(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Failed to refresh user reports for the latest release of {0}: {1}",
                        mod.DisplayName,
                        ex.Message),
                    true);

            await InvokeOnDispatcherAsync(mod.ClearLatestReleaseUserReport, cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreLatestReleaseUserReportEtag(mod.ModId, latestReleaseVersion, null);
            return null;
        }
    }

    public void QueueUserReportRefresh(ModListItemViewModel mod)
    {
        if (mod is null) return;

        if (!_areUserReportsVisible) return;

        _ = RunUserReportOperationAsync(
            ct => RefreshUserReportCoreAsync(mod, true, ct),
            CancellationToken.None);
    }

    public void EnableUserReportFetching(bool includeInstalledWhenAutoRefreshDisabled = false)
    {
        if (!_areUserReportsVisible) return;

        if (_hasFetchedUserReportsThisSession) return;

        _hasEnabledUserReportFetching = true;

        var allowInstalledRefresh = _allowModDetailsRefreshProvider() || includeInstalledWhenAutoRefreshDisabled;
        var hasQueuedRefresh = false;

        if (allowInstalledRefresh)
        {
            foreach (var mod in _installedModSubscriptionsProvider())
            {
                mod.EnsureUserReportStateInitialized();
                QueueUserReportRefresh(mod);
                hasQueuedRefresh = true;
            }
        }

        foreach (var mod in _searchResultSubscriptionsProvider())
        {
            mod.EnsureUserReportStateInitialized();
            if (mod.CanSubmitUserReport)
            {
                QueueUserReportRefresh(mod);
                hasQueuedRefresh = true;
            }

            QueueLatestReleaseUserReportRefresh(mod);
            hasQueuedRefresh = true;
        }

        if (hasQueuedRefresh) _hasFetchedUserReportsThisSession = true;
    }

    public Task<ModVersionVoteSummary?> RefreshUserReportAsync(
        ModListItemViewModel mod,
        CancellationToken cancellationToken = default)
    {
        return RunUserReportOperationAsync(
            ct => RefreshUserReportCoreAsync(mod, false, ct),
            cancellationToken);
    }

    public async Task<ModVersionVoteSummary?> SubmitUserReportVoteAsync(
        ModListItemViewModel mod,
        ModVersionVoteOption? option,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (mod is null) return null;

        var installedGameVersion = _installedGameVersionProvider();

        if (string.IsNullOrWhiteSpace(installedGameVersion) || string.IsNullOrWhiteSpace(mod.UserReportModVersion))
        {
            await InvokeOnDispatcherAsync(
                    () => mod.SetUserReportUnavailable("User reports require a known Vintage Story and mod version."),
                    cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            return null;
        }

        if (InternetAccessManager.IsInternetAccessDisabled)
            throw new InternetAccessDisabledException(
                "Internet access is disabled. Enable it in the File menu to submit your vote.");

        await InvokeOnDispatcherAsync(mod.SetUserReportLoading, cancellationToken, DispatcherPriority.Background)
            .ConfigureAwait(false);

        try
        {
            var (Summary, etag) = option.HasValue
                ? await _voteService
                    .SubmitVoteAsync(
                        mod.ModId,
                        mod.UserReportModVersion!,
                        installedGameVersion,
                        option.Value,
                        comment,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await _voteService
                    .RemoveVoteAsync(mod.ModId, mod.UserReportModVersion!, installedGameVersion, cancellationToken)
                    .ConfigureAwait(false);

            await InvokeOnDispatcherAsync(
                    () => mod.ApplyUserReportSummary(Summary),
                    cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);

            StoreUserReportEtag(mod.ModId, mod.UserReportModVersion, etag);

            if (Summary is not null)
                RaiseUserReportVoteSubmitted(mod, Summary);

            return Summary;
        }
        catch (InternetAccessDisabledException)
        {
            await InvokeOnDispatcherAsync(mod.SetUserReportOffline, cancellationToken, DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreUserReportEtag(mod.ModId, mod.UserReportModVersion, null);
            throw;
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus(
                string.Format(CultureInfo.CurrentCulture, "Failed to submit user report for {0}: {1}", mod.DisplayName,
                    ex.Message),
                true);
            throw;
        }
    }

    private void RaiseUserReportVoteSubmitted(ModListItemViewModel mod, ModVersionVoteSummary summary)
    {
        int? numericId = null;
        if (int.TryParse(mod.ModId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            numericId = parsedId;

        UserReportVoteSubmitted?.Invoke(
            this,
            new MainViewModel.ModUserReportChangedEventArgs(mod.ModId, mod.UserReportModVersion, numericId, summary));
    }

    private async Task<ModVersionVoteSummary?> RefreshUserReportCoreAsync(
        ModListItemViewModel mod,
        bool suppressErrors,
        CancellationToken cancellationToken)
    {
        if (mod is null) return null;

        var installedGameVersion = _installedGameVersionProvider();

        if (string.IsNullOrWhiteSpace(installedGameVersion) || string.IsNullOrWhiteSpace(mod.UserReportModVersion))
        {
            await InvokeOnDispatcherAsync(
                    () => mod.SetUserReportUnavailable("User reports require a known Vintage Story and mod version."),
                    cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreUserReportEtag(mod.ModId, mod.UserReportModVersion, null);
            return null;
        }

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            await InvokeOnDispatcherAsync(mod.SetUserReportOffline, cancellationToken, DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreUserReportEtag(mod.ModId, mod.UserReportModVersion, null);
            return null;
        }

        await InvokeOnDispatcherAsync(mod.SetUserReportLoading, cancellationToken, DispatcherPriority.Background)
            .ConfigureAwait(false);

        try
        {
            var etag = GetVoteEtag(_userReportEtags, "current", mod.ModId, mod.UserReportModVersion,
                installedGameVersion);

            var result = await _voteService
                .GetVoteSummaryIfChangedAsync(
                    mod.ModId,
                    mod.UserReportModVersion!,
                    installedGameVersion,
                    etag,
                    cancellationToken)
                .ConfigureAwait(false);

            var summary = result.Summary ?? mod.UserReportSummary;

            if (!result.IsNotModified || mod.UserReportSummary is null)
                if (summary is not null)
                    await InvokeOnDispatcherAsync(
                            () => mod.ApplyUserReportSummary(summary),
                            cancellationToken,
                            DispatcherPriority.Background)
                        .ConfigureAwait(false);

            StoreUserReportEtag(mod.ModId, mod.UserReportModVersion, result.ETag);

            return summary;
        }
        catch (InternetAccessDisabledException)
        {
            await InvokeOnDispatcherAsync(mod.SetUserReportOffline, cancellationToken, DispatcherPriority.Background)
                .ConfigureAwait(false);
            StoreUserReportEtag(mod.ModId, mod.UserReportModVersion, null);
            return null;
        }
        catch (Exception ex)
        {
            if (!suppressErrors)
                StatusLogService.AppendStatus(
                    string.Format(CultureInfo.CurrentCulture, "Failed to refresh user reports for {0}: {1}",
                        mod.DisplayName, ex.Message),
                    true);

            await InvokeOnDispatcherAsync(
                    () => mod.SetUserReportError(ex.Message),
                    cancellationToken,
                    DispatcherPriority.Background)
                .ConfigureAwait(false);

            if (suppressErrors) return null;

            throw;
        }
    }

    private async Task<ModVersionVoteSummary?> RunUserReportOperationAsync(
        Func<CancellationToken, Task<ModVersionVoteSummary?>> operation,
        CancellationToken cancellationToken)
    {
        using var userReportScope = BeginUserReportOperation();
        var entered = false;

        try
        {
            await _userReportRefreshLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;

            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (entered) _userReportRefreshLimiter.Release();
        }
    }

    private IDisposable BeginUserReportOperation()
    {
        lock (_userReportOperationLock)
        {
            _activeUserReportOperations++;
        }

        var busyScope = _beginBusyScope();
        return new UserReportOperationScope(this, busyScope);
    }

    private void EndUserReportOperation()
    {
        lock (_userReportOperationLock)
        {
            if (_activeUserReportOperations > 0) _activeUserReportOperations--;
        }
    }

    /// <summary>
    ///     Writes the etag observed for a mod's currently-installed-version user report.
    /// </summary>
    internal void StoreUserReportEtag(string modId, string? modVersion, string? etag)
    {
        UpdateVoteEtag(_userReportEtags, "current", modId, modVersion, etag);
    }

    /// <summary>
    ///     Writes the etag observed for a mod's latest-release user report.
    /// </summary>
    internal void StoreLatestReleaseUserReportEtag(string modId, string? modVersion, string? etag)
    {
        UpdateVoteEtag(_latestReleaseUserReportEtags, "latest", modId, modVersion, etag);
    }

    private string? GetVoteEtag(
        Dictionary<string, string> source,
        string prefix,
        string modId,
        string? modVersion,
        string? installedGameVersion)
    {
        if (string.IsNullOrWhiteSpace(modId)
            || string.IsNullOrWhiteSpace(modVersion)
            || string.IsNullOrWhiteSpace(installedGameVersion))
            return null;

        var key = BuildVoteEtagKey(prefix, modId, modVersion, installedGameVersion);
        return source.TryGetValue(key, out var etag) ? etag : null;
    }

    private void UpdateVoteEtag(
        Dictionary<string, string> target,
        string prefix,
        string modId,
        string? modVersion,
        string? etag)
    {
        var installedGameVersion = _installedGameVersionProvider();

        if (string.IsNullOrWhiteSpace(modId)
            || string.IsNullOrWhiteSpace(modVersion)
            || string.IsNullOrWhiteSpace(installedGameVersion))
            return;

        var key = BuildVoteEtagKey(prefix, modId, modVersion, installedGameVersion);
        lock (_voteEtagPersistenceLock)
        {
            var changed = false;

            if (string.IsNullOrEmpty(etag))
            {
                changed = target.Remove(key);
            }
            else if (!target.TryGetValue(key, out var existing)
                     || !string.Equals(existing, etag, StringComparison.Ordinal))
            {
                target[key] = etag;
                changed = true;
            }

            if (changed) PersistVoteEtagsLocked();
        }
    }

    /// <summary>
    ///     Builds the composite dictionary key used by both etag caches. Exposed so
    ///     <see cref="ModUpdatePollingService" /> can look up etags with the exact same key shape without
    ///     duplicating the format string.
    /// </summary>
    internal static string BuildVoteEtagKey(string prefix, string modId, string modVersion, string? gameVersion)
    {
        return string.Concat(prefix, '|', modId, '|', modVersion, '|', gameVersion ?? string.Empty);
    }

    private void LoadVoteEtagsFromDisk()
    {
        lock (_voteEtagPersistenceLock)
        {
            try
            {
                if (!File.Exists(_voteEtagCachePath)) return;

                using var stream = File.OpenRead(_voteEtagCachePath);
                var state = JsonSerializer.Deserialize<VoteEtagCacheState>(stream);

                _userReportEtags.Clear();
                _latestReleaseUserReportEtags.Clear();

                if (state?.Current is { } current)
                    foreach (var entry in current)
                        _userReportEtags[entry.Key] = entry.Value;

                if (state?.Latest is { } latest)
                    foreach (var entry in latest)
                        _latestReleaseUserReportEtags[entry.Key] = entry.Value;
            }
            catch
            {
                // Ignore cache load failures; fall back to empty in-memory cache.
            }
        }
    }

    private void PersistVoteEtagsLocked()
    {
        try
        {
            var directory = Path.GetDirectoryName(_voteEtagCachePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var state = new VoteEtagCacheState
            {
                Current = new Dictionary<string, string>(_userReportEtags, StringComparer.OrdinalIgnoreCase),
                Latest = new Dictionary<string, string>(_latestReleaseUserReportEtags, StringComparer.OrdinalIgnoreCase)
            };

            using var stream = File.Create(_voteEtagCachePath);
            JsonSerializer.Serialize(stream, state);
        }
        catch
        {
            // Persistence errors are non-fatal; ignore them.
        }
    }

    private sealed class VoteEtagCacheState
    {
        public Dictionary<string, string>? Current { get; set; }

        public Dictionary<string, string>? Latest { get; set; }
    }

    private sealed class UserReportOperationScope : IDisposable
    {
        private readonly IDisposable _busyScope;
        private readonly UserReportsCoordinator _owner;
        private bool _disposed;

        public UserReportOperationScope(UserReportsCoordinator owner, IDisposable busyScope)
        {
            _owner = owner;
            _busyScope = busyScope;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _busyScope.Dispose();
            _owner.EndUserReportOperation();
        }
    }

    private static Task InvokeOnDispatcherAsync(Action action, CancellationToken cancellationToken,
        DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (cancellationToken.IsCancellationRequested) return Task.CompletedTask;

        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, priority, cancellationToken).Task;
        }

        action();
        return Task.CompletedTask;
    }
}
