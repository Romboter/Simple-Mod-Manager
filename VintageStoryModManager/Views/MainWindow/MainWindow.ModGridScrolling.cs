#nullable enable

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VintageStoryModManager.Helpers;
using WpfToolTip = System.Windows.Controls.ToolTip;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void LatestVersionTextBlock_OnToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (sender is not FrameworkElement frameworkElement) return;

            if (frameworkElement.ToolTip is not WpfToolTip toolTip) return;

            toolTip.PreviewMouseWheel -= ChangelogToolTip_OnPreviewMouseWheel;
            toolTip.PreviewMouseWheel += ChangelogToolTip_OnPreviewMouseWheel;
        }

    private void LatestVersionTextBlock_OnToolTipClosing(object sender, ToolTipEventArgs e)
        {
            if (sender is not FrameworkElement frameworkElement) return;

            if (frameworkElement.ToolTip is not WpfToolTip toolTip) return;

            toolTip.PreviewMouseWheel -= ChangelogToolTip_OnPreviewMouseWheel;
        }

    private void ChangelogToolTip_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || e.Delta == 0 || sender is not WpfToolTip toolTip) return;

            if (toolTip.Content is not ScrollViewer scrollViewer) return;

            if (!ScrollWheelMath.TryComputeTargetOffset(
                    e.Delta,
                    Mouse.MouseWheelDeltaForOneLine,
                    SystemParameters.WheelScrollLines,
                    GetCurrentScrollMultiplier(),
                    scrollViewer.VerticalOffset,
                    scrollViewer.ScrollableHeight,
                    out var clampedOffset))
                return;

            scrollViewer.ScrollToVerticalOffset(clampedOffset);
            e.Handled = true;
        }

    private void ModsDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || e.Delta == 0) return;

            if (e.OriginalSource is DependencyObject originalSource && IsDescendantOfToolTip(originalSource)) return;

            if (sender is not DependencyObject dependencyObject) return;

            var isModListGrid = ReferenceEquals(dependencyObject, ModsDataGrid);

            var scrollViewer = GetModsScrollViewer(); ;

            if (scrollViewer is null) return;

            if (!ScrollWheelMath.TryComputeTargetOffset(
                    e.Delta,
                    Mouse.MouseWheelDeltaForOneLine,
                    SystemParameters.WheelScrollLines,
                    GetCurrentScrollMultiplier(),
                    scrollViewer.VerticalOffset,
                    scrollViewer.ScrollableHeight,
                    out var clampedOffset))
                return;

            scrollViewer.ScrollToVerticalOffset(clampedOffset);
            e.Handled = true;
        }

    private static bool IsDescendantOfToolTip(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is WpfToolTip || source is Popup) return true;

                source = GetParent(source);
            }

            return false;
        }

    private static DependencyObject? GetParent(DependencyObject current)
        {
            if (current is Visual visual)
            {
                var parent = VisualTreeHelper.GetParent(visual);
                if (parent != null) return parent;

                if (visual is FrameworkElement frameworkElement) return frameworkElement.Parent;
            }

            if (current is FrameworkContentElement contentElement)
                return contentElement.Parent ?? contentElement.TemplatedParent;

            return LogicalTreeHelper.GetParent(current);
        }

    private static T? FindAncestor<T>(DependencyObject? source)
            where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match) return match;

                source = GetParent(source);
            }

            return null;
        }

    private double GetCurrentScrollMultiplier()
        {
            return ModListScrollMultiplier;
        }

    private ScrollViewer? GetModsScrollViewer()
        {

            var targetGrid = ModsDataGrid;

            if (targetGrid == null) return null;

            if (_modsScrollViewer != null && ReferenceEquals(_modsScrollViewerSource, targetGrid)) return _modsScrollViewer;

            _modsScrollViewer = FindDescendantScrollViewer(targetGrid);
            _modsScrollViewerSource = targetGrid;
            return _modsScrollViewer;
        }

    private void ClearScrollViewerCache()
        {
            _modsScrollViewer = null;
            _modsScrollViewerSource = null;
        }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject? current)
        {
            if (current is null) return null;

            if (current is ScrollViewer viewer) return viewer;

            var childCount = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < childCount; i++)
            {
                var result = FindDescendantScrollViewer(VisualTreeHelper.GetChild(current, i));
                if (result != null) return result;
            }

            return null;
        }
}
