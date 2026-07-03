#nullable enable
using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void UseFasterThumbnailsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetUseFasterThumbnails(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.UseFasterThumbnails;
    }

    private void LogModUpdateMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetLogModUpdates(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.LogModUpdates;
    }

    private void LogModInstallMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetLogModInstalls(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.LogModInstalls;
    }

    private void LogModDeletionMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetLogModDeletions(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.LogModDeletions;
    }

    private void LogAppLifecycleMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetLogAppLaunchAndExit(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.LogAppLaunchAndExit;
    }

    private void LogErrorsAndExceptionsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;

        _userConfiguration.SetLogErrorsAndExceptions(menuItem.IsChecked);
        menuItem.IsChecked = _userConfiguration.LogErrorsAndExceptions;
        InitializeTraceListener();
    }

    private void InitializeTraceListener()
    {
        // Remove existing listener if present
        if (_traceListener != null)
        {
            System.Diagnostics.Trace.Listeners.Remove(_traceListener);
            _traceListener.Dispose();
            _traceListener = null;
        }

        // Add new listener if logging is enabled
        if (_userConfiguration.LogErrorsAndExceptions)
        {
            _traceListener = new ModManagerTraceListener(_modActivityLoggingService);
            System.Diagnostics.Trace.Listeners.Add(_traceListener);
            _modActivityLoggingService.LogDiagnostic("Error and exception logging enabled");
        }
    }

    private void UpdateLoggingMenuState()
    {
        if (LogModUpdateMenuItem is not null)
            LogModUpdateMenuItem.IsChecked = _userConfiguration.LogModUpdates;
        if (LogModInstallMenuItem is not null)
            LogModInstallMenuItem.IsChecked = _userConfiguration.LogModInstalls;
        if (LogModDeletionMenuItem is not null)
            LogModDeletionMenuItem.IsChecked = _userConfiguration.LogModDeletions;
        if (LogAppLifecycleMenuItem is not null)
            LogAppLifecycleMenuItem.IsChecked = _userConfiguration.LogAppLaunchAndExit;
        if (LogErrorsAndExceptionsMenuItem is not null)
            LogErrorsAndExceptionsMenuItem.IsChecked = _userConfiguration.LogErrorsAndExceptions;
    }
}
