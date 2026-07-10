#nullable enable

namespace VintageStoryModManager.Services;

internal static class ModConfigDialogTextBuilder
{
    internal static string BuildScanFailureMessage(string errorMessage)
    {
        return $"Failed to scan for mod configuration files:\n{errorMessage}";
    }

    internal static string BuildStoreFailureMessage(string errorMessage)
    {
        return $"Failed to store the configuration path:\n{errorMessage}";
    }

    internal static string BuildOpenFailureMessage(string errorMessage)
    {
        return $"Failed to open the configuration file:\n{errorMessage}";
    }
}
