#nullable enable

using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void DeleteModButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button) return;

        if (button.DataContext is ModListItemViewModel mod)
        {
            e.Handled = true;
            await DeleteSingleModAsync(mod);
            return;
        }

        if (_selectedMods.Count == 0) return;

        e.Handled = true;
        await DeleteSelectedModsAsync();
    }

    private async Task DeleteSelectedModsAsync()
    {
        if (_selectedMods.Count == 0) return;

        if (_selectedMods.Count == 1)
        {
            await DeleteSingleModAsync(_selectedMods[0]);
            return;
        }

        var modsToDelete = _selectedMods.ToList();
        await DeleteMultipleModsAsync(modsToDelete);
    }

    private async Task DeleteSingleModAsync(ModListItemViewModel mod)
    {
        if (!TryGetManagedModPath(mod, out var modPath, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                await _confirmationService.NotifyAsync(
                        errorMessage!,
                        "Simple VS Manager",
                        DialogSeverity.Information)
                    .ConfigureAwait(true);

            return;
        }

        var confirmation = await _confirmationService.ConfirmAsync(
                ModOperationDialogTextBuilder.BuildDeleteConfirmationMessage(
                    mod.DisplayName),
                "Simple VS Manager",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!confirmation) return;

        await CreateAutomaticBackupAsync("ModsDeleted").ConfigureAwait(true);

        var removed = await TryDeleteModAtPathAsync(mod, modPath)
            .ConfigureAwait(true);

        if (_viewModel?.RefreshCommand != null)
            try
            {
                await RefreshModsAsync();
            }
            catch (Exception ex)
            {
                await _confirmationService.NotifyAsync(
                        ModOperationDialogTextBuilder.BuildRefreshAfterDeleteFailureMessage(
                            ex.Message),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
            }

        if (removed) _viewModel?.ReportStatus($"Deleted {mod.DisplayName}.");
    }

    private async Task DeleteMultipleModsAsync(IReadOnlyList<ModListItemViewModel> mods)
    {
        if (mods.Count == 0) return;

        List<(ModListItemViewModel Mod, string Path)> deletable = new();
        foreach (var mod in mods)
        {
            if (!TryGetManagedModPath(mod, out var modPath, out var errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    await _confirmationService.NotifyAsync(
                            errorMessage!,
                            "Simple VS Manager",
                            DialogSeverity.Information)
                        .ConfigureAwait(true);

                continue;
            }

            deletable.Add((mod, modPath));
        }

        if (deletable.Count == 0) return;

        var confirmation = await _confirmationService.ConfirmAsync(
                ModDeletionPromptBuilder.BuildConfirmationMessage(
                    deletable.Select(d => d.Mod.DisplayName).ToList()),
                "Simple VS Manager",
                DialogSeverity.Warning)
            .ConfigureAwait(true);

        if (!confirmation) return;

        await CreateAutomaticBackupAsync("ModsDeleted").ConfigureAwait(true);

        var removedCount = 0;
        foreach (var (mod, path) in deletable)
            if (await TryDeleteModAtPathAsync(mod, path)
                    .ConfigureAwait(true))
                removedCount++;

        if (_viewModel?.RefreshCommand != null)
            try
            {
                await RefreshModsAsync();
            }
            catch (Exception ex)
            {
                await _confirmationService.NotifyAsync(
                        ModOperationDialogTextBuilder.BuildRefreshAfterDeleteFailureMessage(
                            ex.Message),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
            }

        if (removedCount > 0)
            _viewModel?.ReportStatus($"Deleted {removedCount} mod{(removedCount == 1 ? string.Empty : "s")}.");
    }

    private async Task<bool> TryDeleteModAtPathAsync(ModListItemViewModel mod, string modPath)
    {
        var removed = false;
        try
        {
            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, true);
                removed = true;
            }
            else if (File.Exists(modPath))
            {
                File.Delete(modPath);
                removed = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.BuildDeleteFailureMessage(
                        mod.DisplayName,
                        ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return false;
        }

        if (!removed)
        {
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.BuildMissingDeletedModMessage(
                        modPath),
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return false;
        }

        _userConfiguration.RemoveModConfigPath(mod.ModId, true);
        _modActivityLoggingService.LogModDeletion(mod.DisplayName ?? mod.ModId ?? "Unknown");
        return true;
    }
}
