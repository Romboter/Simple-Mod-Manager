#nullable enable

namespace VintageStoryModManager.Services;

internal static class CloudAuthDialogTextBuilder
{
    internal static string BuildRestoreFailureMessage(string? errorMessage)
    {
        return $"Failed to restore firebase-auth.json: {errorMessage}";
    }
}
