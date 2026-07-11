#nullable enable
using System.Windows;
using WinForms = System.Windows.Forms;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void ApplyStoredWindowDimensions()
    {
        var storedWidth = _userConfiguration.WindowWidth;
        var storedHeight = _userConfiguration.WindowHeight;
        var storedLeft = _userConfiguration.WindowLeft;
        var storedTop = _userConfiguration.WindowTop;

        if (!storedWidth.HasValue && !storedHeight.HasValue && !storedLeft.HasValue && !storedTop.HasValue) return;

        SizeToContent = SizeToContent.Manual;

        if (storedWidth.HasValue) Width = storedWidth.Value;

        if (storedHeight.HasValue) Height = storedHeight.Value;

        if (storedLeft.HasValue && storedTop.HasValue && IsWindowPositionOnScreen(storedLeft.Value, storedTop.Value))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = storedLeft.Value;
            Top = storedTop.Value;
        }
    }

    private void SaveWindowDimensions()
    {
        if (_userConfiguration is null) return;

        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        _userConfiguration.SetWindowDimensions(bounds.Width, bounds.Height);
        _userConfiguration.SetWindowPosition(bounds.Left, bounds.Top);
    }

    private static bool IsWindowPositionOnScreen(double left, double top)
    {
        // Check if the position is within the bounds of any screen
        // Use a small margin to ensure the title bar is visible
        var point = new System.Drawing.Point((int)left + WindowPositionScreenMargin, (int)top + WindowPositionScreenMargin);

        foreach (var screen in WinForms.Screen.AllScreens)
        {
            if (screen.WorkingArea.Contains(point))
                return true;
        }

        return false;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (_isWindowActive) return;

        _isWindowActive = true;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        _isWindowActive = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        UnsubscribeModBrowserFromDirectoryWatcher();
        StopModsWatcher();
        _votesCacheWatcher?.Dispose();
        _modlistBackupCoordinator.Dispose();
        _cloudStoreLock.Dispose();
        _cloudModlistStore?.Dispose();
        base.OnClosed(e);
    }
}
