using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UserReportsDialogTextBuilderTests
{
    [Fact]
    public void CannotSubmitUserReportMessage_MatchesExistingCopy()
    {
        Assert.Equal(
            "User reports are unavailable because the mod version or Vintage Story version could not be determined.",
            UserReportsDialogTextBuilder.CannotSubmitUserReportMessage);
    }

    [Fact]
    public void BuildLoadFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to load user reports:\nnetwork down",
            UserReportsDialogTextBuilder.BuildLoadFailureMessage("network down"));
    }
}
