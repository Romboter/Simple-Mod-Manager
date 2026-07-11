using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModRefreshDialogTextBuilderTests
{
    [Fact]
    public void Messages_MatchExistingDialogText()
    {
        Assert.Equal("Failed to refresh mod details:\nrefresh failed", ModRefreshDialogTextBuilder.BuildRefreshModDetailsFailureMessage("refresh failed"));
        Assert.Equal("The mod list could not be refreshed after resolving dependencies:" + Environment.NewLine + "refresh failed", ModRefreshDialogTextBuilder.BuildDependencyResolutionRefreshFailureMessage("refresh failed"));
        Assert.Equal("Failed to refresh mods after loading the modlist:" + Environment.NewLine + "refresh failed", ModRefreshDialogTextBuilder.BuildModlistLoadRefreshFailureMessage("refresh failed"));
        Assert.Equal("Failed to refresh mods automatically:\nrefresh failed", ModRefreshDialogTextBuilder.BuildAutomaticRefreshFailureMessage("refresh failed"));
    }
}
