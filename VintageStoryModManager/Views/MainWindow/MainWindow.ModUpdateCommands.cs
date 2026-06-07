#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;

using ComboBox = System.Windows.Controls.ComboBox;
using Popup = System.Windows.Controls.Primitives.Popup;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void UpdateModButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isModUpdateInProgress) return;

        if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

        e.Handled = true;

        IReadOnlyDictionary<ModListItemViewModel, ModReleaseInfo>? overrides = null;
        if (mod.SelectedVersionOption is { Release: { } selectedRelease, IsInstalled: false })
            overrides = new Dictionary<ModListItemViewModel, ModReleaseInfo>
            {
                [mod] = selectedRelease
            };

        await UpdateModsAsync(new[] { mod }, false, overrides);
    }

    private async void SelectedModVersionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isModUpdateInProgress) return;

        if (sender is not ComboBox comboBox) return;

        if (!comboBox.IsDropDownOpen && !comboBox.IsKeyboardFocusWithin) return;

        if (_viewModel?.SelectedMod is not ModListItemViewModel mod) return;

        if (comboBox.SelectedItem is not ModVersionOptionViewModel option) return;

        if (option.IsInstalled || option.Release is null) return;

        if (string.Equals(mod.Version, option.Version, StringComparison.OrdinalIgnoreCase)) return;

        var overrides = new Dictionary<ModListItemViewModel, ModReleaseInfo>
        {
            [mod] = option.Release
        };

        // Use isBulk=true to leverage the modlist install UI overlay for smooth progress feedback
        // and to prevent UI freezing. Pass showSummary=false to skip the bulk changelog dialog.
        await UpdateModsAsync(new[] { mod }, isBulk: true, overrides, showSummary: false);
    }

    private void SelectedModVersionComboBox_OnDropDownOpened(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox) return;

        void ScrollToTop()
        {
            ScrollViewer? scrollViewer = null;

            if (comboBox.Template?.FindName("Popup", comboBox) is Popup popup)
                scrollViewer = FindDescendantScrollViewer(popup.Child);

            scrollViewer ??= FindDescendantScrollViewer(comboBox);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToHome();
                scrollViewer.ScrollToVerticalOffset(0);
            }
        }

        comboBox.Dispatcher.BeginInvoke((Action)ScrollToTop, DispatcherPriority.Background);
    }

    private async void UpdateAllModsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreset) return;

        if (_isModUpdateInProgress || _viewModel?.ModsView == null) return;

        var mods = _viewModel.ModsView.Cast<ModListItemViewModel>()
            .Where(mod => mod.CanUpdate)
            .ToList();

        Dictionary<ModListItemViewModel, ModReleaseInfo>? overrides = null;
        foreach (var mod in mods)
            if (mod.SelectedVersionOption is { Release: { } selectedRelease, IsInstalled: false })
            {
                overrides ??= new Dictionary<ModListItemViewModel, ModReleaseInfo>();
                overrides[mod] = selectedRelease;
            }

        if (mods.Count == 0)
        {
            WpfMessageBox.Show("All mods are already up to date.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new UpdateModsDialog(_userConfiguration, mods, overrides)
        {
            Owner = this
        };

        var dialogResult = dialog.ShowDialog();
        if (dialogResult != true) return;

        var selectedMods = dialog.SelectedMods;
        if (selectedMods.Count == 0) return;

        Dictionary<ModListItemViewModel, ModReleaseInfo>? selectedOverrides = null;
        if (overrides != null)
            foreach (var mod in selectedMods)
                if (overrides.TryGetValue(mod, out var release) && release != null)
                {
                    selectedOverrides ??= new Dictionary<ModListItemViewModel, ModReleaseInfo>();
                    selectedOverrides[mod] = release;
                }

        await CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait(true);
        await UpdateModsAsync(selectedMods, true, selectedOverrides).ConfigureAwait(true);
    }
}
