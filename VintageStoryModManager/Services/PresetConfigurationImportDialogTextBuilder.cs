#nullable enable

namespace VintageStoryModManager.Services;

internal static class PresetConfigurationImportDialogTextBuilder
{
    internal const string DataDirectoryNotSetMessage =
        "The Vintage Story data directory is not set, so the " +
        "configuration files could not be imported.";

    internal static string BuildPrepareDirectoryFailureMessage(
        string? errorMessage)
    {
        return $"Failed to prepare the configuration directory:\n{errorMessage}";
    }

    internal static string BuildPartialFailureMessage(
        IEnumerable<string> errors)
    {
        return "Some configuration files could not be imported:\n" +
               string.Join("\n", errors);
    }
}
