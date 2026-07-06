#nullable enable

using System.IO;
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

        var prompt = PresetConfigurationImportService.BuildImportPrompt(
            configurations,
            modId => _viewModel?.TryGetInstalledModDisplayName(modId, out var resolvedName) == true
                     && !string.IsNullOrWhiteSpace(resolvedName)
                ? resolvedName.Trim()
                : null);

        var promptResult = WpfMessageBox.Show(
            this,
            prompt.Message,
            "Import Mod Configurations",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (promptResult != MessageBoxResult.Yes) return;

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
                        prompt.ModDisplayNames)
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
