using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace VintageStoryModManager.Views.Dialogs;

/// <summary>
///     Dialog for syncing mods to a remote server via SFTP.
/// </summary>
public partial class SyncToServerDialog : Window
{
    private readonly SyncToServerDialogViewModel _viewModel;

    public SyncToServerDialog(SyncToServerDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;


        Closing += OnClosing;

        // Handle unhandled exceptions in async commands
        Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var message = $"An unexpected error occurred:\n{e.Exception.Message}";
        if (e.Exception.InnerException != null)
        {
            message += $"\n\nInner exception:\n{e.Exception.InnerException.Message}";
        }
        MessageBox.Show(this, message, "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var message = $"An unexpected async error occurred:\n{e.Exception.InnerException?.Message ?? e.Exception.Message}";
        Dispatcher.BeginInvoke(() =>
        {
            MessageBox.Show(this, message, "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
        e.SetObserved();
    }

    /// <summary>
    ///     Gets whether the current step is Options.
    /// </summary>
    public bool IsOptionsStep => _viewModel.CurrentStep == SyncDialogStep.Options;

    /// <summary>
    ///     Gets whether the current step is Preview.
    /// </summary>
    public bool IsPreviewStep => _viewModel.CurrentStep == SyncDialogStep.Preview;

    /// <summary>
    ///     Gets whether the current step is Executing.
    /// </summary>
    public bool IsExecutingStep => _viewModel.CurrentStep == SyncDialogStep.Executing;

    /// <summary>
    ///     Gets whether the current step is Complete.
    /// </summary>
    public bool IsCompleteStep => _viewModel.CurrentStep == SyncDialogStep.Complete;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _viewModel.CurrentStep == SyncDialogStep.Complete && !_viewModel.HasErrors && !_viewModel.WasCancelled;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_viewModel.IsBusy)
        {
            var result = MessageBox.Show(
                this,
                "A sync operation is in progress. Are you sure you want to cancel and close?",
                "Sync in Progress",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _viewModel.Cleanup();
        }
        else
        {
            _viewModel.Cleanup();
        }

        // Unsubscribe from exception handlers
        Dispatcher.UnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }
}


/// <summary>
///     Helper extension for ViewModel step visibility bindings.
/// </summary>
public static class SyncDialogViewModelExtensions
{
    public static bool IsOptionsStep(this SyncToServerDialogViewModel vm) => vm.CurrentStep == SyncDialogStep.Options;
    public static bool IsPreviewStep(this SyncToServerDialogViewModel vm) => vm.CurrentStep == SyncDialogStep.Preview;
    public static bool IsExecutingStep(this SyncToServerDialogViewModel vm) => vm.CurrentStep == SyncDialogStep.Executing;
    public static bool IsCompleteStep(this SyncToServerDialogViewModel vm) => vm.CurrentStep == SyncDialogStep.Complete;
}

/// <summary>
///     Inverts a boolean value for visibility binding.
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
