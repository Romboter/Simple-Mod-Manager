using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CloudSaveDialogTextBuilderTests
{
    [Fact]
    public void BuildReplaceExistingPrompt_FormatsNameAndSlot()
    {
        Assert.Equal(
            "A cloud modlist named \"World Mods\" already exists in Slot 1. Do you want to replace it?",
            CloudSaveDialogTextBuilder.BuildReplaceExistingPrompt("World Mods", "Slot 1"));
    }

    [Fact]
    public void BuildReplacedStatusMessage_FormatsNames()
    {
        Assert.Equal(
            "Replaced cloud modlist \"Old Mods\" with \"New Mods\".",
            CloudSaveDialogTextBuilder.BuildReplacedStatusMessage("Old Mods", "New Mods"));
    }

    [Fact]
    public void BuildSavedStatusMessage_FormatsName()
    {
        Assert.Equal(
            "Saved cloud modlist \"World Mods\" to the cloud.",
            CloudSaveDialogTextBuilder.BuildSavedStatusMessage("World Mods"));
    }
}
