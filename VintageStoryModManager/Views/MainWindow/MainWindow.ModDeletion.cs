#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
                WpfMessageBox.Show(errorMessage!,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

            return;
        }

        var confirmation = WpfMessageBox.Show(
            $"Are you sure you want to delete {mod.DisplayName}? This will remove the mod from disk.",
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        await CreateAutomaticBackupAsync("ModsDeleted").ConfigureAwait(true);

        var removed = TryDeleteModAtPath(mod, modPath);

        if (_viewModel?.RefreshCommand != null)
            try
            {
                await RefreshModsAsync();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"The mod list could not be refreshed:{Environment.NewLine}{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                    WpfMessageBox.Show(errorMessage!,
                        "Simple VS Manager",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                continue;
            }

            deletable.Add((mod, modPath));
        }

        if (deletable.Count == 0) return;

        StringBuilder confirmationBuilder = new();
        confirmationBuilder.Append(
            $"Are you sure you want to delete {deletable.Count} mods? This will remove them from disk.");
        confirmationBuilder.AppendLine();
        confirmationBuilder.AppendLine();

        const int maxListedMods = 10;
        var listedCount = 0;
        foreach (var (mod, _) in deletable)
        {
            if (listedCount >= maxListedMods) break;

            confirmationBuilder.AppendLine($"• {mod.DisplayName}");
            listedCount++;
        }

        if (deletable.Count > maxListedMods) confirmationBuilder.AppendLine("• …");

        var confirmation = WpfMessageBox.Show(
            confirmationBuilder.ToString(),
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes) return;

        await CreateAutomaticBackupAsync("ModsDeleted").ConfigureAwait(true);

        var removedCount = 0;
        foreach (var (mod, path) in deletable)
            if (TryDeleteModAtPath(mod, path))
                removedCount++;

        if (_viewModel?.RefreshCommand != null)
            try
            {
                await RefreshModsAsync();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"The mod list could not be refreshed:{Environment.NewLine}{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

        if (removedCount > 0)
            _viewModel?.ReportStatus($"Deleted {removedCount} mod{(removedCount == 1 ? string.Empty : "s")}.");
    }

    private bool TryDeleteModAtPath(ModListItemViewModel mod, string modPath)
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
            WpfMessageBox.Show($"Failed to delete {mod.DisplayName}:{Environment.NewLine}{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        if (!removed)
        {
            WpfMessageBox.Show(
                $"The mod could not be found at:{Environment.NewLine}{modPath}{Environment.NewLine}It may have already been removed.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        _userConfiguration.RemoveModConfigPath(mod.ModId, true);
        _modActivityLoggingService.LogModDeletion(mod.DisplayName ?? mod.ModId ?? "Unknown");
        return true;
    }
}
