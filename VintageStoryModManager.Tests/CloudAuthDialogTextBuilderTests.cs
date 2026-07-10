using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class CloudAuthDialogTextBuilderTests
{
    [Fact]
    public void BuildRestoreFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to restore firebase-auth.json: disk full",
            CloudAuthDialogTextBuilder.BuildRestoreFailureMessage("disk full"));
    }
}
