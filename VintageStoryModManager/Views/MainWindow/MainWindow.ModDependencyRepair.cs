#nullable enable

using System.IO;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async void FixModButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isModUpdateInProgress) return;

        if (sender is not WpfButton { DataContext: ModListItemViewModel mod }) return;

        e.Handled = true;

        var dependencies = mod.Dependencies;
        if (dependencies.Count == 0)
        {
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.NoAutoFixableDependenciesMessage,
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
            return;
        }

        var errorSourcePathsBeforeFix =
            _viewModel?.GetSourcePathsForModsWithErrors() ?? [];
        var modsToRefresh = new HashSet<string>(errorSourcePathsBeforeFix, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(mod.SourcePath)) modsToRefresh.Add(mod.SourcePath);

        _isModUpdateInProgress = true;
        UpdateSelectedModButtons();

        var failures = new List<string>();
        var anySuccess = false;
        var processedDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var dependency in dependencies)
            {
                if (dependency.IsGameOrCoreDependency || !processedDependencies.Add(dependency.ModId)) continue;

                var installedDependency = _viewModel?.FindInstalledModById(dependency.ModId);

                var listedAsMissing = mod.MissingDependencies.Any(d =>
                    string.Equals(d.ModId, dependency.ModId, StringComparison.OrdinalIgnoreCase));
                var isMissing = DependencyRepairHelper.IsDependencyMissing(listedAsMissing, installedDependency, dependency.Version);

                if (isMissing)
                {
                    var result = await InstallOrUpdateDependencyAsync(dependency, installedDependency)
                        .ConfigureAwait(true);
                    if (!result.Success)
                    {
                        failures.Add($"{dependency.Display}: {result.Message}");
                        _viewModel?.ReportStatus($"Failed to install dependency {dependency.Display}: {result.Message}",
                            true);
                    }
                    else
                    {
                        anySuccess = true;
                        _viewModel?.ReportStatus(result.Message);
                    }

                    continue;
                }

                if (installedDependency != null && !installedDependency.IsActive)
                {
                    installedDependency.IsActive = true;
                    anySuccess = true;
                    _viewModel?.ReportStatus($"Activated dependency {installedDependency.DisplayName}.");
                }
            }
        }
        finally
        {
            _isModUpdateInProgress = false;
            UpdateSelectedModButtons();
        }

        if (_viewModel is { } viewModel && modsToRefresh.Count > 0)
        {
            try
            {
                await viewModel.RefreshModsWithErrorsAsync(modsToRefresh).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await _confirmationService.NotifyAsync(
                        ModOperationDialogTextBuilder.BuildRefreshAfterDependencyRepairFailureMessage(
                            ex.Message),
                        "Simple VS Manager",
                        DialogSeverity.Error)
                    .ConfigureAwait(true);
            }

            UpdateSelectedModButtons();
        }

        if (failures.Count > 0)
        {
            await _confirmationService.NotifyAsync(
                    ModOperationDialogTextBuilder.BuildDependencyFailuresMessage(failures),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            _viewModel?.ReportStatus($"Failed to resolve all dependencies for {mod.DisplayName}.", true);
        }
        else if (anySuccess)
        {
            _viewModel?.ReportStatus($"Resolved dependencies for {mod.DisplayName}.");
        }
        else
        {
            _viewModel?.ReportStatus($"Dependencies for {mod.DisplayName} are already satisfied.");
        }
    }

    private async Task<(bool Success, string Message)> InstallOrUpdateDependencyAsync(
        ModDependencyInfo dependency,
        ModListItemViewModel? installedMod)
    {
        try
        {
            var info = await _modDatabaseService
                .TryLoadDatabaseInfoAsync(dependency.ModId, installedMod?.Version, _viewModel?.InstalledGameVersion,
                    _userConfiguration.RequireExactVsVersionMatch)
                .ConfigureAwait(true);

            if (info is null) return (false, "Mod not found on the mod database.");

            var release = ModReleaseSelectionHelper.SelectReleaseForDependency(dependency, info);
            if (release is null) return (false, "No compatible releases were found.");

            string targetPath;
            bool targetIsDirectory;
            string? existingPath = null;

            if (installedMod != null)
            {
                if (!TryGetManagedModPath(installedMod, out var sourcePath, out var pathError))
                    return (false, pathError ?? "The mod path could not be determined.");

                if (!ModUpdateTargetPathHelper.TryResolveUpdateTarget(installedMod, release, sourcePath,
                        out targetPath, out targetIsDirectory, out existingPath, out var targetError))
                    return (false, targetError ?? "The mod path could not be determined.");
            }
            else
            {
                if (!ModInstallTargetPathHelper.TryGetDependencyInstallTargetPath(_dataDirectory, dependency.ModId,
                        release, out targetPath, out var errorMessage))
                    return (false, errorMessage ?? "The Mods folder is not available.");

                targetIsDirectory = false;
            }

            var wasActive = installedMod?.IsActive == true;

            var descriptor = new ModUpdateDescriptor(
                dependency.ModId,
                dependency.Display,
                release.DownloadUri,
                targetPath,
                targetIsDirectory,
                release.FileName,
                release.Version,
                installedMod?.Version)
            {
                ExistingPath = existingPath
            };

            var progress = new Progress<ModUpdateProgress>(p =>
                _viewModel?.ReportStatus($"{dependency.ModId}: {p.Message}"));

            var outcome = await ModUpdateOperationHelper.ExecuteAsync(
                    _modUpdateService, descriptor, _userConfiguration.CacheAllVersionsLocally, progress,
                    "The installation failed.")
                .ConfigureAwait(true);

            if (!outcome.Success) return (false, outcome.ErrorMessage!);

            if (installedMod != null && _viewModel != null)
                await _viewModel.PreserveActivationStateAsync(
                    dependency.ModId,
                    installedMod.Version,
                    release.Version,
                    wasActive).ConfigureAwait(true);

            var action = installedMod != null ? "Updated" : "Installed";
            var versionSuffix = string.IsNullOrWhiteSpace(release.Version) ? string.Empty : $" {release.Version}";
            return (true, $"{action} dependency {dependency.Display}{versionSuffix}.");
        }
        catch (OperationCanceledException)
        {
            return (false, "The operation was cancelled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return (false, ex.Message);
        }
    }
}
