using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CloudLoadDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "No cloud modlists are available.",
            CloudLoadDialogTextBuilder.NoCloudModlistsAvailableMessage);
        Assert.Equal(
            "The selected cloud modlist is empty.",
            CloudLoadDialogTextBuilder.SelectedCloudModlistEmptyMessage);
        Assert.Equal(
            "No cloud modlists are available to delete.",
            CloudLoadDialogTextBuilder.NoCloudModlistsAvailableToDeleteMessage);
        Assert.Equal(
            "The selected cloud modlist could not be downloaded.",
            CloudLoadDialogTextBuilder.SelectedCloudModlistDownloadFailedMessage);
    }

    [Fact]
    public void BuildCachePreparationFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to prepare the cloud modlist cache:\ndenied",
            CloudLoadDialogTextBuilder.BuildCachePreparationFailureMessage("denied"));
    }

    [Fact]
    public void BuildSelectedModlistCacheFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to cache the selected modlist:\nlocked",
            CloudLoadDialogTextBuilder.BuildSelectedModlistCacheFailureMessage("locked"));
    }

    [Theory]
    [InlineData(null, "Failed to load the downloaded cloud modlist.")]
    [InlineData("Invalid JSON.", "Invalid JSON.")]
    public void BuildDownloadedModlistLoadFailureMessage_UsesLoaderMessageOrFallback(
        string? errorMessage,
        string expected)
    {
        Assert.Equal(
            expected,
            CloudLoadDialogTextBuilder.BuildDownloadedModlistLoadFailureMessage(errorMessage));
    }

    [Theory]
    [InlineData(null, "Failed to load the modlist:\nThe selected cloud modlist is not valid.")]
    [InlineData("Missing mod list.", "Failed to load the modlist:\nMissing mod list.")]
    public void BuildCloudModlistLoadFailureMessage_UsesLoaderMessageOrFallback(
        string? errorMessage,
        string expected)
    {
        Assert.Equal(
            expected,
            CloudLoadDialogTextBuilder.BuildCloudModlistLoadFailureMessage(errorMessage));
    }

    [Fact]
    public void BuildDeleteConfirmationMessage_FormatsDisplayName()
    {
        Assert.Equal(
            "Are you sure you want to delete Slot 1 (\"World Mods\")? This action cannot be undone.",
            CloudLoadDialogTextBuilder.BuildDeleteConfirmationMessage("Slot 1 (\"World Mods\")"));
    }
}
