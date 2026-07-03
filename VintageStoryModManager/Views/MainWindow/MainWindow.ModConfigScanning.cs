#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void ScanForModConfigsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            WpfMessageBox.Show(
                "Mods have not been loaded yet. Load mods before scanning for configuration files.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_viewModel.IsBusy)
        {
            WpfMessageBox.Show(
                "Please wait for the current operation to finish before scanning for configuration files.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            WpfMessageBox.Show(
                "The Vintage Story data directory is not set, so mod configuration files cannot be located.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var configDirectory = Path.Combine(_dataDirectory, "ModConfig");
        if (!Directory.Exists(configDirectory))
        {
            WpfMessageBox.Show(
                $"No mod configuration directory was found at:\n{configDirectory}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var results =
                await ScanForModConfigFilesAsync(_viewModel).ConfigureAwait(true);

            if (results.Count == 0)
            {
                _viewModel.ReportStatus("No missing mod configuration files were found.");
                WpfMessageBox.Show(
                    "No missing mod configuration files were found.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _viewModel.ReportStatus($"Assigned configuration files for {results.Count} mod(s).");

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

            WpfMessageBox.Show(
                builder.ToString(),
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to scan for mod configuration files:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

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
