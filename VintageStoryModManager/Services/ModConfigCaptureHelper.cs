#nullable enable

using System.Diagnostics;
using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Services;

internal static class ModConfigCaptureHelper
{
    internal static List<ModConfigOption> BuildOptions(
        IReadOnlyList<ModListItemViewModel?> mods,
        Func<string, IReadOnlyList<string>> getConfigPaths,
        bool selectByDefault)
    {
        var options = new List<ModConfigOption>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            if (mod is null || string.IsNullOrWhiteSpace(mod.ModId)) continue;

            var normalizedId = mod.ModId.Trim();
            if (!seenIds.Add(normalizedId)) continue;

            var configPaths = getConfigPaths(normalizedId)
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .ToList();

            if (configPaths.Count > 0)
                options.Add(new ModConfigOption(normalizedId, mod.DisplayName, configPaths, selectByDefault));
        }

        options.Sort((left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        return options;
    }

    internal static (Dictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? Configurations, string? ErrorMessage)
        CaptureConfigurations(
            IReadOnlyList<ModConfigOption> selectedConfigOptions,
            string? dataDirectory)
    {
        var requests =
            selectedConfigOptions
                .Where(option => option is not null)
                .Select(option =>
                    new ModConfigurationCaptureRequest(
                        option.ModId,
                        option.DisplayName,
                        option.ConfigPaths))
                .ToList();

        var captureResult =
            ModConfigurationCaptureService.Capture(
                requests,
                dataDirectory);

        string? errorMessage = null;
        if (captureResult.Errors.Count > 0)
        {
            errorMessage =
                "Some configuration files could not be included:\n" +
                string.Join(
                    "\n",
                    captureResult.Errors.Select(
                        error =>
                            $"{error.DisplayName}: {error.Message}"));
        }

        var configurations = captureResult.Configurations is null
            ? null
            : new Dictionary<
                string,
                IReadOnlyList<ModConfigurationSnapshot>>(
                    captureResult.Configurations,
                    StringComparer.OrdinalIgnoreCase);

        return (configurations, errorMessage);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? CaptureConfigurationsForMods(
        IReadOnlyList<ModListItemViewModel> mods,
        Func<string, IReadOnlyList<string>> getConfigPaths,
        string? dataDirectory)
    {
        if (mods is null || mods.Count == 0)
            return null;

        var requests =
            mods
                .Where(mod => mod is not null && !string.IsNullOrWhiteSpace(mod.ModId))
                .GroupBy(mod => mod.ModId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var mod = group.First();
                    var modId = group.Key;

                    var configPaths = getConfigPaths(modId)
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(path => path.Trim())
                        .Where(File.Exists)
                        .ToList();

                    return new ModConfigurationCaptureRequest(modId, mod.DisplayName, configPaths);
                })
                .Where(request => request.ConfigPaths.Count > 0)
                .ToList();

        var captureResult = ModConfigurationCaptureService.Capture(requests, dataDirectory);

        foreach (var error in captureResult.Errors)
        {
            Trace.TraceWarning(
                "Failed to include configuration file {0} for mod {1} in backup: {2}",
                error.Path,
                error.ModId,
                error.Message);
        }

        return captureResult.Configurations;
    }
}
