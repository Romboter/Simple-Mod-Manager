using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Renci.SshNet;
using Renci.SshNet.Common;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     SSH.NET-based implementation of SFTP client operations.
/// </summary>
public sealed class SftpClientWrapper : ISftpClientWrapper
{
    private SftpClient? _client;
    private readonly object _connectLock = new();
    private bool _disposed;

    public bool IsConnected => _client?.IsConnected == true;

    public ServerTarget? Target { get; private set; }

    public async Task ConnectAsync(
        ServerTarget target,
        string? password,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier,
        CancellationToken cancellationToken)
    {
        if (target is null)
            throw new ArgumentNullException(nameof(target));

        lock (_connectLock)
        {
            if (_client?.IsConnected == true)
            {
                _client.Disconnect();
                _client.Dispose();
                _client = null;
            }
        }

        var connectionInfo = BuildConnectionInfo(target, password);
        var client = new SftpClient(connectionInfo);

        var hostKeyVerificationTcs = new TaskCompletionSource<bool>();
        string? receivedFingerprint = null;
        HostKeyVerificationResult verificationResult = HostKeyVerificationResult.NewKey;

        client.HostKeyReceived += (sender, e) =>
        {
            receivedFingerprint = FormatFingerprint(e.HostKey);

            if (!string.IsNullOrEmpty(target.KnownHostKeyFingerprint))
            {
                if (string.Equals(target.KnownHostKeyFingerprint, receivedFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    verificationResult = HostKeyVerificationResult.Trusted;
                    e.CanTrust = true;
                    hostKeyVerificationTcs.TrySetResult(true);
                    return;
                }
                else
                {
                    verificationResult = HostKeyVerificationResult.Mismatch;
                }
            }
            else
            {
                verificationResult = HostKeyVerificationResult.NewKey;
            }

            // For new or mismatched keys, we need to ask the user
            // Set e.CanTrust to false initially, we'll handle this after connection attempt
            e.CanTrust = false;
            hostKeyVerificationTcs.TrySetResult(false);
        };

        try
        {
            // Attempt connection
            await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
        }
        catch (SshConnectionException ex) when (ex.Message.Contains("Key exchange", StringComparison.OrdinalIgnoreCase) ||
                                                  ex.Message.Contains("host key", StringComparison.OrdinalIgnoreCase))
        {
            // Host key was rejected - ask user for verification
            if (receivedFingerprint != null)
            {
                var userTrusts = await hostKeyVerifier(receivedFingerprint, verificationResult).ConfigureAwait(false);

                if (!userTrusts)
                {
                    client.Dispose();
                    throw new SshConnectionException("Host key verification failed: user rejected the key.");
                }

                // User trusted the key - retry connection with trust
                client.Dispose();
                client = new SftpClient(connectionInfo);

                client.HostKeyReceived += (sender, e) =>
                {
                    var fp = FormatFingerprint(e.HostKey);
                    e.CanTrust = string.Equals(fp, receivedFingerprint, StringComparison.OrdinalIgnoreCase);
                };

                await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                client.Dispose();
                throw;
            }
        }
        catch (Exception)
        {
            client.Dispose();
            throw;
        }

        lock (_connectLock)
        {
            _client = client;
            Target = target;
        }
    }

    public Task DisconnectAsync()
    {
        lock (_connectLock)
        {
            if (_client?.IsConnected == true)
            {
                _client.Disconnect();
            }
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SftpFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);
        var results = new List<SftpFileInfo>();

        await Task.Run(() =>
        {
            var entries = _client!.ListDirectory(normalizedPath);

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip . and ..
                if (entry.Name is "." or "..")
                    continue;

                results.Add(new SftpFileInfo
                {
                    Name = entry.Name,
                    FullName = entry.FullName,
                    Size = entry.Length,
                    IsDirectory = entry.IsDirectory,
                    LastModified = entry.LastWriteTime
                });
            }
        }, cancellationToken).ConfigureAwait(false);

        return results;
    }

    public async Task<bool> ExistsAsync(string remotePath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);

