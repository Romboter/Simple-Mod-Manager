#nullable enable
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void StartGameSessionMonitor()
    {
        if (string.IsNullOrWhiteSpace(_dataDirectory) || _viewModel is null) return;

        if (!_userConfiguration.IsModUsageTrackingEnabled) return;

        StopGameSessionMonitor();

        var logsDirectory = Path.Combine(_dataDirectory, "Logs");

        try
        {
            _gameSessionMonitor = new GameSessionMonitor(
                logsDirectory,
                Dispatcher,
                _userConfiguration,
                () => _viewModel.GetActiveModUsageSnapshot());
            _gameSessionMonitor.PromptRequired += GameSessionMonitor_OnPromptRequired;
            _gameSessionMonitor.RefreshPromptState();

            if (_userConfiguration.HasPendingModUsagePrompt)
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Func<Task>(TryShowModUsagePromptAsync));
        }
        catch (Exception ex)
        {
            StatusLogService.AppendStatus(
                string.Format(CultureInfo.CurrentCulture, "Failed to initialize log monitor: {0}", ex.Message),
                true);
        }
    }

    private void StopGameSessionMonitor()
    {
        if (_gameSessionMonitor is null) return;

        _gameSessionMonitor.PromptRequired -= GameSessionMonitor_OnPromptRequired;
        _gameSessionMonitor.Dispose();
        _gameSessionMonitor = null;
    }

    private void GameSessionMonitor_OnPromptRequired(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Func<Task>(TryShowModUsagePromptAsync));
    }

    private Task TryShowModUsagePromptAsync()
    {
        if (_isModUsageDialogOpen) return Task.CompletedTask;

        if (_viewModel is null || !_userConfiguration.IsModUsageTrackingEnabled)
        {
            _modUsagePromptData = null;
            UpdateModUsagePromptIndicator(false);
            return Task.CompletedTask;
        }

        if (!_userConfiguration.HasPendingModUsagePrompt)
        {
            _modUsagePromptData = null;
            UpdateModUsagePromptIndicator(false);
            return Task.CompletedTask;
        }

        var data = PrepareModUsagePromptData();
        if (data is null || data.Candidates.Count == 0)
        {
            _modUsagePromptData = null;
            UpdateModUsagePromptIndicator(false);
            _gameSessionMonitor?.RefreshPromptState();
            return Task.CompletedTask;
        }

        var previousData = _modUsagePromptData;
        _modUsagePromptData = data;
        UpdateModUsagePromptIndicator(true);

        var shouldLogSkipped = data.SkippedCount > 0
                               && (previousData is null
                                   || previousData.SkippedCount != data.SkippedCount
                                   || previousData.Candidates.Count != data.Candidates.Count);

        if (shouldLogSkipped)
            StatusLogService.AppendStatus(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Skipped {0} mod(s) that cannot receive automatic votes.",
                    data.SkippedCount),
                false);

        return Task.CompletedTask;
    }

    private ModUsagePromptData? PrepareModUsagePromptData()
    {
        if (_viewModel is null || !_userConfiguration.IsModUsageTrackingEnabled) return null;

        var usageCounts = _userConfiguration.GetPendingModUsageCounts();
        if (usageCounts.Count == 0)
        {
            _userConfiguration.ResetModUsageTracking();
            return null;
        }

        var installedGameVersion = _viewModel.InstalledGameVersion;
        if (string.IsNullOrWhiteSpace(installedGameVersion))
        {
            _userConfiguration.ResetModUsageTracking();
            return null;
        }

        installedGameVersion = installedGameVersion.Trim();

        var candidates = new List<ModUsageVoteCandidateViewModel>();
        var candidateKeys = new List<ModUsageTrackingKey>();
        var keysToClear = new List<ModUsageTrackingKey>();
        var skippedCount = 0;

        foreach (var entry in usageCounts.OrderByDescending(pair => pair.Value))
        {
            var key = entry.Key;
            if (!key.IsValid)
            {
                keysToClear.Add(key);
                continue;
            }

            var mod = _viewModel.FindInstalledModById(key.ModId);
            if (mod is null)
            {
                keysToClear.Add(key);
                skippedCount++;
                continue;
            }

            if (!mod.CanSubmitUserReport)
            {
                keysToClear.Add(key);
                skippedCount++;
                continue;
            }

            var modVersion = mod.Version;
            if (string.IsNullOrWhiteSpace(modVersion)
                || !string.Equals(modVersion.Trim(), key.ModVersion, StringComparison.OrdinalIgnoreCase))
            {
                keysToClear.Add(key);
                skippedCount++;
                continue;
            }

            if (!string.Equals(installedGameVersion, key.GameVersion, StringComparison.OrdinalIgnoreCase))
            {
                keysToClear.Add(key);
                skippedCount++;
                continue;
            }

            if (mod.UserVoteOption.HasValue)
            {
                keysToClear.Add(key);
                continue;
            }

            candidates.Add(new ModUsageVoteCandidateViewModel(mod, entry.Value, key));
            candidateKeys.Add(key);
        }

        if (keysToClear.Count > 0) _userConfiguration.ResetModUsageCounts(keysToClear);

        if (candidates.Count == 0)
        {
            if (skippedCount > 0)
                StatusLogService.AppendStatus("No mods were eligible for automatic \"No issues\" votes.", false);

            if (_userConfiguration.GetPendingModUsageCounts().Count == 0) _userConfiguration.ResetModUsageTracking();

            return null;
        }

        return new ModUsagePromptData(candidates, candidateKeys, skippedCount);
    }

    private void UpdateModUsagePromptIndicator(bool isVisible)
    {
        if (ModUsagePromptTextBlock is null) return;

        ModUsagePromptTextBlock.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ShowModUsagePromptDialogAsync()
    {
        if (_viewModel is null || !_userConfiguration.IsModUsageTrackingEnabled) return;

        if (_isModUsageDialogOpen) return;

        var data = _modUsagePromptData ?? PrepareModUsagePromptData();
        if (data is null || data.Candidates.Count == 0)
        {
            _modUsagePromptData = null;
            UpdateModUsagePromptIndicator(false);
            _gameSessionMonitor?.RefreshPromptState();
            return;
        }

        _modUsagePromptData = data;
        _isModUsageDialogOpen = true;

        try
        {
            var dialog = new ModUsageNoIssuesDialog(data.Candidates)
            {
                Owner = this
            };

            _ = dialog.ShowDialog();

            var candidateKeys = data.CandidateKeys;

            if (dialog.Result == ModUsageNoIssuesDialogResult.DisableTracking)
            {
                _userConfiguration.DisableModUsageTracking();
                _userConfiguration.ResetModUsageCounts(candidateKeys);
                _gameSessionMonitor?.RefreshPromptState();
                StopGameSessionMonitor();
                return;
            }

            if (dialog.Result != ModUsageNoIssuesDialogResult.SubmitVotes)
            {
                _userConfiguration.ResetModUsageCounts(candidateKeys);
                _gameSessionMonitor?.RefreshPromptState();
                return;
            }

            var selected = dialog.SelectedCandidates;
            if (selected.Count == 0)
            {
                _userConfiguration.ResetModUsageCounts(candidateKeys);
                _gameSessionMonitor?.RefreshPromptState();
                return;
            }

            _viewModel.EnableUserReportFetching();

            var successfulKeys = new List<ModUsageTrackingKey>();
            var errors = new List<string>();

            using var busyScope = _viewModel.EnterBusyScope();
            foreach (var candidate in selected)
                try
                {
                    await _viewModel
                        .SubmitUserReportVoteAsync(candidate.Mod, ModVersionVoteOption.NoIssuesSoFar, null)
                        .ConfigureAwait(true);

                    successfulKeys.Add(candidate.TrackingKey);
                }
                catch (InternetAccessDisabledException ex)
                {
                    _modActivityLoggingService.LogError("Internet access disabled during mod usage vote submission", ex);
                    errors.Add(ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    _modActivityLoggingService.LogError($"Failed to submit user report vote for {candidate.DisplayLabel}", ex);
                    errors.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}: {1}",
                        candidate.DisplayLabel,
                        ex.Message));
                }

            if (successfulKeys.Count > 0)
            {
                _userConfiguration.CompleteModUsageVotes(successfulKeys);
                StatusLogService.AppendStatus(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Submitted \"No issues\" votes for {0} mod(s).",
                        successfulKeys.Count),
                    false);
            }

            _userConfiguration.ResetModUsageCounts(candidateKeys);
            _gameSessionMonitor?.RefreshPromptState();

            if (errors.Count > 0)
            {
                var message = string.Join(Environment.NewLine, errors.Distinct(StringComparer.OrdinalIgnoreCase));
                WpfMessageBox.Show(
                    this,
                    "Some votes could not be submitted:" + Environment.NewLine + message,
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if (successfulKeys.Count > 0)
            {
                WpfMessageBox.Show(
                    this,
                    "Thanks! Your \"No issues\" votes were submitted.",
                    "Simple VS Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        finally
        {
            _isModUsageDialogOpen = false;
            _modUsagePromptData = null;
            await TryShowModUsagePromptAsync().ConfigureAwait(true);
        }
    }

}
