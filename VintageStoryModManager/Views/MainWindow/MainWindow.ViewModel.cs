#nullable enable

using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task InitializeViewModelAsync(MainViewModel viewModel)
    {
        if (_isInitializing) return;

        _isInitializing = true;
        var initialized = false;
        try
        {
            await viewModel.InitializeAsync();
            initialized = true;
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    $"Failed to load mods:\n{ex.Message}",
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        finally
        {
            _isInitializing = false;
        }

        if (initialized)
        {
            StartModsWatcher();
            StartGameSessionMonitor();
            await TryShowModUsagePromptAsync().ConfigureAwait(true);
        }
    }

    private void DisposeCurrentViewModel()
    {
        if (_viewModel is null) return;

        var current = _viewModel;
        _viewModel = null;
        DataContext = null;
        StopGameSessionMonitor();
        DisposeViewModel(current);
    }

    private void DisposeViewModel(MainViewModel? viewModel)
    {
        if (viewModel is null) return;

        viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        viewModel.UserReportVoteSubmitted -= OnUserReportVoteSubmitted;
        viewModel.Dispose();
    }

    private void HandleViewModelInitializationFailure(Exception exception)
    {
        _modActivityLoggingService.LogError("Failed to initialize view model", exception);
        DisposeCurrentViewModel();

        if (_dataDirectory != null) _userConfiguration.ClearDataDirectory();

        _dataDirectory = null;

        var message = $"Failed to initialize the mod manager:\n{exception.Message}\n\n" +
                      "You can set the Vintage Story folders from the File menu once the application has loaded.";

        WpfMessageBox.Show(message,
            "Simple VS Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task ReloadViewModelAsync()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
        {
            await RefreshDeleteCachedModsMenuHeaderAsync();
            return;
        }

        StopModsWatcher();
        StopGameSessionMonitor();

        var previousViewModel = _viewModel;
        if (previousViewModel is not null)
        {
            previousViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            previousViewModel.UserReportVoteSubmitted -= OnUserReportVoteSubmitted;
        }

        MainViewModel? newViewModel = null;

        try
        {
            newViewModel = new MainViewModel(
                _dataDirectory,
                _userConfiguration,
                _gameDirectory);
            newViewModel.IsCompactView = _userConfiguration.IsCompactView;
            newViewModel.UseModDbDesignView = _userConfiguration.UseModDbDesignView;
            newViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            newViewModel.UserReportVoteSubmitted += OnUserReportVoteSubmitted;
            _viewModel = newViewModel;
            ApplyColumnVisibilityPreferencesToViewModel();
            UpdateGameVersionMenuItem(newViewModel.InstalledGameVersion);
            DataContext = newViewModel;
            ApplyPlayerIdentityToUiAndCloudStore();
            AttachToModsView(newViewModel.CurrentModsView);
            await InitializeViewModelAsync(newViewModel);

            DisposeViewModel(previousViewModel);
        }
        catch (Exception ex)
        {
            DisposeViewModel(newViewModel);

            if (previousViewModel is not null)
            {
                _viewModel = previousViewModel;
                previousViewModel.PropertyChanged += ViewModelOnPropertyChanged;
                previousViewModel.UserReportVoteSubmitted += OnUserReportVoteSubmitted;
                DataContext = previousViewModel;
                ApplyPlayerIdentityToUiAndCloudStore();
                AttachToModsView(previousViewModel.CurrentModsView);
                StartModsWatcher();
            }

            await _confirmationService.NotifyAsync(
                    $"Failed to reload mods:\n{ex.Message}",
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }

        await RefreshDeleteCachedModsMenuHeaderAsync();
    }

    private void InitializeViewModel()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory))
            throw new InvalidOperationException("The data directory is not set.");

        _viewModel = new MainViewModel(
            _dataDirectory,
            _userConfiguration,
            _gameDirectory)
        {
            IsCompactView = _userConfiguration.IsCompactView,
            UseModDbDesignView = _userConfiguration.UseModDbDesignView,
        };
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.UserReportVoteSubmitted += OnUserReportVoteSubmitted;
        DataContext = _viewModel;
        ApplyPlayerIdentityToUiAndCloudStore();
        _cloudModlistsLoaded = false;
        _localModlistsLoaded = false;
        _selectedCloudModlist = null;
        AttachToModsView(_viewModel.CurrentModsView);
        RestoreSortPreference();
        UpdateGameVersionMenuItem(_viewModel.InstalledGameVersion);
        ApplyColumnVisibilityPreferencesToViewModel();
        SubscribeModBrowserToDirectoryWatcher();
    }
}
