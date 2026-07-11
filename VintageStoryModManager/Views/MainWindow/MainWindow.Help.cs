#nullable enable

using System.Diagnostics;
using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void HelpMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var managerDirectory = _userConfiguration.GetConfigurationDirectory();
        var cachedModsDirectory = ModCacheLocator.GetCachedModsDirectory();

        var dialog = new HelpDialogWindow(managerDirectory, cachedModsDirectory)
        {
            Owner = this
        };

        _ = dialog.ShowDialog();
    }

    private void GuideMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var managerDirectory = _userConfiguration.GetConfigurationDirectory();
        var configurationFilePath = Path.Combine(managerDirectory, "SimpleVSManagerConfiguration.json");
        var cachedModsDirectory = ModCacheLocator.GetCachedModsDirectory();

        var dialog = new GuideDialogWindow(managerDirectory, cachedModsDirectory, configurationFilePath)
        {
            Owner = this
        };

        _ = dialog.ShowDialog();
    }

    private async void DiscordButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (e is not null)
        {
            e.Handled = true;
        }

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            await _confirmationService.NotifyAsync(
                    "Enable Internet Access in the File menu to open Discord.",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DiscordInviteUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    HelpDialogTextBuilder.BuildOpenDiscordFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async void ModUsagePromptLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (e is not null) e.Handled = true;

        await ShowModUsagePromptDialogAsync().ConfigureAwait(true);
    }
}
