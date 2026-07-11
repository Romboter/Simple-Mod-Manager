using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModDetailsProgressTrackerTests
{
    private const string DefaultStageText = "Loaded 0 mods. Loading mod details...";

    private sealed class StubScope : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class Harness
    {
        public int ScopesCreated { get; private set; }
        public List<StubScope> CreatedScopes { get; } = new();
        public List<bool> LoadingChanges { get; } = new();
        public List<(double Progress, string Text)> ProgressReports { get; } = new();
        public int LoadingStatusRequests { get; private set; }
        public int ReadyStatusRequests { get; private set; }
        public bool StatusActive { get; set; }

        public ModDetailsProgressTracker CreateTracker()
        {
            return new ModDetailsProgressTracker(
                BeginBusyScope,
                () => DefaultStageText,
                () => StatusActive,
                isLoading => LoadingChanges.Add(isLoading),
                (progress, text) => ProgressReports.Add((progress, text)),
                () => LoadingStatusRequests++,
                () => ReadyStatusRequests++);
        }

        private IDisposable BeginBusyScope()
        {
            ScopesCreated++;
            var scope = new StubScope();
            CreatedScopes.Add(scope);
            return scope;
        }
    }

    [Fact]
    public void Enqueue_FirstBatch_OpensBusyScope_SetsLoading_RequestsLoadingStatus()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        tracker.OnRefreshEnqueued(3);

        Assert.Equal(1, harness.ScopesCreated);
        Assert.Equal(new[] { true }, harness.LoadingChanges);
        Assert.Equal(1, harness.LoadingStatusRequests);
        Assert.True(tracker.IsRefreshPending);
    }

    [Fact]
    public void Enqueue_SecondBatch_WhileStatusActive_NoDuplicateStatusRequest()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        harness.StatusActive = false;
        tracker.OnRefreshEnqueued(2);

        harness.StatusActive = true;
        tracker.OnRefreshEnqueued(2);

        Assert.Equal(1, harness.LoadingStatusRequests);
        Assert.Equal(1, harness.ScopesCreated);
    }

    [Fact]
    public void Enqueue_NonPositiveCount_NoOp()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        tracker.OnRefreshEnqueued(0);
        tracker.OnRefreshEnqueued(-1);

        Assert.Equal(0, harness.ScopesCreated);
        Assert.Empty(harness.LoadingChanges);
        Assert.Empty(harness.ProgressReports);
        Assert.Equal(0, harness.LoadingStatusRequests);
        Assert.False(tracker.IsRefreshPending);
    }

    [Fact]
    public void Completed_ReportsClampedPercentage()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        tracker.OnRefreshEnqueued(4);
        tracker.OnRefreshCompleted(1);

        var last = harness.ProgressReports[^1];
        Assert.Equal(25.0, last.Progress);
        Assert.EndsWith("(1/4)", last.Text);

        tracker.OnRefreshCompleted(1);

        last = harness.ProgressReports[^1];
        Assert.Equal(50.0, last.Progress);
        Assert.EndsWith("(2/4)", last.Text);
    }

    [Fact]
    public void Completed_ToZero_ClosesScope_ClearsLoading_ResetsProgress()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();
        harness.StatusActive = true;

        tracker.OnRefreshEnqueued(2);
        tracker.OnRefreshCompleted(2);

        Assert.Single(harness.CreatedScopes);
        Assert.Equal(1, harness.CreatedScopes[0].DisposeCount);
        Assert.False(harness.LoadingChanges[^1]);
        Assert.Equal(1, harness.ReadyStatusRequests);
        Assert.Equal((0, string.Empty), harness.ProgressReports[^1]);
        Assert.False(tracker.IsRefreshPending);
    }

    [Fact]
    public void Completed_MoreThanPending_ClampsAtZero()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        tracker.OnRefreshEnqueued(1);
        tracker.OnRefreshCompleted(5);

        Assert.False(tracker.IsRefreshPending);
        Assert.Single(harness.CreatedScopes);
        Assert.Equal(1, harness.CreatedScopes[0].DisposeCount);

        // A fresh cycle starts cleanly after the clamp.
        tracker.OnRefreshEnqueued(2);

        Assert.Equal(2, harness.CreatedScopes.Count);
        Assert.Equal(0, harness.CreatedScopes[1].DisposeCount);
    }

    [Fact]
    public void Enqueue_WithStageText_UsesItInProgressText()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        tracker.OnRefreshEnqueued(3, "Checking for mod updates...");
        tracker.OnRefreshCompleted(1);

        var reportWithStage = harness.ProgressReports[^1];
        Assert.StartsWith("Checking for mod updates...", reportWithStage.Text);

        // A later enqueue without text keeps the existing stage.
        tracker.OnRefreshEnqueued(2);

        var reportAfterFollowUp = harness.ProgressReports[^1];
        Assert.StartsWith("Checking for mod updates...", reportAfterFollowUp.Text);
    }

    [Fact]
    public void ReleaseBusyScope_Idempotent()
    {
        var harness = new Harness();
        var tracker = harness.CreateTracker();

        tracker.ReleaseBusyScope();
        tracker.ReleaseBusyScope();

        tracker.OnRefreshEnqueued(1);
        tracker.OnRefreshCompleted(1);

        Assert.Equal(1, harness.CreatedScopes[0].DisposeCount);

        tracker.ReleaseBusyScope();

        Assert.Equal(1, harness.CreatedScopes[0].DisposeCount);
    }
}
