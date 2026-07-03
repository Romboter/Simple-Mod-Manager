#nullable enable

using System;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using ModUserReportChangedEventArgs = VintageStoryModManager.ViewModels.MainViewModel.ModUserReportChangedEventArgs;
using WpfButton = System.Windows.Controls.Button;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void InitializeModBrowserView()
    {
        if (ModBrowserView != null)
        {
            var httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var modApiService = new ModApiService(httpClient);
            _modBrowserViewModel = new ModBrowserViewModel(modApiService, _userConfiguration);

            // Set up the installation callback
            _modBrowserViewModel.SetInstallModCallback(InstallModFromBrowserAsync);

            ModBrowserView.DataContext = _modBrowserViewModel;

            UpdateModBrowserTabVisibilityState(DatabaseTab?.IsSelected == true);

            // Set up votes cache watcher to refresh user reports when cache changes
            InitializeVotesCacheWatcher();
        }
    }

    private void OnUserReportVoteSubmitted(object? sender, ModUserReportChangedEventArgs e)
    {
        if (_modBrowserViewModel is null) return;

        int parsedId;
        if (e.NumericModId.HasValue)
        {
            parsedId = e.NumericModId.Value;
        }
        else if (!int.TryParse(e.ModId, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedId))
        {
            return;
        }

        _modBrowserViewModel.InvalidateUserReport(parsedId, e.Summary);
    }

    private void ModDatabasePageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { DataContext: ModListItemViewModel mod }
            && mod.OpenModDatabasePageCommand is ICommand command
            && command.CanExecute(null))
            command.Execute(null);

        e.Handled = true;
    }

    private void ManagerModDbPageMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        OpenManagerModDatabasePage();
    }
}
