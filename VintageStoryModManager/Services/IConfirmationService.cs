namespace VintageStoryModManager.Services
{
    /// <summary>Severity of a seam dialog; maps to the themed dialog's icon.</summary>
    public enum DialogSeverity
    {
        Information,
        Warning,
        Error,
        Question
    }

    /// <summary>
    ///     Service for user confirmation and notification dialogs, backed by the app's standard
    ///     themed message dialog. Lets ViewModels prompt without direct window dependencies.
    /// </summary>
    public interface IConfirmationService
    {
        /// <summary>Yes/No-style confirmation. Custom button text via confirmText/cancelText (null = Yes/No).</summary>
        Task<bool> ConfirmAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? confirmText = null, string? cancelText = null);

        /// <summary>OK-only notification.</summary>
        Task NotifyAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Information);

        /// <summary>OK/Cancel-style confirmation. Custom button text via okText/cancelText (null = OK/Cancel).</summary>
        Task<bool> ConfirmOkCancelAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? okText = null, string? cancelText = null);
    }
}
