using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class DataFolderBackupCoordinatorTests
{
    private static DataBackupSummary MakeSummary(string sourceDataDirectory, string? version) =>
        new("id-1", DateTime.UtcNow, "C:/backups/id-1", sourceDataDirectory, version);

    [Fact]
    public void ValidateRestore_DataDirectoryMissing_ReturnsDataDirectoryUnavailable()
    {
        var coordinator = new DataFolderBackupCoordinator(new DataBackupService(Path.GetTempPath()));
        var summary = MakeSummary("C:/data", "1.20.0");

        var result = coordinator.ValidateRestore(summary, null, "C:/game");

        Assert.Equal(DataBackupRestoreValidation.DataDirectoryUnavailable, result.Result);
    }

    [Fact]
    public void ValidateRestore_DifferentSourceDirectory_ReturnsDifferentDataFolder()
    {
        var coordinator = new DataFolderBackupCoordinator(new DataBackupService(Path.GetTempPath()));
        var dataDir = Directory.CreateTempSubdirectory().FullName;
        var summary = MakeSummary("C:/some-other-data-dir", "1.20.0");

        var result = coordinator.ValidateRestore(summary, dataDir, "C:/game");

        Assert.Equal(DataBackupRestoreValidation.DifferentDataFolder, result.Result);

        Directory.Delete(dataDir, true);
    }

    [Fact]
    public void ValidateRestore_SameDirectoryNoVersionRecorded_ReturnsOk()
    {
        var coordinator = new DataFolderBackupCoordinator(new DataBackupService(Path.GetTempPath()));
        var dataDir = Directory.CreateTempSubdirectory().FullName;
        var summary = MakeSummary(dataDir, null);

        var result = coordinator.ValidateRestore(summary, dataDir, "C:/nonexistent-game-dir");

        Assert.Equal(DataBackupRestoreValidation.Ok, result.Result);

        Directory.Delete(dataDir, true);
    }
}
