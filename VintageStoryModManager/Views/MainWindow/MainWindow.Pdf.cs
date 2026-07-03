#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void SaveInstalledModsPdfMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            WpfMessageBox.Show(
                "Mods are still loading. Please try again once loading is complete.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var mods = _viewModel.GetInstalledModsSnapshot();
        if (mods.Count == 0)
        {
            WpfMessageBox.Show(
                "No installed mods were found to include in the PDF.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var configOptions = BuildModConfigOptions();

        var metadataDialog = new SaveInstalledModsDialog(
            InstalledModsPdfGenerator.BuildCloudModlistName(),
            configOptions,
            GetUploaderNameForPdf(),
            defaultVersion: null,
            defaultGameVersion: _viewModel?.InstalledGameVersion,
            SaveInstalledModsDialogResult.SavePdf)
        {
            Owner = this
        };

        var metadataResult = metadataDialog.ShowDialog();
        if (metadataResult != true) return;

        var listName = metadataDialog.ListName;
        var version = metadataDialog.Version;
        var description = metadataDialog.Description;
        var uploaderName = metadataDialog.CreatedBy;
        if (string.IsNullOrWhiteSpace(uploaderName)) uploaderName = GetUploaderNameForPdf();
        else uploaderName = uploaderName!.Trim();
        var gameVersion = ResolveGameVersion(metadataDialog.VintageStoryVersion);

        var selectedConfigOptions = metadataDialog.GetSelectedConfigOptions();
        var includedConfigurations = TryReadModConfigurations(selectedConfigOptions);

        TrySaveInstalledModsPdf(
            listName,
            version,
            description,
            uploaderName,
            includedConfigurations,
            gameVersion,
            mods);
    }

    private bool TrySaveInstalledModsPdf(
            string listName,
            string? version,
            string? description,
            string uploaderName,
            IReadOnlyDictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations,
            string? gameVersion,
            IReadOnlyList<ModListItemViewModel>? preFetchedMods = null)
    {
        if (_viewModel is null)
        {
            WpfMessageBox.Show(
                "Mods are still loading. Please try again once loading is complete.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var mods = preFetchedMods ?? _viewModel.GetInstalledModsSnapshot();
        if (mods.Count == 0)
        {
            WpfMessageBox.Show(
                "No installed mods were found to include in the PDF.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        string filePath;
        try
        {
            var modListDirectory = EnsureModListDirectory();
            var entryName = FileNameHelper.BuildSuggestedFileName(listName, "Modlist");
            filePath = Path.Combine(modListDirectory, entryName + ".pdf");

            if (File.Exists(filePath))
            {
                var message =
                    $"A modlist PDF named \"{Path.GetFileName(filePath)}\" already exists in the Modlists folder. Do you want to replace it?";
                var confirmation = WpfMessageBox.Show(
                    this,
                    message,
                    "Replace Modlist PDF",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes) return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or PathTooLongException or SecurityException)
        {
            WpfMessageBox.Show($"Failed to prepare the Modlists folder:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        var presetName = string.IsNullOrWhiteSpace(listName)
            ? "Installed Mods"
            : listName.Trim();
        var resolvedGameVersion = ResolveGameVersion(gameVersion);
        var serializable = PresetSnapshotBuilder.BuildSerializablePreset(
            _viewModel!.GetCurrentModStates(),
            presetName,
            true,
            true,
            includedConfigurations,
            resolvedGameVersion);

        serializable.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        serializable.Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        serializable.Uploader = string.IsNullOrWhiteSpace(uploaderName) ? null : uploaderName.Trim();
        if (!string.IsNullOrWhiteSpace(listName)) serializable.Name = listName.Trim();

        var serializableConfigList = PresetConfigurationSerializer.BuildSerializableConfigList(includedConfigurations);

        var normalizedUploader = string.IsNullOrWhiteSpace(uploaderName)
            ? GetUploaderNameForPdf()
            : uploaderName.Trim();

        try
        {
            InstalledModsPdfGenerator.GenerateInstalledModsPdf(
                filePath,
                listName,
                version,
                description,
                normalizedUploader,
                resolvedGameVersion,
                mods,
                serializable,
                serializableConfigList);

            _viewModel.ReportStatus($"Saved installed mods PDF to \"{filePath}\".");

            WpfMessageBox.Show(
                "Saved installed mods PDF successfully.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or PathTooLongException)
        {
            WpfMessageBox.Show(
                $"Failed to save the PDF:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to generate the PDF:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return false;
    }

    private string GetUploaderNameForPdf()
    {
        var configuredName = _userConfiguration?.CloudUploaderName;
        if (!string.IsNullOrWhiteSpace(configuredName)) return configuredName.Trim();

        var playerName = _viewModel?.PlayerName;
        if (!string.IsNullOrWhiteSpace(playerName)) return playerName.Trim();

        var suffixSource = _viewModel?.PlayerUid ?? _cloudModlistStore?.CurrentUserId;
        if (!string.IsNullOrWhiteSpace(suffixSource)) return ResolveUploaderName(suffixSource);

        return "Anonymous";
    }

}
