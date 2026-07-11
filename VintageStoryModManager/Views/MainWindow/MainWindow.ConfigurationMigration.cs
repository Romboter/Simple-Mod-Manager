#nullable enable

using System.Windows;
using VintageStoryModManager.Services;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ProgressBar = System.Windows.Controls.ProgressBar;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task CheckAndPromptMigrationAsync()
    {
        // Check if migration check has already been completed
        if (_userConfiguration.MigrationCheckCompleted) return;

        // Check if migration is needed
        if (!ConfigurationMigrationService.ShouldOfferMigration(out var oldConfigVersion))
        {
            // Mark as completed even if no migration needed
            _userConfiguration.SetMigrationCheckCompleted();
            return;
        }

        // Prompt the user
        var confirmed = await _confirmationService.ConfirmAsync(
                ConfigurationMigrationDialogTextBuilder.MigrationPromptMessage,
                "Simple VS Manager - Configuration Migration",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (confirmed) await PerformMigrationAsync().ConfigureAwait(true);

        // Mark migration check as completed regardless of user choice
        _userConfiguration.SetMigrationCheckCompleted();
    }

    private async Task PerformMigrationAsync()
    {
        // Create and show a blocking dialog
        var stackPanel = new StackPanel();
        stackPanel.VerticalAlignment = VerticalAlignment.Center;
        stackPanel.HorizontalAlignment = HorizontalAlignment.Center;

        var textBlock = new TextBlock();
        textBlock.Text = "Migrating configuration and cache files...";
        textBlock.FontSize = 14;
        textBlock.Margin = new Thickness(20);
        textBlock.TextAlignment = TextAlignment.Center;

        var progressBar = new ProgressBar();
        progressBar.IsIndeterminate = true;
        progressBar.Width = 300;
        progressBar.Height = 20;
        progressBar.Margin = new Thickness(20);

        stackPanel.Children.Add(textBlock);
        stackPanel.Children.Add(progressBar);

        var progressWindow = new Window();
        progressWindow.Title = "Migrating Configuration";
        progressWindow.Width = 400;
        progressWindow.Height = 150;
        progressWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        progressWindow.Owner = this;
        progressWindow.ResizeMode = ResizeMode.NoResize;
        progressWindow.WindowStyle = WindowStyle.ToolWindow;
        progressWindow.Content = stackPanel;

        var migrationSuccess = false;

        // Show the dialog and perform migration in background
        progressWindow.Loaded += async (s, e) =>
        {
            await Task.Run(() => { migrationSuccess = ConfigurationMigrationService.PerformMigration(); })
                .ConfigureAwait(true);

            // Close on UI thread
            progressWindow.Dispatcher.Invoke(() => progressWindow.Close());
        };

        progressWindow.ShowDialog();

        // Show result
        if (migrationSuccess)
            await _confirmationService.NotifyAsync(
                    ConfigurationMigrationDialogTextBuilder.MigrationSuccessMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
        else
            await _confirmationService.NotifyAsync(
                    ConfigurationMigrationDialogTextBuilder.MigrationFailureMessage,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
    }
}
