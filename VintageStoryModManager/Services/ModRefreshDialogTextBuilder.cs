#nullable enable

namespace VintageStoryModManager.Services;

internal static class ModRefreshDialogTextBuilder
{
    internal static string BuildRefreshModDetailsFailureMessage(string? errorMessage) => $"Failed to refresh mod details:\n{errorMessage}";

    internal static string BuildDependencyResolutionRefreshFailureMessage(string? errorMessage) => $"The mod list could not be refreshed after resolving dependencies:{Environment.NewLine}{errorMessage}";

    internal static string BuildModlistLoadRefreshFailureMessage(string? errorMessage) => $"Failed to refresh mods after loading the modlist:{Environment.NewLine}{errorMessage}";

    internal static string BuildAutomaticRefreshFailureMessage(string? errorMessage) => $"Failed to refresh mods automatically:\n{errorMessage}";
}
