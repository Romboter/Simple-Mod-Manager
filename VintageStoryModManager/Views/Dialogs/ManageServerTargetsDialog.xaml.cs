using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace VintageStoryModManager.Views.Dialogs;

/// <summary>
///     Dialog for managing server targets.
/// </summary>
public partial class ManageServerTargetsDialog : Window
{
    private readonly ManageServerTargetsViewModel _viewModel;
    private readonly ServerTargetService _targetService;
    private readonly Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>> _testConnection;
    private readonly Func<string, HostKeyVerificationResult, Task<bool>> _hostKeyVerifier;

    public ManageServerTargetsDialog(
        ServerTargetService targetService,
        Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, Task<bool>> testConnection,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier)
    {
        InitializeComponent();

        _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
        _testConnection = testConnection ?? throw new ArgumentNullException(nameof(testConnection));
        _hostKeyVerifier = hostKeyVerifier ?? throw new ArgumentNullException(nameof(hostKeyVerifier));

        _viewModel = new ManageServerTargetsViewModel(targetService);
        DataContext = _viewModel;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var editorVm = new ServerTargetEditorViewModel(_targetService, _testConnection, _hostKeyVerifier);
        var editor = new ServerTargetEditorDialog(editorVm) { Owner = this };

        if (editor.ShowDialog() == true)
        {
            _viewModel.Refresh();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTarget == null)
            return;

        var target = _targetService.GetTarget(_viewModel.SelectedTarget.Id);
        if (target == null)
            return;

        var editorVm = new ServerTargetEditorViewModel(_targetService, _testConnection, _hostKeyVerifier, target);
        var editor = new ServerTargetEditorDialog(editorVm) { Owner = this };

        if (editor.ShowDialog() == true)
        {
            _viewModel.Refresh();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTarget == null)
            return;

        var result = MessageBox.Show(
            this,
            $"Are you sure you want to delete the server target '{_viewModel.SelectedTarget.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            if (_targetService.TryDeleteTarget(_viewModel.SelectedTarget.Id, out var error))
            {
                _viewModel.Refresh();
            }
            else
            {
                MessageBox.Show(this, error ?? "Failed to delete target.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
///     ViewModel for the server targets management dialog.
/// </summary>
public sealed partial class ManageServerTargetsViewModel : ObservableObject
{
    private readonly ServerTargetService _targetService;

    [ObservableProperty]
    private ServerTargetListItem? _selectedTarget;

    public ManageServerTargetsViewModel(ServerTargetService targetService)
    {
        _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
        Refresh();
    }

    public ObservableCollection<ServerTargetListItem> Targets { get; } = new();

    public bool HasSelection => SelectedTarget != null;

    partial void OnSelectedTargetChanged(ServerTargetListItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
    }

    public void Refresh()
    {
        var selectedId = SelectedTarget?.Id;

        Targets.Clear();
        foreach (var target in _targetService.GetAllTargets().OrderBy(t => t.Name))
        {
            Targets.Add(new ServerTargetListItem(target));
        }

        if (selectedId != null)
        {
            SelectedTarget = Targets.FirstOrDefault(t => t.Id == selectedId);
        }
    }
}

/// <summary>
///     Display item for a server target in the list.
/// </summary>
public sealed class ServerTargetListItem
{
    public ServerTargetListItem(ServerTarget target)
    {
        Id = target.Id;
        Name = target.Name;
        HostDisplay = $"{target.Host}:{target.Port}";
        LastConnectedDisplay = target.LastConnected.HasValue
            ? $"Last: {target.LastConnected.Value:g}"
            : "Never connected";
    }

    public string Id { get; }
    public string Name { get; }
    public string HostDisplay { get; }
    public string LastConnectedDisplay { get; }
}
