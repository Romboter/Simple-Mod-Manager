using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModConfigDialogTextBuilderTests
{
    [Fact]
    public void BuildScanFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal(
            "Failed to scan for mod configuration files:\nscan failed",
            ModConfigDialogTextBuilder.BuildScanFailureMessage("scan failed"));
    }

    [Fact]
    public void BuildStoreFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal(
            "Failed to store the configuration path:\nstore failed",
            ModConfigDialogTextBuilder.BuildStoreFailureMessage("store failed"));
    }

    [Fact]
    public void BuildOpenFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal(
            "Failed to open the configuration file:\nopen failed",
            ModConfigDialogTextBuilder.BuildOpenFailureMessage("open failed"));
    }
}
