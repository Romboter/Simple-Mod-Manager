using VintageStoryModManager.Helpers;
using Xunit;

namespace VintageStoryModManager.Tests;

public class PathRelationshipHelperTests
{
    [Theory]
    [InlineData(@"C:\data\Mods\mod.zip", @"C:\data\Mods", true)]
    [InlineData(@"C:\data\Mods", @"C:\data\Mods", true)] // the directory itself counts
    [InlineData(@"C:\data\Mods\", @"C:\data\Mods", true)]
    [InlineData(@"C:\data\ModsOther\mod.zip", @"C:\data\Mods", false)] // prefix, not child
    [InlineData(@"C:\data\mods\MOD.zip", @"C:\DATA\Mods", true)] // case-insensitive
    [InlineData(@"C:\data\Mods\sub\..\mod.zip", @"C:\data\Mods", true)] // normalized
    [InlineData(@"C:\other\mod.zip", @"C:\data\Mods", false)]
    public void IsPathUnderDirectory_KnownCases(string path, string directory, bool expected)
    {
        Assert.Equal(expected, PathRelationshipHelper.IsPathUnderDirectory(path, directory));
    }

    [Theory]
    [InlineData("", @"C:\data")]
    [InlineData("   ", @"C:\data")]
    [InlineData(@"C:\data", null)]
    [InlineData(@"C:\data", "")]
    public void IsPathUnderDirectory_MissingInput_ReturnsFalse(string path, string? directory)
    {
        Assert.False(PathRelationshipHelper.IsPathUnderDirectory(path, directory));
    }

    [Fact]
    public void IsSameDirectory_NormalizesSeparatorsAndCase()
    {
        Assert.True(PathRelationshipHelper.IsSameDirectory(@"C:\Data\Mods\", @"c:\data\MODS"));
        Assert.False(PathRelationshipHelper.IsSameDirectory(@"C:\Data\Mods", @"C:\Data\Mods2"));
        Assert.False(PathRelationshipHelper.IsSameDirectory(null, @"C:\Data"));
        Assert.False(PathRelationshipHelper.IsSameDirectory(@"C:\Data", "   "));
    }

    [Fact]
    public void IsPathWithinDirectory_ExcludesTheDirectoryItself()
    {
        // Characterization: unlike IsPathUnderDirectory, the directory itself is NOT within.
        Assert.True(PathRelationshipHelper.IsPathWithinDirectory(@"C:\data\Mods", @"C:\data\Mods\mod.zip"));
        Assert.False(PathRelationshipHelper.IsPathWithinDirectory(@"C:\data\Mods", @"C:\data\Mods"));
    }
}
