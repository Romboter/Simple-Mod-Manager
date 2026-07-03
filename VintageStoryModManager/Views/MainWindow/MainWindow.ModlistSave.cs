#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox =
    VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void SaveModlistMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveModlist(null, out var savedFilePath))
        {
            if (!string.IsNullOrWhiteSpace(savedFilePath))
                RefreshLocalModlists(true, new[] { savedFilePath });
            else
                RefreshLocalModlists(true);
        }
    }

    private bool TrySaveModlist()
    {
        return TrySaveModlist(null, out _);
    }

    private bool TrySaveModlist(Func<string?>? suggestedNameProvider, out string? savedFilePath)
    {
        savedFilePath = null;

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
        if (dialogResult != true) return false;

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
            return TrySaveInstalledModsPdf(
                listName,
                version,
                description,
                createdBy,
                includedConfigurations,
                gameVersion);
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
                var message =
                    $"A modlist named \"{Path.GetFileName(filePath)}\" already exists in the Modlists folder. Do you want to replace it?";
                var confirmation = WpfMessageBox.Show(
                    this,
                    message,
                    "Replace Modlist",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes) return false;
            }

            var serializable = PresetSnapshotBuilder.BuildSerializablePreset(
                _viewModel!.GetCurrentModStates(),
                entryName,
                true,
                true,
                includedConfigurations,
                gameVersion);
            if (!string.IsNullOrWhiteSpace(listName)) serializable.Name = listName.Trim();
            serializable.Description = description;
            serializable.Version = version;
            serializable.Uploader = string.IsNullOrWhiteSpace(createdBy)
                ? null
                : createdBy.Trim();

            var json =
                PdfModlistSerializer.SerializeToJson(serializable);
            File.WriteAllText(filePath, json);

            _viewModel?.ReportStatus($"Saved modlist \"{entryName}\".");
            savedFilePath = filePath;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                      or PathTooLongException)
        {
            WpfMessageBox.Show($"Failed to save the modlist:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
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

        try
        {
            var json =
                PdfModlistSerializer.SerializeToJson(serializable);
            File.WriteAllText(filePath, json);

            _viewModel.ReportStatus($"Saved modlist \"{savedName}\".");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WpfMessageBox.Show($"Failed to save the modlist:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            savedName = string.Empty;
            filePath = string.Empty;
            return false;
        }
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

        var serializable = PresetSnapshotBuilder.BuildModlistPreset(
            _viewModel!.GetCurrentModStates(),
            trimmedName,
            description,
            version,
            uploader,
            includedConfigurations,
            gameVersion);

        json =
            PdfModlistSerializer.SerializeToJson(serializable);
        return true;
    }
}
