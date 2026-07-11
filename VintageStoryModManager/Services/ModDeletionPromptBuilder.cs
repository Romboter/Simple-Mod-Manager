#nullable enable

using System.Text;

namespace VintageStoryModManager.Services;

/// <summary>
///     Builds the confirmation message shown when deleting multiple mods.
/// </summary>
internal static class ModDeletionPromptBuilder
{
    internal static string BuildConfirmationMessage(IReadOnlyList<string?> displayNames)
    {
        StringBuilder confirmationBuilder = new();
        confirmationBuilder.Append(
            $"Are you sure you want to delete {displayNames.Count} mods? This will remove them from disk.");
        confirmationBuilder.AppendLine();
        confirmationBuilder.AppendLine();

        const int maxListedMods = 10;
        var listedCount = 0;
        foreach (var name in displayNames)
        {
            if (listedCount >= maxListedMods) break;

            confirmationBuilder.AppendLine($"• {name}");
            listedCount++;
        }

        if (displayNames.Count > maxListedMods) confirmationBuilder.AppendLine("• …");

        return confirmationBuilder.ToString();
    }
}
