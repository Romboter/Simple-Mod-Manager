#nullable enable

namespace VintageStoryModManager.Services;

internal static class ModUsageDialogTextBuilder
{
    internal static string BuildVoteSubmissionFailureMessage(string message) => "Some votes could not be submitted:" + Environment.NewLine + message;
}
