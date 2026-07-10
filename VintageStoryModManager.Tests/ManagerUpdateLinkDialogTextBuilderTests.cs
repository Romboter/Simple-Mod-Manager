using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ManagerUpdateLinkDialogTextBuilderTests
{
    [Fact]
    public void BuildOpenModDatabasePageFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to open the mod database page:\ntimeout",
            ManagerUpdateLinkDialogTextBuilder.BuildOpenModDatabasePageFailureMessage("timeout"));
    }
}
