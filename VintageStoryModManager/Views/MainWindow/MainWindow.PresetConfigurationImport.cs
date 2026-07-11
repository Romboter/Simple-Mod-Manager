#nullable enable

using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

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

        var confirmed = await _confirmationService.ConfirmAsync(
                prompt.Message,
                "Import Mod Configurations",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (!confirmed) return;

        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            await _confirmationService.NotifyAsync(
                    PresetConfigurationImportDialogTextBuilder.DataDirectoryNotSetMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    PresetConfigurationImportDialogTextBuilder
                        .BuildPrepareDirectoryFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    PresetConfigurationImportDialogTextBuilder
                        .BuildPartialFailureMessage(importResult.Errors),
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
    }
}
