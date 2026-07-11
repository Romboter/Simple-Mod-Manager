namespace VintageStoryModManager.Services;

internal static class CloudOperationDialogTextBuilder
{
    internal static string BuildInitializationFailureMessage(string errorMessage)
    {
        return $"Failed to initialize cloud storage:\n{errorMessage}";
    }

    internal static string BuildActionFailureMessage(
        string actionDescription,
        string errorMessage)
    {
        return $"Failed to {actionDescription}:\n{errorMessage}";
    }

    internal static string BuildUnexpectedActionFailureMessage(
        string actionDescription,
        string errorMessage)
    {
        return $"An unexpected error occurred while attempting to {actionDescription}:\n{errorMessage}";
    }
}
