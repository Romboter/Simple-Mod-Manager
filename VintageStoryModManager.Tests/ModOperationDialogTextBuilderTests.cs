using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModOperationDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "No downloadable releases are available for this mod.",
            ModOperationDialogTextBuilder.NoDownloadableReleasesMessage);
        Assert.Equal(
            "All mods are already up to date.",
            ModOperationDialogTextBuilder.AllModsAlreadyUpToDateMessage);
        Assert.Equal(
            "This mod does not declare dependencies that can be fixed automatically.",
            ModOperationDialogTextBuilder.NoAutoFixableDependenciesMessage);
    }

    [Fact]
    public void BuildInstallFailureMessage_FormatsModAndError()
    {
        Assert.Equal(
            $"Failed to install Better Dirt:{Environment.NewLine}download failed",
            ModOperationDialogTextBuilder.BuildInstallFailureMessage(
                "Better Dirt",
                "download failed"));
    }

    [Fact]
    public void BuildDeleteConfirmationMessage_FormatsModName()
    {
        Assert.Equal(
            "Are you sure you want to delete Better Dirt? This will remove the mod from disk.",
            ModOperationDialogTextBuilder.BuildDeleteConfirmationMessage("Better Dirt"));
    }

    [Fact]
    public void BuildRefreshAfterDeleteFailureMessage_FormatsError()
    {
        Assert.Equal(
            $"The mod list could not be refreshed:{Environment.NewLine}locked",
            ModOperationDialogTextBuilder.BuildRefreshAfterDeleteFailureMessage("locked"));
    }

    [Fact]
    public void BuildDeleteFailureMessage_FormatsModAndError()
    {
        Assert.Equal(
            $"Failed to delete Better Dirt:{Environment.NewLine}access denied",
            ModOperationDialogTextBuilder.BuildDeleteFailureMessage(
                "Better Dirt",
                "access denied"));
    }

    [Fact]
    public void BuildMissingDeletedModMessage_FormatsPath()
    {
        Assert.Equal(
            $"The mod could not be found at:{Environment.NewLine}C:\\Mods\\betterdirt.zip{Environment.NewLine}It may have already been removed.",
            ModOperationDialogTextBuilder.BuildMissingDeletedModMessage(
                @"C:\Mods\betterdirt.zip"));
    }

    [Fact]
    public void BuildRefreshAfterDependencyRepairFailureMessage_FormatsError()
    {
        Assert.Equal(
            $"The mods with errors could not be refreshed after fixing dependencies:{Environment.NewLine}refresh failed",
            ModOperationDialogTextBuilder.BuildRefreshAfterDependencyRepairFailureMessage(
                "refresh failed"));
    }

    [Fact]
    public void BuildDependencyFailuresMessage_FormatsFailures()
    {
        Assert.Equal(
            $"Some dependencies could not be resolved:{Environment.NewLine}dep one{Environment.NewLine}dep two",
            ModOperationDialogTextBuilder.BuildDependencyFailuresMessage(
                new[] { "dep one", "dep two" }));
    }

    [Fact]
    public void BuildRefreshAfterUpdateFailureMessage_FormatsError()
    {
        Assert.Equal(
            $"The mod list could not be refreshed after updating mods:{Environment.NewLine}refresh failed",
            ModOperationDialogTextBuilder.BuildRefreshAfterUpdateFailureMessage(
                "refresh failed"));
    }

    [Theory]
    [InlineData(true, "Bulk update cancelled.")]
    [InlineData(false, "Update cancelled.")]
    public void BuildUpdateCancelledMessage_FormatsBulkFlag(bool isBulk, string expected)
    {
        Assert.Equal(expected, ModOperationDialogTextBuilder.BuildUpdateCancelledMessage(isBulk));
    }
}
