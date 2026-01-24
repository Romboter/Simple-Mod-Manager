using System.Diagnostics;
using System.IO;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Orchestrates mod synchronization between local profiles and remote servers.
/// </summary>
public sealed class SyncEngine
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private CancellationTokenSource? _currentSyncCts;
    private string? _currentJobId;

    /// <summary>
    ///     Whether a sync operation is currently in progress.
    /// </summary>
    public bool IsSyncInProgress => _syncLock.CurrentCount == 0;

    /// <summary>
    ///     The current sync job ID, or null if no sync is in progress.
    /// </summary>
    public string? CurrentJobId => _currentJobId;

    /// <summary>
    ///     Generates a preview of all sync operations without executing them.
    /// </summary>
    /// <param name="localDataPath">Local Vintage Story data directory path.</param>
    /// <param name="remoteDataPath">Remote data directory path on server.</param>
    /// <param name="includeModConfig">Whether to include ModConfig folder.</param>
    /// <param name="sftp">Connected SFTP client.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync preview containing all planned operations.</returns>
    public async Task<SyncPreview> GeneratePreviewAsync(
        string localDataPath,
        string remoteDataPath,
        bool includeModConfig,
        ISftpClientWrapper sftp,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!sftp.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected.");

        if (string.IsNullOrWhiteSpace(localDataPath))
            throw new ArgumentException("Local data path cannot be empty.", nameof(localDataPath));

        if (string.IsNullOrWhiteSpace(remoteDataPath))
            throw new ArgumentException("Remote data path cannot be empty.", nameof(remoteDataPath));

        var preview = new SyncPreview();

        try
        {
            // Process Mods folder
            progress?.Report("Scanning local Mods folder...");
            var localModsPath = Path.Combine(localDataPath, "Mods");
            var remoteModsPath = NormalizeRemotePath(remoteDataPath, "Mods");

            Debug.WriteLine($"SyncEngine: Local Mods path = {localModsPath}");
            Debug.WriteLine($"SyncEngine: Remote Mods path = {remoteModsPath}");

            await ProcessFolderAsync(preview, localModsPath, remoteModsPath, "Mods", sftp, progress, cancellationToken)
                .ConfigureAwait(false);

            // Process ModConfig folder if requested
            if (includeModConfig)
            {
                progress?.Report("Scanning local ModConfig folder...");
                var localModConfigPath = Path.Combine(localDataPath, "ModConfig");
                var remoteModConfigPath = NormalizeRemotePath(remoteDataPath, "ModConfig");

                if (Directory.Exists(localModConfigPath))
                {
                    await ProcessFolderAsync(preview, localModConfigPath, remoteModConfigPath, "ModConfig", sftp, progress, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            progress?.Report($"Preview complete: {preview.TotalUploadCount} to upload, {preview.Orphans.Count()} orphans");
            return preview;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"SyncEngine: GeneratePreviewAsync failed - {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"SyncEngine: Stack trace - {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    ///     Executes the sync operation based on the preview.
    /// </summary>
    /// <param name="preview">The sync preview from GeneratePreviewAsync.</param>
    /// <param name="confirmedDeletions">List of orphan items the user confirmed for deletion.</param>
    /// <param name="sftp">Connected SFTP client.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with outcomes for each item.</returns>
    public async Task<SyncResult> ExecuteSyncAsync(
        SyncPreview preview,
        IReadOnlyList<SyncPreviewItem> confirmedDeletions,
        ISftpClientWrapper sftp,
        IProgress<SyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("A sync operation is already in progress.");

        _currentJobId = Guid.NewGuid().ToString("N")[..8];
        _currentSyncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var result = new SyncResult();

        try
        {
            var ct = _currentSyncCts.Token;

            // Create directories first
            var dirsToCreate = preview.DirectoriesToCreate.ToList();
            foreach (var dir in dirsToCreate)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await sftp.CreateDirectoryAsync(dir.RemotePath, ct).ConfigureAwait(false);
                    result.Outcomes.Add(new SyncItemOutcome
                    {
                        Item = dir,
                        Result = SyncItemResult.Success
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result.Outcomes.Add(new SyncItemOutcome
                    {
                        Item = dir,
                        Result = SyncItemResult.Failed,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Upload files
            var uploads = preview.Uploads.ToList();
            var totalBytes = uploads.Sum(u => u.LocalSize);
            var completedBytes = 0L;
            var completedFiles = 0;

            foreach (var item in uploads)
            {
                ct.ThrowIfCancellationRequested();

                progress?.Report(new SyncProgress
                {
                    Phase = "Uploading",
                    CurrentFile = item.RelativePath,
                    CompletedFiles = completedFiles,
                    TotalFiles = uploads.Count,
                    CompletedBytes = completedBytes,
                    TotalBytes = totalBytes,
                    CurrentFileBytes = 0,
                    CurrentFileTotalBytes = item.LocalSize
                });

                try
                {
                    var fileProgress = new Progress<long>(bytes =>
                    {
                        progress?.Report(new SyncProgress
                        {
                            Phase = "Uploading",
                            CurrentFile = item.RelativePath,
                            CompletedFiles = completedFiles,
                            TotalFiles = uploads.Count,
                            CompletedBytes = completedBytes + bytes,
                            TotalBytes = totalBytes,
                            CurrentFileBytes = bytes,
                            CurrentFileTotalBytes = item.LocalSize
                        });
                    });

                    await sftp.UploadFileAsync(item.LocalPath, item.RemotePath, _currentJobId, fileProgress, ct)
                        .ConfigureAwait(false);

                    result.Outcomes.Add(new SyncItemOutcome
                    {
                        Item = item,
                        Result = SyncItemResult.Success
                    });

                    completedBytes += item.LocalSize;
                    completedFiles++;
                }
                catch (OperationCanceledException)
                {
                    result.Outcomes.Add(new SyncItemOutcome
                    {
                        Item = item,
                        Result = SyncItemResult.Cancelled
                    });
                    throw;
                }
                catch (Exception ex)
                {
                    result.Outcomes.Add(new SyncItemOutcome
                    {
                        Item = item,
                        Result = SyncItemResult.Failed,
                        ErrorMessage = ex.Message
                    });
                    // Continue with other files (best-effort)
                }
            }

            // Add skip outcomes
            foreach (var item in preview.Skipped)
            {
                result.Outcomes.Add(new SyncItemOutcome
                {
                    Item = item,
                    Result = SyncItemResult.Skipped
                });
            }

            // Process confirmed deletions
            if (confirmedDeletions.Count > 0)
            {
                progress?.Report(new SyncProgress
                {
                    Phase = "Deleting orphans",
                    CurrentFile = "",
                    CompletedFiles = completedFiles,
                    TotalFiles = uploads.Count + confirmedDeletions.Count,
                    CompletedBytes = totalBytes,
                    TotalBytes = totalBytes
                });

                // Delete files first, then directories (in reverse order to handle nested)
                var filesToDelete = confirmedDeletions.Where(d => d.Action == SyncAction.Delete).ToList();
                var dirsToDelete = confirmedDeletions.Where(d => d.Action == SyncAction.DeleteDirectory)
                    .OrderByDescending(d => d.RemotePath.Length)
                    .ToList();

                foreach (var item in filesToDelete)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        await sftp.DeleteFileAsync(item.RemotePath, ct).ConfigureAwait(false);
                        result.Outcomes.Add(new SyncItemOutcome
                        {
                            Item = item,
                            Result = SyncItemResult.Success
                        });
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        result.Outcomes.Add(new SyncItemOutcome
                        {
                            Item = item,
                            Result = SyncItemResult.Failed,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                foreach (var item in dirsToDelete)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        await sftp.DeleteDirectoryAsync(item.RemotePath, ct).ConfigureAwait(false);
                        result.Outcomes.Add(new SyncItemOutcome
                        {
                            Item = item,
                            Result = SyncItemResult.Success
                        });
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        result.Outcomes.Add(new SyncItemOutcome
                        {
                            Item = item,
                            Result = SyncItemResult.Failed,
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }

            progress?.Report(new SyncProgress
            {
                Phase = "Complete",
                CurrentFile = "",
                CompletedFiles = uploads.Count,
                TotalFiles = uploads.Count,
                CompletedBytes = totalBytes,
                TotalBytes = totalBytes
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            result.WasCancelled = true;
            return result;
        }
        finally
        {
            _currentSyncCts?.Dispose();
            _currentSyncCts = null;
            _currentJobId = null;
            _syncLock.Release();
        }
    }

    /// <summary>
    ///     Cancels the current sync operation if one is in progress.
    /// </summary>
    public void CancelCurrentSync()
    {
        _currentSyncCts?.Cancel();
    }

    private async Task ProcessFolderAsync(
        SyncPreview preview,
        string localPath,
        string remotePath,
        string folderName,
        ISftpClientWrapper sftp,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        // Get local files
        var localFiles = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(localPath))
        {
            try
            {
                progress?.Report($"Enumerating files in {folderName}...");
                foreach (var file in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(localPath, file);
                    localFiles[relativePath] = new FileInfo(file);
                }
                Debug.WriteLine($"SyncEngine: Found {localFiles.Count} local files in {folderName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"SyncEngine: Failed to enumerate local files in {localPath} - {ex.GetType().Name}: {ex.Message}");
                throw new InvalidOperationException($"Failed to enumerate local files in {folderName}: {ex.Message}", ex);
            }
        }
        else
        {
            Debug.WriteLine($"SyncEngine: Local folder does not exist: {localPath}");
        }

        // Check if remote folder exists
        var remoteExists = await sftp.ExistsAsync(remotePath, cancellationToken).ConfigureAwait(false);

        if (!remoteExists)
        {
            // Need to create the folder
            preview.Items.Add(new SyncPreviewItem
            {
                RelativePath = folderName,
                LocalPath = localPath,
                RemotePath = remotePath,
                Action = SyncAction.CreateDirectory,
                IsDirectory = true,
                Reason = "New folder"
            });
        }

        // Get remote files
        var remoteFiles = new Dictionary<string, SftpFileInfo>(StringComparer.OrdinalIgnoreCase);
        if (remoteExists)
        {
            await EnumerateRemoteFilesAsync(sftp, remotePath, "", remoteFiles, cancellationToken).ConfigureAwait(false);
        }

        // Compare and categorize
        foreach (var (relativePath, localFile) in localFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remoteFilePath = $"{remotePath}/{relativePath.Replace('\\', '/')}";
            var fullRelativePath = $"{folderName}/{relativePath}";

            if (remoteFiles.TryGetValue(relativePath, out var remoteFile))
            {
                // File exists on both sides - compare by size
                if (localFile.Length != remoteFile.Size)
                {
                    preview.Items.Add(new SyncPreviewItem
                    {
                        RelativePath = fullRelativePath,
                        LocalPath = localFile.FullName,
                        RemotePath = remoteFilePath,
                        Action = SyncAction.Upload,
                        LocalSize = localFile.Length,
                        RemoteSize = remoteFile.Size,
                        Reason = "Size changed"
                    });
                }
                else
                {
                    preview.Items.Add(new SyncPreviewItem
                    {
                        RelativePath = fullRelativePath,
                        LocalPath = localFile.FullName,
                        RemotePath = remoteFilePath,
                        Action = SyncAction.Skip,
                        LocalSize = localFile.Length,
                        RemoteSize = remoteFile.Size,
                        Reason = "Unchanged"
                    });
                }

                remoteFiles.Remove(relativePath);
            }
            else
            {
                // File only exists locally - upload
                // Check if parent directory needs to be created
                var parentDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    var parentRemotePath = $"{remotePath}/{parentDir.Replace('\\', '/')}";
                    var parentFullRelative = $"{folderName}/{parentDir}";

                    if (!preview.Items.Any(i => i.RemotePath == parentRemotePath && i.Action == SyncAction.CreateDirectory))
                    {
                        // Check if directory exists remotely
                        if (!await sftp.ExistsAsync(parentRemotePath, cancellationToken).ConfigureAwait(false))
                        {
                            preview.Items.Add(new SyncPreviewItem
                            {
                                RelativePath = parentFullRelative,
                                LocalPath = Path.Combine(localPath, parentDir),
                                RemotePath = parentRemotePath,
                                Action = SyncAction.CreateDirectory,
                                IsDirectory = true,
                                Reason = "New folder"
                            });
                        }
                    }
                }

                preview.Items.Add(new SyncPreviewItem
                {
                    RelativePath = fullRelativePath,
                    LocalPath = localFile.FullName,
                    RemotePath = remoteFilePath,
                    Action = SyncAction.Upload,
                    LocalSize = localFile.Length,
                    Reason = "New file"
                });
            }
        }

        // Remaining remote files are orphans
        foreach (var (relativePath, remoteFile) in remoteFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remoteFilePath = remoteFile.FullName;
            var fullRelativePath = $"{folderName}/{relativePath}";

            if (remoteFile.IsDirectory)
            {
                preview.Items.Add(new SyncPreviewItem
                {
                    RelativePath = fullRelativePath,
                    LocalPath = Path.Combine(localPath, relativePath),
                    RemotePath = remoteFilePath,
                    Action = SyncAction.DeleteDirectory,
                    IsDirectory = true,
                    RemoteSize = 0,
                    Reason = "Not in local profile"
                });
            }
            else
            {
                preview.Items.Add(new SyncPreviewItem
                {
                    RelativePath = fullRelativePath,
                    LocalPath = Path.Combine(localPath, relativePath),
                    RemotePath = remoteFilePath,
                    Action = SyncAction.Delete,
                    RemoteSize = remoteFile.Size,
                    Reason = "Not in local profile"
                });
            }
        }
    }

    private async Task EnumerateRemoteFilesAsync(
        ISftpClientWrapper sftp,
        string basePath,
        string relativePath,
        Dictionary<string, SftpFileInfo> results,
        CancellationToken cancellationToken)
    {
        var currentPath = string.IsNullOrEmpty(relativePath) ? basePath : $"{basePath}/{relativePath}";

        IReadOnlyList<SftpFileInfo> entries;
        try
        {
            entries = await sftp.ListDirectoryAsync(currentPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to list directory {currentPath}: {ex.Message}");
            return;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryRelativePath = string.IsNullOrEmpty(relativePath)
                ? entry.Name
                : $"{relativePath}/{entry.Name}";

            if (entry.IsDirectory)
            {
                // Recurse into subdirectory
                await EnumerateRemoteFilesAsync(sftp, basePath, entryRelativePath, results, cancellationToken)
                    .ConfigureAwait(false);

                // Also add the directory itself for orphan tracking
                results[entryRelativePath] = entry;
            }
            else
            {
                results[entryRelativePath] = entry;
            }
        }
    }

    private static string NormalizeRemotePath(string basePath, string subPath)
    {
        var normalized = basePath.Replace('\\', '/').TrimEnd('/');
        return $"{normalized}/{subPath}";
    }

    /// <summary>
    ///     Checks if a remote path appears dangerous (e.g., root directory or system paths).
    /// </summary>
    public static bool IsDangerousPath(string remotePath)
    {
        var normalized = remotePath.Replace('\\', '/').Trim();

        // Root directory
        if (normalized is "/" or "")
            return true;

        // Common dangerous Unix paths
        var dangerousPaths = new[]
        {
            "/bin", "/boot", "/dev", "/etc", "/lib", "/lib64",
            "/opt", "/proc", "/root", "/run", "/sbin", "/srv",
            "/sys", "/tmp", "/usr", "/var"
        };

        foreach (var dangerous in dangerousPaths)
        {
            if (normalized.Equals(dangerous, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(dangerous + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Very short paths are suspicious
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return true;

        return false;
    }
}
