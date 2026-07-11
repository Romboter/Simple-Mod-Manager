namespace VintageStoryModManager.Helpers;

internal static class ModInfoPanelLayoutHelper
{
    internal static (double Left, double Top) ClampPanelPosition(
        double left,
        double top,
        double containerWidth,
        double containerHeight,
        double panelWidth,
        double panelHeight,
        double horizontalOverhang)
    {
        var minLeft = -horizontalOverhang;
        var maxLeft = Math.Max(minLeft, containerWidth - panelWidth + horizontalOverhang);
        double minTop = 0;
        var maxTop = Math.Max(minTop, containerHeight - panelHeight);

        var clampedLeft = Math.Min(Math.Max(left, minLeft), maxLeft);
        var clampedTop = Math.Min(Math.Max(top, minTop), maxTop);

        return (clampedLeft, clampedTop);
    }

    internal static double ComputeDefaultLeft(
        double containerWidth,
        double panelWidth,
        double preferredLeft,
        double rightMargin,
        double horizontalOverhang)
    {
        if (containerWidth > 0 && panelWidth > 0)
        {
            var rightAlignedLeft = containerWidth - panelWidth - rightMargin;
            if (!double.IsNaN(rightAlignedLeft)) preferredLeft = Math.Min(preferredLeft, rightAlignedLeft);

            var minLeft = -horizontalOverhang;
            var maxLeft = Math.Max(minLeft, containerWidth - panelWidth + horizontalOverhang);

            return Math.Min(Math.Max(preferredLeft, minLeft), maxLeft);
        }

        return preferredLeft;
    }
}
