#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

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

    private void DiscordButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (e is not null)
        {
            e.Handled = true;
        }

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            WpfMessageBox.Show(
                "Enable Internet Access in the File menu to open Discord.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            WpfMessageBox.Show(
                $"Failed to open Discord:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ModUsagePromptLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (e is not null) e.Handled = true;

        await ShowModUsagePromptDialogAsync().ConfigureAwait(true);
    }
}
