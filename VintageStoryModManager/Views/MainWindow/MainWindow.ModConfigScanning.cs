#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private Task<IReadOnlyList<(string ModId, string DisplayName, string ConfigPath)>> ScanForModConfigFilesAsync(
        MainViewModel viewModel)
    {
        return ScanForModConfigFilesAsync(viewModel, (IReadOnlyCollection<string>?)null);
    }

    private async Task<IReadOnlyList<(string ModId, string DisplayName, string ConfigPath)>> ScanForModConfigFilesAsync(
        MainViewModel viewModel,
        IReadOnlyCollection<string>? modIds)
    {
        if (viewModel is null) return Array.Empty<(string ModId, string DisplayName, string ConfigPath)>();

        IReadOnlyList<ModListItemViewModel> candidateMods;
        if (modIds is null)
        {
            candidateMods = viewModel.GetInstalledModsSnapshot();
        }
        else
        {
            var normalizedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mods = new List<ModListItemViewModel>();
            foreach (var id in modIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;

                var trimmedId = id.Trim();
                if (!normalizedIds.Add(trimmedId)) continue;

                var installedMod = viewModel.FindInstalledModById(trimmedId);
                if (installedMod != null) mods.Add(installedMod);
            }

            if (mods.Count == 0) return Array.Empty<(string ModId, string DisplayName, string ConfigPath)>();

            candidateMods = mods;
        }

        if (candidateMods.Count == 0) return Array.Empty<(string ModId, string DisplayName, string ConfigPath)>();

        return await ScanForModConfigFilesAsync(viewModel, candidateMods).ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<(string ModId, string DisplayName, string ConfigPath)>> ScanForModConfigFilesAsync(
        MainViewModel viewModel,
        IReadOnlyList<ModListItemViewModel> candidateMods)
    {
        var assigned = new List<(string ModId, string DisplayName, string ConfigPath)>();
        if (string.IsNullOrWhiteSpace(_dataDirectory) || candidateMods.Count == 0) return assigned;

        try
        {
            var configDirectory = Path.Combine(_dataDirectory, "ModConfig");
            if (!Directory.Exists(configDirectory)) return assigned;

            var missingMods = new List<(string ModId, string DisplayName)>();
            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in candidateMods)
            {
                if (mod is null) continue;

                var modId = mod.ModId;
                if (string.IsNullOrWhiteSpace(modId)) continue;

                var trimmedId = modId.Trim();
                if (!seenIds.Add(trimmedId)) continue;

                if (_userConfiguration.TryGetModConfigPath(trimmedId, out var path)
                    && !string.IsNullOrWhiteSpace(path))
                    continue;

                var displayName = string.IsNullOrWhiteSpace(mod.DisplayName)
                    ? trimmedId
                    : mod.DisplayName!.Trim();
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

                _userConfiguration.SetModConfigPath(match.ModId, match.ConfigPath);
                var displayName = displayNames.TryGetValue(match.ModId, out var value)
                    ? value
                    : match.ModId;
                assigned.Add((match.ModId, displayName, match.ConfigPath));
            }

            if (assigned.Count > 0) UpdateSelectedModEditConfigButton(viewModel.SelectedMod);
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus($"Failed to scan for mod configuration files: {ex.Message}", true);
            throw;
        }

        return assigned;
    }
}
