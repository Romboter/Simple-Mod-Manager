#nullable enable

using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private Task SaveModlistToCloudAsync()
        {
            return ExecuteCloudOperationAsync(async store =>
            {
                var suggestedName = InstalledModsPdfGenerator.BuildCloudModlistName();
                var configOptions = BuildModConfigOptions(selectByDefault: false);
                var detailsDialog = new CloudModlistDetailsDialog(
                    this,
                    suggestedName,
                    configOptions,
                    _viewModel?.InstalledGameVersion);
                var dialogResult = detailsDialog.ShowDialog();
                if (dialogResult != true) return;

                var uploader = DetermineUploaderName(store);

                var modlistName = detailsDialog.ModlistName;
                var description = detailsDialog.ModlistDescription;
                var version = detailsDialog.ModlistVersion;

                Dictionary<string, IReadOnlyList<ModConfigurationSnapshot>>? includedConfigurations = null;
                var selectedConfigOptions = detailsDialog.GetSelectedConfigOptions();
                includedConfigurations = TryReadModConfigurations(selectedConfigOptions);

                var gameVersion = ResolveGameVersion(detailsDialog.ModlistGameVersion);

                if (!TryBuildCurrentModlistJson(modlistName,
                        description,
                        version,
                        uploader,
                        includedConfigurations,
                        gameVersion,
                        out var json)) return;

                var savePlan =
                    await CloudModlistSaveService.PrepareSaveAsync(
                        store,
                        modlistName);

                var trimmedModlistName = modlistName.Trim();

                CloudModlistSlot? replacementSlot = null;
                string? slotKey = null;

                if (savePlan.MatchingSlot is { } matchingSlot)
                {
                    var slotLabel =
                        CloudModlistHelper.FormatCloudSlotLabel(
                            matchingSlot.SlotKey);

                    var replaceExisting = await _confirmationService.ConfirmAsync(
                            CloudSaveDialogTextBuilder.BuildReplaceExistingPrompt(
                                trimmedModlistName,
                                slotLabel),
                            "Simple VS Manager",
                            DialogSeverity.Question)
                        .ConfigureAwait(true);

                    if (!replaceExisting) return;

                    replacementSlot = matchingSlot;
                    slotKey = matchingSlot.SlotKey;
                }

                if (slotKey is null)
                    slotKey = savePlan.FreeSlot?.SlotKey;

                if (slotKey is null)
                {
                    replacementSlot =
                        PromptForCloudSaveReplacement(
                            savePlan.Slots);

                    if (replacementSlot is null)
                        return;

                    slotKey = replacementSlot.SlotKey;
                }

                await CloudModlistSaveService.SaveAsync(
                    store,
                    slotKey,
                    json);

                if (replacementSlot is not null)
                {
                    var replacedName = replacementSlot.Name ?? "existing modlist";
                    var replacedVersion = replacementSlot.Version;
                    if (!string.IsNullOrWhiteSpace(replacedVersion)) replacedName = $"{replacedName} (v{replacedVersion})";

                    _viewModel?.ReportStatus(
                        CloudSaveDialogTextBuilder.BuildReplacedStatusMessage(
                            replacedName,
                            modlistName));
                }
                else
                {
                    _viewModel?.ReportStatus(
                        CloudSaveDialogTextBuilder.BuildSavedStatusMessage(modlistName));
                }
            }, "save the modlist to the cloud");
        }

    private CloudModlistSlot? PromptForCloudSaveReplacement(IReadOnlyList<CloudModlistSlot> slots)
        {
            if (slots is null) return null;

            var occupiedSlots = slots.Where(slot => slot.IsOccupied).ToList();
            if (occupiedSlots.Count == 0) return null;

            var dialog = new CloudSlotSelectionDialog(this,
                occupiedSlots,
                "Replace Cloud Modlist",
                "Select a cloud modlist to replace.");
            var dialogResult = dialog.ShowDialog();
            return dialogResult == true ? dialog.SelectedSlot : null;
        }

    private async void SaveModlistToCloudMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            await SaveModlistToCloudAsync();
            if (_viewModel?.IsViewingModlistTab == true)
                await RefreshCloudModlistsAsync(true);
            else
                _cloudModlistsLoaded = false;
        }

    private async void SaveCloudModlistButton_OnClick(object sender, RoutedEventArgs e)
        {
            await SaveModlistToCloudAsync();
            if (_viewModel?.IsViewingModlistTab == true) await RefreshCloudModlistsAsync(true);
        }
}