        return await Task.Run(() => _client!.Exists(normalizedPath), cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> GetFileSizeAsync(string remotePath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);

        return await Task.Run(() =>
        {
            var attrs = _client!.GetAttributes(normalizedPath);
            return attrs.Size;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadFileAsync(
        string localPath,
        string remotePath,
        string jobId,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);
        var tempPath = $"{normalizedPath}.tmp.{jobId}";
        var localFileInfo = new FileInfo(localPath);
        var expectedSize = localFileInfo.Length;

        try
        {
            // Upload to temp file
            await using var localStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

            await Task.Run(() =>
            {
                _client!.UploadFile(localStream, tempPath, bytesUploaded =>
                {
                    progress?.Report((long)bytesUploaded);
                });
            }, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Verify size
            var remoteSize = await GetFileSizeAsync(tempPath, cancellationToken).ConfigureAwait(false);

            if (remoteSize != expectedSize)
            {
                // Size mismatch - clean up and throw
                try
                {
                    await Task.Run(() => _client!.DeleteFile(tempPath), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to clean up temp file after size mismatch: {ex.Message}");
                }

                throw new IOException($"Upload verification failed: expected {expectedSize} bytes, but remote file is {remoteSize} bytes.");
            }

            // Delete existing file if present
            if (await ExistsAsync(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                await Task.Run(() => _client!.DeleteFile(normalizedPath), cancellationToken).ConfigureAwait(false);
            }

            // Atomic rename
            await Task.Run(() => _client!.RenameFile(tempPath, normalizedPath), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Try to clean up temp file on cancellation
            try
            {
                if (await ExistsAsync(tempPath, CancellationToken.None).ConfigureAwait(false))
                {
                    await Task.Run(() => _client!.DeleteFile(tempPath), CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clean up temp file after cancellation: {ex.Message}");
            }
            throw;
        }
    }

    public async Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedOld = NormalizePath(oldPath);
        var normalizedNew = NormalizePath(newPath);

        await Task.Run(() => _client!.RenameFile(normalizedOld, normalizedNew), cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);

        await Task.Run(() => _client!.DeleteFile(normalizedPath), cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);

        // Create directory and any missing parents
        await Task.Run(() =>
        {
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentPath = "";

            foreach (var segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                currentPath = string.IsNullOrEmpty(currentPath) ? $"/{segment}" : $"{currentPath}/{segment}";

                if (!_client!.Exists(currentPath))
                {
                    _client.CreateDirectory(currentPath);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken)
    {
        EnsureConnected();

        var normalizedPath = NormalizePath(remotePath);

        await Task.Run(() => _client!.DeleteDirectory(normalizedPath), cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_connectLock)
        {
            if (_client != null)
            {
                if (_client.IsConnected)
                {
                    try
                    {
                        _client.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error disconnecting SFTP client: {ex.Message}");
                    }
                }
                _client.Dispose();
                _client = null;
            }
        }

        _disposed = true;
    }

    private void EnsureConnected()
    {
        if (_client?.IsConnected != true)
            throw new InvalidOperationException("Not connected to SFTP server.");
    }

    private static ConnectionInfo BuildConnectionInfo(ServerTarget target, string? password)
    {
        var authMethods = new List<AuthenticationMethod>();

        if (target.AuthType == AuthenticationType.PrivateKey && !string.IsNullOrEmpty(target.PrivateKeyPath))
        {
            try
            {
                PrivateKeyFile keyFile;
                if (!string.IsNullOrEmpty(password))
                {
                    keyFile = new PrivateKeyFile(target.PrivateKeyPath, password);
                }
                else
                {
                    keyFile = new PrivateKeyFile(target.PrivateKeyPath);
                }
                authMethods.Add(new PrivateKeyAuthenticationMethod(target.Username, keyFile));
            }
            catch (Exception ex)
            {
                throw new SshAuthenticationException($"Failed to load private key: {ex.Message}", ex);
            }
        }
        else if (!string.IsNullOrEmpty(password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(target.Username, password));
        }

        if (authMethods.Count == 0)
        {
            throw new SshAuthenticationException("No authentication method available. Please provide a password or private key.");
        }

        return new ConnectionInfo(target.Host, target.Port, target.Username, authMethods.ToArray())
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static string FormatFingerprint(byte[] hostKey)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(hostKey);
        return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
    }

    private static string NormalizePath(string path)
    {
        // Convert Windows paths to Unix-style and ensure it starts with /
        var normalized = path.Replace('\\', '/');

        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        // Remove trailing slash unless it's the root
        if (normalized.Length > 1 && normalized.EndsWith('/'))
            normalized = normalized.TrimEnd('/');

        return normalized;
    }
}
