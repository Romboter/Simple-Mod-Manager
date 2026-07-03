#nullable enable

using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Services;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ProgressBar = System.Windows.Controls.ProgressBar;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using VerticalAlignment = System.Windows.VerticalAlignment;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

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
            var message =
                $"Simple VS Manager is moving its configuration and cache files from Documents to AppData/Local for better system integration.\n\n" +
                $"Old location: Documents\\Simple VS Manager\n" +
                $"New location: AppData\\Local\\Simple VS Manager\n\n" +
                $"Would you like to copy your existing settings and cache to the new location?\n\n" +
                $"Selecting 'No' will start fresh with default settings.";

            var result = WpfMessageBox.Show(
                this,
                message,
                "Simple VS Manager - Configuration Migration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes) await PerformMigrationAsync().ConfigureAwait(true);

            // Mark migration check as completed regardless of user choice
            _userConfiguration.SetMigrationCheckCompleted();
        }

    private Task PerformMigrationAsync()
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
                WpfMessageBox.Show(
                    this,
                    "Configuration and cache files have been successfully migrated to the new location.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            else
                WpfMessageBox.Show(
                    this,
                    "Migration could not be completed. Starting with default settings.\n\nYou can manually copy files from Documents\\Simple VS Manager to AppData\\Local\\Simple VS Manager if needed.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

            return Task.CompletedTask;
        }
}
