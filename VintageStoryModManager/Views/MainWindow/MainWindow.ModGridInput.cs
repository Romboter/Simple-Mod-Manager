#nullable enable

using ModernWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VintageStoryModManager.ViewModels;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListViewItem = System.Windows.Controls.ListViewItem;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using TextBox = System.Windows.Controls.TextBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ModsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // We use custom selection tracking via _selectedMods list instead of DataGrid's native selection.
            // Clear any DataGrid selection to prevent it from interfering with our custom selection.
            // With IsSynchronizedWithCurrentItem=False, this should only fire on direct user clicks,
            // which we handle in PreviewMouseLeftButtonDown.
            if (sender is DataGrid dataGrid)
            {
                dataGrid.SelectedIndex = -1;
                dataGrid.UnselectAll();
                dataGrid.UnselectAllCells();
            }
        }

    private async void ModsDataGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (await TryHandleModListKeyDownAsync(e)) e.Handled = true;
        }

    private async void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (await TryHandleModListKeyDownAsync(e)) e.Handled = true;
        }

    private async Task<bool> TryHandleModListKeyDownAsync(KeyEventArgs e)
        {
            if (_isApplyingPreset) return true;

            if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Allow CTRL+A to select text when a TextBox has focus
                if (Keyboard.FocusedElement is TextBox)
                    return false;

                if (ModsDataGrid?.IsVisible == true)
                {
                    SelectAllModsInCurrentView();
                    return true;
                }

                return false;
            }

            if (e.Key == Key.Delete)
            {
                var modifiers = Keyboard.Modifiers;
                if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == ModifierKeys.None &&
                    _selectedMods.Count > 0)
                {
                    await DeleteSelectedModsAsync();
                    return true;
                }
            }

            return false;
        }

    private void ModsDataGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isApplyingPreset)
            {
                e.Handled = true;
                return;
            }

            if (e.Handled) return;

            if (sender is not DataGrid) return;

            if (ShouldIgnoreRowSelection(e.OriginalSource as DependencyObject)) return;

            var source = e.OriginalSource as DependencyObject;
            if (FindAncestor<DataGridRow>(source) != null) return;

            if (FindAncestor<DataGridColumnHeader>(source) != null) return;

            if (FindAncestor<ScrollBar>(source) != null) return;

            ClearSelection(true);
        }

    private void ModsDataGridRow_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isApplyingPreset)
            {
                e.Handled = true;
                return;
            }

            if (ShouldIgnoreRowSelection(e.OriginalSource as DependencyObject)) return;

            if (sender is not DataGridRow row || row.DataContext is not ModListItemViewModel mod) return;

            row.Focus();
            HandleModRowSelection(mod);
            e.Handled = true;
        }

    private void ModDatabaseCard_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isApplyingPreset)
            {
                e.Handled = true;
                return;
            }

            if (ShouldIgnoreRowSelection(e.OriginalSource as DependencyObject)) return;

            if (sender is not ListViewItem item || item.DataContext is not ModListItemViewModel mod) return;

            item.Focus();
            HandleModRowSelection(mod);
            e.Handled = true;
        }

    private bool ShouldIgnoreRowSelection(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is ButtonBase or ToggleButton || source is ToggleSwitch) return true;

                if (source is FrameworkElement { TemplatedParent: ToggleSwitch }) return true;

                if (source is DataGridCell cell && ReferenceEquals(cell.Column, ActiveColumn)) return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }
}
