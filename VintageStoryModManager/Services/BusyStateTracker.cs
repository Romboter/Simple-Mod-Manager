namespace VintageStoryModManager.Services;

/// <summary>
///     Ref-counted busy-scope tracker: tracks a nesting count of "busy" scopes and raises
///     <see cref="BusyChanged" /> immediately when the first scope opens, and (after a short
///     debounce delay, to avoid flicker) when the last scope closes.
/// </summary>
/// <remarks>
///     The delayed-release mechanism is pluggable via the constructor's <c>scheduleRelease</c>
///     delegate so this class stays synchronously testable: pass a delegate that invokes its
///     callback immediately to exercise the delayed-release path deterministically in tests.
///     The default schedule delegate uses <see cref="Task.Delay(TimeSpan, CancellationToken)" />,
///     matching the original production behavior.
/// </remarks>
public sealed class BusyStateTracker
{
    private readonly object _lock = new();
    private readonly TimeSpan _releaseDelay;
    private readonly Action<TimeSpan, CancellationToken, Action> _scheduleRelease;
    private int _operationCount;
    private CancellationTokenSource? _releaseCts;

    public BusyStateTracker(TimeSpan releaseDelay, Action<TimeSpan, CancellationToken, Action>? scheduleRelease = null)
    {
        _releaseDelay = releaseDelay;
        _scheduleRelease = scheduleRelease ?? DefaultScheduleRelease;
    }

    /// <summary>
    ///     Raised whenever the effective busy state transitions. May be raised more than once with
    ///     the same value (callers should treat it as a level, not an edge, exactly like the
    ///     original in-class mechanism did).
    /// </summary>
    public event Action<bool>? BusyChanged;

    public IDisposable BeginScope()
    {
        bool isBusy;
        CancellationTokenSource? pendingRelease = null;
        lock (_lock)
        {
            _operationCount++;
            isBusy = _operationCount > 0;

            if (_releaseCts is not null)
            {
                pendingRelease = _releaseCts;
                _releaseCts = null;
            }
        }

        pendingRelease?.Cancel();

        RaiseBusyChanged(isBusy);
        return new BusyScope(this);
    }

    private void EndScope()
    {
        bool isBusy;
        CancellationTokenSource? pendingRelease = null;
        CancellationTokenSource? releaseToSchedule = null;
        lock (_lock)
        {
            if (_operationCount > 0) _operationCount--;

            isBusy = _operationCount > 0;
            if (!isBusy)
            {
                pendingRelease = _releaseCts;
                releaseToSchedule = new CancellationTokenSource();
                _releaseCts = releaseToSchedule;
            }
        }

        pendingRelease?.Cancel();

        if (isBusy)
            RaiseBusyChanged(true);
        else
            ScheduleRelease(releaseToSchedule);
    }

    private void ScheduleRelease(CancellationTokenSource? releaseCts)
    {
        if (releaseCts is null)
        {
            RaiseBusyChanged(false);
            return;
        }

        var token = releaseCts.Token;
        _scheduleRelease(_releaseDelay, token, () =>
        {
            try
            {
                if (token.IsCancellationRequested) return;

                lock (_lock)
                {
                    if (!ReferenceEquals(_releaseCts, releaseCts)) return;

                    _releaseCts = null;
                }

                RaiseBusyChanged(false);
            }
            finally
            {
                releaseCts.Dispose();
            }
        });
    }

    private static void DefaultScheduleRelease(TimeSpan delay, CancellationToken token, Action onElapsed)
    {
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }

            onElapsed();
        });
    }

    private void RaiseBusyChanged(bool isBusy)
    {
        BusyChanged?.Invoke(isBusy);
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly BusyStateTracker _owner;
        private bool _disposed;

        public BusyScope(BusyStateTracker owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _owner.EndScope();
        }
    }
}
