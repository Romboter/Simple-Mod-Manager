using VintageStoryModManager.Helpers;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ModInfoPanelLayoutHelperTests
{
    [Fact]
    public void ClampPanelPosition_InsideBounds_Unchanged()
    {
        var (left, top) = ModInfoPanelLayoutHelper.ClampPanelPosition(
            left: 100, top: 50, containerWidth: 800, containerHeight: 600, panelWidth: 200, panelHeight: 200,
            horizontalOverhang: 20);

        Assert.Equal(100, left);
        Assert.Equal(50, top);
    }

    [Fact]
    public void ClampPanelPosition_BelowMinimum_ClampsToOverhangAndZero()
    {
        var (left, top) = ModInfoPanelLayoutHelper.ClampPanelPosition(
            left: -500, top: -500, containerWidth: 800, containerHeight: 600, panelWidth: 200, panelHeight: 200,
            horizontalOverhang: 20);

        Assert.Equal(-20, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void ClampPanelPosition_BeyondRightAndBottom_ClampsToMax()
    {
        var (left, top) = ModInfoPanelLayoutHelper.ClampPanelPosition(
            left: 5000, top: 5000, containerWidth: 800, containerHeight: 600, panelWidth: 200, panelHeight: 200,
            horizontalOverhang: 20);

        Assert.Equal(800 - 200 + 20, left);
        Assert.Equal(600 - 200, top);
    }

    [Fact]
    public void ClampPanelPosition_PanelLargerThanContainer_FloorsAtMinimum()
    {
        var (left, top) = ModInfoPanelLayoutHelper.ClampPanelPosition(
            left: 5000, top: 5000, containerWidth: 100, containerHeight: 100, panelWidth: 400, panelHeight: 400,
            horizontalOverhang: 20);

        Assert.Equal(-20, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void ComputeDefaultLeft_WideContainer_RightAlignedWhenSmallerThanPreferred()
    {
        var result = ModInfoPanelLayoutHelper.ComputeDefaultLeft(
            containerWidth: 2000, panelWidth: 300, preferredLeft: 1900, rightMargin: 20, horizontalOverhang: 20);

        Assert.Equal(2000 - 300 - 20, result);
    }

    [Fact]
    public void ComputeDefaultLeft_NarrowContainer_ClampedIntoRange()
    {
        var result = ModInfoPanelLayoutHelper.ComputeDefaultLeft(
            containerWidth: 100, panelWidth: 300, preferredLeft: 900, rightMargin: 20, horizontalOverhang: 20);

        var minLeft = -20;
        var maxLeft = System.Math.Max(minLeft, 100 - 300 + 20);
        Assert.Equal(maxLeft, result);
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(800, 0)]
    [InlineData(-1, 300)]
    public void ComputeDefaultLeft_NonPositiveContainerOrPanelWidth_ReturnsPreferredLeftUnchanged(
        double containerWidth, double panelWidth)
    {
        var result = ModInfoPanelLayoutHelper.ComputeDefaultLeft(
            containerWidth, panelWidth, preferredLeft: 42, rightMargin: 20, horizontalOverhang: 20);

        Assert.Equal(42, result);
    }
}
