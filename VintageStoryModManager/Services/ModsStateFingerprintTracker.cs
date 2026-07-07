namespace VintageStoryModManager.Services;

/// <summary>
///     Detects out-of-band mod-state changes by fingerprinting the discovery service's view of the
///     mods folder and comparing across polls. Used only when the filesystem watcher isn't active;
///     when it is, the snapshot is cleared so the first post-watcher poll re-baselines instead of
///     reporting a spurious change.
/// </summary>
public sealed class ModsStateFingerprintTracker
{
    private readonly Func<string?> _captureFingerprint;
    private readonly Func<bool> _isWatcherActive;

    private readonly object _modsStateLock = new();
    private string? _modsStateFingerprint;

    public ModsStateFingerprintTracker(
        Func<string?> captureFingerprint,
        Func<bool> isWatcherActive)
    {
        ArgumentNullException.ThrowIfNull(captureFingerprint);
        ArgumentNullException.ThrowIfNull(isWatcherActive);

        _captureFingerprint = captureFingerprint;
        _isWatcherActive = isWatcherActive;
    }

    public async Task<bool> HasFingerprintChangedAsync()
    {
        var fingerprint = await CaptureAsync().ConfigureAwait(false);
        if (fingerprint is null) return false;

        lock (_modsStateLock)
        {
            if (_modsStateFingerprint is null)
            {
                _modsStateFingerprint = fingerprint;
                return false;
            }

            if (!string.Equals(_modsStateFingerprint, fingerprint, StringComparison.Ordinal))
            {
                _modsStateFingerprint = fingerprint;
                return true;
            }
        }

        return false;
    }

    public async Task RefreshSnapshotAsync()
    {
        if (_isWatcherActive())
        {
            lock (_modsStateLock)
            {
                _modsStateFingerprint = null;
            }

            return;
        }

        var fingerprint = await CaptureAsync().ConfigureAwait(false);
        if (fingerprint is null) return;

        lock (_modsStateLock)
        {
            _modsStateFingerprint = fingerprint;
        }
    }

    private Task<string?> CaptureAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                return _captureFingerprint();
            }
            catch (Exception)
            {
                return null;
            }
        });
    }
}
