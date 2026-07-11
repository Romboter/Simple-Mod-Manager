#nullable enable

using System.IO;
using System.Security;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void SaveInstalledModsPdfMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.ModsStillLoadingMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var mods = _viewModel.GetInstalledModsSnapshot();
        if (mods.Count == 0)
        {
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.NoInstalledModsMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
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

        await TrySaveInstalledModsPdfAsync(
                listName,
                version,
                description,
                uploaderName,
                includedConfigurations,
                gameVersion,
                mods)
            .ConfigureAwait(true);
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
        return TrySaveInstalledModsPdfAsync(
                listName,
                version,
                description,
                uploaderName,
                includedConfigurations,
                gameVersion,
                preFetchedMods)
            .GetAwaiter()
            .GetResult();
    }

    private async Task<bool> TrySaveInstalledModsPdfAsync(
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
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.ModsStillLoadingMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return false;
        }

        var mods = preFetchedMods ?? _viewModel.GetInstalledModsSnapshot();
        if (mods.Count == 0)
        {
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.NoInstalledModsMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return false;
        }

        ModlistFilePathResolution resolution;
        try
        {
            var modListDirectory = EnsureModListDirectory();
            resolution = ModlistWorkflowService.ResolveModlistFilePath(modListDirectory, listName, null, ".pdf");

            if (resolution.AlreadyExists)
            {
                var confirmation = await _confirmationService.ConfirmAsync(
                        PdfDialogTextBuilder.BuildReplaceExistingMessage(resolution.FilePath),
                        "Replace Modlist PDF",
                        DialogSeverity.Question)
                    .ConfigureAwait(true);

                if (!confirmation) return false;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or PathTooLongException or SecurityException)
        {
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.BuildPrepareFolderFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return false;
        }

        var resolvedGameVersion = ResolveGameVersion(gameVersion);
        var normalizedUploader = string.IsNullOrWhiteSpace(uploaderName)
            ? GetUploaderNameForPdf()
            : uploaderName.Trim();

        try
        {
            ModlistWorkflowService.SavePdfModlist(
                resolution.FilePath,
                listName,
                version,
                description,
                normalizedUploader,
                resolvedGameVersion,
                mods,
                _viewModel!.GetCurrentModStates(),
                includedConfigurations);

            _viewModel.ReportStatus($"Saved installed mods PDF to \"{resolution.FilePath}\".");

            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.SavedSuccessfullyMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or PathTooLongException)
        {
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.BuildSaveFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    PdfDialogTextBuilder.BuildGenerateFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }

        return false;
    }

    private string GetUploaderNameForPdf()
    {
        var configuredName = _userConfiguration?.CloudUploaderName;
        if (!string.IsNullOrWhiteSpace(configuredName)) return configuredName.Trim();

        var playerName = _viewModel?.PlayerName;
        if (!string.IsNullOrWhiteSpace(playerName)) return playerName.Trim();

        var suffixSource = _viewModel?.PlayerUid ?? _cloudWorkflowCoordinator.CurrentStore?.CurrentUserId;
        if (!string.IsNullOrWhiteSpace(suffixSource)) return ResolveUploaderName(suffixSource);

        return "Anonymous";
    }
}
