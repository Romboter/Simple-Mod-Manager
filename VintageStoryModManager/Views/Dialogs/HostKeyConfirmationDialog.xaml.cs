using System.Windows;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views.Dialogs;

/// <summary>
///     Dialog for TOFU (Trust On First Use) host key verification.
/// </summary>
public partial class HostKeyConfirmationDialog : Window
{
    public HostKeyConfirmationDialog(string host, string fingerprint, HostKeyVerificationResult verificationResult)
    {
        InitializeComponent();

        HostText.Text = host;
        FingerprintText.Text = fingerprint;

        if (verificationResult == HostKeyVerificationResult.Mismatch)
        {
            HeaderText.Text = "Host Key Mismatch";
            ExplanationText.Text = "The server's host key doesn't match the previously trusted fingerprint. This could indicate a security issue.";
            MismatchWarning.Visibility = Visibility.Visible;
        }
        else
        {
            HeaderText.Text = "New Server Connection";
            ExplanationText.Text = "You are connecting to this server for the first time. Please verify the fingerprint is correct before trusting this host.";
        }
    }

    private void TrustButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    ///     Shows the host key confirmation dialog and returns whether the user trusts the key.
    /// </summary>
    public static Task<bool> ShowAsync(Window owner, string host, string fingerprint, HostKeyVerificationResult verificationResult)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Ensure we're on the UI thread
        if (owner.Dispatcher.CheckAccess())
        {
            ShowDialogInternal();
        }
        else
        {
            owner.Dispatcher.Invoke(ShowDialogInternal);
        }

        return tcs.Task;

        void ShowDialogInternal()
        {
            var dialog = new HostKeyConfirmationDialog(host, fingerprint, verificationResult)
            {
                Owner = owner
            };

            var result = dialog.ShowDialog() == true;
            tcs.TrySetResult(result);
        }
    }
}
