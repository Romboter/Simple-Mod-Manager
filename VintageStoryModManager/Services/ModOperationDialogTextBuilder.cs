#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace VintageStoryModManager.Services;

internal static class ModOperationDialogTextBuilder
{
    internal const string NoDownloadableReleasesMessage =
        "No downloadable releases are available for this mod.";

    internal const string AllModsAlreadyUpToDateMessage =
        "All mods are already up to date.";

    internal const string NoAutoFixableDependenciesMessage =
        "This mod does not declare dependencies that can be fixed automatically.";

    internal static string BuildInstallFailureMessage(string displayName, string? errorMessage)
    {
        return $"Failed to install {displayName}:{System.Environment.NewLine}{errorMessage}";
    }

    internal static string BuildDeleteConfirmationMessage(string displayName)
    {
        return $"Are you sure you want to delete {displayName}? This will remove the mod from disk.";
    }

    internal static string BuildRefreshAfterDeleteFailureMessage(string? errorMessage)
    {
        return $"The mod list could not be refreshed:{System.Environment.NewLine}{errorMessage}";
    }

    internal static string BuildDeleteFailureMessage(string displayName, string? errorMessage)
    {
        return $"Failed to delete {displayName}:{System.Environment.NewLine}{errorMessage}";
    }

    internal static string BuildMissingDeletedModMessage(string modPath)
    {
        return $"The mod could not be found at:{System.Environment.NewLine}{modPath}{System.Environment.NewLine}It may have already been removed.";
    }

    internal static string BuildRefreshAfterDependencyRepairFailureMessage(string? errorMessage)
    {
        return $"The mods with errors could not be refreshed after fixing dependencies:{System.Environment.NewLine}{errorMessage}";
    }

    internal static string BuildDependencyFailuresMessage(IEnumerable<string> failures)
    {
        return "Some dependencies could not be resolved:" +
               System.Environment.NewLine +
               string.Join(System.Environment.NewLine, failures);
    }

    internal static string BuildRefreshAfterUpdateFailureMessage(string? errorMessage)
    {
        return $"The mod list could not be refreshed after updating mods:{System.Environment.NewLine}{errorMessage}";
    }

    internal static string BuildUpdateCancelledMessage(bool isBulk)
    {
        return isBulk ? "Bulk update cancelled." : "Update cancelled.";
    }
}
