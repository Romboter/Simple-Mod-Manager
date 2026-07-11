using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class InstalledModIdListBuilderTests
{
    [Fact]
    public void Build_SkipsNullWhitespaceAndEmptyIds()
    {
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(new[] { null, "", "   ", "\t" });

        Assert.Empty(installedModIds);
        Assert.Empty(numericInstalledModIds);
    }

    [Fact]
    public void Build_NonNumericIds_LandInStringListOnly()
    {
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(new[] { "smithing", "carryon" });

        Assert.Equal(new[] { "smithing", "carryon" }, installedModIds);
        Assert.Empty(numericInstalledModIds);
    }

    [Fact]
    public void Build_NumericIds_LandInBothLists()
    {
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(new[] { "123", "456" });

        Assert.Equal(new[] { "123", "456" }, installedModIds);
        Assert.Equal(new[] { 123, 456 }, numericInstalledModIds);
    }

    [Fact]
    public void Build_DuplicateNumericIds_DedupedInNumericListButNotStringList()
    {
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(new[] { "123", "123" });

        Assert.Equal(new[] { "123", "123" }, installedModIds);
        Assert.Equal(new[] { 123 }, numericInstalledModIds);
    }

    [Fact]
    public void Build_PreservesFirstSeenOrder()
    {
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(new[] { "456", "smithing", "123" });

        Assert.Equal(new[] { "456", "smithing", "123" }, installedModIds);
        Assert.Equal(new[] { 456, 123 }, numericInstalledModIds);
    }

    [Fact]
    public void Build_MixedRealisticInput()
    {
        var (installedModIds, numericInstalledModIds) =
            InstalledModIdListBuilder.Build(new[] { "smithing", "123", null, " ", "123", "456" });

        Assert.Equal(new[] { "smithing", "123", "123", "456" }, installedModIds);
        Assert.Equal(new[] { 123, 456 }, numericInstalledModIds);
    }
}
