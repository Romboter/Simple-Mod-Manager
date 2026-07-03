#nullable enable
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using Cursors = System.Windows.Input.Cursors;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async void CheckModsCompatibilityMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = _viewModel;
        if (viewModel is null) return;

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            WpfMessageBox.Show(
                "Enable Internet Access in the File menu to check mod compatibility.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        IReadOnlyList<string> recentVersions;
        try
        {
            recentVersions = await VintageStoryGameVersionService
                .GetRecentReleaseVersionsAsync(10)
                .ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            WpfMessageBox.Show(
                $"Failed to retrieve Vintage Story versions:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to retrieve Vintage Story versions:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        if (recentVersions is not { Count: > 0 })
        {
            WpfMessageBox.Show(
                "Could not determine recent Vintage Story versions.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var versionSelectionDialog = new VintageStoryVersionSelectionDialog(
            this,
            recentVersions,
            viewModel.InstalledGameVersion);
        var selectionResult = versionSelectionDialog.ShowDialog();
        if (selectionResult != true) return;

        var targetVersion = versionSelectionDialog.SelectedVersion;
        if (string.IsNullOrWhiteSpace(targetVersion)) return;

        targetVersion = targetVersion.Trim();

        var mods = viewModel.GetInstalledModsSnapshot();
        if (mods.Count == 0)
        {
            WpfMessageBox.Show(
                $"Vintage Story version: {targetVersion}.\n\nNo installed mods were found.",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var incompatible = new List<string>();
        var unknown = new List<string>();

        foreach (var mod in mods)
        {
            if (mod is null) continue;

            var displayName = string.IsNullOrWhiteSpace(mod.DisplayName)
                ? mod.ModId ?? "Unknown mod"
                : mod.DisplayName!;

            var evaluation = ModCompatibilityEvaluator.EvaluateCompatibility(mod, targetVersion, displayName,
                _userConfiguration.RequireExactVsVersionMatch);
            if (evaluation.IsCompatible) continue;

            if (evaluation.IsUnknown)
                unknown.Add(displayName);
            else
                incompatible.Add(displayName);
        }

        incompatible.Sort(StringComparer.CurrentCultureIgnoreCase);
        unknown.Sort(StringComparer.CurrentCultureIgnoreCase);

        var resultsDialog = new CompatibilityResultsDialog(this, targetVersion, incompatible, unknown);
        _ = resultsDialog.ShowDialog();
    }

    private async void ExperimentalCompReviewMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedMod is not ModListItemViewModel selectedMod)
        {
            WpfMessageBox.Show(
                "Select a mod first!",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var modSlug = ModCompatibilityReviewHelper.ResolveExperimentalCompReviewIdentifier(selectedMod);
        var latestVersion = string.IsNullOrWhiteSpace(_viewModel?.InstalledGameVersion)
            ? null
            : _viewModel!.InstalledGameVersion;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            var result = await _modCompatibilityCommentsService
                .GetTop3CommentsAsync(modSlug, latestVersion)
                .ConfigureAwait(true);

            var messageText = ModCompatibilityReviewHelper.BuildExperimentalCompReviewMessage(result);
            if (string.IsNullOrWhiteSpace(messageText))
                messageText = result.Reason ?? "No relevant comments were found.";

            var title = string.Format(
                CultureInfo.CurrentCulture,
                "Compatibility comments for {0}",
                selectedMod.DisplayName);

            WpfMessageBox.Show(
                messageText,
                title,
                MessageBoxButton.OK,
                result.Top3.Count > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (InternetAccessDisabledException ex)
        {
            WpfMessageBox.Show(
                ex.Message,
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"The experimental compatibility review failed:\n{ex.Message}",
                "Simple VS Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

}
