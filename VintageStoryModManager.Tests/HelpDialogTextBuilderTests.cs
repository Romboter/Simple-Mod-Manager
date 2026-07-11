using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class HelpDialogTextBuilderTests
{
    [Fact]
    public void BuildOpenDiscordFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to open Discord:\nno browser found",
            HelpDialogTextBuilder.BuildOpenDiscordFailureMessage("no browser found"));
    }
}
