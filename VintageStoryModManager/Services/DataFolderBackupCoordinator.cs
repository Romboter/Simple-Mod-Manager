#nullable enable

using System.IO;
using VintageStoryModManager.Helpers;

namespace VintageStoryModManager.Services;

public enum DataBackupRestoreValidation
{
    Ok,
    DataDirectoryUnavailable,
    DifferentDataFolder,
    VersionMismatch
}

public readonly record struct DataBackupRestoreCheck(
    DataBackupRestoreValidation Result,
    string? BackupVersionDisplay,
    string? InstalledVersionDisplay);

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

    public DataBackupRestoreCheck ValidateRestore(DataBackupSummary summary, string? dataDirectory, string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory) || !Directory.Exists(dataDirectory))
            return new DataBackupRestoreCheck(DataBackupRestoreValidation.DataDirectoryUnavailable, null, null);

        if (!PathRelationshipHelper.IsSameDirectory(summary.SourceDataDirectory, dataDirectory))
            return new DataBackupRestoreCheck(DataBackupRestoreValidation.DifferentDataFolder, null, null);

        var (installedVersion, normalizedInstalledVersion) = VintageStoryVersionLocator.GetNormalizedInstalledVersion(gameDirectory);
        var normalizedBackupVersion = VersionStringUtility.Normalize(summary.VintageStoryVersion);

        if (!string.IsNullOrWhiteSpace(normalizedBackupVersion)
            && !string.IsNullOrWhiteSpace(normalizedInstalledVersion)
            && !string.Equals(normalizedBackupVersion, normalizedInstalledVersion, StringComparison.OrdinalIgnoreCase))
        {
            var backupVersionDisplay = summary.VintageStoryVersion ?? normalizedBackupVersion;
            var installedVersionDisplay = installedVersion ?? normalizedInstalledVersion;
            return new DataBackupRestoreCheck(DataBackupRestoreValidation.VersionMismatch, backupVersionDisplay, installedVersionDisplay);
        }

        return new DataBackupRestoreCheck(DataBackupRestoreValidation.Ok, null, null);
    }

    public (string? DisplayVersion, string? NormalizedVersion) ResolveInstalledVersionForDelete(string? gameDirectory)
    {
        var (installedVersion, normalizedInstalledVersion) = VintageStoryVersionLocator.GetNormalizedInstalledVersion(gameDirectory);

        if (string.IsNullOrWhiteSpace(normalizedInstalledVersion))
            return (null, null);

        return (installedVersion ?? normalizedInstalledVersion, normalizedInstalledVersion);
    }
}
