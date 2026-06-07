#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using WpfButton = System.Windows.Controls.Button;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void HandleModRowSelection(ModListItemViewModel mod)
        {
            if (_isApplyingPreset) return;

            var isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (isShiftPressed)
            {
                if (_selectionAnchor is not { } anchor)
                {
                    if (!isCtrlPressed) ClearSelection();

                    AddToSelection(mod);
                    _selectionAnchor = mod;
                    return;
                }

                var anchorApplied = ApplyRangeSelection(anchor, mod, isCtrlPressed);
                if (!anchorApplied) _selectionAnchor = mod;

                return;
            }

            if (isCtrlPressed)
            {
                if (_selectedMods.Contains(mod))
                {
                    RemoveFromSelection(mod);
                    _selectionAnchor = mod;
                }
                else
                {
                    AddToSelection(mod);
                    _selectionAnchor = mod;
                }

                return;
            }

            ClearSelection();
            AddToSelection(mod);
            _selectionAnchor = mod;
        }

    private void SelectAllModsInCurrentView()
        {
            if (_isApplyingPreset) return;

            var mods = GetModsInViewOrder();
            ClearSelection(true);

            if (mods.Count == 0) return;

            foreach (var mod in mods) AddToSelection(mod);

            _selectionAnchor = mods[mods.Count - 1];
        }

    private bool ApplyRangeSelection(ModListItemViewModel start, ModListItemViewModel end, bool preserveExisting)
        {
            var mods = GetModsInViewOrder();
            var startIndex = mods.IndexOf(start);
            var endIndex = mods.IndexOf(end);

            if (startIndex < 0 || endIndex < 0)
            {
                if (!preserveExisting) ClearSelection();

                AddToSelection(end);
                return false;
            }

            if (!preserveExisting) ClearSelection();

            if (startIndex > endIndex) (startIndex, endIndex) = (endIndex, startIndex);

            for (var i = startIndex; i <= endIndex; i++) AddToSelection(mods[i]);

            return true;
        }

    private List<ModListItemViewModel> GetModsInViewOrder()
        {
            var view = _viewModel?.CurrentModsView;
            if (view == null) return new List<ModListItemViewModel>();

            return view.Cast<ModListItemViewModel>().ToList();
        }

    private void AddToSelection(ModListItemViewModel mod)
        {
            if (_selectedMods.Contains(mod)) return;

            _selectedMods.Add(mod);
            SubscribeToSelectedMod(mod);
            mod.IsSelected = true;
            UpdateSelectedModButtons();
        }

    private void RemoveFromSelection(ModListItemViewModel mod)
        {
            if (!_selectedMods.Remove(mod)) return;

            mod.IsSelected = false;
            UnsubscribeFromSelectedMod(mod);
            UpdateSelectedModButtons();
        }

    private void ClearSelection(bool resetAnchor = false)
        {
            if (_selectedMods.Count > 0)
            {
                foreach (var mod in _selectedMods)
                {
                    mod.IsSelected = false;
                    UnsubscribeFromSelectedMod(mod);
                }

                _selectedMods.Clear();
            }

            if (resetAnchor) _selectionAnchor = null;

            UpdateSelectedModButtons();
        }

    private void ClearModDatabaseSelections()
        {
            if (_selectedMods.Count > 0)
            {
                var removedAny = false;

                for (var i = _selectedMods.Count - 1; i >= 0; i--)
                {
                    var mod = _selectedMods[i];
                    if (!mod.IsModDatabaseEntry) continue;

                    _selectedMods.RemoveAt(i);
                    mod.IsSelected = false;
                    UnsubscribeFromSelectedMod(mod);
                    removedAny = true;
                }

                if (removedAny)
                {
                    if (_selectionAnchor is { } anchor && anchor.IsModDatabaseEntry) _selectionAnchor = null;
                    UpdateSelectedModButtons();
                }
            }
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

            var selectionChanged = resolved.Count != _selectedMods.Count;
            if (!selectionChanged)
                for (var i = 0; i < resolved.Count; i++)
                    if (!ReferenceEquals(resolved[i], _selectedMods[i]))
                    {
                        selectionChanged = true;
                        break;
                    }

            if (!selectionChanged)
            {
                UpdateSelectionAnchorAfterRestore(resolved, anchorSourcePath);
                return;
            }

            foreach (var mod in _selectedMods)
            {
                mod.IsSelected = false;
                UnsubscribeFromSelectedMod(mod);
            }

            _selectedMods.Clear();

            foreach (var mod in resolved)
            {
                _selectedMods.Add(mod);
                mod.IsSelected = true;
                SubscribeToSelectedMod(mod);
            }

            UpdateSelectionAnchorAfterRestore(resolved, anchorSourcePath);
            UpdateSelectedModButtons();
        }

    private void UpdateSelectionAnchorAfterRestore(IReadOnlyList<ModListItemViewModel> selection,
            string? anchorSourcePath)
        {
            if (selection.Count == 0)
            {
                _selectionAnchor = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(anchorSourcePath))
                foreach (var mod in selection)
                    if (string.Equals(mod.SourcePath, anchorSourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectionAnchor = mod;
                        return;
                    }

            _selectionAnchor = selection[selection.Count - 1];
        }

    private void SubscribeToSelectedMod(ModListItemViewModel mod)
        {
            if (_selectedModPropertyHandlers.ContainsKey(mod)) return;

            PropertyChangedEventHandler handler = (_, args) =>
            {
                var shouldRefreshFixButton = string.IsNullOrEmpty(args.PropertyName)
                                             || args.PropertyName == nameof(ModListItemViewModel.CanFixDependencyIssues)
                                             || args.PropertyName == nameof(ModListItemViewModel.HasDependencyIssues)
                                             || args.PropertyName == nameof(ModListItemViewModel.MissingDependencies)
                                             || args.PropertyName == nameof(ModListItemViewModel.DependencyHasErrors);

                var shouldRefreshCopyButton = string.IsNullOrEmpty(args.PropertyName)
                                              || args.PropertyName == nameof(ModListItemViewModel.Version);

                if (!shouldRefreshFixButton && !shouldRefreshCopyButton) return;

                void RefreshButtons()
                {
                    if (shouldRefreshFixButton) RefreshSelectedModFixButton(mod);

                    if (shouldRefreshCopyButton) RefreshSelectedModCopyForServerButton(mod);
                }

                if (Dispatcher.CheckAccess())
                    RefreshButtons();
                else
                    Dispatcher.Invoke(RefreshButtons);
            };

            mod.PropertyChanged += handler;
            _selectedModPropertyHandlers[mod] = handler;
        }

    private void UnsubscribeFromSelectedMod(ModListItemViewModel mod)
        {
            if (_selectedModPropertyHandlers.TryGetValue(mod, out var handler))
            {
                mod.PropertyChanged -= handler;
                _selectedModPropertyHandlers.Remove(mod);
            }
        }

    private void RefreshSelectedModFixButton(ModListItemViewModel mod)
        {

            if (_selectedMods.Count == 1 && ReferenceEquals(_selectedMods[0], mod)) UpdateSelectedModFixButton(mod);
        }

    private void RefreshSelectedModCopyForServerButton(ModListItemViewModel mod)
        {

            if (_selectedMods.Count == 1 && ReferenceEquals(_selectedMods[0], mod))
                UpdateSelectedModCopyForServerButton(mod);
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
