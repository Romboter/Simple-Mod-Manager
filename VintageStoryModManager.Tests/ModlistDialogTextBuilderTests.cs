using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistDialogTextBuilderTests
{
    [Fact]
    public void BuildReplaceExistingMessage_MatchesOriginalText()
    {
        Assert.Equal(
            "A modlist named \"Example.json\" already exists in the Modlists folder. Do you want to replace it?",
            ModlistDialogTextBuilder.BuildReplaceExistingMessage("Example.json"));
    }

    [Fact]
    public void BuildSaveFailureMessage_MatchesOriginalText()
    {
        Assert.Equal(
            "Failed to save the modlist:\ndenied",
            ModlistDialogTextBuilder.BuildSaveFailureMessage("denied"));
    }

    [Fact]
    public void BuildInvalidFileMessage_MatchesOriginalText()
    {
        Assert.Equal(
            "The file is not a valid SVSM modlist.\ninvalid",
            ModlistDialogTextBuilder.BuildInvalidFileMessage("invalid"));
    }
}
