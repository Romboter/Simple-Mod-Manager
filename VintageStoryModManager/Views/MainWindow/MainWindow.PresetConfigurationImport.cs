#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task ImportPresetConfigsAsync(ModPreset preset)
    {
        if (preset.ModStates.Count == 0) return;

        var configurations =
            PresetConfigurationImportService.CollectConfigurations(preset);

        if (configurations.Count == 0) return;

        var modDisplayNames =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        var promptNames = new List<string>(configurations.Count);

        foreach (var configuration in configurations)
        {
            var displayName = configuration.ModId;

            if (_viewModel?.TryGetInstalledModDisplayName(
                    configuration.ModId,
                    out var resolvedName) == true &&
                !string.IsNullOrWhiteSpace(resolvedName))
            {
                displayName = resolvedName.Trim();
            }

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

        var prompt = WpfMessageBox.Show(
            this,
            message,
            "Import Mod Configurations",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (prompt != MessageBoxResult.Yes) return;

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            WpfMessageBox.Show(
                "The Vintage Story data directory is not set, so the " +
                "configuration files could not be imported.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PresetConfigurationImportResult importResult;

        try
        {
            importResult =
                await PresetConfigurationImportService.ImportAsync(
                        configurations,
                        _dataDirectory,
                        _userConfiguration,
                        modDisplayNames)
                    .ConfigureAwait(true);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            WpfMessageBox.Show(
                $"Failed to prepare the configuration directory:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (importResult.ImportedCount > 0)
        {
            _viewModel?.ReportStatus(
                $"Imported configuration files for " +
                $"{importResult.ImportedCount} mod(s).");

            UpdateSelectedModButtons();
        }

        if (importResult.Errors.Count > 0)
        {
            WpfMessageBox.Show(
                "Some configuration files could not be imported:\n" +
                string.Join("\n", importResult.Errors),
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
