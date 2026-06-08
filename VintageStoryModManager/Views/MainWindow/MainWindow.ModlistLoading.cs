#nullable enable

using System.IO;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;

using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void LoadModlistMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var modListDirectory = EnsureModListDirectory();
        var dialog = new OpenFileDialog
        {
            Title = "Load Modlist",
            Filter =
                "Modlist files (*.json;*.pdf)|*.json;*.pdf|JSON files (*.json)|*.json|PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            DefaultExt = ".json",
            InitialDirectory = modListDirectory,
            Multiselect = false
        };

        var dialogResult = dialog.ShowDialog(this);
        if (dialogResult != true) return;

        await LoadModlistFromFileAsync(dialog.FileName).ConfigureAwait(true);
    }

    private async Task LoadModlistFromFileAsync(string filePath)
    {
        if (_viewModel is null) return;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            WpfMessageBox.Show(
                "The selected file could not be found.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var loadMode = PromptModlistLoadMode();
        if (loadMode is not ModlistLoadMode mode) return;

        if (mode == ModlistLoadMode.Replace && !EnsureModlistBackupBeforeLoad()) return;

        PrepareForModlistLoad();

        var loadOptions = GetModlistLoadOptions(mode);

        if (!PresetFileLoader.TryLoadPresetFromFile(filePath, "Modlist", loadOptions, out var preset, out var errorMessage))
        {
            var message = "The file is not a valid SVSM modlist.";
            if (!string.IsNullOrWhiteSpace(errorMessage)) message += $"\n{errorMessage}";

            WpfMessageBox.Show(
                message,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var loadedModlist = preset!;
        await CreateAutomaticBackupAsync("ModlistLoaded").ConfigureAwait(true);
        await ApplyPresetAsync(loadedModlist);
        var status = mode == ModlistLoadMode.Replace
            ? $"Loaded modlist \"{loadedModlist.Name}\"."
            : $"Added mods from modlist \"{loadedModlist.Name}\".";
        _viewModel?.ReportStatus(status);
    }

    private void MainWindow_OnPreviewDragEnter(object sender, DragEventArgs e)
    {
        HandleModlistDragEvent(e);
    }

    private void MainWindow_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        HandleModlistDragEvent(e);
    }

    private async void MainWindow_OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (!ModlistDropHelper.TryGetDroppedModlistFile(e, out var filePath))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Handled = true;
        e.Effects = DragDropEffects.Copy;

        await LoadModlistFromFileAsync(filePath!).ConfigureAwait(true);
    }

    private void HandleModlistDragEvent(DragEventArgs e)
    {
        e.Handled = true;

        if (ModlistDropHelper.TryGetDroppedModlistFile(e, out _))
        {
            e.Effects = DragDropEffects.Copy;
            return;
        }

        e.Effects = DragDropEffects.None;
    }

    private void SwitchToInstalledModsTab()
    {
        if (_viewModel?.ShowMainTabCommand?.CanExecute(null) == true)
            _viewModel.ShowMainTabCommand.Execute(null);
    }

    private void PrepareForModlistLoad()
    {
        SwitchToInstalledModsTab();
        ClearSelection(true);
    }

    private void UpdateModlistLoadingUiState()
    {
        var isEnabled = !_isApplyingPreset;

        if (UpdateAllButton != null) UpdateAllButton.IsEnabled = isEnabled;

        if (LaunchGameButton != null) LaunchGameButton.IsEnabled = isEnabled;

        if (PresetsAndModlistsMenuItem != null) PresetsAndModlistsMenuItem.IsEnabled = isEnabled;

        if (ModsDataGrid != null)
        {
            if (isEnabled)
                ModsDataGrid.ClearValue(IsEnabledProperty);
            else
                ModsDataGrid.IsEnabled = false;
        }

    }
}
