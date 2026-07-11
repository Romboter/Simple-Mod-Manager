#nullable enable

using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void SaveModlistMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var saveResult = await TrySaveModlistAsync(null).ConfigureAwait(true);
        if (saveResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(saveResult.SavedFilePath))
                await RefreshLocalModlistsAsync(true, new[] { saveResult.SavedFilePath }).ConfigureAwait(true);
            else
                await RefreshLocalModlistsAsync(true).ConfigureAwait(true);
        }
    }

    private bool TrySaveModlist()
    {
        return TrySaveModlist(null, out _);
    }

    private bool TrySaveModlist(Func<string?>? suggestedNameProvider, out string? savedFilePath)
    {
        var result = TrySaveModlistAsync(suggestedNameProvider).GetAwaiter().GetResult();
        savedFilePath = result.SavedFilePath;
        return result.Success;
    }

    private async Task<(bool Success, string? SavedFilePath)> TrySaveModlistAsync(
        Func<string?>? suggestedNameProvider)
    {

        var configOptions = BuildModConfigOptions();
        var suggestedName = suggestedNameProvider?.Invoke();

        var metadataDialog = new SaveInstalledModsDialog(
            suggestedName,
            configOptions,
            GetUploaderNameForPdf(),
            defaultVersion: null,
            defaultGameVersion: _viewModel?.InstalledGameVersion,
            SaveInstalledModsDialogResult.SaveJson)
        {
            Owner = this
        };

        var dialogResult = metadataDialog.ShowDialog();
        if (dialogResult != true) return (false, null);

        var listName = metadataDialog.ListName;
        var version = metadataDialog.Version;
        var description = metadataDialog.Description;
        var createdBy = metadataDialog.CreatedBy;
        createdBy = string.IsNullOrWhiteSpace(createdBy)
            ? GetUploaderNameForPdf()
            : createdBy!.Trim();
        var gameVersion = ResolveGameVersion(metadataDialog.VintageStoryVersion);

        var selectedConfigOptions = metadataDialog.GetSelectedConfigOptions();
        var includedConfigurations = TryReadModConfigurations(selectedConfigOptions);

        if (metadataDialog.SelectedAction == SaveInstalledModsDialogResult.SavePdf)
        {
            return (TrySaveInstalledModsPdf(
                listName,
                version,
                description,
                createdBy,
                includedConfigurations,
                gameVersion), null);
        }

        try
        {
            var modListDirectory = EnsureModListDirectory();
            var suggestedEntryName = !string.IsNullOrWhiteSpace(listName)
                ? listName
                : suggestedName;
            var entryName = FileNameHelper.BuildSuggestedFileName(suggestedEntryName, "Modlist");
            var filePath = Path.Combine(modListDirectory, entryName + ".json");

            if (File.Exists(filePath))
            {
                var message = ModlistDialogTextBuilder.BuildReplaceExistingMessage(Path.GetFileName(filePath));
                var confirmation = await _confirmationService.ConfirmAsync(
                        message,
                        "Replace Modlist",
                        DialogSeverity.Question)
                    .ConfigureAwait(true);

                if (!confirmation) return (false, null);
            }

            var serializable = PresetSnapshotBuilder.BuildModlistPreset(
                _viewModel!.GetCurrentModStates(),
                string.IsNullOrWhiteSpace(listName) ? entryName : listName,
                description,
                version,
                createdBy,
                includedConfigurations,
                gameVersion);

            var saveResult = LocalModlistFileService.Save(filePath, serializable);
            if (!saveResult.Success)
            {
                await _confirmationService.NotifyAsync(
                        ModlistDialogTextBuilder.BuildSaveFailureMessage(saveResult.ErrorMessage!),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
                return (false, null);
            }

            _viewModel?.ReportStatus($"Saved modlist \"{entryName}\".");
            return (true, filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                      or PathTooLongException)
        {
            await _confirmationService.NotifyAsync(
                    ModlistDialogTextBuilder.BuildSaveFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return (false, null);
        }
    }

    private bool TrySaveAutomaticModlist(string requestedName, out string savedName, out string filePath)
    {
        savedName = string.Empty;
        filePath = string.Empty;

        if (_viewModel is null) return false;

        var modListDirectory = EnsureRebuiltModListDirectory();
        savedName = FileNameHelper.BuildSuggestedFileName(requestedName, "Modlist");
        filePath = Path.Combine(modListDirectory, savedName + ".json");

        var serializable = PresetSnapshotBuilder.BuildSerializablePreset(
            _viewModel!.GetCurrentModStates(),
            savedName,
            true,
            true,
            gameVersion: ResolveGameVersion(null));

        var saveResult = LocalModlistFileService.Save(filePath, serializable);
        if (!saveResult.Success)
        {
            _ = _confirmationService.NotifyAsync(
                ModlistDialogTextBuilder.BuildSaveFailureMessage(saveResult.ErrorMessage!),
                "Simple VS Manager",
                DialogSeverity.Error);
            savedName = string.Empty;
            filePath = string.Empty;
            return false;
        }

        _viewModel.ReportStatus($"Saved modlist \"{savedName}\".");
        return true;
    }

    private bool TryBuildCurrentModlistJson(
            string modlistName,
            string? description,
            string? version,
            string uploader,
            IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
            string? gameVersion,
            out string json)
    {
        json = string.Empty;

        var trimmedName = string.IsNullOrWhiteSpace(modlistName) ? null : modlistName.Trim();
        if (string.IsNullOrEmpty(trimmedName) || _viewModel is null) return false;

        json = ModlistWorkflowService.BuildModlistJson(
            _viewModel!.GetCurrentModStates(),
            trimmedName,
            description,
            version,
            uploader,
            includedConfigurations,
            gameVersion);

        return true;
    }
}
