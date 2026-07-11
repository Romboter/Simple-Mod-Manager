namespace VintageStoryModManager.Services;

/// <summary>
///     Tracks the mod-details refresh workload: a pending-operation count that opens/closes a paired
///     busy scope and a total/completed work counter that drives the details progress bar. Pure
///     counter machine — presentation (dispatcher marshalling, bound properties, status-bar text)
///     stays with the owner via the constructor delegates, mirroring <see cref="BusyStateTracker" />.
/// </summary>
public sealed class ModDetailsProgressTracker
{
    private readonly Func<IDisposable> _beginBusyScope;
    private readonly Func<string> _defaultStageText;
    private readonly Func<bool> _isModDetailsStatusActive;
    private readonly Action<bool> _isLoadingChanged;
    private readonly Action<double, string> _progressChanged;
    private readonly Action _requestLoadingStatus;
    private readonly Action _requestReadyStatus;

    private readonly object _busyScopeLock = new();
    private IDisposable? _busyScope;
    private int _pendingRefreshCount;
    private int _totalWork;
    private int _completedWork;
    private string _progressStage = string.Empty;

    public ModDetailsProgressTracker(
        Func<IDisposable> beginBusyScope,
        Func<string> defaultStageText,
        Func<bool> isModDetailsStatusActive,
        Action<bool> isLoadingChanged,
        Action<double, string> progressChanged,
        Action requestLoadingStatus,
        Action requestReadyStatus)
    {
        ArgumentNullException.ThrowIfNull(beginBusyScope);
        ArgumentNullException.ThrowIfNull(defaultStageText);
        ArgumentNullException.ThrowIfNull(isModDetailsStatusActive);
        ArgumentNullException.ThrowIfNull(isLoadingChanged);
        ArgumentNullException.ThrowIfNull(progressChanged);
        ArgumentNullException.ThrowIfNull(requestLoadingStatus);
        ArgumentNullException.ThrowIfNull(requestReadyStatus);

        _beginBusyScope = beginBusyScope;
        _defaultStageText = defaultStageText;
        _isModDetailsStatusActive = isModDetailsStatusActive;
        _isLoadingChanged = isLoadingChanged;
        _progressChanged = progressChanged;
        _requestLoadingStatus = requestLoadingStatus;
        _requestReadyStatus = requestReadyStatus;
    }

    public bool IsRefreshPending =>
        Interlocked.CompareExchange(ref _pendingRefreshCount, 0, 0) > 0;

    public void OnRefreshEnqueued(int count, string? statusText = null)
    {
        if (count <= 0) return;

        var newCount = Interlocked.Add(ref _pendingRefreshCount, count);
        if (newCount <= 0)
        {
            Interlocked.Exchange(ref _pendingRefreshCount, 0);
            return;
        }

        if (newCount == count) ResetProgress();

        EnsureBusyScope();
        _isLoadingChanged(true);

        AddWork(count, statusText);

        if (newCount == count || !_isModDetailsStatusActive())
            _requestLoadingStatus();
    }

    public void OnRefreshCompleted(int completedCount = 1)
    {
        if (completedCount <= 0) return;

        var newCount = Interlocked.Add(ref _pendingRefreshCount, -completedCount);
        if (newCount < 0)
        {
            Interlocked.Exchange(ref _pendingRefreshCount, 0);
            newCount = 0;
        }

        Interlocked.Add(ref _completedWork, completedCount);
        UpdateProgress();

        if (newCount <= 0)
        {
            Interlocked.Exchange(ref _pendingRefreshCount, 0);
            ReleaseBusyScope();
            _isLoadingChanged(false);

            if (_isModDetailsStatusActive()) _requestReadyStatus();

            ResetProgress();
        }
    }

    public void ReleaseBusyScope()
    {
        lock (_busyScopeLock)
        {
            _busyScope?.Dispose();
            _busyScope = null;
        }
    }

    private void EnsureBusyScope()
    {
        lock (_busyScopeLock)
        {
            _busyScope ??= _beginBusyScope();
        }
    }

    private void AddWork(int count, string? statusText)
    {
        if (count <= 0) return;

        Interlocked.Add(ref _totalWork, count);
        UpdateProgress(statusText);
    }

    private void UpdateProgress(string? statusText = null)
    {
        if (!string.IsNullOrWhiteSpace(statusText)) _progressStage = statusText;

        var total = Interlocked.CompareExchange(ref _totalWork, 0, 0);
        if (total <= 0)
        {
            _progressChanged(0, string.Empty);
            return;
        }

        var completed = Interlocked.CompareExchange(ref _completedWork, 0, 0);
        completed = Math.Clamp(completed, 0, total);

        var percentage = (double)completed / total * 100;

        var baseText = string.IsNullOrWhiteSpace(_progressStage)
            ? _defaultStageText()
            : _progressStage;

        _progressChanged(percentage, $"{baseText} ({completed}/{total})");
    }

    private void ResetProgress()
    {
        Interlocked.Exchange(ref _completedWork, 0);
        Interlocked.Exchange(ref _totalWork, 0);
        _progressStage = string.Empty;
        _progressChanged(0, string.Empty);
    }
}
