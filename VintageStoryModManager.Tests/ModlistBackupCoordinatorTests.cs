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

    [Fact]
    public void LoadBackupForRestore_MissingFile_ReturnsFileMissing()
    {
        var coordinator = CreateCoordinator();

        var result = coordinator.LoadBackupForRestore(Path.Combine(_backupDirectory, "does-not-exist.json"));

        Assert.True(result.FileMissing);
        Assert.Null(result.Preset);
    }

    [Fact]
    public void LoadBackupForRestore_InvalidJson_ReturnsErrorMessage()
    {
        var coordinator = CreateCoordinator();
        var path = Path.Combine(_backupDirectory, "bad.json");
        File.WriteAllText(path, "{ not valid json ");

        var result = coordinator.LoadBackupForRestore(path);

        Assert.False(result.FileMissing);
        Assert.Null(result.Preset);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void ListBackupFiles_MultipleAppStartedBackups_OnlyNewestIncluded()
    {
        var coordinator = CreateCoordinator();
        var older = Path.Combine(_backupDirectory, "old -- AppStarted (1 mod).json");
        var newer = Path.Combine(_backupDirectory, "new -- AppStarted (1 mod).json");
        File.WriteAllText(older, "{}");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-10));
        File.WriteAllText(newer, "{}");

        var files = coordinator.ListBackupFiles();

        Assert.Single(files);
        Assert.Equal(newer, files[0]);
    }

    [Fact]
    public void ListBackupFiles_NoFiles_ReturnsEmpty()
    {
        var coordinator = CreateCoordinator();

        var files = coordinator.ListBackupFiles();

        Assert.Empty(files);
    }
}
