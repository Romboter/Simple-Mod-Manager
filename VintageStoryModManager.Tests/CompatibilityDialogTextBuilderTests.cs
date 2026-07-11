using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CompatibilityDialogTextBuilderTests
{
    [Fact]
    public void BuildFailedToRetrieveVersionsMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to retrieve Vintage Story versions:\nconnection refused",
            CompatibilityDialogTextBuilder.BuildFailedToRetrieveVersionsMessage("connection refused"));
    }

    [Fact]
    public void BuildNoInstalledModsMessage_IncludesVersion()
    {
        Assert.Equal(
            "Vintage Story version: 1.19.8.\n\nNo installed mods were found.",
            CompatibilityDialogTextBuilder.BuildNoInstalledModsMessage("1.19.8"));
    }

    [Fact]
    public void BuildExperimentalCompReviewFailedMessage_FormatsError()
    {
        Assert.Equal(
            "The experimental compatibility review failed:\ntimeout",
            CompatibilityDialogTextBuilder.BuildExperimentalCompReviewFailedMessage("timeout"));
    }

    [Fact]
    public void BuildCompatibilityCommentsTitle_IncludesDisplayName()
    {
        Assert.Equal(
            "Compatibility comments for My Mod",
            CompatibilityDialogTextBuilder.BuildCompatibilityCommentsTitle("My Mod"));
    }
}
