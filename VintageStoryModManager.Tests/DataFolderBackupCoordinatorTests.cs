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

    [Fact]
    public void ResolveInstalledVersionForDelete_NoInstalledVersion_ReturnsNulls()
    {
        // VintageStoryVersionLocator also probes the VINTAGE_STORY environment variable, which can
        // point at a real install on a dev machine. Scope-clear it for this test only so the result
        // is deterministic regardless of the host environment.
        var previousVintageStoryEnv = Environment.GetEnvironmentVariable("VINTAGE_STORY");
        Environment.SetEnvironmentVariable("VINTAGE_STORY", null);
        try
        {
            var coordinator = new DataFolderBackupCoordinator(new DataBackupService(Path.GetTempPath()));

            var (display, normalized) = coordinator.ResolveInstalledVersionForDelete("C:/definitely-not-a-vs-install");

            Assert.Null(display);
            Assert.Null(normalized);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VINTAGE_STORY", previousVintageStoryEnv);
        }
    }

    [Fact]
    public void GetBackupsForRestoreMenu_NullDataDirectory_ReturnsEmpty()
    {
        var coordinator = new DataFolderBackupCoordinator(new DataBackupService(Path.GetTempPath()));

        var result = coordinator.GetBackupsForRestoreMenu(null, maxItems: 15);

        Assert.Empty(result.Displayed);
        Assert.Equal(0, result.TotalMatching);
    }

    [Fact]
    public void GetBackupsForRestoreMenu_NoBackupsOnDisk_ReturnsEmpty()
    {
        var tempConfigDir = Directory.CreateTempSubdirectory().FullName;
        var coordinator = new DataFolderBackupCoordinator(new DataBackupService(tempConfigDir));

        var result = coordinator.GetBackupsForRestoreMenu("C:/some-data-dir", maxItems: 15);

        Assert.Empty(result.Displayed);
        Assert.Equal(0, result.TotalMatching);

        Directory.Delete(tempConfigDir, true);
    }
}
