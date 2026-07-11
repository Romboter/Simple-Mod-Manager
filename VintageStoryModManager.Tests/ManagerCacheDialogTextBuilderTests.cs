using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ManagerCacheDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "This will only delete the managers cached mods to save some disk space, it will not affect your installed mods.",
            ManagerCacheDialogTextBuilder.DeleteCachedModsConfirmationMessage);
        Assert.Equal(
            "Could not determine the cached mods directory.",
            ManagerCacheDialogTextBuilder.CachedModsDirectoryUnavailableMessage);
        Assert.Equal(
            "No cached mods were found.",
            ManagerCacheDialogTextBuilder.NoCachedModsFoundMessage);
        Assert.Equal(
            "Cached mods deleted successfully.",
            ManagerCacheDialogTextBuilder.CachedModsDeletedSuccessfullyMessage);
        Assert.Equal(
            "Could not locate the Simple VS Manager data directory.",
            ManagerCacheDialogTextBuilder.ManagerDataDirectoryUnavailableMessage);
    }

    [Fact]
    public void BuildDeleteCachedModsFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to delete cached mods:\nlocked",
            ManagerCacheDialogTextBuilder.BuildDeleteCachedModsFailureMessage("locked"));
    }

    [Fact]
    public void BuildClearAllCachesResultMessage_FormatsDeletedFolders()
    {
        var nl = Environment.NewLine;

        Assert.Equal(
            $"Successfully deleted the following cache folders:{nl}• Temp Cache{nl}• Image Cache{nl}",
            ManagerCacheDialogTextBuilder.BuildClearAllCachesResultMessage(
                new[] { "Temp Cache", "Image Cache" },
                Array.Empty<string>()));
    }

    [Fact]
    public void BuildClearAllCachesResultMessage_FormatsNoFoldersFound()
    {
        Assert.Equal(
            $"No cache folders were found to delete.{Environment.NewLine}",
            ManagerCacheDialogTextBuilder.BuildClearAllCachesResultMessage(
                Array.Empty<string>(),
                Array.Empty<string>()));
    }

    [Fact]
    public void BuildClearAllCachesResultMessage_FormatsDeletedAndFailedFolders()
    {
        var nl = Environment.NewLine;

        Assert.Equal(
            $"Successfully deleted the following cache folders:{nl}• Temp Cache{nl}{nl}" +
            $"Failed to delete the following cache folders:{nl}• locked folder{nl}",
            ManagerCacheDialogTextBuilder.BuildClearAllCachesResultMessage(
                new[] { "Temp Cache" },
                new[] { "locked folder" }));
    }

    [Fact]
    public void BuildClearAllCachesFailureMessage_FormatsError()
    {
        Assert.Equal(
            "An error occurred while clearing caches:\n\naccess denied",
            ManagerCacheDialogTextBuilder.BuildClearAllCachesFailureMessage("access denied"));
    }
}
