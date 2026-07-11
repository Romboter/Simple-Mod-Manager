using VintageStoryModManager.Models;
using Timer = System.Threading.Timer;

namespace VintageStoryModManager.Services;

/// <summary>
///     Owns the periodic "FastCheck" update-polling timer/re-entrancy machine: on a fixed interval (or
///     on demand), snapshots the currently-known mods, asks the database service whether newer releases
///     exist for each, and reports back the subset that changed.
/// </summary>
public sealed class ModUpdatePollingService : IDisposable
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(2);

    private readonly TimeSpan _interval;
    private readonly Func<bool> _isAutoRefreshDisabled;
    private readonly Func<CancellationToken, Task<List<ModEntry>>> _snapshotProvider;
    private readonly Func<string, CancellationToken, Task<string?>> _fetchLatestReleaseVersionAsync;
    private readonly Action<IReadOnlyList<ModEntry>> _onUpdateCandidates;
    private readonly Action<bool> _onInProgressChanged;

    private readonly object _fastCheckTimerLock = new();
    private Timer? _fastCheckTimer;
    private volatile bool _hasPendingFastCheck;
    private int _isFastCheckRunning;
    private bool _disposed;

    public ModUpdatePollingService(
        TimeSpan interval,
        Func<bool> isAutoRefreshDisabled,
        Func<CancellationToken, Task<List<ModEntry>>> snapshotProvider,
        Func<string, CancellationToken, Task<string?>> fetchLatestReleaseVersionAsync,
        Action<IReadOnlyList<ModEntry>> onUpdateCandidates,
        Action<bool> onInProgressChanged)
    {
        ArgumentNullException.ThrowIfNull(isAutoRefreshDisabled);
        ArgumentNullException.ThrowIfNull(snapshotProvider);
        ArgumentNullException.ThrowIfNull(fetchLatestReleaseVersionAsync);
        ArgumentNullException.ThrowIfNull(onUpdateCandidates);
        ArgumentNullException.ThrowIfNull(onInProgressChanged);

        _interval = interval;
        _isAutoRefreshDisabled = isAutoRefreshDisabled;
        _snapshotProvider = snapshotProvider;
        _fetchLatestReleaseVersionAsync = fetchLatestReleaseVersionAsync;
        _onUpdateCandidates = onUpdateCandidates;
        _onInProgressChanged = onInProgressChanged;
    }

    public void Dispose()
    {
        _disposed = true;
        StopTimer();

        lock (_fastCheckTimerLock)
        {
            _fastCheckTimer?.Dispose();
            _fastCheckTimer = null;
        }
    }

    public void FastCheck()
    {
        ResetTimer();

        if (_isAutoRefreshDisabled()) return;

        if (InternetAccessManager.IsInternetAccessDisabled) return;

        _hasPendingFastCheck = true;

        if (Interlocked.CompareExchange(ref _isFastCheckRunning, 1, 0) == 0) _ = Task.Run(RunFastCheckAsync);
    }

    private async Task RunFastCheckAsync()
    {
        try
        {
            _onInProgressChanged(true);

            while (_hasPendingFastCheck)
            {
                _hasPendingFastCheck = false;

                if (InternetAccessManager.IsInternetAccessDisabled) break;

                var updateCandidates = await CheckForNewModReleasesAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                if (updateCandidates.Count > 0) _onUpdateCandidates(updateCandidates);
            }
        }
        catch
        {
            // Swallow failures to ensure subsequent checks can continue.
        }
        finally
        {
            _onInProgressChanged(false);

            Interlocked.Exchange(ref _isFastCheckRunning, 0);

            if (_hasPendingFastCheck && !InternetAccessManager.IsInternetAccessDisabled) FastCheck();
        }
    }

    public void ResetTimer()
    {
        if (_disposed || _isAutoRefreshDisabled()) return;

        lock (_fastCheckTimerLock)
        {
            _fastCheckTimer ??= new Timer(OnFastCheckTimerElapsed, null, Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _fastCheckTimer.Change(_interval, Timeout.InfiniteTimeSpan);
        }
    }

    public void StopTimer()
    {
        lock (_fastCheckTimerLock)
        {
            _fastCheckTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnFastCheckTimerElapsed(object? state)
    {
        if (_disposed || _isAutoRefreshDisabled()) return;

        FastCheck();
    }

    private async Task<IReadOnlyList<ModEntry>> CheckForNewModReleasesAsync(
        CancellationToken cancellationToken)
    {
        var entries = new List<ModEntry>();

        try
        {
            entries = await _snapshotProvider(cancellationToken).ConfigureAwait(false);

            if (entries.Count == 0) return Array.Empty<ModEntry>();

            var updateCandidates = new List<ModEntry>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var modId = entry.ModId;
                if (!processed.Add(modId)) continue;

                var latestVersion = await _fetchLatestReleaseVersionAsync(modId, cancellationToken)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(latestVersion)) continue;

                if (!IsDifferentVersion(entry.Version, latestVersion)) continue;

                var knownLatest = entry.DatabaseInfo?.LatestRelease?.Version
                                  ?? entry.DatabaseInfo?.LatestVersion;

                if (string.Equals(knownLatest, latestVersion, StringComparison.OrdinalIgnoreCase)) continue;

                updateCandidates.Add(entry);
            }

            return updateCandidates.Count == 0
                ? Array.Empty<ModEntry>()
                : updateCandidates;
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<ModEntry>();
        }
        catch
        {
            return Array.Empty<ModEntry>();
        }
    }

    internal static bool IsDifferentVersion(string? installedVersion, string? latestVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion) || string.IsNullOrWhiteSpace(installedVersion)) return false;

        var normalizedInstalled = VersionStringUtility.Normalize(installedVersion);
        var normalizedLatest = VersionStringUtility.Normalize(latestVersion);

        if (!string.IsNullOrWhiteSpace(normalizedInstalled) && !string.IsNullOrWhiteSpace(normalizedLatest))
            return !string.Equals(normalizedInstalled, normalizedLatest, StringComparison.OrdinalIgnoreCase);

        return !string.Equals(installedVersion.Trim(), latestVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
