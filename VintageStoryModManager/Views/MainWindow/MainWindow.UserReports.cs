#nullable enable

using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using WpfButton = System.Windows.Controls.Button;

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
            await _confirmationService.NotifyAsync(
                    UserReportsDialogTextBuilder.CannotSubmitUserReportMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    ex.Message,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    UserReportsDialogTextBuilder.BuildLoadFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }
}
