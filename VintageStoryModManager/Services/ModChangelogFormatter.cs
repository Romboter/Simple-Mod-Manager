using System.Text;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal static class ModChangelogFormatter
{
    internal static string? BuildChangelogSummary(IReadOnlyList<ModListItemViewModel.ReleaseChangelog> changelogEntries)
        {
            if (changelogEntries is not { Count: > 0 }) return null;

            var builder = new StringBuilder();

            for (var i = 0; i < changelogEntries.Count; i++)
            {
                var entry = changelogEntries[i];
                if (i > 0) builder.AppendLine();

                builder.AppendLine($"{entry.Version}:");
                builder.AppendLine(entry.Changelog);
            }

            return builder.ToString().TrimEnd();
        }
}
