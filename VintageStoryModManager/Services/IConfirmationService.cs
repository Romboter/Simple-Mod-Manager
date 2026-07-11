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

    /// <summary>Result of a three-way (Yes/No/Cancel) seam confirmation.</summary>
    public enum ThreeWayConfirmResult
    {
        Yes,
        No,
        Cancel
    }

    /// <summary>
    ///     An extra dialog button that maps to the "No" outcome but also fires a side-effecting
    ///     callback when clicked (e.g. "No, don't ask again" persisting a suppression setting).
    /// </summary>
    public sealed class SuppressibleConfirmOption
    {
        public SuppressibleConfirmOption(string buttonText, Action onSelected)
        {
            ButtonText = buttonText ?? throw new ArgumentNullException(nameof(buttonText));
            OnSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
        }

        public string ButtonText { get; }

        public Action OnSelected { get; }
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

        /// <summary>
        ///     Yes/No/Cancel-style confirmation. Custom button text via yesText/noText (null = Yes/No).
        ///     An optional <paramref name="suppressOption"/> adds a fourth button that maps to the
        ///     "No" outcome and additionally invokes its callback when clicked.
        /// </summary>
        Task<ThreeWayConfirmResult> ConfirmThreeWayAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? yesText = null, string? noText = null,
            SuppressibleConfirmOption? suppressOption = null);

        /// <summary>OK/Cancel-style confirmation. Custom button text via okText/cancelText (null = OK/Cancel).</summary>
        Task<bool> ConfirmOkCancelAsync(string message, string title,
            DialogSeverity severity = DialogSeverity.Question,
            string? okText = null, string? cancelText = null);
    }
}
