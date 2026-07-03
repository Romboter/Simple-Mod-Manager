#nullable enable

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ModsDataGridRow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        row.DataContextChanged -= ModsDataGridRow_OnDataContextChanged;
        row.DataContextChanged += ModsDataGridRow_OnDataContextChanged;
        UpdateRowModSubscription(row, row.DataContext as ModListItemViewModel);
        SetRowIsHovered(row, row.IsMouseOver);

        // Skip overlay reset during loading to reduce UI overhead
        // XAML triggers will handle initial state correctly
        if (!AreHoverOverlaysSuppressed())
            ResetRowOverlays(row);
    }

    private void ModsDataGridRow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        UpdateRowModSubscription(row, e.NewValue as ModListItemViewModel);

        // Skip overlay reset during loading to reduce UI overhead
        if (!AreHoverOverlaysSuppressed())
            ResetRowOverlays(row);
    }

    private void ModsDataGridRow_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        row.DataContextChanged -= ModsDataGridRow_OnDataContextChanged;
        UpdateRowModSubscription(row, null);
        // Skip overlay cleanup during loading - overlays will be recreated anyway
        if (!AreHoverOverlaysSuppressed())
            ClearRowOverlayValues(row);
        SetRowIsHovered(row, false);
    }

    private void ModsDataGridRow_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        SetRowIsHovered(row, true);

        // Skip overlay updates during loading - XAML triggers handle hover via storyboards
        if (AreHoverOverlaysSuppressed()) return;

        ResetRowOverlays(row);
    }

    private void ModsDataGridRow_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not DataGridRow row) return;

        SetRowIsHovered(row, false);

        // Skip overlay updates during loading - XAML triggers handle hover via storyboards
        if (AreHoverOverlaysSuppressed()) return;

        ResetRowOverlays(row);
    }

    private static void ResetRowOverlays(DataGridRow row)
    {
        // Avoid expensive ApplyTemplate() call - template is already applied when row is loaded
        // and FindName works without calling ApplyTemplate on an already-loaded row.
        if (row.Template is null) return;

        var selectionOverlay = row.Template.FindName("SelectionOverlay", row) as Border;
        var hoverOverlay = row.Template.FindName("HoverOverlay", row) as Border;

        if (selectionOverlay == null && hoverOverlay == null) return;

        if (row.DataContext is not ModListItemViewModel mod)
        {
            selectionOverlay?.ClearValue(OpacityProperty);
            hoverOverlay?.ClearValue(OpacityProperty);
            return;
        }

        var isModSelected = mod.IsSelected || row.IsSelected;
        var isHovered = GetRowIsHovered(row);

        if (selectionOverlay != null)
        {
            var targetOpacity = isModSelected ? SelectionOverlayOpacity : 0;
            selectionOverlay.Opacity = targetOpacity;
        }

        if (hoverOverlay != null)
        {
            var shouldShowHover =
                isHovered
                && !isModSelected
                && !AreHoverEffectsDisabled(row)
                && !AreHoverOverlaysSuppressed(row);
            hoverOverlay.Opacity = shouldShowHover ? HoverOverlayOpacity : 0;
        }
    }

    private bool AreHoverOverlaysSuppressed()
    {
        return _viewModel?.IsLoadingMods == true || _viewModel?.IsLoadingModDetails == true;
    }

    private static bool AreHoverOverlaysSuppressed(DataGridRow row)
    {
        return GetWindow(row) is MainWindow mainWindow && mainWindow.AreHoverOverlaysSuppressed();
    }

    private static bool AreHoverEffectsDisabled(DependencyObject dependencyObject)
    {
        return HoverEffectHelper.GetDisableHoverEffects(dependencyObject);
    }

    private static void ClearRowOverlayValues(DataGridRow row)
    {
        // Avoid expensive ApplyTemplate() call - template is already applied when row is loaded
        if (row.Template is null) return;

        if (row.Template.FindName("SelectionOverlay", row) is Border selectionOverlay)
            selectionOverlay.ClearValue(OpacityProperty);

        if (row.Template.FindName("HoverOverlay", row) is Border hoverOverlay)
            hoverOverlay.ClearValue(OpacityProperty);
    }

    private void DisableHoverEffectsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetDisableHoverEffects(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.DisableHoverEffects;

        // Set the attached property on the main window to disable hover effects globally
        HoverEffectHelper.SetDisableHoverEffects(this, menuItem.IsChecked);

        RefreshHoverOverlayState();
    }

    private void RefreshHoverOverlayState()
    {
        RefreshRowHoverOverlays(ModsDataGrid);
        RefreshRowHoverOverlays(CloudModlistsDataGrid);
    }

    private static void RefreshRowHoverOverlays(DataGrid? dataGrid)
    {
        if (dataGrid == null) return;

        var generator = dataGrid.ItemContainerGenerator;
        foreach (var item in dataGrid.Items)
            if (generator.ContainerFromItem(item) is DataGridRow row)
                ResetRowOverlays(row);
    }

    private static void UpdateRowModSubscription(DataGridRow row, ModListItemViewModel? newMod)
    {
        if (GetBoundMod(row) is { } oldMod && GetBoundModHandler(row) is { } oldHandler)
            oldMod.PropertyChanged -= oldHandler;

        if (newMod is not null)
        {
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (args.PropertyName == nameof(ModListItemViewModel.IsSelected))
                {
                    // Skip overlay updates during loading to reduce UI overhead
                    if (AreHoverOverlaysSuppressed(row)) return;

                    if (row.Dispatcher.CheckAccess())
                        ResetRowOverlays(row);
                    else
                        row.Dispatcher.Invoke(() => ResetRowOverlays(row));
                }
            };

            newMod.PropertyChanged += handler;
            SetBoundModHandler(row, handler);
        }
        else
        {
            SetBoundModHandler(row, null);
        }

        SetBoundMod(row, newMod);
    }

    private static void SetBoundMod(DataGridRow row, ModListItemViewModel? mod)
    {
        row.SetValue(BoundModProperty, mod);
    }

    private static ModListItemViewModel? GetBoundMod(DataGridRow row)
    {
        return (ModListItemViewModel?)row.GetValue(BoundModProperty);
    }

    private static void SetBoundModHandler(DataGridRow row, PropertyChangedEventHandler? handler)
    {
        row.SetValue(BoundModHandlerProperty, handler);
    }

    private static PropertyChangedEventHandler? GetBoundModHandler(DataGridRow row)
    {
        return (PropertyChangedEventHandler?)row.GetValue(BoundModHandlerProperty);
    }

    private static void SetRowIsHovered(DataGridRow row, bool value)
    {
        row.SetValue(RowIsHoveredProperty, value);
    }

    private static bool GetRowIsHovered(DataGridRow row)
    {
        return (bool)row.GetValue(RowIsHoveredProperty);
    }
}
