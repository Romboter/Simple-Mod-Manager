using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using VintageStoryModManager.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace VintageStoryModManager.Views.Dialogs;

/// <summary>
///     Dialog for adding or editing a server target configuration.
/// </summary>
public partial class ServerTargetEditorDialog : Window
{
    private readonly ServerTargetEditorViewModel _viewModel;

    public ServerTargetEditorDialog(ServerTargetEditorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Add converters to resources
        Resources["InverseBoolConverter"] = new InverseBooleanConverter();
        Resources["NullToCollapsedConverter"] = new NullToCollapsedConverter();

        // Set initial password if editing
        if (!string.IsNullOrEmpty(_viewModel.Password))
        {
            PasswordBox.Password = _viewModel.Password;
            PassphraseBox.Password = _viewModel.Password;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private void PassphraseBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // For private key auth, the "password" field is used for the passphrase
        _viewModel.Password = PassphraseBox.Password;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save(out var error))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show(this, error ?? "Failed to save target.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
///     Converts true to false and vice versa.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }
}

/// <summary>
///     Converts null/empty to Collapsed, otherwise Visible.
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return Visibility.Collapsed;

        if (value is string s && string.IsNullOrEmpty(s))
            return Visibility.Collapsed;

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
