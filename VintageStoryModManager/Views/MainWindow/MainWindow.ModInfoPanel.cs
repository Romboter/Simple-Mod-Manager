#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VintageStoryModManager.Helpers;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Selector = System.Windows.Controls.Primitives.Selector;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void RootGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isDraggingModInfoPanel) return;

        EnsureModInfoPanelWithinBounds(true);
    }

    private void ModInfoBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;

        if (IsModInfoDragInitiationBlocked(e.OriginalSource as DependencyObject)) return;

        _isDraggingModInfoPanel = true;
        _modInfoDragOffset = e.GetPosition(border);
        border.CaptureMouse();
        e.Handled = true;
    }

    private void ModInfoBorder_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingModInfoPanel || sender is not Border border) return;

        var pointerPosition = e.GetPosition(RootGrid);
        var left = pointerPosition.X - _modInfoDragOffset.X;
        var top = pointerPosition.Y - _modInfoDragOffset.Y;

        SetModInfoPanelPosition(left, top, false);
        e.Handled = true;
    }

    private void ModInfoBorder_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingModInfoPanel || sender is not Border border) return;

        _isDraggingModInfoPanel = false;
        border.ReleaseMouseCapture();
        EnsureModInfoPanelWithinBounds(true);
        e.Handled = true;
    }

    private void ModInfoBorder_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_isDraggingModInfoPanel) return;

        _isDraggingModInfoPanel = false;
        EnsureModInfoPanelWithinBounds(true);
    }

    private void ApplyStoredModInfoPanelPosition()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var left = _userConfiguration.ModInfoPanelLeft ?? ComputeDefaultModInfoPanelLeft();
            var top = _userConfiguration.ModInfoPanelTop ?? DefaultModInfoPanelTop;

            SetModInfoPanelPosition(left, top, false);
            _hasAppliedInitialModInfoPanelPosition = true;
        }), DispatcherPriority.Loaded);
    }

    private void EnsureModInfoPanelWithinBounds(bool persist)
    {
        if (MODINFO_border is null) return;

        var left = Canvas.GetLeft(MODINFO_border);
        if (double.IsNaN(left)) left = _userConfiguration.ModInfoPanelLeft ?? ComputeDefaultModInfoPanelLeft();

        var top = Canvas.GetTop(MODINFO_border);
        if (double.IsNaN(top)) top = _userConfiguration.ModInfoPanelTop ?? DefaultModInfoPanelTop;

        var shouldPersist = persist && _hasAppliedInitialModInfoPanelPosition;
        SetModInfoPanelPosition(left, top, shouldPersist);
    }

    private void SetModInfoPanelPosition(double left, double top, bool persist)
    {
        if (RootGrid is null || MODINFO_border is null) return;

        var containerWidth = RootGrid.ActualWidth;
        var containerHeight = RootGrid.ActualHeight;

        if (containerWidth <= 0 || containerHeight <= 0)
        {
            Dispatcher.BeginInvoke(new Action(() => SetModInfoPanelPosition(left, top, persist)),
                DispatcherPriority.Loaded);
            return;
        }

        var panelWidth = GetModInfoPanelWidth();
        var panelHeight = GetModInfoPanelHeight();

        var (clampedLeft, clampedTop) = ModInfoPanelLayoutHelper.ClampPanelPosition(
            left, top, containerWidth, containerHeight, panelWidth, panelHeight, ModInfoPanelHorizontalOverhang);

        Canvas.SetLeft(MODINFO_border, clampedLeft);
        Canvas.SetTop(MODINFO_border, clampedTop);

        if (persist) _userConfiguration.SetModInfoPanelPosition(clampedLeft, clampedTop);
    }

    private double ComputeDefaultModInfoPanelLeft()
    {
        var containerWidth = RootGrid?.ActualWidth ?? ActualWidth;

        if (containerWidth <= 0) containerWidth = Width;

        var panelWidth = GetModInfoPanelWidth();

        return ModInfoPanelLayoutHelper.ComputeDefaultLeft(
            containerWidth,
            panelWidth,
            DefaultModInfoPanelLeft,
            DefaultModInfoPanelRightMargin,
            ModInfoPanelHorizontalOverhang);
    }

    private double GetModInfoPanelWidth()
    {
        if (MODINFO_border is null) return 0;

        var width = MODINFO_border.ActualWidth;
        if (width <= 0) width = MODINFO_border.Width;

        return double.IsNaN(width) ? 0 : width;
    }

    private double GetModInfoPanelHeight()
    {
        if (MODINFO_border is null) return 0;

        var height = MODINFO_border.ActualHeight;
        if (height <= 0) height = MODINFO_border.Height;

        return double.IsNaN(height) ? 0 : height;
    }

    private static bool IsModInfoDragInitiationBlocked(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Border border && border.Name == nameof(MODINFO_border)) break;

            if (source is ButtonBase || source is Selector || source is TextBoxBase || source is Hyperlink ||
                source is Slider || source is ScrollBar) return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
