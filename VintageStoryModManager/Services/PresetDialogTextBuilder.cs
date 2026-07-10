#nullable enable

namespace VintageStoryModManager.Services;

internal static class PresetDialogTextBuilder
{
    internal static string BuildSaveFailureMessage(string failureContext, string? errorMessage)
    {
        return $"Failed to save the {failureContext}:\n{errorMessage}";
    }

    internal static string BuildLoadFailureMessage(string message)
    {
        return $"Failed to load the preset:\n{message}";
    }

    internal static string BuildExclusiveRemovalFailureMessage(IEnumerable<string> failures)
    {
        var lines = new List<string> { "Some mods could not be removed:" };
        lines.AddRange(failures
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(failure => $" • {failure}"));
        return string.Join(Environment.NewLine, lines).Trim();
    }
}
