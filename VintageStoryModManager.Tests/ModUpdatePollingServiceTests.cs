using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModUpdatePollingServiceTests
{
    private static ModUpdatePollingService CreateService(
        Func<bool>? isAutoRefreshDisabled = null,
        Func<CancellationToken, Task<List<ModEntry>>>? snapshotProvider = null,
        Func<string, CancellationToken, Task<string?>>? fetchLatestReleaseVersionAsync = null,
        Action<IReadOnlyList<ModEntry>>? onUpdateCandidates = null,
        Action<bool>? onInProgressChanged = null)
    {
        return new ModUpdatePollingService(
            TimeSpan.FromMinutes(2),
            isAutoRefreshDisabled ?? (() => false),
            snapshotProvider ?? (_ => Task.FromResult(new List<ModEntry>())),
            fetchLatestReleaseVersionAsync ?? ((_, _) => Task.FromResult<string?>(null)),
            onUpdateCandidates ?? (_ => { }),
            onInProgressChanged ?? (_ => { }));
    }

    [Theory]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.2.0", "1.3.0", true)]
    [InlineData("1.2.0", null, false)]
    [InlineData("1.2.0", "", false)]
    [InlineData("1.2.0", "v1.2.0", false)]
    [InlineData(null, "1.2.0", false)]
    public void IsDifferentVersion_MatchesExpectedContract(string? installed, string? latest, bool expected)
    {
        Assert.Equal(expected, ModUpdatePollingService.IsDifferentVersion(installed, latest));
    }

    [Fact]
    public void FastCheck_WithAutoRefreshDisabled_NeverInvokesSnapshotProvider()
    {
        var snapshotCalls = 0;

        var service = CreateService(
            isAutoRefreshDisabled: () => true,
            snapshotProvider: ct =>
            {
                Interlocked.Increment(ref snapshotCalls);
                return Task.FromResult(new List<ModEntry>());
            });

        service.FastCheck();

        Assert.Equal(0, snapshotCalls);

        service.Dispose();
    }

    [Fact]
    public async Task FastCheck_WithEmptySnapshot_ReportsInProgressThenNotInProgress_AndSkipsUpdateCandidates()
    {
        var progressEvents = new List<bool>();
        var updateCandidatesCalled = false;
        using var doneSignal = new SemaphoreSlim(0);

        var service = CreateService(
            snapshotProvider: _ => Task.FromResult(new List<ModEntry>()),
            onUpdateCandidates: _ => updateCandidatesCalled = true,
            onInProgressChanged: inProgress =>
            {
                lock (progressEvents) progressEvents.Add(inProgress);
                if (!inProgress) doneSignal.Release();
            });

        service.FastCheck();

        var completed = await doneSignal.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(completed);
        Assert.False(updateCandidatesCalled);
        Assert.Equal(new[] { true, false }, progressEvents);

        service.Dispose();
    }

    [Fact]
    public async Task FastCheck_WithUpdatedEntry_InvokesOnUpdateCandidatesWithThatEntry()
    {
        var entry = new ModEntry { ModId = "mod1", Name = "Mod One", Version = "1.0.0" };
        IReadOnlyList<ModEntry>? receivedCandidates = null;
        using var doneSignal = new SemaphoreSlim(0);

        var service = CreateService(
            snapshotProvider: _ => Task.FromResult(new List<ModEntry> { entry }),
            fetchLatestReleaseVersionAsync: (modId, ct) => Task.FromResult<string?>("2.0.0"),
            onUpdateCandidates: candidates => receivedCandidates = candidates,
            onInProgressChanged: inProgress =>
            {
                if (!inProgress) doneSignal.Release();
            });

        service.FastCheck();

        var completed = await doneSignal.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(completed);
        Assert.NotNull(receivedCandidates);
        Assert.Single(receivedCandidates!);
        Assert.Same(entry, receivedCandidates![0]);

        service.Dispose();
    }

    [Fact]
    public async Task FastCheck_WithTwoEntriesSharingModId_InvokesFetchDelegateOnlyOnce()
    {
        var entryA = new ModEntry { ModId = "dupmod", Name = "Dup A", Version = "1.0.0" };
        var entryB = new ModEntry { ModId = "dupmod", Name = "Dup B", Version = "1.0.0" };
        var fetchCallCount = 0;
        using var doneSignal = new SemaphoreSlim(0);

        var service = CreateService(
            snapshotProvider: _ => Task.FromResult(new List<ModEntry> { entryA, entryB }),
            fetchLatestReleaseVersionAsync: (modId, ct) =>
            {
                Interlocked.Increment(ref fetchCallCount);
                return Task.FromResult<string?>(null);
            },
            onInProgressChanged: inProgress =>
            {
                if (!inProgress) doneSignal.Release();
            });

        service.FastCheck();

        var completed = await doneSignal.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(completed);
        Assert.Equal(1, fetchCallCount);

        service.Dispose();
    }
}
