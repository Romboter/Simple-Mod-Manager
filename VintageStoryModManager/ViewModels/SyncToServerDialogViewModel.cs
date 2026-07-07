using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Sync dialog steps/phases.
/// </summary>
public enum SyncDialogStep
{
    Options,
    Preview,
    Executing,
    Complete
}

/// <summary>
///     ViewModel for the Sync to Server dialog.
/// </summary>
public sealed partial class SyncToServerDialogViewModel : ObservableObject
{
    private readonly ServerTarget _target;
    private readonly string _localDataPath;
    private readonly ServerTargetService _targetService;
    private readonly Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, ISftpClientWrapper> _sftpFactory;
    private readonly SyncEngine _syncEngine;
    private readonly Func<string, HostKeyVerificationResult, Task<bool>> _hostKeyVerifier;
    private readonly Dispatcher _dispatcher;
    private readonly IConfirmationService _confirmationService;

    private ISftpClientWrapper? _sftp;
    private SyncPreview? _currentPreview;
    private CancellationTokenSource? _operationCts;

    [ObservableProperty]
    private SyncDialogStep _currentStep = SyncDialogStep.Options;

    [ObservableProperty]
    private bool _includeModConfig;


    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isGeneratingPreview;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private double _currentFileProgress;

    [ObservableProperty]
    private int _completedFiles;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private bool _hasErrors;

    [ObservableProperty]
    private bool _wasCancelled;

    // ...existing code...

    /// <summary>
    ///     Server target display name.
    /// </summary>
    public string TargetName => _target.Name;

    /// <summary>
    ///     Server host for display.
    /// </summary>
    public string TargetHost => $"{_target.Host}:{_target.Port}";

    /// <summary>
    ///     Remote data path for display.
    /// </summary>
    public string RemoteDataPath => _target.RemoteDataPath;

    /// <summary>
    ///     Whether the remote path appears dangerous.
    /// </summary>
    public bool IsDangerousPath => SyncEngine.IsDangerousPath(_target.RemoteDataPath);

    /// <summary>
    ///     All preview items.
    /// </summary>
    public ObservableCollection<SyncPreviewItemViewModel> PreviewItems { get; } = new();

