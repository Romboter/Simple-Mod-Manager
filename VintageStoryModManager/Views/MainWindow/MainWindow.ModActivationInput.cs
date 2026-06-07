#nullable enable

using ModernWpf.Controls;
using System.Windows;
using System.Windows.Input;
using VintageStoryModManager.ViewModels;

using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void ActiveToggle_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ToggleSwitch toggleSwitch) return;

        if (!toggleSwitch.IsEnabled) return;

        e.Handled = true;

        toggleSwitch.Focus();
        toggleSwitch.IsOn = !toggleSwitch.IsOn;
    }

    private void ActiveToggle_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleSwitch) e.Handled = true;
    }

    private void ActiveToggle_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ToggleSwitch || e.LeftButton != MouseButtonState.Pressed) return;

        e.Handled = true;
    }

    private void ActiveToggle_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingMultiToggle) return;

        if (sender is not ToggleSwitch { DataContext: ModListItemViewModel mod }) return;

        if (!_selectedMods.Contains(mod) || _selectedMods.Count <= 1) return;

        var desiredState = mod.IsActive;

        try
        {
            _isApplyingMultiToggle = true;

            foreach (var selected in _selectedMods)
            {
                if (ReferenceEquals(selected, mod)) continue;

                if (!selected.CanToggle || selected.IsActive == desiredState) continue;

                selected.IsActive = desiredState;
            }
        }
        finally
        {
            _isApplyingMultiToggle = false;
        }
    }
}
