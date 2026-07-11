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

        public Task<ThreeWayConfirmResult> ConfirmThreeWayAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? yesText = null, string? noText = null,
            SuppressibleConfirmOption? suppressOption = null)
        {
            MessageDialogButtonContentOverrides? overrides = null;
            if (yesText is not null || noText is not null)
                overrides = new MessageDialogButtonContentOverrides { Yes = yesText, No = noText };

            MessageDialogExtraButton? extraButton = null;
            if (suppressOption is not null)
                extraButton = new MessageDialogExtraButton(
                    suppressOption.ButtonText,
                    MessageBoxResult.No,
                    suppressOption.OnSelected);

            var result = ModManagerMessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNoCancel,
                MapSeverity(severity),
                extraButton,
                overrides);

            return Task.FromResult(result switch
            {
                MessageBoxResult.Yes => ThreeWayConfirmResult.Yes,
                MessageBoxResult.No => ThreeWayConfirmResult.No,
                _ => ThreeWayConfirmResult.Cancel
            });
        }

        public Task<bool> ConfirmOkCancelAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? okText = null, string? cancelText = null)
        {
            MessageDialogButtonContentOverrides? overrides = null;
            if (okText is not null || cancelText is not null)
                overrides = new MessageDialogButtonContentOverrides { Ok = okText, Cancel = cancelText };

            var result = ModManagerMessageBox.Show(
                message,
                title,
                MessageBoxButton.OKCancel,
                MapSeverity(severity),
                buttonContentOverrides: overrides);

            return Task.FromResult(result == MessageBoxResult.OK);
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
