using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class InstalledModLogIdentifierListBuilderTests
{
    [Fact]
    public void Build_FormatsLabel_WithAndWithoutDisplayName()
    {
        var mods = new (string? ModId, string? DisplayName)[]
        {
            ("smellyfeet", "Smelly Feet"),
            ("noname", null)
        };

        var result = InstalledModLogIdentifierListBuilder.Build(mods);

        Assert.Equal(2, result.Count);
        Assert.Equal("Smelly Feet (smellyfeet)", result[0].DisplayLabel);
        Assert.Equal("noname", result[1].DisplayLabel);
    }

    [Fact]
    public void Build_TrimsIdAndDisplayName()
    {
        var mods = new (string? ModId, string? DisplayName)[] { (" id ", "  Name  ") };

        var result = InstalledModLogIdentifierListBuilder.Build(mods);

        var entry = Assert.Single(result);
        Assert.Equal("id", entry.SearchValue);
        Assert.Equal("Name (id)", entry.DisplayLabel);
    }

    [Fact]
    public void Build_DedupesCaseInsensitively_FirstWins()
    {
        var mods = new (string? ModId, string? DisplayName)[]
        {
            ("ModA", "First"),
            ("moda", "Second")
        };

        var result = InstalledModLogIdentifierListBuilder.Build(mods);

        var entry = Assert.Single(result);
        Assert.Equal("First (ModA)", entry.DisplayLabel);
    }

    [Fact]
    public void Build_SkipsNullAndWhitespaceIds()
    {
        var mods = new (string? ModId, string? DisplayName)[]
        {
            (null, "X"),
            ("   ", "Y"),
            ("ok", "Z")
        };

        var result = InstalledModLogIdentifierListBuilder.Build(mods);

        var entry = Assert.Single(result);
        Assert.Equal("ok", entry.SearchValue);
    }

    [Fact]
    public void Build_WhitespaceDisplayName_FallsBackToId()
    {
        var mods = new (string? ModId, string? DisplayName)[] { ("id", "   ") };

        var result = InstalledModLogIdentifierListBuilder.Build(mods);

        var entry = Assert.Single(result);
        Assert.Equal("id", entry.DisplayLabel);
    }
}
