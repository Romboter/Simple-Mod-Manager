#nullable enable

using SimpleVsManager.Cloud;
using System.Net.Http;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task ExecuteCloudOperationAsync(Func<FirebaseModlistStore, Task> operation, string actionDescription)
    {
        try
        {
            InternetAccessManager.ThrowIfInternetAccessDisabled();
        }
        catch (InternetAccessDisabledException ex)
        {
            await _confirmationService.NotifyAsync(
                    ex.Message,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
            return;
        }

        FirebaseModlistStore store;
        try
        {
            store = await _cloudWorkflowCoordinator.EnsureStoreInitializedAsync();
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus($"Failed to initialize cloud storage: {ex}", true);
            await _confirmationService.NotifyAsync(
                    CloudOperationDialogTextBuilder.BuildInitializationFailureMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        try
        {
            await operation(store);
            EnsureFirebaseAuthBackedUpIfAvailable();
        }
        catch (InternetAccessDisabledException ex)
        {
            await _confirmationService.NotifyAsync(
                    ex.Message,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            StatusLogService.AppendStatus($"Network error while attempting to {actionDescription}: {ex.Message}", true);
            await _confirmationService.NotifyAsync(
                    CloudOperationDialogTextBuilder.BuildActionFailureMessage(
                        actionDescription,
                        ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            StatusLogService.AppendStatus(
                $"Cloud operation failed while attempting to {actionDescription}: {ex.Message}", true);
            await _confirmationService.NotifyAsync(
                    CloudOperationDialogTextBuilder.BuildActionFailureMessage(
                        actionDescription,
                        ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus($"Unexpected error while attempting to {actionDescription}: {ex}", true);
            await _confirmationService.NotifyAsync(
                    CloudOperationDialogTextBuilder.BuildUnexpectedActionFailureMessage(
                        actionDescription,
                        ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
    }
}
