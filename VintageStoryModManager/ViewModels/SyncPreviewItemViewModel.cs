using CommunityToolkit.Mvvm.ComponentModel;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     ViewModel wrapper for a sync preview item with selection state for confirmations.
/// </summary>
public sealed class SyncPreviewItemViewModel : ObservableObject
{
    private bool _isConfirmedForDeletion;
    private SyncItemResult _result = SyncItemResult.Pending;
    private string? _errorMessage;

    public SyncPreviewItemViewModel(SyncPreviewItem item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    ///     The underlying sync preview item.
    /// </summary>
    public SyncPreviewItem Item { get; }

    /// <summary>
    ///     Relative path for display.
    /// </summary>
    public string RelativePath => Item.RelativePath;

    /// <summary>
    ///     The action to be performed.
    /// </summary>
    public SyncAction Action => Item.Action;

    /// <summary>
    ///     Display string for the action.
    /// </summary>
    public string ActionDisplay => Action switch
    {
        SyncAction.Upload => "Upload",
        SyncAction.Skip => "Skip",
        SyncAction.Delete => "Delete",
        SyncAction.CreateDirectory => "Create folder",
        SyncAction.DeleteDirectory => "Delete folder",
        _ => "Unknown"
    };

    /// <summary>
    ///     Reason for the action.
    /// </summary>
    public string? Reason => Item.Reason;

    /// <summary>
    ///     Local file size for display.
    /// </summary>
    public string LocalSizeDisplay => FormatSize(Item.LocalSize);

    /// <summary>
    ///     Remote file size for display.
    /// </summary>
    public string RemoteSizeDisplay => FormatSize(Item.RemoteSize);

    /// <summary>
    ///     Whether this item is a directory.
    /// </summary>
    public bool IsDirectory => Item.IsDirectory;

    /// <summary>
    ///     Whether this is an orphan item that can be selected for deletion.
    /// </summary>
    public bool IsOrphan => Action is SyncAction.Delete or SyncAction.DeleteDirectory;

    /// <summary>
    ///     Whether the user has confirmed this orphan for deletion.
    /// </summary>
    public bool IsConfirmedForDeletion
    {
        get => _isConfirmedForDeletion;
        set => SetProperty(ref _isConfirmedForDeletion, value);
    }

    /// <summary>
    ///     Current result status of this item.
    /// </summary>
    public SyncItemResult Result
    {
        get => _result;
        set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(ResultDisplay));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>
    ///     Error message if the sync failed.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>
    ///     Display string for the result.
    /// </summary>
    public string ResultDisplay => Result switch
    {
        SyncItemResult.Pending => "",
        SyncItemResult.InProgress => "Uploading...",
        SyncItemResult.Success => "Done",
        SyncItemResult.Failed => "Failed",
        SyncItemResult.Skipped => "Skipped",
        SyncItemResult.Cancelled => "Cancelled",
        _ => ""
    };

    /// <summary>
    ///     Whether this item has completed processing.
    /// </summary>
    public bool IsCompleted => Result is SyncItemResult.Success or SyncItemResult.Failed or SyncItemResult.Skipped or SyncItemResult.Cancelled;

    /// <summary>
    ///     Whether this item has an error.
    /// </summary>
    public bool HasError => Result == SyncItemResult.Failed || !string.IsNullOrEmpty(ErrorMessage);

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "-";

        string[] suffixes = { "B", "KB", "MB", "GB" };
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {suffixes[order]}";
    }
}
