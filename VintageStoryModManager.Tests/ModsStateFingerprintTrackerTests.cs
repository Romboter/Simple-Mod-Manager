using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModsStateFingerprintTrackerTests
{
    private sealed class Harness
    {
        public string? Fingerprint;
        public bool Watching;
        public bool Throw;
        public readonly ModsStateFingerprintTracker Tracker;

        public Harness()
        {
            Tracker = new ModsStateFingerprintTracker(
                () =>
                {
                    if (Throw) throw new InvalidOperationException("capture failed");
                    return Fingerprint;
                },
                () => Watching);
        }
    }

    [Fact]
    public async Task FirstCapture_Baselines_ReturnsFalse()
    {
        var harness = new Harness { Fingerprint = "A" };

        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());
        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());
    }

    [Fact]
    public async Task ChangedFingerprint_ReturnsTrue_AndRebaselines()
    {
        var harness = new Harness { Fingerprint = "A" };

        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());

        harness.Fingerprint = "B";
        Assert.True(await harness.Tracker.HasFingerprintChangedAsync());

        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());
    }

    [Fact]
    public async Task NullCapture_ReturnsFalse_KeepsBaseline()
    {
        var harness = new Harness { Fingerprint = "A" };

        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());

        harness.Fingerprint = null;
        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());

        harness.Fingerprint = "B";
        Assert.True(await harness.Tracker.HasFingerprintChangedAsync());
    }

    [Fact]
    public async Task CaptureThrows_ReturnsFalse()
    {
        var harness = new Harness { Throw = true };

        var result = await Record.ExceptionAsync(async () =>
            Assert.False(await harness.Tracker.HasFingerprintChangedAsync()));

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshSnapshot_WhileWatching_ClearsBaseline()
    {
        var harness = new Harness { Fingerprint = "A" };

        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());

        harness.Watching = true;
        await harness.Tracker.RefreshSnapshotAsync();
        harness.Watching = false;

        Assert.False(await harness.Tracker.HasFingerprintChangedAsync());
    }
}
