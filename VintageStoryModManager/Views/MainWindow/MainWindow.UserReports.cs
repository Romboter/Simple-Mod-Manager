#nullable enable

using System;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox =
    VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void UserReportsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

            e.Handled = true;

            if (_viewModel is null) return;

            if (!mod.CanSubmitUserReport)
            {
                WpfMessageBox.Show(
                    "User reports are unavailable because the mod version or Vintage Story version could not be determined.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!EnsureUserReportVotingConsent()) return;

            _viewModel.EnableUserReportFetching();

            try
            {
                var summary = await _viewModel
                    .RefreshUserReportAsync(mod)
                    .ConfigureAwait(true);

                summary ??= mod.UserReportSummary;

                if (summary is null)
                    summary = new ModVersionVoteSummary(
                        mod.ModId,
                        mod.Version ?? string.Empty,
                        _viewModel.InstalledGameVersion,
                        ModVersionVoteCounts.Empty,
                        ModVersionVoteComments.Empty,
                        null,
                        null);

                var dialog = new ModVoteDialog(
                    mod,
                    summary,
                    (option, comment) => _viewModel.SubmitUserReportVoteAsync(mod, option, comment));

                dialog.Owner = this;
                dialog.ShowDialog();
            }
            catch (InternetAccessDisabledException ex)
            {
                WpfMessageBox.Show(
                    ex.Message,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    $"Failed to load user reports:\n{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
}
