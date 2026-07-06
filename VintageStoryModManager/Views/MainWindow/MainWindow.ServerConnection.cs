#nullable enable

using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task<bool> ShowHostKeyVerificationAsync(string fingerprint, HostKeyVerificationResult verificationResult)
    {
        // Ensure we're on the UI thread
        if (!Dispatcher.CheckAccess())
        {
            return await Dispatcher.InvokeAsync(async () =>
                await ShowHostKeyVerificationAsync(fingerprint, verificationResult)).Result.ConfigureAwait(false);
        }

        var target = _serverTargetService.GetAllTargets().FirstOrDefault();
        var host = target?.Host ?? "Unknown host";

        return await HostKeyConfirmationDialog.ShowAsync(this, host, fingerprint, verificationResult).ConfigureAwait(false);
    }
}
