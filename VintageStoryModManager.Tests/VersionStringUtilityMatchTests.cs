using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class VersionStringUtilityMatchTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3", "1.2.3", "1.2.3", true)] // exact match
    [InlineData("1.2.3", "1.2.3.0", "1.2.3.0", "1.2.3.0", true)] // normalized-only match (candidate differs textually)
    [InlineData("1.2.3", "1.2.3", "1.2.4", "1.2.4", false)] // no match
    [InlineData(null, null, "1.2.3", "1.2.3", false)] // desired null/whitespace -> false
    [InlineData("", "", "1.2.3", "1.2.3", false)]
    [InlineData("1.2.3", "1.2.3", null, null, false)] // candidate null/whitespace -> false
    [InlineData("1.2.3", "1.2.3", "", "", false)]
    public void MatchesDesiredVersion_ReturnsExpected(
        string? desiredVersion,
        string? desiredNormalized,
        string? candidateVersion,
        string? candidateNormalized,
        bool expected)
    {
        var result = VersionStringUtility.MatchesDesiredVersion(
            desiredVersion, desiredNormalized, candidateVersion, candidateNormalized);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void MatchesDesiredVersion_ExactTextDiffers_ButNormalizedMatches_ReturnsTrue()
    {
        var result = VersionStringUtility.MatchesDesiredVersion(
            "V1.2.3", "1.2.3", "1.2.3-release", "1.2.3");

        Assert.True(result);
    }

    [Fact]
    public void MatchesDesiredVersion_CaseInsensitiveExactMatch_ReturnsTrue()
    {
        var result = VersionStringUtility.MatchesDesiredVersion(
            "1.2.3-BETA", null, "1.2.3-beta", null);

        Assert.True(result);
    }
}
