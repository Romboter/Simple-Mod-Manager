namespace VintageStoryModManager.Models;

/// <summary>
///     Profile type distinguishing local profiles from server-sync profiles.
/// </summary>
public enum ProfileType
{
    /// <summary>
    ///     Standard local profile - mods managed locally only.
    /// </summary>
    Local = 0,

    /// <summary>
    ///     Server profile - can sync mods to a remote server via SFTP.
    /// </summary>
    Server = 1
}

/// <summary>
///     Action to be taken for a file during sync.
/// </summary>
public enum SyncAction
{
    /// <summary>
    ///     Upload new or changed file to server.
    /// </summary>
    Upload,

    /// <summary>
    ///     File unchanged, skip.
    /// </summary>
    Skip,

    /// <summary>
    ///     File exists on server but not locally (orphan) - may be deleted.
    /// </summary>
    Delete,

    /// <summary>
    ///     Create directory on server.
    /// </summary>
    CreateDirectory,

    /// <summary>
    ///     Delete directory on server.
    /// </summary>
    DeleteDirectory
}

/// <summary>
///     Result status for an individual file sync operation.
/// </summary>
public enum SyncItemResult
{
    Pending,
    InProgress,
    Success,
    Failed,
    Skipped,
    Cancelled
}

/// <summary>
///     Represents a file or directory to be synced.
/// </summary>
public sealed class SyncPreviewItem
{
    /// <summary>
    ///     Relative path from the data directory root (e.g., "Mods/mymod.zip").
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    ///     Full local file path.
    /// </summary>
    public required string LocalPath { get; init; }

    /// <summary>
    ///     Full remote file path.
    /// </summary>
    public required string RemotePath { get; init; }

    /// <summary>
    ///     Action to perform for this item.
    /// </summary>
    public required SyncAction Action { get; init; }

    /// <summary>
    ///     Local file size in bytes (0 for directories or deletions).
    /// </summary>
    public long LocalSize { get; init; }

    /// <summary>
    ///     Remote file size in bytes (0 for new files or directories).
    /// </summary>
    public long RemoteSize { get; init; }

    /// <summary>
    ///     Human-readable reason for the action (e.g., "New file", "Size changed").
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Whether this item is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }
}

/// <summary>
///     Contains the full preview of a sync operation.
/// </summary>
public sealed class SyncPreview
{
    /// <summary>
    ///     All items in the sync preview.
    /// </summary>
    public List<SyncPreviewItem> Items { get; } = new();

    /// <summary>
    ///     Items that will be uploaded.
    /// </summary>
    public IEnumerable<SyncPreviewItem> Uploads => Items.Where(i => i.Action == SyncAction.Upload);

    /// <summary>
    ///     Items that will be skipped (unchanged).
    /// </summary>
    public IEnumerable<SyncPreviewItem> Skipped => Items.Where(i => i.Action == SyncAction.Skip);

    /// <summary>
    ///     Orphan items that may be deleted (require confirmation).
    /// </summary>
    public IEnumerable<SyncPreviewItem> Orphans => Items.Where(i => i.Action is SyncAction.Delete or SyncAction.DeleteDirectory);

    /// <summary>
    ///     Directories that need to be created.
    /// </summary>
    public IEnumerable<SyncPreviewItem> DirectoriesToCreate => Items.Where(i => i.Action == SyncAction.CreateDirectory);

    /// <summary>
    ///     Total bytes to upload.
    /// </summary>
    public long TotalUploadBytes => Uploads.Sum(i => i.LocalSize);

    /// <summary>
    ///     Total number of files to upload.
    /// </summary>
    public int TotalUploadCount => Uploads.Count();

    /// <summary>
    ///     Whether there are any orphan files on the server.
    /// </summary>
    public bool HasOrphans => Orphans.Any();
}

/// <summary>
///     Progress information during sync execution.
/// </summary>
public sealed class SyncProgress
{
    /// <summary>
    ///     Name of the file currently being processed.
    /// </summary>
    public string CurrentFile { get; init; } = string.Empty;

    /// <summary>
    ///     Bytes transferred for the current file.
    /// </summary>
    public long CurrentFileBytes { get; init; }

    /// <summary>
    ///     Total bytes for the current file.
    /// </summary>
    public long CurrentFileTotalBytes { get; init; }

    /// <summary>
    ///     Number of files completed.
    /// </summary>
    public int CompletedFiles { get; init; }

    /// <summary>
    ///     Total number of files to process.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    ///     Total bytes completed across all files.
    /// </summary>
    public long CompletedBytes { get; init; }

    /// <summary>
    ///     Total bytes to transfer.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    ///     Current phase of the sync operation.
    /// </summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    ///     Overall progress percentage (0-100).
    /// </summary>
    public double OverallPercent => TotalBytes > 0 ? (double)CompletedBytes / TotalBytes * 100 : 0;

    /// <summary>
    ///     Current file progress percentage (0-100).
    /// </summary>
    public double CurrentFilePercent => CurrentFileTotalBytes > 0 ? (double)CurrentFileBytes / CurrentFileTotalBytes * 100 : 0;
}

/// <summary>
///     Result of a completed sync item.
/// </summary>
public sealed class SyncItemOutcome
{
    public required SyncPreviewItem Item { get; init; }
    public required SyncItemResult Result { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
///     Overall result of a sync operation.
/// </summary>
public sealed class SyncResult
{
    public List<SyncItemOutcome> Outcomes { get; } = new();

    public bool WasCancelled { get; set; }

    public int SuccessCount => Outcomes.Count(o => o.Result == SyncItemResult.Success);
    public int FailedCount => Outcomes.Count(o => o.Result == SyncItemResult.Failed);
    public int SkippedCount => Outcomes.Count(o => o.Result is SyncItemResult.Skipped or SyncItemResult.Cancelled);

    public bool HasErrors => FailedCount > 0;
    public bool IsFullySuccessful => !WasCancelled && FailedCount == 0;
}
