using System.Windows;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Services
{
    /// <summary>
    ///     Default implementation of IConfirmationService over ModManagerMessageBox
    ///     (the app-standard themed MessageDialogWindow).
    /// </summary>
    public class ConfirmationService : IConfirmationService
    {
        public Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null)
        {
            MessageDialogButtonContentOverrides? overrides = null;
            if (confirmText is not null || cancelText is not null)
                overrides = new MessageDialogButtonContentOverrides { Yes = confirmText, No = cancelText };

            var result = ModManagerMessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MapSeverity(severity),
                buttonContentOverrides: overrides);

            return Task.FromResult(result == MessageBoxResult.Yes);
        }

        public Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information)
        {
            ModManagerMessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MapSeverity(severity));
            return Task.CompletedTask;
        }

        private static MessageBoxImage MapSeverity(DialogSeverity severity) => severity switch
        {
            DialogSeverity.Warning => MessageBoxImage.Warning,
            DialogSeverity.Error => MessageBoxImage.Error,
            DialogSeverity.Question => MessageBoxImage.Question,
            _ => MessageBoxImage.Information
        };
    }
}
