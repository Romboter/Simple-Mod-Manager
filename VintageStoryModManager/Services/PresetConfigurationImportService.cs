using System.IO;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class PresetConfigurationImportService
{
    internal static IReadOnlyList<PresetConfigurationImportEntry> CollectConfigurations(
        ModPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var configurations = new List<PresetConfigurationImportEntry>();
        var seenConfigurations = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var state in preset.ModStates)
        {
            if (state is null || string.IsNullOrWhiteSpace(state.ModId))
                continue;

            var trimmedId = state.ModId.Trim();

            if (state.Configurations is not null &&
                state.Configurations.Count > 0)
            {
                foreach (var configuration in state.Configurations)
                {
                    if (configuration is null ||
                        string.IsNullOrEmpty(configuration.Content))
                        continue;

                    var key =
                        $"{trimmedId}::{configuration.FileName}::" +
                        $"{configuration.RelativePath}::{configuration.Content}";

                    if (!seenConfigurations.Add(key))
                        continue;

                    configurations.Add(
                        new PresetConfigurationImportEntry(
                            trimmedId,
                            configuration.FileName,
                            configuration.RelativePath,
                            configuration.Content));
                }

                continue;
            }

            if (state.ConfigurationContent is null)
                continue;

            var legacyKey =
                $"{trimmedId}::{state.ConfigurationFileName}::" +
                state.ConfigurationContent;

            if (!seenConfigurations.Add(legacyKey))
                continue;

            configurations.Add(
                new PresetConfigurationImportEntry(
                    trimmedId,
                    state.ConfigurationFileName,
                    null,
                    state.ConfigurationContent));
        }

        return configurations;
    }

    internal static async Task<PresetConfigurationImportResult> ImportAsync(
        IReadOnlyList<PresetConfigurationImportEntry> configurations,
        string dataDirectory,
        UserConfigurationService userConfiguration,
        IReadOnlyDictionary<string, string> modDisplayNames)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(userConfiguration);
        ArgumentNullException.ThrowIfNull(modDisplayNames);

        var configDirectory = Path.Combine(dataDirectory, "ModConfig");
        Directory.CreateDirectory(configDirectory);

        var usedRelativePaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var importedCount = 0;

        var modConfigTargets =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);
        var modConfigNames =
            new Dictionary<string, List<string?>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurations)
        {
            var fileName = ModConfigPathHelper.GetSafeConfigFileName(
                configuration.FileName,
                configuration.ModId);

            var relativePath = ModConfigPathHelper.NormalizeRelativeConfigPath(
                                   configuration.RelativePath,
                                   fileName)
                               ?? fileName;

            var uniqueRelativePath =
                ModConfigPathHelper.EnsureUniqueRelativePath(
                    relativePath,
                    usedRelativePaths);

            var targetPath = Path.Combine(
                configDirectory,
                uniqueRelativePath);

            if (!PathRelationshipHelper.IsPathWithinDirectory(
                    configDirectory,
                    targetPath))
            {
                uniqueRelativePath =
                    ModConfigPathHelper.EnsureUniqueRelativePath(
                        fileName,
                        usedRelativePaths);

                targetPath = Path.Combine(
                    configDirectory,
                    uniqueRelativePath);
            }

            try
            {
                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                    Directory.CreateDirectory(targetDirectory);

                await File.WriteAllTextAsync(
                        targetPath,
                        configuration.Content)
                    .ConfigureAwait(false);

                if (!modConfigTargets.TryGetValue(
                        configuration.ModId,
                        out var pathsForMod))
                {
                    pathsForMod = new List<string>();
                    modConfigTargets[configuration.ModId] = pathsForMod;
                }

                if (!modConfigNames.TryGetValue(
                        configuration.ModId,
                        out var namesForMod))
                {
                    namesForMod = new List<string?>();
                    modConfigNames[configuration.ModId] = namesForMod;
                }

                pathsForMod.Add(targetPath);
                namesForMod.Add(configuration.FileName);
                importedCount++;
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException or
                PathTooLongException)
            {
                errors.Add(
                    $"{GetDisplayName(configuration.ModId, modDisplayNames)}: " +
                    ex.Message);
            }
        }

        if (importedCount > 0)
        {
            foreach (var pair in modConfigTargets)
            {
                var modId = pair.Key;
                var paths = pair.Value;

                var names = modConfigNames.TryGetValue(
                    modId,
                    out var configNames)
                    ? configNames
                    : null;

                try
                {
                    userConfiguration.SetModConfigPaths(
                        modId,
                        paths,
                        names);
                }
                catch (Exception ex) when (
                    ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
                {
                    errors.Add(
                        $"{GetDisplayName(modId, modDisplayNames)}: " +
                        ex.Message);
                }
            }
        }

        return new PresetConfigurationImportResult(
            importedCount,
            errors);
    }

    internal static PresetConfigurationImportPrompt BuildImportPrompt(
        IReadOnlyList<PresetConfigurationImportEntry> configurations,
        Func<string, string?> resolveDisplayName)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        ArgumentNullException.ThrowIfNull(resolveDisplayName);

        var modDisplayNames =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        var promptNames = new List<string>(configurations.Count);

        foreach (var configuration in configurations)
        {
            var displayName = configuration.ModId;

            var resolvedName = resolveDisplayName(configuration.ModId);
            if (!string.IsNullOrWhiteSpace(resolvedName))
                displayName = resolvedName;

            if (!modDisplayNames.ContainsKey(configuration.ModId))
                modDisplayNames.Add(configuration.ModId, displayName);

            promptNames.Add(displayName);
        }

        promptNames = promptNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summary = promptNames.Count == 0
            ? string.Empty
            : string.Join(
                "\n",
                promptNames.Select(name => $"• {name}"));

        var message =
            "This modlist includes configuration files for the following mods:";

        if (!string.IsNullOrEmpty(summary))
            message += $"\n\n{summary}";

        message +=
            "\n\nImporting these configurations will overwrite your existing " +
            "settings for these mods if they are already installed. " +
            "Do you want to import them?";

        return new PresetConfigurationImportPrompt(message, modDisplayNames);
    }

    private static string GetDisplayName(
        string modId,
        IReadOnlyDictionary<string, string> modDisplayNames)
    {
        return modDisplayNames.TryGetValue(modId, out var displayName) &&
               !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : modId;
    }
}

internal sealed record PresetConfigurationImportEntry(
    string ModId,
    string? FileName,
    string? RelativePath,
    string Content);

internal sealed record PresetConfigurationImportResult(
    int ImportedCount,
    IReadOnlyList<string> Errors);

internal sealed record PresetConfigurationImportPrompt(
    string Message,
    IReadOnlyDictionary<string, string> ModDisplayNames);
