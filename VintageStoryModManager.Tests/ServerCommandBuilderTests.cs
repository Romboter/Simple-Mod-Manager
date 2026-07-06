using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ServerCommandBuilderTests
{
    [Theory]
    [InlineData("primitivesurvival", "3.8.6", "/moddb install primitivesurvival@3.8.6")]
    [InlineData(" spaced ", " 1.0 ", "/moddb install spaced@1.0")]
    public void TryBuildInstallCommand_ValidInputs_BuildsCommand(string modId, string version, string expected)
    {
        Assert.Equal(expected, ServerCommandBuilder.TryBuildInstallCommand(modId, version));
    }

    [Theory]
    [InlineData(null, "1.0")]
    [InlineData("", "1.0")]
    [InlineData("   ", "1.0")]
    [InlineData("mod", null)]
    [InlineData("mod", "")]
    [InlineData("mod", "   ")]
    public void TryBuildInstallCommand_MissingParts_ReturnsNull(string? modId, string? version)
    {
        Assert.Null(ServerCommandBuilder.TryBuildInstallCommand(modId, version));
    }

    [Fact]
    public void CanCopyInstallCommand_RequiresServerOptionsAndValidCommand()
    {
        Assert.True(ServerCommandBuilder.CanCopyInstallCommand(true, "mod", "1.0"));
        Assert.False(ServerCommandBuilder.CanCopyInstallCommand(false, "mod", "1.0"));
        Assert.False(ServerCommandBuilder.CanCopyInstallCommand(true, null, "1.0"));
    }

    [Fact]
    public void InternalsAreVisibleToTestAssembly()
    {
        // Probes InternalsVisibleTo wiring; PathRelationshipHelper is internal.
        Assert.False(PathRelationshipHelper.IsPathUnderDirectory("", null));
    }
}