    public SyncToServerDialogViewModel(
        ServerTarget target,
        string localDataPath,
        ServerTargetService targetService,
        SyncEngine syncEngine,
        Func<ServerTarget, string?, Func<string, HostKeyVerificationResult, Task<bool>>, ISftpClientWrapper> sftpFactory,
        Func<string, HostKeyVerificationResult, Task<bool>> hostKeyVerifier,
        IConfirmationService confirmationService,
        bool defaultIncludeModConfig = false)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _localDataPath = localDataPath ?? throw new ArgumentNullException(nameof(localDataPath));
        _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
        _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
        _sftpFactory = sftpFactory ?? throw new ArgumentNullException(nameof(sftpFactory));
        _hostKeyVerifier = hostKeyVerifier ?? throw new ArgumentNullException(nameof(hostKeyVerifier));
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));
        _includeModConfig = defaultIncludeModConfig;

        PreviewItems.CollectionChanged += PreviewItems_CollectionChanged;
    }

    private void PreviewItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<SyncPreviewItemViewModel>())
            {
                item.PropertyChanged += PreviewItem_PropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<SyncPreviewItemViewModel>())
            {
                item.PropertyChanged -= PreviewItem_PropertyChanged;
            }
        }
    }

    private void PreviewItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SyncPreviewItemViewModel.IsConfirmedForDeletion))
        {
            OnPropertyChanged(nameof(CanExecuteSync));
            ExecuteSyncCommand?.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    ///     Items to be uploaded.
    /// </summary>
    public IEnumerable<SyncPreviewItemViewModel> UploadItems => PreviewItems.Where(p => p.Action == SyncAction.Upload);

    /// <summary>
    ///     Items to be skipped.
    /// </summary>
    public IEnumerable<SyncPreviewItemViewModel> SkipItems => PreviewItems.Where(p => p.Action == SyncAction.Skip);

    /// <summary>
    ///     Orphan items (potential deletions).
    /// </summary>
    public IEnumerable<SyncPreviewItemViewModel> OrphanItems => PreviewItems.Where(p => p.IsOrphan);

    /// <summary>
    ///     Directories to create.
    /// </summary>
    public IEnumerable<SyncPreviewItemViewModel> DirectoryItems => PreviewItems.Where(p => p.Action == SyncAction.CreateDirectory);

    /// <summary>
    ///     Whether there are orphan items.
    /// </summary>
    public bool HasOrphans => PreviewItems.Any(p => p.IsOrphan);

    /// <summary>
    ///     Upload count for display.
    /// </summary>
    public int UploadCount => UploadItems.Count();

    /// <summary>
    ///     Skip count for display.
    /// </summary>
    public int SkipCount => SkipItems.Count();

    /// <summary>
    ///     Orphan count for display.
    /// </summary>
    public int OrphanCount => OrphanItems.Count();

    /// <summary>
    ///     Whether we're busy with any operation.
    /// </summary>
    public bool IsBusy => IsConnecting || IsGeneratingPreview || IsExecuting;

    /// <summary>
    ///     Whether we can start the preview generation.
    /// </summary>
    public bool CanGeneratePreview => !IsBusy && CurrentStep == SyncDialogStep.Options;

    /// <summary>
    ///     Whether we can execute the sync.
    /// </summary>
    public bool CanExecuteSync =>
        !IsBusy
        && CurrentStep == SyncDialogStep.Preview
        && (UploadCount > 0 || OrphanItems.Any(o => o.IsConfirmedForDeletion));

    /// <summary>
    ///     Whether we can cancel.
    /// </summary>
    public bool CanCancel => IsBusy;

    /// <summary>
    ///     Whether the current step is Options.
    /// </summary>
    public bool IsOptionsStep => CurrentStep == SyncDialogStep.Options;

    /// <summary>
    ///     Whether the current step is Preview.
    /// </summary>
    public bool IsPreviewStep => CurrentStep == SyncDialogStep.Preview;

    /// <summary>
    ///     Whether the current step is Executing.
    /// </summary>
    public bool IsExecutingStep => CurrentStep == SyncDialogStep.Executing;

    /// <summary>
    ///     Whether the current step is Complete.
    /// </summary>
    public bool IsCompleteStep => CurrentStep == SyncDialogStep.Complete;

    /// <summary>
    ///     Connects to server and generates sync preview.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGeneratePreview))]
    private async Task GeneratePreviewAsync()
    {
        _operationCts = new CancellationTokenSource();
        var ct = _operationCts.Token;

        // Create progress reporter on UI thread before any background work
        var progress = new Progress<string>(msg =>
        {
            try
            {
                StatusText = msg;
            }
            catch
            {
                // Ignore progress update errors
            }
        });

        try
        {
            IsConnecting = true;
            StatusText = "Connecting to server...";
            UpdateCommandStates();

            // Get password if needed
            var password = _targetService.GetDecryptedPassword(_target.Id);

            // Create SFTP client
            _sftp = _sftpFactory(_target, password, _hostKeyVerifier);

            await _sftp.ConnectAsync(_target, password, _hostKeyVerifier, ct).ConfigureAwait(false);

            // Update last connected
            _targetService.UpdateLastConnected(_target.Id);

            await _dispatcher.InvokeAsync(() =>
            {
                IsConnecting = false;
                IsGeneratingPreview = true;
                StatusText = "Analyzing files...";
            });

            _currentPreview = await _syncEngine.GeneratePreviewAsync(
                _localDataPath,
                _target.RemoteDataPath,
                IncludeModConfig,
                _sftp,
                progress,
                ct).ConfigureAwait(false);

            // Marshal UI updates back to the dispatcher
            await _dispatcher.InvokeAsync(() =>
            {
                // Populate preview items
                PreviewItems.Clear();
                foreach (var item in _currentPreview.Items.OrderBy(i => i.Action).ThenBy(i => i.RelativePath))
                {
                    PreviewItems.Add(new SyncPreviewItemViewModel(item));
                }

                CurrentStep = SyncDialogStep.Preview;
                StatusText = $"Preview ready: {UploadCount} to upload, {SkipCount} unchanged, {OrphanCount} orphans";

                OnPropertyChanged(nameof(HasOrphans));
                OnPropertyChanged(nameof(UploadCount));
                OnPropertyChanged(nameof(SkipCount));
                OnPropertyChanged(nameof(OrphanCount));
            });
        }
        catch (OperationCanceledException)
        {
            await SetStatusSafeAsync("Operation cancelled.");
        }
        catch (Exception ex)
        {
            try
            {
                _sftp?.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
            _sftp = null;
            await SetStatusSafeAsync($"Error: {ex.Message}");
        }
        finally
        {
            await _dispatcher.InvokeAsync(() =>
            {
                try
                {
                    IsConnecting = false;
                    IsGeneratingPreview = false;
                    UpdateCommandStates();
                }
                catch
                {
                    // Ignore final cleanup errors
                }
            });
        }
    }

    private async Task SetStatusSafeAsync(string status)
    {
        try
        {
            await _dispatcher.InvokeAsync(() => StatusText = status);
        }
        catch
        {
            // Ignore status update errors
        }
    }

    /// <summary>
    ///     Executes the sync operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteSync))]
    private async Task ExecuteSyncAsync()
    {
        if (_currentPreview is null || _sftp is null)
            return;

        _operationCts = new CancellationTokenSource();
        var ct = _operationCts.Token;

        // Get confirmed deletions
        var confirmedDeletions = OrphanItems.Where(o => o.IsConfirmedForDeletion).Select(o => o.Item).ToList();

        // If there are deletions, confirm with the user
        if (confirmedDeletions.Count > 0)
        {
            var confirmed = await _confirmationService.ConfirmAsync(
                "You are about to delete files or folders from the server. This action cannot be undone.\n\nDo you want to proceed?",
                "Confirm Deletions", DialogSeverity.Warning, confirmText: "Delete", cancelText: "Cancel");
            if (!confirmed)
            {
                StatusText = "Sync cancelled by user (deletion not confirmed).";
                return;
            }
        }

        try
        {
            IsExecuting = true;
            CurrentStep = SyncDialogStep.Executing;
            StatusText = "Syncing...";
            UpdateCommandStates();

            var progress = new Progress<SyncProgress>(p =>
            {
                CurrentFileName = p.CurrentFile;
                OverallProgress = p.OverallPercent;
                CurrentFileProgress = p.CurrentFilePercent;
                CompletedFiles = p.CompletedFiles;
                TotalFiles = p.TotalFiles;
                StatusText = $"{p.Phase}: {p.CurrentFile}";

                // Update item status
                var currentItem = PreviewItems.FirstOrDefault(i => i.RelativePath == p.CurrentFile);
                if (currentItem != null)
                {
                    currentItem.Result = SyncItemResult.InProgress;
                }
            });

            var result = await _syncEngine.ExecuteSyncAsync(
                _currentPreview,
                confirmedDeletions,
                _sftp,
                progress,
                ct).ConfigureAwait(false);

            // Marshal UI updates back to the dispatcher
            await _dispatcher.InvokeAsync(() =>
            {
                // Update item results
                foreach (var outcome in result.Outcomes)
                {
                    var item = PreviewItems.FirstOrDefault(i => i.Item == outcome.Item);
                    if (item != null)
                    {
                        item.Result = outcome.Result;
                        item.ErrorMessage = outcome.ErrorMessage;
                    }
                }

                CurrentStep = SyncDialogStep.Complete;
                HasErrors = result.HasErrors;
                WasCancelled = result.WasCancelled;

                ResultSummary = WasCancelled
                    ? "Sync was cancelled."
                    : HasErrors
                        ? $"Completed with errors. {result.SuccessCount} succeeded, {result.FailedCount} failed."
                        : $"Sync complete! {result.SuccessCount} files synced successfully.";

                StatusText = ResultSummary;
            });
        }
        catch (OperationCanceledException)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                CurrentStep = SyncDialogStep.Complete;
                WasCancelled = true;
                ResultSummary = "Sync was cancelled.";
                StatusText = ResultSummary;
            });
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                CurrentStep = SyncDialogStep.Complete;
                HasErrors = true;
                ResultSummary = $"Sync failed: {ex.Message}";
                StatusText = ResultSummary;
            });
        }
        finally
        {
            _dispatcher.Invoke(() =>
            {
                IsExecuting = false;
                UpdateCommandStates();
            });
        }
    }

    /// <summary>
    ///     Cancels the current operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _operationCts?.Cancel();
        _syncEngine.CancelCurrentSync();
        StatusText = "Cancelling...";
    }

    /// <summary>
    ///     Selects all orphans for deletion.
    /// </summary>
    [RelayCommand]
    private void SelectAllOrphans()
    {
        foreach (var item in OrphanItems)
        {
            item.IsConfirmedForDeletion = true;
        }
    }

    /// <summary>
    ///     Deselects all orphans.
    /// </summary>
    [RelayCommand]
    private void DeselectAllOrphans()
    {
        foreach (var item in OrphanItems)
        {
            item.IsConfirmedForDeletion = false;
        }
    }

    /// <summary>
    ///     Goes back to options step.
    /// </summary>
    [RelayCommand]
    private void BackToOptions()
    {
        if (CurrentStep == SyncDialogStep.Preview)
        {
            PreviewItems.Clear();
            _currentPreview = null;
            CurrentStep = SyncDialogStep.Options;
            UpdateCommandStates();
        }
    }

    /// <summary>
    ///     Cleans up resources.
    /// </summary>
    public void Cleanup()
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _sftp?.Dispose();
    }

    private void UpdateCommandStates()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanGeneratePreview));
        OnPropertyChanged(nameof(CanExecuteSync));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(IsOptionsStep));
        OnPropertyChanged(nameof(IsPreviewStep));
        OnPropertyChanged(nameof(IsExecutingStep));
        OnPropertyChanged(nameof(IsCompleteStep));

        GeneratePreviewCommand.NotifyCanExecuteChanged();
        ExecuteSyncCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
