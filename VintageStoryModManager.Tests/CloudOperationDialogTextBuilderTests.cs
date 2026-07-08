using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CloudOperationDialogTextBuilderTests
{
    [Fact]
    public void BuildInitializationFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to initialize cloud storage:\ndenied",
            CloudOperationDialogTextBuilder.BuildInitializationFailureMessage("denied"));
    }

    [Theory]
    [InlineData("load the modlist from the cloud", "offline", "Failed to load the modlist from the cloud:\noffline")]
    [InlineData("delete the cloud modlist", "forbidden", "Failed to delete the cloud modlist:\nforbidden")]
    public void BuildActionFailureMessage_FormatsActionAndError(
        string actionDescription,
        string errorMessage,
        string expected)
    {
        Assert.Equal(
            expected,
            CloudOperationDialogTextBuilder.BuildActionFailureMessage(
                actionDescription,
                errorMessage));
    }

    [Fact]
    public void BuildUnexpectedActionFailureMessage_FormatsActionAndError()
    {
        Assert.Equal(
            "An unexpected error occurred while attempting to save the modlist to the cloud:\nboom",
            CloudOperationDialogTextBuilder.BuildUnexpectedActionFailureMessage(
                "save the modlist to the cloud",
                "boom"));
    }
}
