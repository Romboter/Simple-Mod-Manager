#nullable enable
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{

    private void BeginModlistInstallUi(int totalSteps, string message)
    {
        _modlistInstallTotalSteps = Math.Max(1, totalSteps);
        _modlistInstallCompletedSteps = 0;
        ModlistInstallProgress = 0;
        ModlistInstallStatusMessage = message;
        UpdateModlistDownloadSpeed(null);
        IsModlistInstallInProgress = true;
    }

    private void UpdateModlistInstallUi(string status, double? stepPercent, double? bytesPerSecond)
    {
        if (!IsModlistInstallInProgress) return;

        var clampedPercent = ClampPercent(stepPercent);
        var progress = (_modlistInstallCompletedSteps + clampedPercent / 100d) / _modlistInstallTotalSteps * 100d;

        ModlistInstallProgress = Math.Clamp(progress, 0, 100);
        ModlistInstallStatusMessage = status;
        UpdateModlistDownloadSpeed(bytesPerSecond);
    }

    private void CompleteModlistInstallStep(string status)
    {
        if (!IsModlistInstallInProgress) return;

        _modlistInstallCompletedSteps = Math.Min(_modlistInstallCompletedSteps + 1, _modlistInstallTotalSteps);
        ModlistInstallProgress = (double)_modlistInstallCompletedSteps / _modlistInstallTotalSteps * 100d;
        ModlistInstallStatusMessage = status;
        UpdateModlistDownloadSpeed(null);
    }

    private void EndModlistInstallUi()
    {
        IsModlistInstallInProgress = false;
        ModlistInstallProgress = 0;
        ModlistInstallStatusMessage = string.Empty;
        UpdateModlistDownloadSpeed(null);
        _modlistInstallCompletedSteps = 0;
        _modlistInstallTotalSteps = 0;
    }

    private static double ClampPercent(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value)) return 0;
        return Math.Clamp(value.Value, 0, 100);
    }

    private void UpdateModlistDownloadSpeed(double? bytesPerSecond)
    {
        if (bytesPerSecond.HasValue && bytesPerSecond.Value > 0)
        {
            ModlistDownloadSpeed = ProgressDisplayFormatter.FormatDownloadSpeed(bytesPerSecond.Value);
            HasModlistDownloadSpeed = true;
            return;
        }

        ModlistDownloadSpeed = string.Empty;
        HasModlistDownloadSpeed = false;
    }

    private IProgress<ModUpdateProgress> CreateModlistInstallProgressReporter(string modDisplayName)
    {
        var display = string.IsNullOrWhiteSpace(modDisplayName) ? "Mod" : modDisplayName;

        return new Progress<ModUpdateProgress>(p =>
        {
            var status = $"{display}: {p.Message}";
            var bytesPerSecond = p.Stage == ModUpdateStage.Downloading ? p.BytesPerSecond : null;
            UpdateModlistInstallUi(status, p.Percent, bytesPerSecond);
        });
    }

    private IProgress<ModUpdateProgress> CreateModUpdateProgressReporter(string modDisplayName,
            IProgress<ModUpdateProgress>? additionalProgress = null)
    {
        var display = string.IsNullOrWhiteSpace(modDisplayName) ? "Mod" : modDisplayName;

        return new Progress<ModUpdateProgress>(p =>
        {
            additionalProgress?.Report(p);
            _viewModel?.ReportStatus($"{display}: {p.Message}");
        });
    }
}
