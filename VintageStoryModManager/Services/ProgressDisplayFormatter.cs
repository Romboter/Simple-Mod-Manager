namespace VintageStoryModManager.Services;

internal static class ProgressDisplayFormatter
{
    public static string FormatDownloadSpeed(double bytesPerSecond)
    {
        const double kb = 1024d;
        const double mb = kb * 1024d;
        const double gb = mb * 1024d;

        return bytesPerSecond switch
        {
            < kb => $"{bytesPerSecond:0} B/s",
            < mb => $"{bytesPerSecond / kb:0.0} KB/s",
            < gb => $"{bytesPerSecond / mb:0.0} MB/s",
            _ => $"{bytesPerSecond / gb:0.0} GB/s"
        };
    }
}
