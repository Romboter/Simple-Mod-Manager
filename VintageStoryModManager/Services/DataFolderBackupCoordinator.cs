#nullable enable

namespace VintageStoryModManager.Services;

internal sealed class DataFolderBackupCoordinator
{
    private DataBackupService _dataBackupService;

    public DataFolderBackupCoordinator(DataBackupService initialService)
    {
        _dataBackupService = initialService;
    }

    public string GetBackupRootDirectory()
    {
        return _dataBackupService.GetBackupRootDirectory();
    }

    public IReadOnlyList<DataBackupSummary> GetAvailableBackups()
    {
        return _dataBackupService.GetAvailableBackups();
    }

    public int DeleteBackups(string dataDirectory, string vintageStoryVersion)
    {
        return _dataBackupService.DeleteBackups(dataDirectory, vintageStoryVersion);
    }

    public Task<DataBackupResult> CreateBackupAsync(
        string dataDirectory,
        string? vintageStoryVersion,
        IProgress<DataBackupProgress>? progress,
        CancellationToken cancellationToken)
    {
        return _dataBackupService.CreateBackupAsync(dataDirectory, vintageStoryVersion, progress, cancellationToken);
    }

    public Task RestoreBackupAsync(
        DataBackupSummary summary,
        string dataDirectory,
        IProgress<DataBackupProgress>? progress,
        CancellationToken cancellationToken)
    {
        return _dataBackupService.RestoreBackupAsync(summary, dataDirectory, progress, cancellationToken);
    }

    public void ChangeLocation(string configurationDirectory, string? customLocation)
    {
        _dataBackupService = new DataBackupService(configurationDirectory, customLocation);
    }
}
