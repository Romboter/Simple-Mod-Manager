using System.Text;

namespace VintageStoryModManager.Services;

internal static class PresetApplicationSummaryBuilder
{
    internal static string BuildInstallFailureSummary(
        IReadOnlyList<string> missingMods,
        IReadOnlyList<string> missingVersions,
        IReadOnlyList<string> installFailures,
        string? localBackupDirectory,
        IReadOnlyList<string>? backedUpModNames)
    {
        var builder = new StringBuilder();

        if (missingMods.Count > 0)
        {
            builder.AppendLine("The following mods from the preset could not be installed:");
            foreach (var modId in missingMods.Distinct(StringComparer.OrdinalIgnoreCase))
                builder.AppendLine($" • {modId}");
        }

        if (missingVersions.Count > 0)
        {
            if (builder.Length > 0) builder.AppendLine();

            builder.AppendLine("The following mod versions could not be located:");
            foreach (var entry in missingVersions.Distinct(StringComparer.OrdinalIgnoreCase))
                builder.AppendLine($" • {entry}");
        }

        if (installFailures.Count > 0)
        {
            if (builder.Length > 0) builder.AppendLine();

            builder.AppendLine("Some mods failed to install:");
            foreach (var failure in installFailures.Distinct(StringComparer.OrdinalIgnoreCase))
                builder.AppendLine($" • {failure}");
        }

        if (missingMods.Count > 0
            && !string.IsNullOrWhiteSpace(localBackupDirectory)
            && backedUpModNames is { Count: > 0 })
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.AppendLine("Local copies of mods that are not on the mod database were saved to:");
            builder.AppendLine($" • {localBackupDirectory}");

            var distinctBackups = backedUpModNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctBackups.Count > 0)
            {
                builder.AppendLine("Backed up mods:");
                foreach (var backupName in distinctBackups) builder.AppendLine($"   • {backupName}");
            }
        }

        return builder.ToString().Trim();
    }
}
