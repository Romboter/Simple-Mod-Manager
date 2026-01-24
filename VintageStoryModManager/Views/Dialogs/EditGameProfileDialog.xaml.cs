using System.Windows;
using System.Windows.Controls;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views.Dialogs;

public partial class EditGameProfileDialog : Window
{
    private readonly ServerTargetService _serverTargetService;
    private readonly Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>> _testConnection;
    private readonly Func<string, HostKeyVerificationResult, Task<bool>> _hostKeyVerifier;
    private readonly string? _currentServerTargetId;
    private readonly bool _serverOptionsEnabled;

    public EditGameProfileDialog(
        Window owner,
        string profileName,
        ProfileType currentProfileType,
        string? currentServerTargetId,
        ServerTargetService serverTargetService,
        Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>> testConnection,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier,
        bool serverOptionsEnabled = true)
    {
        InitializeComponent();

        Owner = owner;
        _serverTargetService = serverTargetService ?? throw new ArgumentNullException(nameof(serverTargetService));
        _testConnection = testConnection ?? throw new ArgumentNullException(nameof(testConnection));
        _hostKeyVerifier = hostKeyVerifier ?? throw new ArgumentNullException(nameof(hostKeyVerifier));
        _currentServerTargetId = currentServerTargetId;
        _serverOptionsEnabled = serverOptionsEnabled;

        ProfileNameText.Text = profileName;

        // Set initial profile type
        if (currentProfileType == ProfileType.Server)
        {
            ServerRadio.IsChecked = true;
            ServerTargetPanel.Visibility = Visibility.Visible;
        }
        else
        {
            LocalRadio.IsChecked = true;
            ServerTargetPanel.Visibility = Visibility.Collapsed;
        }

        RefreshServerTargets();
        UpdateSaveButtonState();
    }

    public ProfileType SelectedProfileType =>
        ServerRadio.IsChecked == true ? ProfileType.Server : ProfileType.Local;

    public string? SelectedServerTargetId =>
        SelectedProfileType == ProfileType.Server
            ? (ServerTargetCombo.SelectedItem as ServerTarget)?.Id
            : null;

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Validate server target selection for server profiles
        if (SelectedProfileType == ProfileType.Server && string.IsNullOrEmpty(SelectedServerTargetId))
        {
            System.Windows.MessageBox.Show(
                this,
                "Please select a server target for the server profile.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void ProfileType_Changed(object sender, RoutedEventArgs e)
    {
        if (ServerTargetPanel is null) return;

        ServerTargetPanel.Visibility = ServerRadio.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateSaveButtonState();
    }

    private void ServerTargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSaveButtonState();
    }

    private void ManageTargets_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ManageServerTargetsDialog(_serverTargetService, _testConnection, _hostKeyVerifier)
        {
            Owner = this
        };
        dialog.ShowDialog();

        RefreshServerTargets();
    }

    private void RefreshServerTargets()
    {
        if (ServerTargetCombo == null)
            return;

        var targets = _serverTargetService.GetAllTargets();
        ServerTargetCombo.ItemsSource = targets;

        // Try to select the current server target
        if (!string.IsNullOrEmpty(_currentServerTargetId))
        {
            ServerTargetCombo.SelectedItem = targets.FirstOrDefault(t => t.Id == _currentServerTargetId);
        }

        // Select first if nothing selected
        if (ServerTargetCombo.SelectedItem == null && targets.Count > 0)
        {
            ServerTargetCombo.SelectedIndex = 0;
        }

        // Show warning if no targets
        if (NoTargetsWarning != null)
        {
            NoTargetsWarning.Visibility = targets.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        UpdateSaveButtonState();
    }

    private void UpdateSaveButtonState()
    {
        if (SaveButton is null) return;

        var isServerValid = SelectedProfileType != ProfileType.Server ||
                            ServerTargetCombo?.SelectedItem != null;

        SaveButton.IsEnabled = isServerValid;
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        // Control profile type section visibility based on server options setting
        if (ProfileTypeSection != null)
        {
            ProfileTypeSection.Visibility = _serverOptionsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        // If server options are disabled, force Local profile type
        if (!_serverOptionsEnabled && LocalRadio != null)
        {
            LocalRadio.IsChecked = true;
            ServerTargetPanel.Visibility = Visibility.Collapsed;
        }

        // Focus the save button
        SaveButton.Focus();
    }
}
