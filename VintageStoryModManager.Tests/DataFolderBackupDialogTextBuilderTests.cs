using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class DataFolderBackupDialogTextBuilderTests
{
    [Fact]
    public void RestoreConfirmationMessage_MatchesExistingCopy()
    {
        Assert.Equal(
            "Restoring a VintagestoryData backup replaces the entire folder (the Cache folder will be cleared). Continue?",
            DataFolderBackupDialogTextBuilder.RestoreConfirmationMessage);
    }

    [Fact]
    public void BuildDeleteConfirmation_IncludesVersion()
    {
        Assert.Equal(
            "Delete all VintagestoryData backups for Vintage Story 1.20.4? This action cannot be undone.",
            DataFolderBackupDialogTextBuilder.BuildDeleteConfirmation("1.20.4"));
    }

    [Theory]
    [InlineData(1, "Deleted 1 VintagestoryData backup.")]
    [InlineData(2, "Deleted 2 VintagestoryData backups.")]
    public void BuildDeletedStatusMessage_FormatsCount(int deletedCount, string expected)
    {
        Assert.Equal(
            expected,
            DataFolderBackupDialogTextBuilder.BuildDeletedStatusMessage(deletedCount));
    }

    [Fact]
    public void BuildDeleteFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to delete the VintagestoryData backups:\ndenied",
            DataFolderBackupDialogTextBuilder.BuildDeleteFailureMessage("denied"));
    }

    [Fact]
    public void BuildVersionMismatchMessage_IncludesBothVersions()
    {
        Assert.Equal(
            "This backup was created for Vintage Story 1.19.8, but the installed version is 1.20.4. Install the matching Vintage Story version before restoring this backup.",
            DataFolderBackupDialogTextBuilder.BuildVersionMismatchMessage("1.19.8", "1.20.4"));
    }

    [Fact]
    public void BuildRestoreFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to restore the selected VintagestoryData backup:\nlocked",
            DataFolderBackupDialogTextBuilder.BuildRestoreFailureMessage("locked"));
    }

    [Fact]
    public void FixedMessages_MatchExistingCopy()
    {
        Assert.Equal(
            "The VintagestoryData folder is not available. Please set it before restoring a backup.",
            DataFolderBackupDialogTextBuilder.DataDirectoryUnavailableForRestoreMessage);

        Assert.Equal(
            "Set the VintagestoryData folder before deleting backups.",
            DataFolderBackupDialogTextBuilder.SetDataDirectoryBeforeDeleteMessage);

        Assert.Equal(
            "The installed Vintage Story version could not be determined, so backups cannot be deleted safely.",
            DataFolderBackupDialogTextBuilder.UnknownInstalledVersionDeleteMessage);

        Assert.Equal(
            "No backups matching the current data folder and Vintage Story version were found.",
            DataFolderBackupDialogTextBuilder.NoMatchingBackupsMessage);

        Assert.Equal(
            "This backup was created for a different VintagestoryData folder and cannot be restored.",
            DataFolderBackupDialogTextBuilder.DifferentDataFolderRestoreMessage);

        Assert.Equal(
            "VintagestoryData was restored from the selected backup.",
            DataFolderBackupDialogTextBuilder.RestoreSuccessMessage);
    }
}
