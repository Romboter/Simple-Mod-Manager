using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CloudManagementDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "You do not have any cloud modlists saved.",
            CloudManagementDialogTextBuilder.NoCloudModlistsSavedMessage);
        Assert.Equal(
            "The selected cloud modlist could not be loaded.",
            CloudManagementDialogTextBuilder.ContentUnavailableMessage);
        Assert.Equal(
            "Cloud modlists and Firebase authorization have been deleted.",
            CloudManagementDialogTextBuilder.DeletedAllCloudModlistsAndAuthorizationMessage);
    }

    [Fact]
    public void BuildRenameLoadFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to load the cloud modlist before renaming:\ntimeout",
            CloudManagementDialogTextBuilder.BuildRenameLoadFailureMessage("timeout"));
    }

    [Fact]
    public void BuildInvalidRenameContentMessage_FormatsError()
    {
        Assert.Equal(
            "The cloud modlist data is invalid and could not be renamed:\ninvalid json",
            CloudManagementDialogTextBuilder.BuildInvalidRenameContentMessage("invalid json"));
    }

    [Fact]
    public void BuildRenameSaveFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to rename the cloud modlist:\npermission denied",
            CloudManagementDialogTextBuilder.BuildRenameSaveFailureMessage("permission denied"));
    }

    [Theory]
    [InlineData("network down", "Failed to delete the cloud modlist:\nnetwork down")]
    [InlineData("invalid request", "Failed to delete the cloud modlist:\ninvalid request")]
    public void BuildDeleteFailureMessage_FormatsError(string errorMessage, string expected)
    {
        Assert.Equal(
            expected,
            CloudManagementDialogTextBuilder.BuildDeleteFailureMessage(errorMessage));
    }

    [Fact]
    public void BuildRenamedStatusMessage_FormatsSlotAndName()
    {
        Assert.Equal(
            "Renamed cloud modlist in Slot 2 to \"World Mods\".",
            CloudManagementDialogTextBuilder.BuildRenamedStatusMessage("Slot 2", "World Mods"));
    }

    [Fact]
    public void BuildDeletedStatusMessage_FormatsSlot()
    {
        Assert.Equal(
            "Deleted cloud modlist from Slot 3.",
            CloudManagementDialogTextBuilder.BuildDeletedStatusMessage("Slot 3"));
    }
}
