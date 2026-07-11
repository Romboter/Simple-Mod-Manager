namespace VintageStoryModManager.Services;

internal static class UserReportsDialogTextBuilder
{
    internal const string CannotSubmitUserReportMessage =
        "User reports are unavailable because the mod version or Vintage Story version could not be determined.";

    internal static string BuildLoadFailureMessage(string errorMessage)
    {
        return $"Failed to load user reports:\n{errorMessage}";
    }
}
