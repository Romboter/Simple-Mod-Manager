using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ManagerFolderDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "The manager data folder is not available on this system.",
            ManagerFolderDialogTextBuilder.ManagerDataFolderUnavailableMessage);
        Assert.Equal(
            "Cannot determine the current manager data folder location.",
            ManagerFolderDialogTextBuilder.CannotDetermineCurrentManagerFolderMessage);
        Assert.Equal(
            "Cannot determine the default manager data folder location.",
            ManagerFolderDialogTextBuilder.CannotDetermineDefaultManagerFolderMessage);
        Assert.Equal(
            "The selected location is the same as the current location.",
            ManagerFolderDialogTextBuilder.SameLocationMessage);
    }

    [Fact]
    public void BuildOpenManagerFolderFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to open the manager data folder:\ndenied",
            ManagerFolderDialogTextBuilder.BuildOpenManagerFolderFailureMessage("denied"));
    }

    [Fact]
    public void BuildExistingFolderPrompt_FormatsFolder()
    {
        Assert.Equal(
            "The folder \"C:\\SVSM\" already exists.\n\n" +
            "Do you want to merge with the existing folder?\n" +
            "(Existing files with the same name will be overwritten)",
            ManagerFolderDialogTextBuilder.BuildExistingFolderPrompt(@"C:\SVSM"));
    }

    [Fact]
    public void BuildMoveConfirmationMessage_FormatsFolders()
    {
        Assert.Equal(
            "Move manager folder from:\nC:\\Old\n\n" +
            "To:\nC:\\New\n\n" +
            "The application will restart after the move is complete.\n\n" +
            "Continue?",
            ManagerFolderDialogTextBuilder.BuildMoveConfirmationMessage(
                @"C:\Old",
                @"C:\New"));
    }

    [Fact]
    public void BuildMoveSuccessMessage_FormatsFolder()
    {
        Assert.Equal(
            "Manager folder moved successfully to:\nC:\\New\n\n" +
            "The application will now restart.",
            ManagerFolderDialogTextBuilder.BuildMoveSuccessMessage(@"C:\New"));
    }

    [Fact]
    public void BuildMoveFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to move the manager folder:\n\nlocked",
            ManagerFolderDialogTextBuilder.BuildMoveFailureMessage("locked"));
    }

    [Fact]
    public void BuildAlreadyDefaultMessage_FormatsFolder()
    {
        Assert.Equal(
            "Already using the default manager folder location:\nC:\\Default",
            ManagerFolderDialogTextBuilder.BuildAlreadyDefaultMessage(@"C:\Default"));
    }

    [Fact]
    public void BuildResetConfirmationMessage_FormatsFolders()
    {
        Assert.Equal(
            "Reset manager folder to default location?\n\n" +
            "Current location:\nC:\\Custom\n\n" +
            "Default location:\nC:\\Default\n\n" +
            "The manager will:\n" +
            "• Move all configuration files, cached mods, backups, and presets\n" +
            "• Update the configuration to use the default location\n" +
            "• Require a restart to complete the change\n\n" +
            "Note: The Firebase authentication backup (SVSM Backup folder) will remain in its original location.\n\n" +
            "Continue?",
            ManagerFolderDialogTextBuilder.BuildResetConfirmationMessage(
                @"C:\Custom",
                @"C:\Default"));
    }

    [Fact]
    public void BuildResetSuccessMessage_FormatsFolder()
    {
        Assert.Equal(
            "Manager folder reset to default location:\nC:\\Default\n\n" +
            "The application will now restart.",
            ManagerFolderDialogTextBuilder.BuildResetSuccessMessage(@"C:\Default"));
    }

    [Fact]
    public void BuildResetFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to reset the manager folder:\n\nfailed",
            ManagerFolderDialogTextBuilder.BuildResetFailureMessage("failed"));
    }
}
