#nullable enable
using System.Diagnostics;
using System.IO;
using System.Windows;
using VintageStoryModManager.Services;
using WinForms = System.Windows.Forms;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async Task<bool> TryEnsureDataBackupBeforeLaunchAsync()
    {
        if (!_userConfiguration.AutomaticDataBackupsEnabled) return true;
        if (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory)) return true;

        ShowDataBackupOverlay("Preparing VintagestoryData backup...");
        var progress = CreateDataBackupProgressReporter("Backing up VintagestoryData...");

        var installedGameVersion = VintageStoryVersionLocator.GetInstalledVersion(_gameDirectory);

        try
        {
            await _dataFolderBackupCoordinator
                .CreateBackupAsync(_dataDirectory!, installedGameVersion, progress, CancellationToken.None)
                .ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var confirmed = await _confirmationService.ConfirmAsync(
                    GameLaunchDialogTextBuilder.BuildBackupFailedPromptMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return confirmed;
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    GameLaunchDialogTextBuilder.BuildBackupFailedMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return false;
        }
        finally
        {
            HideDataBackupOverlay();
        }
    }

    private async void LaunchGameButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreset) return;

        if (!await TryEnsureDataBackupBeforeLaunchAsync().ConfigureAwait(true)) return;

        if (!string.IsNullOrWhiteSpace(_customShortcutPath))
        {
            if (!File.Exists(_customShortcutPath))
            {
                await _confirmationService.NotifyAsync(
                        "The custom Vintage Story shortcut could not be found. Please set it again from File > Set custom Vintage Story shortcut.",
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                _userConfiguration.ClearCustomShortcutPath();
                _customShortcutPath = null;
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _customShortcutPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await _confirmationService.NotifyAsync(
                        GameLaunchDialogTextBuilder.BuildShortcutLaunchFailedMessage(ex.Message),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDirectory) || !Directory.Exists(_dataDirectory))
        {
            await _confirmationService.NotifyAsync(
                    "The VintagestoryData folder could not be located. Please verify it from File > Set Data Folder before launching the game.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        var executable = GameDirectoryLocator.FindExecutable(_gameDirectory);
        if (executable is null)
        {
            await _confirmationService.NotifyAsync(
                    "The Vintage Story executable could not be found. Verify the game folder in File > Set Game Folder.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--dataPath");
            startInfo.ArgumentList.Add(_dataDirectory);

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    GameLaunchDialogTextBuilder.BuildLaunchFailedMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }

    private async void SetCustomShortcutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        string? initialDirectory = null;
        string? initialFileName = null;

        if (!string.IsNullOrWhiteSpace(_customShortcutPath))
            try
            {
                initialDirectory = Path.GetDirectoryName(_customShortcutPath);
                initialFileName = Path.GetFileName(_customShortcutPath);
            }
            catch (Exception)
            {
                initialDirectory = null;
                initialFileName = null;
            }

        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "Select Vintage Story shortcut",
            Filter = "Shortcut files (*.lnk)|*.lnk|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        if (!string.IsNullOrWhiteSpace(initialFileName)) dialog.FileName = initialFileName;

        var result = dialog.ShowDialog();
        if (result == WinForms.DialogResult.OK)
        {
            var selected = dialog.FileName;
            if (!File.Exists(selected))
            {
                await _confirmationService.NotifyAsync(
                        "The selected shortcut could not be found.",
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
                return;
            }

            try
            {
                _userConfiguration.SetCustomShortcutPath(selected);
                _customShortcutPath = _userConfiguration.CustomShortcutPath;
            }
            catch (ArgumentException ex)
            {
                await _confirmationService.NotifyAsync(
                        GameLaunchDialogTextBuilder.BuildInvalidShortcutMessage(ex.Message),
                        "Simple VS Manager",
                        DialogSeverity.Warning)
                    .ConfigureAwait(true);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_customShortcutPath)) return;

        var clear = await _confirmationService.ConfirmAsync(
                "Do you want to clear the custom Vintage Story shortcut?",
                "Simple VS Manager",
                DialogSeverity.Question)
            .ConfigureAwait(true);

        if (!clear) return;

        _userConfiguration.ClearCustomShortcutPath();
        _customShortcutPath = null;
    }
}
