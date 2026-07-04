#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SimpleVsManager.Cloud;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;

using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void AddModToInstalledAndRemoveFromSearch(int modId)
    {
        if (_modBrowserViewModel == null) return;

        _modBrowserViewModel.AddInstalledMod(modId.ToString(CultureInfo.InvariantCulture), modId);

        // Remove the installed mod from the current search results
        var modToRemove = _modBrowserViewModel.ModsList.FirstOrDefault(m => m.ModId == modId);
        if (modToRemove != null)
        {
            _modBrowserViewModel.ModsList.Remove(modToRemove);

            // Clear selection if the removed mod was selected
            if (ReferenceEquals(_modBrowserViewModel.SelectedMod, modToRemove))
            {
                _modBrowserViewModel.SelectedMod = null;
            }
        }
    }

    private async Task InstallModFromBrowserAsync(DownloadableMod mod)
    {
        if (_isModUpdateInProgress)
            return;

        // Convert and validate the mod for installation
        var modViewModel = ConvertToModListItemViewModel(mod);

        if (!modViewModel.HasDownloadableRelease)
        {
            WpfMessageBox.Show("No downloadable releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var release = ModReleaseSelectionHelper.SelectReleaseForInstall(modViewModel);
        if (release is null)
        {
            WpfMessageBox.Show("No downloadable releases are available for this mod.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!TryGetInstallTargetPath(modViewModel, release, out var targetPath, out var errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
                WpfMessageBox.Show(errorMessage!,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

            return;
        }

        await CreateAutomaticBackupAsync("ModsUpdated").ConfigureAwait(true);

        _isModUpdateInProgress = true;
        UpdateSelectedModButtons();

        try
        {
            var descriptor = new ModUpdateDescriptor(
                modViewModel.ModId,
                modViewModel.DisplayName,
                release.DownloadUri,
                targetPath,
                false,
                release.FileName,
                release.Version,
                modViewModel.Version);

            var progress = new Progress<ModUpdateProgress>(p =>
                _viewModel?.ReportStatus($"{modViewModel.DisplayName}: {p.Message}"));

            var result = await _modUpdateService
                .UpdateAsync(descriptor, _userConfiguration.CacheAllVersionsLocally, progress)
                .ConfigureAwait(true);

            if (!result.Success)
            {
                var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "The installation failed."
                    : result.ErrorMessage!;
                _viewModel?.ReportStatus($"Failed to install {modViewModel.DisplayName}: {message}", true);
                WpfMessageBox.Show($"Failed to install {modViewModel.DisplayName}:{Environment.NewLine}{message}",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // Step 11: Use the SAME success reporting
            var versionText = string.IsNullOrWhiteSpace(release.Version) ? string.Empty : $" {release.Version}";
            _viewModel?.ReportStatus($"Installed {modViewModel.DisplayName}{versionText}.");
            _modActivityLoggingService.LogModInstall(modViewModel.DisplayName ?? modViewModel.ModId ?? "Unknown", release.Version);

            // Step 12: Use the SAME refresh function
            await RefreshModsAsync().ConfigureAwait(true);

            // Step 13: ModBrowser-specific cleanup
            // Update the ModBrowserViewModel to mark as installed and remove from search
            AddModToInstalledAndRemoveFromSearch(mod.ModId);
        }
        catch (OperationCanceledException)
        {
            _viewModel?.ReportStatus("Installation cancelled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _modActivityLoggingService.LogError($"Failed to install {modViewModel.DisplayName}", ex);
            _viewModel?.ReportStatus($"Failed to install {modViewModel.DisplayName}: {ex.Message}", true);
            WpfMessageBox.Show($"Failed to install {modViewModel.DisplayName}:{Environment.NewLine}{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            // Step 14: Use the SAME cleanup
            _isModUpdateInProgress = false;
            UpdateSelectedModButtons();
        }
    }

    private ModListItemViewModel ConvertToModListItemViewModel(DownloadableMod mod)
    {
        // Only consider the most recent release with a valid downloadable file
        var latestRelease = mod.Releases?
            .OrderByDescending(r => DateTime.TryParse(r.Created, out var created) ? created : DateTime.MinValue)
            .Select(ConvertToModReleaseInfo)
            .FirstOrDefault(r => r != null);

        var releases = latestRelease != null
            ? new List<ModReleaseInfo> { latestRelease }
            : new List<ModReleaseInfo>();

        // Create ModDatabaseInfo with converted data
        var logoUrlSource = !string.IsNullOrWhiteSpace(mod.LogoFileDatabase)
            ? "logofiledb"
            : null;

        var databaseInfo = new ModDatabaseInfo
        {
            Tags = mod.Tags?.ToArray() ?? Array.Empty<string>(),
            AssetId = mod.AssetId.ToString(),
            ModPageUrl = $"https://mods.vintagestory.at/show/mod/{mod.AssetId}",
            LatestVersion = latestRelease?.Version,
            LatestCompatibleVersion = latestRelease?.Version,
            RequiredGameVersions = releases.SelectMany(r => r.GameVersionTags).Distinct().ToArray(),
            Downloads = mod.Downloads,
            Comments = mod.Comments,
            Follows = mod.Follows,
            TrendingPoints = mod.TrendingPoints,
            LogoUrl = mod.LogoFileDatabase,
            LogoUrlSource = logoUrlSource,
            LastReleasedUtc = latestRelease?.CreatedUtc,
            LatestRelease = latestRelease,
            LatestCompatibleRelease = latestRelease,
            Releases = releases,
            Side = mod.Side
        };

        // Create ModEntry with the mod data
        var entry = new ModEntry
        {
            ModId = mod.ModIdStr ?? mod.ModId.ToString(),
            Name = mod.Name,
            Version = null, // Not installed yet
            Authors = !string.IsNullOrWhiteSpace(mod.Author)
                ? new[] { mod.Author }
                : Array.Empty<string>(),
            Website = mod.HomepageUrl,
            SourcePath = string.Empty,
            SourceKind = ModSourceKind.ZipArchive,
            DatabaseInfo = databaseInfo
        };

        // Create ModListItemViewModel using the constructor
        // Note: We use a dummy activation handler since this is only for installation
        var viewModel = new ModListItemViewModel(
            entry,
            isActive: false,
            location: "Mod Database",
            activationHandler: (_, _) => Task.FromResult(new ActivationResult(false, null)),
            installedGameVersion: _viewModel?.InstalledGameVersion,
            isInstalled: false,
            shouldSkipVersion: null,
            requireExactVersionMatch: null);

        return viewModel;
    }

    private ModReleaseInfo? ConvertToModReleaseInfo(DownloadableModRelease release)
    {
        if (string.IsNullOrWhiteSpace(release.MainFile) || string.IsNullOrWhiteSpace(release.Filename))
            return null;

        // MainFile already contains the full download URL
        var downloadUri = new Uri(release.MainFile);

        // Parse the created date
        DateTime? createdUtc = null;
        if (DateTime.TryParse(release.Created, out var parsedDate))
            createdUtc = parsedDate.ToUniversalTime();

        return new ModReleaseInfo
        {
            Version = release.ModVersion,
            DownloadUri = downloadUri,
            FileName = release.Filename,
            GameVersionTags = release.Tags?.ToArray() ?? Array.Empty<string>(),
            IsCompatibleWithInstalledGame = true,
            Changelog = release.Changelog,
            Downloads = release.Downloads,
            CreatedUtc = createdUtc
        };
    }
}
