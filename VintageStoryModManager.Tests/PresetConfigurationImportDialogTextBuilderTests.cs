using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class PresetConfigurationImportDialogTextBuilderTests
{
    [Fact]
    public void DataDirectoryNotSetMessage_MatchesExistingDialogText()
    {
        Assert.Equal(
            "The Vintage Story data directory is not set, so the " +
            "configuration files could not be imported.",
            PresetConfigurationImportDialogTextBuilder.DataDirectoryNotSetMessage);
    }

    [Fact]
    public void BuildPrepareDirectoryFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to prepare the configuration directory:\naccess denied",
            PresetConfigurationImportDialogTextBuilder
                .BuildPrepareDirectoryFailureMessage("access denied"));
    }

    [Fact]
    public void BuildPartialFailureMessage_JoinsErrorsOnSeparateLines()
    {
        Assert.Equal(
            "Some configuration files could not be imported:\n" +
            "First Mod: disk full\nSecond Mod: access denied",
            PresetConfigurationImportDialogTextBuilder
                .BuildPartialFailureMessage(
                    new[]
                    {
                        "First Mod: disk full",
                        "Second Mod: access denied"
                    }));
    }
}
