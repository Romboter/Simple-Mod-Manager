using VintageStoryModManager.Helpers;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ScrollWheelMathTests
{
    [Fact]
    public void TryComputeTargetOffset_ZeroDelta_ReturnsFalse()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(0, 120, 3, 1, 100, 1000, out var targetOffset);

        Assert.False(result);
        Assert.Equal(0, targetOffset);
    }

    [Fact]
    public void TryComputeTargetOffset_ZeroScrollMultiplier_ReturnsFalse()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(120, 120, 3, 0, 100, 1000, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryComputeTargetOffset_PositiveDelta_ScrollsUp()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(120, 120, 3, 1, 100, 1000, out var targetOffset);

        Assert.True(result);
        Assert.True(targetOffset < 100);
    }

    [Fact]
    public void TryComputeTargetOffset_NegativeDelta_ScrollsDown()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(-120, 120, 3, 1, 100, 1000, out var targetOffset);

        Assert.True(result);
        Assert.True(targetOffset > 100);
    }

    [Fact]
    public void TryComputeTargetOffset_ClampsToZeroAtTop()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(120, 120, 3, 1, 1, 1000, out var targetOffset);

        Assert.True(result);
        Assert.Equal(0, targetOffset);
    }

    [Fact]
    public void TryComputeTargetOffset_ClampsToScrollableHeightAtBottom()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(-120, 120, 3, 1, 999, 1000, out var targetOffset);

        Assert.True(result);
        Assert.Equal(1000, targetOffset);
    }

    [Fact]
    public void TryComputeTargetOffset_WheelScrollLinesBelowOne_FlooredToOne()
    {
        var flooredResult = ScrollWheelMath.TryComputeTargetOffset(120, 120, 0, 1, 100, 1000, out var flooredOffset);
        var oneLineResult = ScrollWheelMath.TryComputeTargetOffset(120, 120, 1, 1, 100, 1000, out var oneLineOffset);

        Assert.True(flooredResult);
        Assert.True(oneLineResult);
        Assert.Equal(oneLineOffset, flooredOffset);
    }

    [Fact]
    public void TryComputeTargetOffset_SanityCheck_MatchesExpectedValue()
    {
        var result = ScrollWheelMath.TryComputeTargetOffset(120, 120, 3, 1, 100, 1000, out var targetOffset);

        Assert.True(result);
        Assert.Equal(97, targetOffset);
    }
}
