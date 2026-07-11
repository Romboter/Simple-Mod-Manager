using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModUsageDialogTextBuilderTests
{
    [Fact]
    public void BuildVoteSubmissionFailureMessage_MatchesExistingDialogText()
    {
        Assert.Equal("Some votes could not be submitted:" + Environment.NewLine + "vote failed", ModUsageDialogTextBuilder.BuildVoteSubmissionFailureMessage("vote failed"));
    }
}
