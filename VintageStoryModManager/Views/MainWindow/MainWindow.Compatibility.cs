#nullable enable
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using Cursors = System.Windows.Input.Cursors;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private async void CheckModsCompatibilityMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var viewModel = _viewModel;
        if (viewModel is null) return;

        if (InternetAccessManager.IsInternetAccessDisabled)
        {
            await _confirmationService.NotifyAsync(
                    "Enable Internet Access in the File menu to check mod compatibility.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    CompatibilityDialogTextBuilder.BuildFailedToRetrieveVersionsMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    CompatibilityDialogTextBuilder.BuildFailedToRetrieveVersionsMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
            return;
        }

        if (recentVersions is not { Count: > 0 })
        {
            await _confirmationService.NotifyAsync(
                    "Could not determine recent Vintage Story versions.",
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    CompatibilityDialogTextBuilder.BuildNoInstalledModsMessage(targetVersion),
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
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
            await _confirmationService.NotifyAsync(
                    "Select a mod first!",
                    "Simple VS Manager",
                    DialogSeverity.Information)
                .ConfigureAwait(true);
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

            var title = CompatibilityDialogTextBuilder.BuildCompatibilityCommentsTitle(selectedMod.DisplayName);

            await _confirmationService.NotifyAsync(
                    messageText,
                    title,
                    result.Top3.Count > 0 ? DialogSeverity.Information : DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
        catch (InternetAccessDisabledException ex)
        {
            await _confirmationService.NotifyAsync(
                    ex.Message,
                    "Simple VS Manager",
                    DialogSeverity.Warning)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await _confirmationService.NotifyAsync(
                    CompatibilityDialogTextBuilder.BuildExperimentalCompReviewFailedMessage(ex.Message),
                    "Simple VS Manager",
                    DialogSeverity.Error)
                .ConfigureAwait(true);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

}
