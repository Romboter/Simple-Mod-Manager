namespace VintageStoryModManager.Services;

internal static class ManagerUpdateLinkDialogTextBuilder
{
    internal static string BuildOpenModDatabasePageFailureMessage(string errorMessage)
    {
        return $"Failed to open the mod database page:\n{errorMessage}";
    }
}
