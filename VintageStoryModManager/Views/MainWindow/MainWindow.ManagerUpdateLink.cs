#nullable enable

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private async Task RefreshManagerUpdateLinkAsync()
        {
            if (ManagerUpdateLinkTextBlock is null) return;

            if (InternetAccessManager.IsInternetAccessDisabled)
            {
                ManagerUpdateLinkTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            var currentVersion = GetManagerInformationalVersion();
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                ManagerUpdateLinkTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                // Use relaxed mode for manager updates to allow more flexible compatibility
                var info = await _modDatabaseService
                    .TryLoadDatabaseInfoAsync(ManagerModDatabaseModId, currentVersion, null)
                    .ConfigureAwait(true);

                var hasUpdate = info?.LatestVersion is string latestVersion
                                && VersionStringUtility.IsCandidateVersionNewer(latestVersion, currentVersion);

                ManagerUpdateLinkTextBlock.Visibility = hasUpdate ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                ManagerUpdateLinkTextBlock.Visibility = Visibility.Collapsed;
            }
        }

    private static string? GetManagerInformationalVersion()
        {
            try
            {
                var assembly = typeof(MainWindow).Assembly;
                if (assembly is null) return null;

                var informationalVersion = assembly
                                               .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                               .InformationalVersion
                                           ?? assembly.GetName().Version?.ToString();

                if (string.IsNullOrWhiteSpace(informationalVersion)) return null;

                var buildMetadataSeparatorIndex = informationalVersion.IndexOf('+');
                if (buildMetadataSeparatorIndex >= 0)
                    informationalVersion = informationalVersion[..buildMetadataSeparatorIndex];

                return informationalVersion.Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }
}
