using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistBackupCoordinatorTests : IDisposable
{
    private readonly string _backupDirectory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_backupDirectory)) Directory.Delete(_backupDirectory, true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private ModlistBackupCoordinator CreateCoordinator() =>
        new(() => _backupDirectory);

    [Fact]
    public async Task CreateBackupAsync_WritesOneJsonFileToBackupDirectory()
    {
        var coordinator = CreateCoordinator();

        await coordinator.CreateBackupAsync(
            "Manual",
            "Backup",
            pruneAutomaticBackups: false,
            pruneAppStartedBackups: false,
            modStates: Array.Empty<ModPresetModState>(),
            modCount: 0,
            includedConfigurations: null,
            gameVersion: null);

        var files = Directory.GetFiles(_backupDirectory, "*.json");
        Assert.Single(files);
    }

    [Fact]
    public async Task CreateBackupAsync_TwoCallsInARow_ProduceTwoDistinctFiles()
    {
        // The backup file name is timestamped to second precision (pre-existing behavior, unchanged
        // by this move) - two calls within the same second collide on file name. Cross a second
        // boundary between calls so this test reflects real usage instead of a sub-millisecond race.
        var coordinator = CreateCoordinator();

        await coordinator.CreateBackupAsync("Manual", "Backup", false, false, Array.Empty<ModPresetModState>(), 0, null, null);
        await Task.Delay(1100);
        await coordinator.CreateBackupAsync("Manual", "Backup", false, false, Array.Empty<ModPresetModState>(), 0, null, null);

        var files = Directory.GetFiles(_backupDirectory, "*.json");
        Assert.Equal(2, files.Length);
    }
}
