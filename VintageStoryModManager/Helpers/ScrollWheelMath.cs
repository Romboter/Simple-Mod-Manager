namespace VintageStoryModManager.Helpers;

internal static class ScrollWheelMath
{
    internal static bool TryComputeTargetOffset(
        double delta,
        double wheelDeltaForOneLine,
        double wheelScrollLines,
        double scrollMultiplier,
        double currentOffset,
        double scrollableHeight,
        out double targetOffset)
    {
        targetOffset = 0;

        var lines = Math.Max(1, wheelScrollLines);
        var deltaMultiplier = delta / wheelDeltaForOneLine;
        var offsetChange = deltaMultiplier * lines * scrollMultiplier;
        if (Math.Abs(offsetChange) < double.Epsilon) return false;

        targetOffset = Math.Max(0, Math.Min(currentOffset - offsetChange, scrollableHeight));
        return true;
    }
}
