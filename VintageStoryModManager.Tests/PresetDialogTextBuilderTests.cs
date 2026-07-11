using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class PresetDialogTextBuilderTests
{
    [Fact]
    public void BuildSaveFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal("Failed to save the preset:\ndisk full",
            PresetDialogTextBuilder.BuildSaveFailureMessage("preset", "disk full"));
    }

    [Fact]
    public void BuildLoadFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal("Failed to load the preset:\ninvalid file",
            PresetDialogTextBuilder.BuildLoadFailureMessage("invalid file"));
    }

    [Fact]
    public void BuildExclusiveRemovalFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal("Some mods could not be removed:\r\n • First failure\r\n • Second failure",
            PresetDialogTextBuilder.BuildExclusiveRemovalFailureMessage(
                new[] { "First failure", "Second failure" }));
    }
}
