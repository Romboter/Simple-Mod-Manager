using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class FirebaseAuthFileServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-test-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void GetDataDirectoryBackupPath_ComposesExpectedPath()
    {
        var expected = Path.Combine(_dir, "ModData", "SimpleVSManager", "firebase-auth.json");
        Assert.Equal(expected, FirebaseAuthFileService.GetDataDirectoryBackupPath(_dir));
    }

    [Fact]
    public void BackupAuthStateToDataDirectory_CopiesFileIntoCreatedBackupDirectory()
    {
        var statePath = Path.Combine(_dir, "firebase-auth-state.json");
        File.WriteAllText(statePath, "state-content");

        FirebaseAuthFileService.BackupAuthStateToDataDirectory(statePath, _dir);

        var backupPath = FirebaseAuthFileService.GetDataDirectoryBackupPath(_dir);
        Assert.True(File.Exists(backupPath));
        Assert.Equal("state-content", File.ReadAllText(backupPath));
    }

    [Fact]
    public void BackupAuthStateToDataDirectory_OverwritesExistingBackup()
    {
        var statePath = Path.Combine(_dir, "firebase-auth-state.json");
        File.WriteAllText(statePath, "new-content");

        var backupPath = FirebaseAuthFileService.GetDataDirectoryBackupPath(_dir);
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.WriteAllText(backupPath, "stale-content");

        FirebaseAuthFileService.BackupAuthStateToDataDirectory(statePath, _dir);

        Assert.Equal("new-content", File.ReadAllText(backupPath));
    }
}
