#nullable enable

using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private IReadOnlyList<ModListItemViewModel> _selectedMods => _modSelection.SelectedMods;

    private ModListItemViewModel? _selectionAnchor => _modSelection.SelectionAnchor;

    private void HandleModRowSelection(ModListItemViewModel mod)
    {
        _modSelection.HandleModRowSelection(mod, _isApplyingPreset, GetModsInViewOrder());
    }

    private void SelectAllModsInCurrentView()
    {
        _modSelection.SelectAllModsInCurrentView(_isApplyingPreset, GetModsInViewOrder());
    }

    private List<ModListItemViewModel> GetModsInViewOrder()
    {
        var view = _viewModel?.CurrentModsView;
        if (view == null) return new List<ModListItemViewModel>();

        return view.Cast<ModListItemViewModel>().ToList();
    }

    private void AddToSelection(ModListItemViewModel mod)
    {
        _modSelection.AddToSelection(mod);
    }

    private void RemoveFromSelection(ModListItemViewModel mod)
    {
        _modSelection.RemoveFromSelection(mod);
    }

    private void ClearSelection(bool resetAnchor = false)
    {
        _modSelection.ClearSelection(resetAnchor);
    }

    private void ClearModDatabaseSelections()
    {
        _modSelection.ClearModDatabaseSelections();
    }

    private void RestoreSelectionFromSourcePaths(IReadOnlyList<string> sourcePaths, string? anchorSourcePath)
    {
        if (_viewModel is null) return;

        var resolved = new List<ModListItemViewModel>(sourcePaths.Count);
        foreach (var path in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var current = _viewModel.FindModBySourcePath(path);
            if (current != null && !resolved.Contains(current)) resolved.Add(current);
        }

        _modSelection.RestoreSelection(resolved, anchorSourcePath);
    }

    private void UpdateSelectedModButtons()
    {
        var selectionCount = _selectedMods.Count;
        var singleSelection = selectionCount == 1 ? _selectedMods[0] : null;
        var hasMultipleSelection = selectionCount > 1;

        if (hasMultipleSelection)
        {
            UpdateSelectedModButton(SelectedModDatabasePageButton, null, true);
            UpdateSelectedModButton(SelectedModUpdateButton, null, false, true);
            UpdateSelectedModEditConfigButton(null);
            UpdateSelectedModFixButton(null);
            UpdateSelectedModCopyForServerButton(null);

            if (SelectedModDeleteButton is not null)
            {
                var allowDeletion = true;
                SelectedModDeleteButton.DataContext = null;
                SelectedModDeleteButton.Visibility = allowDeletion ? Visibility.Visible : Visibility.Collapsed;
                SelectedModDeleteButton.IsEnabled = allowDeletion;
            }
        }
        else
        {
            UpdateSelectedModButton(SelectedModDatabasePageButton, singleSelection, true);
            UpdateSelectedModButton(SelectedModUpdateButton, singleSelection, false, true);
            UpdateSelectedModEditConfigButton(singleSelection);
            UpdateSelectedModButton(SelectedModDeleteButton, singleSelection, false);
            UpdateSelectedModFixButton(singleSelection);
            UpdateSelectedModCopyForServerButton(singleSelection);
        }

        _viewModel?.SetSelectedMod(singleSelection, selectionCount);
    }

    private void UpdateSelectedModFixButton(ModListItemViewModel? mod)
    {
        if (SelectedModFixButton is null) return;

        if (mod is null || !mod.CanFixDependencyIssues || _isModUpdateInProgress)
        {
            SelectedModFixButton.DataContext = null;
            SelectedModFixButton.Visibility = Visibility.Collapsed;
            SelectedModFixButton.IsEnabled = false;
            return;
        }

        SelectedModFixButton.DataContext = mod;
        SelectedModFixButton.Visibility = Visibility.Visible;
        SelectedModFixButton.IsEnabled = true;
    }

    private void UpdateSelectedModCopyForServerButton(ModListItemViewModel? mod)
    {
        if (SelectedModCopyForServerButton is null) return;

        if (!_userConfiguration.EnableServerOptions || mod is null)
        {
            SelectedModCopyForServerButton.DataContext = null;
            SelectedModCopyForServerButton.Visibility = Visibility.Collapsed;
            SelectedModCopyForServerButton.IsEnabled = false;
            return;
        }

        var command = ServerCommandBuilder.TryBuildInstallCommand(mod.ModId, mod.Version);
        if (string.IsNullOrWhiteSpace(command))
        {
            SelectedModCopyForServerButton.DataContext = null;
            SelectedModCopyForServerButton.Visibility = Visibility.Collapsed;
            SelectedModCopyForServerButton.IsEnabled = false;
            return;
        }

        SelectedModCopyForServerButton.DataContext = mod;
        SelectedModCopyForServerButton.Visibility = Visibility.Visible;
        SelectedModCopyForServerButton.IsEnabled = true;
    }

    private static void UpdateSelectedModButton(WpfButton? button, ModListItemViewModel? mod,
            bool requireModDatabaseLink, bool requireUpdate = false)
    {
        if (button is null) return;

        if (mod is null
            || (requireModDatabaseLink && !mod.HasModDatabasePageLink)
            || (requireUpdate && !mod.CanUpdate))
        {
            button.DataContext = null;
            button.Visibility = Visibility.Collapsed;
            button.IsEnabled = false;
            return;
        }

        button.DataContext = mod;
        button.Visibility = Visibility.Visible;
        button.IsEnabled = true;
    }

    private void UpdateSelectedModEditConfigButton(ModListItemViewModel? mod)
    {
        if (SelectedModEditConfigButton is null) return;

        UpdateSelectedModButton(SelectedModEditConfigButton, mod, false);

        if (SelectedModEditConfigButton.DataContext is not ModListItemViewModel context)
        {
            SelectedModEditConfigButton.ToolTip = null;
            SelectedModEditConfigButton.Content = "Edit Config";
            return;
        }

        var hasConfigPath = !string.IsNullOrWhiteSpace(context.ModId)
                            && _userConfiguration.TryGetModConfigPath(context.ModId, out var path)
                            && !string.IsNullOrWhiteSpace(path);

        SelectedModEditConfigButton.Content = hasConfigPath ? "Edit Config" : "Set Config...";
        SelectedModEditConfigButton.ToolTip = hasConfigPath
            ? $"Edit Config for {context.DisplayName}"
            : $"Set Config for {context.DisplayName}";
    }

    private void CloseModInfoButton_OnClick(object sender, RoutedEventArgs e)
    {
        ClearSelection(resetAnchor: true);
        e.Handled = true;
    }
}
