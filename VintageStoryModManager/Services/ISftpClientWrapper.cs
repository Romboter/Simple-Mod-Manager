using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Represents file information from a remote SFTP server.
/// </summary>
public sealed class SftpFileInfo
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public long Size { get; init; }
    public bool IsDirectory { get; init; }
    public DateTime LastModified { get; init; }
}

/// <summary>
///     Result of a host key verification check.
/// </summary>
public enum HostKeyVerificationResult
{
    /// <summary>Host key is trusted (matches stored fingerprint).</summary>
    Trusted,
    /// <summary>Host key is new and needs user approval (TOFU).</summary>
    NewKey,
    /// <summary>Host key doesn't match stored fingerprint (potential security issue).</summary>
    Mismatch
}

/// <summary>
///     Abstraction over SFTP client operations to allow for testing and isolation of SSH.NET dependency.
/// </summary>
public interface ISftpClientWrapper : IDisposable
{
    /// <summary>
    ///     Whether the client is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    ///     The server target this client is configured for.
    /// </summary>
    ServerTarget? Target { get; }

    /// <summary>
    ///     Connects to the SFTP server.
    /// </summary>
    /// <param name="target">Server target configuration.</param>
    /// <param name="password">Password or key passphrase.</param>
    /// <param name="hostKeyVerifier">Callback to verify host key (returns true if trusted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(
        ServerTarget target,
        string? password,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Disconnects from the server.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    ///     Lists files and directories in a remote directory.
    /// </summary>
    /// <param name="remotePath">Remote directory path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file/directory entries.</returns>
    Task<IReadOnlyList<SftpFileInfo>> ListDirectoryAsync(string remotePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Checks if a remote path exists.
    /// </summary>
    Task<bool> ExistsAsync(string remotePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the size of a remote file.
    /// </summary>
    Task<long> GetFileSizeAsync(string remotePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Uploads a file to the server using atomic upload pattern (temp file + rename).
    /// </summary>
    /// <param name="localPath">Local file path.</param>
    /// <param name="remotePath">Remote destination path.</param>
    /// <param name="jobId">Unique job ID for temp file naming.</param>
    /// <param name="progress">Progress callback (bytes uploaded).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UploadFileAsync(
        string localPath,
        string remotePath,
        string jobId,
        IProgress<long>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Renames a remote file or directory.
    /// </summary>
    Task RenameAsync(string oldPath, string newPath, CancellationToken cancellationToken);

    /// <summary>
    ///     Deletes a remote file.
    /// </summary>
    Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a remote directory (including parents if needed).
    /// </summary>
    Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Deletes a remote directory.
    /// </summary>
    Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken);
}
