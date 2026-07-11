#nullable enable

using System.Text;
using System.Windows;
using VintageStoryModManager.Views;

namespace VintageStoryModManager.Services;

internal static class ModUpdateDialogHelper
{
    internal static void ShowBulkUpdateChangelogDialog(Window owner, IReadOnlyList<ModUpdateOperationResult> results)
    {
        if (results is not { Count: > 0 }) return;

        var items = new List<BulkUpdateChangelogWindow.BulkUpdateChangelogItem>();

        foreach (var result in results)
        {
            if (!result.Success) continue;

            var fromVersion = string.IsNullOrWhiteSpace(result.OldVersion)
                ? "Unknown"
                : result.OldVersion!;
            var toVersion = string.IsNullOrWhiteSpace(result.NewVersion)
                ? "Unknown"
                : result.NewVersion!;
            var title = $"{result.Mod.DisplayName} ({fromVersion} → {toVersion})";
            var changelog = string.IsNullOrWhiteSpace(result.ChangelogSummary)
                ? "No changelog entries were provided for this update."
                : result.ChangelogSummary!;
            items.Add(new BulkUpdateChangelogWindow.BulkUpdateChangelogItem(title, changelog));
        }

        if (items.Count == 0) return;

        var dialog = new BulkUpdateChangelogWindow(items)
        {
            Owner = owner
        };

        dialog.ShowDialog();
    }

    internal static void ShowUpdateSummary(IReadOnlyList<ModUpdateOperationResult> results, bool isBulk, bool aborted)
    {
        if (results.Count == 0) return;

        var successCount = results.Count(result => result.Success);
        var failureCount = results.Count(result => !result.Success && !result.Skipped);
        var skippedCount = results.Count(result => result.Skipped);

        if (!isBulk && failureCount == 0 && skippedCount == 0) return;

        if (isBulk && failureCount == 0 && skippedCount == 0 && !aborted) return;

        var builder = new StringBuilder();
        builder.AppendLine(isBulk ? "Bulk update completed." : "Update completed.");
        if (aborted) builder.AppendLine("The operation was cancelled.");

        builder.AppendLine($"Updated: {successCount}");

        if (failureCount > 0) builder.AppendLine($"Failed: {failureCount}");

        if (skippedCount > 0) builder.AppendLine($"Skipped: {skippedCount}");

        if (failureCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Failures:");
            foreach (var failure in results.Where(result => !result.Success && !result.Skipped))
                builder.AppendLine($" • {failure.Mod.DisplayName}: {failure.Message}");
        }

        if (skippedCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Skipped:");
            foreach (var skipped in results.Where(result => result.Skipped))
                builder.AppendLine($" • {skipped.Mod.DisplayName}: {skipped.Message}");
        }

        MessageBoxImage icon;
        if (isBulk)
            icon = MessageBoxImage.None;
        else
            icon = failureCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
        ModManagerMessageBox.Show(builder.ToString(), "Simple VS Manager", MessageBoxButton.OK, icon);
    }
}
