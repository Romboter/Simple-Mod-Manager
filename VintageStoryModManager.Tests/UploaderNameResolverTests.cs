using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class UploaderNameResolverTests
{
    [Fact]
    public void Resolve_ReturnsTrimmedPlayerName_WhenPlayerNamePresent()
    {
        Assert.Equal("Steve", UploaderNameResolver.Resolve("  Steve  ", "someSuffixSource"));
    }

    [Theory]
    [InlineData("ab", "Anonymousab")]
    [InlineData("a", "Anonymousa")]
    [InlineData("abcd", "Anonymousabcd")]
    public void Resolve_UsesWholeSuffixSource_WhenFourCharsOrShorter(string suffixSource, string expected)
    {
        Assert.Equal(expected, UploaderNameResolver.Resolve(null, suffixSource));
    }

    [Fact]
    public void Resolve_UsesLastFourChars_WhenSuffixSourceLongerThanFour()
    {
        Assert.Equal("Anonymousf123", UploaderNameResolver.Resolve(null, "abcdef123"));
    }

    [Fact]
    public void Resolve_TrimsSuffixSourceBeforeSlicing()
    {
        Assert.Equal("Anonymousabcd", UploaderNameResolver.Resolve("   ", "  abcd  "));
    }

    [Fact]
    public void Resolve_ReturnsDefault_WhenBothPlayerNameAndSuffixSourceMissing()
    {
        Assert.Equal("Anonymous0000", UploaderNameResolver.Resolve(null, null));
        Assert.Equal("Anonymous0000", UploaderNameResolver.Resolve("   ", "   "));
    }

    [Fact]
    public void Resolve_PlayerNameWinsOverSuffixSource_WhenBothPresent()
    {
        Assert.Equal("Alice", UploaderNameResolver.Resolve("Alice", "abcdef123"));
    }
}
