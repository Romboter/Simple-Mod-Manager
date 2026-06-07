#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            WpfMessageBox.Show("This mod does not declare dependencies that can be fixed automatically.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

                var isMissing = mod.MissingDependencies.Any(d =>
                    string.Equals(d.ModId, dependency.ModId, StringComparison.OrdinalIgnoreCase));
                if (!isMissing && installedDependency is null) isMissing = true;

                if (!isMissing && installedDependency != null)
                {
                    var satisfies =
                        VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, installedDependency.Version);
                    if (!satisfies) isMissing = true;
                }

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
                WpfMessageBox.Show(
                    $"The mods with errors could not be refreshed after fixing dependencies:{Environment.NewLine}{ex.Message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            UpdateSelectedModButtons();
        }

        if (failures.Count > 0)
        {
            var message = string.Join(Environment.NewLine, failures);
            WpfMessageBox.Show($"Some dependencies could not be resolved:{Environment.NewLine}{message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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

            var release = SelectReleaseForDependency(dependency, info);
            if (release is null) return (false, "No compatible releases were found.");

            string targetPath;
            bool targetIsDirectory;
            string? existingPath = null;

            if (installedMod != null)
            {
                if (!TryGetManagedModPath(installedMod, out targetPath, out var pathError))
                    return (false, pathError ?? "The mod path could not be determined.");

                targetIsDirectory = Directory.Exists(targetPath);
                if (!targetIsDirectory && !File.Exists(targetPath) && installedMod.SourceKind == ModSourceKind.Folder)
                    targetIsDirectory = true;

                if (!targetIsDirectory)
                {
                    if (!TryGetUpdateTargetPath(installedMod, release, targetPath, out var resolvedPath,
                            out var targetError))
                        return (false, targetError ?? "The mod path could not be determined.");

                    existingPath = targetPath;
                    targetPath = resolvedPath;
                }
            }
            else
            {
                if (!TryGetDependencyInstallTargetPath(dependency.ModId, release, out targetPath, out var errorMessage))
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

            var updateResult = await _modUpdateService
                .UpdateAsync(descriptor, _userConfiguration.CacheAllVersionsLocally, progress)
                .ConfigureAwait(true);

            if (!updateResult.Success)
            {
                var message = string.IsNullOrWhiteSpace(updateResult.ErrorMessage)
                    ? "The installation failed."
                    : updateResult.ErrorMessage!;
                return (false, message);
            }

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

    private static ModReleaseInfo? SelectReleaseForDependency(ModDependencyInfo dependency, ModDatabaseInfo info)
    {
        if (info is null) return null;

        var releases = info.Releases ?? Array.Empty<ModReleaseInfo>();
        if (releases.Count == 0) return null;

        foreach (var release in releases)
            if (release.IsCompatibleWithInstalledGame
                && VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, release.Version))
                return release;

        foreach (var release in releases)
            if (VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, release.Version))
                return release;

        var fallback = releases.FirstOrDefault(r => r.IsCompatibleWithInstalledGame)
                       ?? releases[0];

        var availableVersion = string.IsNullOrWhiteSpace(fallback.Version)
            ? "the latest available release"
            : $"version {fallback.Version}";

        var requirement = string.IsNullOrWhiteSpace(dependency.Version)
            ? dependency.ModId
            : $"{dependency.ModId} {dependency.Version} or newer";

        var message =
            $"No release that satisfies the required minimum version for {dependency.Display} could be found.{Environment.NewLine}{Environment.NewLine}" +
            $"The mod database only provides {availableVersion}, which may not resolve the dependency requirement for {requirement}.{Environment.NewLine}{Environment.NewLine}" +
            "Do you want to install this older release anyway?";

        var confirmation = WpfMessageBox.Show(
            message,
            "Simple VS Manager",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return confirmation == MessageBoxResult.Yes ? fallback : null;
    }
}
