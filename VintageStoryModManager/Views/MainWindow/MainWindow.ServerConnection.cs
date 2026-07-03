#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.Views.Dialogs;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private Func<ServerTarget, Func<string, HostKeyVerificationResult, Task<bool>>, Func<string, HostKeyVerificationResult, Task<bool>>> CreateHostKeyVerifierWithStorage()
    {
        return (serverTarget, baseVerifier) => async (fingerprint, result) =>
        {
            var trusted = await baseVerifier(fingerprint, result).ConfigureAwait(false);
            if (trusted && result == HostKeyVerificationResult.NewKey && !string.IsNullOrEmpty(serverTarget.Id))
            {
                _serverTargetService.TryUpdateHostKeyFingerprint(serverTarget.Id, fingerprint, out _);
            }
            return trusted;
        };
    }

    private ISftpClientWrapper CreateSftpClientWrapper(
        ServerTarget target,
        string? password,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier)
    {
        return new SftpClientWrapper();
    }

    private async Task<bool> TestServerConnectionAsync(
        ServerTarget target,
        string? password,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier)
    {
        // Wrap the hostKeyVerifier to store fingerprints
        var wrappedVerifier = CreateHostKeyVerifierWithStorage()(target, hostKeyVerifier);
        using var sftp = new SftpClientWrapper();
        await sftp.ConnectAsync(target, password, wrappedVerifier, CancellationToken.None).ConfigureAwait(false);
        return sftp.IsConnected;
    }

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
