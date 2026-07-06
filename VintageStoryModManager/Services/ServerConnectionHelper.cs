#nullable enable

using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class ServerConnectionHelper
{
    internal static Func<string, HostKeyVerificationResult, Task<bool>> CreateHostKeyVerifierWithStorage(
        ServerTargetService serverTargetService,
        ServerTarget serverTarget,
        Func<string, HostKeyVerificationResult, Task<bool>> baseVerifier)
    {
        return async (fingerprint, result) =>
        {
            var trusted = await baseVerifier(fingerprint, result).ConfigureAwait(false);
            if (trusted && result == HostKeyVerificationResult.NewKey && !string.IsNullOrEmpty(serverTarget.Id))
            {
                serverTargetService.TryUpdateHostKeyFingerprint(serverTarget.Id, fingerprint, out _);
            }

            return trusted;
        };
    }

    internal static ISftpClientWrapper CreateSftpClientWrapper(
        ServerTarget target,
        string? password,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier)
    {
        return new SftpClientWrapper();
    }

    internal static async Task<bool> TestServerConnectionAsync(
        ServerTargetService serverTargetService,
        ServerTarget target,
        string? password,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier)
    {
        // Wrap the hostKeyVerifier to store fingerprints
        var wrappedVerifier = CreateHostKeyVerifierWithStorage(serverTargetService, target, hostKeyVerifier);
        using var sftp = new SftpClientWrapper();
        await sftp.ConnectAsync(target, password, wrappedVerifier, CancellationToken.None).ConfigureAwait(false);
        return sftp.IsConnected;
    }
}
