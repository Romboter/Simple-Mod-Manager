using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class BusyStateTrackerTests
{
    /// <summary>
    ///     Synchronous scheduler for tests: invokes the release callback immediately instead of
    ///     waiting on a real timer/dispatcher, so the delayed-release path is deterministic.
    /// </summary>
    private static void SynchronousSchedule(TimeSpan delay, CancellationToken token, Action onElapsed)
    {
        onElapsed();
    }

    [Fact]
    public void BeginScope_RaisesBusyChangedTrue_OnFirstScope()
    {
        var tracker = new BusyStateTracker(TimeSpan.Zero, SynchronousSchedule);
        var events = new List<bool>();
        tracker.BusyChanged += events.Add;

        using var scope = tracker.BeginScope();

        Assert.Equal(new[] { true }, events);
    }

    [Fact]
    public void NestedScopes_StayBusy_UntilLastDisposes()
    {
        var tracker = new BusyStateTracker(TimeSpan.Zero, SynchronousSchedule);
        var events = new List<bool>();
        tracker.BusyChanged += events.Add;

        var outer = tracker.BeginScope();
        var inner = tracker.BeginScope();

        // Two BeginScope calls -> two "true" raises (level, not edge), matching original semantics.
        Assert.Equal(new[] { true, true }, events);

        inner.Dispose();

        // Still one scope open (outer) -> stays busy. The original mechanism re-raises "true"
        // (a level, not an edge) whenever a scope ends but the count is still > 0.
        Assert.Equal(new[] { true, true, true }, events);

        outer.Dispose();

        // Last scope closed -> release scheduled and (with synchronous scheduler) fires immediately.
        Assert.Equal(new[] { true, true, true, false }, events);
    }

    [Fact]
    public void DoubleDispose_OfSameScope_IsSafe()
    {
        var tracker = new BusyStateTracker(TimeSpan.Zero, SynchronousSchedule);
        var events = new List<bool>();
        tracker.BusyChanged += events.Add;

        var scope = tracker.BeginScope();
        scope.Dispose();
        scope.Dispose();

        // Only one release should have been raised despite two Dispose() calls.
        Assert.Equal(new[] { true, false }, events);
    }

    [Fact]
    public void DelayedRelease_UsesSuppliedScheduler()
    {
        var scheduleCalls = 0;
        Action? capturedCallback = null;

        void CapturingSchedule(TimeSpan delay, CancellationToken token, Action onElapsed)
        {
            scheduleCalls++;
            capturedCallback = onElapsed;
        }

        var tracker = new BusyStateTracker(TimeSpan.FromMilliseconds(600), CapturingSchedule);
        var events = new List<bool>();
        tracker.BusyChanged += events.Add;

        var scope = tracker.BeginScope();
        scope.Dispose();

        // Release should have been scheduled, not applied immediately.
        Assert.Equal(1, scheduleCalls);
        Assert.Equal(new[] { true }, events);

        // Simulate the timer elapsing.
        capturedCallback!.Invoke();

        Assert.Equal(new[] { true, false }, events);
    }

    [Fact]
    public void ReopeningScope_BeforeReleaseFires_CancelsThePendingRelease()
    {
        Action? capturedCallback = null;
        var cancelledTokens = new List<bool>();

        void CapturingSchedule(TimeSpan delay, CancellationToken token, Action onElapsed)
        {
            capturedCallback = onElapsed;
            token.Register(() => cancelledTokens.Add(true));
        }

        var tracker = new BusyStateTracker(TimeSpan.FromMilliseconds(600), CapturingSchedule);
        var events = new List<bool>();
        tracker.BusyChanged += events.Add;

        var scope = tracker.BeginScope();
        scope.Dispose(); // schedules a release

        // Re-enter before the release fires.
        using var secondScope = tracker.BeginScope();

        Assert.Single(cancelledTokens);
        Assert.Equal(new[] { true, true }, events);

        // If the original (cancelled) callback still fires somehow, it must be a no-op.
        capturedCallback!.Invoke();
        Assert.Equal(new[] { true, true }, events);
    }

    [Fact]
    public void BusyChanged_OnlyFiresOnTransitionsRequestedByCallers()
    {
        var tracker = new BusyStateTracker(TimeSpan.Zero, SynchronousSchedule);
        var events = new List<bool>();
        tracker.BusyChanged += events.Add;

        // No calls yet -> no events.
        Assert.Empty(events);

        using (tracker.BeginScope())
        {
            Assert.Equal(new[] { true }, events);
        }

        Assert.Equal(new[] { true, false }, events);
    }
}
