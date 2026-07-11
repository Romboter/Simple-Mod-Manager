#nullable enable

using System.IO;
using System.Text;
using VintageStoryModManager.Helpers;

namespace VintageStoryModManager.Services;

internal static class ModConfigDiscoveryService
{
    internal static string? GetScanBlockedMessage(bool modsLoaded, bool isBusy, string? dataDirectory)
    {
        if (!modsLoaded)
            return "Mods have not been loaded yet. Load mods before scanning for configuration files.";

        if (isBusy)
            return "Please wait for the current operation to finish before scanning for configuration files.";

        if (string.IsNullOrWhiteSpace(dataDirectory))
            return "The Vintage Story data directory is not set, so mod configuration files cannot be located.";

        var configDirectory = Path.Combine(dataDirectory, "ModConfig");
        if (!Directory.Exists(configDirectory))
            return $"No mod configuration directory was found at:\n{configDirectory}";

        return null;
    }

    internal static async Task<IReadOnlyList<ModConfigScanResult>> ScanAsync(
        string? dataDirectory,
        IReadOnlyList<(string? ModId, string? DisplayName)> candidateMods,
        Func<string, string?> tryGetConfigPath,
        Action<string, string> setConfigPath)
    {
        var assigned = new List<ModConfigScanResult>();
        if (string.IsNullOrWhiteSpace(dataDirectory) || candidateMods.Count == 0) return assigned;

        try
        {
            var configDirectory = Path.Combine(dataDirectory, "ModConfig");
            if (!Directory.Exists(configDirectory)) return assigned;

            var missingMods = new List<(string ModId, string DisplayName)>();
            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (modId, displayNameRaw) in candidateMods)
            {
                if (string.IsNullOrWhiteSpace(modId)) continue;

                var trimmedId = modId.Trim();
                if (!seenIds.Add(trimmedId)) continue;

                if (!string.IsNullOrWhiteSpace(tryGetConfigPath(trimmedId))) continue;

                var displayName = string.IsNullOrWhiteSpace(displayNameRaw)
                    ? trimmedId
                    : displayNameRaw!.Trim();
                missingMods.Add((trimmedId, displayName));
                displayNames[trimmedId] = displayName;
            }

            if (missingMods.Count == 0) return assigned;

            var configFiles = ModConfigPathHelper.GetSupportedConfigFiles(configDirectory);
            if (configFiles.Length == 0) return assigned;

            var matches =
                await Task.Run(() => ModConfigurationMatcher.FindConfigMatches(missingMods, configFiles)).ConfigureAwait(true);
            if (matches.Count == 0) return assigned;

            foreach (var match in matches)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(match.ConfigPath) || !File.Exists(match.ConfigPath)) continue;
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                setConfigPath(match.ModId, match.ConfigPath);
                var displayName = displayNames.TryGetValue(match.ModId, out var value)
                    ? value
                    : match.ModId;
                assigned.Add(new ModConfigScanResult(match.ModId, displayName, match.ConfigPath));
            }
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus($"Failed to scan for mod configuration files: {ex.Message}", true);
            throw;
        }

        return assigned;
    }

    internal static string FormatAssignedConfigsMessage(IReadOnlyList<ModConfigScanResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Assigned configuration files for the following mods:");
        foreach (var result in results
                     .OrderBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            builder.Append(" • ");
            builder.Append(result.DisplayName);
            if (!string.Equals(result.DisplayName, result.ModId, StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" (");
                builder.Append(result.ModId);
                builder.Append(')');
            }

            builder.AppendLine();
            builder.Append("    ");
            var configFileName = Path.GetFileName(result.ConfigPath);
            builder.AppendLine(string.IsNullOrEmpty(configFileName) ? result.ConfigPath : configFileName);
        }

        return builder.ToString();
    }
}

internal sealed record ModConfigScanResult(string ModId, string DisplayName, string ConfigPath);
